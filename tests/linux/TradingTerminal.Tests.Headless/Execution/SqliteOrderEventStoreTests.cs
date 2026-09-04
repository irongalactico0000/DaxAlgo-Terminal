using FluentAssertions;
using Microsoft.Data.Sqlite;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.Infrastructure.Execution;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class SqliteOrderEventStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);
    private static readonly InstrumentId Instrument = new(909);
    private static readonly ExecutionResource Resource = new(
        new VenueId("paper"),
        new TradingAccountId("sqlite-paper-account"),
        ExecutionEnvironment.SimulatedPaper);

    [Fact]
    public void Partial_fill_reopens_with_identical_events_hashes_and_exact_projection()
    {
        WithDatabase(path =>
        {
            IReadOnlyList<string> originalEventJson;
            OmsOrderProjection originalProjection;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var venue = new DeterministicPaperVenue();
                venue.OnMarket(new PaperMarketSnapshot(
                    Instrument,
                    new ScaledPrice(99, 0),
                    new ScaledPrice(100_125, 3),
                    new ScaledQuantity(2, 3),
                    Now));
                store.Acquire(
                    new ExecutionResource(new VenueId("paper"), new TradingAccountId("sqlite-paper-account"), ExecutionEnvironment.SimulatedPaper),
                    new ExecutionLeaseId("sqlite-test-lease"),
                    new RuntimeInstanceId("sqlite-test-runtime"),
                    Now,
                    Now.AddHours(1)).IsSuccess.Should().BeTrue();
                var oms = new OrderManagementService(store, venue, store, new FixedClock(Now.UtcDateTime));
                var command = Submit(
                    "restart",
                    new OrderTerms(
                        OrderSide.Buy,
                        OrderType.Limit,
                        new ScaledQuantity(5, 3),
                        limitPrice: new ScaledPrice(101, 0)));

                var result = oms.Submit(command, RiskContext(), Context("restart"));

                result.IsSuccess.Should().BeTrue(result.Reason);
                result.Projection!.State.Should().Be(OrderLifecycleState.PartiallyFilled);
                result.Projection.FilledQuantity.Should().Be(new ScaledQuantity(2, 3));
                result.Projection.AverageFillPrice.Should().Be(new ScaledPrice(100_125, 3));
                originalProjection = result.Projection;
                originalEventJson = store.Read(command.ClientOrderId)
                    .Select(ExecutionCanonicalJson.Serialize)
                    .ToArray();
                store.Integrity.IsValid.Should().BeTrue();
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            var recovered = reopened.ReadProjection(originalProjection.ClientOrderId);

            reopened.Integrity.IsValid.Should().BeTrue(reopened.Integrity.Detail);
            reopened.CanAdmitNewOrders.Should().BeFalse("a non-terminal broker order requires reconciliation after restart");
            reopened.StartupRecovery.Should().ContainSingle(entry =>
                entry.ClientOrderId == originalProjection.ClientOrderId &&
                entry.State == OrderLifecycleState.PartiallyFilled);
            recovered.Should().BeEquivalentTo(originalProjection);
            reopened.Read(originalProjection.ClientOrderId)
                .Select(ExecutionCanonicalJson.Serialize)
                .Should().Equal(originalEventJson);
            reopened.ReadOutbox().Should().HaveCount(originalEventJson.Count);
        });
    }

    [Fact]
    public void Reopened_partial_fill_restores_venue_reconciles_then_continues_without_identity_reuse()
    {
        WithDatabase(path =>
        {
            SubmitOrderCommand original;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var venue = new DeterministicPaperVenue();
                venue.OnMarket(new PaperMarketSnapshot(Instrument, P(99), P(100), Q(2), Now));
                AcquireLease(store);
                var oms = new OrderManagementService(store, venue, store, new FixedClock(Now.UtcDateTime));
                original = Submit(
                    "recover-continue",
                    new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(5), limitPrice: P(101)));

                var submitted = oms.Submit(original, RiskContext(), Context("recover-continue"));

                submitted.IsSuccess.Should().BeTrue(submitted.Reason);
                submitted.Projection!.State.Should().Be(OrderLifecycleState.PartiallyFilled);
                submitted.Projection.BrokerOrderId.Should().Be(new BrokerOrderId("PAPER-1"));
                submitted.Projection.FilledQuantity.Should().Be(Q(2));
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            var recoveredVenue = new DeterministicPaperVenue();

            var recovery = recoveredVenue.RestoreFromLedger(Resource, reopened, Now.AddMinutes(1));
            var engine = new ReconciliationEngine(reopened);
            var startup = new PaperExecutionReconciliationCoordinator(
                    Resource,
                    reopened,
                    recoveredVenue,
                    engine,
                    new FixedClock(Now.AddMinutes(1).UtcDateTime))
                .RunStartup();

            recovery.IsSuccess.Should().BeTrue(recovery.Reason);
            recovery.WorkingOrderCount.Should().Be(1);
            recovery.CompletedOrderCount.Should().Be(0);
            recovery.FillCount.Should().Be(1);
            startup.IsSuccess.Should().BeTrue(startup.Reason);
            startup.IsAdmissionBlocked.Should().BeFalse();
            reopened.StartupRecovery.Should().BeEmpty();
            reopened.CanAdmitNewOrders.Should().BeTrue();
            reopened.ReadPositionProjections(Resource)
                .Should().ContainSingle().Which.Quantity.Should().Be(Q(2));
            reopened.ReadCashProjections(Resource)
                .Should().ContainSingle().Which.Total.Should().Be(M(-200));

            var resumedOms = new OrderManagementService(
                reopened,
                recoveredVenue,
                reopened,
                new FixedClock(Now.AddMinutes(2).UtcDateTime),
                engine);
            recoveredVenue.OnMarket(new PaperMarketSnapshot(
                Instrument,
                P(100),
                P(101),
                bidAvailableQuantity: Q(10),
                askAvailableQuantity: Q(3),
                Now.AddMinutes(2)));

            resumedOms.ProcessVenueEvents().Should().ContainSingle(result => result.IsSuccess);
            var filled = resumedOms.Query(original.ClientOrderId)!;
            filled.State.Should().Be(OrderLifecycleState.Filled);
            filled.FilledQuantity.Should().Be(Q(5));
            reopened.Read(original.ClientOrderId)
                .Where(orderEvent => orderEvent.Fill is not null)
                .Select(orderEvent => orderEvent.Fill!.TradeId.Value)
                .Should().Equal("PAPER-TRADE-1", "PAPER-TRADE-2");

            var next = Submit(
                "recover-next",
                new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(1), limitPrice: P(90)));
            var nextResult = resumedOms.Submit(next, RiskContext(), Context("recover-next"));
            nextResult.IsSuccess.Should().BeTrue(nextResult.Reason);
            nextResult.Projection!.BrokerOrderId.Should().Be(new BrokerOrderId("PAPER-2"));

            var snapshot = recoveredVenue.CaptureReconciliationSnapshot(Resource, Now.AddMinutes(3));
            snapshot.Positions.Should().ContainSingle().Which.Quantity.Should().Be(Q(5));
            snapshot.Cash.Should().ContainSingle().Which.Total.Should().Be(M(-503));
            reopened.ReadPositionProjections(Resource)
                .Should().ContainSingle().Which.Quantity.Should().Be(Q(5));
            reopened.ReadCashProjections(Resource)
                .Should().ContainSingle().Which.Total.Should().Be(M(-503));
        });
    }

    [Fact]
    public void Inactive_zero_fill_stop_restores_monitoring_and_triggers_only_after_threshold()
    {
        WithDatabase(path =>
        {
            SubmitOrderCommand stop;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var venue = new DeterministicPaperVenue();
                AcquireLease(store);
                var oms = new OrderManagementService(store, venue, store, new FixedClock(Now.UtcDateTime));
                stop = Submit(
                    "inactive-stop",
                    new OrderTerms(
                        OrderSide.Buy,
                        OrderType.Stop,
                        Q(2),
                        stopPrice: P(105)));
                var submitted = oms.Submit(stop, RiskContext(), Context("inactive-stop"));
                submitted.Projection!.State.Should().Be(OrderLifecycleState.Working);
                store.Read(stop.ClientOrderId).Select(item => item.Kind).Should().ContainInOrder(
                    OrderEventKind.VenueAcknowledged,
                    OrderEventKind.StopMonitoringStarted);
                store.Read(stop.ClientOrderId).Select(item => item.Kind).Should().NotContain(
                    OrderEventKind.StopActivated);
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            var recoveredVenue = new DeterministicPaperVenue();

            var recovery = recoveredVenue.RestoreFromLedger(Resource, reopened, Now.AddMinutes(1));

            recovery.IsSuccess.Should().BeTrue(recovery.Reason);
            recovery.WorkingOrderCount.Should().Be(1);
            var resumedOms = new OrderManagementService(
                reopened,
                recoveredVenue,
                reopened,
                new FixedClock(Now.AddMinutes(2).UtcDateTime));
            recoveredVenue.OnMarket(new PaperMarketSnapshot(Instrument, P(103), P(104), Q(2), Now.AddMinutes(2)));
            resumedOms.ProcessVenueEvents().Should().BeEmpty();
            resumedOms.Query(stop.ClientOrderId)!.State.Should().Be(OrderLifecycleState.Working);

            recoveredVenue.OnMarket(new PaperMarketSnapshot(Instrument, P(104), P(105), Q(2), Now.AddMinutes(2)));
            resumedOms.ProcessVenueEvents().Should().HaveCount(2).And.OnlyContain(result => result.IsSuccess);
            resumedOms.Query(stop.ClientOrderId)!.State.Should().Be(OrderLifecycleState.Filled);
            reopened.Read(stop.ClientOrderId).Select(item => item.Kind).Should().ContainInOrder(
                OrderEventKind.StopMonitoringStarted,
                OrderEventKind.StopActivated,
                OrderEventKind.FillReceived);
        });
    }

    [Fact]
    public void Activated_zero_fill_stop_limit_restores_active_and_fills_without_retriggering()
    {
        WithDatabase(path =>
        {
            SubmitOrderCommand stopLimit;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var venue = new DeterministicPaperVenue();
                AcquireLease(store);
                var oms = new OrderManagementService(store, venue, store, new FixedClock(Now.UtcDateTime));
                stopLimit = Submit(
                    "activated-stop-limit",
                    new OrderTerms(
                        OrderSide.Buy,
                        OrderType.StopLimit,
                        Q(2),
                        limitPrice: P(100),
                        stopPrice: P(105)));
                oms.Submit(stopLimit, RiskContext(), Context("activated-stop-limit"))
                    .Projection!.State.Should().Be(OrderLifecycleState.Working);

                venue.OnMarket(new PaperMarketSnapshot(Instrument, P(105), P(106), Q(2), Now));
                oms.ProcessVenueEvents().Should().ContainSingle(result => result.IsSuccess);
                var activated = oms.Query(stopLimit.ClientOrderId)!;
                activated.State.Should().Be(OrderLifecycleState.Working);
                activated.FilledQuantity.Should().Be(ScaledQuantity.Zero);
                store.Read(stopLimit.ClientOrderId).Select(item => item.Kind).Should().ContainInOrder(
                    OrderEventKind.StopMonitoringStarted,
                    OrderEventKind.StopActivated);
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            var recoveredVenue = new DeterministicPaperVenue();
            var recovery = recoveredVenue.RestoreFromLedger(Resource, reopened, Now.AddMinutes(1));

            recovery.IsSuccess.Should().BeTrue(recovery.Reason);
            var resumedOms = new OrderManagementService(
                reopened,
                recoveredVenue,
                reopened,
                new FixedClock(Now.AddMinutes(2).UtcDateTime));
            recoveredVenue.OnMarket(new PaperMarketSnapshot(Instrument, P(89), P(90), Q(2), Now.AddMinutes(2)));

            resumedOms.ProcessVenueEvents().Should().ContainSingle(result => result.IsSuccess);
            var filled = resumedOms.Query(stopLimit.ClientOrderId)!;
            filled.State.Should().Be(OrderLifecycleState.Filled);
            filled.FilledQuantity.Should().Be(Q(2));
            reopened.Read(stopLimit.ClientOrderId).Count(item => item.Kind == OrderEventKind.StopActivated)
                .Should().Be(1, "the restored StopLimit was already activated before restart");
        });
    }

    [Fact]
    public void Legacy_zero_fill_stop_without_monitoring_evidence_remains_fail_closed()
    {
        WithDatabase(path =>
        {
            SubmitOrderCommand stop;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                AcquireLease(store);
                var dispatcher = new DeferredCommandDispatcher();
                var oms = new OrderManagementService(store, dispatcher, store, new FixedClock(Now.UtcDateTime));
                stop = Submit(
                    "legacy-ambiguous-stop",
                    new OrderTerms(OrderSide.Buy, OrderType.Stop, Q(2), stopPrice: P(105)));
                oms.Submit(stop, RiskContext(), Context("legacy-ambiguous-stop"))
                    .Projection!.State.Should().Be(OrderLifecycleState.Working);
                store.Read(stop.ClientOrderId).Select(item => item.Kind).Should().NotContain(
                    OrderEventKind.StopMonitoringStarted);
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            var recoveredVenue = new DeterministicPaperVenue();
            var recovery = recoveredVenue.RestoreFromLedger(Resource, reopened, Now.AddMinutes(1));

            recovery.Fault.Should().Be(PaperVenueRecoveryFault.AmbiguousStopActivation);
            recovery.BlockedOrderId.Should().Be(stop.ClientOrderId);
            reopened.CanAdmitNewOrders.Should().BeFalse();
            recoveredVenue.CaptureReconciliationSnapshot(Resource, Now.AddMinutes(1)).Orders.Should().BeEmpty();
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Pending_cancel_or_replace_without_a_receipt_is_replayed_on_the_pristine_Paper_venue(bool replace)
    {
        WithDatabase(path =>
        {
            SubmitOrderCommand submit;
            OrderTerms? replacementTerms = null;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                AcquireLease(store);
                var venue = new DeterministicPaperVenue();
                var crashStore = new CrashBeforeDispatchReceiptStore(store);
                var oms = new OrderManagementService(crashStore, venue, store, new FixedClock(Now.UtcDateTime));
                submit = Submit(
                    $"pending-{(replace ? "replace" : "cancel")}",
                    new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(90)));
                var working = oms.Submit(submit, RiskContext(), Context("pending-command-submit"));
                working.Projection!.State.Should().Be(OrderLifecycleState.Working);

                replacementTerms = new OrderTerms(
                    OrderSide.Buy,
                    OrderType.Limit,
                    Q(2),
                    limitPrice: P(91));
                Func<OmsCommandResult> action = replace
                    ? () => oms.Replace(
                        new ReplaceOrderCommand(
                            Metadata("pending-replace", working.Projection.LastSequence),
                            submit.OrderId,
                            replacementTerms),
                        RiskContext(),
                        Context("pending-replace"))
                    : () => oms.Cancel(
                        new CancelOrderCommand(
                            Metadata("pending-cancel", working.Projection.LastSequence),
                            submit.OrderId),
                        Context("pending-cancel"));

                action.Should().Throw<InjectedDispatchReceiptCrash>();
                store.ReadProjection(submit.ClientOrderId)!.State.Should().Be(
                    replace ? OrderLifecycleState.PendingReplace : OrderLifecycleState.PendingCancel);
                store.Read(submit.ClientOrderId).Select(item => item.Kind).Should().NotContain(
                    replace ? OrderEventKind.ReplaceDispatchRecorded : OrderEventKind.CancelDispatchRecorded);
            }

            var recoveredAt = Now.AddHours(2);
            using var runtime = PaperExecutionServiceRuntime.Create(
                path,
                Resource,
                new FixedClock(recoveredAt.UtcDateTime),
                new ExecutionLeaseId($"replayed-{(replace ? "replace" : "cancel")}-lease"),
                new RuntimeInstanceId($"replayed-{(replace ? "replace" : "cancel")}-owner"));
            var recovered = runtime.Oms.Query(submit.ClientOrderId)!;

            recovered.State.Should().Be(replace ? OrderLifecycleState.Working : OrderLifecycleState.Cancelled);
            if (replace)
                recovered.Terms.Should().Be(replacementTerms);
            runtime.Ledger.CanAdmitNewOrders.Should().BeTrue();
            runtime.Ledger.Read(submit.ClientOrderId).Select(item => item.Kind).Should().ContainInOrder(
                replace ? OrderEventKind.ReplaceRequested : OrderEventKind.CancelRequested,
                replace ? OrderEventKind.ReplaceConfirmed : OrderEventKind.CancelConfirmed);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Receipt_recorded_pending_cancel_or_replace_finishes_its_callback_after_restart(bool replace)
    {
        WithDatabase(path =>
        {
            SubmitOrderCommand submit;
            OrderTerms? replacementTerms = null;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                AcquireLease(store);
                var dispatcher = new DeferredCommandDispatcher();
                var oms = new OrderManagementService(store, dispatcher, store, new FixedClock(Now.UtcDateTime));
                submit = Submit(
                    $"receipt-{(replace ? "replace" : "cancel")}",
                    new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(90)));
                var working = oms.Submit(submit, RiskContext(), Context("receipt-command-submit"));
                working.Projection!.State.Should().Be(OrderLifecycleState.Working);

                replacementTerms = new OrderTerms(
                    OrderSide.Buy,
                    OrderType.Limit,
                    Q(2),
                    limitPrice: P(91));
                var pending = replace
                    ? oms.Replace(
                        new ReplaceOrderCommand(
                            Metadata("receipt-replace", working.Projection.LastSequence),
                            submit.OrderId,
                            replacementTerms),
                        RiskContext(),
                        Context("receipt-replace"))
                    : oms.Cancel(
                        new CancelOrderCommand(
                            Metadata("receipt-cancel", working.Projection.LastSequence),
                            submit.OrderId),
                        Context("receipt-cancel"));

                pending.Projection!.State.Should().Be(
                    replace ? OrderLifecycleState.PendingReplace : OrderLifecycleState.PendingCancel);
                var receiptEvent = store.Read(submit.ClientOrderId).Single(item => item.Kind ==
                    (replace ? OrderEventKind.ReplaceDispatchRecorded : OrderEventKind.CancelDispatchRecorded));
                receiptEvent.DispatchReceipt.Should().NotBeNull();
                receiptEvent.EventHash.Should().NotBeNullOrWhiteSpace();
                var tamperedJson = ExecutionCanonicalJson.Serialize(receiptEvent).Replace(
                    receiptEvent.DispatchReceipt!.DispatchAttemptId.Value,
                    "tampered-dispatch-attempt",
                    StringComparison.Ordinal);
                var tamperedReceipt = ExecutionCanonicalJson.Deserialize<OmsOrderEvent>(tamperedJson);
                var tamperedChain = store.Read(submit.ClientOrderId).ToArray();
                tamperedChain[^1] = tamperedReceipt;
                OrderEventChainVerifier.Verify(tamperedChain).Fault.Should().Be(
                    OrderEventChainFault.EventHashMismatch,
                    "the dispatch receipt is part of the immutable event hash");
            }

            var recoveredAt = Now.AddHours(2);
            using var runtime = PaperExecutionServiceRuntime.Create(
                path,
                Resource,
                new FixedClock(recoveredAt.UtcDateTime),
                new ExecutionLeaseId($"receipt-{(replace ? "replace" : "cancel")}-lease"),
                new RuntimeInstanceId($"receipt-{(replace ? "replace" : "cancel")}-owner"));
            var recovered = runtime.Oms.Query(submit.ClientOrderId)!;

            recovered.State.Should().Be(replace ? OrderLifecycleState.Working : OrderLifecycleState.Cancelled);
            if (replace)
                recovered.Terms.Should().Be(replacementTerms);
            runtime.Ledger.CanAdmitNewOrders.Should().BeTrue();
            runtime.Ledger.Read(submit.ClientOrderId).Select(item => item.Kind).Should().ContainInOrder(
                replace ? OrderEventKind.ReplaceDispatchRecorded : OrderEventKind.CancelDispatchRecorded,
                replace ? OrderEventKind.ReplaceConfirmed : OrderEventKind.CancelConfirmed);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicitly_unknown_cancel_or_replace_outcome_remains_fail_closed(bool replace)
    {
        WithDatabase(path =>
        {
            SubmitOrderCommand submit;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                AcquireLease(store);
                var dispatcher = new DeferredCommandDispatcher(unknownCommands: true);
                var oms = new OrderManagementService(store, dispatcher, store, new FixedClock(Now.UtcDateTime));
                submit = Submit(
                    $"unknown-{(replace ? "replace" : "cancel")}",
                    new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(90)));
                var working = oms.Submit(submit, RiskContext(), Context("unknown-command-submit"));

                var unknown = replace
                    ? oms.Replace(
                        new ReplaceOrderCommand(
                            Metadata("unknown-replace", working.Projection!.LastSequence),
                            submit.OrderId,
                            new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(91))),
                        RiskContext(),
                        Context("unknown-replace"))
                    : oms.Cancel(
                        new CancelOrderCommand(
                            Metadata("unknown-cancel", working.Projection!.LastSequence),
                            submit.OrderId),
                        Context("unknown-cancel"));

                unknown.Fault.Should().Be(OmsCommandFault.DispatchOutcomeUnknown);
                unknown.Projection!.State.Should().Be(OrderLifecycleState.Unknown);
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            var recoveredVenue = new DeterministicPaperVenue();
            var recovery = recoveredVenue.RestoreFromLedger(Resource, reopened, Now.AddMinutes(1));

            recovery.Fault.Should().Be(PaperVenueRecoveryFault.UnsafeLifecycleState);
            recovery.BlockedOrderId.Should().Be(submit.ClientOrderId);
            reopened.CanAdmitNewOrders.Should().BeFalse();
        });
    }

    [Fact]
    public void Inbox_exact_replay_and_conflicting_duplicate_survive_restart()
    {
        WithDatabase(path =>
        {
            var command = Submit("dedupe", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1)));
            var draft = Draft(command, "persistent-dedupe", reason: null);
            OmsOrderEvent committed;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var first = store.Append(draft, Now);
                first.WasAppended.Should().BeTrue();
                committed = first.Event!;
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            var replay = reopened.Append(draft, Now.AddMinutes(2));
            var conflict = reopened.Append(
                Draft(command, "persistent-dedupe", reason: "different payload"),
                Now.AddMinutes(2));

            replay.IsExactReplay.Should().BeTrue();
            replay.Event!.EventHash.Should().Be(committed.EventHash);
            conflict.Fault.Should().Be(OrderEventAppendFault.ConflictingDuplicate);
            reopened.Read(command.ClientOrderId).Should().ContainSingle();
            reopened.ReadOutbox().Should().ContainSingle();
        });
    }

    [Fact]
    public void Failure_before_commit_leaves_no_partial_inbox_event_projection_or_outbox()
    {
        WithDatabase(path =>
        {
            var command = Submit("rollback", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1)));
            var draft = Draft(command, "rollback-dedupe", reason: null);
            using (var store = new SqliteOrderEventStore(
                       path,
                       Now,
                       _ => throw new InjectedAppendFailure()))
            {
                var action = () => store.Append(draft, Now);
                action.Should().Throw<InjectedAppendFailure>();
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            reopened.Read(command.ClientOrderId).Should().BeEmpty();
            reopened.ReadProjection(command.ClientOrderId).Should().BeNull();
            reopened.ReadOutbox().Should().BeEmpty();
            reopened.Integrity.IsValid.Should().BeTrue(reopened.Integrity.Detail);
        });
    }

    [Fact]
    public void Tampered_materialized_projection_blocks_admission_on_reopen()
    {
        WithDatabase(path =>
        {
            var command = Submit("tamper", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1)));
            using (var store = new SqliteOrderEventStore(path, Now))
                store.Append(Draft(command, "tamper-dedupe", reason: null), Now).WasAppended.Should().BeTrue();

            using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                using var update = connection.CreateCommand();
                update.CommandText = """
                    UPDATE execution_order_projections
                    SET last_event_hash = $tampered;
                    """;
                update.Parameters.AddWithValue("$tampered", new string('0', 64));
                update.ExecuteNonQuery().Should().Be(1);
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            reopened.Integrity.Fault.Should().Be(SqliteExecutionLedgerFault.ProjectionMismatch);
            reopened.CanAdmitNewOrders.Should().BeFalse();
            reopened.Append(
                Draft(Submit("blocked", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1))), "blocked", null),
                Now.AddMinutes(2)).Fault.Should().Be(OrderEventAppendFault.LedgerIntegrityBlocked);
        });
    }

    [Fact]
    public void Schema_claims_application_id_version_wal_and_append_only_events()
    {
        WithDatabase(path =>
        {
            var command = Submit("schema", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1)));
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                store.SchemaVersion.Should().Be(2);
                store.JournalMode.Should().BeEquivalentTo("wal");
                store.Append(Draft(command, "schema-dedupe", null), Now).WasAppended.Should().BeTrue();
            }

            using var connection = new SqliteConnection($"Data Source={path}");
            connection.Open();
            Scalar(connection, "PRAGMA application_id;").Should().Be(0x44415845);
            Scalar(connection, "PRAGMA user_version;").Should().Be(2);
            using var update = connection.CreateCommand();
            update.CommandText = "UPDATE execution_order_events SET state_after = state_after;";
            var action = () => update.ExecuteNonQuery();
            action.Should().Throw<SqliteException>().WithMessage("*append-only*");
        });
    }

    [Fact]
    public void Version_one_ledger_migrates_and_backfills_position_and_cash_from_immutable_fills()
    {
        WithDatabase(path =>
        {
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var venue = new DeterministicPaperVenue();
                venue.OnMarket(new PaperMarketSnapshot(Instrument, P(99), P(100), Q(2), Now));
                AcquireLease(store);
                var oms = new OrderManagementService(store, venue, store, new FixedClock(Now.UtcDateTime));
                oms.Submit(
                    Submit("migration", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2))),
                    RiskContext(),
                    Context("migration")).IsSuccess.Should().BeTrue();
            }

            using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                using var downgrade = connection.CreateCommand();
                downgrade.CommandText = """
                    DROP TABLE execution_position_projections;
                    DROP TABLE execution_cash_projections;
                    DELETE FROM execution_schema_migrations WHERE version = 2;
                    PRAGMA user_version=1;
                    """;
                downgrade.ExecuteNonQuery();
            }

            using var migrated = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            migrated.SchemaVersion.Should().Be(2);
            migrated.Integrity.IsValid.Should().BeTrue(migrated.Integrity.Detail);
            migrated.ReadPositionProjections(Resource)
                .Should().ContainSingle().Which.Quantity.Should().Be(Q(2));
            migrated.ReadCashProjections(Resource)
                .Should().ContainSingle().Which.Total.Should().Be(M(-200));

            using var verify = new SqliteConnection($"Data Source={path}");
            verify.Open();
            Scalar(verify, "SELECT COUNT(*) FROM execution_schema_migrations;").Should().Be(2);
        });
    }

    [Fact]
    public void Tampered_position_projection_is_detected_against_fill_replay_on_reopen()
    {
        WithDatabase(path =>
        {
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var venue = new DeterministicPaperVenue();
                venue.OnMarket(new PaperMarketSnapshot(Instrument, P(99), P(100), Q(2), Now));
                AcquireLease(store);
                var oms = new OrderManagementService(store, venue, store, new FixedClock(Now.UtcDateTime));
                oms.Submit(
                    Submit("economic-tamper", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2))),
                    RiskContext(),
                    Context("economic-tamper")).IsSuccess.Should().BeTrue();
            }

            using (var connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                using var tamper = connection.CreateCommand();
                tamper.CommandText = "UPDATE execution_position_projections SET quantity_coefficient = 999;";
                tamper.ExecuteNonQuery().Should().Be(1);
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            reopened.Integrity.Fault.Should().Be(SqliteExecutionLedgerFault.EconomicProjectionMismatch);
            reopened.CanAdmitNewOrders.Should().BeFalse();
        });
    }

    [Fact]
    public void Fractional_buy_and_sell_net_exact_position_and_cash_materializations()
    {
        WithDatabase(path =>
        {
            using var store = new SqliteOrderEventStore(path, Now);
            var venue = new DeterministicPaperVenue();
            venue.OnMarket(new PaperMarketSnapshot(
                Instrument,
                P(100),
                new ScaledPrice(10_025, 2),
                bidAvailableQuantity: ScaledQuantity.Zero,
                askAvailableQuantity: new ScaledQuantity(15, 1),
                Now));
            AcquireLease(store);
            var oms = new OrderManagementService(store, venue, store, new FixedClock(Now.UtcDateTime));

            oms.Submit(
                Submit("fractional-buy", new OrderTerms(
                    OrderSide.Buy, OrderType.Market, new ScaledQuantity(15, 1))),
                RiskContext(),
                Context("fractional-buy")).IsSuccess.Should().BeTrue();
            venue.OnMarket(new PaperMarketSnapshot(
                Instrument,
                new ScaledPrice(10_175, 2),
                P(102),
                bidAvailableQuantity: new ScaledQuantity(5, 1),
                askAvailableQuantity: ScaledQuantity.Zero,
                Now));
            oms.Submit(
                Submit("fractional-sell", new OrderTerms(
                    OrderSide.Sell, OrderType.Market, new ScaledQuantity(5, 1))),
                RiskContext(),
                Context("fractional-sell")).IsSuccess.Should().BeTrue();

            store.ReadPositionProjections(Resource)
                .Should().ContainSingle().Which.Quantity.Should().Be(Q(1));
            store.ReadCashProjections(Resource)
                .Should().ContainSingle().Which.Total.Should().Be(new ScaledMoney(-995, 1));
            store.VerifyIntegrity().IsValid.Should().BeTrue();
        });
    }

    [Fact]
    public void Failure_after_fill_materialization_before_commit_rolls_back_fill_position_and_cash_together()
    {
        WithDatabase(path =>
        {
            var appendCount = 0;
            using (var store = new SqliteOrderEventStore(
                       path,
                       Now,
                       _ =>
                       {
                           appendCount++;
                           if (appendCount == 8) throw new InjectedAppendFailure();
                       }))
            {
                var venue = new DeterministicPaperVenue();
                venue.OnMarket(new PaperMarketSnapshot(Instrument, P(99), P(100), Q(2), Now));
                AcquireLease(store);
                var oms = new OrderManagementService(store, venue, store, new FixedClock(Now.UtcDateTime));

                var action = () => oms.Submit(
                    Submit("economic-rollback", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2))),
                    RiskContext(),
                    Context("economic-rollback"));

                action.Should().Throw<InjectedAppendFailure>();
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            reopened.Integrity.IsValid.Should().BeTrue(reopened.Integrity.Detail);
            reopened.ReadOutbox().Select(entry => entry.Event.Kind)
                .Should().NotContain(OrderEventKind.FillReceived);
            reopened.ReadPositionProjections(Resource).Should().BeEmpty();
            reopened.ReadCashProjections(Resource).Should().BeEmpty();
        });
    }

    private static SubmitOrderCommand Submit(string suffix, OrderTerms terms)
    {
        var metadata = Metadata($"submit-{suffix}", 0);
        var clientOrderId = new ClientOrderId($"client-{suffix}");
        var signed = terms.Side == OrderSide.Buy
            ? terms.Quantity
            : new ScaledQuantity(-terms.Quantity.Coefficient, terms.Quantity.Scale);
        var context = new CanonicalInstructionMappingContext(
            new IntentId($"intent-{suffix}"),
            null,
            new LegId($"leg-{suffix}"),
            new ExecutionLeaseId("sqlite-test-lease"),
            new FencingToken(1),
            TradeIntentQuantityMode.Delta,
            signed,
            ScaledQuantity.Zero,
            null,
            null,
            ScaledMoney.Zero,
            1,
            "sqlite-test-policy");
        CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            clientOrderId,
            terms,
            context,
            out var instruction).Should().Be(OrderDomainFault.None);
        return new SubmitOrderCommand(
            metadata,
            new OrderId($"order-{suffix}"),
            clientOrderId,
            terms,
            instruction!);
    }

    private static OrderEventDraft Draft(
        SubmitOrderCommand command,
        string dedupe,
        string? reason) => new(
            command.ClientOrderId,
            OrderEventKind.DraftCreated,
            OrderLifecycleState.Draft,
            OrderEventSource.Command,
            new DeduplicationKey(dedupe),
            Now,
            new CausationId($"cause-{dedupe}"),
            SubmitCommand: command,
            Reason: reason);

    private static ExecutionCommandMetadata Metadata(string operation, long expectedSequence) => new(
        new CommandId($"command-{operation}"),
        new CorrelationId("sqlite-correlation"),
        new CausationId($"cause-{operation}"),
        new TradingAccountId("sqlite-paper-account"),
        new StrategyId("sqlite-strategy"),
        new StrategyVersion("1.0.0"),
        new VenueId("paper"),
        Instrument,
        ExecutionEnvironment.SimulatedPaper,
        Now,
        expectedSequence);

    private static RiskEvaluationContext RiskContext() => new(
        new RiskLimits(
            Q(100),
            Q(100),
            M(100_000),
            ScaledMoney.Zero,
            M(10_000),
            M(10_000),
            100,
            TimeSpan.FromMinutes(1)),
        RiskControlMode.Active,
        killSwitchActive: false,
        currentPositionQuantity: ScaledQuantity.Zero,
        currentBuyReservedQuantity: ScaledQuantity.Zero,
        currentSellReservedQuantity: ScaledQuantity.Zero,
        currentGrossReservedNotional: ScaledMoney.Zero,
        existingOrderSignedReservation: ScaledQuantity.Zero,
        existingOrderGrossReservation: ScaledMoney.Zero,
        existingOrderFilledQuantity: ScaledQuantity.Zero,
        availableBuyingPower: M(100_000),
        dailyNetRealizedPnl: ScaledMoney.Zero,
        currentEquity: M(100_000),
        peakEquity: M(100_000),
        marketPrice: new ScaledPrice(100, 0),
        exposureCommandsInWindow: 0,
        evaluatedAtUtc: Now);

    private static OrderCommandContext Context(string suffix) => new(
        new CausationId($"cause-{suffix}"),
        new DeduplicationKey($"command-{suffix}"));

    private static ScaledQuantity Q(long value) => ScaledQuantity.FromWhole(value);
    private static ScaledPrice P(long value) => new(value, 0);
    private static ScaledMoney M(long value) => new(value, 0);

    private static void AcquireLease(SqliteOrderEventStore store) =>
        store.Acquire(
            Resource,
            new ExecutionLeaseId("sqlite-test-lease"),
            new RuntimeInstanceId("sqlite-test-runtime"),
            Now,
            Now.AddHours(1)).IsSuccess.Should().BeTrue();

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static void WithDatabase(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-sqlite-tests", Guid.NewGuid().ToString("N"));
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

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class InjectedAppendFailure : Exception;
    private sealed class InjectedDispatchReceiptCrash : Exception;

    private sealed class CrashBeforeDispatchReceiptStore(IOrderEventStore inner) : IOrderEventStore
    {
        public OrderEventAppendResult Append(OrderEventDraft draft, DateTimeOffset recordedAtUtc)
        {
            if (draft.Kind is OrderEventKind.CancelDispatchRecorded or OrderEventKind.ReplaceDispatchRecorded)
                throw new InjectedDispatchReceiptCrash();
            return inner.Append(draft, recordedAtUtc);
        }

        public IReadOnlyList<OmsOrderEvent> Read(ClientOrderId aggregateId) => inner.Read(aggregateId);
        public OmsOrderProjection? ReadProjection(ClientOrderId aggregateId) => inner.ReadProjection(aggregateId);
        public IReadOnlyList<OrderEventOutboxEntry> ReadOutbox(long afterExclusiveSequence = 0) =>
            inner.ReadOutbox(afterExclusiveSequence);
    }

    private sealed class DeferredCommandDispatcher(bool unknownCommands = false) : IPaperExecutionDispatcher
    {
        private readonly Queue<PaperVenueEvent> _events = [];
        private readonly BrokerOrderId _brokerOrderId = new("PAPER-1");
        private long _nextDispatch;

        public ExecutionDispatchResult Submit(SubmitOrderCommand command, OmsOrderProjection projection)
        {
            _events.Enqueue(new PaperVenueEvent(
                new ExecutionEventId("deferred-event-ack-1"),
                PaperVenueEventKind.Acknowledged,
                command.ClientOrderId,
                command.Metadata.CreatedAtUtc,
                command.Metadata.CausationId ?? new CausationId(command.Metadata.CommandId.Value),
                _brokerOrderId));
            return Dispatched("submit", command.Metadata.CreatedAtUtc);
        }

        public ExecutionDispatchResult Cancel(CancelOrderCommand command, OmsOrderProjection projection) =>
            unknownCommands
                ? ExecutionDispatchResult.Unknown("The injected cancel outcome is unknown.")
                : Dispatched("cancel", command.Metadata.CreatedAtUtc);

        public ExecutionDispatchResult Replace(ReplaceOrderCommand command, OmsOrderProjection projection) =>
            unknownCommands
                ? ExecutionDispatchResult.Unknown("The injected replace outcome is unknown.")
                : Dispatched("replace", command.Metadata.CreatedAtUtc);

        public IReadOnlyList<PaperVenueEvent> DrainEvents()
        {
            var result = _events.ToArray();
            _events.Clear();
            return Array.AsReadOnly(result);
        }

        private ExecutionDispatchResult Dispatched(string operation, DateTimeOffset atUtc) =>
            ExecutionDispatchResult.Dispatched(new ExecutionDispatchReceipt(
                new DispatchAttemptId($"deferred-{operation}-{checked(++_nextDispatch)}"),
                atUtc,
                _brokerOrderId));
    }
}
