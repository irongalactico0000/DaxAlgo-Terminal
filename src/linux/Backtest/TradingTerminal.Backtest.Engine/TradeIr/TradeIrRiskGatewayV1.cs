using System.Collections.ObjectModel;
using TradingTerminal.Backtest.Engine.Accounting;
using TradingTerminal.Backtest.Engine.Execution;
using TradingTerminal.Core.Backtesting;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Strategies.Definition;
using TradingTerminal.Core.Trading;
using TradingTerminal.TradeIr.Runtime;

namespace TradingTerminal.Backtest.Engine.TradeIr;

/// <summary>
/// Product-owned risk settings for the closed TradeIR backtest lane. Strategy definitions and
/// evaluator outputs cannot supply or override these values.
/// </summary>
internal sealed record BacktestTradeIrHostPolicyV1(
    string ProfileId,
    TradingAccountId AccountId,
    VenueId VenueId,
    RiskLimits Limits,
    RiskControlMode ControlMode,
    bool KillSwitchActive);

internal static class TradeIrGatewayDecisionCodesV1
{
    public const string Allowed = "TRADEIR_INTENT_ALLOWED";
    public const string DefinitionMismatch = "TRADEIR_INTENT_DEFINITION_MISMATCH";
    public const string AdmissionManifestMismatch = "TRADEIR_INTENT_ADMISSION_MANIFEST_MISMATCH";
    public const string OutputMismatch = "TRADEIR_INTENT_OUTPUT_MISMATCH";
    public const string InstrumentMismatch = "TRADEIR_INTENT_INSTRUMENT_MISMATCH";
    public const string SourceSequenceMismatch = "TRADEIR_INTENT_SOURCE_SEQUENCE_MISMATCH";
    public const string SequenceMismatch = "TRADEIR_INTENT_SEQUENCE_MISMATCH";
    public const string EventTimeMismatch = "TRADEIR_INTENT_EVENT_TIME_MISMATCH";
    public const string DeltaMismatch = "TRADEIR_INTENT_DELTA_MISMATCH";
    public const string RiskDenied = "TRADEIR_INTENT_RISK_DENIED";
}

/// <summary>
/// Append-only in-run evidence. The gateway appends this record before it can call the simulated
/// order book, preserving the same decision-before-effect ordering required of a durable host.
/// </summary>
internal sealed record TradeIrGatewayDecisionTraceV1(
    long TraceSequence,
    long IntentSequence,
    bool IsAllowed,
    string Code,
    string Reason,
    string? CommandPayloadHashSha256,
    RiskDecision? RiskDecision,
    RiskPolicyEvidence? PolicyEvidence,
    DateTimeOffset EvaluatedAtUtc);

internal sealed record TradeIrGatewayAdmissionV1(
    TradeIrGatewayDecisionTraceV1 Decision,
    SubmitOrderCommand? Command,
    OrderResult? Submission)
{
    public bool IsAdmitted => Decision.IsAllowed && Command is not null && Submission is not null;
}

/// <summary>
/// Mandatory effect boundary for the new TradeIR backtest lane. It owns the only reference to the
/// simulated order book, derives command authority from host state, calls <see cref="RiskPolicy"/>,
/// records the verdict before submission, and returns terminal denied feedback without touching
/// the book when any lineage, delta, or policy check fails.
/// </summary>
internal sealed class TradeIrRiskGatewayV1 : IDisposable
{
    private readonly StrategyIntermediateRepresentationV1 _definition;
    private readonly CompiledTradeIrPlanV1 _plan;
    private readonly InstrumentSpec _instrument;
    private readonly BacktestTradeIrHostPolicyV1 _policy;
    private readonly SimulatedOrderBook _book;
    private readonly Portfolio _portfolio;
    private readonly SimClock _clock;
    private readonly IDisposable _bookTransitionBinding;
    private readonly List<TradeIrGatewayDecisionTraceV1> _decisions = [];
    private readonly ReadOnlyCollection<TradeIrGatewayDecisionTraceV1> _readOnlyDecisions;
    private readonly Queue<TradeIrOrderFeedbackV1> _feedback = [];
    private readonly Dictionary<string, PendingOrder> _pending = new(StringComparer.Ordinal);
    private readonly Queue<DateTimeOffset> _exposureAttempts = [];
    private long _lastIntentSequence;
    private long _traceSequence;
    private long? _lastQuoteTimeUnixMicroseconds;
    private long? _lastQuoteSourceSequence;
    private decimal _lastMarketPrice;
    private decimal _peakEquity;
    private DateOnly? _riskDay;
    private decimal _realizedPnlAtDayStart;
    private bool _disposed;

    public TradeIrRiskGatewayV1(
        StrategyCompilationAdmissionManifestV1 admissionManifest,
        CompiledTradeIrPlanV1 plan,
        InstrumentSpec instrument,
        BacktestTradeIrHostPolicyV1 policy,
        SimulatedOrderBook book,
        Portfolio portfolio,
        SimClock clock)
    {
        ArgumentNullException.ThrowIfNull(admissionManifest);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(instrument);
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(policy.Limits);
        ArgumentNullException.ThrowIfNull(book);
        ArgumentNullException.ThrowIfNull(portfolio);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentException.ThrowIfNullOrWhiteSpace(policy.ProfileId);
        if (policy.AccountId.IsEmpty)
            throw new ArgumentException("TradeIR host policy requires a non-empty trading account id.", nameof(policy));
        if (policy.VenueId.IsEmpty)
            throw new ArgumentException("TradeIR host policy requires a non-empty venue id.", nameof(policy));
        if (instrument.Id.Value <= 0) throw new ArgumentOutOfRangeException(nameof(instrument));
        ValidateContractMultiplier(instrument.ContractMultiplier);
        if (!Enum.IsDefined(policy.ControlMode)) throw new ArgumentOutOfRangeException(nameof(policy));
        if (!StringComparer.Ordinal.Equals(
                admissionManifest.ManifestHashSha256,
                plan.AdmissionManifestSha256))
            throw new ArgumentException("Compiled plan does not identify the supplied admission manifest.", nameof(plan));
        var frozenDefinition = admissionManifest.ReadDefinitionForCompilation();
        if (!StringComparer.Ordinal.Equals(
                StrategyIrCanonicalJsonV1.Hash(frozenDefinition),
                plan.DefinitionSha256))
            throw new ArgumentException(
                "Admission manifest does not freeze the compiled definition.",
                nameof(admissionManifest));

        var portableInstrument = ResolvePortableInstrument(frozenDefinition, plan.InstrumentKey);
        if (!MatchesHostInstrument(portableInstrument, instrument.Contract))
        {
            throw new ArgumentException(
                $"Host instrument '{Describe(instrument.Contract)}' does not exactly resolve portable instrument " +
                $"'{Describe(portableInstrument)}'.",
                nameof(instrument));
        }

        _definition = frozenDefinition;
        _plan = plan;
        _instrument = instrument;
        _policy = policy;
        _book = book;
        _portfolio = portfolio;
        _clock = clock;
        _readOnlyDecisions = _decisions.AsReadOnly();
        _peakEquity = RequireFiniteDecimal(portfolio.Equity(), "portfolio equity");
        _bookTransitionBinding = _book.BindRequiredTransitionSink(OnBookEvent);
    }

    public IReadOnlyList<TradeIrGatewayDecisionTraceV1> Decisions => _readOnlyDecisions;

    public int SubmittedOrderCount { get; private set; }

    /// <summary>
    /// Advances only host-owned market/accounting state. The evaluator receives a separate inert
    /// frame and never obtains the book or portfolio references held here.
    /// </summary>
    public void ObserveQuote(TradingTerminal.Core.Domain.Tick quote, long sourceSequence)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(quote);
        if (sourceSequence < 0) throw new ArgumentOutOfRangeException(nameof(sourceSequence));
        if (quote.TimestampUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("TradeIR quote time must be UTC.", nameof(quote));
        if (!double.IsFinite(quote.Bid) || !double.IsFinite(quote.Ask) || quote.Bid <= 0d || quote.Ask < quote.Bid)
            throw new ArgumentOutOfRangeException(nameof(quote), "TradeIR quotes require finite positive non-crossed prices.");

        var observedAt = new DateTimeOffset(quote.TimestampUtc);
        var quoteTimeUnixMicroseconds = ToUnixMicroseconds(observedAt);
        _ = RequireFiniteDecimal(quote.Bid, "bid price");
        var marketPrice = RequireFiniteDecimal(quote.Ask, "ask price");
        var mid = (quote.Bid * 0.5d) + (quote.Ask * 0.5d);
        _ = RequireFiniteDecimal(mid, "mid price");
        if (_lastQuoteTimeUnixMicroseconds is { } previousQuoteTime &&
            quoteTimeUnixMicroseconds < previousQuoteTime)
        {
            throw new InvalidOperationException(
                "TradeIR quote event time cannot move backward before host state can advance.");
        }
        if (_lastQuoteSourceSequence is { } previousSourceSequence &&
            sourceSequence <= previousSourceSequence)
        {
            throw new InvalidOperationException(
                "TradeIR quote source sequence must increase strictly before host state can advance.");
        }

        _clock.SetTo(quote.TimestampUtc);
        AdvanceRiskDay(observedAt);
        _book.OnQuote(_instrument.Id, quote);
        _portfolio.OnMark(_instrument.Id, mid);
        // Reserve market orders at the adverse L1 touch rather than the mid used for marks.
        _lastMarketPrice = marketPrice;
        _lastQuoteTimeUnixMicroseconds = quoteTimeUnixMicroseconds;
        _lastQuoteSourceSequence = sourceSequence;
        UpdatePeakEquity();
    }

    public TradeIrGatewayAdmissionV1 Admit(TradeIrOrderIntentV1 intent)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(intent);

        if (!StringComparer.Ordinal.Equals(intent.DefinitionSha256, _plan.DefinitionSha256))
            return RejectIntent(intent, TradeIrGatewayDecisionCodesV1.DefinitionMismatch,
                "Intent definition hash does not match the admitted compiled plan.");
        if (!StringComparer.Ordinal.Equals(
                intent.AdmissionManifestSha256,
                _plan.AdmissionManifestSha256))
            return RejectIntent(intent, TradeIrGatewayDecisionCodesV1.AdmissionManifestMismatch,
                "Intent admission-manifest hash does not match the admitted compiled plan.");
        if (!StringComparer.Ordinal.Equals(intent.OutputId, _plan.OrderIntentOutputId) ||
            !StringComparer.Ordinal.Equals(intent.NodeId, _plan.OrderIntentNodeId))
            return RejectIntent(intent, TradeIrGatewayDecisionCodesV1.OutputMismatch,
                "Intent output identity does not match the admitted compiled plan.");
        if (!StringComparer.Ordinal.Equals(intent.InstrumentKey, _plan.InstrumentKey))
            return RejectIntent(intent, TradeIrGatewayDecisionCodesV1.InstrumentMismatch,
                "Intent instrument key does not match the host-resolved plan binding.");
        if (_lastQuoteSourceSequence is null ||
            intent.SourceEventSequence != _lastQuoteSourceSequence.Value)
            return RejectIntent(intent, TradeIrGatewayDecisionCodesV1.SourceSequenceMismatch,
                "Intent source event sequence does not match the latest host-observed quote.",
                consumeSequence: false);
        if (_lastQuoteTimeUnixMicroseconds is null ||
            intent.EventTimeUnixMicroseconds != _lastQuoteTimeUnixMicroseconds.Value)
            return RejectIntent(intent, TradeIrGatewayDecisionCodesV1.EventTimeMismatch,
                "Intent event time does not match the latest host-observed quote.",
                consumeSequence: false);
        if (intent.IntentSequence != _lastIntentSequence + 1)
            return RejectIntent(intent, TradeIrGatewayDecisionCodesV1.SequenceMismatch,
                $"Expected intent sequence {_lastIntentSequence + 1}, found {intent.IntentSequence}.", consumeSequence: false);

        _lastIntentSequence = intent.IntentSequence;

        var currentPosition = _portfolio.SnapshotOf(_instrument.Id).Quantity;
        if (!HasExactDelta(intent, currentPosition))
            return RejectIntent(intent, TradeIrGatewayDecisionCodesV1.DeltaMismatch,
                "Intent side, quantity, target, or reduce-only flag does not match the host-derived position delta.");

        var evaluatedAt = FromUnixMicroseconds(intent.EventTimeUnixMicroseconds);
        _clock.SetTo(evaluatedAt.UtcDateTime);
        AdvanceRiskDay(evaluatedAt);
        PruneExposureAttempts(evaluatedAt);
        UpdatePeakEquity();

        var command = CreateCommand(intent, evaluatedAt, currentPosition);
        var context = CreateRiskContext(evaluatedAt);
        var evidence = RiskPolicyEvidence.Capture(context);
        var riskDecision = RiskPolicy.Evaluate(command, context);
        _exposureAttempts.Enqueue(evaluatedAt);

        var trace = AppendDecision(
            intent.IntentSequence,
            riskDecision.IsAllowed,
            riskDecision.IsAllowed
                ? TradeIrGatewayDecisionCodesV1.Allowed
                : TradeIrGatewayDecisionCodesV1.RiskDenied,
            riskDecision.Reason,
            command.PayloadHashSha256,
            riskDecision,
            evidence,
            evaluatedAt);

        if (!riskDecision.IsAllowed)
        {
            _feedback.Enqueue(new TradeIrOrderFeedbackV1(
                intent.IntentSequence,
                TradeIrOrderFeedbackStatusV1.Denied,
                cumulativeFilledQuantity: 0));
            return new TradeIrGatewayAdmissionV1(trace, command, Submission: null);
        }

        var clientOrderId = command.ClientOrderId.Value;
        var reservationPrice = _lastMarketPrice;
        _pending.Add(clientOrderId, new PendingOrder(
            intent.IntentSequence,
            command.Terms.Side,
            intent.Quantity,
            FilledQuantity: 0,
            reservationPrice));

        var submission = _book.Submit(ToOrderRequest(command), _instrument.Id);
        SubmittedOrderCount++;
        return new TradeIrGatewayAdmissionV1(trace, command, submission);
    }

    public IReadOnlyList<TradeIrOrderFeedbackV1> DrainFeedback()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var result = new List<TradeIrOrderFeedbackV1>(_feedback.Count);
        while (_feedback.TryDequeue(out var feedback)) result.Add(feedback);
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _bookTransitionBinding.Dispose();
    }

    private TradeIrGatewayAdmissionV1 RejectIntent(
        TradeIrOrderIntentV1 intent,
        string code,
        string reason,
        bool consumeSequence = true)
    {
        if (consumeSequence && intent.IntentSequence == _lastIntentSequence + 1)
            _lastIntentSequence = intent.IntentSequence;
        var evaluatedAt = TryFromUnixMicroseconds(intent.EventTimeUnixMicroseconds, out var instant)
            ? instant
            : DateTimeOffset.UnixEpoch;
        var trace = AppendDecision(
            intent.IntentSequence,
            IsAllowed: false,
            code,
            reason,
            CommandPayloadHashSha256: null,
            RiskDecision: null,
            PolicyEvidence: null,
            evaluatedAt);
        _feedback.Enqueue(new TradeIrOrderFeedbackV1(
            intent.IntentSequence,
            TradeIrOrderFeedbackStatusV1.Denied,
            cumulativeFilledQuantity: 0));
        return new TradeIrGatewayAdmissionV1(trace, Command: null, Submission: null);
    }

    private TradeIrGatewayDecisionTraceV1 AppendDecision(
        long intentSequence,
        bool IsAllowed,
        string code,
        string reason,
        string? CommandPayloadHashSha256,
        RiskDecision? RiskDecision,
        RiskPolicyEvidence? PolicyEvidence,
        DateTimeOffset evaluatedAtUtc)
    {
        var trace = new TradeIrGatewayDecisionTraceV1(
            checked(++_traceSequence),
            intentSequence,
            IsAllowed,
            code,
            reason,
            CommandPayloadHashSha256,
            RiskDecision,
            PolicyEvidence,
            evaluatedAtUtc);
        _decisions.Add(trace);
        return trace;
    }

    private SubmitOrderCommand CreateCommand(
        TradeIrOrderIntentV1 intent,
        DateTimeOffset evaluatedAt,
        long currentPosition)
    {
        var stem = $"{_plan.DefinitionSha256[..16]}-{intent.IntentSequence:D12}";
        var metadata = new ExecutionCommandMetadata(
            new CommandId($"tradeir-cmd-{stem}"),
            new CorrelationId($"tradeir-run-{_plan.DefinitionSha256[..24]}"),
            causationId: null,
            _policy.AccountId,
            new StrategyId(_definition.StrategyId),
            new StrategyVersion(_definition.StrategyVersion),
            _policy.VenueId,
            _instrument.Id,
            ExecutionEnvironment.Backtest,
            evaluatedAt,
            expectedOrderSequence: 0);
        var terms = new OrderTerms(
            intent.Side == TradeIrOrderSideV1.Buy ? OrderSide.Buy : OrderSide.Sell,
            OrderType.Market,
            ScaledQuantity.FromWhole(intent.Quantity),
            timeInForce: MapTimeInForce(intent.TimeInForce),
            reduceOnly: intent.ReduceOnly);
        var clientOrderId = new ClientOrderId($"tradeir-client-{stem}");
        var instructionContext = new CanonicalInstructionMappingContext(
            new IntentId($"tradeir-intent-{stem}"),
            bucketId: null,
            new LegId($"tradeir-leg-{stem}"),
            new ExecutionLeaseId($"backtest-{_policy.AccountId.Value}"),
            new FencingToken(1),
            TradeIntentQuantityMode.TargetPosition,
            ScaledQuantity.FromWhole(intent.TargetQuantity),
            ScaledQuantity.FromWhole(currentPosition),
            protectiveStopPrice: null,
            profitTargetPrice: null,
            estimatedRoundTripCostPerUnit: ScaledMoney.Zero,
            strategyNoteId: intent.SourceEventSequence,
            policyVersion: _policy.ProfileId);
        var instructionFault = CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            clientOrderId,
            terms,
            instructionContext,
            out var canonicalInstruction);
        if (instructionFault != OrderDomainFault.None || canonicalInstruction is null)
            throw new InvalidOperationException($"Host-derived TradeIR instruction is invalid: {instructionFault}.");
        return new SubmitOrderCommand(
            metadata,
            new OrderId($"tradeir-order-{stem}"),
            clientOrderId,
            terms,
            canonicalInstruction);
    }

    private RiskEvaluationContext CreateRiskContext(DateTimeOffset evaluatedAt)
    {
        var position = _portfolio.SnapshotOf(_instrument.Id).Quantity;
        var buyReserved = _pending.Values
            .Where(static order => order.Side == OrderSide.Buy)
            .Sum(static order => (decimal)order.RemainingQuantity);
        var sellReserved = _pending.Values
            .Where(static order => order.Side == OrderSide.Sell)
            .Sum(static order => (decimal)order.RemainingQuantity);
        var multiplier = RequireFiniteDecimal(_instrument.ContractMultiplier, "contract multiplier");
        var grossReserved = _pending.Values.Sum(order =>
            order.RemainingQuantity * order.ReservationPrice * multiplier);
        var equity = RequireFiniteDecimal(_portfolio.Equity(), "portfolio equity");
        _peakEquity = Math.Max(_peakEquity, equity);
        var hasUnrepresentableMarketEconomics = false;
        var marketPrice = CapturePrice(_lastMarketPrice, ref hasUnrepresentableMarketEconomics);
        var exactMultiplier = CaptureRatio(multiplier, ref hasUnrepresentableMarketEconomics);

        return new RiskEvaluationContext(
            _policy.Limits,
            _policy.ControlMode,
            _policy.KillSwitchActive,
            ScaledQuantity.FromWhole(position),
            ExecutionNumericBoundary.QuantityFromDecimal(buyReserved),
            ExecutionNumericBoundary.QuantityFromDecimal(sellReserved),
            ExecutionNumericBoundary.MoneyFromDecimal(grossReserved),
            existingOrderSignedReservation: ScaledQuantity.Zero,
            existingOrderGrossReservation: ScaledMoney.Zero,
            existingOrderFilledQuantity: ScaledQuantity.Zero,
            availableBuyingPower: ExecutionNumericBoundary.MoneyFromDecimal(
                Math.Max(0m, RequireFiniteDecimal(_portfolio.Cash, "portfolio cash"))),
            dailyNetRealizedPnl: ExecutionNumericBoundary.MoneyFromDecimal(
                RequireFiniteDecimal(_portfolio.TotalRealizedPnl, "realized PnL") - _realizedPnlAtDayStart),
            currentEquity: ExecutionNumericBoundary.MoneyFromDecimal(equity),
            peakEquity: ExecutionNumericBoundary.MoneyFromDecimal(_peakEquity),
            marketPrice: marketPrice,
            exposureCommandsInWindow: _exposureAttempts.Count,
            evaluatedAtUtc: evaluatedAt,
            contractMultiplier: exactMultiplier,
            accountCurrency: _instrument.Contract.Currency,
            hasUnrepresentableMarketEconomics: hasUnrepresentableMarketEconomics);
    }

    private static ScaledPrice CapturePrice(decimal value, ref bool outOfRange)
    {
        if (ExecutionNumericBoundary.TryPriceFromDecimal(value, out var exact) && exact.Coefficient > 0)
            return exact;
        outOfRange = true;
        return new ScaledPrice(1, 0);
    }

    private static ScaledRatio CaptureRatio(decimal value, ref bool outOfRange)
    {
        if (ExecutionNumericBoundary.TryRatioFromDecimal(value, out var exact) && exact.Coefficient > 0)
            return exact;
        outOfRange = true;
        return new ScaledRatio(1, 0);
    }

    private void OnBookEvent(TradingTerminal.Core.Domain.InstrumentId instrument, OrderEvent orderEvent)
    {
        if (instrument != _instrument.Id || !_pending.TryGetValue(orderEvent.ClientOrderId, out var pending))
            return;
        if (orderEvent.FilledQuantity < pending.FilledQuantity || orderEvent.FilledQuantity > pending.Quantity)
            throw new InvalidOperationException("Simulated book emitted an invalid cumulative fill quantity.");

        if (orderEvent.LastFillQuantity > 0 && orderEvent.LastFillPrice is { } price)
            _portfolio.OnFill(
                instrument,
                orderEvent.TimestampUtc,
                orderEvent.Side,
                orderEvent.LastFillQuantity,
                price,
                orderEvent.Liquidity);

        pending = pending with { FilledQuantity = orderEvent.FilledQuantity };
        var status = orderEvent.State switch
        {
            OrderState.Working => TradeIrOrderFeedbackStatusV1.Working,
            OrderState.PartiallyFilled => TradeIrOrderFeedbackStatusV1.PartiallyFilled,
            OrderState.Filled => TradeIrOrderFeedbackStatusV1.Filled,
            OrderState.Cancelled => TradeIrOrderFeedbackStatusV1.Cancelled,
            OrderState.Rejected => TradeIrOrderFeedbackStatusV1.Rejected,
            _ => throw new InvalidOperationException($"Unsupported simulated order state '{orderEvent.State}'."),
        };
        _feedback.Enqueue(new TradeIrOrderFeedbackV1(
            pending.IntentSequence,
            status,
            orderEvent.FilledQuantity));

        if (orderEvent.State is OrderState.Filled or OrderState.Cancelled or OrderState.Rejected)
            _pending.Remove(orderEvent.ClientOrderId);
        else
            _pending[orderEvent.ClientOrderId] = pending;
        UpdatePeakEquity();
    }

    private OrderRequest ToOrderRequest(SubmitOrderCommand command) => new(
        command.ClientOrderId.Value,
        _instrument.Contract,
        command.Terms.Side,
        command.Terms.Type,
        RequireWholeQuantity(command.Terms.Quantity),
        command.Terms.LimitPrice is { } limit
            ? checked((double)ExecutionNumericBoundary.ToDecimal(limit))
            : null,
        command.Terms.StopPrice is { } stop
            ? checked((double)ExecutionNumericBoundary.ToDecimal(stop))
            : null,
        command.Terms.TimeInForce);

    private static long RequireWholeQuantity(ScaledQuantity quantity) =>
        quantity.TryGetWholeUnits(out var units) && units > 0
            ? units
            : throw new InvalidOperationException(
                "The legacy backtest book accepts positive whole quantities only.");

    private bool HasExactDelta(TradeIrOrderIntentV1 intent, long currentPosition)
    {
        long delta;
        long quantity;
        try
        {
            delta = checked(intent.TargetQuantity - currentPosition);
            if (delta == 0) return false;
            quantity = delta > 0 ? delta : checked(-delta);
        }
        catch (OverflowException)
        {
            return false;
        }

        var expectedSide = delta > 0 ? TradeIrOrderSideV1.Buy : TradeIrOrderSideV1.Sell;
        var expectedReduceOnly = currentPosition switch
        {
            > 0 => intent.TargetQuantity >= 0 && intent.TargetQuantity < currentPosition,
            < 0 => intent.TargetQuantity <= 0 && intent.TargetQuantity > currentPosition,
            _ => false,
        };
        return intent.Side == expectedSide && intent.Quantity == quantity &&
               intent.ReduceOnly == expectedReduceOnly;
    }

    private void PruneExposureAttempts(DateTimeOffset evaluatedAt)
    {
        var floorUtcTicks = evaluatedAt.UtcTicks - _policy.Limits.RateLimitWindow.Ticks;
        var inclusiveFloor = floorUtcTicks <= DateTimeOffset.MinValue.UtcTicks
            ? DateTimeOffset.MinValue
            : new DateTimeOffset(floorUtcTicks, TimeSpan.Zero);
        while (_exposureAttempts.TryPeek(out var attempt) && attempt < inclusiveFloor)
            _exposureAttempts.Dequeue();
    }

    private void AdvanceRiskDay(DateTimeOffset evaluatedAt)
    {
        var day = DateOnly.FromDateTime(evaluatedAt.UtcDateTime);
        if (_riskDay == day) return;
        _riskDay = day;
        _realizedPnlAtDayStart = RequireFiniteDecimal(_portfolio.TotalRealizedPnl, "realized PnL");
    }

    private void UpdatePeakEquity()
    {
        var equity = RequireFiniteDecimal(_portfolio.Equity(), "portfolio equity");
        _peakEquity = Math.Max(_peakEquity, equity);
    }

    private static TimeInForce MapTimeInForce(TradeIrTimeInForceV1 value) => value switch
    {
        TradeIrTimeInForceV1.Day => TimeInForce.Day,
        TradeIrTimeInForceV1.GoodTilCancelled => TimeInForce.Gtc,
        TradeIrTimeInForceV1.ImmediateOrCancel => TimeInForce.Ioc,
        _ => throw new ArgumentOutOfRangeException(nameof(value)),
    };

    private static SourceIndependentInstrumentRef ResolvePortableInstrument(
        StrategyIntermediateRepresentationV1 definition,
        string instrumentKey)
    {
        var matches = (definition.DataRequirements ?? [])
            .Where(static requirement => requirement is not null)
            .SelectMany(static requirement => requirement.InstrumentSelector?.References ?? [])
            .Where(reference => reference is not null &&
                StringComparer.Ordinal.Equals(reference.InstrumentKey, instrumentKey))
            .Distinct()
            .ToArray();
        if (matches.Length != 1)
        {
            throw new ArgumentException(
                $"Compiled plan instrument '{instrumentKey}' must resolve to exactly one portable instrument in the definition.",
                nameof(definition));
        }
        return matches[0];
    }

    private static bool MatchesHostInstrument(
        SourceIndependentInstrumentRef portable,
        Contract contract)
    {
        var venue = string.IsNullOrWhiteSpace(contract.PrimaryExchange)
            ? contract.Exchange
            : contract.PrimaryExchange;
        return StringComparer.Ordinal.Equals(portable.Symbol, contract.Symbol) &&
               StringComparer.Ordinal.Equals(portable.Currency, contract.Currency) &&
               StringComparer.Ordinal.Equals(portable.Venue, venue) &&
               StringComparer.Ordinal.Equals(ExpectedSecurityType(portable.AssetClass), contract.SecType);
    }

    private static string? ExpectedSecurityType(AssetClass assetClass) => assetClass switch
    {
        AssetClass.Equity => "STK",
        AssetClass.Future => "FUT",
        AssetClass.Forex => "CASH",
        AssetClass.Crypto => "CRYPTO",
        AssetClass.Option => "OPT",
        AssetClass.Index => "IND",
        _ => null,
    };

    private static string Describe(SourceIndependentInstrumentRef instrument) =>
        $"{instrument.InstrumentKey}:{instrument.AssetClass}:{instrument.Symbol}:{instrument.Venue}:{instrument.Currency}";

    private static string Describe(Contract contract) =>
        $"{contract.SecType}:{contract.Symbol}:{contract.PrimaryExchange}:{contract.Currency}";

    private static decimal RequireFiniteDecimal(double value, string label)
    {
        if (!double.IsFinite(value))
            throw new InvalidOperationException($"Host {label} is not representable as a finite decimal.");
        try
        {
            return checked((decimal)value);
        }
        catch (OverflowException exception)
        {
            throw new InvalidOperationException(
                $"Host {label} is not representable as a finite decimal.",
                exception);
        }
    }

    private static void ValidateContractMultiplier(double value)
    {
        decimal multiplier;
        try
        {
            multiplier = RequireFiniteDecimal(value, "contract multiplier");
        }
        catch (InvalidOperationException exception)
        {
            throw new ArgumentOutOfRangeException(
                "instrument",
                value,
                exception.Message);
        }
        if (multiplier <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                "instrument",
                value,
                "Host contract multiplier must be a positive decimal value.");
        }
    }

    private static DateTimeOffset FromUnixMicroseconds(long microseconds)
    {
        if (!TryFromUnixMicroseconds(microseconds, out var result))
            throw new ArgumentOutOfRangeException(nameof(microseconds));
        return result;
    }

    private static bool TryFromUnixMicroseconds(long microseconds, out DateTimeOffset result)
    {
        try
        {
            result = DateTimeOffset.UnixEpoch.AddTicks(checked(microseconds * 10));
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            result = default;
            return false;
        }
        catch (OverflowException)
        {
            result = default;
            return false;
        }
    }

    private static long ToUnixMicroseconds(DateTimeOffset value) => checked(
        (value.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / 10);

    private sealed record PendingOrder(
        long IntentSequence,
        OrderSide Side,
        long Quantity,
        long FilledQuantity,
        decimal ReservationPrice)
    {
        public long RemainingQuantity => Quantity - FilledQuantity;
    }
}
