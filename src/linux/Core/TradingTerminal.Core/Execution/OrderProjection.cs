using TradingTerminal.Core.Trading;

namespace TradingTerminal.Core.Execution;

public enum OrderEventChainFault : byte
{
    None = 0,
    MissingEvents = 1,
    InvalidAggregate = 2,
    SequenceGap = 3,
    InvalidInitialEvent = 4,
    StateBeforeMismatch = 5,
    IllegalTransition = 6,
    InvalidEventSource = 7,
    PreviousHashMismatch = 8,
    EventHashMismatch = 9,
    InvalidTimestamp = 10,
}

public readonly record struct OrderEventChainVerification(
    OrderEventChainFault Fault,
    int EventIndex)
{
    public bool IsValid => Fault == OrderEventChainFault.None;
}

/// <summary>Verifies aggregate identity, sequence, lifecycle cause, timestamps, and hash binding.</summary>
public static class OrderEventChainVerifier
{
    public static OrderEventChainVerification Verify(IReadOnlyList<OmsOrderEvent>? events)
    {
        if (events is null || events.Count == 0)
            return Failed(OrderEventChainFault.MissingEvents, -1);

        var aggregateId = events[0].AggregateId;
        if (aggregateId.IsEmpty)
            return Failed(OrderEventChainFault.InvalidAggregate, 0);

        OmsOrderEvent? previous = null;
        for (var index = 0; index < events.Count; index++)
        {
            var current = events[index];
            if (current.AggregateId != aggregateId)
                return Failed(OrderEventChainFault.InvalidAggregate, index);
            if (current.AggregateSequence != index + 1L)
                return Failed(OrderEventChainFault.SequenceGap, index);
            if (current.RecordedAtUtc.Offset != TimeSpan.Zero ||
                current.OccurredAtUtc.Offset != TimeSpan.Zero ||
                current.RecordedAtUtc < current.OccurredAtUtc ||
                (previous is not null && current.RecordedAtUtc < previous.RecordedAtUtc))
            {
                return Failed(OrderEventChainFault.InvalidTimestamp, index);
            }
            if (!OrderLifecycle.IsEventSourceAllowed(current.Kind, current.Source))
                return Failed(OrderEventChainFault.InvalidEventSource, index);

            if (previous is null)
            {
                if (current.Kind != OrderEventKind.DraftCreated ||
                    current.StateBefore is not null ||
                    current.StateAfter != OrderLifecycleState.Draft ||
                    current.PreviousEventHash.Length != 0)
                {
                    return Failed(OrderEventChainFault.InvalidInitialEvent, index);
                }
            }
            else
            {
                if (current.StateBefore != previous.StateAfter)
                    return Failed(OrderEventChainFault.StateBeforeMismatch, index);
                if (!OrderLifecycle.CanApplyEvent(current.Kind, previous.StateAfter, current.StateAfter))
                    return Failed(OrderEventChainFault.IllegalTransition, index);
                if (!string.Equals(current.PreviousEventHash, previous.EventHash, StringComparison.Ordinal))
                    return Failed(OrderEventChainFault.PreviousHashMismatch, index);
            }

            if (!string.Equals(
                    current.EventHash,
                    OmsOrderEventHash.Compute(current),
                    StringComparison.Ordinal))
            {
                return Failed(OrderEventChainFault.EventHashMismatch, index);
            }

            previous = current;
        }

        return new OrderEventChainVerification(OrderEventChainFault.None, -1);
    }

    private static OrderEventChainVerification Failed(OrderEventChainFault fault, int index) =>
        new(fault, index);
}

/// <summary>
/// Durable Paper stop-trigger state. <see cref="Unknown"/> means the event stream predates explicit
/// monitoring evidence and cannot safely prove whether a zero-fill stop already activated.
/// </summary>
public enum OrderStopActivationState : byte
{
    NotApplicable = 0,
    Unknown = 1,
    Monitoring = 2,
    Activated = 3,
}

internal readonly record struct OrderStopActivationProjectionResult(
    bool IsSuccess,
    OrderStopActivationState State,
    int EventIndex);

internal static class OrderStopActivationProjector
{
    internal static OrderStopActivationProjectionResult Rebuild(IReadOnlyList<OmsOrderEvent> events)
    {
        OrderTerms? terms = null;
        var state = OrderStopActivationState.NotApplicable;
        for (var index = 0; index < events.Count; index++)
        {
            var orderEvent = events[index];
            if (orderEvent.SubmitCommand is { } submit)
            {
                terms = submit.Terms;
                state = Initial(terms);
            }
            if (orderEvent.Kind == OrderEventKind.ReplaceConfirmed &&
                orderEvent.ReplacementTerms is { } replacement)
            {
                terms = replacement;
                state = Initial(terms);
            }
            if (terms is null || !TryApply(orderEvent.Kind, terms, ref state))
                return new OrderStopActivationProjectionResult(false, state, index);
        }
        return new OrderStopActivationProjectionResult(terms is not null, state, terms is null ? 0 : -1);
    }

    internal static OrderStopActivationState Initial(OrderTerms terms) =>
        IsStop(terms) ? OrderStopActivationState.Unknown : OrderStopActivationState.NotApplicable;

    internal static bool TryApply(
        OrderEventKind kind,
        OrderTerms terms,
        ref OrderStopActivationState state)
    {
        var isStop = IsStop(terms);
        switch (kind)
        {
            case OrderEventKind.StopMonitoringStarted:
                if (!isStop || state != OrderStopActivationState.Unknown) return false;
                state = OrderStopActivationState.Monitoring;
                return true;
            case OrderEventKind.StopActivated:
                if (!isStop || state != OrderStopActivationState.Monitoring) return false;
                state = OrderStopActivationState.Activated;
                return true;
            case OrderEventKind.FillReceived when isStop:
                if (state == OrderStopActivationState.Unknown)
                {
                    // Legacy fills prove that activation happened even though older streams did not
                    // record the trigger separately.
                    state = OrderStopActivationState.Activated;
                    return true;
                }
                return state == OrderStopActivationState.Activated;
            default:
                return true;
        }
    }

    private static bool IsStop(OrderTerms terms) =>
        terms.Type is OrderType.Stop or OrderType.StopLimit;
}

public enum OrderProjectionFault : byte
{
    None = 0,
    InvalidEventChain = 1,
    MissingSubmitCommand = 2,
    SubmitCommandChanged = 3,
    InvalidPayload = 4,
    RiskDecisionMismatch = 5,
    ExternalIdentityChanged = 6,
    ReplacementTermsInvalid = 7,
    DuplicateFill = 8,
    FillQuantityExceeded = 9,
    ArithmeticOverflow = 10,
    FillStateMismatch = 11,
    InvalidCanonicalInstruction = 12,
    InvalidStopActivationEvidence = 13,
}

public readonly record struct OrderProjectionResult(
    OrderProjectionFault Fault,
    OmsOrderProjection? Projection,
    int EventIndex,
    OrderEventChainFault ChainFault = OrderEventChainFault.None)
{
    public bool IsSuccess => Fault == OrderProjectionFault.None && Projection is not null;
}

/// <summary>Current OMS order state rebuilt exclusively from its verified immutable event stream.</summary>
public sealed record OmsOrderProjection(
    ClientOrderId ClientOrderId,
    OrderId OrderId,
    OrderLifecycleState State,
    SubmitOrderCommand SubmitCommand,
    CanonicalOrderInstruction Instruction,
    OrderTerms Terms,
    CanonicalOrderTerms CanonicalTerms,
    OrderTerms? PendingReplacementTerms,
    OrderRiskObservation? RiskObservation,
    BrokerOrderId? BrokerOrderId,
    ExchangeOrderId? ExchangeOrderId,
    ScaledQuantity FilledQuantity,
    ScaledPrice AverageFillPrice,
    ScaledMoney TotalFees,
    long LastSequence,
    string LastEventHash,
    CausationId LastCausationId)
{
    public bool BlocksRetry => OrderLifecycle.BlocksRetry(State);

    public static OrderProjectionResult Rebuild(IReadOnlyList<OmsOrderEvent>? events) =>
        OmsOrderProjector.Rebuild(events);
}

/// <summary>Pure fold from the append-only order ledger into one current-state projection.</summary>
public static class OmsOrderProjector
{
    public static OrderProjectionResult Rebuild(IReadOnlyList<OmsOrderEvent>? events)
    {
        var chain = OrderEventChainVerifier.Verify(events);
        if (!chain.IsValid)
            return Failed(OrderProjectionFault.InvalidEventChain, chain.EventIndex, chain.Fault);

        SubmitOrderCommand? submit = null;
        CanonicalOrderInstruction? instruction = null;
        OrderTerms? terms = null;
        var canonicalTerms = default(CanonicalOrderTerms);
        OrderTerms? pendingReplacement = null;
        OrderRiskObservation? risk = null;
        BrokerOrderId? brokerOrderId = null;
        ExchangeOrderId? exchangeOrderId = null;
        var fillIds = new HashSet<TradeId>();
        var filledQuantity = ScaledQuantity.Zero;
        Int128 fillNotionalCoefficient = 0;
        var fillNotionalScale = 0;
        var totalFees = ScaledMoney.Zero;
        var stopActivation = OrderStopActivationState.NotApplicable;

        for (var index = 0; index < events!.Count; index++)
        {
            var orderEvent = events[index];
            var payloadFault = ValidatePayload(orderEvent, index);
            if (payloadFault != OrderProjectionFault.None)
                return Failed(payloadFault, index);

            if (orderEvent.SubmitCommand is not null)
            {
                var proposedInstruction = orderEvent.SubmitCommand.CanonicalInstruction;
                if (orderEvent.SubmitCommand.ClientOrderId != orderEvent.AggregateId ||
                    proposedInstruction.Validate() != OrderDomainFault.None ||
                    proposedInstruction.Identity.ClientOrderId != orderEvent.AggregateId ||
                    !CanonicalOrderInstructionMapper.MatchesCommand(
                        proposedInstruction,
                        orderEvent.SubmitCommand.Metadata,
                        orderEvent.SubmitCommand.ClientOrderId,
                        orderEvent.SubmitCommand.Terms))
                {
                    return Failed(OrderProjectionFault.InvalidCanonicalInstruction, index);
                }
                if (submit is null)
                {
                    submit = orderEvent.SubmitCommand;
                    instruction = proposedInstruction;
                    terms = submit.Terms;
                    canonicalTerms = instruction.Terms;
                    stopActivation = OrderStopActivationProjector.Initial(terms);
                    brokerOrderId = instruction.Identity.BrokerOrderId;
                    exchangeOrderId = instruction.Identity.ExchangeOrderId;
                }
                else if (submit != orderEvent.SubmitCommand)
                {
                    return Failed(OrderProjectionFault.SubmitCommandChanged, index);
                }
            }

            if (index == 0 && submit is null)
                return Failed(OrderProjectionFault.MissingSubmitCommand, index);

            if (orderEvent.RiskObservation is not null)
            {
                var expectedAllowed = orderEvent.Kind is
                    OrderEventKind.RiskAccepted or OrderEventKind.ReplaceRiskAccepted;
                if (orderEvent.RiskObservation.Decision.IsAllowed != expectedAllowed)
                    return Failed(OrderProjectionFault.RiskDecisionMismatch, index);
                if (orderEvent.Kind is OrderEventKind.RiskAccepted or OrderEventKind.RiskRejected &&
                    !string.Equals(
                        orderEvent.RiskObservation.CommandPayloadHashSha256,
                        submit!.PayloadHashSha256,
                        StringComparison.Ordinal))
                {
                    return Failed(OrderProjectionFault.RiskDecisionMismatch, index);
                }
                risk = orderEvent.RiskObservation;
            }

            if (!TryAdoptExternalId(orderEvent.BrokerOrderId, ref brokerOrderId) ||
                !TryAdoptExternalId(orderEvent.ExchangeOrderId, ref exchangeOrderId))
            {
                return Failed(OrderProjectionFault.ExternalIdentityChanged, index);
            }

            if (orderEvent.ReplacementTerms is not null)
            {
                if (!CanContainFilledQuantity(orderEvent.ReplacementTerms.Quantity, filledQuantity))
                    return Failed(OrderProjectionFault.ReplacementTermsInvalid, index);

                if (orderEvent.Kind == OrderEventKind.ReplaceConfirmed)
                {
                    terms = orderEvent.ReplacementTerms;
                    canonicalTerms = CanonicalOrderInstructionMapper.ToCanonicalTerms(terms);
                    pendingReplacement = null;
                    stopActivation = OrderStopActivationProjector.Initial(terms);
                }
                else
                {
                    pendingReplacement = orderEvent.ReplacementTerms;
                }
            }
            else if (orderEvent.Kind == OrderEventKind.ReplaceRiskRejected)
            {
                pendingReplacement = null;
            }

            if (!OrderStopActivationProjector.TryApply(orderEvent.Kind, terms!, ref stopActivation))
                return Failed(OrderProjectionFault.InvalidStopActivationEvidence, index);

            if (orderEvent.Fill is { } fill)
            {
                if (!fillIds.Add(fill.TradeId))
                    return Failed(OrderProjectionFault.DuplicateFill, index);

                if (!ScaledValueMath.TryAddQuantity(filledQuantity, fill.Quantity, out filledQuantity) ||
                    !ScaledValueMath.TryMultiply(fill.Quantity.Coefficient, fill.Price.Coefficient, out var fillNotional) ||
                    !ScaledValueMath.TryAdd(
                        fillNotionalCoefficient,
                        fillNotionalScale,
                        fillNotional,
                        fill.Quantity.Scale + fill.Price.Scale,
                        out fillNotionalCoefficient,
                        out fillNotionalScale) ||
                    !ScaledValueMath.TryAddMoney(totalFees, fill.Fee, out totalFees))
                {
                    return Failed(OrderProjectionFault.ArithmeticOverflow, index);
                }

                if (!ScaledValueMath.TryComparePositive(
                        filledQuantity.Coefficient,
                        filledQuantity.Scale,
                        terms!.Quantity.Coefficient,
                        terms.Quantity.Scale,
                        out var filledComparison))
                    return Failed(OrderProjectionFault.ArithmeticOverflow, index);
                if (filledComparison > 0)
                    return Failed(OrderProjectionFault.FillQuantityExceeded, index);
            }
        }

        var last = events[^1];
        if (!StateMatchesFills(last.StateAfter, terms!.Quantity, filledQuantity))
            return Failed(OrderProjectionFault.FillStateMismatch, events.Count - 1);

        var averageFillPrice = new ScaledPrice(0, 0);
        if (filledQuantity.Coefficient > 0 &&
            !ScaledValueMath.TryAveragePrice(
                fillNotionalCoefficient,
                fillNotionalScale,
                filledQuantity,
                out averageFillPrice))
            return Failed(OrderProjectionFault.ArithmeticOverflow, events.Count - 1);

        return new OrderProjectionResult(
            OrderProjectionFault.None,
            new OmsOrderProjection(
                last.AggregateId,
                submit!.OrderId,
                last.StateAfter,
                submit,
                instruction!,
                terms,
                canonicalTerms,
                pendingReplacement,
                risk,
                brokerOrderId,
                exchangeOrderId,
                filledQuantity,
                averageFillPrice,
                totalFees,
                last.AggregateSequence,
                last.EventHash,
                last.CausationId),
            -1);
    }

    private static OrderProjectionFault ValidatePayload(OmsOrderEvent orderEvent, int index)
    {
        if ((orderEvent.SubmitCommand is not null) != (index == 0 && orderEvent.Kind == OrderEventKind.DraftCreated))
            return OrderProjectionFault.InvalidPayload;

        var needsRisk = orderEvent.Kind is
            OrderEventKind.RiskAccepted or
            OrderEventKind.RiskRejected or
            OrderEventKind.ReplaceRiskAccepted or
            OrderEventKind.ReplaceRiskRejected;
        if ((orderEvent.RiskObservation is not null) != needsRisk)
            return OrderProjectionFault.InvalidPayload;

        if ((orderEvent.Fill is not null) != (orderEvent.Kind == OrderEventKind.FillReceived))
            return OrderProjectionFault.InvalidPayload;
        if ((orderEvent.Commission is not null) != (orderEvent.Kind == OrderEventKind.CommissionObserved))
            return OrderProjectionFault.InvalidPayload;
        if ((orderEvent.Position is not null) != (orderEvent.Kind == OrderEventKind.PositionObserved))
            return OrderProjectionFault.InvalidPayload;

        var needsDispatchReceipt = orderEvent.Kind is
            OrderEventKind.CancelDispatchRecorded or
            OrderEventKind.ReplaceDispatchRecorded;
        if ((orderEvent.DispatchReceipt is not null) != needsDispatchReceipt)
            return OrderProjectionFault.InvalidPayload;

        var needsReplacement = orderEvent.Kind is
            OrderEventKind.ReplaceRequested or
            OrderEventKind.ReplaceConfirmed or
            OrderEventKind.ReplaceRiskAccepted or
            OrderEventKind.ReplaceRiskRejected;
        if ((orderEvent.ReplacementTerms is not null) != needsReplacement)
            return OrderProjectionFault.InvalidPayload;

        if (orderEvent.Kind == OrderEventKind.Reconciled && orderEvent.Reconciliation is null)
            return OrderProjectionFault.InvalidPayload;
        if (orderEvent.Kind != OrderEventKind.Reconciled && orderEvent.Reconciliation is not null)
            return OrderProjectionFault.InvalidPayload;

        return OrderProjectionFault.None;
    }

    private static bool TryAdoptExternalId<T>(T? proposed, ref T? current)
        where T : struct, IExecutionIdentifier<T>
    {
        if (proposed is null) return true;
        if (current is not null && !EqualityComparer<T>.Default.Equals(current.Value, proposed.Value))
            return false;
        current = proposed;
        return true;
    }

    private static bool StateMatchesFills(
        OrderLifecycleState state,
        ScaledQuantity orderQuantity,
        ScaledQuantity filledQuantity)
    {
        if (filledQuantity.Coefficient == 0)
            return state is not (OrderLifecycleState.Filled or OrderLifecycleState.PartiallyFilled);
        if (!ScaledValueMath.TryComparePositive(
                filledQuantity.Coefficient,
                filledQuantity.Scale,
                orderQuantity.Coefficient,
                orderQuantity.Scale,
                out var comparison))
            return false;
        if (state == OrderLifecycleState.Filled)
            return comparison == 0;
        if (state == OrderLifecycleState.PartiallyFilled)
            return comparison < 0;
        if (state == OrderLifecycleState.Reconciled)
            return comparison <= 0;
        return comparison < 0;
    }

    private static bool CanContainFilledQuantity(
        in ScaledQuantity orderQuantity,
        in ScaledQuantity filledQuantity) =>
        filledQuantity.Coefficient == 0 ||
        ScaledValueMath.TryComparePositive(
            orderQuantity.Coefficient,
            orderQuantity.Scale,
            filledQuantity.Coefficient,
            filledQuantity.Scale,
            out var comparison) && comparison >= 0;

    private static OrderProjectionResult Failed(
        OrderProjectionFault fault,
        int eventIndex,
        OrderEventChainFault chainFault = OrderEventChainFault.None) =>
        new(fault, null, eventIndex, chainFault);
}
