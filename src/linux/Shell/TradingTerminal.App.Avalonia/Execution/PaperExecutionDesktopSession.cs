using System.Globalization;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.Infrastructure.Execution;
using TradingTerminal.UI.Execution;

namespace TradingTerminal.App.Avalonia.Execution;

/// <summary>
/// One app-lifetime, simulation-only execution account. This is deliberately a composition object:
/// it selects the Mac ledger path and constructs exact ticket/flatten requests, while all order
/// behavior remains in the portable runtime, service, OMS, and client.
/// </summary>
public sealed class PaperExecutionDesktopSession :
    IPaperExecutionOrderFactory,
    IPaperExecutionFlattenOrderFactory,
    IPaperStrategyTargetOrderFactory,
    IDisposable
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LeaseRenewalInterval = TimeSpan.FromMinutes(1);
    private static readonly ExecutionResource DefaultPaperResource = new(
        new VenueId("paper-simulator"),
        new TradingAccountId("local-paper"),
        ExecutionEnvironment.SimulatedPaper);

    private readonly ExecutionResource _resource;
    private readonly IClock _clock;
    private readonly IInstrumentRegistry _registry;
    private readonly Dictionary<InstrumentId, ScaledPrice> _lastMarks = [];
    private readonly object _lifetimeGate = new();
    private readonly Timer _leaseRenewalTimer;
    private readonly CancellationTokenSource _ipcStop;
    private readonly string _ipcSocketDirectory;
    private readonly ExecutionUnixSocketServer _ipcServer;
    private readonly Task _ipcServerTask;
    private readonly ExecutionUnixSocketClientEndpoint _ipcEndpoint;
    private long _sequence;
    private bool _disposed;

    public PaperExecutionDesktopSession(IClock clock, IInstrumentRegistry registry)
        : this(
            "local-paper",
            "Local Paper",
            DefaultPaperResource,
            clock,
            registry,
            LedgerPath,
            new MacExecutionServiceSecretStore())
    {
    }

    /// <summary>Creates the same Paper-only desktop composition at an explicitly isolated ledger.</summary>
    public static PaperExecutionDesktopSession CreateForLedger(
        string databasePath,
        IClock clock,
        IInstrumentRegistry registry,
        IExecutionServiceSecretStore secretStore) =>
        new("local-paper", "Local Paper", DefaultPaperResource, clock, registry, databasePath, secretStore);

    /// <summary>Creates one book-scoped Paper account with its own ledger and writer resource.</summary>
    public static PaperExecutionDesktopSession CreateForBook(
        string bookId,
        string bookName,
        ExecutionResource resource,
        string databasePath,
        IClock clock,
        IInstrumentRegistry registry,
        IExecutionServiceSecretStore secretStore,
        ScaledMoney openingBalance) =>
        new(bookId, bookName, resource, clock, registry, databasePath, secretStore, openingBalance);

    private PaperExecutionDesktopSession(
        string bookId,
        string bookName,
        ExecutionResource resource,
        IClock clock,
        IInstrumentRegistry registry,
        string databasePath,
        IExecutionServiceSecretStore secretStore,
        ScaledMoney? openingBalance = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookId);
        ArgumentException.ThrowIfNullOrWhiteSpace(bookName);
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (!resource.IsValid || resource.Environment != ExecutionEnvironment.SimulatedPaper)
            throw new ArgumentException("A desktop Paper book requires one valid SimulatedPaper resource.", nameof(resource));
        BookId = bookId.Trim();
        BookName = bookName.Trim();
        _resource = resource;
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        Instruments = LoadInstruments(registry);

        var owner = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        Runtime = PaperExecutionServiceRuntime.Create(
            databasePath,
            _resource,
            clock,
            new ExecutionLeaseId($"desktop-paper-{owner}"),
            new RuntimeInstanceId($"desktop-{owner}"),
            LeaseDuration);
        _sequence = RecoverSequence(Runtime.Ledger.ReadOutbox());
        var socketDirectory = Path.Combine("/private/tmp", $"dax-exec-{owner[..12]}");
        var socketPath = Path.Combine(socketDirectory, "paper.sock");
        var stop = new CancellationTokenSource();
        ExecutionUnixSocketServer? server = null;
        Task? serverTask = null;
        ExecutionUnixSocketClientEndpoint? endpoint = null;
        try
        {
            server = new ExecutionUnixSocketServer(Runtime.Service, secretStore, socketPath);
            serverTask = server.RunAsync(stop.Token);
            endpoint = ExecutionUnixSocketClientEndpoint.ConnectAsync(
                    socketPath,
                    secretStore,
                    Runtime.Service.Resource,
                    Runtime.LeaseGrant)
                .GetAwaiter().GetResult();
        }
        catch
        {
            stop.Cancel();
            if (serverTask is not null)
            {
                try { serverTask.GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { }
            }
            if (endpoint is not null) endpoint.DisposeAsync().AsTask().GetAwaiter().GetResult();
            if (server is not null) server.DisposeAsync().AsTask().GetAwaiter().GetResult();
            TryDeleteSocketDirectory(socketDirectory);
            stop.Dispose();
            Runtime.Dispose();
            throw;
        }
        _ipcStop = stop;
        _ipcSocketDirectory = socketDirectory;
        _ipcServer = server;
        _ipcServerTask = serverTask;
        _ipcEndpoint = endpoint;
        Client = new PaperExecutionClient(
            _ipcEndpoint,
            clock,
            this,
            openingBalance ?? ExecutionNumericBoundary.MoneyFromDecimal(100_000m),
            CurrentMark);
        _leaseRenewalTimer = new Timer(
            RenewLease,
            null,
            LeaseRenewalInterval,
            LeaseRenewalInterval);
    }

    public static string LedgerPath
    {
        get
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Path.Combine(Path.GetTempPath(), "DaxAlgoTerminal");
            var directory = Path.Combine(root, "DaxAlgoTerminal", "execution", "paper", "local-paper");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "orders.db");
        }
    }

    public PaperExecutionServiceRuntime Runtime { get; }
    public PaperExecutionClient Client { get; }
    public IReadOnlyList<PaperExecutionInstrumentChoice> Instruments { get; }
    public string BookId { get; }
    public string BookName { get; }
    public ExecutionResource Resource => _resource;

    private ScaledPrice? CurrentMark(InstrumentId instrumentId) =>
        _lastMarks.TryGetValue(instrumentId, out var mark) ? mark : null;

    public bool TryCreateSubmit(
        PaperOrderTicketDraft draft,
        PaperExecutionClientSnapshot snapshot,
        out ExecutionSubmitRequest? request,
        out string? reason)
    {
        request = null;
        if (!TryCreateTerms(draft, out var terms, out var mark, out reason) || terms is null)
            return false;
        if (snapshot.Resource != _resource || !snapshot.LeaseHeld)
            return Fail("The local Paper writer lease is not current.", out reason);

        ObserveMarket(draft.Instrument.InstrumentId, mark);
        var sequence = NextSequence();
        var metadata = Metadata(draft.Instrument.InstrumentId, sequence, "manual-ticket", 0);
        var clientOrderId = new ClientOrderId($"paper-client-{sequence}");
        var currentPosition = Position(snapshot, draft.Instrument.InstrumentId);
        var signedDelta = terms.Side == OrderSide.Buy
            ? terms.Quantity
            : new ScaledQuantity(checked(-terms.Quantity.Coefficient), terms.Quantity.Scale);
        var mapping = new CanonicalInstructionMappingContext(
            new IntentId($"paper-intent-{sequence}"),
            null,
            new LegId($"paper-leg-{sequence}"),
            Runtime.LeaseGrant.Claim.LeaseId,
            Runtime.LeaseGrant.Claim.FencingToken,
            TradeIntentQuantityMode.Delta,
            signedDelta,
            currentPosition,
            null,
            null,
            ScaledMoney.Zero,
            sequence,
            "desktop-paper-manual-v1",
            terms.LimitPrice,
            terms.StopPrice);
        var fault = CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            clientOrderId,
            terms,
            mapping,
            out var instruction);
        if (fault != OrderDomainFault.None || instruction is null)
            return Fail($"Canonical Paper instruction was rejected: {fault}.", out reason);

        var command = new SubmitOrderCommand(
            metadata,
            new OrderId($"paper-order-{sequence}"),
            clientOrderId,
            terms,
            instruction);
        request = new ExecutionSubmitRequest(command, Risk(draft.Instrument.InstrumentId, mark, snapshot));
        reason = null;
        return true;
    }

    public bool TryCreateReplacementTerms(
        PaperOrderTicketDraft draft,
        out OrderTerms? terms,
        out string? reason)
    {
        if (!TryCreateTerms(draft, out terms, out var mark, out reason)) return false;
        ObserveMarket(draft.Instrument.InstrumentId, mark);
        return true;
    }

    public RiskEvaluationContext CreateReplacementRisk(
        PaperOrderTicketDraft draft,
        OmsOrderProjection order,
        PaperExecutionClientSnapshot snapshot)
    {
        if (!TryMark(draft.MarkPrice, out var mark))
            throw new ArgumentException("A positive exact mark price is required.", nameof(draft));
        var baseRisk = Risk(draft.Instrument.InstrumentId, mark, snapshot);
        var signedReservation = order.Terms.Side == OrderSide.Buy
            ? order.Terms.Quantity
            : new ScaledQuantity(checked(-order.Terms.Quantity.Coefficient), order.Terms.Quantity.Scale);
        var gross = Multiply(order.Terms.Quantity, mark);
        return new RiskEvaluationContext(
            baseRisk.Limits,
            baseRisk.ControlMode,
            baseRisk.KillSwitchActive,
            baseRisk.CurrentPositionQuantity,
            baseRisk.CurrentBuyReservedQuantity,
            baseRisk.CurrentSellReservedQuantity,
            baseRisk.CurrentGrossReservedNotional,
            signedReservation,
            gross,
            order.FilledQuantity,
            baseRisk.AvailableBuyingPower,
            baseRisk.DailyNetRealizedPnl,
            baseRisk.CurrentEquity,
            baseRisk.PeakEquity,
            mark,
            baseRisk.ExposureCommandsInWindow,
            UtcNow(),
            baseRisk.ContractMultiplier,
            baseRisk.AccountCurrency,
            baseRisk.HasUnrepresentableMarketEconomics);
    }

    public bool TryCreateTargetSubmit(
        TradeIntent intent,
        ScaledPrice marketPrice,
        PaperExecutionClientSnapshot snapshot,
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        out ExecutionSubmitRequest? request,
        out string? reason)
    {
        request = null;
        reason = null;
        if (intent.QuantityMode != TradeIntentQuantityMode.TargetPosition ||
            intent.Instrument.IsNone || !intent.SignedUnits.IsValid)
        {
            return Fail("The strategy target is not a valid exact TargetPosition intent.", out reason);
        }
        if (strategyId.IsEmpty || strategyVersion.IsEmpty ||
            !string.Equals(intent.StrategyId, strategyId.Value, StringComparison.Ordinal))
        {
            return Fail("The strategy target provenance does not match the selected Paper runner.", out reason);
        }
        if (_registry.Get(intent.Instrument) is null)
            return Fail("The strategy target instrument is absent from the canonical registry.", out reason);
        if (!marketPrice.IsValid || marketPrice.Coefficient <= 0)
            return Fail("The strategy target requires a positive exact Paper reference price.", out reason);
        if (snapshot.Resource != _resource || !snapshot.AdmissionOpen)
            return Fail("The local Paper writer lease or order-intake gate is not open.", out reason);

        var currentPosition = Position(snapshot, intent.Instrument);
        var active = snapshot.Orders
            .Where(order =>
                order.Instruction.TradeIntent.Instrument == intent.Instrument &&
                !OrderLifecycle.IsTerminal(order.State))
            .ToArray();
        var current = ExecutionNumericBoundary.ToDecimal(currentPosition);
        var target = ExecutionNumericBoundary.ToDecimal(intent.SignedUnits);
        decimal reservation = 0m;
        foreach (var order in active)
        {
            var remaining = checked(
                ExecutionNumericBoundary.ToDecimal(order.Terms.Quantity) -
                ExecutionNumericBoundary.ToDecimal(order.FilledQuantity));
            if (remaining < 0m)
                return Fail("A nonterminal Paper order is overfilled; reconcile before retargeting.", out reason);
            reservation = checked(reservation +
                (order.Terms.Side == OrderSide.Buy ? remaining : -remaining));
        }

        if (active.Length != 0)
        {
            return current + reservation == target
                ? NoOrder("A nonterminal Paper order is already converging to this strategy target.", out reason)
                : Fail(
                    "A different Paper order remains nonterminal for this asset; cancel or reconcile it before retargeting.",
                    out reason);
        }
        if (current == target)
            return NoOrder("The verified Paper position already matches this strategy target.", out reason);

        var delta = checked(target - current);
        if (!ExecutionNumericBoundary.TryQuantityFromDecimal(decimal.Abs(delta), out var quantity) ||
            quantity.Coefficient <= 0)
        {
            return Fail("The exact strategy target delta cannot be represented.", out reason);
        }

        var sequence = NextSequence();
        var metadata = new ExecutionCommandMetadata(
            new CommandId($"paper-strategy-command-{sequence}"),
            new CorrelationId($"paper-strategy-correlation-{sequence}"),
            new CausationId($"paper-strategy-cause-{sequence}"),
            _resource.TradingAccountId,
            strategyId,
            strategyVersion,
            _resource.VenueId,
            intent.Instrument,
            _resource.Environment,
            UtcNow(),
            expectedOrderSequence: 0);
        var terms = new OrderTerms(
            delta > 0m ? OrderSide.Buy : OrderSide.Sell,
            EntryOrderType(intent),
            quantity,
            intent.EntryLimitPrice,
            intent.EntryStopPrice,
            TimeInForce.Day,
            reduceOnly: IsNonCrossingReduction(current, target));
        var clientOrderId = new ClientOrderId($"paper-strategy-client-{sequence}");
        var mapping = new CanonicalInstructionMappingContext(
            new IntentId($"paper-strategy-intent-{sequence}"),
            null,
            new LegId($"paper-strategy-leg-{sequence}"),
            snapshot.ExecutionLeaseId,
            snapshot.FencingToken,
            TradeIntentQuantityMode.TargetPosition,
            intent.SignedUnits,
            currentPosition,
            intent.ProtectiveStopPrice,
            intent.ProfitTargetPrice,
            intent.EstimatedRoundTripCostPerUnit,
            intent.StrategyNoteId,
            intent.PolicyVersion,
            intent.EntryLimitPrice,
            intent.EntryStopPrice);
        var fault = CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            clientOrderId,
            terms,
            mapping,
            out var instruction);
        if (fault != OrderDomainFault.None || instruction is null)
            return Fail($"The canonical strategy instruction was rejected: {fault}.", out reason);

        ObserveMarket(intent.Instrument, marketPrice);
        request = new ExecutionSubmitRequest(
            new SubmitOrderCommand(
                metadata,
                new OrderId($"paper-strategy-order-{sequence}"),
                clientOrderId,
                terms,
                instruction),
            Risk(intent.Instrument, marketPrice, snapshot));
        return true;
    }

    public bool TryCreateFlattenOrder(
        ReconciliationPositionSnapshot position,
        ExecutionLeaseGrant leaseGrant,
        DateTimeOffset createdAtUtc,
        out ExecutionSubmitRequest? request,
        out string? reason)
    {
        request = null;
        reason = null;
        if (position.Quantity.Coefficient == 0)
            return Fail("The position is already flat.", out reason);
        if (!TryResolveMark(position.InstrumentId, out var mark))
            return Fail("No exact Paper mark or prior fill is available for this position.", out reason);

        ObserveMarket(position.InstrumentId, mark);
        var sequence = NextSequence();
        var side = position.Quantity.Coefficient > 0 ? OrderSide.Sell : OrderSide.Buy;
        var absolute = new ScaledQuantity(checked(Math.Abs(position.Quantity.Coefficient)), position.Quantity.Scale);
        var terms = new OrderTerms(side, OrderType.Market, absolute, reduceOnly: true);
        var metadata = Metadata(position.InstrumentId, sequence, "system-kill", 0, createdAtUtc);
        var clientOrderId = new ClientOrderId($"paper-kill-client-{sequence}");
        var mapping = new CanonicalInstructionMappingContext(
            new IntentId($"paper-kill-intent-{sequence}"),
            null,
            new LegId($"paper-kill-leg-{sequence}"),
            leaseGrant.Claim.LeaseId,
            leaseGrant.Claim.FencingToken,
            TradeIntentQuantityMode.Delta,
            new ScaledQuantity(checked(-position.Quantity.Coefficient), position.Quantity.Scale),
            position.Quantity,
            null,
            null,
            ScaledMoney.Zero,
            sequence,
            "desktop-paper-kill-v1");
        var fault = CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            clientOrderId,
            terms,
            mapping,
            out var instruction);
        if (fault != OrderDomainFault.None || instruction is null)
            return Fail($"Canonical flatten instruction was rejected: {fault}.", out reason);

        var snapshot = Client.GetSnapshot();
        request = new ExecutionSubmitRequest(
            new SubmitOrderCommand(
                metadata,
                new OrderId($"paper-kill-order-{sequence}"),
                clientOrderId,
                terms,
                instruction),
            Risk(position.InstrumentId, mark, snapshot, reduceOnly: true));
        return true;
    }

    private bool TryCreateTerms(
        PaperOrderTicketDraft draft,
        out OrderTerms? terms,
        out ScaledPrice mark,
        out string? reason)
    {
        terms = null;
        mark = default;
        reason = null;
        if (draft.Instrument.InstrumentId.IsNone || _registry.Get(draft.Instrument.InstrumentId) is null)
            return Fail("Select a canonical instrument from the Paper ticket.", out reason);
        if (!decimal.TryParse(draft.Quantity, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantityDecimal) ||
            quantityDecimal <= 0 ||
            !ExecutionNumericBoundary.TryQuantityFromDecimal(quantityDecimal, out var quantity))
            return Fail("Quantity must be a positive exact decimal.", out reason);
        if (!TryMark(draft.MarkPrice, out mark))
            return Fail("Mark price must be a positive exact decimal.", out reason);

        ScaledPrice? limit = null;
        ScaledPrice? stop = null;
        if (draft.OrderType is OrderType.Limit or OrderType.StopLimit)
        {
            if (!TryMark(draft.LimitPrice, out var parsed))
                return Fail("This order type requires a positive exact limit price.", out reason);
            limit = parsed;
        }
        if (draft.OrderType is OrderType.Stop or OrderType.StopLimit)
        {
            if (!TryMark(draft.StopPrice, out var parsed))
                return Fail("This order type requires a positive exact stop price.", out reason);
            stop = parsed;
        }
        try
        {
            terms = new OrderTerms(
                draft.Side,
                draft.OrderType,
                quantity,
                limit,
                stop,
                draft.TimeInForce,
                draft.ReduceOnly);
            return true;
        }
        catch (ArgumentException exception)
        {
            return Fail(exception.Message, out reason);
        }
    }

    private RiskEvaluationContext Risk(
        InstrumentId instrumentId,
        ScaledPrice marketPrice,
        PaperExecutionClientSnapshot snapshot,
        bool reduceOnly = false)
    {
        var currentPosition = Position(snapshot, instrumentId);
        var open = snapshot.Orders.Where(item =>
            item.Instruction.TradeIntent.Instrument == instrumentId &&
            item.State is OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled).ToArray();
        decimal buys = 0m;
        decimal sells = 0m;
        decimal gross = 0m;
        foreach (var order in open)
        {
            var remaining = ExecutionNumericBoundary.ToDecimal(order.Terms.Quantity) -
                            ExecutionNumericBoundary.ToDecimal(order.FilledQuantity);
            if (order.Terms.Side == OrderSide.Buy) buys = checked(buys + remaining);
            else sells = checked(sells + remaining);
            gross = checked(gross + remaining * ExecutionNumericBoundary.ToDecimal(marketPrice));
        }

        var limits = new RiskLimits(
            ExecutionNumericBoundary.QuantityFromDecimal(1_000_000m),
            ExecutionNumericBoundary.QuantityFromDecimal(1_000_000m),
            ExecutionNumericBoundary.MoneyFromDecimal(1_000_000_000m),
            ScaledMoney.Zero,
            ExecutionNumericBoundary.MoneyFromDecimal(10_000_000m),
            ExecutionNumericBoundary.MoneyFromDecimal(10_000_000m),
            10_000,
            TimeSpan.FromMinutes(1));
        var analytics = snapshot.PortfolioAnalytics;
        var availableBuyingPower = analytics is null
            ? 100_000m
            : Math.Max(0m, analytics.CurrentCash);
        var dailyRealized = analytics?.Period(PaperExecutionTimeRange.SevenDays)
            .DailyProfitAndLossSeries.LastOrDefault()?.RealizedProfitAndLoss ?? 0m;
        var currentEquity = analytics?.MarkedEquity ?? 100_000m;
        var peakEquity = analytics is null
            ? currentEquity
            : Math.Max(
                currentEquity,
                analytics.Periods.SelectMany(period => period.EquitySeries)
                    .Select(point => point.Equity)
                    .Append(analytics.OpeningBalance)
                    .Max());
        return new RiskEvaluationContext(
            limits,
            RiskControlMode.Active,
            false,
            currentPosition,
            ExecutionNumericBoundary.QuantityFromDecimal(buys),
            ExecutionNumericBoundary.QuantityFromDecimal(sells),
            ExecutionNumericBoundary.MoneyFromDecimal(gross),
            ScaledQuantity.Zero,
            ScaledMoney.Zero,
            ScaledQuantity.Zero,
            ExecutionNumericBoundary.MoneyFromDecimal(availableBuyingPower),
            ExecutionNumericBoundary.MoneyFromDecimal(dailyRealized),
            ExecutionNumericBoundary.MoneyFromDecimal(currentEquity),
            ExecutionNumericBoundary.MoneyFromDecimal(peakEquity),
            marketPrice,
            0,
            UtcNow(),
            ContractMultiplier(instrumentId),
            _registry.Get(instrumentId)?.Currency ?? "USD");
    }

    private ExecutionCommandMetadata Metadata(
        InstrumentId instrumentId,
        long sequence,
        string strategy,
        long expectedSequence,
        DateTimeOffset? createdAt = null) =>
        new(
            new CommandId($"paper-command-{sequence}"),
            new CorrelationId($"paper-correlation-{sequence}"),
            new CausationId($"paper-cause-{sequence}"),
            _resource.TradingAccountId,
            new StrategyId(strategy),
            new StrategyVersion("1.0.0"),
            _resource.VenueId,
            instrumentId,
            _resource.Environment,
            createdAt ?? UtcNow(),
            expectedSequence);

    private void ObserveMarket(InstrumentId instrumentId, ScaledPrice mark)
    {
        _lastMarks[instrumentId] = mark;
        Runtime.Venue.OnMarket(new PaperMarketSnapshot(
            instrumentId,
            mark,
            mark,
            ExecutionNumericBoundary.QuantityFromDecimal(1_000_000m),
            UtcNow()));
    }

    private bool TryResolveMark(InstrumentId instrumentId, out ScaledPrice mark)
    {
        if (_lastMarks.TryGetValue(instrumentId, out mark)) return true;
        var latest = Runtime.Ledger.ReadOutbox()
            .Select(item => item.Event)
            .Where(item => item.Fill is not null)
            .OrderByDescending(item => item.RecordedAtUtc)
            .FirstOrDefault(item => Runtime.Ledger.ReadProjection(item.AggregateId)?.Instruction.TradeIntent.Instrument == instrumentId);
        if (latest?.Fill is null) return false;
        mark = latest.Fill.Price;
        return true;
    }

    private ScaledRatio ContractMultiplier(InstrumentId instrumentId)
    {
        var value = _registry.Get(instrumentId)?.Multiplier ?? 1d;
        try { return ExecutionNumericBoundary.RatioFromDecimal(Convert.ToDecimal(value, CultureInfo.InvariantCulture)); }
        catch { return new ScaledRatio(1, 0); }
    }

    private static ScaledQuantity Position(PaperExecutionClientSnapshot snapshot, InstrumentId instrumentId) =>
        snapshot.Economics.Positions.FirstOrDefault(item => item.InstrumentId == instrumentId)?.Quantity ??
        ScaledQuantity.Zero;

    private static OrderType EntryOrderType(TradeIntent intent) =>
        (intent.EntryLimitPrice.HasValue, intent.EntryStopPrice.HasValue) switch
        {
            (false, false) => OrderType.Market,
            (true, false) => OrderType.Limit,
            (false, true) => OrderType.Stop,
            (true, true) => OrderType.StopLimit,
        };

    private static bool IsNonCrossingReduction(decimal current, decimal target) =>
        current > 0m && target >= 0m && target < current ||
        current < 0m && target <= 0m && target > current;

    private static ScaledMoney Multiply(ScaledQuantity quantity, ScaledPrice price)
    {
        var value = checked(ExecutionNumericBoundary.ToDecimal(quantity) * ExecutionNumericBoundary.ToDecimal(price));
        return ExecutionNumericBoundary.MoneyFromDecimal(value);
    }

    private static bool TryMark(string? text, out ScaledPrice price)
    {
        price = default;
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) &&
               value > 0 && ExecutionNumericBoundary.TryPriceFromDecimal(value, out price);
    }

    private static IReadOnlyList<PaperExecutionInstrumentChoice> LoadInstruments(IInstrumentRegistry registry)
    {
        if (registry.All().Count == 0)
        {
            registry.ResolveOrCreate(Contract.UsStock("SPY", "ARCA"), BrokerKind.Simulated);
            registry.ResolveOrCreate(Contract.UsStock("QQQ", "NASDAQ"), BrokerKind.Simulated);
        }
        return Array.AsReadOnly(registry.All()
            .Where(item => !item.Id.IsNone)
            .OrderBy(item => item.CanonicalSymbol, StringComparer.OrdinalIgnoreCase)
            .Take(256)
            .Select(item => new PaperExecutionInstrumentChoice(
                item.Id,
                item.CanonicalSymbol,
                item.AssetClass.ToString(),
                item.Exchange,
                item.Currency))
            .ToArray());
    }

    private static long RecoverSequence(IReadOnlyList<OrderEventOutboxEntry> outbox)
    {
        var maximum = 0L;
        foreach (var aggregateId in outbox.Select(item => item.Event.AggregateId.Value).Distinct(StringComparer.Ordinal))
        {
            var prefixLength = aggregateId.StartsWith("paper-client-", StringComparison.Ordinal)
                ? "paper-client-".Length
                : aggregateId.StartsWith("paper-strategy-client-", StringComparison.Ordinal)
                    ? "paper-strategy-client-".Length
                : aggregateId.StartsWith("paper-kill-client-", StringComparison.Ordinal)
                    ? "paper-kill-client-".Length
                    : -1;
            if (prefixLength >= 0 &&
                long.TryParse(
                    aggregateId.AsSpan(prefixLength),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var sequence))
            {
                maximum = Math.Max(maximum, sequence);
            }
        }
        return maximum;
    }

    private long NextSequence() => Interlocked.Increment(ref _sequence);

    private void RenewLease(object? state)
    {
        lock (_lifetimeGate)
        {
            if (_disposed) return;
            Runtime.RenewLease(LeaseDuration);
        }
    }

    private DateTimeOffset UtcNow()
    {
        var value = _clock.UtcNow;
        if (value.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("The Paper desktop clock must return UTC.");
        return new DateTimeOffset(value);
    }

    private static bool Fail(string message, out string? reason)
    {
        reason = message;
        return false;
    }

    private static bool NoOrder(string message, out string? reason)
    {
        reason = message;
        return true;
    }

    public void Dispose()
    {
        lock (_lifetimeGate)
        {
            if (_disposed) return;
            _disposed = true;
            _leaseRenewalTimer.Dispose();
            Client.Dispose();
            _ipcEndpoint.DisposeAsync().AsTask().GetAwaiter().GetResult();
            _ipcStop.Cancel();
            try { _ipcServerTask.GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { }
            _ipcServer.DisposeAsync().AsTask().GetAwaiter().GetResult();
            TryDeleteSocketDirectory(_ipcSocketDirectory);
            _ipcStop.Dispose();
            Runtime.Dispose();
        }
    }

    private static void TryDeleteSocketDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }
        catch
        {
            // The socket itself is already closed. Refuse broad or recursive cleanup here.
        }
    }
}
