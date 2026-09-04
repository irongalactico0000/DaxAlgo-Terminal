using System.Collections.ObjectModel;
using Microsoft.Data.Sqlite;
using TradingTerminal.Core.Execution;

namespace TradingTerminal.Infrastructure.Execution;

public enum SqliteExecutionLedgerFault : byte
{
    None = 0,
    SqliteIntegrityCheckFailed = 1,
    EventPayloadInvalid = 2,
    EventChainInvalid = 3,
    ProjectionMismatch = 4,
    DeliveryMetadataInvalid = 5,
    ReconciliationEvidenceInvalid = 6,
    EconomicProjectionMismatch = 7,
}

public readonly record struct SqliteExecutionLedgerIntegrity(
    SqliteExecutionLedgerFault Fault,
    ClientOrderId? AggregateId,
    int EventIndex,
    string Detail)
{
    public bool IsValid => Fault == SqliteExecutionLedgerFault.None;
}

public sealed record DurableOrderRecoveryEntry(
    ClientOrderId ClientOrderId,
    OrderLifecycleState State,
    long LastSequence);

public enum SqliteAppendStage : byte
{
    BeforeCommit = 0,
}

/// <summary>
/// File-backed Paper OMS event store. Inbox dedupe, immutable event, replayed projection, risk/fill
/// evidence, and outbox publication are committed in one SQLite transaction.
/// </summary>
public sealed class SqliteOrderEventStore :
    IOrderEventStore,
    IExecutionStartupRecoveryGate,
    IExecutionLeaseStore,
    IReconciliationCaseStore,
    IDisposable
{
    private readonly object _gate = new();
    private readonly SqliteConnection _connection;
    private readonly FileStream? _writerLease;
    private readonly Action<SqliteAppendStage>? _failureInjector;
    private readonly List<DurableOrderRecoveryEntry> _startupRecovery;
    private readonly ReadOnlyCollection<DurableOrderRecoveryEntry> _startupRecoveryView;
    private SqliteExecutionLedgerIntegrity _integrity;
    private bool _disposed;

    public SqliteOrderEventStore(
        string databasePath,
        DateTimeOffset migrationAtUtc,
        Action<SqliteAppendStage>? failureInjector = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        if (migrationAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Migration time must use UTC.", nameof(migrationAtUtc));

        DatabasePath = databasePath == ":memory:"
            ? databasePath
            : Path.GetFullPath(databasePath);
        _failureInjector = failureInjector;

        if (DatabasePath != ":memory:")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
            try
            {
                _writerLease = new FileStream(
                    DatabasePath + ".writer.lock",
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.Read,
                    1,
                    FileOptions.DeleteOnClose);
            }
            catch (IOException exception)
            {
                throw new IOException("Another writer already owns this execution ledger.", exception);
            }
        }

        _connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private,
        }.ToString());

        try
        {
            _connection.Open();
            JournalMode = SqliteOrderLedgerSchema.ApplyConnectionPragmas(_connection);
            if (DatabasePath != ":memory:" &&
                !string.Equals(JournalMode, "wal", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"The execution ledger requires WAL; SQLite returned '{JournalMode}'.");
            SchemaVersion = SqliteOrderLedgerSchema.EnsureCreated(_connection, migrationAtUtc);
            _integrity = VerifyIntegrityCore();
            _startupRecovery = _integrity.IsValid ? LoadStartupRecovery() : [];
            _startupRecoveryView = new ReadOnlyCollection<DurableOrderRecoveryEntry>(_startupRecovery);
        }
        catch
        {
            _connection.Dispose();
            _writerLease?.Dispose();
            throw;
        }
    }

    public string DatabasePath { get; }
    public int SchemaVersion { get; }
    public string JournalMode { get; }
    public SqliteExecutionLedgerIntegrity Integrity => _integrity;
    public IReadOnlyList<DurableOrderRecoveryEntry> StartupRecovery => _startupRecoveryView;
    public bool CanAdmitNewOrders
    {
        get { lock (_gate) return !_disposed && _integrity.IsValid && _startupRecovery.Count == 0; }
    }
    public bool CanAdmitAfterStartupReconciliation => CanAdmitNewOrders;

    public bool TryCompleteStartupReconciliation(ReconciliationCycleResult result)
    {
        if (result is null || result.Trigger != ReconciliationTrigger.Startup ||
            !result.IsSuccess || result.IsAdmissionBlocked || !result.Resource.IsValid ||
            result.CompletedAtUtc.Offset != TimeSpan.Zero)
            return false;

        lock (_gate)
        {
            ThrowIfDisposed();
            if (!_integrity.IsValid) return false;
            foreach (var entry in _startupRecovery)
            {
                var projection = ReadProjection(entry.ClientOrderId);
                if (projection is null ||
                    projection.SubmitCommand.Metadata.VenueId != result.Resource.VenueId ||
                    projection.SubmitCommand.Metadata.TradingAccountId != result.Resource.TradingAccountId ||
                    projection.SubmitCommand.Metadata.Environment != result.Resource.Environment)
                    return false;
            }
            _startupRecovery.Clear();
            return true;
        }
    }

    public OrderEventAppendResult Append(OrderEventDraft draft, DateTimeOffset recordedAtUtc)
    {
        var basicFault = ValidateDraft(draft, recordedAtUtc);
        if (basicFault != OrderEventAppendFault.None)
            return Rejected(basicFault);

        lock (_gate)
        {
            ThrowIfDisposed();
            if (!_integrity.IsValid)
                return Rejected(OrderEventAppendFault.LedgerIntegrityBlocked);

            using var transaction = _connection.BeginTransaction();
            var draftHash = ExecutionCanonicalJson.Hash(draft);
            var prior = ReadInbox(draft.Source, draft.DeduplicationKey, transaction);
            if (prior is not null)
            {
                if (!string.Equals(prior.Value.DraftHash, draftHash, StringComparison.Ordinal))
                    return Rejected(OrderEventAppendFault.ConflictingDuplicate);
                var priorEvent = ReadEvent(prior.Value.AggregateId, prior.Value.AggregateSequence, transaction);
                if (!string.Equals(priorEvent.EventHash, prior.Value.EventHash, StringComparison.Ordinal))
                    throw new InvalidDataException("Inbox metadata disagrees with the committed event.");
                return new OrderEventAppendResult(
                    OrderEventAppendStatus.ExactReplay,
                    OrderEventAppendFault.None,
                    priorEvent);
            }

            var stream = ReadStream(draft.AggregateId, transaction);
            var previous = stream.Count == 0 ? null : stream[^1];
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
                    return Rejected(OrderEventAppendFault.InvalidInitialEvent);
            }
            else if (!OrderLifecycle.CanApplyEvent(draft.Kind, previous.StateAfter, draft.StateAfter))
            {
                return Rejected(OrderEventAppendFault.IllegalTransition);
            }

            if (previous?.AggregateSequence == long.MaxValue)
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

            var candidate = new List<OmsOrderEvent>(stream.Count + 1);
            candidate.AddRange(stream);
            candidate.Add(committed);
            var projection = OmsOrderProjector.Rebuild(candidate);
            if (!projection.IsSuccess)
            {
                return new OrderEventAppendResult(
                    OrderEventAppendStatus.Rejected,
                    OrderEventAppendFault.ProjectionRejected,
                    null,
                    projection.Fault);
            }

            InsertEvent(committed, transaction);
            InsertInbox(draft, draftHash, committed, transaction);
            UpsertProjection(projection.Projection!, transaction);
            InsertRiskAndFill(committed, projection.Projection!, transaction);
            InsertOutbox(committed, transaction);
            _failureInjector?.Invoke(SqliteAppendStage.BeforeCommit);
            transaction.Commit();

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
            ThrowIfDisposed();
            var stream = ReadStream(aggregateId, null);
            return stream.Count == 0 ? Array.Empty<OmsOrderEvent>() : Array.AsReadOnly(stream.ToArray());
        }
    }

    public OmsOrderProjection? ReadProjection(ClientOrderId aggregateId)
    {
        if (aggregateId.IsEmpty) return null;
        lock (_gate)
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT projection_payload_json FROM execution_order_projections
                WHERE client_order_id = $aggregateId;
                """;
            command.Parameters.AddWithValue("$aggregateId", aggregateId.Value);
            return command.ExecuteScalar() is string json
                ? ExecutionCanonicalJson.Deserialize<OmsOrderProjection>(json)
                : null;
        }
    }

    public IReadOnlyList<OrderEventOutboxEntry> ReadOutbox(long afterExclusiveSequence = 0)
    {
        if (afterExclusiveSequence < 0) throw new ArgumentOutOfRangeException(nameof(afterExclusiveSequence));
        lock (_gate)
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT o.outbox_sequence, e.event_payload_json
                FROM execution_outbox AS o
                JOIN execution_order_events AS e
                  ON e.aggregate_id = o.aggregate_id
                 AND e.aggregate_sequence = o.aggregate_sequence
                WHERE o.outbox_sequence > $after
                ORDER BY o.outbox_sequence;
                """;
            command.Parameters.AddWithValue("$after", afterExclusiveSequence);
            using var reader = command.ExecuteReader();
            var entries = new List<OrderEventOutboxEntry>();
            while (reader.Read())
            {
                entries.Add(new OrderEventOutboxEntry(
                    reader.GetInt64(0),
                    ExecutionCanonicalJson.Deserialize<OmsOrderEvent>(reader.GetString(1))));
            }
            return entries.Count == 0 ? Array.Empty<OrderEventOutboxEntry>() : Array.AsReadOnly(entries.ToArray());
        }
    }

    public IReadOnlyList<ReconciliationPositionSnapshot> ReadPositionProjections(ExecutionResource resource)
    {
        if (!resource.IsValid) throw new ArgumentException("The execution resource is invalid.", nameof(resource));
        lock (_gate)
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT instrument_id, quantity_coefficient, quantity_scale, observed_at_utc_ticks
                FROM execution_position_projections
                WHERE venue_id = $venueId AND trading_account_id = $accountId AND environment = $environment
                ORDER BY instrument_id;
                """;
            AddResource(command, resource);
            using var reader = command.ExecuteReader();
            var result = new List<ReconciliationPositionSnapshot>();
            while (reader.Read())
            {
                result.Add(new ReconciliationPositionSnapshot(
                    new TradingTerminal.Core.Domain.InstrumentId(checked((int)reader.GetInt64(0))),
                    new ScaledQuantity(reader.GetInt64(1), checked((byte)reader.GetInt32(2))),
                    UtcFromTicks(reader.GetInt64(3))));
            }
            return result.Count == 0 ? [] : Array.AsReadOnly(result.ToArray());
        }
    }

    public IReadOnlyList<ReconciliationCashSnapshot> ReadCashProjections(ExecutionResource resource)
    {
        if (!resource.IsValid) throw new ArgumentException("The execution resource is invalid.", nameof(resource));
        lock (_gate)
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT currency, total_coefficient, total_scale,
                       available_coefficient, available_scale, observed_at_utc_ticks
                FROM execution_cash_projections
                WHERE venue_id = $venueId AND trading_account_id = $accountId AND environment = $environment
                ORDER BY currency;
                """;
            AddResource(command, resource);
            using var reader = command.ExecuteReader();
            var result = new List<ReconciliationCashSnapshot>();
            while (reader.Read())
            {
                result.Add(new ReconciliationCashSnapshot(
                    reader.GetString(0),
                    new ScaledMoney(reader.GetInt64(1), checked((byte)reader.GetInt32(2))),
                    new ScaledMoney(reader.GetInt64(3), checked((byte)reader.GetInt32(4))),
                    UtcFromTicks(reader.GetInt64(5))));
            }
            return result.Count == 0 ? [] : Array.AsReadOnly(result.ToArray());
        }
    }

    public SqliteExecutionLedgerIntegrity VerifyIntegrity()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            _integrity = VerifyIntegrityCore();
            return _integrity;
        }
    }

    public bool TryAppend(ReconciliationCase reconciliationCase)
    {
        if (reconciliationCase is null || !reconciliationCase.IsValid) return false;
        lock (_gate)
        {
            ThrowIfDisposed();
            if (!_integrity.IsValid) return false;
            using var transaction = _connection.BeginTransaction();
            var existing = ReadReconciliationCase(reconciliationCase.CaseId, transaction);
            if (existing.Count == 0)
            {
                if (reconciliationCase.Status != ReconciliationCaseStatus.Open) return false;
            }
            else
            {
                var latest = existing[^1];
                if (latest == reconciliationCase) return true;
                if (!InMemoryReconciliationCaseStore.SameObservation(latest, reconciliationCase) ||
                    latest.Status == ReconciliationCaseStatus.Resolved ||
                    reconciliationCase.Status <= latest.Status)
                    return false;
            }

            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO execution_reconciliation_cases(
                    case_id, fact_sequence, venue_id, trading_account_id, environment,
                    subject_kind, subject_key, client_order_id, case_kind, case_status,
                    opened_at_utc_ticks, resolved_at_utc_ticks, case_payload_json)
                VALUES ($caseId, $sequence, $venueId, $accountId, $environment,
                        $subjectKind, $subjectKey, $clientOrderId, $caseKind, $status,
                        $openedAt, $resolvedAt, $payload);
                """;
            command.Parameters.AddWithValue("$caseId", reconciliationCase.CaseId.Value);
            command.Parameters.AddWithValue("$sequence", existing.Count + 1L);
            AddResource(command, reconciliationCase.Resource);
            command.Parameters.AddWithValue("$subjectKind", (int)reconciliationCase.SubjectKind);
            command.Parameters.AddWithValue("$subjectKey", reconciliationCase.SubjectKey);
            command.Parameters.AddWithValue("$clientOrderId", Db(reconciliationCase.ClientOrderId?.Value));
            command.Parameters.AddWithValue("$caseKind", (int)reconciliationCase.Kind);
            command.Parameters.AddWithValue("$status", (int)reconciliationCase.Status);
            command.Parameters.AddWithValue("$openedAt", reconciliationCase.OpenedAtUtc.UtcDateTime.Ticks);
            command.Parameters.AddWithValue("$resolvedAt", Db(reconciliationCase.ResolvedAtUtc?.UtcDateTime.Ticks));
            command.Parameters.AddWithValue("$payload", ExecutionCanonicalJson.Serialize(reconciliationCase));
            command.ExecuteNonQuery();
            transaction.Commit();
            return true;
        }
    }

    public IReadOnlyList<ReconciliationCase> Read(ExecutionResource resource)
    {
        if (!resource.IsValid) return Array.Empty<ReconciliationCase>();
        lock (_gate)
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = """
                SELECT case_id, fact_sequence, venue_id, trading_account_id, environment,
                       subject_kind, subject_key, client_order_id, case_kind, case_status,
                       opened_at_utc_ticks, resolved_at_utc_ticks, case_payload_json
                FROM execution_reconciliation_cases
                WHERE venue_id = $venueId AND trading_account_id = $accountId AND environment = $environment
                ORDER BY case_id, fact_sequence;
                """;
            AddResource(command, resource);
            return ReadReconciliationRows(command);
        }
    }

    public IReadOnlyList<ReconciliationCase> Read(ReconciliationCaseId caseId)
    {
        if (caseId.IsEmpty) return Array.Empty<ReconciliationCase>();
        lock (_gate)
        {
            ThrowIfDisposed();
            return ReadReconciliationCase(caseId, null);
        }
    }

    public ExecutionLeaseAcquireResult Acquire(
        ExecutionResource resource,
        ExecutionLeaseId leaseId,
        RuntimeInstanceId ownerId,
        DateTimeOffset acquiredAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (!resource.IsValid || leaseId.IsEmpty || ownerId.IsEmpty ||
            acquiredAtUtc.Offset != TimeSpan.Zero || expiresAtUtc.Offset != TimeSpan.Zero ||
            expiresAtUtc <= acquiredAtUtc)
        {
            return LeaseAcquireFailed(ExecutionLeaseFault.InvalidInput, "The lease acquisition fields are invalid.");
        }

        lock (_gate)
        {
            ThrowIfDisposed();
            if (!_integrity.IsValid)
                return LeaseAcquireFailed(ExecutionLeaseFault.NotCurrent, "Ledger integrity blocks lease acquisition.");
            using var transaction = _connection.BeginTransaction();
            if (LeaseIdExists(leaseId, transaction))
                return LeaseAcquireFailed(ExecutionLeaseFault.LeaseIdentityConflict, "The lease id was already used.");
            var prior = ReadLatestLease(resource, transaction);
            if (prior is not null && prior.Value.ReleasedAtUtc is null && prior.Value.Grant.ExpiresAtUtc > acquiredAtUtc)
                return LeaseAcquireFailed(ExecutionLeaseFault.HeldByAnotherOwner, "An unexpired owner already holds the resource.");
            var priorToken = prior?.Grant.Claim.FencingToken.Value ?? 0L;
            if (priorToken == long.MaxValue)
                return LeaseAcquireFailed(ExecutionLeaseFault.TokenExhausted, "The fencing-token space is exhausted.");

            var grant = new ExecutionLeaseGrant(
                new ExecutionLeaseClaim(resource, leaseId, new FencingToken(checked(priorToken + 1))),
                ownerId,
                acquiredAtUtc,
                expiresAtUtc);
            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO execution_lease_generations(
                    venue_id, trading_account_id, environment, fencing_token,
                    execution_lease_id, owner_runtime_id, acquired_at_utc_ticks, expires_at_utc_ticks)
                VALUES ($venueId, $accountId, $environment, $token,
                        $leaseId, $ownerId, $acquiredAt, $expiresAt);
                """;
            AddResource(command, resource);
            command.Parameters.AddWithValue("$token", grant.Claim.FencingToken.Value);
            command.Parameters.AddWithValue("$leaseId", leaseId.Value);
            command.Parameters.AddWithValue("$ownerId", ownerId.Value);
            command.Parameters.AddWithValue("$acquiredAt", acquiredAtUtc.UtcDateTime.Ticks);
            command.Parameters.AddWithValue("$expiresAt", expiresAtUtc.UtcDateTime.Ticks);
            command.ExecuteNonQuery();
            transaction.Commit();
            return new ExecutionLeaseAcquireResult(ExecutionLeaseFault.None, grant);
        }
    }

    public ExecutionLeaseMutationResult Renew(
        in ExecutionLeaseGrant grant,
        DateTimeOffset renewedAtUtc,
        DateTimeOffset expiresAtUtc)
    {
        if (!grant.IsValid || renewedAtUtc.Offset != TimeSpan.Zero || expiresAtUtc.Offset != TimeSpan.Zero ||
            expiresAtUtc <= renewedAtUtc)
            return LeaseMutationFailed(ExecutionLeaseFault.InvalidInput, "The renewal fields are invalid.");

        lock (_gate)
        {
            ThrowIfDisposed();
            using var transaction = _connection.BeginTransaction();
            var current = ReadLatestLease(grant.Claim.Resource, transaction);
            var fault = CompareCurrent(current, grant, renewedAtUtc, permitExpired: false);
            if (fault != ExecutionLeaseFault.None)
                return LeaseMutationFailed(fault, $"The lease cannot be renewed: {fault}.");

            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE execution_lease_generations
                SET expires_at_utc_ticks = $expiresAt, renewed_at_utc_ticks = $renewedAt
                WHERE venue_id = $venueId AND trading_account_id = $accountId
                  AND environment = $environment AND fencing_token = $token;
                """;
            AddResource(command, grant.Claim.Resource);
            command.Parameters.AddWithValue("$token", grant.Claim.FencingToken.Value);
            command.Parameters.AddWithValue("$renewedAt", renewedAtUtc.UtcDateTime.Ticks);
            command.Parameters.AddWithValue("$expiresAt", expiresAtUtc.UtcDateTime.Ticks);
            if (command.ExecuteNonQuery() != 1)
                return LeaseMutationFailed(ExecutionLeaseFault.NotCurrent, "The current lease row disappeared.");
            transaction.Commit();
            return new ExecutionLeaseMutationResult(
                ExecutionLeaseFault.None,
                grant with { ExpiresAtUtc = expiresAtUtc });
        }
    }

    public ExecutionLeaseMutationResult Release(
        in ExecutionLeaseGrant grant,
        DateTimeOffset releasedAtUtc)
    {
        if (!grant.IsValid || releasedAtUtc.Offset != TimeSpan.Zero)
            return LeaseMutationFailed(ExecutionLeaseFault.InvalidInput, "The release fields are invalid.");

        lock (_gate)
        {
            ThrowIfDisposed();
            using var transaction = _connection.BeginTransaction();
            var current = ReadLatestLease(grant.Claim.Resource, transaction);
            var fault = CompareCurrent(current, grant, releasedAtUtc, permitExpired: true);
            if (fault != ExecutionLeaseFault.None)
                return LeaseMutationFailed(fault, $"The lease cannot be released: {fault}.");

            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = """
                UPDATE execution_lease_generations
                SET released_at_utc_ticks = $releasedAt
                WHERE venue_id = $venueId AND trading_account_id = $accountId
                  AND environment = $environment AND fencing_token = $token;
                """;
            AddResource(command, grant.Claim.Resource);
            command.Parameters.AddWithValue("$token", grant.Claim.FencingToken.Value);
            command.Parameters.AddWithValue("$releasedAt", releasedAtUtc.UtcDateTime.Ticks);
            if (command.ExecuteNonQuery() != 1)
                return LeaseMutationFailed(ExecutionLeaseFault.NotCurrent, "The current lease row disappeared.");
            transaction.Commit();
            return new ExecutionLeaseMutationResult(ExecutionLeaseFault.None, current!.Value.Grant);
        }
    }

    public ExecutionLeaseValidationResult Validate(in ExecutionLeaseClaim claim, DateTimeOffset atUtc)
    {
        if (!claim.IsValid || atUtc.Offset != TimeSpan.Zero)
            return new ExecutionLeaseValidationResult(ExecutionLeaseFault.InvalidInput, false, "The lease claim or time is invalid.");
        lock (_gate)
        {
            ThrowIfDisposed();
            var current = ReadLatestLease(claim.Resource, null);
            if (current is null || current.Value.Grant.Claim != claim)
                return new ExecutionLeaseValidationResult(ExecutionLeaseFault.NotCurrent, false, "The claim is not the latest generation.");
            if (current.Value.ReleasedAtUtc is not null)
                return new ExecutionLeaseValidationResult(ExecutionLeaseFault.Released, false, "The lease was released.");
            if (current.Value.Grant.ExpiresAtUtc <= atUtc)
                return new ExecutionLeaseValidationResult(ExecutionLeaseFault.Expired, false, "The lease expired.");
            return new ExecutionLeaseValidationResult(ExecutionLeaseFault.None, true);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _connection.Dispose();
            _writerLease?.Dispose();
        }
    }

    private SqliteExecutionLedgerIntegrity VerifyIntegrityCore()
    {
        try
        {
            using (var quick = _connection.CreateCommand())
            {
                quick.CommandText = "PRAGMA quick_check;";
                if (!string.Equals(Convert.ToString(quick.ExecuteScalar()), "ok", StringComparison.OrdinalIgnoreCase))
                    return Failed(SqliteExecutionLedgerFault.SqliteIntegrityCheckFailed, null, -1, "SQLite quick_check failed.");
            }
            using (var foreignKeys = _connection.CreateCommand())
            {
                foreignKeys.CommandText = "PRAGMA foreign_key_check;";
                using var reader = foreignKeys.ExecuteReader();
                if (reader.Read())
                    return Failed(SqliteExecutionLedgerFault.SqliteIntegrityCheckFailed, null, -1, "SQLite foreign_key_check failed.");
            }

            foreach (var aggregateId in ReadAggregateIds())
            {
                List<OmsOrderEvent> stream;
                try
                {
                    stream = ReadStream(aggregateId, null);
                }
                catch (Exception exception) when (exception is InvalidDataException or System.Text.Json.JsonException)
                {
                    return Failed(SqliteExecutionLedgerFault.EventPayloadInvalid, aggregateId, -1, exception.Message);
                }

                var replay = OmsOrderProjector.Rebuild(stream);
                if (!replay.IsSuccess)
                {
                    return Failed(
                        SqliteExecutionLedgerFault.EventChainInvalid,
                        aggregateId,
                        replay.EventIndex,
                        $"Replay failed: {replay.Fault}/{replay.ChainFault}.");
                }

                using var projection = _connection.CreateCommand();
                projection.CommandText = """
                    SELECT last_sequence, last_event_hash, projection_payload_json
                    FROM execution_order_projections WHERE client_order_id = $aggregateId;
                    """;
                projection.Parameters.AddWithValue("$aggregateId", aggregateId.Value);
                using var reader = projection.ExecuteReader();
                if (!reader.Read() ||
                    reader.GetInt64(0) != replay.Projection!.LastSequence ||
                    !string.Equals(reader.GetString(1), replay.Projection.LastEventHash, StringComparison.Ordinal) ||
                    !string.Equals(
                        ExecutionCanonicalJson.Canonicalize(reader.GetString(2)),
                        ExecutionCanonicalJson.Serialize(replay.Projection),
                        StringComparison.Ordinal))
                {
                    return Failed(SqliteExecutionLedgerFault.ProjectionMismatch, aggregateId, -1, "Materialized projection differs from event replay.");
                }
            }

            using var delivery = _connection.CreateCommand();
            delivery.CommandText = """
                SELECT COUNT(*)
                FROM execution_order_events AS e
                LEFT JOIN execution_inbox AS i
                  ON i.aggregate_id = e.aggregate_id AND i.aggregate_sequence = e.aggregate_sequence
                LEFT JOIN execution_outbox AS o
                  ON o.aggregate_id = e.aggregate_id AND o.aggregate_sequence = e.aggregate_sequence
                WHERE i.aggregate_id IS NULL OR o.aggregate_id IS NULL
                   OR i.event_hash <> e.event_hash OR o.event_hash <> e.event_hash;
                """;
            if (Convert.ToInt64(delivery.ExecuteScalar()) != 0)
                return Failed(SqliteExecutionLedgerFault.DeliveryMetadataInvalid, null, -1, "Inbox/outbox linkage is incomplete or inconsistent.");

            using var evidence = _connection.CreateCommand();
            evidence.CommandText = $"""
                SELECT COUNT(*)
                FROM execution_order_events AS e
                LEFT JOIN execution_risk_decisions AS r
                  ON r.aggregate_id = e.aggregate_id AND r.aggregate_sequence = e.aggregate_sequence
                LEFT JOIN execution_fills AS f
                  ON f.aggregate_id = e.aggregate_id AND f.aggregate_sequence = e.aggregate_sequence
                WHERE ((e.event_kind IN ({(int)OrderEventKind.RiskAccepted}, {(int)OrderEventKind.RiskRejected},
                                         {(int)OrderEventKind.ReplaceRiskAccepted}, {(int)OrderEventKind.ReplaceRiskRejected}))
                       <> (r.aggregate_id IS NOT NULL))
                   OR ((e.event_kind = {(int)OrderEventKind.FillReceived}) <> (f.aggregate_id IS NOT NULL));
                """;
            if (Convert.ToInt64(evidence.ExecuteScalar()) != 0)
                return Failed(SqliteExecutionLedgerFault.DeliveryMetadataInvalid, null, -1, "Risk/fill evidence linkage is incomplete or inconsistent.");

            var economicFault = VerifyEconomicProjections();
            if (economicFault is { } fault) return fault;

            try
            {
                VerifyReconciliationCases();
            }
            catch (Exception exception) when (exception is InvalidDataException or ArgumentException or System.Text.Json.JsonException)
            {
                return Failed(SqliteExecutionLedgerFault.ReconciliationEvidenceInvalid, null, -1, exception.Message);
            }

            return new SqliteExecutionLedgerIntegrity(SqliteExecutionLedgerFault.None, null, -1, "ok");
        }
        catch (SqliteException exception)
        {
            return Failed(SqliteExecutionLedgerFault.SqliteIntegrityCheckFailed, null, -1, exception.Message);
        }
    }

    private SqliteExecutionLedgerIntegrity? VerifyEconomicProjections()
    {
        var resources = ReadAggregateIds()
            .Select(ReadProjection)
            .Where(static projection => projection is not null)
            .Select(projection => new ExecutionResource(
                projection!.SubmitCommand.Metadata.VenueId,
                projection.SubmitCommand.Metadata.TradingAccountId,
                projection.SubmitCommand.Metadata.Environment))
            .Distinct()
            .ToArray();
        if (resources.Length == 0) return null;
        if (resources.Length != 1)
            return Failed(
                SqliteExecutionLedgerFault.EconomicProjectionMismatch,
                null,
                -1,
                "One execution ledger contains multiple venue/account/environment resources.");

        var lastRecordedAt = ReadOutbox()
            .Select(entry => entry.Event.RecordedAtUtc)
            .DefaultIfEmpty(DateTimeOffset.UnixEpoch)
            .Max();
        var replay = ExecutionReconciliationSnapshotBuilder.FromLedger(
            resources[0],
            lastRecordedAt,
            this);
        var positions = ReadPositionProjections(resources[0]);
        var cash = ReadCashProjections(resources[0]);
        if (replay.Fills.Count == 0)
        {
            return positions.Count == 0 && cash.Count == 0
                ? null
                : Failed(
                    SqliteExecutionLedgerFault.EconomicProjectionMismatch,
                    null,
                    -1,
                    "Economic projection rows exist without immutable fills.");
        }

        var expectedPositions = replay.Positions.ToDictionary(item => item.InstrumentId);
        var actualPositions = positions.ToDictionary(item => item.InstrumentId);
        if (expectedPositions.Count != actualPositions.Count || expectedPositions.Any(pair =>
                !actualPositions.TryGetValue(pair.Key, out var actual) ||
                !ScaledValueMath.TryCompare(
                    pair.Value.Quantity.Coefficient,
                    pair.Value.Quantity.Scale,
                    actual.Quantity.Coefficient,
                    actual.Quantity.Scale,
                    out var comparison) || comparison != 0))
        {
            return Failed(
                SqliteExecutionLedgerFault.EconomicProjectionMismatch,
                null,
                -1,
                "Materialized positions differ from immutable fill replay.");
        }

        var expectedCash = replay.Cash.Single(item =>
            string.Equals(item.Currency, "SIM", StringComparison.Ordinal));
        var actualCash = cash.SingleOrDefault(item =>
            string.Equals(item.Currency, "SIM", StringComparison.Ordinal));
        if (actualCash is null ||
            !ScaledValueMath.TryCompare(
                expectedCash.Total.Coefficient,
                expectedCash.Total.Scale,
                actualCash.Total.Coefficient,
                actualCash.Total.Scale,
                out var totalComparison) || totalComparison != 0 ||
            !ScaledValueMath.TryCompare(
                expectedCash.Available.Coefficient,
                expectedCash.Available.Scale,
                actualCash.Available.Coefficient,
                actualCash.Available.Scale,
                out var availableComparison) || availableComparison != 0)
        {
            return Failed(
                SqliteExecutionLedgerFault.EconomicProjectionMismatch,
                null,
                -1,
                "Materialized cash differs from immutable fill replay.");
        }
        return null;
    }

    private List<ClientOrderId> ReadAggregateIds()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT DISTINCT aggregate_id FROM execution_order_events ORDER BY aggregate_id;";
        using var reader = command.ExecuteReader();
        var result = new List<ClientOrderId>();
        while (reader.Read()) result.Add(new ClientOrderId(reader.GetString(0)));
        return result;
    }

    private void VerifyReconciliationCases()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT case_id, fact_sequence, venue_id, trading_account_id, environment,
                   subject_kind, subject_key, client_order_id, case_kind, case_status,
                   opened_at_utc_ticks, resolved_at_utc_ticks, case_payload_json
            FROM execution_reconciliation_cases
            ORDER BY case_id, fact_sequence;
            """;
        var facts = ReadReconciliationRows(command);
        foreach (var sequence in facts.GroupBy(item => item.CaseId))
        {
            ReconciliationCase? prior = null;
            var index = 0;
            foreach (var fact in sequence)
            {
                index++;
                if (index == 1 && fact.Status != ReconciliationCaseStatus.Open ||
                    prior is not null &&
                    (!InMemoryReconciliationCaseStore.SameObservation(prior, fact) ||
                     prior.Status == ReconciliationCaseStatus.Resolved || fact.Status <= prior.Status))
                    throw new InvalidDataException($"Reconciliation case '{fact.CaseId}' has an invalid append-only fact sequence.");
                prior = fact;
            }
        }
    }

    private List<ReconciliationCase> ReadReconciliationCase(
        ReconciliationCaseId caseId,
        SqliteTransaction? transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT case_id, fact_sequence, venue_id, trading_account_id, environment,
                   subject_kind, subject_key, client_order_id, case_kind, case_status,
                   opened_at_utc_ticks, resolved_at_utc_ticks, case_payload_json
            FROM execution_reconciliation_cases
            WHERE case_id = $caseId ORDER BY fact_sequence;
            """;
        command.Parameters.AddWithValue("$caseId", caseId.Value);
        return ReadReconciliationRows(command);
    }

    private static List<ReconciliationCase> ReadReconciliationRows(SqliteCommand command)
    {
        using var reader = command.ExecuteReader();
        var result = new List<ReconciliationCase>();
        string? priorCaseId = null;
        long expectedSequence = 0;
        while (reader.Read())
        {
            var caseId = reader.GetString(0);
            expectedSequence = string.Equals(priorCaseId, caseId, StringComparison.Ordinal)
                ? checked(expectedSequence + 1)
                : 1;
            if (reader.GetInt64(1) != expectedSequence)
                throw new InvalidDataException($"Reconciliation case '{caseId}' has a fact-sequence gap.");
            priorCaseId = caseId;
            var fact = ExecutionCanonicalJson.Deserialize<ReconciliationCase>(reader.GetString(12));
            if (!fact.IsValid || !string.Equals(fact.CaseId.Value, caseId, StringComparison.Ordinal) ||
                !string.Equals(fact.Resource.VenueId.Value, reader.GetString(2), StringComparison.Ordinal) ||
                !string.Equals(fact.Resource.TradingAccountId.Value, reader.GetString(3), StringComparison.Ordinal) ||
                (int)fact.Resource.Environment != reader.GetInt32(4) ||
                (int)fact.SubjectKind != reader.GetInt32(5) ||
                !string.Equals(fact.SubjectKey, reader.GetString(6), StringComparison.Ordinal) ||
                !OptionalIdEquals(fact.ClientOrderId?.Value, reader, 7) ||
                (int)fact.Kind != reader.GetInt32(8) || (int)fact.Status != reader.GetInt32(9) ||
                fact.OpenedAtUtc.UtcDateTime.Ticks != reader.GetInt64(10) ||
                (fact.ResolvedAtUtc?.UtcDateTime.Ticks) != (reader.IsDBNull(11) ? null : reader.GetInt64(11)))
                throw new InvalidDataException($"Reconciliation case '{caseId}' payload disagrees with its SQLite envelope.");
            result.Add(fact);
        }
        return result;
    }

    private List<DurableOrderRecoveryEntry> LoadStartupRecovery()
    {
        using var command = _connection.CreateCommand();
        command.CommandText = """
            SELECT client_order_id, projection_payload_json
            FROM execution_order_projections ORDER BY client_order_id;
            """;
        using var reader = command.ExecuteReader();
        var result = new List<DurableOrderRecoveryEntry>();
        while (reader.Read())
        {
            var projection = ExecutionCanonicalJson.Deserialize<OmsOrderProjection>(reader.GetString(1));
            if (!OrderLifecycle.IsTerminal(projection.State))
                result.Add(new DurableOrderRecoveryEntry(projection.ClientOrderId, projection.State, projection.LastSequence));
        }
        return result;
    }

    private List<OmsOrderEvent> ReadStream(ClientOrderId aggregateId, SqliteTransaction? transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT aggregate_sequence, event_kind, state_before, state_after, source,
                   deduplication_key, occurred_at_utc_ticks, recorded_at_utc_ticks, causation_id,
                   previous_event_hash, event_hash, broker_order_id, exchange_order_id,
                   event_payload_json
            FROM execution_order_events
            WHERE aggregate_id = $aggregateId
            ORDER BY aggregate_sequence;
            """;
        command.Parameters.AddWithValue("$aggregateId", aggregateId.Value);
        using var reader = command.ExecuteReader();
        var stream = new List<OmsOrderEvent>();
        while (reader.Read())
        {
            var orderEvent = ExecutionCanonicalJson.Deserialize<OmsOrderEvent>(reader.GetString(13));
            if (orderEvent.AggregateId != aggregateId ||
                orderEvent.AggregateSequence != reader.GetInt64(0) ||
                (int)orderEvent.Kind != reader.GetInt32(1) ||
                (orderEvent.StateBefore.HasValue ? (int)orderEvent.StateBefore.Value : (int?)null) !=
                    (reader.IsDBNull(2) ? null : reader.GetInt32(2)) ||
                (int)orderEvent.StateAfter != reader.GetInt32(3) ||
                (int)orderEvent.Source != reader.GetInt32(4) ||
                !string.Equals(orderEvent.DeduplicationKey.Value, reader.GetString(5), StringComparison.Ordinal) ||
                orderEvent.OccurredAtUtc.UtcDateTime.Ticks != reader.GetInt64(6) ||
                orderEvent.RecordedAtUtc.UtcDateTime.Ticks != reader.GetInt64(7) ||
                !string.Equals(orderEvent.CausationId.Value, reader.GetString(8), StringComparison.Ordinal) ||
                !string.Equals(orderEvent.PreviousEventHash, reader.GetString(9), StringComparison.Ordinal) ||
                !string.Equals(orderEvent.EventHash, reader.GetString(10), StringComparison.Ordinal) ||
                !OptionalIdEquals(orderEvent.BrokerOrderId?.Value, reader, 11) ||
                !OptionalIdEquals(orderEvent.ExchangeOrderId?.Value, reader, 12))
            {
                throw new InvalidDataException("An event payload disagrees with its normalized SQLite envelope.");
            }
            stream.Add(orderEvent);
        }
        return stream;
    }

    private OmsOrderEvent ReadEvent(ClientOrderId aggregateId, long sequence, SqliteTransaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT event_payload_json FROM execution_order_events
            WHERE aggregate_id = $aggregateId AND aggregate_sequence = $sequence;
            """;
        command.Parameters.AddWithValue("$aggregateId", aggregateId.Value);
        command.Parameters.AddWithValue("$sequence", sequence);
        return command.ExecuteScalar() is string json
            ? ExecutionCanonicalJson.Deserialize<OmsOrderEvent>(json)
            : throw new InvalidDataException("Inbox references a missing event.");
    }

    private InboxRow? ReadInbox(
        OrderEventSource source,
        DeduplicationKey key,
        SqliteTransaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT draft_hash, aggregate_id, aggregate_sequence, event_hash
            FROM execution_inbox WHERE source = $source AND deduplication_key = $key;
            """;
        command.Parameters.AddWithValue("$source", (int)source);
        command.Parameters.AddWithValue("$key", key.Value);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new InboxRow(reader.GetString(0), new ClientOrderId(reader.GetString(1)), reader.GetInt64(2), reader.GetString(3))
            : null;
    }

    private void InsertEvent(OmsOrderEvent orderEvent, SqliteTransaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO execution_order_events(
                aggregate_id, aggregate_sequence, event_kind, state_before, state_after, source,
                deduplication_key, occurred_at_utc_ticks, recorded_at_utc_ticks, causation_id,
                previous_event_hash, event_hash, broker_order_id, exchange_order_id,
                event_payload_json)
            VALUES ($aggregateId, $sequence, $kind, $stateBefore, $stateAfter, $source,
                    $dedupe, $occurred, $recorded, $causation, $previousHash, $eventHash,
                    $brokerOrderId, $exchangeOrderId, $payload);
            """;
        command.Parameters.AddWithValue("$aggregateId", orderEvent.AggregateId.Value);
        command.Parameters.AddWithValue("$sequence", orderEvent.AggregateSequence);
        command.Parameters.AddWithValue("$kind", (int)orderEvent.Kind);
        command.Parameters.AddWithValue("$stateBefore", Db(orderEvent.StateBefore is { } state ? (int)state : null));
        command.Parameters.AddWithValue("$stateAfter", (int)orderEvent.StateAfter);
        command.Parameters.AddWithValue("$source", (int)orderEvent.Source);
        command.Parameters.AddWithValue("$dedupe", orderEvent.DeduplicationKey.Value);
        command.Parameters.AddWithValue("$occurred", orderEvent.OccurredAtUtc.UtcDateTime.Ticks);
        command.Parameters.AddWithValue("$recorded", orderEvent.RecordedAtUtc.UtcDateTime.Ticks);
        command.Parameters.AddWithValue("$causation", orderEvent.CausationId.Value);
        command.Parameters.AddWithValue("$previousHash", orderEvent.PreviousEventHash);
        command.Parameters.AddWithValue("$eventHash", orderEvent.EventHash);
        command.Parameters.AddWithValue("$brokerOrderId", Db(orderEvent.BrokerOrderId?.Value));
        command.Parameters.AddWithValue("$exchangeOrderId", Db(orderEvent.ExchangeOrderId?.Value));
        command.Parameters.AddWithValue("$payload", ExecutionCanonicalJson.Serialize(orderEvent));
        command.ExecuteNonQuery();
    }

    private void InsertInbox(
        OrderEventDraft draft,
        string draftHash,
        OmsOrderEvent orderEvent,
        SqliteTransaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO execution_inbox(
                source, deduplication_key, draft_hash, aggregate_id, aggregate_sequence, event_hash)
            VALUES ($source, $dedupe, $draftHash, $aggregateId, $sequence, $eventHash);
            """;
        command.Parameters.AddWithValue("$source", (int)draft.Source);
        command.Parameters.AddWithValue("$dedupe", draft.DeduplicationKey.Value);
        command.Parameters.AddWithValue("$draftHash", draftHash);
        command.Parameters.AddWithValue("$aggregateId", orderEvent.AggregateId.Value);
        command.Parameters.AddWithValue("$sequence", orderEvent.AggregateSequence);
        command.Parameters.AddWithValue("$eventHash", orderEvent.EventHash);
        command.ExecuteNonQuery();
    }

    private void UpsertProjection(OmsOrderProjection projection, SqliteTransaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO execution_order_projections(
                client_order_id, last_sequence, last_event_hash, projection_payload_json)
            VALUES ($aggregateId, $sequence, $eventHash, $payload)
            ON CONFLICT(client_order_id) DO UPDATE SET
                last_sequence = excluded.last_sequence,
                last_event_hash = excluded.last_event_hash,
                projection_payload_json = excluded.projection_payload_json;
            """;
        command.Parameters.AddWithValue("$aggregateId", projection.ClientOrderId.Value);
        command.Parameters.AddWithValue("$sequence", projection.LastSequence);
        command.Parameters.AddWithValue("$eventHash", projection.LastEventHash);
        command.Parameters.AddWithValue("$payload", ExecutionCanonicalJson.Serialize(projection));
        command.ExecuteNonQuery();
    }

    private void InsertRiskAndFill(
        OmsOrderEvent orderEvent,
        OmsOrderProjection projection,
        SqliteTransaction transaction)
    {
        if (orderEvent.RiskObservation is not null)
        {
            using var risk = _connection.CreateCommand();
            risk.Transaction = transaction;
            risk.CommandText = """
                INSERT INTO execution_risk_decisions(aggregate_id, aggregate_sequence, decision_payload_json)
                VALUES ($aggregateId, $sequence, $payload);
                """;
            risk.Parameters.AddWithValue("$aggregateId", orderEvent.AggregateId.Value);
            risk.Parameters.AddWithValue("$sequence", orderEvent.AggregateSequence);
            risk.Parameters.AddWithValue("$payload", ExecutionCanonicalJson.Serialize(orderEvent.RiskObservation));
            risk.ExecuteNonQuery();
        }

        if (orderEvent.Fill is not null)
        {
            using var fill = _connection.CreateCommand();
            fill.Transaction = transaction;
            fill.CommandText = """
                INSERT INTO execution_fills(
                    aggregate_id, aggregate_sequence, trade_id,
                    quantity_coefficient, quantity_scale, price_coefficient, price_scale,
                    fee_coefficient, fee_scale, fill_payload_json)
                VALUES ($aggregateId, $sequence, $tradeId, $quantityCoefficient, $quantityScale,
                        $priceCoefficient, $priceScale, $feeCoefficient, $feeScale, $payload);
                """;
            fill.Parameters.AddWithValue("$aggregateId", orderEvent.AggregateId.Value);
            fill.Parameters.AddWithValue("$sequence", orderEvent.AggregateSequence);
            fill.Parameters.AddWithValue("$tradeId", orderEvent.Fill.TradeId.Value);
            fill.Parameters.AddWithValue("$quantityCoefficient", orderEvent.Fill.Quantity.Coefficient);
            fill.Parameters.AddWithValue("$quantityScale", orderEvent.Fill.Quantity.Scale);
            fill.Parameters.AddWithValue("$priceCoefficient", orderEvent.Fill.Price.Coefficient);
            fill.Parameters.AddWithValue("$priceScale", orderEvent.Fill.Price.Scale);
            fill.Parameters.AddWithValue("$feeCoefficient", orderEvent.Fill.Fee.Coefficient);
            fill.Parameters.AddWithValue("$feeScale", orderEvent.Fill.Fee.Scale);
            fill.Parameters.AddWithValue("$payload", ExecutionCanonicalJson.Serialize(orderEvent.Fill));
            fill.ExecuteNonQuery();
            UpdateEconomicProjections(orderEvent, projection, transaction);
        }
    }

    private void UpdateEconomicProjections(
        OmsOrderEvent orderEvent,
        OmsOrderProjection projection,
        SqliteTransaction transaction)
    {
        var fill = orderEvent.Fill ?? throw new InvalidOperationException("Economic projection requires a fill.");
        var metadata = projection.SubmitCommand.Metadata;
        var resource = new ExecutionResource(metadata.VenueId, metadata.TradingAccountId, metadata.Environment);
        var instrumentId = projection.Instruction.TradeIntent.Instrument;
        var position = ReadPositionProjection(resource, instrumentId, transaction);
        var cash = ReadCashProjection(resource, transaction);
        var positions = new Dictionary<TradingTerminal.Core.Domain.InstrumentId, ScaledQuantity>
        {
            [instrumentId] = position?.Quantity ?? ScaledQuantity.Zero,
        };
        var cashTotal = cash?.Total ?? ScaledMoney.Zero;
        var snapshot = new ReconciliationFillSnapshot(
            fill.TradeId,
            projection.ClientOrderId,
            orderEvent.BrokerOrderId ?? projection.BrokerOrderId,
            orderEvent.ExchangeOrderId ?? projection.ExchangeOrderId,
            instrumentId,
            projection.CanonicalTerms.Side,
            fill.Quantity,
            fill.Price,
            fill.Fee,
            fill.OccurredAtUtc);
        ExecutionReconciliationSnapshotBuilder.ApplyFill(snapshot, positions, ref cashTotal);
        var observedAt = position is null || fill.OccurredAtUtc > position.ObservedAtUtc
            ? fill.OccurredAtUtc
            : position.ObservedAtUtc;
        var cashObservedAt = cash is null || fill.OccurredAtUtc > cash.ObservedAtUtc
            ? fill.OccurredAtUtc
            : cash.ObservedAtUtc;

        using (var command = _connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO execution_position_projections(
                    venue_id, trading_account_id, environment, instrument_id,
                    quantity_coefficient, quantity_scale, observed_at_utc_ticks,
                    last_aggregate_id, last_aggregate_sequence, last_event_hash)
                VALUES ($venueId, $accountId, $environment, $instrumentId,
                        $coefficient, $scale, $observed, $aggregateId, $sequence, $eventHash)
                ON CONFLICT(venue_id, trading_account_id, environment, instrument_id) DO UPDATE SET
                    quantity_coefficient = excluded.quantity_coefficient,
                    quantity_scale = excluded.quantity_scale,
                    observed_at_utc_ticks = excluded.observed_at_utc_ticks,
                    last_aggregate_id = excluded.last_aggregate_id,
                    last_aggregate_sequence = excluded.last_aggregate_sequence,
                    last_event_hash = excluded.last_event_hash;
                """;
            AddResource(command, resource);
            command.Parameters.AddWithValue("$instrumentId", instrumentId.Value);
            command.Parameters.AddWithValue("$coefficient", positions[instrumentId].Coefficient);
            command.Parameters.AddWithValue("$scale", positions[instrumentId].Scale);
            command.Parameters.AddWithValue("$observed", observedAt.UtcDateTime.Ticks);
            AddEventEvidence(command, orderEvent);
            command.ExecuteNonQuery();
        }

        using (var command = _connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                INSERT INTO execution_cash_projections(
                    venue_id, trading_account_id, environment, currency,
                    total_coefficient, total_scale, available_coefficient, available_scale,
                    observed_at_utc_ticks, last_aggregate_id, last_aggregate_sequence, last_event_hash)
                VALUES ($venueId, $accountId, $environment, 'SIM',
                        $coefficient, $scale, $coefficient, $scale,
                        $observed, $aggregateId, $sequence, $eventHash)
                ON CONFLICT(venue_id, trading_account_id, environment, currency) DO UPDATE SET
                    total_coefficient = excluded.total_coefficient,
                    total_scale = excluded.total_scale,
                    available_coefficient = excluded.available_coefficient,
                    available_scale = excluded.available_scale,
                    observed_at_utc_ticks = excluded.observed_at_utc_ticks,
                    last_aggregate_id = excluded.last_aggregate_id,
                    last_aggregate_sequence = excluded.last_aggregate_sequence,
                    last_event_hash = excluded.last_event_hash;
                """;
            AddResource(command, resource);
            command.Parameters.AddWithValue("$coefficient", cashTotal.Coefficient);
            command.Parameters.AddWithValue("$scale", cashTotal.Scale);
            command.Parameters.AddWithValue("$observed", cashObservedAt.UtcDateTime.Ticks);
            AddEventEvidence(command, orderEvent);
            command.ExecuteNonQuery();
        }
    }

    private ReconciliationPositionSnapshot? ReadPositionProjection(
        ExecutionResource resource,
        TradingTerminal.Core.Domain.InstrumentId instrumentId,
        SqliteTransaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT quantity_coefficient, quantity_scale, observed_at_utc_ticks
            FROM execution_position_projections
            WHERE venue_id = $venueId AND trading_account_id = $accountId AND environment = $environment
              AND instrument_id = $instrumentId;
            """;
        AddResource(command, resource);
        command.Parameters.AddWithValue("$instrumentId", instrumentId.Value);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new ReconciliationPositionSnapshot(
                instrumentId,
                new ScaledQuantity(reader.GetInt64(0), checked((byte)reader.GetInt32(1))),
                UtcFromTicks(reader.GetInt64(2)))
            : null;
    }

    private ReconciliationCashSnapshot? ReadCashProjection(
        ExecutionResource resource,
        SqliteTransaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT total_coefficient, total_scale, available_coefficient, available_scale,
                   observed_at_utc_ticks
            FROM execution_cash_projections
            WHERE venue_id = $venueId AND trading_account_id = $accountId AND environment = $environment
              AND currency = 'SIM';
            """;
        AddResource(command, resource);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new ReconciliationCashSnapshot(
                "SIM",
                new ScaledMoney(reader.GetInt64(0), checked((byte)reader.GetInt32(1))),
                new ScaledMoney(reader.GetInt64(2), checked((byte)reader.GetInt32(3))),
                UtcFromTicks(reader.GetInt64(4)))
            : null;
    }

    private static void AddEventEvidence(SqliteCommand command, OmsOrderEvent orderEvent)
    {
        command.Parameters.AddWithValue("$aggregateId", orderEvent.AggregateId.Value);
        command.Parameters.AddWithValue("$sequence", orderEvent.AggregateSequence);
        command.Parameters.AddWithValue("$eventHash", orderEvent.EventHash);
    }

    private void InsertOutbox(OmsOrderEvent orderEvent, SqliteTransaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO execution_outbox(aggregate_id, aggregate_sequence, event_hash)
            VALUES ($aggregateId, $sequence, $eventHash);
            """;
        command.Parameters.AddWithValue("$aggregateId", orderEvent.AggregateId.Value);
        command.Parameters.AddWithValue("$sequence", orderEvent.AggregateSequence);
        command.Parameters.AddWithValue("$eventHash", orderEvent.EventHash);
        command.ExecuteNonQuery();
    }

    private bool LeaseIdExists(ExecutionLeaseId leaseId, SqliteTransaction transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM execution_lease_generations WHERE execution_lease_id = $leaseId);";
        command.Parameters.AddWithValue("$leaseId", leaseId.Value);
        return Convert.ToInt64(command.ExecuteScalar()) != 0;
    }

    private LeaseRow? ReadLatestLease(ExecutionResource resource, SqliteTransaction? transaction)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            SELECT fencing_token, execution_lease_id, owner_runtime_id,
                   acquired_at_utc_ticks, expires_at_utc_ticks, released_at_utc_ticks
            FROM execution_lease_generations
            WHERE venue_id = $venueId AND trading_account_id = $accountId AND environment = $environment
            ORDER BY fencing_token DESC LIMIT 1;
            """;
        AddResource(command, resource);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;
        var claim = new ExecutionLeaseClaim(
            resource,
            new ExecutionLeaseId(reader.GetString(1)),
            new FencingToken(reader.GetInt64(0)));
        var grant = new ExecutionLeaseGrant(
            claim,
            new RuntimeInstanceId(reader.GetString(2)),
            UtcFromTicks(reader.GetInt64(3)),
            UtcFromTicks(reader.GetInt64(4)));
        return new LeaseRow(grant, reader.IsDBNull(5) ? null : UtcFromTicks(reader.GetInt64(5)));
    }

    private static ExecutionLeaseFault CompareCurrent(
        LeaseRow? current,
        in ExecutionLeaseGrant presented,
        DateTimeOffset atUtc,
        bool permitExpired)
    {
        if (current is null || current.Value.Grant.Claim != presented.Claim ||
            current.Value.Grant.OwnerId != presented.OwnerId ||
            current.Value.Grant.ExpiresAtUtc != presented.ExpiresAtUtc)
            return ExecutionLeaseFault.NotCurrent;
        if (current.Value.ReleasedAtUtc is not null)
            return ExecutionLeaseFault.Released;
        if (!permitExpired && current.Value.Grant.ExpiresAtUtc <= atUtc)
            return ExecutionLeaseFault.Expired;
        return ExecutionLeaseFault.None;
    }

    private static void AddResource(SqliteCommand command, in ExecutionResource resource)
    {
        command.Parameters.AddWithValue("$venueId", resource.VenueId.Value);
        command.Parameters.AddWithValue("$accountId", resource.TradingAccountId.Value);
        command.Parameters.AddWithValue("$environment", (int)resource.Environment);
    }

    private static DateTimeOffset UtcFromTicks(long ticks) => new(ticks, TimeSpan.Zero);

    private static OrderEventAppendFault ValidateDraft(OrderEventDraft? draft, DateTimeOffset recordedAtUtc)
    {
        if (draft is null) return OrderEventAppendFault.MissingDraft;
        if (draft.AggregateId.IsEmpty) return OrderEventAppendFault.InvalidAggregateId;
        if (!Enum.IsDefined(draft.Kind) || !Enum.IsDefined(draft.StateAfter) || !Enum.IsDefined(draft.Source))
            return OrderEventAppendFault.InvalidClassification;
        if (draft.DeduplicationKey.IsEmpty) return OrderEventAppendFault.InvalidDeduplicationKey;
        if (draft.CausationId.IsEmpty) return OrderEventAppendFault.InvalidCausationId;
        if (draft.OccurredAtUtc.Offset != TimeSpan.Zero ||
            recordedAtUtc.Offset != TimeSpan.Zero ||
            recordedAtUtc < draft.OccurredAtUtc)
            return OrderEventAppendFault.InvalidTimestamp;
        return OrderEventAppendFault.None;
    }

    private static bool OptionalIdEquals(string? value, SqliteDataReader reader, int ordinal) =>
        value is null ? reader.IsDBNull(ordinal) : !reader.IsDBNull(ordinal) &&
            string.Equals(value, reader.GetString(ordinal), StringComparison.Ordinal);

    private static object Db(object? value) => value ?? DBNull.Value;
    private static OrderEventAppendResult Rejected(OrderEventAppendFault fault) =>
        new(OrderEventAppendStatus.Rejected, fault, null);
    private static ExecutionLeaseAcquireResult LeaseAcquireFailed(ExecutionLeaseFault fault, string reason) =>
        new(fault, null, reason);
    private static ExecutionLeaseMutationResult LeaseMutationFailed(ExecutionLeaseFault fault, string reason) =>
        new(fault, null, reason);
    private static SqliteExecutionLedgerIntegrity Failed(
        SqliteExecutionLedgerFault fault,
        ClientOrderId? aggregateId,
        int eventIndex,
        string detail) => new(fault, aggregateId, eventIndex, detail);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private readonly record struct InboxRow(
        string DraftHash,
        ClientOrderId AggregateId,
        long AggregateSequence,
        string EventHash);
    private readonly record struct LeaseRow(ExecutionLeaseGrant Grant, DateTimeOffset? ReleasedAtUtc);
}
