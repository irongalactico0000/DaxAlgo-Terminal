using System.Collections.ObjectModel;

namespace TradingTerminal.Core.Execution;

public enum OrderEventAppendStatus : byte
{
    Appended = 0,
    ExactReplay = 1,
    Rejected = 2,
}

public enum OrderEventAppendFault : byte
{
    None = 0,
    MissingDraft = 1,
    InvalidAggregateId = 2,
    InvalidClassification = 3,
    InvalidDeduplicationKey = 4,
    InvalidCausationId = 5,
    InvalidTimestamp = 6,
    ConflictingDuplicate = 7,
    InvalidInitialEvent = 8,
    IllegalTransition = 9,
    SequenceExhausted = 10,
    ProjectionRejected = 11,
    InvalidEventSource = 12,
    InvalidPayload = 13,
    LedgerIntegrityBlocked = 14,
}

public readonly record struct OrderEventAppendResult(
    OrderEventAppendStatus Status,
    OrderEventAppendFault Fault,
    OmsOrderEvent? Event,
    OrderProjectionFault ProjectionFault = OrderProjectionFault.None)
{
    public bool IsSuccess => Fault == OrderEventAppendFault.None && Event is not null;
    public bool WasAppended => Status == OrderEventAppendStatus.Appended && IsSuccess;
    public bool IsExactReplay => Status == OrderEventAppendStatus.ExactReplay && IsSuccess;
}

/// <summary>Event and its monotonic process-local publication position.</summary>
public sealed record OrderEventOutboxEntry(long OutboxSequence, OmsOrderEvent Event);

/// <summary>
/// Append-only Paper OMS persistence seam. An implementation must atomically deduplicate the inbox,
/// append the event, update its projection, and create an outbox entry.
/// </summary>
public interface IOrderEventStore
{
    OrderEventAppendResult Append(OrderEventDraft draft, DateTimeOffset recordedAtUtc);
    IReadOnlyList<OmsOrderEvent> Read(ClientOrderId aggregateId);
    OmsOrderProjection? ReadProjection(ClientOrderId aggregateId);
    IReadOnlyList<OrderEventOutboxEntry> ReadOutbox(long afterExclusiveSequence = 0);
}

/// <summary>Startup admission state exposed later by the durable SQLite implementation.</summary>
public interface IExecutionAdmissionGate
{
    bool CanAdmitNewOrders { get; }
    bool CanAdmitAfterStartupReconciliation { get; }
}

/// <summary>
/// Durable startup gate that can be released only by a successful, discrepancy-free startup
/// reconciliation. Reconnect or manually constructed success cannot arm a recovered ledger.
/// </summary>
public interface IExecutionStartupRecoveryGate : IExecutionAdmissionGate
{
    bool TryCompleteStartupReconciliation(ReconciliationCycleResult result);
}

/// <summary>
/// Deterministic in-process implementation of the complete inbox/event/projection/outbox
/// transaction. It performs no I/O and never reads an ambient clock.
/// </summary>
public sealed class InMemoryOrderEventStore : IOrderEventStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ClientOrderId, List<OmsOrderEvent>> _streams = [];
    private readonly Dictionary<ClientOrderId, OmsOrderProjection> _projections = [];
    private readonly Dictionary<InboxIdentity, InboxRecord> _inbox = [];
    private readonly List<OrderEventOutboxEntry> _outbox = [];
    private long _lastOutboxSequence;

    public OrderEventAppendResult Append(OrderEventDraft draft, DateTimeOffset recordedAtUtc)
    {
        var basicFault = ValidateDraft(draft, recordedAtUtc);
        if (basicFault != OrderEventAppendFault.None)
            return Rejected(basicFault);

        lock (_gate)
        {
            var inboxIdentity = new InboxIdentity(draft.Source, draft.DeduplicationKey);
            if (_inbox.TryGetValue(inboxIdentity, out var prior))
            {
                return prior.Draft == draft
                    ? new OrderEventAppendResult(
                        OrderEventAppendStatus.ExactReplay,
                        OrderEventAppendFault.None,
                        prior.Event)
                    : Rejected(OrderEventAppendFault.ConflictingDuplicate);
            }

            _streams.TryGetValue(draft.AggregateId, out var stream);
            var previous = stream is { Count: > 0 } ? stream[^1] : null;
            if (!OrderLifecycle.IsEventSourceAllowed(draft.Kind, draft.Source))
                return Rejected(OrderEventAppendFault.InvalidEventSource);

            if (previous is null)
            {
                if (draft.Kind != OrderEventKind.DraftCreated ||
                    draft.StateAfter != OrderLifecycleState.Draft ||
                    draft.SubmitCommand is null ||
                    draft.SubmitCommand.ClientOrderId != draft.AggregateId ||
                    draft.SubmitCommand.CanonicalInstruction.Validate() != OrderDomainFault.None ||
                    draft.SubmitCommand.CanonicalInstruction.Identity.ClientOrderId != draft.AggregateId)
                {
                    return Rejected(OrderEventAppendFault.InvalidInitialEvent);
                }
            }
            else if (!OrderLifecycle.CanApplyEvent(
                         draft.Kind,
                         previous.StateAfter,
                         draft.StateAfter))
            {
                return Rejected(OrderEventAppendFault.IllegalTransition);
            }

            if (previous?.AggregateSequence == long.MaxValue || _lastOutboxSequence == long.MaxValue)
                return Rejected(OrderEventAppendFault.SequenceExhausted);

            OmsOrderEvent committed;
            try
            {
                committed = OmsOrderEventHash.Commit(
                    draft,
                    previous is null ? 1 : previous.AggregateSequence + 1,
                    previous?.StateAfter,
                    previous?.EventHash ?? OmsOrderEventHash.EmptyPreviousHash,
                    recordedAtUtc);
            }
            catch (ArgumentException)
            {
                return Rejected(OrderEventAppendFault.InvalidPayload);
            }

            var candidate = new OmsOrderEvent[(stream?.Count ?? 0) + 1];
            if (stream is not null)
                stream.CopyTo(candidate, 0);
            candidate[^1] = committed;

            var projectionResult = OmsOrderProjector.Rebuild(candidate);
            if (!projectionResult.IsSuccess)
            {
                return new OrderEventAppendResult(
                    OrderEventAppendStatus.Rejected,
                    OrderEventAppendFault.ProjectionRejected,
                    null,
                    projectionResult.Fault);
            }

            if (stream is null)
            {
                stream = [];
                _streams.Add(draft.AggregateId, stream);
            }

            var outboxSequence = _lastOutboxSequence + 1;
            stream.Add(committed);
            _projections[draft.AggregateId] = projectionResult.Projection!;
            _inbox.Add(inboxIdentity, new InboxRecord(draft, committed));
            _outbox.Add(new OrderEventOutboxEntry(outboxSequence, committed));
            _lastOutboxSequence = outboxSequence;

            return new OrderEventAppendResult(
                OrderEventAppendStatus.Appended,
                OrderEventAppendFault.None,
                committed);
        }
    }

    public IReadOnlyList<OmsOrderEvent> Read(ClientOrderId aggregateId)
    {
        if (aggregateId.IsEmpty) return Array.Empty<OmsOrderEvent>();
        lock (_gate)
        {
            return _streams.TryGetValue(aggregateId, out var stream)
                ? Array.AsReadOnly(stream.ToArray())
                : Array.Empty<OmsOrderEvent>();
        }
    }

    public OmsOrderProjection? ReadProjection(ClientOrderId aggregateId)
    {
        if (aggregateId.IsEmpty) return null;
        lock (_gate)
            return _projections.GetValueOrDefault(aggregateId);
    }

    public IReadOnlyList<OrderEventOutboxEntry> ReadOutbox(long afterExclusiveSequence = 0)
    {
        if (afterExclusiveSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(afterExclusiveSequence));

        lock (_gate)
        {
            if (_outbox.Count == 0 || afterExclusiveSequence >= _lastOutboxSequence)
                return Array.Empty<OrderEventOutboxEntry>();

            var copy = new List<OrderEventOutboxEntry>(_outbox.Count);
            foreach (var entry in _outbox)
            {
                if (entry.OutboxSequence > afterExclusiveSequence)
                    copy.Add(entry);
            }
            return new ReadOnlyCollection<OrderEventOutboxEntry>(copy);
        }
    }

    private static OrderEventAppendFault ValidateDraft(
        OrderEventDraft? draft,
        DateTimeOffset recordedAtUtc)
    {
        if (draft is null)
            return OrderEventAppendFault.MissingDraft;
        if (draft.AggregateId.IsEmpty)
            return OrderEventAppendFault.InvalidAggregateId;
        if (!Enum.IsDefined(draft.Kind) ||
            !Enum.IsDefined(draft.StateAfter) ||
            !Enum.IsDefined(draft.Source))
        {
            return OrderEventAppendFault.InvalidClassification;
        }
        if (draft.DeduplicationKey.IsEmpty)
            return OrderEventAppendFault.InvalidDeduplicationKey;
        if (draft.CausationId.IsEmpty)
            return OrderEventAppendFault.InvalidCausationId;
        if (draft.OccurredAtUtc.Offset != TimeSpan.Zero ||
            recordedAtUtc.Offset != TimeSpan.Zero ||
            recordedAtUtc < draft.OccurredAtUtc)
        {
            return OrderEventAppendFault.InvalidTimestamp;
        }

        return OrderEventAppendFault.None;
    }

    private static OrderEventAppendResult Rejected(OrderEventAppendFault fault) =>
        new(OrderEventAppendStatus.Rejected, fault, null);

    private readonly record struct InboxIdentity(
        OrderEventSource Source,
        DeduplicationKey DeduplicationKey);

    private sealed record InboxRecord(OrderEventDraft Draft, OmsOrderEvent Event);
}
