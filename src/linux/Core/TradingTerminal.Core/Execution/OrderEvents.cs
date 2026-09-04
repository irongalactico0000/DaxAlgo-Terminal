using System.Text.Json.Serialization;

namespace TradingTerminal.Core.Execution;

/// <summary>One exact venue fill. Quantity, price, and fee retain coefficient/scale semantics.</summary>
public sealed record OrderFill
{
    public OrderFill(
        TradeId tradeId,
        ScaledQuantity quantity,
        ScaledPrice price,
        ScaledMoney fee,
        DateTimeOffset occurredAtUtc)
    {
        ExecutionIdentifier.Require(tradeId, nameof(tradeId));
        if (!quantity.IsValid || quantity.Coefficient <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        if (!price.IsValid || price.Coefficient <= 0) throw new ArgumentOutOfRangeException(nameof(price));
        if (!fee.IsValid || fee.Coefficient < 0) throw new ArgumentOutOfRangeException(nameof(fee));

        TradeId = tradeId;
        Quantity = quantity;
        Price = price;
        Fee = fee;
        OccurredAtUtc = ExecutionValidation.RequireUtc(occurredAtUtc, nameof(occurredAtUtc));
    }

    public TradeId TradeId { get; }
    public ScaledQuantity Quantity { get; }
    public ScaledPrice Price { get; }
    public ScaledMoney Fee { get; }
    public DateTimeOffset OccurredAtUtc { get; }
}

/// <summary>Versioned decision and inputs captured before an exposure-changing dispatch.</summary>
public sealed record OrderRiskObservation
{
    public OrderRiskObservation(
        string commandPayloadHashSha256,
        RiskDecision decision,
        RiskPolicyEvidence evidence)
    {
        CommandPayloadHashSha256 = ExecutionValidation.RequireSha256(
            commandPayloadHashSha256,
            nameof(commandPayloadHashSha256));
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(evidence);
        Decision = decision;
        Evidence = evidence;
    }

    public string CommandPayloadHashSha256 { get; }
    public RiskDecision Decision { get; }
    public RiskPolicyEvidence Evidence { get; }

    public static OrderRiskObservation Capture(
        ExecutionCommand command,
        RiskDecision decision,
        RiskEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        return new(command.PayloadHashSha256, decision, RiskPolicyEvidence.Capture(context));
    }
}

public enum ReconciliationOutcome : byte
{
    LocalStateConfirmed = 0,
    BrokerStateAdopted = 1,
    LocalOrderMissingAtBroker = 2,
    BrokerOrderImported = 3,
    ManualReviewRequired = 4,
}

/// <summary>Evidence that closes or updates an explicit order reconciliation case.</summary>
public sealed record OrderReconciliationEvidence
{
    public OrderReconciliationEvidence(
        ReconciliationCaseId caseId,
        ReconciliationOutcome outcome,
        string evidence,
        DateTimeOffset observedAtUtc)
    {
        ExecutionIdentifier.Require(caseId, nameof(caseId));
        if (!Enum.IsDefined(outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
        CaseId = caseId;
        Outcome = outcome;
        Evidence = ExecutionValidation.RequireText(evidence, nameof(evidence), 4096);
        ObservedAtUtc = ExecutionValidation.RequireUtc(observedAtUtc, nameof(observedAtUtc));
    }

    public ReconciliationCaseId CaseId { get; }
    public ReconciliationOutcome Outcome { get; }
    public string Evidence { get; }
    public DateTimeOffset ObservedAtUtc { get; }
}

/// <summary>A separate exact fee callback that may arrive after its fill.</summary>
public sealed record OrderCommissionObservation
{
    public OrderCommissionObservation(
        TradeId tradeId,
        ScaledMoney fee,
        string currency,
        DateTimeOffset observedAtUtc)
    {
        ExecutionIdentifier.Require(tradeId, nameof(tradeId));
        if (!fee.IsValid || fee.Coefficient < 0) throw new ArgumentOutOfRangeException(nameof(fee));
        TradeId = tradeId;
        Fee = fee;
        Currency = ExecutionValidation.RequireText(currency.ToUpperInvariant(), nameof(currency), 16);
        ObservedAtUtc = ExecutionValidation.RequireUtc(observedAtUtc, nameof(observedAtUtc));
    }

    public TradeId TradeId { get; }
    public ScaledMoney Fee { get; }
    public string Currency { get; }
    public DateTimeOffset ObservedAtUtc { get; }
}

/// <summary>Account-position evidence used by recovery and reconciliation.</summary>
public sealed record OrderPositionObservation
{
    public OrderPositionObservation(
        TradingAccountId tradingAccountId,
        ScaledQuantity netQuantity,
        DateTimeOffset observedAtUtc)
    {
        ExecutionIdentifier.Require(tradingAccountId, nameof(tradingAccountId));
        if (!netQuantity.IsValid) throw new ArgumentOutOfRangeException(nameof(netQuantity));
        TradingAccountId = tradingAccountId;
        NetQuantity = netQuantity;
        ObservedAtUtc = ExecutionValidation.RequireUtc(observedAtUtc, nameof(observedAtUtc));
    }

    public TradingAccountId TradingAccountId { get; }
    public ScaledQuantity NetQuantity { get; }
    public DateTimeOffset ObservedAtUtc { get; }
}

/// <summary>
/// Proposed fact before the store assigns aggregate sequence, previous hash, recorded time, and
/// event hash. All causal inputs are explicit; persistence never reads an ambient clock.
/// </summary>
public sealed record OrderEventDraft(
    ClientOrderId AggregateId,
    OrderEventKind Kind,
    OrderLifecycleState StateAfter,
    OrderEventSource Source,
    DeduplicationKey DeduplicationKey,
    DateTimeOffset OccurredAtUtc,
    CausationId CausationId,
    SubmitOrderCommand? SubmitCommand = null,
    OrderRiskObservation? RiskObservation = null,
    BrokerOrderId? BrokerOrderId = null,
    ExchangeOrderId? ExchangeOrderId = null,
    OrderFill? Fill = null,
    OrderTerms? ReplacementTerms = null,
    OrderReconciliationEvidence? Reconciliation = null,
    OrderCommissionObservation? Commission = null,
    OrderPositionObservation? Position = null,
    string? Reason = null,
    ExecutionDispatchReceipt? DispatchReceipt = null);

/// <summary>Immutable append-only OMS fact. The hash covers every persisted field.</summary>
public sealed record OmsOrderEvent
{
    [JsonConstructor]
    internal OmsOrderEvent(
        ClientOrderId aggregateId,
        long aggregateSequence,
        OrderEventKind kind,
        OrderLifecycleState? stateBefore,
        OrderLifecycleState stateAfter,
        OrderEventSource source,
        DeduplicationKey deduplicationKey,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset recordedAtUtc,
        CausationId causationId,
        string previousEventHash,
        string eventHash,
        SubmitOrderCommand? submitCommand,
        OrderRiskObservation? riskObservation,
        BrokerOrderId? brokerOrderId,
        ExchangeOrderId? exchangeOrderId,
        OrderFill? fill,
        OrderTerms? replacementTerms,
        OrderReconciliationEvidence? reconciliation,
        OrderCommissionObservation? commission,
        OrderPositionObservation? position,
        string? reason,
        ExecutionDispatchReceipt? dispatchReceipt = null)
    {
        ExecutionIdentifier.Require(aggregateId, nameof(aggregateId));
        if (aggregateSequence <= 0) throw new ArgumentOutOfRangeException(nameof(aggregateSequence));
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (stateBefore is { } before && !Enum.IsDefined(before))
            throw new ArgumentOutOfRangeException(nameof(stateBefore));
        if (!Enum.IsDefined(stateAfter)) throw new ArgumentOutOfRangeException(nameof(stateAfter));
        if (!Enum.IsDefined(source)) throw new ArgumentOutOfRangeException(nameof(source));
        ExecutionIdentifier.Require(deduplicationKey, nameof(deduplicationKey));
        ExecutionIdentifier.Require(causationId, nameof(causationId));

        var occurred = ExecutionValidation.RequireUtc(occurredAtUtc, nameof(occurredAtUtc));
        var recorded = ExecutionValidation.RequireUtc(recordedAtUtc, nameof(recordedAtUtc));
        if (recorded < occurred)
            throw new ArgumentOutOfRangeException(nameof(recordedAtUtc), "Recorded time cannot precede occurrence time.");
        if (aggregateSequence == 1)
        {
            if (stateBefore is not null)
                throw new ArgumentException("The first event cannot have a prior state.", nameof(stateBefore));
            if (previousEventHash.Length != 0)
                throw new ArgumentException("The first event cannot have a previous hash.", nameof(previousEventHash));
        }
        else
        {
            _ = ExecutionValidation.RequireSha256(previousEventHash, nameof(previousEventHash));
        }

        AggregateId = aggregateId;
        AggregateSequence = aggregateSequence;
        Kind = kind;
        StateBefore = stateBefore;
        StateAfter = stateAfter;
        Source = source;
        DeduplicationKey = deduplicationKey;
        OccurredAtUtc = occurred;
        RecordedAtUtc = recorded;
        CausationId = causationId;
        PreviousEventHash = previousEventHash.ToLowerInvariant();
        EventHash = ExecutionValidation.RequireSha256(eventHash, nameof(eventHash));
        SubmitCommand = submitCommand;
        RiskObservation = riskObservation;
        BrokerOrderId = brokerOrderId;
        ExchangeOrderId = exchangeOrderId;
        Fill = fill;
        ReplacementTerms = replacementTerms;
        Reconciliation = reconciliation;
        Commission = commission;
        Position = position;
        Reason = reason is null ? null : ExecutionValidation.RequireText(reason, nameof(reason), 4096);
        DispatchReceipt = dispatchReceipt;
    }

    public ClientOrderId AggregateId { get; }
    public long AggregateSequence { get; }
    public OrderEventKind Kind { get; }
    public OrderLifecycleState? StateBefore { get; }
    public OrderLifecycleState StateAfter { get; }
    public OrderEventSource Source { get; }
    public DeduplicationKey DeduplicationKey { get; }
    public DateTimeOffset OccurredAtUtc { get; }
    public DateTimeOffset RecordedAtUtc { get; }
    public CausationId CausationId { get; }
    public string PreviousEventHash { get; }
    public string EventHash { get; }
    public SubmitOrderCommand? SubmitCommand { get; }
    public OrderRiskObservation? RiskObservation { get; }
    public BrokerOrderId? BrokerOrderId { get; }
    public ExchangeOrderId? ExchangeOrderId { get; }
    public OrderFill? Fill { get; }
    public OrderTerms? ReplacementTerms { get; }
    public OrderReconciliationEvidence? Reconciliation { get; }
    public OrderCommissionObservation? Commission { get; }
    public OrderPositionObservation? Position { get; }
    public string? Reason { get; }
    public ExecutionDispatchReceipt? DispatchReceipt { get; }
}

internal static class OmsOrderEventHash
{
    internal const string EmptyPreviousHash = "";

    internal static OmsOrderEvent Commit(
        OrderEventDraft draft,
        long aggregateSequence,
        OrderLifecycleState? stateBefore,
        string previousEventHash,
        DateTimeOffset recordedAtUtc)
    {
        var eventHash = Compute(
            draft.AggregateId,
            aggregateSequence,
            draft.Kind,
            stateBefore,
            draft.StateAfter,
            draft.Source,
            draft.DeduplicationKey,
            draft.OccurredAtUtc,
            recordedAtUtc,
            draft.CausationId,
            previousEventHash,
            draft.SubmitCommand,
            draft.RiskObservation,
            draft.BrokerOrderId,
            draft.ExchangeOrderId,
            draft.Fill,
            draft.ReplacementTerms,
            draft.Reconciliation,
            draft.Commission,
            draft.Position,
            draft.Reason,
            draft.DispatchReceipt);

        return new OmsOrderEvent(
            draft.AggregateId,
            aggregateSequence,
            draft.Kind,
            stateBefore,
            draft.StateAfter,
            draft.Source,
            draft.DeduplicationKey,
            draft.OccurredAtUtc,
            recordedAtUtc,
            draft.CausationId,
            previousEventHash,
            eventHash,
            draft.SubmitCommand,
            draft.RiskObservation,
            draft.BrokerOrderId,
            draft.ExchangeOrderId,
            draft.Fill,
            draft.ReplacementTerms,
            draft.Reconciliation,
            draft.Commission,
            draft.Position,
            draft.Reason,
            draft.DispatchReceipt);
    }

    internal static string Compute(OmsOrderEvent orderEvent) => Compute(
        orderEvent.AggregateId,
        orderEvent.AggregateSequence,
        orderEvent.Kind,
        orderEvent.StateBefore,
        orderEvent.StateAfter,
        orderEvent.Source,
        orderEvent.DeduplicationKey,
        orderEvent.OccurredAtUtc,
        orderEvent.RecordedAtUtc,
        orderEvent.CausationId,
        orderEvent.PreviousEventHash,
        orderEvent.SubmitCommand,
        orderEvent.RiskObservation,
        orderEvent.BrokerOrderId,
        orderEvent.ExchangeOrderId,
        orderEvent.Fill,
        orderEvent.ReplacementTerms,
        orderEvent.Reconciliation,
        orderEvent.Commission,
        orderEvent.Position,
        orderEvent.Reason,
        orderEvent.DispatchReceipt);

    private static string Compute(
        ClientOrderId aggregateId,
        long aggregateSequence,
        OrderEventKind kind,
        OrderLifecycleState? stateBefore,
        OrderLifecycleState stateAfter,
        OrderEventSource source,
        DeduplicationKey deduplicationKey,
        DateTimeOffset occurredAtUtc,
        DateTimeOffset recordedAtUtc,
        CausationId causationId,
        string previousEventHash,
        SubmitOrderCommand? submitCommand,
        OrderRiskObservation? riskObservation,
        BrokerOrderId? brokerOrderId,
        ExchangeOrderId? exchangeOrderId,
        OrderFill? fill,
        OrderTerms? replacementTerms,
        OrderReconciliationEvidence? reconciliation,
        OrderCommissionObservation? commission,
        OrderPositionObservation? position,
        string? reason,
        ExecutionDispatchReceipt? dispatchReceipt) =>
        dispatchReceipt is null
            ? ExecutionCanonicalJson.Hash(new OrderEventHashPayload(
            "daxalgo-oms-order-event-v1",
            aggregateId,
            aggregateSequence,
            kind,
            stateBefore,
            stateAfter,
            source,
            deduplicationKey,
            occurredAtUtc,
            recordedAtUtc,
            causationId,
            previousEventHash,
            submitCommand,
            riskObservation,
            brokerOrderId,
            exchangeOrderId,
            fill,
            replacementTerms,
            reconciliation,
            commission,
            position,
            reason))
            : ExecutionCanonicalJson.Hash(new OrderEventHashPayloadV2(
                "daxalgo-oms-order-event-v2",
                aggregateId,
                aggregateSequence,
                kind,
                stateBefore,
                stateAfter,
                source,
                deduplicationKey,
                occurredAtUtc,
                recordedAtUtc,
                causationId,
                previousEventHash,
                submitCommand,
                riskObservation,
                brokerOrderId,
                exchangeOrderId,
                fill,
                replacementTerms,
                reconciliation,
                commission,
                position,
                reason,
                dispatchReceipt));

    private sealed record OrderEventHashPayload(
        string Schema,
        ClientOrderId AggregateId,
        long AggregateSequence,
        OrderEventKind Kind,
        OrderLifecycleState? StateBefore,
        OrderLifecycleState StateAfter,
        OrderEventSource Source,
        DeduplicationKey DeduplicationKey,
        DateTimeOffset OccurredAtUtc,
        DateTimeOffset RecordedAtUtc,
        CausationId CausationId,
        string PreviousEventHash,
        SubmitOrderCommand? SubmitCommand,
        OrderRiskObservation? RiskObservation,
        BrokerOrderId? BrokerOrderId,
        ExchangeOrderId? ExchangeOrderId,
        OrderFill? Fill,
        OrderTerms? ReplacementTerms,
        OrderReconciliationEvidence? Reconciliation,
        OrderCommissionObservation? Commission,
        OrderPositionObservation? Position,
        string? Reason);

    private sealed record OrderEventHashPayloadV2(
        string Schema,
        ClientOrderId AggregateId,
        long AggregateSequence,
        OrderEventKind Kind,
        OrderLifecycleState? StateBefore,
        OrderLifecycleState StateAfter,
        OrderEventSource Source,
        DeduplicationKey DeduplicationKey,
        DateTimeOffset OccurredAtUtc,
        DateTimeOffset RecordedAtUtc,
        CausationId CausationId,
        string PreviousEventHash,
        SubmitOrderCommand? SubmitCommand,
        OrderRiskObservation? RiskObservation,
        BrokerOrderId? BrokerOrderId,
        ExchangeOrderId? ExchangeOrderId,
        OrderFill? Fill,
        OrderTerms? ReplacementTerms,
        OrderReconciliationEvidence? Reconciliation,
        OrderCommissionObservation? Commission,
        OrderPositionObservation? Position,
        string? Reason,
        ExecutionDispatchReceipt DispatchReceipt);
}
