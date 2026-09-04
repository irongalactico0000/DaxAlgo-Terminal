using TradingTerminal.Core.Time;

namespace TradingTerminal.Core.Execution;

/// <summary>Explicit causation and inbox identity shared by every event from one OMS command.</summary>
public readonly record struct OrderCommandContext(
    CausationId CausationId,
    DeduplicationKey DeduplicationKey)
{
    public bool IsValid => !CausationId.IsEmpty && !DeduplicationKey.IsEmpty;
}

public enum OmsCommandFault : byte
{
    None = 0,
    InvalidCommand = 1,
    OrderAlreadyExists = 2,
    OrderNotFound = 3,
    StaleOrderSequence = 4,
    IllegalTransition = 5,
    RiskRejected = 6,
    PersistenceRejected = 7,
    DuplicateConflict = 8,
    DispatchRejectedBeforeAcceptance = 9,
    DispatchOutcomeUnknown = 10,
    RetryBlockedUnknown = 11,
    InvalidVenueEvent = 12,
    ExecutionLeaseRejected = 13,
    ReconciliationRequired = 14,
}

public readonly record struct OmsCommandResult(
    OmsCommandFault Fault,
    OmsOrderProjection? Projection,
    RiskDecision? RiskDecision = null,
    string? Reason = null)
{
    public bool IsSuccess => Fault == OmsCommandFault.None && Projection is not null;
    public bool CanRetrySameClientOrderId =>
        Fault == OmsCommandFault.DispatchRejectedBeforeAcceptance &&
        Projection?.State == OrderLifecycleState.Armed;
}

/// <summary>
/// Paper OMS coordinator. It composes the existing Mac execution commands and risk policy with the
/// canonical event ledger and a deterministic Paper dispatcher; no live adapter can be supplied.
/// </summary>
public sealed class OrderManagementService
{
    private readonly IOrderEventStore _eventStore;
    private readonly IPaperExecutionDispatcher _dispatcher;
    private readonly IExecutionLeaseValidator _leaseValidator;
    private readonly IExecutionReconciliationAdmissionGate? _reconciliationGate;
    private readonly IClock _clock;

    public OrderManagementService(
        IOrderEventStore eventStore,
        IPaperExecutionDispatcher dispatcher,
        IExecutionLeaseValidator leaseValidator,
        IClock clock,
        IExecutionReconciliationAdmissionGate? reconciliationGate = null)
    {
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _leaseValidator = leaseValidator ?? throw new ArgumentNullException(nameof(leaseValidator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _reconciliationGate = reconciliationGate;
    }

    public bool CanAdmitNewOrders =>
        _eventStore is not IExecutionAdmissionGate gate || gate.CanAdmitNewOrders;

    /// <summary>Risk-admits, durably stages, and dispatches one new Paper order.</summary>
    public OmsCommandResult Submit(
        SubmitOrderCommand command,
        RiskEvaluationContext riskContext,
        in OrderCommandContext context)
    {
        if (command is null || riskContext is null || !context.IsValid)
            return Failed(OmsCommandFault.InvalidCommand, "Submit command, risk context, and command context are required.");
        if (!CanAdmitNewOrders)
            return Failed(OmsCommandFault.RetryBlockedUnknown, "The ledger recovery gate blocks new-order admission.");
        var reconciliation = ValidateReconciliationAdmission(command.Metadata);
        if (reconciliation.Fault != OmsCommandFault.None) return reconciliation;

        var existing = _eventStore.ReadProjection(command.ClientOrderId);
        if (existing is not null)
        {
            return existing.SubmitCommand == command
                ? new OmsCommandResult(OmsCommandFault.None, existing)
                : Failed(OmsCommandFault.OrderAlreadyExists, "The client order id already belongs to different economics.", existing);
        }

        var now = UtcNow();
        var draft = Append(
            new OrderEventDraft(
                command.ClientOrderId,
                OrderEventKind.DraftCreated,
                OrderLifecycleState.Draft,
                OrderEventSource.Command,
                Derive(context, "draft"),
                now,
                context.CausationId,
                SubmitCommand: command),
            now);
        if (!draft.IsSuccess) return draft;

        var decision = RiskPolicy.Evaluate(command, riskContext);
        var riskObservation = OrderRiskObservation.Capture(command, decision, riskContext);
        var risk = Append(
            new OrderEventDraft(
                command.ClientOrderId,
                decision.IsAllowed ? OrderEventKind.RiskAccepted : OrderEventKind.RiskRejected,
                decision.IsAllowed ? OrderLifecycleState.Validated : OrderLifecycleState.Rejected,
                OrderEventSource.Risk,
                Derive(context, "risk"),
                now,
                context.CausationId,
                RiskObservation: riskObservation,
                Reason: decision.Reason),
            now,
            decision);
        if (!risk.IsSuccess) return risk;
        if (!decision.IsAllowed)
            return new OmsCommandResult(OmsCommandFault.RiskRejected, risk.Projection, decision, decision.Reason);

        var prepared = AppendSimple(
            command.ClientOrderId,
            OrderEventKind.Prepared,
            OrderLifecycleState.Prepared,
            OrderEventSource.Command,
            context,
            "prepared",
            now,
            decision);
        if (!prepared.IsSuccess) return prepared;

        var armed = AppendSimple(
            command.ClientOrderId,
            OrderEventKind.Armed,
            OrderLifecycleState.Armed,
            OrderEventSource.Command,
            context,
            "armed",
            now,
            decision);
        if (!armed.IsSuccess) return armed;

        return DispatchSubmit(command, context, decision);
    }

    /// <summary>Cancels a fillable Paper order; duplicate commands are deduped by the event store.</summary>
    public OmsCommandResult Cancel(
        CancelOrderCommand command,
        in OrderCommandContext context)
    {
        if (command is null || !context.IsValid)
            return Failed(OmsCommandFault.InvalidCommand, "Cancel command and command context are required.");
        var current = ValidateExisting(command, allowUnknown: false);
        if (!current.IsSuccess) return current;
        if (current.Projection!.State is not (OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled))
            return Failed(OmsCommandFault.IllegalTransition, "Only working or partially-filled orders can be cancelled.", current.Projection);
        var lease = ValidateDispatchLease(current.Projection);
        if (!lease.IsSuccess) return lease;

        var previousState = current.Projection.State;
        var now = UtcNow();
        var pending = AppendSimple(
            current.Projection.ClientOrderId,
            OrderEventKind.CancelRequested,
            OrderLifecycleState.PendingCancel,
            OrderEventSource.Command,
            context,
            "cancel-requested",
            now);
        if (!pending.IsSuccess) return pending;

        ExecutionDispatchResult dispatch;
        try
        {
            dispatch = _dispatcher.Cancel(command, pending.Projection!);
        }
        catch (Exception exception)
        {
            dispatch = ExecutionDispatchResult.Unknown($"Paper cancel dispatch threw {exception.GetType().Name}.");
        }

        return ResolvePendingDispatch(
            pending.Projection!,
            previousState,
            dispatch,
            context,
            "cancel");
    }

    /// <summary>Freshly risk-admits changed terms, then requests one Paper replacement.</summary>
    public OmsCommandResult Replace(
        ReplaceOrderCommand command,
        RiskEvaluationContext riskContext,
        in OrderCommandContext context)
    {
        if (command is null || riskContext is null || !context.IsValid)
            return Failed(OmsCommandFault.InvalidCommand, "Replace command, risk context, and command context are required.");
        var current = ValidateExisting(command, allowUnknown: false);
        if (!current.IsSuccess) return current;
        if (current.Projection!.State is not (OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled))
            return Failed(OmsCommandFault.IllegalTransition, "Only working or partially-filled orders can be replaced.", current.Projection);
        var reconciliation = ValidateReconciliationAdmission(current.Projection.SubmitCommand.Metadata, current.Projection);
        if (reconciliation.Fault != OmsCommandFault.None) return reconciliation;
        var lease = ValidateDispatchLease(current.Projection);
        if (!lease.IsSuccess) return lease;

        var previousState = current.Projection.State;
        var now = UtcNow();
        var decision = RiskPolicy.Evaluate(command, riskContext);
        var observation = OrderRiskObservation.Capture(command, decision, riskContext);
        var risk = Append(
            new OrderEventDraft(
                current.Projection.ClientOrderId,
                decision.IsAllowed ? OrderEventKind.ReplaceRiskAccepted : OrderEventKind.ReplaceRiskRejected,
                previousState,
                OrderEventSource.Risk,
                Derive(context, "replace-risk"),
                now,
                context.CausationId,
                RiskObservation: observation,
                ReplacementTerms: command.ReplacementTerms,
                Reason: decision.Reason),
            now,
            decision);
        if (!risk.IsSuccess) return risk;
        if (!decision.IsAllowed)
            return new OmsCommandResult(OmsCommandFault.RiskRejected, risk.Projection, decision, decision.Reason);

        var pending = Append(
            new OrderEventDraft(
                current.Projection.ClientOrderId,
                OrderEventKind.ReplaceRequested,
                OrderLifecycleState.PendingReplace,
                OrderEventSource.Command,
                Derive(context, "replace-requested"),
                now,
                context.CausationId,
                ReplacementTerms: command.ReplacementTerms),
            now,
            decision);
        if (!pending.IsSuccess) return pending;

        ExecutionDispatchResult dispatch;
        try
        {
            dispatch = _dispatcher.Replace(command, pending.Projection!);
        }
        catch (Exception exception)
        {
            dispatch = ExecutionDispatchResult.Unknown($"Paper replace dispatch threw {exception.GetType().Name}.");
        }

        return ResolvePendingDispatch(
            pending.Projection!,
            previousState,
            dispatch,
            context,
            "replace",
            decision);
    }

    public OmsOrderProjection? Query(ClientOrderId clientOrderId) =>
        clientOrderId.IsEmpty ? null : _eventStore.ReadProjection(clientOrderId);

    /// <summary>
    /// Applies every callback queued behind the dispatch-receipt barrier. Each callback has its own
    /// inbox identity, so replay is idempotent and conflicting reuse fails closed.
    /// </summary>
    public IReadOnlyList<OmsCommandResult> ProcessVenueEvents()
    {
        var venueEvents = _dispatcher.DrainEvents();
        if (venueEvents.Count == 0) return Array.Empty<OmsCommandResult>();

        var results = new OmsCommandResult[venueEvents.Count];
        for (var index = 0; index < venueEvents.Count; index++)
            results[index] = ApplyVenueEvent(venueEvents[index]);
        return Array.AsReadOnly(results);
    }

    /// <summary>Translates one Paper callback into its exact immutable OMS fact.</summary>
    public OmsCommandResult ApplyVenueEvent(PaperVenueEvent venueEvent)
    {
        if (!IsValidVenueEvent(venueEvent))
            return Failed(OmsCommandFault.InvalidVenueEvent, "The Paper venue callback is malformed.");

        var current = _eventStore.ReadProjection(venueEvent.ClientOrderId);
        if (current is null)
            return Failed(OmsCommandFault.OrderNotFound, "The Paper callback references an unknown order.");

        var kind = venueEvent.Kind switch
        {
            PaperVenueEventKind.Acknowledged => OrderEventKind.VenueAcknowledged,
            PaperVenueEventKind.Fill => OrderEventKind.FillReceived,
            PaperVenueEventKind.Cancelled => OrderEventKind.CancelConfirmed,
            PaperVenueEventKind.Replaced => OrderEventKind.ReplaceConfirmed,
            PaperVenueEventKind.Rejected => OrderEventKind.VenueRejected,
            PaperVenueEventKind.Expired => OrderEventKind.Expired,
            PaperVenueEventKind.StopMonitoringStarted => OrderEventKind.StopMonitoringStarted,
            PaperVenueEventKind.StopActivated => OrderEventKind.StopActivated,
            _ => throw new ArgumentOutOfRangeException(nameof(venueEvent)),
        };
        var stateAfter = ResolveVenueState(venueEvent, current);
        if (stateAfter is null || !OrderLifecycle.CanApplyEvent(kind, current.State, stateAfter.Value))
        {
            return Failed(
                OmsCommandFault.InvalidVenueEvent,
                $"Paper callback {venueEvent.Kind} cannot apply to {current.State}.",
                current);
        }

        var result = Append(
            new OrderEventDraft(
                venueEvent.ClientOrderId,
                kind,
                stateAfter.Value,
                OrderEventSource.SimulatedVenue,
                VenueDeduplicationKey(venueEvent.EventId),
                venueEvent.OccurredAtUtc,
                venueEvent.CausationId,
                BrokerOrderId: venueEvent.BrokerOrderId,
                ExchangeOrderId: venueEvent.ExchangeOrderId,
                Fill: venueEvent.Fill,
                ReplacementTerms: venueEvent.ReplacementTerms,
                Reason: venueEvent.Reason),
            UtcNow());
        return result.IsSuccess
            ? result
            : new OmsCommandResult(
                result.Fault == OmsCommandFault.IllegalTransition
                    ? OmsCommandFault.InvalidVenueEvent
                    : result.Fault,
                result.Projection,
                result.RiskDecision,
                result.Reason);
    }

    private OmsCommandResult DispatchSubmit(
        SubmitOrderCommand command,
        in OrderCommandContext context,
        RiskDecision decision)
    {
        var current = _eventStore.ReadProjection(command.ClientOrderId);
        if (current is null)
            return Failed(OmsCommandFault.OrderNotFound, "The armed order disappeared before dispatch.");
        var lease = ValidateDispatchLease(current);
        if (!lease.IsSuccess) return lease;

        var now = UtcNow();
        var releasing = AppendSimple(
            command.ClientOrderId,
            OrderEventKind.SendStarted,
            OrderLifecycleState.Releasing,
            OrderEventSource.Command,
            context,
            "send-started",
            now,
            decision);
        if (!releasing.IsSuccess) return releasing;

        ExecutionDispatchResult dispatch;
        try
        {
            dispatch = _dispatcher.Submit(command, releasing.Projection!);
        }
        catch (Exception exception)
        {
            dispatch = ExecutionDispatchResult.Unknown($"Paper submit dispatch threw {exception.GetType().Name}.");
        }

        var dispatchResult = dispatch.Status switch
        {
            ExecutionDispatchStatus.Dispatched => Append(
                new OrderEventDraft(
                    command.ClientOrderId,
                    OrderEventKind.SubmissionRecorded,
                    OrderLifecycleState.Acknowledging,
                    OrderEventSource.Command,
                    Derive(context, "submission-recorded"),
                    dispatch.Receipt!.DispatchedAtUtc,
                    context.CausationId,
                    BrokerOrderId: dispatch.Receipt.BrokerOrderId,
                    ExchangeOrderId: dispatch.Receipt.ExchangeOrderId),
                UtcNow(),
                decision),
            ExecutionDispatchStatus.RejectedBeforeDispatch => WithFault(
                AppendSimple(
                    command.ClientOrderId,
                    OrderEventKind.SendFailedBeforeAcceptance,
                    OrderLifecycleState.Armed,
                    OrderEventSource.SimulatedVenue,
                    context,
                    "send-rejected",
                    UtcNow(),
                    decision,
                    dispatch.Reason),
                OmsCommandFault.DispatchRejectedBeforeAcceptance,
                dispatch.Reason),
            _ => WithFault(
                AppendSimple(
                    command.ClientOrderId,
                    OrderEventKind.OutcomeUnknown,
                    OrderLifecycleState.Unknown,
                    OrderEventSource.SimulatedVenue,
                    context,
                    "send-unknown",
                    UtcNow(),
                    decision,
                    dispatch.Reason),
                OmsCommandFault.DispatchOutcomeUnknown,
                dispatch.Reason),
        };
        if (dispatch.Status != ExecutionDispatchStatus.Dispatched || !dispatchResult.IsSuccess)
            return dispatchResult;

        return ProcessVenueEventsFor(command.ClientOrderId, dispatchResult);
    }

    private OmsCommandResult ResolvePendingDispatch(
        OmsOrderProjection pending,
        OrderLifecycleState previousState,
        ExecutionDispatchResult dispatch,
        in OrderCommandContext context,
        string operation,
        RiskDecision? decision = null)
    {
        if (dispatch.Status == ExecutionDispatchStatus.Dispatched)
        {
            var receipt = dispatch.Receipt!;
            var dispatchRecorded = Append(
                new OrderEventDraft(
                    pending.ClientOrderId,
                    operation == "cancel"
                        ? OrderEventKind.CancelDispatchRecorded
                        : OrderEventKind.ReplaceDispatchRecorded,
                    pending.State,
                    OrderEventSource.Command,
                    Derive(context, $"{operation}-dispatch-recorded"),
                    receipt.DispatchedAtUtc,
                    context.CausationId,
                    BrokerOrderId: receipt.BrokerOrderId,
                    ExchangeOrderId: receipt.ExchangeOrderId,
                    DispatchReceipt: receipt),
                UtcNow(),
                decision);
            if (!dispatchRecorded.IsSuccess)
                return dispatchRecorded;

            return ProcessVenueEventsFor(
                pending.ClientOrderId,
                dispatchRecorded);
        }

        if (dispatch.Status == ExecutionDispatchStatus.RejectedBeforeDispatch)
        {
            var restored = AppendSimple(
                pending.ClientOrderId,
                OrderEventKind.RecoveryObserved,
                previousState,
                OrderEventSource.Recovery,
                context,
                $"{operation}-rejected",
                UtcNow(),
                decision,
                dispatch.Reason);
            return WithFault(restored, OmsCommandFault.DispatchRejectedBeforeAcceptance, dispatch.Reason);
        }

        var unknown = AppendSimple(
            pending.ClientOrderId,
            OrderEventKind.OutcomeUnknown,
            OrderLifecycleState.Unknown,
            OrderEventSource.SimulatedVenue,
            context,
            $"{operation}-unknown",
            UtcNow(),
            decision,
            dispatch.Reason);
        return WithFault(unknown, OmsCommandFault.DispatchOutcomeUnknown, dispatch.Reason);
    }

    private OmsCommandResult ValidateExisting(ExecutionCommand command, bool allowUnknown)
    {
        var projection = command switch
        {
            CancelOrderCommand or ReplaceOrderCommand => FindProjection(command.OrderId),
            _ => null,
        };
        if (projection is null)
            return Failed(OmsCommandFault.OrderNotFound, "No order matches the command order id.");
        if (command.Metadata.ExpectedOrderSequence != projection.LastSequence)
            return Failed(OmsCommandFault.StaleOrderSequence, "The command expected a stale order sequence.", projection);
        if (projection.State is OrderLifecycleState.Unknown or OrderLifecycleState.Reconciling && !allowUnknown)
            return Failed(OmsCommandFault.RetryBlockedUnknown, "Unknown or reconciling orders require explicit reconciliation.", projection);
        if (command.Metadata.TradingAccountId != projection.SubmitCommand.Metadata.TradingAccountId ||
            command.Metadata.VenueId != projection.SubmitCommand.Metadata.VenueId ||
            command.Metadata.InstrumentId != projection.SubmitCommand.Metadata.InstrumentId ||
            command.Metadata.Environment != projection.SubmitCommand.Metadata.Environment)
        {
            return Failed(OmsCommandFault.InvalidCommand, "The command routing identity differs from the original order.", projection);
        }
        return new OmsCommandResult(OmsCommandFault.None, projection);
    }

    private OmsCommandResult ValidateDispatchLease(OmsOrderProjection projection)
    {
        var metadata = projection.SubmitCommand.Metadata;
        var identity = projection.Instruction.Identity;
        var claim = new ExecutionLeaseClaim(
            new ExecutionResource(metadata.VenueId, metadata.TradingAccountId, metadata.Environment),
            identity.ExecutionLeaseId,
            identity.FencingToken);
        var validation = _leaseValidator.Validate(claim, UtcNow());
        return validation.IsSuccess
            ? new OmsCommandResult(OmsCommandFault.None, projection)
            : Failed(
                OmsCommandFault.ExecutionLeaseRejected,
                $"Execution lease rejected: {validation.Fault}. {validation.Reason}",
                projection);
    }

    private OmsCommandResult ValidateReconciliationAdmission(
        ExecutionCommandMetadata metadata,
        OmsOrderProjection? projection = null)
    {
        if (_reconciliationGate is null) return new OmsCommandResult(OmsCommandFault.None, projection);
        var resource = new ExecutionResource(metadata.VenueId, metadata.TradingAccountId, metadata.Environment);
        return _reconciliationGate.CanAdmitNewExposure(resource)
            ? new OmsCommandResult(OmsCommandFault.None, projection)
            : Failed(
                OmsCommandFault.ReconciliationRequired,
                "Unresolved broker-versus-ledger reconciliation evidence blocks new exposure.",
                projection);
    }

    private OmsCommandResult ProcessVenueEventsFor(
        ClientOrderId clientOrderId,
        OmsCommandResult fallback)
    {
        var results = ProcessVenueEvents();
        foreach (var result in results)
        {
            if (!result.IsSuccess)
                return result;
        }

        var projection = _eventStore.ReadProjection(clientOrderId);
        return projection is null
            ? fallback
            : new OmsCommandResult(
                OmsCommandFault.None,
                projection,
                fallback.RiskDecision,
                fallback.Reason);
    }

    private static OrderLifecycleState? ResolveVenueState(
        PaperVenueEvent venueEvent,
        OmsOrderProjection current) =>
        venueEvent.Kind switch
        {
            PaperVenueEventKind.Acknowledged => current.State == OrderLifecycleState.Acknowledging
                ? OrderLifecycleState.Working
                : current.State,
            PaperVenueEventKind.Fill when venueEvent.Fill is not null =>
                ResolveFillState(current, venueEvent.Fill),
            PaperVenueEventKind.Cancelled => OrderLifecycleState.Cancelled,
            PaperVenueEventKind.Replaced => current.FilledQuantity.Coefficient > 0
                ? OrderLifecycleState.PartiallyFilled
                : OrderLifecycleState.Working,
            PaperVenueEventKind.Rejected => OrderLifecycleState.Rejected,
            PaperVenueEventKind.Expired => OrderLifecycleState.Expired,
            PaperVenueEventKind.StopMonitoringStarted or PaperVenueEventKind.StopActivated => current.State,
            _ => null,
        };

    private static OrderLifecycleState? ResolveFillState(
        OmsOrderProjection current,
        OrderFill fill)
    {
        if (!ScaledValueMath.TryAddQuantity(current.FilledQuantity, fill.Quantity, out var nextQuantity) ||
            !ScaledValueMath.TryComparePositive(
                nextQuantity.Coefficient,
                nextQuantity.Scale,
                current.Terms.Quantity.Coefficient,
                current.Terms.Quantity.Scale,
                out var comparison))
            return null;

        if (comparison >= 0)
            return OrderLifecycleState.Filled;
        return current.State switch
        {
            OrderLifecycleState.PendingCancel => OrderLifecycleState.PendingCancel,
            OrderLifecycleState.PendingReplace => OrderLifecycleState.PendingReplace,
            _ => OrderLifecycleState.PartiallyFilled,
        };
    }

    private static DeduplicationKey VenueDeduplicationKey(ExecutionEventId eventId) =>
        new($"paper-venue:{ExecutionCanonicalJson.Sha256(eventId.Value)}");

    private static bool IsValidVenueEvent(PaperVenueEvent? venueEvent)
    {
        if (venueEvent is null ||
            venueEvent.EventId.IsEmpty ||
            venueEvent.ClientOrderId.IsEmpty ||
            venueEvent.CausationId.IsEmpty ||
            !Enum.IsDefined(venueEvent.Kind) ||
            venueEvent.OccurredAtUtc.Offset != TimeSpan.Zero)
        {
            return false;
        }

        if ((venueEvent.Fill is not null) != (venueEvent.Kind == PaperVenueEventKind.Fill))
            return false;
        if ((venueEvent.ReplacementTerms is not null) !=
            (venueEvent.Kind == PaperVenueEventKind.Replaced))
        {
            return false;
        }
        return true;
    }

    private OmsOrderProjection? FindProjection(OrderId orderId)
    {
        foreach (var entry in _eventStore.ReadOutbox())
        {
            var projection = _eventStore.ReadProjection(entry.Event.AggregateId);
            if (projection?.OrderId == orderId)
                return projection;
        }
        return null;
    }

    private OmsCommandResult AppendSimple(
        ClientOrderId aggregateId,
        OrderEventKind kind,
        OrderLifecycleState stateAfter,
        OrderEventSource source,
        in OrderCommandContext context,
        string dedupeSuffix,
        DateTimeOffset occurredAtUtc,
        RiskDecision? decision = null,
        string? reason = null) =>
        Append(
            new OrderEventDraft(
                aggregateId,
                kind,
                stateAfter,
                source,
                Derive(context, dedupeSuffix),
                occurredAtUtc,
                context.CausationId,
                Reason: reason),
            UtcNow(),
            decision);

    private OmsCommandResult Append(
        OrderEventDraft draft,
        DateTimeOffset recordedAtUtc,
        RiskDecision? decision = null)
    {
        var result = _eventStore.Append(draft, recordedAtUtc);
        if (!result.IsSuccess)
        {
            var fault = result.Fault == OrderEventAppendFault.ConflictingDuplicate
                ? OmsCommandFault.DuplicateConflict
                : result.Fault == OrderEventAppendFault.IllegalTransition
                    ? OmsCommandFault.IllegalTransition
                    : OmsCommandFault.PersistenceRejected;
            return Failed(fault, $"Event append failed: {result.Fault}/{result.ProjectionFault}.");
        }
        return new OmsCommandResult(
            OmsCommandFault.None,
            _eventStore.ReadProjection(draft.AggregateId),
            decision);
    }

    private static OmsCommandResult WithFault(
        OmsCommandResult result,
        OmsCommandFault fault,
        string? reason) =>
        result.IsSuccess
            ? new OmsCommandResult(fault, result.Projection, result.RiskDecision, reason)
            : result;

    private static OmsCommandResult Failed(
        OmsCommandFault fault,
        string reason,
        OmsOrderProjection? projection = null) =>
        new(fault, projection, null, reason);

    private static DeduplicationKey Derive(
        in OrderCommandContext context,
        string suffix) =>
        context.DeduplicationKey.Derive(suffix);

    private DateTimeOffset UtcNow()
    {
        var value = _clock.UtcNow;
        if (value.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("The OMS clock must return DateTimeKind.Utc values.");
        return new DateTimeOffset(value);
    }
}
