using TradingTerminal.Core.Time;

namespace TradingTerminal.Core.Execution;

/// <summary>
/// Serialized, lease-fenced, Paper-only service boundary over the existing OMS and durable outbox.
/// It contains no UI, IPC, storage implementation, broker SDK, credential, or live-order route.
/// </summary>
public sealed class ExecutionServiceEngine : IExecutionServiceEndpoint
{
    private const int MaximumRememberedRequests = 4_096;
    private readonly object _gate = new();
    private readonly IOrderEventStore _ledger;
    private readonly OrderManagementService _oms;
    private readonly IExecutionLeaseValidator _leaseValidator;
    private readonly IExecutionServiceReconciliationRunner? _reconciliation;
    private readonly IClock _clock;
    private readonly Dictionary<string, RememberedExchange> _remembered = new(StringComparer.Ordinal);
    private readonly Queue<string> _rememberedOrder = new();

    public ExecutionServiceEngine(
        IOrderEventStore ledger,
        OrderManagementService oms,
        IExecutionLeaseValidator leaseValidator,
        ExecutionLeaseGrant leaseGrant,
        IClock clock,
        IExecutionServiceReconciliationRunner? reconciliation = null)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _oms = oms ?? throw new ArgumentNullException(nameof(oms));
        _leaseValidator = leaseValidator ?? throw new ArgumentNullException(nameof(leaseValidator));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _reconciliation = reconciliation;
        if (!leaseGrant.IsValid)
            throw new ArgumentException("A valid execution lease grant is required.", nameof(leaseGrant));
        LeaseGrant = leaseGrant;
    }

    public ExecutionResource Resource => LeaseGrant.Claim.Resource;
    public ExecutionLeaseGrant LeaseGrant { get; }

    /// <summary>
    /// Executes one exchange atomically with respect to request-id replay. An exact replay returns
    /// the original response/event batch; reuse of the id for different input fails closed.
    /// </summary>
    public ExecutionServiceExchange Handle(ExecutionServiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            if (CanRemember(request) && _remembered.TryGetValue(request.RequestId, out var prior))
            {
                return prior.Request == request
                    ? prior.Exchange
                    : CreateExchange(
                        request,
                        ExecutionServiceFault.DuplicateRequestConflict,
                        null,
                        "The request id was already used for a different request.");
            }

            var exchange = Execute(request);
            if (CanRemember(request)) Remember(request, exchange);
            return exchange;
        }
    }

    private ExecutionServiceExchange Execute(ExecutionServiceRequest request)
    {
        ExecutionServiceFault fault;
        OrderLifecycleState? state = null;
        string? reason = null;
        try
        {
            if (!request.HasValidEnvelope)
            {
                fault = ExecutionServiceFault.InvalidRequest;
                reason = "The request envelope is invalid.";
            }
            else if (request.ProtocolVersion != ExecutionServiceProtocol.CurrentVersion)
            {
                fault = ExecutionServiceFault.ProtocolVersionMismatch;
                reason = $"Protocol version mismatch. Service={ExecutionServiceProtocol.CurrentVersion}; client={request.ProtocolVersion}.";
            }
            else if (request.Resource != Resource)
            {
                fault = ExecutionServiceFault.InvalidResource;
                reason = "The request names a different execution resource.";
            }
            else if (!HasValidPayloadShape(request))
            {
                fault = ExecutionServiceFault.InvalidRequest;
                reason = "The request payload does not match its operation kind.";
            }
            else if (request.Kind is ExecutionServiceRequestKind.Status or
                     ExecutionServiceRequestKind.Resync or
                     ExecutionServiceRequestKind.ReconciliationCases)
            {
                if (request.Kind == ExecutionServiceRequestKind.ReconciliationCases && _reconciliation is null)
                {
                    fault = ExecutionServiceFault.ReconciliationFailed;
                    reason = "No reconciliation control plane is composed.";
                }
                else
                {
                    var current = ValidateCurrentLease(LeaseGrant.Claim);
                    fault = request.Kind == ExecutionServiceRequestKind.Status && !current.IsSuccess
                        ? MapLeaseFault(current.Fault)
                        : ExecutionServiceFault.None;
                    reason = fault == ExecutionServiceFault.None
                        ? null
                        : current.Reason ?? "The service writer lease is no longer current; read-only synchronization remains available.";
                }
            }
            else
            {
                if (request.ExecutionLeaseId != LeaseGrant.Claim.LeaseId ||
                    request.FencingToken != LeaseGrant.Claim.FencingToken)
                {
                    return CreateExchange(
                        request,
                        ExecutionServiceFault.StaleFencingToken,
                        null,
                        "The request does not present this service generation's lease and fencing token.");
                }
                var claim = new ExecutionLeaseClaim(
                    request.Resource,
                    request.ExecutionLeaseId,
                    request.FencingToken);
                var lease = ValidateCurrentLease(claim);
                if (!lease.IsSuccess)
                {
                    fault = MapLeaseFault(lease.Fault);
                    reason = lease.Reason;
                }
                else
                {
                    var mutation = ExecuteMutation(request);
                    fault = mutation.Fault;
                    state = mutation.State;
                    reason = mutation.Reason;
                }
            }
        }
        catch (Exception exception)
        {
            fault = ExecutionServiceFault.InternalFailure;
            reason = $"{exception.GetType().Name}: {exception.Message}";
        }

        return CreateExchange(request, fault, state, reason);
    }

    private MutationResult ExecuteMutation(ExecutionServiceRequest request) => request.Kind switch
    {
        ExecutionServiceRequestKind.Submit => Submit(request),
        ExecutionServiceRequestKind.Cancel => Cancel(request),
        ExecutionServiceRequestKind.Replace => Replace(request),
        ExecutionServiceRequestKind.Reconcile => Reconcile(request),
        ExecutionServiceRequestKind.ResolveReconciliationCase => ResolveReconciliationCase(request),
        _ => Invalid("The request kind is not a state-mutating operation."),
    };

    private MutationResult Submit(ExecutionServiceRequest request)
    {
        var payload = request.Submit!;
        var command = payload.Command;
        if (!MatchesResource(command.Metadata, request.Resource) ||
            command.CanonicalInstruction.Identity.ExecutionLeaseId != request.ExecutionLeaseId ||
            command.CanonicalInstruction.Identity.FencingToken != request.FencingToken ||
            command.CanonicalInstruction.Validate() != OrderDomainFault.None)
        {
            return Invalid("The submit command or its lease/fencing identity is invalid.");
        }

        return FromOms(_oms.Submit(command, payload.RiskContext, Context(request.RequestId, "submit")));
    }

    private MutationResult Cancel(ExecutionServiceRequest request)
    {
        var command = request.Cancel!.Command;
        if (!MatchesResource(command.Metadata, request.Resource))
            return Invalid("The cancel command routing identity is invalid.");
        return FromOms(_oms.Cancel(command, Context(request.RequestId, "cancel")));
    }

    private MutationResult Replace(ExecutionServiceRequest request)
    {
        var payload = request.Replace!;
        if (!MatchesResource(payload.Command.Metadata, request.Resource))
            return Invalid("The replacement command routing identity is invalid.");
        return FromOms(_oms.Replace(
            payload.Command,
            payload.RiskContext,
            Context(request.RequestId, "replace")));
    }

    private MutationResult Reconcile(ExecutionServiceRequest request)
    {
        if (_reconciliation is null)
            return new(ExecutionServiceFault.ReconciliationFailed, null, "No reconciliation runner is composed.");

        var result = _reconciliation.Run(request.ReconciliationTrigger!.Value, request.Resource);
        return result.IsSuccess
            ? new MutationResult(ExecutionServiceFault.None, null, null)
            : new MutationResult(
                ExecutionServiceFault.ReconciliationFailed,
                null,
                result.Reason ?? result.Fault.ToString());
    }

    private MutationResult ResolveReconciliationCase(ExecutionServiceRequest request)
    {
        if (_reconciliation is null)
            return new(ExecutionServiceFault.ReconciliationFailed, null, "No reconciliation control plane is composed.");

        var resolution = request.ReconciliationResolution!;
        var resolved = _reconciliation.ResolveCase(
            request.Resource,
            resolution.CaseId,
            resolution.ResolvedBy,
            resolution.ResolutionEvidence,
            CurrentUtc());
        return resolved
            ? new MutationResult(ExecutionServiceFault.None, null, null)
            : new MutationResult(
                ExecutionServiceFault.ReconciliationFailed,
                null,
                "The reconciliation case is missing, belongs to another resource, is invalid, or could not append its resolution evidence.");
    }

    private ExecutionServiceExchange CreateExchange(
        ExecutionServiceRequest request,
        ExecutionServiceFault fault,
        OrderLifecycleState? state,
        string? reason)
    {
        IReadOnlyList<ExecutionServiceEvent> events;
        IReadOnlyList<ReconciliationCase> reconciliationCases = Array.Empty<ReconciliationCase>();
        ReconciliationCaseId? lastReconciliationCaseId = null;
        var hasMoreReconciliationCases = false;
        var reconciliationAdmissionBlocked = _reconciliation is null ||
                                             !_reconciliation.CanAdmitNewExposure(Resource);
        long last;
        try
        {
            var batch = request.Kind == ExecutionServiceRequestKind.ReconciliationCases
                ? Array.Empty<ExecutionServiceEvent>()
                : _ledger.ReadOutbox(Math.Max(0, request.AfterOutboxSequence))
                    .Take(ExecutionServiceProtocol.MaximumEventsPerExchange)
                    .Select(item => new ExecutionServiceEvent(item.OutboxSequence, item.Event))
                    .ToArray();
            events = Array.AsReadOnly(batch);
            last = batch.Length == 0 ? Math.Max(0, request.AfterOutboxSequence) : batch[^1].OutboxSequence;
        }
        catch (Exception exception)
        {
            events = Array.Empty<ExecutionServiceEvent>();
            last = Math.Max(0, request.AfterOutboxSequence);
            fault = ExecutionServiceFault.InternalFailure;
            reason = $"Outbox read failed: {exception.GetType().Name}: {exception.Message}";
        }

        if (fault == ExecutionServiceFault.None &&
            request.Kind == ExecutionServiceRequestKind.ReconciliationCases)
        {
            try
            {
                var after = request.AfterReconciliationCaseId?.Value;
                var page = _reconciliation!.ReadLatestCases(Resource)
                    .Where(item => after is null || string.CompareOrdinal(item.CaseId.Value, after) > 0)
                    .OrderBy(item => item.CaseId.Value, StringComparer.Ordinal)
                    .Take(ExecutionServiceProtocol.MaximumReconciliationCasesPerExchange + 1)
                    .ToArray();
                hasMoreReconciliationCases = page.Length > ExecutionServiceProtocol.MaximumReconciliationCasesPerExchange;
                var returned = page.Take(ExecutionServiceProtocol.MaximumReconciliationCasesPerExchange).ToArray();
                reconciliationCases = Array.AsReadOnly(returned);
                if (returned.Length != 0) lastReconciliationCaseId = returned[^1].CaseId;
            }
            catch (Exception exception)
            {
                reconciliationCases = Array.Empty<ReconciliationCase>();
                lastReconciliationCaseId = null;
                hasMoreReconciliationCases = false;
                fault = ExecutionServiceFault.InternalFailure;
                reason = $"Reconciliation-case read failed: {exception.GetType().Name}: {exception.Message}";
            }
        }

        return new ExecutionServiceExchange(
            new ExecutionServiceResponse(
                ExecutionServiceProtocol.CurrentVersion,
                request.RequestId ?? string.Empty,
                fault,
                Resource,
                LeaseGrant.Claim.LeaseId,
                LeaseGrant.Claim.FencingToken,
                state,
                last,
                events.Count,
                reason,
                lastReconciliationCaseId,
                reconciliationCases.Count,
                hasMoreReconciliationCases,
                reconciliationAdmissionBlocked),
            events,
            reconciliationCases);
    }

    private ExecutionLeaseValidationResult ValidateCurrentLease(in ExecutionLeaseClaim claim)
    {
        var now = _clock.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
        {
            return new ExecutionLeaseValidationResult(
                ExecutionLeaseFault.InvalidInput,
                false,
                "The execution-service clock must return UTC.");
        }
        return _leaseValidator.Validate(claim, new DateTimeOffset(now));
    }

    private static bool HasValidPayloadShape(ExecutionServiceRequest request)
    {
        var count = (request.Submit is null ? 0 : 1) +
                    (request.Cancel is null ? 0 : 1) +
                    (request.Replace is null ? 0 : 1) +
                    (request.ReconciliationTrigger.HasValue ? 1 : 0) +
                    (request.ReconciliationResolution is null ? 0 : 1);
        return request.Kind switch
        {
            ExecutionServiceRequestKind.Status or ExecutionServiceRequestKind.Resync =>
                count == 0 && request.AfterReconciliationCaseId is null,
            ExecutionServiceRequestKind.ReconciliationCases => count == 0 &&
                (!request.AfterReconciliationCaseId.HasValue ||
                 !request.AfterReconciliationCaseId.Value.IsEmpty),
            ExecutionServiceRequestKind.Submit => count == 1 && request.Submit is
                { Command: not null, RiskContext: not null } && request.AfterReconciliationCaseId is null,
            ExecutionServiceRequestKind.Cancel => count == 1 && request.Cancel is { Command: not null } &&
                request.AfterReconciliationCaseId is null,
            ExecutionServiceRequestKind.Replace => count == 1 && request.Replace is
                { Command: not null, RiskContext: not null } && request.AfterReconciliationCaseId is null,
            ExecutionServiceRequestKind.Reconcile => count == 1 &&
                request.ReconciliationTrigger is { } trigger && Enum.IsDefined(trigger) &&
                request.AfterReconciliationCaseId is null,
            ExecutionServiceRequestKind.ResolveReconciliationCase => count == 1 &&
                request.ReconciliationResolution is { } resolution &&
                !resolution.CaseId.IsEmpty &&
                ValidResolutionText(resolution.ResolvedBy, 256) &&
                ValidResolutionText(resolution.ResolutionEvidence, 8_192) &&
                request.AfterReconciliationCaseId is null,
            _ => false,
        };
    }

    private static bool ValidResolutionText(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal) &&
        !value.Any(char.IsControl);

    private static bool MatchesResource(ExecutionCommandMetadata metadata, ExecutionResource resource) =>
        metadata.VenueId == resource.VenueId &&
        metadata.TradingAccountId == resource.TradingAccountId &&
        metadata.Environment == resource.Environment;

    private static OrderCommandContext Context(string requestId, string operation) =>
        new(
            new CausationId($"service:{requestId}:{operation}"),
            new DeduplicationKey($"service:{requestId}:{operation}"));

    private static MutationResult FromOms(in OmsCommandResult result) => result.IsSuccess
        ? new MutationResult(ExecutionServiceFault.None, result.Projection!.State, null)
        : new MutationResult(
            result.Fault == OmsCommandFault.ExecutionLeaseRejected
                ? ExecutionServiceFault.StaleFencingToken
                : ExecutionServiceFault.OmsRejected,
            result.Projection?.State,
            result.Reason ?? result.Fault.ToString());

    private static MutationResult Invalid(string reason) =>
        new(ExecutionServiceFault.InvalidRequest, null, reason);

    private DateTimeOffset CurrentUtc()
    {
        var now = _clock.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("The execution-service clock must return UTC.");
        return new DateTimeOffset(now);
    }

    private static ExecutionServiceFault MapLeaseFault(ExecutionLeaseFault fault) => fault switch
    {
        ExecutionLeaseFault.HeldByAnotherOwner => ExecutionServiceFault.LeaseUnavailable,
        ExecutionLeaseFault.NotCurrent or ExecutionLeaseFault.Expired or ExecutionLeaseFault.Released =>
            ExecutionServiceFault.LeaseLost,
        ExecutionLeaseFault.LeaseIdentityConflict => ExecutionServiceFault.StaleFencingToken,
        ExecutionLeaseFault.InvalidInput => ExecutionServiceFault.InvalidRequest,
        _ => ExecutionServiceFault.LeasePersistenceFailure,
    };

    private static bool CanRemember(ExecutionServiceRequest request) =>
        !string.IsNullOrWhiteSpace(request.RequestId) &&
        request.RequestId.Length <= ExecutionServiceProtocol.MaximumRequestIdLength;

    private void Remember(ExecutionServiceRequest request, ExecutionServiceExchange exchange)
    {
        if (_remembered.ContainsKey(request.RequestId)) return;
        while (_remembered.Count >= MaximumRememberedRequests)
            _remembered.Remove(_rememberedOrder.Dequeue());
        _remembered.Add(request.RequestId, new RememberedExchange(request, exchange));
        _rememberedOrder.Enqueue(request.RequestId);
    }

    private sealed record RememberedExchange(
        ExecutionServiceRequest Request,
        ExecutionServiceExchange Exchange);

    private readonly record struct MutationResult(
        ExecutionServiceFault Fault,
        OrderLifecycleState? State,
        string? Reason);
}

/// <summary>Captures both Paper truths and runs the existing exact reconciliation engine.</summary>
public sealed class PaperExecutionServiceReconciliationRunner : IExecutionServiceReconciliationRunner
{
    private readonly IOrderEventStore _ledger;
    private readonly IReconciliationCaseStore _cases;
    private readonly IExecutionReconciliationSnapshotProvider _paperVenue;
    private readonly ReconciliationEngine _engine;
    private readonly IClock _clock;

    public PaperExecutionServiceReconciliationRunner(
        IOrderEventStore ledger,
        IReconciliationCaseStore cases,
        IExecutionReconciliationSnapshotProvider paperVenue,
        ReconciliationEngine engine,
        IClock clock)
    {
        _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
        _cases = cases ?? throw new ArgumentNullException(nameof(cases));
        _paperVenue = paperVenue ?? throw new ArgumentNullException(nameof(paperVenue));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public ReconciliationCycleResult Run(ReconciliationTrigger trigger, ExecutionResource resource)
    {
        var now = UtcNow();
        try
        {
            var local = ExecutionReconciliationSnapshotBuilder.FromLedger(resource, now, _ledger);
            var broker = _paperVenue.CaptureReconciliationSnapshot(resource, now);
            var result = _engine.RunCycle(trigger, local, broker, now);
            if (trigger == ReconciliationTrigger.Startup &&
                _ledger is IExecutionStartupRecoveryGate startupGate)
            {
                startupGate.TryCompleteStartupReconciliation(result);
            }
            return result;
        }
        catch (Exception exception)
        {
            return _engine.ReportAcquisitionFailure(
                trigger,
                resource,
                now,
                $"Paper reconciliation acquisition failed: {exception.GetType().Name}: {exception.Message}");
        }
    }

    public IReadOnlyList<ReconciliationCase> ReadLatestCases(ExecutionResource resource)
    {
        if (!resource.IsValid) return Array.Empty<ReconciliationCase>();
        return Array.AsReadOnly(_cases.Read(resource)
            .GroupBy(item => item.CaseId)
            .Select(group => group.Last())
            .OrderBy(item => item.CaseId.Value, StringComparer.Ordinal)
            .ToArray());
    }

    public bool CanAdmitNewExposure(ExecutionResource resource) =>
        _engine.CanAdmitNewExposure(resource);

    public bool ResolveCase(
        ExecutionResource resource,
        ReconciliationCaseId caseId,
        string resolvedBy,
        string resolutionEvidence,
        DateTimeOffset resolvedAtUtc)
    {
        if (!resource.IsValid || caseId.IsEmpty) return false;
        var current = _cases.Read(caseId).LastOrDefault();
        return current is not null && current.Resource == resource &&
               _engine.ResolveCase(caseId, resolvedBy, resolutionEvidence, resolvedAtUtc);
    }

    private DateTimeOffset UtcNow()
    {
        var now = _clock.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("The reconciliation clock must return UTC.");
        return new DateTimeOffset(now);
    }
}
