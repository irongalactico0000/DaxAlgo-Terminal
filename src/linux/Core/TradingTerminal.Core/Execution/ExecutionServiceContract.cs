namespace TradingTerminal.Core.Execution;

/// <summary>Versioned contract shared by a Paper execution host and its control-plane client.</summary>
public static class ExecutionServiceProtocol
{
    public const int CurrentVersion = 2;
    public const int MaximumRequestIdLength = 128;
    public const int MaximumEventsPerExchange = 256;
    public const int MaximumReconciliationCasesPerExchange = 128;
}

/// <summary>
/// Operations admitted by the Windows-compatible service boundary. Query/read-model behavior is
/// represented by Status and Resync; a kill operation is client-side orchestration over Cancel.
/// </summary>
public enum ExecutionServiceRequestKind : byte
{
    Status = 0,
    Submit = 1,
    Cancel = 2,
    Replace = 3,
    Reconcile = 4,
    Resync = 5,
    ReconciliationCases = 6,
    ResolveReconciliationCase = 7,
}

public sealed record ExecutionSubmitRequest(
    SubmitOrderCommand Command,
    RiskEvaluationContext RiskContext);

public sealed record ExecutionCancelRequest(CancelOrderCommand Command);

public sealed record ExecutionReplaceRequest(
    ReplaceOrderCommand Command,
    RiskEvaluationContext RiskContext);

/// <summary>One explicit operator resolution appended to an existing reconciliation case.</summary>
public sealed record ExecutionReconciliationResolutionRequest(
    ReconciliationCaseId CaseId,
    string ResolvedBy,
    string ResolutionEvidence);

/// <summary>One request-correlated, lease-fenced control-plane command.</summary>
public sealed record ExecutionServiceRequest(
    int ProtocolVersion,
    string RequestId,
    ExecutionServiceRequestKind Kind,
    ExecutionResource Resource,
    ExecutionLeaseId ExecutionLeaseId,
    FencingToken FencingToken,
    long AfterOutboxSequence = 0,
    ExecutionSubmitRequest? Submit = null,
    ExecutionCancelRequest? Cancel = null,
    ExecutionReplaceRequest? Replace = null,
    ReconciliationTrigger? ReconciliationTrigger = null,
    ReconciliationCaseId? AfterReconciliationCaseId = null,
    ExecutionReconciliationResolutionRequest? ReconciliationResolution = null)
{
    public bool HasValidEnvelope =>
        ProtocolVersion > 0 &&
        !string.IsNullOrWhiteSpace(RequestId) &&
        RequestId.Length <= ExecutionServiceProtocol.MaximumRequestIdLength &&
        string.Equals(RequestId, RequestId.Trim(), StringComparison.Ordinal) &&
        !RequestId.Any(char.IsControl) &&
        Enum.IsDefined(Kind) &&
        Resource.IsValid &&
        !ExecutionLeaseId.IsEmpty &&
        FencingToken.IsValid &&
        AfterOutboxSequence >= 0;
}

/// <summary>Stable failures that can cross a future IPC boundary without exception coupling.</summary>
public enum ExecutionServiceFault : byte
{
    None = 0,
    InvalidRequest = 1,
    ProtocolVersionMismatch = 2,
    InvalidResource = 3,
    LeaseUnavailable = 4,
    LeaseLost = 5,
    StaleFencingToken = 6,
    LeasePersistenceFailure = 7,
    OmsRejected = 8,
    ReconciliationFailed = 9,
    DuplicateRequestConflict = 10,
    InternalFailure = 11,
}

/// <summary>One response followed by exactly <see cref="EventCount"/> durable outbox facts.</summary>
public sealed record ExecutionServiceResponse(
    int ProtocolVersion,
    string RequestId,
    ExecutionServiceFault Fault,
    ExecutionResource Resource,
    ExecutionLeaseId ExecutionLeaseId,
    FencingToken FencingToken,
    OrderLifecycleState? State,
    long LastOutboxSequence,
    int EventCount,
    string? Reason = null,
    ReconciliationCaseId? LastReconciliationCaseId = null,
    int ReconciliationCaseCount = 0,
    bool HasMoreReconciliationCases = false,
    bool ReconciliationAdmissionBlocked = false)
{
    public bool IsSuccess => Fault == ExecutionServiceFault.None;
}

public sealed record ExecutionServiceEvent(long OutboxSequence, OmsOrderEvent Event);

public sealed record ExecutionServiceExchange(
    ExecutionServiceResponse Response,
    IReadOnlyList<ExecutionServiceEvent> Events,
    IReadOnlyList<ReconciliationCase>? ReconciliationCases = null)
{
    public IReadOnlyList<ReconciliationCase> CaseFacts =>
        ReconciliationCases ?? Array.Empty<ReconciliationCase>();
}

/// <summary>
/// One request-correlated execution-service boundary. Implementations may be the in-process engine
/// or an authenticated local IPC client; callers cannot reach the OMS or ledger directly.
/// </summary>
public interface IExecutionServiceEndpoint
{
    ExecutionResource Resource { get; }
    ExecutionLeaseGrant LeaseGrant { get; }
    ExecutionServiceExchange Handle(ExecutionServiceRequest request);
}

/// <summary>Host-owned reconciliation acquisition and comparison boundary.</summary>
public interface IExecutionServiceReconciliationRunner
{
    ReconciliationCycleResult Run(ReconciliationTrigger trigger, ExecutionResource resource);
    IReadOnlyList<ReconciliationCase> ReadLatestCases(ExecutionResource resource);
    bool CanAdmitNewExposure(ExecutionResource resource);
    bool ResolveCase(
        ExecutionResource resource,
        ReconciliationCaseId caseId,
        string resolvedBy,
        string resolutionEvidence,
        DateTimeOffset resolvedAtUtc);
}
