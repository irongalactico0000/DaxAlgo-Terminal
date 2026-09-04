namespace TradingTerminal.Core.Execution;

/// <summary>
/// Canonical Paper-OMS order lifecycle. This replaces the six-state trading projection at the
/// execution boundary; the older <c>Trading.OrderState</c> remains a UI/backtest projection only.
/// </summary>
public enum OrderLifecycleState : byte
{
    Draft = 0,
    Validated = 1,
    Prepared = 2,
    Armed = 3,
    Releasing = 4,
    Acknowledging = 5,
    Working = 6,
    PartiallyFilled = 7,
    Filled = 8,
    Cancelled = 9,
    Rejected = 10,
    Expired = 11,
    Unknown = 12,
    Reconciling = 13,
    PendingCancel = 14,
    PendingReplace = 15,
    Reconciled = 16,
}

/// <summary>One explicitly admitted directed lifecycle edge.</summary>
public readonly record struct OrderLifecycleTransition(
    OrderLifecycleState From,
    OrderLifecycleState To);

/// <summary>Semantic fact that may cause or annotate one canonical lifecycle state.</summary>
public enum OrderEventKind : byte
{
    DraftCreated = 0,
    RiskAccepted = 1,
    RiskRejected = 2,
    ValidationRejected = 3,
    Prepared = 4,
    Armed = 5,
    SendStarted = 6,
    SendFailedBeforeAcceptance = 7,
    SubmissionRecorded = 8,
    VenueAcknowledged = 9,
    FillReceived = 10,
    CancelRequested = 11,
    CancelConfirmed = 12,
    ReplaceRequested = 13,
    ReplaceConfirmed = 14,
    VenueRejected = 15,
    Expired = 16,
    OutcomeUnknown = 17,
    ReconciliationStarted = 18,
    Reconciled = 19,
    RecoveryObserved = 20,
    ReplaceRiskAccepted = 21,
    ReplaceRiskRejected = 22,
    CommissionObserved = 23,
    PositionObserved = 24,
    StopMonitoringStarted = 25,
    StopActivated = 26,
    CancelDispatchRecorded = 27,
    ReplaceDispatchRecorded = 28,
}

/// <summary>Subsystem authorized to produce an OMS event.</summary>
public enum OrderEventSource : byte
{
    Command = 0,
    Risk = 1,
    SimulatedVenue = 2,
    Recovery = 3,
    Reconciliation = 4,
}

/// <summary>
/// Pure fail-closed transition policy. No caller may infer legality from enum ordering or apply an
/// unlisted transition. Terminal states can move only to <see cref="OrderLifecycleState.Reconciled"/>.
/// </summary>
public static class OrderLifecycle
{
    private static readonly OrderLifecycleTransition[] TransitionArray =
    [
        new(OrderLifecycleState.Draft, OrderLifecycleState.Validated),
        new(OrderLifecycleState.Draft, OrderLifecycleState.Rejected),
        new(OrderLifecycleState.Validated, OrderLifecycleState.Prepared),
        new(OrderLifecycleState.Validated, OrderLifecycleState.Rejected),
        new(OrderLifecycleState.Prepared, OrderLifecycleState.Armed),
        new(OrderLifecycleState.Prepared, OrderLifecycleState.Rejected),
        new(OrderLifecycleState.Prepared, OrderLifecycleState.Expired),
        new(OrderLifecycleState.Armed, OrderLifecycleState.Releasing),
        new(OrderLifecycleState.Armed, OrderLifecycleState.Cancelled),
        new(OrderLifecycleState.Armed, OrderLifecycleState.Expired),
        new(OrderLifecycleState.Releasing, OrderLifecycleState.Acknowledging),
        new(OrderLifecycleState.Releasing, OrderLifecycleState.Armed),
        new(OrderLifecycleState.Releasing, OrderLifecycleState.Unknown),
        new(OrderLifecycleState.Releasing, OrderLifecycleState.Rejected),
        new(OrderLifecycleState.Acknowledging, OrderLifecycleState.Working),
        new(OrderLifecycleState.Acknowledging, OrderLifecycleState.PartiallyFilled),
        new(OrderLifecycleState.Acknowledging, OrderLifecycleState.Filled),
        new(OrderLifecycleState.Acknowledging, OrderLifecycleState.Cancelled),
        new(OrderLifecycleState.Acknowledging, OrderLifecycleState.Rejected),
        new(OrderLifecycleState.Acknowledging, OrderLifecycleState.Expired),
        new(OrderLifecycleState.Acknowledging, OrderLifecycleState.Unknown),
        new(OrderLifecycleState.Working, OrderLifecycleState.PartiallyFilled),
        new(OrderLifecycleState.Working, OrderLifecycleState.Filled),
        new(OrderLifecycleState.Working, OrderLifecycleState.PendingCancel),
        new(OrderLifecycleState.Working, OrderLifecycleState.PendingReplace),
        new(OrderLifecycleState.Working, OrderLifecycleState.Cancelled),
        new(OrderLifecycleState.Working, OrderLifecycleState.Rejected),
        new(OrderLifecycleState.Working, OrderLifecycleState.Expired),
        new(OrderLifecycleState.Working, OrderLifecycleState.Unknown),
        new(OrderLifecycleState.PartiallyFilled, OrderLifecycleState.Filled),
        new(OrderLifecycleState.PartiallyFilled, OrderLifecycleState.PendingCancel),
        new(OrderLifecycleState.PartiallyFilled, OrderLifecycleState.PendingReplace),
        new(OrderLifecycleState.PartiallyFilled, OrderLifecycleState.Cancelled),
        new(OrderLifecycleState.PartiallyFilled, OrderLifecycleState.Expired),
        new(OrderLifecycleState.PartiallyFilled, OrderLifecycleState.Unknown),
        new(OrderLifecycleState.PendingCancel, OrderLifecycleState.Working),
        new(OrderLifecycleState.PendingCancel, OrderLifecycleState.PartiallyFilled),
        new(OrderLifecycleState.PendingCancel, OrderLifecycleState.Filled),
        new(OrderLifecycleState.PendingCancel, OrderLifecycleState.Cancelled),
        new(OrderLifecycleState.PendingCancel, OrderLifecycleState.Rejected),
        new(OrderLifecycleState.PendingCancel, OrderLifecycleState.Expired),
        new(OrderLifecycleState.PendingCancel, OrderLifecycleState.Unknown),
        new(OrderLifecycleState.PendingReplace, OrderLifecycleState.Working),
        new(OrderLifecycleState.PendingReplace, OrderLifecycleState.PartiallyFilled),
        new(OrderLifecycleState.PendingReplace, OrderLifecycleState.Filled),
        new(OrderLifecycleState.PendingReplace, OrderLifecycleState.Cancelled),
        new(OrderLifecycleState.PendingReplace, OrderLifecycleState.Rejected),
        new(OrderLifecycleState.PendingReplace, OrderLifecycleState.Expired),
        new(OrderLifecycleState.PendingReplace, OrderLifecycleState.Unknown),
        new(OrderLifecycleState.Unknown, OrderLifecycleState.Reconciling),
        new(OrderLifecycleState.Reconciling, OrderLifecycleState.Working),
        new(OrderLifecycleState.Reconciling, OrderLifecycleState.PartiallyFilled),
        new(OrderLifecycleState.Reconciling, OrderLifecycleState.Filled),
        new(OrderLifecycleState.Reconciling, OrderLifecycleState.Cancelled),
        new(OrderLifecycleState.Reconciling, OrderLifecycleState.Rejected),
        new(OrderLifecycleState.Reconciling, OrderLifecycleState.Expired),
        new(OrderLifecycleState.Reconciling, OrderLifecycleState.Unknown),
        new(OrderLifecycleState.Reconciling, OrderLifecycleState.Reconciled),
        new(OrderLifecycleState.Filled, OrderLifecycleState.Reconciled),
        new(OrderLifecycleState.Cancelled, OrderLifecycleState.Reconciled),
        new(OrderLifecycleState.Rejected, OrderLifecycleState.Reconciled),
        new(OrderLifecycleState.Expired, OrderLifecycleState.Reconciled),
    ];

    private static readonly IReadOnlyList<OrderLifecycleTransition> ReadOnlyTransitions =
        Array.AsReadOnly(TransitionArray);

    private static readonly IReadOnlySet<OrderLifecycleTransition> TransitionSet =
        TransitionArray.ToHashSet();

    public static IReadOnlyList<OrderLifecycleTransition> LegalTransitions => ReadOnlyTransitions;

    public static bool IsTerminal(OrderLifecycleState state) => state is
        OrderLifecycleState.Filled or
        OrderLifecycleState.Cancelled or
        OrderLifecycleState.Rejected or
        OrderLifecycleState.Expired or
        OrderLifecycleState.Reconciled;

    public static bool CanTransition(OrderLifecycleState from, OrderLifecycleState to) =>
        Enum.IsDefined(from) && Enum.IsDefined(to) &&
        TransitionSet.Contains(new OrderLifecycleTransition(from, to));

    /// <summary>
    /// Binds each semantic event to the exact state edge it is allowed to cause. A structurally
    /// legal edge is still rejected when its declared cause cannot produce that transition.
    /// </summary>
    public static bool CanApplyEvent(
        OrderEventKind kind,
        OrderLifecycleState from,
        OrderLifecycleState to)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(from) || !Enum.IsDefined(to)) return false;

        if (from == to)
        {
            return kind switch
            {
                OrderEventKind.FillReceived => to is
                    OrderLifecycleState.PartiallyFilled or
                    OrderLifecycleState.PendingCancel or
                    OrderLifecycleState.PendingReplace,
                OrderEventKind.VenueAcknowledged => to is
                    OrderLifecycleState.Working or
                    OrderLifecycleState.PartiallyFilled or
                    OrderLifecycleState.PendingCancel or
                    OrderLifecycleState.PendingReplace or
                    OrderLifecycleState.Filled or
                    OrderLifecycleState.Cancelled or
                    OrderLifecycleState.Rejected or
                    OrderLifecycleState.Expired,
                OrderEventKind.RecoveryObserved => to == OrderLifecycleState.Prepared,
                OrderEventKind.StopMonitoringStarted or OrderEventKind.StopActivated => to is
                    OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled,
                OrderEventKind.CancelDispatchRecorded => to == OrderLifecycleState.PendingCancel,
                OrderEventKind.ReplaceDispatchRecorded => to == OrderLifecycleState.PendingReplace,
                OrderEventKind.ReplaceRiskAccepted or OrderEventKind.ReplaceRiskRejected =>
                    to is OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled,
                OrderEventKind.CommissionObserved or OrderEventKind.PositionObserved => to is
                    OrderLifecycleState.Acknowledging or
                    OrderLifecycleState.Working or
                    OrderLifecycleState.PartiallyFilled or
                    OrderLifecycleState.PendingCancel or
                    OrderLifecycleState.PendingReplace or
                    OrderLifecycleState.Filled or
                    OrderLifecycleState.Cancelled or
                    OrderLifecycleState.Rejected or
                    OrderLifecycleState.Expired or
                    OrderLifecycleState.Unknown or
                    OrderLifecycleState.Reconciling or
                    OrderLifecycleState.Reconciled,
                _ => false,
            };
        }

        if (!CanTransition(from, to)) return false;

        return kind switch
        {
            OrderEventKind.RiskAccepted =>
                from == OrderLifecycleState.Draft && to == OrderLifecycleState.Validated,
            OrderEventKind.RiskRejected =>
                from == OrderLifecycleState.Draft && to == OrderLifecycleState.Rejected,
            OrderEventKind.ValidationRejected =>
                from is OrderLifecycleState.Draft or OrderLifecycleState.Validated or OrderLifecycleState.Prepared &&
                to == OrderLifecycleState.Rejected,
            OrderEventKind.Prepared =>
                from == OrderLifecycleState.Validated && to == OrderLifecycleState.Prepared,
            OrderEventKind.Armed =>
                from == OrderLifecycleState.Prepared && to == OrderLifecycleState.Armed,
            OrderEventKind.SendStarted =>
                from == OrderLifecycleState.Armed && to == OrderLifecycleState.Releasing,
            OrderEventKind.SendFailedBeforeAcceptance =>
                from == OrderLifecycleState.Releasing && to == OrderLifecycleState.Armed,
            OrderEventKind.SubmissionRecorded =>
                from == OrderLifecycleState.Releasing && to == OrderLifecycleState.Acknowledging,
            OrderEventKind.VenueAcknowledged =>
                from is OrderLifecycleState.Acknowledging or OrderLifecycleState.Reconciling &&
                to == OrderLifecycleState.Working,
            OrderEventKind.FillReceived =>
                from is OrderLifecycleState.Acknowledging or
                    OrderLifecycleState.Working or
                    OrderLifecycleState.PartiallyFilled or
                    OrderLifecycleState.PendingCancel or
                    OrderLifecycleState.PendingReplace or
                    OrderLifecycleState.Reconciling &&
                to is OrderLifecycleState.PartiallyFilled or OrderLifecycleState.Filled,
            OrderEventKind.CancelRequested =>
                from is OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled &&
                to == OrderLifecycleState.PendingCancel,
            OrderEventKind.CancelConfirmed =>
                from is OrderLifecycleState.Armed or
                    OrderLifecycleState.Acknowledging or
                    OrderLifecycleState.Working or
                    OrderLifecycleState.PartiallyFilled or
                    OrderLifecycleState.PendingCancel or
                    OrderLifecycleState.PendingReplace or
                    OrderLifecycleState.Reconciling &&
                to == OrderLifecycleState.Cancelled,
            OrderEventKind.ReplaceRequested =>
                from is OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled &&
                to == OrderLifecycleState.PendingReplace,
            OrderEventKind.ReplaceConfirmed =>
                from == OrderLifecycleState.PendingReplace &&
                to is OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled,
            OrderEventKind.VenueRejected =>
                from is OrderLifecycleState.Releasing or
                    OrderLifecycleState.Acknowledging or
                    OrderLifecycleState.Working or
                    OrderLifecycleState.PendingCancel or
                    OrderLifecycleState.PendingReplace or
                    OrderLifecycleState.Reconciling &&
                to == OrderLifecycleState.Rejected,
            OrderEventKind.Expired =>
                from is OrderLifecycleState.Prepared or
                    OrderLifecycleState.Armed or
                    OrderLifecycleState.Acknowledging or
                    OrderLifecycleState.Working or
                    OrderLifecycleState.PartiallyFilled or
                    OrderLifecycleState.PendingCancel or
                    OrderLifecycleState.PendingReplace or
                    OrderLifecycleState.Reconciling &&
                to == OrderLifecycleState.Expired,
            OrderEventKind.OutcomeUnknown =>
                from is OrderLifecycleState.Releasing or
                    OrderLifecycleState.Acknowledging or
                    OrderLifecycleState.Working or
                    OrderLifecycleState.PartiallyFilled or
                    OrderLifecycleState.PendingCancel or
                    OrderLifecycleState.PendingReplace or
                    OrderLifecycleState.Reconciling &&
                to == OrderLifecycleState.Unknown,
            OrderEventKind.ReconciliationStarted =>
                from == OrderLifecycleState.Unknown && to == OrderLifecycleState.Reconciling,
            OrderEventKind.Reconciled =>
                from is OrderLifecycleState.Reconciling or
                    OrderLifecycleState.Filled or
                    OrderLifecycleState.Cancelled or
                    OrderLifecycleState.Rejected or
                    OrderLifecycleState.Expired &&
                to == OrderLifecycleState.Reconciled,
            OrderEventKind.RecoveryObserved =>
                from is OrderLifecycleState.PendingCancel or OrderLifecycleState.PendingReplace &&
                to is OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled,
            _ => false,
        };
    }

    /// <summary>Prevents one subsystem from impersonating another in the event ledger.</summary>
    public static bool IsEventSourceAllowed(OrderEventKind kind, OrderEventSource source)
    {
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(source)) return false;

        return kind switch
        {
            OrderEventKind.DraftCreated or
            OrderEventKind.ValidationRejected or
            OrderEventKind.Prepared or
            OrderEventKind.Armed or
            OrderEventKind.SendStarted or
            OrderEventKind.SubmissionRecorded or
            OrderEventKind.CancelRequested or
            OrderEventKind.ReplaceRequested or
            OrderEventKind.CancelDispatchRecorded or
            OrderEventKind.ReplaceDispatchRecorded => source == OrderEventSource.Command,
            OrderEventKind.RiskAccepted or
            OrderEventKind.RiskRejected or
            OrderEventKind.ReplaceRiskAccepted or
            OrderEventKind.ReplaceRiskRejected => source == OrderEventSource.Risk,
            OrderEventKind.SendFailedBeforeAcceptance or
            OrderEventKind.VenueAcknowledged or
            OrderEventKind.StopMonitoringStarted or
            OrderEventKind.StopActivated or
            OrderEventKind.FillReceived or
            OrderEventKind.CancelConfirmed or
            OrderEventKind.ReplaceConfirmed or
            OrderEventKind.VenueRejected or
            OrderEventKind.Expired => source == OrderEventSource.SimulatedVenue,
            OrderEventKind.OutcomeUnknown =>
                source is OrderEventSource.SimulatedVenue or OrderEventSource.Recovery,
            OrderEventKind.ReconciliationStarted or OrderEventKind.Reconciled =>
                source == OrderEventSource.Reconciliation,
            OrderEventKind.RecoveryObserved =>
                source is OrderEventSource.Recovery or OrderEventSource.SimulatedVenue,
            OrderEventKind.CommissionObserved or OrderEventKind.PositionObserved =>
                source == OrderEventSource.SimulatedVenue,
            _ => false,
        };
    }

    public static bool BlocksRetry(OrderLifecycleState state) =>
        state is OrderLifecycleState.Unknown or OrderLifecycleState.Reconciling;

    /// <summary>Returns <paramref name="to"/> for a legal edge; otherwise throws without mutation.</summary>
    public static OrderLifecycleState Transition(OrderLifecycleState from, OrderLifecycleState to)
    {
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"Illegal order lifecycle transition: {from} -> {to}.");
        return to;
    }
}
