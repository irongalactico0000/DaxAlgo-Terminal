using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Trading;

namespace TradingTerminal.UI.Execution;

public sealed record PaperExecutionInstrumentChoice(
    InstrumentId InstrumentId,
    string Symbol,
    string AssetClass,
    string Exchange,
    string Currency)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Exchange)
        ? $"{Symbol} · {AssetClass}"
        : $"{Symbol} · {AssetClass} · {Exchange}";
}

public sealed record PaperOrderTicketDraft(
    PaperExecutionInstrumentChoice Instrument,
    OrderSide Side,
    OrderType OrderType,
    TimeInForce TimeInForce,
    string Quantity,
    string MarkPrice,
    string? LimitPrice,
    string? StopPrice,
    bool ReduceOnly);

/// <summary>
/// App boundary that resolves ticket text into exact OMS values and binds it to the active Paper
/// resource, writer lease, market observation, canonical instruction, and risk evidence.
/// </summary>
public interface IPaperExecutionOrderFactory
{
    IReadOnlyList<PaperExecutionInstrumentChoice> Instruments { get; }

    bool TryCreateSubmit(
        PaperOrderTicketDraft draft,
        PaperExecutionClientSnapshot snapshot,
        out ExecutionSubmitRequest? request,
        out string? reason);

    bool TryCreateReplacementTerms(
        PaperOrderTicketDraft draft,
        out OrderTerms? terms,
        out string? reason);

    RiskEvaluationContext CreateReplacementRisk(
        PaperOrderTicketDraft draft,
        OmsOrderProjection order,
        PaperExecutionClientSnapshot snapshot);
}

public sealed record PaperOrderRow(
    ClientOrderId ClientOrderId,
    string ClientId,
    string Instrument,
    string Side,
    string Type,
    string Quantity,
    string Filled,
    string Limit,
    string Stop,
    string State,
    string LastSequence,
    bool CanCancel,
    bool CanReplace);

public sealed record PaperPositionRow(string Instrument, string Quantity, string Direction, string ObservedUtc);
public sealed record PaperFillRow(string TradeId, string Instrument, string Side, string Quantity, string Price, string Fee, string OccurredUtc);
public sealed record PaperCashRow(string Currency, string Total, string Available, string ObservedUtc);
public sealed record PaperPortfolioSummaryRow(
    string OpeningBalance,
    string CurrentCash,
    string MarkedEquity,
    string RealizedProfitAndLoss,
    string UnrealizedProfitAndLoss,
    string GrossExposure,
    string NetExposure,
    string OpenPositions,
    string Currency,
    string ValuationBasis);
public sealed record PaperPerformancePeriodRow(
    string Range,
    string Equity,
    string ProfitAndLoss,
    string Return,
    string Sharpe,
    string MaximumDrawdown,
    string WinRate,
    string Trades);
public sealed record PaperExposureRow(
    string Instrument,
    string Quantity,
    string AverageEntry,
    string Mark,
    string MarketValue,
    string UnrealizedProfitAndLoss,
    string MarkBasis);
public sealed record PaperEventRow(string Sequence, string ClientId, string Kind, string State, string Source, string RecordedUtc);
public sealed record PaperExecutionQualityRow(
    string FillRate,
    string AverageSlippage,
    string RejectRate,
    string AverageAcknowledgement,
    string Orders,
    string FilledOrders,
    string Rejects,
    string Cancels,
    string ReconciliationCases,
    string UnknownOutcomes,
    string Provenance);
public sealed record PaperRiskDecisionRow(
    ClientOrderId ClientOrderId,
    long AggregateSequence,
    string ClientId,
    string Sequence,
    string Phase,
    string Decision,
    string Code,
    string Reason,
    string ProjectedNetQuantity,
    string ProjectedGrossNotional,
    string PolicyVersion,
    string LimitsHash,
    string CommandPayloadHash,
    string ControlState,
    string PositionEvidence,
    string ReservationEvidence,
    string AccountEvidence,
    string MarketEvidence,
    string LimitsEvidence,
    string EvaluatedUtc,
    string RecordedUtc);
public sealed record PaperReconciliationCaseRow(
    ReconciliationCaseId CaseId,
    string CaseIdText,
    string Subject,
    string Kind,
    string Status,
    string OpenedUtc,
    string LocalEvidence,
    string VenueEvidence,
    string Resolution,
    bool CanResolve);

/// <summary>
/// Portable operational projection for the native Avalonia Paper console. Every command crosses
/// <see cref="IPaperExecutionClient"/>; this view model never mutates order state locally.
/// </summary>
public sealed partial class PaperExecutionConsoleViewModel : ObservableObject, IDisposable
{
    private readonly IPaperExecutionClient _client;
    private readonly IPaperExecutionOrderFactory _orderFactory;
    private readonly IReadOnlyDictionary<InstrumentId, string> _symbols;
    private bool _disposed;

    public PaperExecutionConsoleViewModel(
        IPaperExecutionClient client,
        IPaperExecutionOrderFactory orderFactory)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _orderFactory = orderFactory ?? throw new ArgumentNullException(nameof(orderFactory));
        Instruments = orderFactory.Instruments;
        _symbols = Instruments
            .GroupBy(item => item.InstrumentId)
            .ToDictionary(group => group.Key, group => group.First().Symbol);
        SelectedInstrument = Instruments.FirstOrDefault();
        _client.SnapshotInvalidated += OnSnapshotInvalidated;
        ApplySnapshot(_client.GetSnapshot());
    }

    public IReadOnlyList<PaperExecutionInstrumentChoice> Instruments { get; }
    public IReadOnlyList<OrderSide> Sides { get; } = Enum.GetValues<OrderSide>();
    public IReadOnlyList<OrderType> OrderTypes { get; } = Enum.GetValues<OrderType>();
    public IReadOnlyList<TimeInForce> TimeInForces { get; } = Enum.GetValues<TimeInForce>();

    public ObservableCollection<PaperOrderRow> Orders { get; } = [];
    public ObservableCollection<PaperPositionRow> Positions { get; } = [];
    public ObservableCollection<PaperFillRow> Fills { get; } = [];
    public ObservableCollection<PaperCashRow> Cash { get; } = [];
    public ObservableCollection<PaperPerformancePeriodRow> PerformancePeriods { get; } = [];
    public ObservableCollection<PaperExposureRow> PortfolioExposures { get; } = [];
    public ObservableCollection<PaperEventRow> Events { get; } = [];
    public ObservableCollection<PaperRiskDecisionRow> RiskDecisions { get; } = [];
    public ObservableCollection<PaperReconciliationCaseRow> ReconciliationCases { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private PaperExecutionInstrumentChoice? _selectedInstrument;

    [ObservableProperty] private OrderSide _selectedSide = OrderSide.Buy;
    [ObservableProperty] private OrderType _selectedOrderType = OrderType.Market;
    [ObservableProperty] private TimeInForce _selectedTimeInForce = TimeInForce.Day;
    [ObservableProperty] private string _quantity = "1";
    [ObservableProperty] private string _markPrice = "100";
    [ObservableProperty] private string _limitPrice = "99";
    [ObservableProperty] private string _stopPrice = "101";
    [ObservableProperty] private bool _reduceOnly;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReplaceSelectedCommand))]
    private PaperOrderRow? _selectedOrder;

    [ObservableProperty]
    private PaperRiskDecisionRow? _selectedRiskDecision;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResolveSelectedReconciliationCaseCommand))]
    private PaperReconciliationCaseRow? _selectedReconciliationCase;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResolveSelectedReconciliationCaseCommand))]
    private string _resolutionOperator = Environment.UserName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResolveSelectedReconciliationCaseCommand))]
    private string _resolutionEvidence = string.Empty;

    [ObservableProperty] private string _modeText = "PAPER · SIMULATED · NO LIVE ROUTE";
    [ObservableProperty] private string _writerStatus = "Writer unknown";
    [ObservableProperty] private string _intakeStatus = "Intake unknown";
    [ObservableProperty] private string _accountStatus = "Local Paper account";
    [ObservableProperty] private string _lastMessage = "Paper execution state has not been refreshed.";
    [ObservableProperty] private bool _lastOperationSucceeded = true;
    [ObservableProperty] private bool _leaseHeld;
    [ObservableProperty] private bool _intakePaused;
    [ObservableProperty] private bool _reconciliationAdmissionBlocked;
    [ObservableProperty] private bool _killConfirmationArmed;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _orderCount = "0";
    [ObservableProperty] private string _openOrderCount = "0";
    [ObservableProperty] private string _fillCount = "0";
    [ObservableProperty] private string _positionCount = "0";
    [ObservableProperty] private string _cashTotal = "0";
    [ObservableProperty] private string _markedEquity = "100000 SIM";
    [ObservableProperty] private PaperPortfolioSummaryRow _portfolio = new(
        "100000", "100000", "100000", "0", "0", "0", "0", "0", "SIM",
        "No execution fills have been recorded.");
    [ObservableProperty] private string _lastLedgerSequence = "0";
    [ObservableProperty] private PaperExecutionQualityRow _executionQuality = new(
        "0.0%", "n/a", "0.0%", "n/a", "0", "0", "0", "0", "0", "0",
        "Verified projections and immutable ledger events.");
    [ObservableProperty] private string _riskDecisionCount = "0";
    [ObservableProperty] private string _reconciliationStatus = "Reconciliation unknown";
    [ObservableProperty] private string _reconciliationCaseCount = "0";

    public bool IsMarketOrder => SelectedOrderType == OrderType.Market;
    public bool NeedsLimitPrice => SelectedOrderType is OrderType.Limit or OrderType.StopLimit;
    public bool NeedsStopPrice => SelectedOrderType is OrderType.Stop or OrderType.StopLimit;
    public bool CanIssueCommands => LeaseHeld && !IntakePaused && !ReconciliationAdmissionBlocked && !IsBusy;
    public string IntakeActionText => IntakePaused ? "Resume intake" : "Pause intake";
    public string KillActionText => KillConfirmationArmed ? "CONFIRM KILL + FLATTEN" : "Arm kill + flatten";

    partial void OnSelectedOrderTypeChanged(OrderType value)
    {
        OnPropertyChanged(nameof(IsMarketOrder));
        OnPropertyChanged(nameof(NeedsLimitPrice));
        OnPropertyChanged(nameof(NeedsStopPrice));
    }

    partial void OnLeaseHeldChanged(bool value) => CommandStateChanged();
    partial void OnIntakePausedChanged(bool value)
    {
        OnPropertyChanged(nameof(IntakeActionText));
        CommandStateChanged();
    }
    partial void OnReconciliationAdmissionBlockedChanged(bool value) => CommandStateChanged();
    partial void OnIsBusyChanged(bool value) => CommandStateChanged();
    partial void OnKillConfirmationArmedChanged(bool value) => OnPropertyChanged(nameof(KillActionText));

    [RelayCommand]
    private async Task RefreshAsync() => await RunAsync(() => _client.RefreshAsync());

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        var snapshot = _client.GetSnapshot();
        var draft = Draft();
        if (draft is null)
        {
            SetResult(false, "Select a valid instrument and complete the order ticket.");
            return;
        }
        if (!_orderFactory.TryCreateSubmit(draft, snapshot, out var request, out var reason) || request is null)
        {
            SetResult(false, reason ?? "The Paper order could not be constructed.");
            return;
        }
        await RunAsync(() => _client.SubmitAsync(request));
    }

    [RelayCommand(CanExecute = nameof(CanCancelSelected))]
    private async Task CancelSelectedAsync()
    {
        if (SelectedOrder is null) return;
        await RunAsync(() => _client.CancelAsync(SelectedOrder.ClientOrderId));
    }

    [RelayCommand(CanExecute = nameof(CanReplaceSelected))]
    private async Task ReplaceSelectedAsync()
    {
        var draft = Draft();
        var snapshot = _client.GetSnapshot();
        var projection = SelectedOrder is null
            ? null
            : snapshot.Orders.FirstOrDefault(item => item.ClientOrderId == SelectedOrder.ClientOrderId);
        if (draft is null || projection is null)
        {
            SetResult(false, "Select a replaceable order and valid replacement terms.");
            return;
        }
        if (!_orderFactory.TryCreateReplacementTerms(draft, out var terms, out var reason) || terms is null)
        {
            SetResult(false, reason ?? "The replacement terms are invalid.");
            return;
        }
        var risk = _orderFactory.CreateReplacementRisk(draft, projection, snapshot);
        await RunAsync(() => _client.ReplaceAsync(projection.ClientOrderId, terms, risk));
    }

    [RelayCommand]
    private async Task ReconcileAsync() => await RunAsync(() => _client.ReconcileAsync());

    [RelayCommand(CanExecute = nameof(CanResolveSelectedReconciliationCase))]
    private async Task ResolveSelectedReconciliationCaseAsync()
    {
        if (SelectedReconciliationCase is null) return;
        await RunAsync(() => _client.ResolveReconciliationCaseAsync(
            SelectedReconciliationCase.CaseId,
            ResolutionOperator,
            ResolutionEvidence));
        if (LastOperationSucceeded)
        {
            ResolutionEvidence = string.Empty;
            SelectedReconciliationCase = null;
        }
    }

    [RelayCommand]
    private async Task ToggleIntakeAsync()
    {
        KillConfirmationArmed = false;
        await RunAsync(() => _client.SetIntakePausedAsync(!IntakePaused));
    }

    [RelayCommand]
    private async Task KillAsync()
    {
        if (!KillConfirmationArmed)
        {
            KillConfirmationArmed = true;
            SetResult(false, "Kill is armed. Press CONFIRM KILL + FLATTEN to pause intake, cancel orders, flatten, and verify.");
            return;
        }
        KillConfirmationArmed = false;
        await RunAsync(() => _client.KillAsync());
    }

    private bool CanSubmit() => CanIssueCommands && SelectedInstrument is not null;
    private bool CanCancelSelected() => LeaseHeld && !IsBusy && SelectedOrder?.CanCancel == true;
    private bool CanReplaceSelected() => CanIssueCommands && SelectedOrder?.CanReplace == true;
    private bool CanResolveSelectedReconciliationCase() =>
        LeaseHeld && !IsBusy && SelectedReconciliationCase?.CanResolve == true &&
        !string.IsNullOrWhiteSpace(ResolutionOperator) &&
        !string.IsNullOrWhiteSpace(ResolutionEvidence);

    private PaperOrderTicketDraft? Draft() => SelectedInstrument is null
        ? null
        : new PaperOrderTicketDraft(
            SelectedInstrument,
            SelectedSide,
            SelectedOrderType,
            SelectedTimeInForce,
            Quantity,
            MarkPrice,
            LimitPrice,
            StopPrice,
            ReduceOnly);

    private async Task RunAsync(Func<ValueTask<PaperExecutionClientResult>> operation)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await operation();
            SetResult(result.IsSuccess, result.Message);
            ApplySnapshot(_client.GetSnapshot());
        }
        catch (Exception exception)
        {
            SetResult(false, $"Paper execution failed safely: {exception.Message}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSnapshotInvalidated(object? sender, EventArgs e) => ApplySnapshot(_client.GetSnapshot());

    private void ApplySnapshot(PaperExecutionClientSnapshot snapshot)
    {
        LeaseHeld = snapshot.LeaseHeld;
        IntakePaused = snapshot.IntakePaused;
        ReconciliationAdmissionBlocked = snapshot.ReconciliationAdmissionBlocked;
        WriterStatus = snapshot.LeaseHeld
            ? $"Writer active · fence {snapshot.FencingToken.Value}"
            : "Read-only · writer lease unavailable";
        IntakeStatus = snapshot.IntakePaused ? "New intake paused" : "New intake enabled";
        ReconciliationStatus = snapshot.ReconciliationAdmissionBlocked
            ? $"BLOCKED · {snapshot.UnresolvedMaterialCaseCount} unresolved material case(s)"
            : "Reconciled · new exposure eligible";
        AccountStatus = $"{snapshot.Resource.TradingAccountId.Value} · {snapshot.Resource.VenueId.Value}";
        LastLedgerSequence = snapshot.LastOutboxSequence.ToString(CultureInfo.InvariantCulture);

        Replace(Orders, snapshot.Orders
            .OrderByDescending(item => item.LastSequence)
            .Select(item => new PaperOrderRow(
                item.ClientOrderId,
                item.ClientOrderId.Value,
                Symbol(item.Instruction.TradeIntent.Instrument),
                item.Terms.Side.ToString(),
                item.Terms.Type.ToString(),
                Exact(item.Terms.Quantity),
                Exact(item.FilledQuantity),
                item.Terms.LimitPrice is { } limit ? Exact(limit) : "—",
                item.Terms.StopPrice is { } stop ? Exact(stop) : "—",
                item.State.ToString(),
                item.LastSequence.ToString(CultureInfo.InvariantCulture),
                item.State is OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled,
                item.State is OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled)));

        Replace(Positions, snapshot.Economics.Positions.Select(item => new PaperPositionRow(
            Symbol(item.InstrumentId),
            Exact(item.Quantity),
            item.Quantity.Coefficient switch { > 0 => "LONG", < 0 => "SHORT", _ => "FLAT" },
            item.ObservedAtUtc.ToString("u", CultureInfo.InvariantCulture))));
        Replace(Fills, snapshot.Economics.Fills.OrderByDescending(item => item.OccurredAtUtc).Select(item => new PaperFillRow(
            item.TradeId.Value,
            Symbol(item.InstrumentId),
            item.Side.ToString(),
            Exact(item.Quantity),
            Exact(item.Price),
            Exact(item.Fee),
            item.OccurredAtUtc.ToString("u", CultureInfo.InvariantCulture))));
        Replace(Cash, snapshot.Economics.Cash.Select(item => new PaperCashRow(
            item.Currency,
            Exact(item.Total),
            Exact(item.Available),
            item.ObservedAtUtc.ToString("u", CultureInfo.InvariantCulture))));
        if (snapshot.PortfolioAnalytics is { } analytics)
        {
            Replace(Cash,
            [
                new PaperCashRow(
                    analytics.Currency,
                    Money(analytics.CurrentCash),
                    Money(analytics.CurrentCash),
                    snapshot.ObservedAtUtc.ToString("u", CultureInfo.InvariantCulture)),
            ]);
            Portfolio = new PaperPortfolioSummaryRow(
                Money(analytics.OpeningBalance),
                Money(analytics.CurrentCash),
                Money(analytics.MarkedEquity),
                SignedMoney(analytics.RealizedProfitAndLoss),
                SignedMoney(analytics.UnrealizedProfitAndLoss),
                Money(analytics.GrossExposure),
                SignedMoney(analytics.NetExposure),
                analytics.OpenPositionCount.ToString(CultureInfo.InvariantCulture),
                analytics.Currency,
                analytics.ValuationBasis + "; Paper SIM units, no broker/FX claim.");
            Replace(PerformancePeriods, analytics.Periods.Select(item => new PaperPerformancePeriodRow(
                item.Label,
                Money(item.RealizedEquity),
                SignedMoney(item.RealizedProfitAndLoss),
                $"{item.ReturnPercent:0.00}%",
                item.AnnualizedSharpe.ToString("0.00", CultureInfo.InvariantCulture),
                $"{item.MaximumDrawdownPercent:0.00}%",
                $"{item.WinRatePercent:0.0}%",
                item.TradeCount.ToString(CultureInfo.InvariantCulture))));
            Replace(PortfolioExposures, analytics.Exposures.Select(item => new PaperExposureRow(
                Symbol(item.InstrumentId),
                item.SignedQuantity.ToString("0.##################", CultureInfo.InvariantCulture),
                Money(item.AverageEntryPrice),
                Money(item.MarkPrice),
                SignedMoney(item.MarketValue),
                SignedMoney(item.UnrealizedProfitAndLoss),
                item.MarkBasis == PaperMarkBasis.CurrentPaperMark ? "Current Paper mark" : "Latest ledger fill")));
            MarkedEquity = $"{Money(analytics.MarkedEquity)} {analytics.Currency}";
        }
        Replace(Events, snapshot.LedgerEvents.OrderByDescending(item => item.RecordedAtUtc).Select(item => new PaperEventRow(
            item.AggregateSequence.ToString(CultureInfo.InvariantCulture),
            item.AggregateId.Value,
            item.Kind.ToString(),
            item.StateAfter.ToString(),
            item.Source.ToString(),
            item.RecordedAtUtc.ToString("u", CultureInfo.InvariantCulture))));
        var selectedRiskIdentity = SelectedRiskDecision is { } selectedRisk
            ? (selectedRisk.ClientOrderId, selectedRisk.AggregateSequence)
            : ((ClientOrderId, long)?)null;
        Replace(RiskDecisions, snapshot.RiskDecisionFacts
            .OrderByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.AggregateSequence)
            .Select(ToRiskDecisionRow));
        SelectedRiskDecision = selectedRiskIdentity is { } identity
            ? RiskDecisions.FirstOrDefault(item =>
                item.ClientOrderId == identity.Item1 && item.AggregateSequence == identity.Item2)
            : RiskDecisions.FirstOrDefault();
        Replace(ReconciliationCases, snapshot.CaseFacts.Select(item => new PaperReconciliationCaseRow(
            item.CaseId,
            item.CaseId.Value,
            $"{item.SubjectKind} · {item.SubjectKey}",
            item.Kind.ToString(),
            item.Status.ToString(),
            item.OpenedAtUtc.ToString("u", CultureInfo.InvariantCulture),
            item.LocalEvidence,
            item.BrokerEvidence,
            item.Status == ReconciliationCaseStatus.Resolved
                ? $"{item.ResolvedBy} · {item.ResolvedAtUtc:u} · {item.ResolutionEvidence}"
                : "Unresolved",
            item.IsMaterial && item.Status != ReconciliationCaseStatus.Resolved)));

        var quality = snapshot.QualityFacts;
        ExecutionQuality = new PaperExecutionQualityRow(
            $"{quality.FillRatePercent:0.0}%",
            quality.SlippageObservationCount == 0 ? "n/a" : $"{quality.AverageSlippageTicks:0.00} tk",
            $"{quality.RejectRatePercent:0.0}%",
            quality.AcknowledgementObservationCount == 0
                ? "n/a"
                : $"{quality.AverageAcknowledgementLatencyMilliseconds:0} ms",
            quality.Orders.ToString(CultureInfo.InvariantCulture),
            quality.FilledOrders.ToString(CultureInfo.InvariantCulture),
            quality.Rejects.ToString(CultureInfo.InvariantCulture),
            quality.Cancels.ToString(CultureInfo.InvariantCulture),
            quality.ReconciliationCases.ToString(CultureInfo.InvariantCulture),
            quality.UnknownOutcomes.ToString(CultureInfo.InvariantCulture),
            "Verified projections and immutable ledger events; slippage is unavailable without an arrival-price benchmark.");

        OrderCount = Orders.Count.ToString(CultureInfo.InvariantCulture);
        OpenOrderCount = Orders.Count(item => item.CanCancel).ToString(CultureInfo.InvariantCulture);
        FillCount = Fills.Count.ToString(CultureInfo.InvariantCulture);
        PositionCount = Positions.Count(item => item.Direction != "FLAT").ToString(CultureInfo.InvariantCulture);
        CashTotal = Cash.FirstOrDefault() is { } cash ? $"{cash.Total} {cash.Currency}" : "0 SIM";
        RiskDecisionCount = RiskDecisions.Count.ToString(CultureInfo.InvariantCulture);
        ReconciliationCaseCount = ReconciliationCases.Count.ToString(CultureInfo.InvariantCulture);
        if (!string.IsNullOrWhiteSpace(snapshot.LastOperationMessage))
            LastMessage = snapshot.LastOperationMessage;
    }

    private void CommandStateChanged()
    {
        OnPropertyChanged(nameof(CanIssueCommands));
        SubmitCommand.NotifyCanExecuteChanged();
        CancelSelectedCommand.NotifyCanExecuteChanged();
        ReplaceSelectedCommand.NotifyCanExecuteChanged();
        ResolveSelectedReconciliationCaseCommand.NotifyCanExecuteChanged();
    }

    private void SetResult(bool success, string message)
    {
        LastOperationSucceeded = success;
        LastMessage = message;
    }

    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> values)
    {
        target.Clear();
        foreach (var value in values) target.Add(value);
    }

    private static string Exact(ScaledQuantity value) =>
        ExecutionNumericBoundary.ToDecimal(value).ToString("0.##################", CultureInfo.InvariantCulture);
    private static string Exact(ScaledPrice value) =>
        ExecutionNumericBoundary.ToDecimal(value).ToString("0.##################", CultureInfo.InvariantCulture);
    private static string Exact(ScaledMoney value) =>
        ExecutionNumericBoundary.ToDecimal(value).ToString("0.##################", CultureInfo.InvariantCulture);
    private static string Exact(ScaledRatio value) =>
        ExecutionNumericBoundary.ToDecimal(value).ToString("0.##################", CultureInfo.InvariantCulture);
    private static string Money(decimal value) =>
        value.ToString("0.00", CultureInfo.InvariantCulture);
    private static string SignedMoney(decimal value) =>
        value.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

    private static PaperRiskDecisionRow ToRiskDecisionRow(PaperRiskDecisionSnapshot item)
    {
        var observation = item.Observation;
        var decision = observation.Decision;
        var evidence = observation.Evidence;
        var context = evidence.Context;
        var limits = context.Limits;
        var phase = item.EventKind switch
        {
            OrderEventKind.RiskAccepted or OrderEventKind.RiskRejected => "SUBMIT",
            OrderEventKind.ReplaceRiskAccepted or OrderEventKind.ReplaceRiskRejected => "REPLACE",
            _ => "UNKNOWN",
        };
        return new PaperRiskDecisionRow(
            item.ClientOrderId,
            item.AggregateSequence,
            item.ClientOrderId.Value,
            item.AggregateSequence.ToString(CultureInfo.InvariantCulture),
            phase,
            decision.IsAllowed ? "ALLOW" : "DENY",
            decision.Code.ToString(),
            decision.Reason,
            Exact(decision.ProjectedNetQuantity),
            $"{Exact(decision.ProjectedGrossNotional)} {context.AccountCurrency}",
            evidence.PolicyVersion,
            evidence.LimitsHashSha256,
            observation.CommandPayloadHashSha256,
            $"{context.ControlMode} · kill switch {(context.KillSwitchActive ? "ON" : "OFF")} · " +
            $"unrepresentable market {(context.HasUnrepresentableMarketEconomics ? "YES" : "NO")}",
            $"position {Exact(context.CurrentPositionQuantity)} · buy reserved {Exact(context.CurrentBuyReservedQuantity)} · " +
            $"sell reserved {Exact(context.CurrentSellReservedQuantity)} · existing signed {Exact(context.ExistingOrderSignedReservation)} · " +
            $"existing filled {Exact(context.ExistingOrderFilledQuantity)}",
            $"gross reserved {Exact(context.CurrentGrossReservedNotional)} {context.AccountCurrency} · " +
            $"existing gross {Exact(context.ExistingOrderGrossReservation)} {context.AccountCurrency}",
            $"buying power {Exact(context.AvailableBuyingPower)} {context.AccountCurrency} · " +
            $"realized P&L {Exact(context.DailyNetRealizedPnl)} · equity {Exact(context.CurrentEquity)} · peak {Exact(context.PeakEquity)}",
            $"mark {Exact(context.MarketPrice)} · multiplier {Exact(context.ContractMultiplier)} · " +
            $"commands {context.ExposureCommandsInWindow}/{limits.MaximumExposureCommandsPerWindow} per {limits.RateLimitWindow}",
            $"order ≤ {Exact(limits.MaximumOrderQuantity)} · |position| ≤ {Exact(limits.MaximumAbsolutePosition)} · " +
            $"gross ≤ {Exact(limits.MaximumGrossNotional)} · minimum buying power {Exact(limits.MinimumBuyingPower)} · " +
            $"daily loss ≤ {Exact(limits.MaximumDailyLoss)} · drawdown ≤ {Exact(limits.MaximumDrawdown)}",
            context.EvaluatedAtUtc.ToString("u", CultureInfo.InvariantCulture),
            item.RecordedAtUtc.ToString("u", CultureInfo.InvariantCulture));
    }

    private string Symbol(InstrumentId instrumentId) =>
        _symbols.TryGetValue(instrumentId, out var symbol)
            ? symbol
            : $"Instrument {instrumentId.Value.ToString(CultureInfo.InvariantCulture)}";

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _client.SnapshotInvalidated -= OnSnapshotInvalidated;
    }
}
