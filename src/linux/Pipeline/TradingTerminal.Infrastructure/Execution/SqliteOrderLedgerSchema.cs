using Microsoft.Data.Sqlite;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;

namespace TradingTerminal.Infrastructure.Execution;

internal static class SqliteOrderLedgerSchema
{
    internal const int ApplicationId = 0x44415845; // DAXE
    internal const int CurrentVersion = 2;

    internal static string ApplyConnectionPragmas(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "PRAGMA busy_timeout=5000; PRAGMA foreign_keys=ON; PRAGMA synchronous=FULL;";
            command.ExecuteNonQuery();
        }

        using var journal = connection.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode=WAL;";
        return Convert.ToString(journal.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    internal static int EnsureCreated(SqliteConnection connection, DateTimeOffset appliedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (appliedAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Migration time must use UTC.", nameof(appliedAtUtc));

        var applicationId = ScalarInt32(connection, "PRAGMA application_id;");
        if (applicationId != 0 && applicationId != ApplicationId)
            throw new InvalidDataException("The configured database belongs to another application.");

        var version = ScalarInt32(connection, "PRAGMA user_version;");
        if (version > CurrentVersion)
            throw new NotSupportedException($"Execution-ledger schema {version} is newer than supported schema {CurrentVersion}.");
        if (version == CurrentVersion)
        {
            if (applicationId != ApplicationId)
                throw new InvalidDataException("The configured database is not a DaxAlgo execution ledger.");
            return version;
        }

        if (version == 1)
        {
            if (applicationId != ApplicationId)
                throw new InvalidDataException("The configured database is not a DaxAlgo execution ledger.");
            using var upgrade = connection.BeginTransaction();
            Execute(connection, upgrade, Version2Sql);
            BackfillVersion2(connection, upgrade);
            InsertMigration(connection, upgrade, 2, "durable position and cash projections", appliedAtUtc);
            Execute(connection, upgrade, $"PRAGMA user_version={CurrentVersion};");
            upgrade.Commit();
            return CurrentVersion;
        }
        if (version != 0)
            throw new NotSupportedException($"Execution-ledger schema {version} cannot be upgraded.");

        EnsureUnclaimed(connection);
        using var transaction = connection.BeginTransaction();
        Execute(connection, transaction, Version1Sql);
        InsertMigration(connection, transaction, 1, "durable paper order event ledger", appliedAtUtc);
        Execute(connection, transaction, Version2Sql);
        InsertMigration(connection, transaction, 2, "durable position and cash projections", appliedAtUtc);
        using (var claim = connection.CreateCommand())
        {
            claim.Transaction = transaction;
            claim.CommandText = $"PRAGMA application_id={ApplicationId}; PRAGMA user_version={CurrentVersion};";
            claim.ExecuteNonQuery();
        }
        transaction.Commit();

        if (ScalarInt32(connection, "PRAGMA application_id;") != ApplicationId ||
            ScalarInt32(connection, "PRAGMA user_version;") != CurrentVersion)
            throw new InvalidDataException("Execution-ledger schema ownership was not committed.");
        return CurrentVersion;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static void InsertMigration(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int version,
        string name,
        DateTimeOffset appliedAtUtc)
    {
        using var migration = connection.CreateCommand();
        migration.Transaction = transaction;
        migration.CommandText = """
            INSERT INTO execution_schema_migrations(version, name, applied_at_utc_ticks)
            VALUES ($version, $name, $appliedAt);
            """;
        migration.Parameters.AddWithValue("$version", version);
        migration.Parameters.AddWithValue("$name", name);
        migration.Parameters.AddWithValue("$appliedAt", appliedAtUtc.UtcDateTime.Ticks);
        migration.ExecuteNonQuery();
    }

    private static void BackfillVersion2(SqliteConnection connection, SqliteTransaction transaction)
    {
        var rows = new List<MigrationFill>();
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                SELECT e.aggregate_id, e.aggregate_sequence, e.event_hash,
                       e.event_payload_json, p.projection_payload_json
                FROM execution_order_events AS e
                JOIN execution_order_projections AS p ON p.client_order_id = e.aggregate_id
                WHERE e.event_kind = $fillKind
                ORDER BY e.recorded_at_utc_ticks, e.aggregate_id, e.aggregate_sequence;
                """;
            command.Parameters.AddWithValue("$fillKind", (int)OrderEventKind.FillReceived);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new MigrationFill(
                    new ClientOrderId(reader.GetString(0)),
                    reader.GetInt64(1),
                    reader.GetString(2),
                    ExecutionCanonicalJson.Deserialize<OmsOrderEvent>(reader.GetString(3)),
                    ExecutionCanonicalJson.Deserialize<OmsOrderProjection>(reader.GetString(4))));
            }
        }

        var states = new Dictionary<ExecutionResource, MigrationEconomicState>();
        foreach (var row in rows)
        {
            if (row.Event.AggregateId != row.AggregateId || row.Event.AggregateSequence != row.Sequence ||
                !string.Equals(row.Event.EventHash, row.EventHash, StringComparison.Ordinal) ||
                row.Event.Fill is not { } fill || row.Projection.ClientOrderId != row.AggregateId)
                throw new InvalidDataException("A v1 fill cannot be migrated because its envelope is inconsistent.");
            var metadata = row.Projection.SubmitCommand.Metadata;
            var resource = new ExecutionResource(metadata.VenueId, metadata.TradingAccountId, metadata.Environment);
            if (!states.TryGetValue(resource, out var state))
            {
                state = new MigrationEconomicState();
                states.Add(resource, state);
            }
            var snapshot = new ReconciliationFillSnapshot(
                fill.TradeId,
                row.AggregateId,
                row.Event.BrokerOrderId ?? row.Projection.BrokerOrderId,
                row.Event.ExchangeOrderId ?? row.Projection.ExchangeOrderId,
                row.Projection.Instruction.TradeIntent.Instrument,
                row.Projection.CanonicalTerms.Side,
                fill.Quantity,
                fill.Price,
                fill.Fee,
                fill.OccurredAtUtc);
            ExecutionReconciliationSnapshotBuilder.ApplyFill(snapshot, state.Positions, ref state.Cash);
            state.PositionEvidence[snapshot.InstrumentId] = new MigrationEvidence(
                snapshot.OccurredAtUtc, row.AggregateId, row.Sequence, row.EventHash);
            state.CashEvidence = new MigrationEvidence(
                snapshot.OccurredAtUtc, row.AggregateId, row.Sequence, row.EventHash);
        }

        foreach (var pair in states)
        {
            foreach (var position in pair.Value.Positions)
                InsertPosition(connection, transaction, pair.Key, position.Key, position.Value,
                    pair.Value.PositionEvidence[position.Key]);
            if (pair.Value.CashEvidence is { } cashEvidence)
                InsertCash(connection, transaction, pair.Key, pair.Value.Cash, cashEvidence);
        }
    }

    private static void InsertPosition(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ExecutionResource resource,
        InstrumentId instrumentId,
        ScaledQuantity quantity,
        MigrationEvidence evidence)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO execution_position_projections(
                venue_id, trading_account_id, environment, instrument_id,
                quantity_coefficient, quantity_scale, observed_at_utc_ticks,
                last_aggregate_id, last_aggregate_sequence, last_event_hash)
            VALUES ($venue, $account, $environment, $instrument, $coefficient, $scale, $observed,
                    $aggregate, $sequence, $hash);
            """;
        AddResource(command, resource);
        command.Parameters.AddWithValue("$instrument", instrumentId.Value);
        command.Parameters.AddWithValue("$coefficient", quantity.Coefficient);
        command.Parameters.AddWithValue("$scale", quantity.Scale);
        AddEvidence(command, evidence);
        command.ExecuteNonQuery();
    }

    private static void InsertCash(
        SqliteConnection connection,
        SqliteTransaction transaction,
        ExecutionResource resource,
        ScaledMoney cash,
        MigrationEvidence evidence)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO execution_cash_projections(
                venue_id, trading_account_id, environment, currency,
                total_coefficient, total_scale, available_coefficient, available_scale,
                observed_at_utc_ticks, last_aggregate_id, last_aggregate_sequence, last_event_hash)
            VALUES ($venue, $account, $environment, 'SIM', $coefficient, $scale, $coefficient, $scale,
                    $observed, $aggregate, $sequence, $hash);
            """;
        AddResource(command, resource);
        command.Parameters.AddWithValue("$coefficient", cash.Coefficient);
        command.Parameters.AddWithValue("$scale", cash.Scale);
        AddEvidence(command, evidence);
        command.ExecuteNonQuery();
    }

    private static void AddResource(SqliteCommand command, ExecutionResource resource)
    {
        command.Parameters.AddWithValue("$venue", resource.VenueId.Value);
        command.Parameters.AddWithValue("$account", resource.TradingAccountId.Value);
        command.Parameters.AddWithValue("$environment", (int)resource.Environment);
    }

    private static void AddEvidence(SqliteCommand command, MigrationEvidence evidence)
    {
        command.Parameters.AddWithValue("$observed", evidence.ObservedAtUtc.UtcDateTime.Ticks);
        command.Parameters.AddWithValue("$aggregate", evidence.AggregateId.Value);
        command.Parameters.AddWithValue("$sequence", evidence.AggregateSequence);
        command.Parameters.AddWithValue("$hash", evidence.EventHash);
    }

    private static void EnsureUnclaimed(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT name FROM sqlite_master
            WHERE type = 'table' AND name NOT LIKE 'sqlite_%'
            LIMIT 1;
            """;
        if (command.ExecuteScalar() is string table)
            throw new InvalidDataException($"The execution ledger cannot claim a database containing unrelated table '{table}'.");
    }

    private static int ScalarInt32(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt32(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
    }

    private const string Version1Sql = """
        CREATE TABLE execution_schema_migrations (
            version                 INTEGER PRIMARY KEY CHECK (version > 0),
            name                    TEXT NOT NULL,
            applied_at_utc_ticks    INTEGER NOT NULL
        );

        CREATE TABLE execution_order_events (
            aggregate_id            TEXT NOT NULL,
            aggregate_sequence      INTEGER NOT NULL CHECK (aggregate_sequence > 0),
            event_kind              INTEGER NOT NULL,
            state_before            INTEGER,
            state_after             INTEGER NOT NULL,
            source                  INTEGER NOT NULL,
            deduplication_key       TEXT NOT NULL,
            occurred_at_utc_ticks   INTEGER NOT NULL,
            recorded_at_utc_ticks   INTEGER NOT NULL,
            causation_id            TEXT NOT NULL,
            previous_event_hash     TEXT NOT NULL,
            event_hash              TEXT NOT NULL UNIQUE,
            broker_order_id         TEXT,
            exchange_order_id       TEXT,
            event_payload_json      TEXT NOT NULL,
            PRIMARY KEY (aggregate_id, aggregate_sequence)
        );

        CREATE TRIGGER execution_order_events_no_update
        BEFORE UPDATE ON execution_order_events
        BEGIN
            SELECT RAISE(ABORT, 'execution_order_events is append-only');
        END;

        CREATE TRIGGER execution_order_events_no_delete
        BEFORE DELETE ON execution_order_events
        BEGIN
            SELECT RAISE(ABORT, 'execution_order_events is append-only');
        END;

        CREATE TABLE execution_order_projections (
            client_order_id         TEXT PRIMARY KEY,
            last_sequence           INTEGER NOT NULL CHECK (last_sequence > 0),
            last_event_hash         TEXT NOT NULL,
            projection_payload_json TEXT NOT NULL
        );

        CREATE TABLE execution_inbox (
            source                  INTEGER NOT NULL,
            deduplication_key       TEXT NOT NULL,
            draft_hash              TEXT NOT NULL,
            aggregate_id            TEXT NOT NULL,
            aggregate_sequence      INTEGER NOT NULL,
            event_hash              TEXT NOT NULL,
            PRIMARY KEY (source, deduplication_key),
            FOREIGN KEY (aggregate_id, aggregate_sequence)
                REFERENCES execution_order_events(aggregate_id, aggregate_sequence)
        );

        CREATE TABLE execution_outbox (
            outbox_sequence         INTEGER PRIMARY KEY AUTOINCREMENT,
            aggregate_id            TEXT NOT NULL,
            aggregate_sequence      INTEGER NOT NULL,
            event_hash              TEXT NOT NULL UNIQUE,
            FOREIGN KEY (aggregate_id, aggregate_sequence)
                REFERENCES execution_order_events(aggregate_id, aggregate_sequence)
        );

        CREATE TABLE execution_risk_decisions (
            aggregate_id            TEXT NOT NULL,
            aggregate_sequence      INTEGER NOT NULL,
            decision_payload_json   TEXT NOT NULL,
            PRIMARY KEY (aggregate_id, aggregate_sequence),
            FOREIGN KEY (aggregate_id, aggregate_sequence)
                REFERENCES execution_order_events(aggregate_id, aggregate_sequence)
        );

        CREATE TABLE execution_fills (
            aggregate_id            TEXT NOT NULL,
            aggregate_sequence      INTEGER NOT NULL,
            trade_id                TEXT NOT NULL UNIQUE,
            quantity_coefficient    INTEGER NOT NULL,
            quantity_scale          INTEGER NOT NULL,
            price_coefficient       INTEGER NOT NULL,
            price_scale             INTEGER NOT NULL,
            fee_coefficient         INTEGER NOT NULL,
            fee_scale               INTEGER NOT NULL,
            fill_payload_json       TEXT NOT NULL,
            PRIMARY KEY (aggregate_id, aggregate_sequence),
            FOREIGN KEY (aggregate_id, aggregate_sequence)
                REFERENCES execution_order_events(aggregate_id, aggregate_sequence)
        );

        CREATE TABLE execution_lease_generations (
            venue_id                TEXT NOT NULL,
            trading_account_id      TEXT NOT NULL,
            environment             INTEGER NOT NULL,
            fencing_token           INTEGER NOT NULL CHECK (fencing_token > 0),
            execution_lease_id      TEXT NOT NULL UNIQUE,
            owner_runtime_id        TEXT NOT NULL,
            acquired_at_utc_ticks   INTEGER NOT NULL,
            expires_at_utc_ticks    INTEGER NOT NULL,
            renewed_at_utc_ticks    INTEGER,
            released_at_utc_ticks   INTEGER,
            PRIMARY KEY (venue_id, trading_account_id, environment, fencing_token),
            CHECK (expires_at_utc_ticks > acquired_at_utc_ticks)
        );

        CREATE INDEX ix_execution_lease_latest
            ON execution_lease_generations(
                venue_id, trading_account_id, environment, fencing_token DESC);

        CREATE TABLE execution_reconciliation_cases (
            case_id                 TEXT NOT NULL,
            fact_sequence           INTEGER NOT NULL CHECK (fact_sequence > 0),
            venue_id                TEXT NOT NULL,
            trading_account_id      TEXT NOT NULL,
            environment             INTEGER NOT NULL,
            subject_kind            INTEGER NOT NULL,
            subject_key             TEXT NOT NULL,
            client_order_id         TEXT,
            case_kind               INTEGER NOT NULL,
            case_status             INTEGER NOT NULL,
            opened_at_utc_ticks     INTEGER NOT NULL,
            resolved_at_utc_ticks   INTEGER,
            case_payload_json       TEXT NOT NULL,
            PRIMARY KEY (case_id, fact_sequence)
        );

        CREATE TRIGGER execution_reconciliation_cases_no_update
        BEFORE UPDATE ON execution_reconciliation_cases
        BEGIN
            SELECT RAISE(ABORT, 'execution_reconciliation_cases is append-only');
        END;

        CREATE TRIGGER execution_reconciliation_cases_no_delete
        BEFORE DELETE ON execution_reconciliation_cases
        BEGIN
            SELECT RAISE(ABORT, 'execution_reconciliation_cases is append-only');
        END;

        CREATE INDEX ix_execution_reconciliation_resource
            ON execution_reconciliation_cases(
                venue_id, trading_account_id, environment, case_id, fact_sequence);

        CREATE INDEX ix_execution_events_recorded
            ON execution_order_events(recorded_at_utc_ticks, aggregate_id, aggregate_sequence);
        CREATE INDEX ix_execution_outbox_aggregate
            ON execution_outbox(aggregate_id, aggregate_sequence);
        """;

    private const string Version2Sql = """
        CREATE TABLE execution_position_projections (
            venue_id                   TEXT NOT NULL,
            trading_account_id         TEXT NOT NULL,
            environment                INTEGER NOT NULL,
            instrument_id              INTEGER NOT NULL,
            quantity_coefficient       INTEGER NOT NULL,
            quantity_scale             INTEGER NOT NULL,
            observed_at_utc_ticks      INTEGER NOT NULL,
            last_aggregate_id          TEXT NOT NULL,
            last_aggregate_sequence    INTEGER NOT NULL,
            last_event_hash            TEXT NOT NULL,
            PRIMARY KEY (venue_id, trading_account_id, environment, instrument_id),
            FOREIGN KEY (last_aggregate_id, last_aggregate_sequence)
                REFERENCES execution_order_events(aggregate_id, aggregate_sequence)
        );

        CREATE TABLE execution_cash_projections (
            venue_id                   TEXT NOT NULL,
            trading_account_id         TEXT NOT NULL,
            environment                INTEGER NOT NULL,
            currency                   TEXT NOT NULL,
            total_coefficient          INTEGER NOT NULL,
            total_scale                INTEGER NOT NULL,
            available_coefficient      INTEGER NOT NULL,
            available_scale            INTEGER NOT NULL,
            observed_at_utc_ticks      INTEGER NOT NULL,
            last_aggregate_id          TEXT NOT NULL,
            last_aggregate_sequence    INTEGER NOT NULL,
            last_event_hash            TEXT NOT NULL,
            PRIMARY KEY (venue_id, trading_account_id, environment, currency),
            FOREIGN KEY (last_aggregate_id, last_aggregate_sequence)
                REFERENCES execution_order_events(aggregate_id, aggregate_sequence)
        );
        """;

    private sealed class MigrationEconomicState
    {
        internal Dictionary<InstrumentId, ScaledQuantity> Positions { get; } = [];
        internal Dictionary<InstrumentId, MigrationEvidence> PositionEvidence { get; } = [];
        internal ScaledMoney Cash;
        internal MigrationEvidence? CashEvidence;
    }

    private readonly record struct MigrationEvidence(
        DateTimeOffset ObservedAtUtc,
        ClientOrderId AggregateId,
        long AggregateSequence,
        string EventHash);

    private sealed record MigrationFill(
        ClientOrderId AggregateId,
        long Sequence,
        string EventHash,
        OmsOrderEvent Event,
        OmsOrderProjection Projection);
}
