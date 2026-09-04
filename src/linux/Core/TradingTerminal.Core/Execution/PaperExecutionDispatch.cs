namespace TradingTerminal.Core.Execution;

public enum ExecutionDispatchStatus : byte
{
    Dispatched = 0,
    RejectedBeforeDispatch = 1,
    OutcomeUnknown = 2,
}

/// <summary>Local proof returned by the Paper adapter after one command crossed its boundary.</summary>
public sealed record ExecutionDispatchReceipt
{
    public ExecutionDispatchReceipt(
        DispatchAttemptId dispatchAttemptId,
        DateTimeOffset dispatchedAtUtc,
        BrokerOrderId? brokerOrderId = null,
        ExchangeOrderId? exchangeOrderId = null)
    {
        ExecutionIdentifier.Require(dispatchAttemptId, nameof(dispatchAttemptId));
        if (brokerOrderId is { } broker) ExecutionIdentifier.Require(broker, nameof(brokerOrderId));
        if (exchangeOrderId is { } exchange) ExecutionIdentifier.Require(exchange, nameof(exchangeOrderId));
        DispatchAttemptId = dispatchAttemptId;
        DispatchedAtUtc = ExecutionValidation.RequireUtc(dispatchedAtUtc, nameof(dispatchedAtUtc));
        BrokerOrderId = brokerOrderId;
        ExchangeOrderId = exchangeOrderId;
    }

    public DispatchAttemptId DispatchAttemptId { get; }
    public DateTimeOffset DispatchedAtUtc { get; }
    public BrokerOrderId? BrokerOrderId { get; }
    public ExchangeOrderId? ExchangeOrderId { get; }
}

public sealed record ExecutionDispatchResult
{
    private ExecutionDispatchResult(
        ExecutionDispatchStatus status,
        ExecutionDispatchReceipt? receipt,
        string? reason)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        if ((status == ExecutionDispatchStatus.Dispatched) != (receipt is not null))
            throw new ArgumentException("Only a dispatched result can contain a receipt.", nameof(receipt));
        Status = status;
        Receipt = receipt;
        Reason = reason is null ? null : ExecutionValidation.RequireText(reason, nameof(reason), 4096);
    }

    public ExecutionDispatchStatus Status { get; }
    public ExecutionDispatchReceipt? Receipt { get; }
    public string? Reason { get; }

    public static ExecutionDispatchResult Dispatched(ExecutionDispatchReceipt receipt) =>
        new(ExecutionDispatchStatus.Dispatched, receipt ?? throw new ArgumentNullException(nameof(receipt)), null);

    public static ExecutionDispatchResult RejectedBeforeDispatch(string reason) =>
        new(ExecutionDispatchStatus.RejectedBeforeDispatch, null, reason);

    public static ExecutionDispatchResult Unknown(string reason) =>
        new(ExecutionDispatchStatus.OutcomeUnknown, null, reason);
}

public enum PaperVenueEventKind : byte
{
    Acknowledged = 0,
    Fill = 1,
    Cancelled = 2,
    Replaced = 3,
    Rejected = 4,
    Expired = 5,
    StopMonitoringStarted = 6,
    StopActivated = 7,
}

/// <summary>One callback queued by the Paper venue after its dispatch receipt was returned.</summary>
public sealed record PaperVenueEvent(
    ExecutionEventId EventId,
    PaperVenueEventKind Kind,
    ClientOrderId ClientOrderId,
    DateTimeOffset OccurredAtUtc,
    CausationId CausationId,
    BrokerOrderId? BrokerOrderId = null,
    ExchangeOrderId? ExchangeOrderId = null,
    OrderFill? Fill = null,
    OrderTerms? ReplacementTerms = null,
    string? Reason = null);

/// <summary>
/// Paper-only dispatch boundary. The interface deliberately exposes no broker SDK or live mode;
/// the deterministic venue implementation is the only composition target in this repository.
/// </summary>
public interface IPaperExecutionDispatcher
{
    ExecutionDispatchResult Submit(SubmitOrderCommand command, OmsOrderProjection projection);
    ExecutionDispatchResult Cancel(CancelOrderCommand command, OmsOrderProjection projection);
    ExecutionDispatchResult Replace(ReplaceOrderCommand command, OmsOrderProjection projection);

    /// <summary>
    /// Drains callbacks queued after a dispatch receipt. This explicit barrier prevents a venue
    /// acknowledgement from reaching the ledger before local submission is durably recorded.
    /// </summary>
    IReadOnlyList<PaperVenueEvent> DrainEvents();
}
