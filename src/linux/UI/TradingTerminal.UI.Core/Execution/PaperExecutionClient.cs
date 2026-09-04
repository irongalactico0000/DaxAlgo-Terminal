using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;

namespace TradingTerminal.UI.Execution;

public readonly record struct PaperExecutionClientResult(
    bool IsSuccess,
    string Message,
    ExecutionServiceFault Fault = ExecutionServiceFault.None)
{
    public static PaperExecutionClientResult Success(string message) => new(true, message);
    public static PaperExecutionClientResult Failure(ExecutionServiceFault fault, string message) =>
        new(false, message, fault);
}

/// <summary>One immutable risk fact projected from a verified order-event stream.</summary>
public sealed record PaperRiskDecisionSnapshot(
    ClientOrderId ClientOrderId,
    long AggregateSequence,
    OrderEventKind EventKind,
    DateTimeOffset RecordedAtUtc,
    OrderRiskObservation Observation);

/// <summary>Windows-equivalent execution-quality facts derived only from verified ledger state.</summary>
public sealed record PaperExecutionQualitySnapshot(
    int Orders,
    int FilledOrders,
    int Rejects,
    int Cancels,
    int ReconciliationCases,
    int UnknownOutcomes,
    int SlippageObservationCount,
    double TotalSlippageTicks,
    int AcknowledgementObservationCount,
    double TotalAcknowledgementLatencyMilliseconds)
{
    public double FillRatePercent => Orders == 0 ? 0d : FilledOrders * 100d / Orders;
    public double RejectRatePercent => Orders == 0 ? 0d : Rejects * 100d / Orders;
    public double AverageSlippageTicks =>
        SlippageObservationCount == 0 ? 0d : TotalSlippageTicks / SlippageObservationCount;
    public double AverageAcknowledgementLatencyMilliseconds =>
        AcknowledgementObservationCount == 0
            ? 0d
            : TotalAcknowledgementLatencyMilliseconds / AcknowledgementObservationCount;
}

/// <summary>Exact, UI-independent state rebuilt from the execution service outbox.</summary>
public sealed record PaperExecutionClientSnapshot(
    ExecutionResource Resource,
    ExecutionLeaseId ExecutionLeaseId,
    FencingToken FencingToken,
    bool LeaseHeld,
    bool IntakePaused,
    long LastOutboxSequence,
    IReadOnlyList<OmsOrderProjection> Orders,
    ExecutionReconciliationSnapshot Economics,
    IReadOnlyList<OmsOrderEvent> LedgerEvents,
    DateTimeOffset ObservedAtUtc,
    string? LastOperationMessage,
    IReadOnlyList<ReconciliationCase>? ReconciliationCases = null,
    bool ReconciliationAdmissionBlocked = false,
    IReadOnlyList<PaperRiskDecisionSnapshot>? RiskDecisions = null,
    PaperExecutionQualitySnapshot? ExecutionQuality = null,
    PaperPortfolioAnalyticsSnapshot? PortfolioAnalytics = null)
{
    public IReadOnlyList<ReconciliationCase> CaseFacts =>
        ReconciliationCases ?? Array.Empty<ReconciliationCase>();
    public int UnresolvedMaterialCaseCount => CaseFacts.Count(item =>
        item.IsMaterial && item.Status != ReconciliationCaseStatus.Resolved);
    public IReadOnlyList<PaperRiskDecisionSnapshot> RiskDecisionFacts =>
        RiskDecisions ?? Array.Empty<PaperRiskDecisionSnapshot>();
    public PaperExecutionQualitySnapshot QualityFacts => ExecutionQuality ?? new(
        0, 0, 0, 0, CaseFacts.Count, 0, 0, 0d, 0, 0d);
    public bool AdmissionOpen => LeaseHeld && !IntakePaused && !ReconciliationAdmissionBlocked;
}

/// <summary>Creates one exact exposure-reducing market instruction for a Kill operation.</summary>
public interface IPaperExecutionFlattenOrderFactory
{
    bool TryCreateFlattenOrder(
        ReconciliationPositionSnapshot position,
        ExecutionLeaseGrant leaseGrant,
        DateTimeOffset createdAtUtc,
        out ExecutionSubmitRequest? request,
        out string? reason);
}

public interface IPaperExecutionClient : IDisposable
{
    event EventHandler? SnapshotInvalidated;
    PaperExecutionClientSnapshot GetSnapshot();
    ValueTask<PaperExecutionClientResult> RefreshAsync(CancellationToken cancellationToken = default);
    ValueTask<PaperExecutionClientResult> SetIntakePausedAsync(bool paused, CancellationToken cancellationToken = default);
    ValueTask<PaperExecutionClientResult> SubmitAsync(ExecutionSubmitRequest request, CancellationToken cancellationToken = default);
    ValueTask<PaperExecutionClientResult> CancelAsync(ClientOrderId clientOrderId, CancellationToken cancellationToken = default);
    ValueTask<PaperExecutionClientResult> ReplaceAsync(ClientOrderId clientOrderId, OrderTerms replacementTerms, RiskEvaluationContext riskContext, CancellationToken cancellationToken = default);
    ValueTask<PaperExecutionClientResult> ReconcileAsync(CancellationToken cancellationToken = default);
    ValueTask<PaperExecutionClientResult> ResolveReconciliationCaseAsync(
        ReconciliationCaseId caseId,
        string resolvedBy,
        string resolutionEvidence,
        CancellationToken cancellationToken = default);
    ValueTask<PaperExecutionClientResult> KillAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// In-process Paper desktop client. It uses the same versioned service contract later transported
/// over IPC and never reaches the OMS, ledger implementation, or Paper venue directly.
/// </summary>
public sealed class PaperExecutionClient : IPaperExecutionClient
{
    private readonly object _gate = new();
    private readonly IExecutionServiceEndpoint _service;
    private readonly IClock _clock;
    private readonly IPaperExecutionFlattenOrderFactory? _flattenFactory;
    private readonly decimal _openingBalance;
    private readonly Func<InstrumentId, ScaledPrice?>? _currentMark;
    private readonly string _requestSession = Guid.NewGuid().ToString("N");
    private readonly List<ExecutionServiceEvent> _outbox = [];
    private readonly Dictionary<ClientOrderId, List<OmsOrderEvent>> _streams = [];
    private readonly Dictionary<ReconciliationCaseId, ReconciliationCase> _reconciliationCases = [];
    private long _requestSequence;
    private long _cursor;
    private bool _leaseHeld;
    private bool _intakePaused;
    private bool _reconciliationAdmissionBlocked;
    private bool _disposed;
    private string? _lastOperationMessage;

    public PaperExecutionClient(
        IExecutionServiceEndpoint service,
        IClock clock,
        IPaperExecutionFlattenOrderFactory? flattenFactory = null,
        ScaledMoney? openingBalance = null,
        Func<InstrumentId, ScaledPrice?>? currentMark = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _flattenFactory = flattenFactory;
        _openingBalance = ExecutionNumericBoundary.ToDecimal(
            openingBalance ?? ExecutionNumericBoundary.MoneyFromDecimal(100_000m));
        if (_openingBalance <= 0m)
            throw new ArgumentOutOfRangeException(nameof(openingBalance), "Opening balance must be positive.");
        _currentMark = currentMark;
    }

    public event EventHandler? SnapshotInvalidated;

    public PaperExecutionClientSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return BuildSnapshot();
        }
    }

    public ValueTask<PaperExecutionClientResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PaperExecutionClientResult result;
        lock (_gate)
        {
            ThrowIfDisposed();
            result = RefreshCore();
            _lastOperationMessage = result.Message;
        }
        Invalidate();
        return ValueTask.FromResult(result);
    }

    public ValueTask<PaperExecutionClientResult> SetIntakePausedAsync(
        bool paused,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            ThrowIfDisposed();
            _intakePaused = paused;
            _lastOperationMessage = paused ? "Paper order intake paused." : "Paper order intake resumed.";
        }
        Invalidate();
        return ValueTask.FromResult(PaperExecutionClientResult.Success(_lastOperationMessage));
    }

    public ValueTask<PaperExecutionClientResult> SubmitAsync(
        ExecutionSubmitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        PaperExecutionClientResult result;
        lock (_gate)
        {
            ThrowIfDisposed();
            result = _intakePaused
                ? PaperExecutionClientResult.Failure(
                    ExecutionServiceFault.OmsRejected,
                    "Submit refused because Paper order intake is paused.")
                : _reconciliationAdmissionBlocked
                    ? PaperExecutionClientResult.Failure(
                        ExecutionServiceFault.ReconciliationFailed,
                        "Submit refused because unresolved reconciliation state blocks new exposure.")
                : SubmitCore(request, "submit");
            _lastOperationMessage = result.Message;
        }
        Invalidate();
        return ValueTask.FromResult(result);
    }

    public ValueTask<PaperExecutionClientResult> CancelAsync(
        ClientOrderId clientOrderId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PaperExecutionClientResult result;
        lock (_gate)
        {
            ThrowIfDisposed();
            result = CancelCore(clientOrderId, "cancel");
            _lastOperationMessage = result.Message;
        }
        Invalidate();
        return ValueTask.FromResult(result);
    }

    public ValueTask<PaperExecutionClientResult> ReplaceAsync(
        ClientOrderId clientOrderId,
        OrderTerms replacementTerms,
        RiskEvaluationContext riskContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(replacementTerms);
        ArgumentNullException.ThrowIfNull(riskContext);
        cancellationToken.ThrowIfCancellationRequested();
        PaperExecutionClientResult result;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (_intakePaused)
            {
                result = PaperExecutionClientResult.Failure(
                    ExecutionServiceFault.OmsRejected,
                    "Replace refused because Paper order intake is paused.");
            }
            else
            {
                var refresh = RefreshCore();
                var projection = Projection(clientOrderId);
                if (!refresh.IsSuccess || projection is null)
                {
                    result = !refresh.IsSuccess
                        ? refresh
                        : PaperExecutionClientResult.Failure(
                            ExecutionServiceFault.InvalidRequest,
                            "Replace refused because the order is absent from the verified client view.");
                }
                else
                {
                    var command = new ReplaceOrderCommand(
                        Metadata(projection, "replace"),
                        projection.OrderId,
                        replacementTerms);
                    var exchange = _service.Handle(MutationRequest(
                        "replace",
                        ExecutionServiceRequestKind.Replace,
                        replace: new ExecutionReplaceRequest(command, riskContext)));
                    result = FromExchange(exchange, "Replacement accepted by the Paper OMS.");
                    result = RefreshAfterMutation(result);
                }
            }
            _lastOperationMessage = result.Message;
        }
        Invalidate();
        return ValueTask.FromResult(result);
    }

    public ValueTask<PaperExecutionClientResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PaperExecutionClientResult result;
        lock (_gate)
        {
            ThrowIfDisposed();
            var exchange = _service.Handle(MutationRequest(
                "reconcile",
                ExecutionServiceRequestKind.Reconcile,
                trigger: ReconciliationTrigger.OperatorRequest));
            result = FromExchange(exchange, "Paper reconciliation completed.");
            if (result.IsSuccess) result = RefreshCore();
            _lastOperationMessage = result.Message;
        }
        Invalidate();
        return ValueTask.FromResult(result);
    }

    public ValueTask<PaperExecutionClientResult> ResolveReconciliationCaseAsync(
        ReconciliationCaseId caseId,
        string resolvedBy,
        string resolutionEvidence,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PaperExecutionClientResult result;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (caseId.IsEmpty || string.IsNullOrWhiteSpace(resolvedBy) ||
                string.IsNullOrWhiteSpace(resolutionEvidence))
            {
                result = PaperExecutionClientResult.Failure(
                    ExecutionServiceFault.InvalidRequest,
                    "Select an open reconciliation case and provide operator identity plus resolution evidence.");
            }
            else
            {
                var resolution = new ExecutionReconciliationResolutionRequest(
                    caseId,
                    resolvedBy.Trim(),
                    resolutionEvidence.Trim());
                var exchange = _service.Handle(MutationRequest(
                    "resolve-reconciliation",
                    ExecutionServiceRequestKind.ResolveReconciliationCase,
                    resolution: resolution));
                result = FromExchange(exchange, "Reconciliation resolution was appended to the durable case history.");
                if (result.IsSuccess) result = RefreshCore();
            }
            _lastOperationMessage = result.Message;
        }
        Invalidate();
        return ValueTask.FromResult(result);
    }

    public ValueTask<PaperExecutionClientResult> KillAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        PaperExecutionClientResult result;
        lock (_gate)
        {
            ThrowIfDisposed();
            _intakePaused = true;
            result = KillCore(cancellationToken);
            if (!result.IsSuccess)
                result = result with { Message = $"{result.Message} New order intake remains paused." };
            _lastOperationMessage = result.Message;
        }
        Invalidate();
        return ValueTask.FromResult(result);
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _outbox.Clear();
            _streams.Clear();
            _reconciliationCases.Clear();
        }
    }

    private PaperExecutionClientResult KillCore(CancellationToken cancellationToken)
    {
        var reconciliation = _service.Handle(MutationRequest(
            "kill-reconcile",
            ExecutionServiceRequestKind.Reconcile,
            trigger: ReconciliationTrigger.OperatorRequest));
        var reconciled = FromExchange(reconciliation, "Kill reconciliation completed.");
        if (!reconciled.IsSuccess) return reconciled;
        var refreshed = RefreshCore();
        if (!refreshed.IsSuccess) return refreshed;

        var cancellable = Projections()
            .Where(item => item.State is OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled)
            .Select(item => item.ClientOrderId)
            .ToArray();
        foreach (var clientOrderId in cancellable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cancelled = CancelCore(clientOrderId, "kill-cancel");
            if (!cancelled.IsSuccess)
                return PaperExecutionClientResult.Failure(
                    cancelled.Fault,
                    $"Kill stopped while cancelling {clientOrderId}: {cancelled.Message}");
        }

        refreshed = RefreshCore();
        if (!refreshed.IsSuccess) return refreshed;
        var snapshot = BuildSnapshot();
        var nonFlat = snapshot.Economics.Positions
            .Where(item => item.Quantity.Coefficient != 0)
            .ToArray();
        if (nonFlat.Length == 0)
            return PaperExecutionClientResult.Success(
                $"Kill cancelled {cancellable.Length} outstanding order(s); Paper positions are flat and intake remains paused.");
        if (_flattenFactory is null)
            return PaperExecutionClientResult.Failure(
                ExecutionServiceFault.OmsRejected,
                "Kill cancelled outstanding orders but no exact flatten-order factory is composed.");

        foreach (var position in nonFlat)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_flattenFactory.TryCreateFlattenOrder(
                    position,
                    _service.LeaseGrant,
                    UtcNow(),
                    out var flatten,
                    out var reason) || flatten is null)
            {
                return PaperExecutionClientResult.Failure(
                    ExecutionServiceFault.InvalidRequest,
                    $"Kill could not construct a flatten order for instrument {position.InstrumentId.Value}: {reason}");
            }
            var submitted = SubmitCore(flatten, "kill-flatten");
            if (!submitted.IsSuccess)
                return PaperExecutionClientResult.Failure(
                    submitted.Fault,
                    $"Kill flatten failed for instrument {position.InstrumentId.Value}: {submitted.Message}");
        }

        refreshed = RefreshCore();
        if (!refreshed.IsSuccess) return refreshed;
        var remaining = BuildSnapshot().Economics.Positions
            .Where(item => item.Quantity.Coefficient != 0)
            .ToArray();
        return remaining.Length == 0
            ? PaperExecutionClientResult.Success(
                $"Kill cancelled {cancellable.Length} outstanding order(s), flattened {nonFlat.Length} position(s), verified Paper flatness, and paused intake.")
            : PaperExecutionClientResult.Failure(
                ExecutionServiceFault.OmsRejected,
                $"Kill completed its commands but {remaining.Length} non-flat Paper position(s) remain.");
    }

    private PaperExecutionClientResult SubmitCore(ExecutionSubmitRequest request, string operation)
    {
        var exchange = _service.Handle(MutationRequest(
            operation,
            ExecutionServiceRequestKind.Submit,
            submit: request));
        var result = FromExchange(exchange, "Order accepted by the Paper OMS.");
        return RefreshAfterMutation(result);
    }

    private PaperExecutionClientResult CancelCore(ClientOrderId clientOrderId, string operation)
    {
        if (clientOrderId.IsEmpty)
            return PaperExecutionClientResult.Failure(ExecutionServiceFault.InvalidRequest, "A client order id is required.");
        var refresh = RefreshCore();
        if (!refresh.IsSuccess) return refresh;
        var projection = Projection(clientOrderId);
        if (projection is null)
            return PaperExecutionClientResult.Failure(
                ExecutionServiceFault.InvalidRequest,
                "Cancel refused because the order is absent from the verified client view.");
        var command = new CancelOrderCommand(
            Metadata(projection, operation),
            projection.OrderId);
        var exchange = _service.Handle(MutationRequest(
            operation,
            ExecutionServiceRequestKind.Cancel,
            cancel: new ExecutionCancelRequest(command)));
        var result = FromExchange(exchange, "Cancellation accepted by the Paper OMS.");
        return result.IsSuccess ? RefreshCore() : result;
    }

    private PaperExecutionClientResult RefreshCore()
    {
        var status = _service.Handle(ReadRequest("status", ExecutionServiceRequestKind.Status));
        _leaseHeld = status.Response.IsSuccess;
        var statusFault = status.Response.Fault;
        var statusReason = status.Response.Reason;

        while (true)
        {
            var exchange = _service.Handle(ReadRequest(
                "resync",
                ExecutionServiceRequestKind.Resync,
                _cursor));
            if (!exchange.Response.IsSuccess)
                return FromExchange(exchange, "");
            try
            {
                Apply(exchange.Events);
            }
            catch (Exception exception) when (exception is InvalidDataException or OverflowException)
            {
                return PaperExecutionClientResult.Failure(
                    ExecutionServiceFault.InternalFailure,
                    $"Execution resync failed closed: {exception.Message}");
            }
            _cursor = exchange.Response.LastOutboxSequence;
            if (exchange.Events.Count < ExecutionServiceProtocol.MaximumEventsPerExchange)
                break;
        }

        var reconciliation = RefreshReconciliationCasesCore();
        if (!reconciliation.IsSuccess) return reconciliation;

        return statusFault == ExecutionServiceFault.None
            ? PaperExecutionClientResult.Success("Paper execution state synchronized.")
            : PaperExecutionClientResult.Failure(
                statusFault,
                statusReason ?? "The Paper writer lease is not current; read-only state was synchronized.");
    }

    private PaperExecutionClientResult RefreshAfterMutation(PaperExecutionClientResult mutation)
    {
        var refresh = RefreshCore();
        if (!refresh.IsSuccess)
        {
            return PaperExecutionClientResult.Failure(
                refresh.Fault,
                $"{mutation.Message} The immutable execution state could not be resynchronized: {refresh.Message}");
        }

        return mutation.IsSuccess ? refresh : mutation;
    }

    private PaperExecutionClientResult RefreshReconciliationCasesCore()
    {
        var cases = new Dictionary<ReconciliationCaseId, ReconciliationCase>();
        ReconciliationCaseId? after = null;
        var admissionBlocked = false;
        while (true)
        {
            var exchange = _service.Handle(ReadRequest(
                "reconciliation-cases",
                ExecutionServiceRequestKind.ReconciliationCases,
                afterReconciliationCaseId: after));
            if (!exchange.Response.IsSuccess)
                return FromExchange(exchange, string.Empty);
            if (exchange.Response.ReconciliationCaseCount != exchange.CaseFacts.Count)
                return PaperExecutionClientResult.Failure(
                    ExecutionServiceFault.InternalFailure,
                    "Reconciliation synchronization failed closed because the case count disagrees with the response envelope.");
            foreach (var item in exchange.CaseFacts)
            {
                if (!item.IsValid || item.Resource != _service.Resource ||
                    cases.ContainsKey(item.CaseId))
                    return PaperExecutionClientResult.Failure(
                        ExecutionServiceFault.InternalFailure,
                        "Reconciliation synchronization failed closed because a case is invalid, duplicated, or misrouted.");
                cases.Add(item.CaseId, item);
            }
            admissionBlocked = exchange.Response.ReconciliationAdmissionBlocked;
            if (!exchange.Response.HasMoreReconciliationCases) break;
            if (!exchange.Response.LastReconciliationCaseId.HasValue)
                return PaperExecutionClientResult.Failure(
                    ExecutionServiceFault.InternalFailure,
                    "Reconciliation synchronization failed closed because a paged response omitted its cursor.");
            after = exchange.Response.LastReconciliationCaseId;
        }

        _reconciliationCases.Clear();
        foreach (var item in cases) _reconciliationCases.Add(item.Key, item.Value);
        _reconciliationAdmissionBlocked = admissionBlocked;
        return PaperExecutionClientResult.Success("Reconciliation cases synchronized.");
    }

    private void Apply(IReadOnlyList<ExecutionServiceEvent> events)
    {
        foreach (var item in events)
        {
            if (item.OutboxSequence != _cursor + 1)
                throw new InvalidDataException("The execution outbox contains a sequence gap or duplicate.");
            if (!_streams.TryGetValue(item.Event.AggregateId, out var stream))
            {
                stream = [];
                _streams.Add(item.Event.AggregateId, stream);
            }
            stream.Add(item.Event);
            var projection = OmsOrderProjection.Rebuild(stream);
            if (!projection.IsSuccess)
            {
                stream.RemoveAt(stream.Count - 1);
                throw new InvalidDataException(
                    $"Order projection {item.Event.AggregateId} failed: {projection.Fault}/{projection.ChainFault}.");
            }
            _outbox.Add(item);
            _cursor = item.OutboxSequence;
        }
    }

    private PaperExecutionClientSnapshot BuildSnapshot()
    {
        var observedAt = UtcNow();
        var orders = Projections();
        var ledgerView = new ClientLedgerView(_outbox, _streams, orders);
        var economics = ExecutionReconciliationSnapshotBuilder.FromLedger(
            _service.Resource,
            observedAt,
            ledgerView);
        var quality = BuildExecutionQuality(orders);
        var analytics = PaperPortfolioAnalyticsCalculator.Calculate(
            _openingBalance,
            economics,
            observedAt,
            _currentMark);
        return new PaperExecutionClientSnapshot(
            _service.Resource,
            _service.LeaseGrant.Claim.LeaseId,
            _service.LeaseGrant.Claim.FencingToken,
            _leaseHeld,
            _intakePaused,
            _cursor,
            orders,
            economics,
            Array.AsReadOnly(_outbox.Select(item => item.Event).ToArray()),
            observedAt,
            _lastOperationMessage,
            Array.AsReadOnly(_reconciliationCases.Values
                .OrderByDescending(item => item.OpenedAtUtc)
                .ThenBy(item => item.CaseId.Value, StringComparer.Ordinal)
                .ToArray()),
            _reconciliationAdmissionBlocked,
            Array.AsReadOnly(_outbox
                .Where(item => item.Event.RiskObservation is not null)
                .Select(item => new PaperRiskDecisionSnapshot(
                    item.Event.AggregateId,
                    item.Event.AggregateSequence,
                    item.Event.Kind,
                    item.Event.RecordedAtUtc,
                    item.Event.RiskObservation!))
                .ToArray()),
            quality,
            analytics);
    }

    private PaperExecutionQualitySnapshot BuildExecutionQuality(IReadOnlyList<OmsOrderProjection> orders)
    {
        var acknowledgementLatencies = _streams.Values
            .Select(stream =>
            {
                var dispatched = stream.FirstOrDefault(item => item.Kind == OrderEventKind.SubmissionRecorded);
                var acknowledged = stream.FirstOrDefault(item => item.Kind == OrderEventKind.VenueAcknowledged);
                return dispatched is null || acknowledged is null || acknowledged.OccurredAtUtc < dispatched.OccurredAtUtc
                    ? (double?)null
                    : (acknowledged.OccurredAtUtc - dispatched.OccurredAtUtc).TotalMilliseconds;
            })
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToArray();
        return new PaperExecutionQualitySnapshot(
            orders.Count,
            orders.Count(item => item.State == OrderLifecycleState.Filled),
            orders.Count(item => item.State == OrderLifecycleState.Rejected),
            _outbox.Count(item => item.Event.Kind == OrderEventKind.CancelRequested),
            _reconciliationCases.Count,
            orders.Count(item => item.State == OrderLifecycleState.Unknown),
            0,
            0d,
            acknowledgementLatencies.Length,
            acknowledgementLatencies.Sum());
    }

    private IReadOnlyList<OmsOrderProjection> Projections() =>
        Array.AsReadOnly(_streams
            .OrderBy(item => item.Key.Value, StringComparer.Ordinal)
            .Select(item => OmsOrderProjection.Rebuild(item.Value))
            .Where(item => item.IsSuccess)
            .Select(item => item.Projection!)
            .ToArray());

    private OmsOrderProjection? Projection(ClientOrderId clientOrderId) =>
        _streams.TryGetValue(clientOrderId, out var stream)
            ? OmsOrderProjection.Rebuild(stream).Projection
            : null;

    private ExecutionCommandMetadata Metadata(OmsOrderProjection projection, string operation)
    {
        var original = projection.SubmitCommand.Metadata;
        var token = NextRequestId(operation);
        return new ExecutionCommandMetadata(
            new CommandId($"client-command-{token}"),
            original.CorrelationId,
            new CausationId($"client-cause-{token}"),
            original.TradingAccountId,
            original.StrategyId,
            original.StrategyVersion,
            original.VenueId,
            original.InstrumentId,
            original.Environment,
            UtcNow(),
            projection.LastSequence);
    }

    private ExecutionServiceRequest ReadRequest(
        string operation,
        ExecutionServiceRequestKind kind,
        long afterOutboxSequence = 0,
        ReconciliationCaseId? afterReconciliationCaseId = null) =>
        new(
            ExecutionServiceProtocol.CurrentVersion,
            NextRequestId(operation),
            kind,
            _service.Resource,
            _service.LeaseGrant.Claim.LeaseId,
            _service.LeaseGrant.Claim.FencingToken,
            afterOutboxSequence,
            AfterReconciliationCaseId: afterReconciliationCaseId);

    private ExecutionServiceRequest MutationRequest(
        string operation,
        ExecutionServiceRequestKind kind,
        ExecutionSubmitRequest? submit = null,
        ExecutionCancelRequest? cancel = null,
        ExecutionReplaceRequest? replace = null,
        ReconciliationTrigger? trigger = null,
        ExecutionReconciliationResolutionRequest? resolution = null) =>
        new(
            ExecutionServiceProtocol.CurrentVersion,
            NextRequestId(operation),
            kind,
            _service.Resource,
            _service.LeaseGrant.Claim.LeaseId,
            _service.LeaseGrant.Claim.FencingToken,
            Submit: submit,
            Cancel: cancel,
            Replace: replace,
            ReconciliationTrigger: trigger,
            ReconciliationResolution: resolution);

    private string NextRequestId(string operation) =>
        $"desktop-{_requestSession}-{operation}-{checked(++_requestSequence)}";

    private static PaperExecutionClientResult FromExchange(
        ExecutionServiceExchange exchange,
        string successMessage) =>
        exchange.Response.IsSuccess
            ? PaperExecutionClientResult.Success(successMessage)
            : PaperExecutionClientResult.Failure(
                exchange.Response.Fault,
                exchange.Response.Reason ?? exchange.Response.Fault.ToString());

    private DateTimeOffset UtcNow()
    {
        var now = _clock.UtcNow;
        if (now.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("The Paper execution client clock must return UTC.");
        return new DateTimeOffset(now);
    }

    private void Invalidate() => SnapshotInvalidated?.Invoke(this, EventArgs.Empty);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private sealed class ClientLedgerView(
        IReadOnlyList<ExecutionServiceEvent> outbox,
        IReadOnlyDictionary<ClientOrderId, List<OmsOrderEvent>> streams,
        IReadOnlyList<OmsOrderProjection> projections) : IOrderEventStore
    {
        private readonly IReadOnlyDictionary<ClientOrderId, OmsOrderProjection> _projections =
            projections.ToDictionary(item => item.ClientOrderId);

        public OrderEventAppendResult Append(OrderEventDraft draft, DateTimeOffset recordedAtUtc) =>
            throw new NotSupportedException("The desktop client ledger view is read-only.");

        public IReadOnlyList<OmsOrderEvent> Read(ClientOrderId aggregateId) =>
            streams.TryGetValue(aggregateId, out var stream)
                ? Array.AsReadOnly(stream.ToArray())
                : Array.Empty<OmsOrderEvent>();

        public OmsOrderProjection? ReadProjection(ClientOrderId aggregateId) =>
            _projections.GetValueOrDefault(aggregateId);

        public IReadOnlyList<OrderEventOutboxEntry> ReadOutbox(long afterExclusiveSequence = 0) =>
            Array.AsReadOnly(outbox
                .Where(item => item.OutboxSequence > afterExclusiveSequence)
                .Select(item => new OrderEventOutboxEntry(item.OutboxSequence, item.Event))
                .ToArray());
    }
}
