using System.Collections.ObjectModel;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Trading;

namespace TradingTerminal.Core.Execution;

public enum ReconciliationTrigger : byte
{
    Startup = 0,
    Reconnect = 1,
    UnknownOutcome = 2,
    Periodic = 3,
    OperatorRequest = 4,
}

public enum ReconciliationSubjectKind : byte
{
    Order = 0,
    Fill = 1,
    Position = 2,
    Cash = 3,
    Account = 4,
}

public enum ReconciliationCaseKind : byte
{
    Matched = 0,
    LocallyMissing = 1,
    BrokerMissing = 2,
    IdentityMismatch = 3,
    QuantityMismatch = 4,
    PriceMismatch = 5,
    LifecycleMismatch = 6,
    FillMismatch = 7,
    PositionMismatch = 8,
    CashMismatch = 9,
    DuplicateIdentity = 10,
    ManualException = 11,
}

public enum ReconciliationCaseStatus : byte
{
    Open = 0,
    Investigating = 1,
    Resolved = 2,
}

/// <summary>One append-only discrepancy or resolution fact.</summary>
public sealed record ReconciliationCase
{
    public ReconciliationCase(
        ReconciliationCaseId caseId,
        ExecutionResource resource,
        ReconciliationSubjectKind subjectKind,
        string subjectKey,
        ClientOrderId? clientOrderId,
        ReconciliationCaseKind kind,
        ReconciliationCaseStatus status,
        string localEvidence,
        string brokerEvidence,
        DateTimeOffset openedAtUtc,
        DateTimeOffset? resolvedAtUtc = null,
        string? resolvedBy = null,
        string? resolutionEvidence = null)
    {
        CaseId = caseId;
        Resource = resource;
        SubjectKind = subjectKind;
        SubjectKey = subjectKey;
        ClientOrderId = clientOrderId;
        Kind = kind;
        Status = status;
        LocalEvidence = localEvidence;
        BrokerEvidence = brokerEvidence;
        OpenedAtUtc = openedAtUtc;
        ResolvedAtUtc = resolvedAtUtc;
        ResolvedBy = resolvedBy;
        ResolutionEvidence = resolutionEvidence;
        if (!IsValid) throw new ArgumentException("The reconciliation case is invalid.", nameof(caseId));
    }

    public ReconciliationCaseId CaseId { get; }
    public ExecutionResource Resource { get; }
    public ReconciliationSubjectKind SubjectKind { get; }
    public string SubjectKey { get; }
    public ClientOrderId? ClientOrderId { get; }
    public ReconciliationCaseKind Kind { get; }
    public ReconciliationCaseStatus Status { get; }
    public string LocalEvidence { get; }
    public string BrokerEvidence { get; }
    public DateTimeOffset OpenedAtUtc { get; }
    public DateTimeOffset? ResolvedAtUtc { get; }
    public string? ResolvedBy { get; }
    public string? ResolutionEvidence { get; }
    public bool IsMaterial => Kind != ReconciliationCaseKind.Matched;

    public bool IsValid =>
        !CaseId.IsEmpty &&
        Resource.IsValid &&
        Enum.IsDefined(SubjectKind) &&
        !string.IsNullOrWhiteSpace(SubjectKey) && SubjectKey.Length <= 512 &&
        (!ClientOrderId.HasValue || !ClientOrderId.Value.IsEmpty) &&
        Enum.IsDefined(Kind) &&
        Enum.IsDefined(Status) &&
        !string.IsNullOrWhiteSpace(LocalEvidence) && LocalEvidence.Length <= 32_768 &&
        !string.IsNullOrWhiteSpace(BrokerEvidence) && BrokerEvidence.Length <= 32_768 &&
        OpenedAtUtc.Offset == TimeSpan.Zero &&
        (Status == ReconciliationCaseStatus.Resolved
            ? ResolvedAtUtc is { Offset: var offset } resolved &&
              offset == TimeSpan.Zero && resolved >= OpenedAtUtc &&
              !string.IsNullOrWhiteSpace(ResolvedBy) &&
              !string.IsNullOrWhiteSpace(ResolutionEvidence)
            : ResolvedAtUtc is null && ResolvedBy is null && ResolutionEvidence is null);

    public ReconciliationCase Resolve(DateTimeOffset resolvedAtUtc, string resolvedBy, string evidence) =>
        new(
            CaseId,
            Resource,
            SubjectKind,
            SubjectKey,
            ClientOrderId,
            Kind,
            ReconciliationCaseStatus.Resolved,
            LocalEvidence,
            BrokerEvidence,
            OpenedAtUtc,
            resolvedAtUtc,
            ExecutionValidation.RequireText(resolvedBy.Trim(), nameof(resolvedBy), 256),
            ExecutionValidation.RequireText(evidence.Trim(), nameof(evidence), 8192));
}

public interface IReconciliationCaseStore
{
    bool TryAppend(ReconciliationCase reconciliationCase);
    IReadOnlyList<ReconciliationCase> Read(ExecutionResource resource);
    IReadOnlyList<ReconciliationCase> Read(ReconciliationCaseId caseId);
}

public sealed class InMemoryReconciliationCaseStore : IReconciliationCaseStore
{
    private readonly object _gate = new();
    private readonly Dictionary<ReconciliationCaseId, List<ReconciliationCase>> _facts = [];

    public bool TryAppend(ReconciliationCase reconciliationCase)
    {
        if (reconciliationCase is null || !reconciliationCase.IsValid) return false;
        lock (_gate)
        {
            if (!_facts.TryGetValue(reconciliationCase.CaseId, out var sequence))
            {
                if (reconciliationCase.Status != ReconciliationCaseStatus.Open) return false;
                sequence = [];
                _facts.Add(reconciliationCase.CaseId, sequence);
            }
            else
            {
                var latest = sequence[^1];
                if (latest == reconciliationCase) return true;
                if (!SameObservation(latest, reconciliationCase) ||
                    latest.Status == ReconciliationCaseStatus.Resolved ||
                    reconciliationCase.Status <= latest.Status)
                    return false;
            }
            sequence.Add(reconciliationCase);
            return true;
        }
    }

    public IReadOnlyList<ReconciliationCase> Read(ExecutionResource resource)
    {
        lock (_gate)
        {
            return Array.AsReadOnly(_facts.Values.SelectMany(static item => item)
                .Where(item => item.Resource == resource)
                .OrderBy(item => item.CaseId.Value, StringComparer.Ordinal)
                .ThenBy(item => item.Status)
                .ToArray());
        }
    }

    public IReadOnlyList<ReconciliationCase> Read(ReconciliationCaseId caseId)
    {
        lock (_gate)
        {
            return _facts.TryGetValue(caseId, out var sequence)
                ? Array.AsReadOnly(sequence.ToArray())
                : Array.Empty<ReconciliationCase>();
        }
    }

    internal static bool SameObservation(ReconciliationCase left, ReconciliationCase right) =>
        left.CaseId == right.CaseId && left.Resource == right.Resource &&
        left.SubjectKind == right.SubjectKind &&
        string.Equals(left.SubjectKey, right.SubjectKey, StringComparison.Ordinal) &&
        left.ClientOrderId == right.ClientOrderId && left.Kind == right.Kind &&
        string.Equals(left.LocalEvidence, right.LocalEvidence, StringComparison.Ordinal) &&
        string.Equals(left.BrokerEvidence, right.BrokerEvidence, StringComparison.Ordinal) &&
        left.OpenedAtUtc == right.OpenedAtUtc;
}

public sealed record ReconciliationOrderSnapshot(
    CanonicalOrderInstruction Instruction,
    CanonicalOrderTerms CurrentTerms,
    OrderLifecycleState State,
    bool WasDispatched,
    BrokerOrderId? BrokerOrderId,
    ExchangeOrderId? ExchangeOrderId,
    ScaledQuantity FilledQuantity)
{
    public ClientOrderId ClientOrderId => Instruction.Identity.ClientOrderId;
    public bool RequiresBrokerPresence => WasDispatched && State is not OrderLifecycleState.Reconciled;
    public bool IsValid =>
        Instruction is not null && Instruction.Validate() == OrderDomainFault.None &&
        CurrentTerms.Validate() == OrderDomainFault.None && Enum.IsDefined(State) &&
        (!BrokerOrderId.HasValue || !BrokerOrderId.Value.IsEmpty) &&
        (!ExchangeOrderId.HasValue || !ExchangeOrderId.Value.IsEmpty) &&
        FilledQuantity.IsValid && FilledQuantity.Coefficient >= 0 &&
        ScaledValueMath.TryCompare(
            FilledQuantity.Coefficient, FilledQuantity.Scale,
            CurrentTerms.Quantity.Coefficient, CurrentTerms.Quantity.Scale,
            out var comparison) && comparison <= 0;
}

public sealed record ReconciliationFillSnapshot(
    TradeId TradeId,
    ClientOrderId ClientOrderId,
    BrokerOrderId? BrokerOrderId,
    ExchangeOrderId? ExchangeOrderId,
    InstrumentId InstrumentId,
    OrderSide Side,
    ScaledQuantity Quantity,
    ScaledPrice Price,
    ScaledMoney Fee,
    DateTimeOffset OccurredAtUtc)
{
    public bool IsValid =>
        !TradeId.IsEmpty && !ClientOrderId.IsEmpty &&
        (!BrokerOrderId.HasValue || !BrokerOrderId.Value.IsEmpty) &&
        (!ExchangeOrderId.HasValue || !ExchangeOrderId.Value.IsEmpty) &&
        !InstrumentId.IsNone && Enum.IsDefined(Side) &&
        Quantity.IsValid && Quantity.Coefficient > 0 &&
        Price.IsValid && Price.Coefficient > 0 &&
        Fee.IsValid && Fee.Coefficient >= 0 && OccurredAtUtc.Offset == TimeSpan.Zero;
}

public sealed record ReconciliationPositionSnapshot(
    InstrumentId InstrumentId,
    ScaledQuantity Quantity,
    DateTimeOffset ObservedAtUtc)
{
    public bool IsValid => !InstrumentId.IsNone && Quantity.IsValid && ObservedAtUtc.Offset == TimeSpan.Zero;
}

public sealed record ReconciliationCashSnapshot(
    string Currency,
    ScaledMoney Total,
    ScaledMoney Available,
    DateTimeOffset ObservedAtUtc)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(Currency) && Currency.Length <= 32 &&
        string.Equals(Currency, Currency.Trim(), StringComparison.Ordinal) &&
        Total.IsValid && Available.IsValid && ObservedAtUtc.Offset == TimeSpan.Zero;
}

/// <summary>One point-in-time local-ledger or broker/account truth snapshot.</summary>
public sealed record ExecutionReconciliationSnapshot(
    ExecutionResource Resource,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<ReconciliationOrderSnapshot> Orders,
    IReadOnlyList<ReconciliationFillSnapshot> Fills,
    IReadOnlyList<ReconciliationPositionSnapshot> Positions,
    IReadOnlyList<ReconciliationCashSnapshot> Cash)
{
    public bool TryValidate(out string? reason)
    {
        reason = null;
        if (!Resource.IsValid || CapturedAtUtc.Offset != TimeSpan.Zero ||
            Orders is null || Fills is null || Positions is null || Cash is null)
            return Fail("The snapshot envelope is invalid.", out reason);
        if (Orders.Any(static item => item is null || !item.IsValid) ||
            Fills.Any(static item => item is null || !item.IsValid) ||
            Positions.Any(static item => item is null || !item.IsValid) ||
            Cash.Any(static item => item is null || !item.IsValid))
            return Fail("A snapshot subject is invalid.", out reason);
        if (Fills.Any(item => item.OccurredAtUtc > CapturedAtUtc) ||
            Positions.Any(item => item.ObservedAtUtc > CapturedAtUtc) ||
            Cash.Any(item => item.ObservedAtUtc > CapturedAtUtc))
            return Fail("A snapshot observation occurs after capture.", out reason);
        if (HasDuplicates(Orders.Select(item => item.ClientOrderId.Value)) ||
            HasDuplicates(Orders.Where(item => item.BrokerOrderId.HasValue).Select(item => item.BrokerOrderId!.Value.Value)) ||
            HasDuplicates(Orders.Where(item => item.ExchangeOrderId.HasValue).Select(item => item.ExchangeOrderId!.Value.Value)) ||
            HasDuplicates(Fills.Select(item => item.TradeId.Value)) ||
            HasDuplicates(Positions.Select(item => item.InstrumentId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))) ||
            HasDuplicates(Cash.Select(item => item.Currency.ToUpperInvariant())))
            return Fail("The snapshot reuses a canonical or native identity.", out reason);
        return true;
    }

    private static bool HasDuplicates(IEnumerable<string> values) =>
        values.GroupBy(static value => value, StringComparer.Ordinal).Any(static group => group.Count() > 1);
    private static bool Fail(string value, out string? reason) { reason = value; return false; }
}

public interface IExecutionReconciliationSnapshotProvider
{
    ExecutionReconciliationSnapshot CaptureReconciliationSnapshot(
        ExecutionResource resource,
        DateTimeOffset capturedAtUtc);
}

/// <summary>Rebuilds the local side of reconciliation only from immutable ledger evidence.</summary>
public static class ExecutionReconciliationSnapshotBuilder
{
    public static ExecutionReconciliationSnapshot FromLedger(
        ExecutionResource resource,
        DateTimeOffset capturedAtUtc,
        IOrderEventStore eventStore,
        string cashCurrency = "SIM")
    {
        if (!resource.IsValid) throw new ArgumentException("The execution resource is invalid.", nameof(resource));
        ExecutionValidation.RequireUtc(capturedAtUtc, nameof(capturedAtUtc));
        ArgumentNullException.ThrowIfNull(eventStore);
        var currency = ExecutionValidation.RequireText(cashCurrency.Trim(), nameof(cashCurrency), 32);

        var aggregateIds = eventStore.ReadOutbox()
            .Select(item => item.Event.AggregateId)
            .Distinct()
            .OrderBy(item => item.Value, StringComparer.Ordinal)
            .ToArray();
        var orders = new List<ReconciliationOrderSnapshot>(aggregateIds.Length);
        var fills = new List<ReconciliationFillSnapshot>();
        var positionQuantities = new Dictionary<InstrumentId, ScaledQuantity>();
        var cash = ScaledMoney.Zero;
        var lastEconomicObservation = capturedAtUtc;

        foreach (var aggregateId in aggregateIds)
        {
            var projection = eventStore.ReadProjection(aggregateId) ??
                throw new InvalidDataException($"Ledger projection '{aggregateId}' is missing.");
            var events = eventStore.Read(aggregateId);
            if (events.Count == 0)
                throw new InvalidDataException($"Ledger event stream '{aggregateId}' is missing.");
            var wasDispatched = events.Any(item => item.Kind == OrderEventKind.SubmissionRecorded);
            orders.Add(new ReconciliationOrderSnapshot(
                projection.Instruction,
                projection.CanonicalTerms,
                projection.State,
                wasDispatched,
                projection.BrokerOrderId,
                projection.ExchangeOrderId,
                projection.FilledQuantity));

            foreach (var orderEvent in events.Where(item => item.Fill is not null))
            {
                var fill = orderEvent.Fill!;
                var snapshot = new ReconciliationFillSnapshot(
                    fill.TradeId,
                    projection.ClientOrderId,
                    orderEvent.BrokerOrderId ?? projection.BrokerOrderId,
                    orderEvent.ExchangeOrderId ?? projection.ExchangeOrderId,
                    projection.Instruction.TradeIntent.Instrument,
                    projection.CanonicalTerms.Side,
                    fill.Quantity,
                    fill.Price,
                    fill.Fee,
                    fill.OccurredAtUtc);
                fills.Add(snapshot);
                ApplyFill(snapshot, positionQuantities, ref cash);
                if (fill.OccurredAtUtc > lastEconomicObservation)
                    lastEconomicObservation = fill.OccurredAtUtc;
            }
        }

        var positions = positionQuantities
            .OrderBy(item => item.Key.Value)
            .Select(item => new ReconciliationPositionSnapshot(item.Key, item.Value, lastEconomicObservation))
            .ToArray();
        return new ExecutionReconciliationSnapshot(
            resource,
            capturedAtUtc,
            Array.AsReadOnly(orders.ToArray()),
            Array.AsReadOnly(fills.OrderBy(item => item.TradeId.Value, StringComparer.Ordinal).ToArray()),
            Array.AsReadOnly(positions),
            Array.AsReadOnly(new[] { new ReconciliationCashSnapshot(currency, cash, cash, lastEconomicObservation) }));
    }

    internal static void ApplyFill(
        ReconciliationFillSnapshot fill,
        Dictionary<InstrumentId, ScaledQuantity> positions,
        ref ScaledMoney cash)
    {
        if (!fill.IsValid) throw new InvalidDataException("A fill cannot be projected into reconciliation state.");
        var signedQuantity = fill.Side == OrderSide.Buy
            ? fill.Quantity
            : new ScaledQuantity(checked(-fill.Quantity.Coefficient), fill.Quantity.Scale);
        positions.TryGetValue(fill.InstrumentId, out var existing);
        if (!ScaledValueMath.TryAddQuantity(existing, signedQuantity, out var position))
            throw new OverflowException("Reconciliation position arithmetic overflowed.");
        positions[fill.InstrumentId] = position;

        if (!ScaledValueMath.TryMultiply(fill.Quantity.Coefficient, fill.Price.Coefficient, out var notional) ||
            !ScaledValueMath.TryNarrow(
                fill.Side == OrderSide.Buy ? -notional : notional,
                fill.Quantity.Scale + fill.Price.Scale,
                out var notionalCoefficient,
                out var notionalScale) ||
            !ScaledValueMath.TryAddMoney(cash, new ScaledMoney(notionalCoefficient, notionalScale), out var afterNotional) ||
            !ScaledValueMath.TryAddMoney(
                afterNotional,
                new ScaledMoney(checked(-fill.Fee.Coefficient), fill.Fee.Scale),
                out cash))
            throw new OverflowException("Reconciliation cash arithmetic overflowed.");
    }
}

public enum ReconciliationCycleFault : byte
{
    None = 0,
    InvalidInput = 1,
    SnapshotStale = 2,
    PersistenceRejected = 3,
}

public sealed record ReconciliationCycleResult(
    ReconciliationCycleFault Fault,
    ReconciliationTrigger Trigger,
    ExecutionResource Resource,
    DateTimeOffset CompletedAtUtc,
    IReadOnlyList<ReconciliationCase> Facts,
    int UnresolvedMaterialCaseCount,
    string? Reason = null)
{
    public bool IsSuccess => Fault == ReconciliationCycleFault.None;
    public bool IsAdmissionBlocked => !IsSuccess || UnresolvedMaterialCaseCount > 0;
}

public interface IExecutionReconciliationAdmissionGate
{
    bool CanAdmitNewExposure(ExecutionResource resource);
}

/// <summary>Exact, synchronous comparison engine. The host owns snapshot acquisition and trigger timing.</summary>
public sealed class ReconciliationEngine : IExecutionReconciliationAdmissionGate
{
    private static readonly TimeSpan MaximumBrokerSnapshotAge = TimeSpan.FromSeconds(5);
    private readonly object _gate = new();
    private readonly IReconciliationCaseStore _store;
    private readonly HashSet<ExecutionResource> _failedResources = [];

    public ReconciliationEngine(IReconciliationCaseStore store) =>
        _store = store ?? throw new ArgumentNullException(nameof(store));

    public bool CanAdmitNewExposure(ExecutionResource resource)
    {
        if (!resource.IsValid) return false;
        lock (_gate)
            return !_failedResources.Contains(resource) && CountOpen(_store.Read(resource)) == 0;
    }

    public ReconciliationCycleResult RunCycle(
        ReconciliationTrigger trigger,
        ExecutionReconciliationSnapshot local,
        ExecutionReconciliationSnapshot broker,
        DateTimeOffset completedAtUtc)
    {
        string? localReason = null;
        string? brokerReason = null;
        if (!Enum.IsDefined(trigger) || local is null || broker is null ||
            completedAtUtc.Offset != TimeSpan.Zero || local.Resource != broker.Resource ||
            !local.TryValidate(out localReason) || !broker.TryValidate(out brokerReason))
            return FailClosed(ReconciliationCycleFault.InvalidInput, trigger, local?.Resource ?? default,
                completedAtUtc, localReason ?? brokerReason ?? "The reconciliation inputs are invalid.");
        if (broker.CapturedAtUtc > completedAtUtc || completedAtUtc - broker.CapturedAtUtc > MaximumBrokerSnapshotAge)
            return FailClosed(ReconciliationCycleFault.SnapshotStale, trigger, local.Resource,
                completedAtUtc, "The broker snapshot is stale or from the future.");

        lock (_gate)
        {
            var observations = Compare(local, broker);
            var changedFacts = new List<ReconciliationCase>();
            var currentKeys = observations.Select(static item => item.Key).ToHashSet();
            var existing = _store.Read(local.Resource);
            foreach (var open in Latest(existing).Where(static item => item.Status != ReconciliationCaseStatus.Resolved))
            {
                var key = ObservationKey.Of(open.SubjectKind, open.SubjectKey, open.Kind);
                if (currentKeys.Contains(key)) continue;
                var resolved = open.Resolve(completedAtUtc, "system:reconciliation", "A later valid snapshot no longer reproduces this discrepancy.");
                if (!_store.TryAppend(resolved))
                    return PersistenceFailure(trigger, local.Resource, completedAtUtc, changedFacts);
                changedFacts.Add(resolved);
            }

            foreach (var observation in observations)
            {
                var prior = Latest(_store.Read(local.Resource)).FirstOrDefault(item =>
                    item.SubjectKind == observation.SubjectKind &&
                    string.Equals(item.SubjectKey, observation.SubjectKey, StringComparison.Ordinal) &&
                    item.Kind == observation.Kind && item.Status != ReconciliationCaseStatus.Resolved);
                if (prior is not null) continue;
                var opened = observation.ToCase(local.Resource, completedAtUtc);
                if (!_store.TryAppend(opened))
                    return PersistenceFailure(trigger, local.Resource, completedAtUtc, changedFacts);
                changedFacts.Add(opened);
            }

            _failedResources.Remove(local.Resource);
            var unresolved = CountOpen(_store.Read(local.Resource));
            return new(ReconciliationCycleFault.None, trigger, local.Resource, completedAtUtc,
                Array.AsReadOnly(changedFacts.ToArray()), unresolved);
        }
    }

    public bool ResolveCase(
        ReconciliationCaseId caseId,
        string resolvedBy,
        string resolutionEvidence,
        DateTimeOffset resolvedAtUtc)
    {
        if (caseId.IsEmpty || string.IsNullOrWhiteSpace(resolvedBy) ||
            string.IsNullOrWhiteSpace(resolutionEvidence) || resolvedAtUtc.Offset != TimeSpan.Zero)
            return false;
        lock (_gate)
        {
            var current = _store.Read(caseId).LastOrDefault();
            if (current is null) return false;
            if (current.Status == ReconciliationCaseStatus.Resolved) return true;
            return _store.TryAppend(current.Resolve(resolvedAtUtc, resolvedBy, resolutionEvidence));
        }
    }

    public ReconciliationCycleResult ReportAcquisitionFailure(
        ReconciliationTrigger trigger,
        ExecutionResource resource,
        DateTimeOffset failedAtUtc,
        string reason)
    {
        if (!Enum.IsDefined(trigger) || !resource.IsValid || failedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("The reconciliation failure identity is invalid.", nameof(resource));
        return FailClosed(
            ReconciliationCycleFault.InvalidInput,
            trigger,
            resource,
            failedAtUtc,
            ExecutionValidation.RequireText(reason, nameof(reason), 4096));
    }

    private static IReadOnlyList<Observation> Compare(
        ExecutionReconciliationSnapshot local,
        ExecutionReconciliationSnapshot broker)
    {
        var result = new List<Observation>();
        CompareOrders(local.Orders, broker.Orders, result);
        CompareFills(local.Fills, broker.Fills, result);
        ComparePositions(local.Positions, broker.Positions, result);
        CompareCash(local.Cash, broker.Cash, result);
        return result;
    }

    private static void CompareOrders(
        IReadOnlyList<ReconciliationOrderSnapshot> local,
        IReadOnlyList<ReconciliationOrderSnapshot> broker,
        List<Observation> result)
    {
        var brokerByClient = broker.ToDictionary(item => item.ClientOrderId);
        var matchedBroker = new HashSet<ClientOrderId>();
        foreach (var left in local)
        {
            if (!brokerByClient.TryGetValue(left.ClientOrderId, out var right))
            {
                var nativeMatches = broker.Where(item => NativeIdentityMatches(left, item)).ToArray();
                if (nativeMatches.Length > 0)
                {
                    result.Add(Observation.Order(left.ClientOrderId, ReconciliationCaseKind.IdentityMismatch, left, nativeMatches));
                    foreach (var match in nativeMatches) matchedBroker.Add(match.ClientOrderId);
                }
                else if (left.RequiresBrokerPresence)
                    result.Add(Observation.Order(left.ClientOrderId, ReconciliationCaseKind.BrokerMissing, left, null));
                continue;
            }
            matchedBroker.Add(right.ClientOrderId);
            if (left.BrokerOrderId != right.BrokerOrderId || left.ExchangeOrderId != right.ExchangeOrderId)
                result.Add(Observation.Order(left.ClientOrderId, ReconciliationCaseKind.IdentityMismatch, left, right));
            if (!QuantityEquals(left.CurrentTerms.Quantity, right.CurrentTerms.Quantity) ||
                !QuantityEquals(left.FilledQuantity, right.FilledQuantity))
                result.Add(Observation.Order(left.ClientOrderId, ReconciliationCaseKind.QuantityMismatch, left, right));
            if (!PriceEquals(left.CurrentTerms.LimitPrice, right.CurrentTerms.LimitPrice) ||
                !PriceEquals(left.CurrentTerms.StopPrice, right.CurrentTerms.StopPrice))
                result.Add(Observation.Order(left.ClientOrderId, ReconciliationCaseKind.PriceMismatch, left, right));
            if (left.CurrentTerms.Side != right.CurrentTerms.Side ||
                left.CurrentTerms.OrderType != right.CurrentTerms.OrderType ||
                left.CurrentTerms.TimeInForce != right.CurrentTerms.TimeInForce || left.State != right.State)
                result.Add(Observation.Order(left.ClientOrderId, ReconciliationCaseKind.LifecycleMismatch, left, right));
        }
        foreach (var right in broker.Where(item => !matchedBroker.Contains(item.ClientOrderId)))
            result.Add(Observation.Order(right.ClientOrderId, ReconciliationCaseKind.LocallyMissing, null, right));
    }

    private static void CompareFills(
        IReadOnlyList<ReconciliationFillSnapshot> local,
        IReadOnlyList<ReconciliationFillSnapshot> broker,
        List<Observation> result)
    {
        var localById = local.ToDictionary(item => item.TradeId);
        var brokerById = broker.ToDictionary(item => item.TradeId);
        foreach (var pair in localById)
        {
            if (!brokerById.TryGetValue(pair.Key, out var right))
            {
                result.Add(Observation.Fill(pair.Value, ReconciliationCaseKind.BrokerMissing, pair.Value, null));
                continue;
            }
            var left = pair.Value;
            if (left.ClientOrderId != right.ClientOrderId || left.BrokerOrderId != right.BrokerOrderId ||
                left.ExchangeOrderId != right.ExchangeOrderId || left.InstrumentId != right.InstrumentId ||
                left.Side != right.Side || !QuantityEquals(left.Quantity, right.Quantity) ||
                !PriceEquals(left.Price, right.Price) || !MoneyEquals(left.Fee, right.Fee))
                result.Add(Observation.Fill(left, ReconciliationCaseKind.FillMismatch, left, right));
        }
        foreach (var pair in brokerById.Where(pair => !localById.ContainsKey(pair.Key)))
            result.Add(Observation.Fill(pair.Value, ReconciliationCaseKind.LocallyMissing, null, pair.Value));
    }

    private static void ComparePositions(
        IReadOnlyList<ReconciliationPositionSnapshot> local,
        IReadOnlyList<ReconciliationPositionSnapshot> broker,
        List<Observation> result)
    {
        var localById = local.ToDictionary(item => item.InstrumentId);
        var brokerById = broker.ToDictionary(item => item.InstrumentId);
        foreach (var id in localById.Keys.Union(brokerById.Keys))
        {
            localById.TryGetValue(id, out var left);
            brokerById.TryGetValue(id, out var right);
            if (left is null || right is null || !QuantityEquals(left.Quantity, right.Quantity))
                result.Add(Observation.Position(id, left, right));
        }
    }

    private static void CompareCash(
        IReadOnlyList<ReconciliationCashSnapshot> local,
        IReadOnlyList<ReconciliationCashSnapshot> broker,
        List<Observation> result)
    {
        var localById = local.ToDictionary(item => item.Currency.ToUpperInvariant(), StringComparer.Ordinal);
        var brokerById = broker.ToDictionary(item => item.Currency.ToUpperInvariant(), StringComparer.Ordinal);
        foreach (var currency in localById.Keys.Union(brokerById.Keys, StringComparer.Ordinal))
        {
            localById.TryGetValue(currency, out var left);
            brokerById.TryGetValue(currency, out var right);
            if (left is null || right is null || !MoneyEquals(left.Total, right.Total) ||
                !MoneyEquals(left.Available, right.Available))
                result.Add(Observation.Cash(currency, left, right));
        }
    }

    private ReconciliationCycleResult FailClosed(
        ReconciliationCycleFault fault, ReconciliationTrigger trigger, ExecutionResource resource,
        DateTimeOffset completedAtUtc, string reason)
    {
        lock (_gate) if (resource.IsValid) _failedResources.Add(resource);
        return new(fault, trigger, resource, completedAtUtc, Array.Empty<ReconciliationCase>(),
            resource.IsValid ? CountOpen(_store.Read(resource)) : 0, reason);
    }

    private ReconciliationCycleResult PersistenceFailure(
        ReconciliationTrigger trigger, ExecutionResource resource, DateTimeOffset atUtc,
        List<ReconciliationCase> facts)
    {
        _failedResources.Add(resource);
        return new(ReconciliationCycleFault.PersistenceRejected, trigger, resource, atUtc,
            Array.AsReadOnly(facts.ToArray()), CountOpen(_store.Read(resource)),
            "A reconciliation case fact could not be appended.");
    }

    private static IEnumerable<ReconciliationCase> Latest(IReadOnlyList<ReconciliationCase> facts) =>
        facts.GroupBy(item => item.CaseId).Select(group => group.Last());
    private static int CountOpen(IReadOnlyList<ReconciliationCase> facts) =>
        Latest(facts).Count(item => item.IsMaterial && item.Status != ReconciliationCaseStatus.Resolved);
    private static bool NativeIdentityMatches(ReconciliationOrderSnapshot left, ReconciliationOrderSnapshot right) =>
        left.BrokerOrderId.HasValue && left.BrokerOrderId == right.BrokerOrderId ||
        left.ExchangeOrderId.HasValue && left.ExchangeOrderId == right.ExchangeOrderId;
    private static bool QuantityEquals(ScaledQuantity left, ScaledQuantity right) =>
        ScaledValueMath.TryCompare(left.Coefficient, left.Scale, right.Coefficient, right.Scale, out var value) && value == 0;
    private static bool PriceEquals(ScaledPrice left, ScaledPrice right) =>
        ScaledValueMath.TryCompare(left.Coefficient, left.Scale, right.Coefficient, right.Scale, out var value) && value == 0;
    private static bool PriceEquals(ScaledPrice? left, ScaledPrice? right) =>
        left.HasValue == right.HasValue && (!left.HasValue || PriceEquals(left.Value, right!.Value));
    private static bool MoneyEquals(ScaledMoney left, ScaledMoney right) =>
        ScaledValueMath.TryCompare(left.Coefficient, left.Scale, right.Coefficient, right.Scale, out var value) && value == 0;

    private readonly record struct ObservationKey(ReconciliationSubjectKind SubjectKind, string SubjectKey, ReconciliationCaseKind Kind)
    {
        public static ObservationKey Of(ReconciliationSubjectKind kind, string key, ReconciliationCaseKind caseKind) => new(kind, key, caseKind);
    }

    private sealed record Observation(
        ReconciliationSubjectKind SubjectKind,
        string SubjectKey,
        ClientOrderId? ClientOrderId,
        ReconciliationCaseKind Kind,
        string LocalEvidence,
        string BrokerEvidence)
    {
        public ObservationKey Key => ObservationKey.Of(SubjectKind, SubjectKey, Kind);

        public ReconciliationCase ToCase(ExecutionResource resource, DateTimeOffset openedAtUtc)
        {
            var seed = $"{resource.VenueId.Value}|{resource.TradingAccountId.Value}|{(byte)resource.Environment}|{(byte)SubjectKind}|{SubjectKey}|{(byte)Kind}";
            var caseId = new ReconciliationCaseId($"recon-{ExecutionCanonicalJson.Sha256(seed)}");
            return new(caseId, resource, SubjectKind, SubjectKey, ClientOrderId, Kind,
                ReconciliationCaseStatus.Open, LocalEvidence, BrokerEvidence, openedAtUtc);
        }

        public static Observation Order(ClientOrderId id, ReconciliationCaseKind kind, object? local, object? broker) =>
            new(ReconciliationSubjectKind.Order, id.Value, id, kind, Evidence(local), Evidence(broker));
        public static Observation Fill(ReconciliationFillSnapshot fill, ReconciliationCaseKind kind, object? local, object? broker) =>
            new(ReconciliationSubjectKind.Fill, fill.TradeId.Value, fill.ClientOrderId, kind, Evidence(local), Evidence(broker));
        public static Observation Position(InstrumentId id, object? local, object? broker) =>
            new(ReconciliationSubjectKind.Position, id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), null,
                ReconciliationCaseKind.PositionMismatch, Evidence(local), Evidence(broker));
        public static Observation Cash(string currency, object? local, object? broker) =>
            new(ReconciliationSubjectKind.Cash, currency, null, ReconciliationCaseKind.CashMismatch, Evidence(local), Evidence(broker));
        private static string Evidence(object? value) => value is null ? "v1:absent" : ExecutionCanonicalJson.Serialize(value);
    }
}

/// <summary>
/// Paper-only trigger boundary that independently captures ledger and venue truth at startup,
/// reconnect, or an explicit caller request. Snapshot acquisition failure closes admission.
/// </summary>
public sealed class PaperExecutionReconciliationCoordinator
{
    private readonly ExecutionResource _resource;
    private readonly IOrderEventStore _eventStore;
    private readonly IExecutionReconciliationSnapshotProvider _snapshotProvider;
    private readonly ReconciliationEngine _engine;
    private readonly TradingTerminal.Core.Time.IClock _clock;

    public PaperExecutionReconciliationCoordinator(
        ExecutionResource resource,
        IOrderEventStore eventStore,
        IExecutionReconciliationSnapshotProvider snapshotProvider,
        ReconciliationEngine engine,
        TradingTerminal.Core.Time.IClock clock)
    {
        if (!resource.IsValid) throw new ArgumentException("The execution resource is invalid.", nameof(resource));
        _resource = resource;
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public ReconciliationCycleResult RunStartup()
    {
        var result = Run(ReconciliationTrigger.Startup);
        if (result.IsAdmissionBlocked || _eventStore is not IExecutionStartupRecoveryGate recoveryGate)
            return result;
        if (recoveryGate.TryCompleteStartupReconciliation(result))
            return result;

        return _engine.ReportAcquisitionFailure(
            ReconciliationTrigger.Startup,
            _resource,
            result.CompletedAtUtc,
            "The durable startup recovery gate rejected reconciliation completion.");
    }
    public ReconciliationCycleResult RunReconnect() => Run(ReconciliationTrigger.Reconnect);

    public ReconciliationCycleResult Run(ReconciliationTrigger trigger)
    {
        var capturedAt = new DateTimeOffset(_clock.UtcNow);
        try
        {
            var local = ExecutionReconciliationSnapshotBuilder.FromLedger(_resource, capturedAt, _eventStore);
            var broker = _snapshotProvider.CaptureReconciliationSnapshot(_resource, capturedAt);
            return _engine.RunCycle(trigger, local, broker, capturedAt);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return _engine.ReportAcquisitionFailure(
                trigger,
                _resource,
                capturedAt,
                $"Snapshot acquisition failed: {exception.GetType().Name}.");
        }
    }
}
