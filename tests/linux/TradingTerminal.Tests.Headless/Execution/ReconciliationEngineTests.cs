using FluentAssertions;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.Infrastructure.Execution;
using Microsoft.Data.Sqlite;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class ReconciliationEngineTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly InstrumentId Instrument = new(731);
    private static readonly ExecutionResource Resource = new(
        new VenueId("paper"),
        new TradingAccountId("reconciliation-account"),
        ExecutionEnvironment.SimulatedPaper);

    [Fact]
    public void Exact_snapshot_keeps_admission_open_and_creates_no_discrepancy()
    {
        var store = new InMemoryReconciliationCaseStore();
        var engine = new ReconciliationEngine(store);
        var snapshot = Snapshot(
            orders: [Order("matching")],
            fills: [Fill("matching")],
            positions: [Position(2)],
            cash: [Cash(9_800)]);

        var result = engine.RunCycle(
            ReconciliationTrigger.Startup,
            snapshot,
            snapshot with { },
            Now.AddSeconds(1));

        result.IsSuccess.Should().BeTrue(result.Reason);
        result.Facts.Should().BeEmpty();
        result.UnresolvedMaterialCaseCount.Should().Be(0);
        engine.CanAdmitNewExposure(Resource).Should().BeTrue();
    }

    [Fact]
    public void Order_fill_position_and_cash_differences_are_separately_classified()
    {
        var engine = new ReconciliationEngine(new InMemoryReconciliationCaseStore());
        var matchingLocal = Order("matching");
        var matchingBroker = matchingLocal with
        {
            CurrentTerms = matchingLocal.CurrentTerms with { Quantity = Q(3) },
            FilledQuantity = Q(1),
            State = OrderLifecycleState.PartiallyFilled,
            BrokerOrderId = new BrokerOrderId("broker-changed"),
        };
        var local = Snapshot(
            orders: [matchingLocal, Order("ledger-only"), Order("predispatch", wasDispatched: false)],
            fills: [Fill("matching"), Fill("ledger-fill")],
            positions: [Position(2)],
            cash: [Cash(9_800)]);
        var changedFill = Fill("matching") with { Fee = M(2) };
        var broker = Snapshot(
            orders: [matchingBroker, Order("broker-only")],
            fills: [changedFill, Fill("broker-fill")],
            positions: [Position(3)],
            cash: [Cash(9_700)]);

        var result = engine.RunCycle(
            ReconciliationTrigger.Reconnect,
            local,
            broker,
            Now.AddSeconds(1));

        result.IsSuccess.Should().BeTrue(result.Reason);
        result.IsAdmissionBlocked.Should().BeTrue();
        result.Facts.Select(item => item.Kind).Should().Contain(new[]
        {
            ReconciliationCaseKind.IdentityMismatch,
            ReconciliationCaseKind.QuantityMismatch,
            ReconciliationCaseKind.LifecycleMismatch,
            ReconciliationCaseKind.BrokerMissing,
            ReconciliationCaseKind.LocallyMissing,
            ReconciliationCaseKind.FillMismatch,
            ReconciliationCaseKind.PositionMismatch,
            ReconciliationCaseKind.CashMismatch,
        });
        result.Facts.Should().NotContain(item => item.SubjectKey == "client-predispatch");
        result.Facts.Should().Contain(item =>
            item.SubjectKind == ReconciliationSubjectKind.Fill &&
            item.Kind == ReconciliationCaseKind.BrokerMissing);
        result.Facts.Should().Contain(item =>
            item.SubjectKind == ReconciliationSubjectKind.Fill &&
            item.Kind == ReconciliationCaseKind.LocallyMissing);
        engine.CanAdmitNewExposure(Resource).Should().BeFalse();
    }

    [Fact]
    public void Later_matching_snapshot_appends_resolution_and_reopens_admission()
    {
        var store = new InMemoryReconciliationCaseStore();
        var engine = new ReconciliationEngine(store);
        var order = Order("reappears");
        var mismatch = engine.RunCycle(
            ReconciliationTrigger.Startup,
            Snapshot(orders: [order]),
            Snapshot(),
            Now.AddSeconds(1));
        var opened = mismatch.Facts.Should().ContainSingle().Subject;

        var matchedSnapshot = Snapshot(orders: [order]);
        var cleared = engine.RunCycle(
            ReconciliationTrigger.Reconnect,
            matchedSnapshot,
            matchedSnapshot with { },
            Now.AddSeconds(2));

        opened.Status.Should().Be(ReconciliationCaseStatus.Open);
        cleared.Facts.Should().ContainSingle(item =>
            item.CaseId == opened.CaseId && item.Status == ReconciliationCaseStatus.Resolved);
        var sequence = store.Read(opened.CaseId);
        sequence.Should().HaveCount(2);
        sequence[0].Should().Be(opened);
        sequence[1].OpenedAtUtc.Should().Be(opened.OpenedAtUtc);
        engine.CanAdmitNewExposure(Resource).Should().BeTrue();
    }

    [Fact]
    public void Operator_resolution_is_append_only_and_preserves_original_evidence()
    {
        var store = new InMemoryReconciliationCaseStore();
        var engine = new ReconciliationEngine(store);
        var cycle = engine.RunCycle(
            ReconciliationTrigger.OperatorRequest,
            Snapshot(orders: [Order("operator")]),
            Snapshot(),
            Now.AddSeconds(1));
        var opened = cycle.Facts.Single();

        engine.ResolveCase(
            opened.CaseId,
            " operator-7 ",
            "Accepted the local Paper ledger after inspecting the venue snapshot.",
            Now.AddSeconds(2)).Should().BeTrue();

        var sequence = store.Read(opened.CaseId);
        sequence.Should().HaveCount(2);
        sequence[0].Should().Be(opened);
        sequence[1].Status.Should().Be(ReconciliationCaseStatus.Resolved);
        sequence[1].ResolvedBy.Should().Be("operator-7");
        sequence[1].LocalEvidence.Should().Be(opened.LocalEvidence);
        sequence[1].BrokerEvidence.Should().Be(opened.BrokerEvidence);
        engine.CanAdmitNewExposure(Resource).Should().BeTrue();
    }

    [Fact]
    public void Stale_or_duplicate_snapshot_fails_closed()
    {
        var engine = new ReconciliationEngine(new InMemoryReconciliationCaseStore());
        var duplicate = Snapshot(orders: [Order("duplicate"), Order("duplicate")]);

        var invalid = engine.RunCycle(
            ReconciliationTrigger.Startup,
            Snapshot(),
            duplicate,
            Now.AddSeconds(1));
        var stale = engine.RunCycle(
            ReconciliationTrigger.Reconnect,
            Snapshot(),
            Snapshot(capturedAtUtc: Now),
            Now.AddSeconds(6));

        invalid.Fault.Should().Be(ReconciliationCycleFault.InvalidInput);
        stale.Fault.Should().Be(ReconciliationCycleFault.SnapshotStale);
        engine.CanAdmitNewExposure(Resource).Should().BeFalse();
    }

    [Fact]
    public void Oms_blocks_submit_and_replace_but_still_allows_cancel()
    {
        var gate = new MutableAdmissionGate { IsOpen = true };
        var clock = new MutableClock(Now.UtcDateTime);
        var leases = new InMemoryExecutionLeaseStore();
        var grant = leases.Acquire(
            Resource,
            new ExecutionLeaseId("reconciliation-lease"),
            new RuntimeInstanceId("reconciliation-runtime"),
            Now,
            Now.AddHours(1)).Grant!.Value;
        var store = new InMemoryOrderEventStore();
        var venue = new DeterministicPaperVenue();
        var oms = new OrderManagementService(store, venue, leases, clock, gate);
        var workingCommand = Submit("working", grant.Claim);
        var working = oms.Submit(workingCommand, RiskContext(), Context("working"));
        working.Projection!.State.Should().Be(OrderLifecycleState.Working);
        gate.IsOpen = false;

        var blockedSubmit = oms.Submit(Submit("blocked", grant.Claim), RiskContext(), Context("blocked"));
        var blockedReplace = oms.Replace(
            new ReplaceOrderCommand(
                Metadata("replace", working.Projection.LastSequence),
                workingCommand.OrderId,
                new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(1), limitPrice: P(99))),
            RiskContext(),
            Context("replace"));
        var cancel = oms.Cancel(
            new CancelOrderCommand(Metadata("cancel", working.Projection.LastSequence), workingCommand.OrderId),
            Context("cancel"));

        blockedSubmit.Fault.Should().Be(OmsCommandFault.ReconciliationRequired);
        blockedReplace.Fault.Should().Be(OmsCommandFault.ReconciliationRequired);
        cancel.IsSuccess.Should().BeTrue(cancel.Reason);
        cancel.Projection!.State.Should().Be(OrderLifecycleState.Cancelled);
    }

    [Fact]
    public void Material_case_and_resolution_survive_sqlite_restart()
    {
        WithDatabase(path =>
        {
            ReconciliationCase opened;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var engine = new ReconciliationEngine(store);
                opened = engine.RunCycle(
                    ReconciliationTrigger.Startup,
                    Snapshot(orders: [Order("durable")]),
                    Snapshot(),
                    Now.AddSeconds(1)).Facts.Single();
                engine.CanAdmitNewExposure(Resource).Should().BeFalse();
            }

            using (var reopened = new SqliteOrderEventStore(path, Now.AddSeconds(2)))
            {
                var engine = new ReconciliationEngine(reopened);
                reopened.Read(Resource).Should().ContainSingle().Which.Should().Be(opened);
                engine.CanAdmitNewExposure(Resource).Should().BeFalse();
                engine.ResolveCase(
                    opened.CaseId,
                    "operator-9",
                    "Verified the Paper venue was reset and retained the local ledger.",
                    Now.AddSeconds(3)).Should().BeTrue();
            }

            using var resolved = new SqliteOrderEventStore(path, Now.AddSeconds(4));
            var facts = resolved.Read(opened.CaseId);
            facts.Should().HaveCount(2);
            facts[0].Should().Be(opened);
            facts[1].Status.Should().Be(ReconciliationCaseStatus.Resolved);
            new ReconciliationEngine(resolved).CanAdmitNewExposure(Resource).Should().BeTrue();
            resolved.Integrity.IsValid.Should().BeTrue(resolved.Integrity.Detail);
        });
    }

    [Fact]
    public void Paper_venue_snapshot_matches_independent_ledger_rebuild_after_fill()
    {
        var clock = new MutableClock(Now.UtcDateTime);
        var leases = new InMemoryExecutionLeaseStore();
        var grant = leases.Acquire(
            Resource,
            new ExecutionLeaseId("reconciliation-lease"),
            new RuntimeInstanceId("snapshot-runtime"),
            Now,
            Now.AddHours(1)).Grant!.Value;
        var store = new InMemoryOrderEventStore();
        var venue = new DeterministicPaperVenue();
        venue.OnMarket(new PaperMarketSnapshot(Instrument, P(99), P(100), Q(2), Now));
        var oms = new OrderManagementService(store, venue, leases, clock);

        var result = oms.Submit(Submit("snapshot", grant.Claim), RiskContext(), Context("snapshot"));
        var capturedAt = Now.AddSeconds(1);
        var ledgerSnapshot = ExecutionReconciliationSnapshotBuilder.FromLedger(Resource, capturedAt, store);
        var venueSnapshot = venue.CaptureReconciliationSnapshot(Resource, capturedAt);
        clock.UtcNow = capturedAt.UtcDateTime;
        var engine = new ReconciliationEngine(new InMemoryReconciliationCaseStore());
        var cycle = new PaperExecutionReconciliationCoordinator(Resource, store, venue, engine, clock)
            .RunStartup();

        result.Projection!.State.Should().Be(OrderLifecycleState.Filled);
        ledgerSnapshot.Fills.Should().ContainSingle();
        venueSnapshot.Fills.Should().ContainSingle();
        ledgerSnapshot.Positions.Should().ContainSingle().Which.Quantity.Should().Be(Q(2));
        venueSnapshot.Positions.Should().ContainSingle().Which.Quantity.Should().Be(Q(2));
        ledgerSnapshot.Cash.Should().ContainSingle().Which.Total.Should().Be(M(-200));
        cycle.IsSuccess.Should().BeTrue(cycle.Reason);
        cycle.IsAdmissionBlocked.Should().BeFalse();
        cycle.Facts.Should().BeEmpty();
    }

    [Fact]
    public void Snapshot_acquisition_failure_closes_the_resource_gate()
    {
        var engine = new ReconciliationEngine(new InMemoryReconciliationCaseStore());
        var coordinator = new PaperExecutionReconciliationCoordinator(
            Resource,
            new InMemoryOrderEventStore(),
            new ThrowingSnapshotProvider(),
            engine,
            new MutableClock(Now.UtcDateTime));

        var result = coordinator.RunReconnect();

        result.Fault.Should().Be(ReconciliationCycleFault.InvalidInput);
        result.Reason.Should().Contain("Snapshot acquisition failed");
        engine.CanAdmitNewExposure(Resource).Should().BeFalse();
    }

    [Fact]
    public void Invalid_sqlite_case_payload_is_detected_on_startup()
    {
        WithDatabase(path =>
        {
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var engine = new ReconciliationEngine(store);
                engine.RunCycle(
                    ReconciliationTrigger.Startup,
                    Snapshot(orders: [Order("tamper")]),
                    Snapshot(),
                    Now.AddSeconds(1)).IsSuccess.Should().BeTrue();
            }

            using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO execution_reconciliation_cases(
                        case_id, fact_sequence, venue_id, trading_account_id, environment,
                        subject_kind, subject_key, case_kind, case_status,
                        opened_at_utc_ticks, case_payload_json)
                    VALUES ('tampered-case', 1, 'paper', 'reconciliation-account', 0,
                            0, 'tampered', 2, 0, $openedAt, '{}');
                    """;
                command.Parameters.AddWithValue("$openedAt", Now.UtcDateTime.Ticks);
                command.ExecuteNonQuery().Should().Be(1);
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddSeconds(2));
            reopened.Integrity.Fault.Should().Be(SqliteExecutionLedgerFault.ReconciliationEvidenceInvalid);
            reopened.CanAdmitNewOrders.Should().BeFalse();
        });
    }

    private static ExecutionReconciliationSnapshot Snapshot(
        IReadOnlyList<ReconciliationOrderSnapshot>? orders = null,
        IReadOnlyList<ReconciliationFillSnapshot>? fills = null,
        IReadOnlyList<ReconciliationPositionSnapshot>? positions = null,
        IReadOnlyList<ReconciliationCashSnapshot>? cash = null,
        DateTimeOffset? capturedAtUtc = null) =>
        new(Resource, capturedAtUtc ?? Now,
            orders ?? [], fills ?? [], positions ?? [], cash ?? []);

    private static ReconciliationOrderSnapshot Order(string suffix, bool wasDispatched = true)
    {
        var instruction = Instruction(suffix);
        return new(
            instruction,
            instruction.Terms,
            wasDispatched ? OrderLifecycleState.Working : OrderLifecycleState.Armed,
            wasDispatched,
            wasDispatched ? new BrokerOrderId($"broker-{suffix}") : null,
            null,
            ScaledQuantity.Zero);
    }

    private static ReconciliationFillSnapshot Fill(string suffix) =>
        new(
            new TradeId($"trade-{suffix}"),
            new ClientOrderId($"client-{suffix}"),
            new BrokerOrderId($"broker-{suffix}"),
            null,
            Instrument,
            OrderSide.Buy,
            Q(1),
            P(100),
            ScaledMoney.Zero,
            Now);

    private static ReconciliationPositionSnapshot Position(long quantity) => new(Instrument, Q(quantity), Now);
    private static ReconciliationCashSnapshot Cash(long total) => new("USD", M(total), M(total), Now);

    private static CanonicalOrderInstruction Instruction(string suffix)
    {
        var terms = new CanonicalOrderTerms(
            OrderSide.Buy,
            CanonicalOrderType.Limit,
            CanonicalTimeInForce.GoodTillCancelled,
            Q(2),
            P(100),
            null);
        return new(
            new OrderIdentity(
                new IntentId($"intent-{suffix}"),
                null,
                new LegId($"leg-{suffix}"),
                new ClientOrderId($"client-{suffix}"),
                null,
                null,
                new CorrelationId("reconciliation-correlation"),
                new CausationId($"cause-{suffix}"),
                new ExecutionLeaseId("reconciliation-lease"),
                new FencingToken(1)),
            new TradeIntent(
                Instrument,
                TradeIntentQuantityMode.Delta,
                Q(2),
                null,
                null,
                ScaledMoney.Zero,
                "reconciliation-strategy",
                1,
                "reconciliation-policy",
                EntryLimitPrice: P(100)),
            terms);
    }

    private static SubmitOrderCommand Submit(string suffix, ExecutionLeaseClaim claim)
    {
        var metadata = Metadata($"submit-{suffix}", 0);
        var terms = new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(100));
        var mapping = new CanonicalInstructionMappingContext(
            new IntentId($"intent-{suffix}"), null, new LegId($"leg-{suffix}"),
            claim.LeaseId, claim.FencingToken, TradeIntentQuantityMode.Delta, Q(2),
            ScaledQuantity.Zero, null, null, ScaledMoney.Zero, 1, "reconciliation-policy",
            entryLimitPrice: P(100));
        CanonicalOrderInstructionMapper.TryCreate(
            metadata, new ClientOrderId($"client-{suffix}"), terms, mapping, out var instruction)
            .Should().Be(OrderDomainFault.None);
        return new SubmitOrderCommand(
            metadata, new OrderId($"order-{suffix}"), new ClientOrderId($"client-{suffix}"), terms, instruction!);
    }

    private static ExecutionCommandMetadata Metadata(string suffix, long expectedSequence) =>
        new(
            new CommandId($"command-{suffix}"),
            new CorrelationId("reconciliation-correlation"),
            new CausationId($"cause-{suffix}"),
            Resource.TradingAccountId,
            new StrategyId("reconciliation-strategy"),
            new StrategyVersion("1.0.0"),
            Resource.VenueId,
            Instrument,
            Resource.Environment,
            Now,
            expectedSequence);

    private static RiskEvaluationContext RiskContext() =>
        new(
            new RiskLimits(Q(100), Q(100), M(1_000_000), ScaledMoney.Zero, M(100_000), M(100_000), 100, TimeSpan.FromMinutes(1)),
            RiskControlMode.Active,
            false,
            ScaledQuantity.Zero,
            ScaledQuantity.Zero,
            ScaledQuantity.Zero,
            ScaledMoney.Zero,
            ScaledQuantity.Zero,
            ScaledMoney.Zero,
            ScaledQuantity.Zero,
            M(1_000_000),
            ScaledMoney.Zero,
            M(100_000),
            M(100_000),
            P(100),
            0,
            Now);

    private static OrderCommandContext Context(string suffix) =>
        new(new CausationId($"cause-{suffix}"), new DeduplicationKey($"dedupe-{suffix}"));
    private static ScaledQuantity Q(long value) => ScaledQuantity.FromWhole(value);
    private static ScaledPrice P(long value) => new(value, 0);
    private static ScaledMoney M(long value) => new(value, 0);

    private static void WithDatabase(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-reconciliation-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            action(Path.Combine(directory, "execution-ledger.db"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class MutableAdmissionGate : IExecutionReconciliationAdmissionGate
    {
        public bool IsOpen { get; set; }
        public bool CanAdmitNewExposure(ExecutionResource resource) => IsOpen && resource == Resource;
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class ThrowingSnapshotProvider : IExecutionReconciliationSnapshotProvider
    {
        public ExecutionReconciliationSnapshot CaptureReconciliationSnapshot(
            ExecutionResource resource,
            DateTimeOffset capturedAtUtc) => throw new InvalidDataException("Injected snapshot failure.");
    }
}
