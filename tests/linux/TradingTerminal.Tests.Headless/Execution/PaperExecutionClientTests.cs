using FluentAssertions;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.Infrastructure.Execution;
using TradingTerminal.UI.Execution;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class PaperExecutionClientTests
{
    private static readonly ExecutionResource Resource = new(
        new VenueId("paper"),
        new TradingAccountId("desktop-client-account"),
        ExecutionEnvironment.SimulatedPaper);
    private static readonly InstrumentId Instrument = new(831);

    [Fact]
    public async Task Resync_rebuilds_exact_orders_fills_positions_cash_and_event_history()
    {
        await WithRuntime(async fixture =>
        {
            using var client = new PaperExecutionClient(fixture.Runtime.Service, fixture.Clock);
            var submit = fixture.Submit("read-model", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2)));

            var result = await client.SubmitAsync(new ExecutionSubmitRequest(submit, fixture.RiskContext()));
            var snapshot = client.GetSnapshot();

            result.IsSuccess.Should().BeTrue(result.Message);
            snapshot.LeaseHeld.Should().BeTrue();
            snapshot.AdmissionOpen.Should().BeTrue();
            snapshot.Orders.Should().ContainSingle().Which.State.Should().Be(OrderLifecycleState.Filled);
            snapshot.Economics.Fills.Should().ContainSingle().Which.Quantity.Should().Be(Q(2));
            snapshot.Economics.Positions.Should().ContainSingle().Which.Quantity.Should().Be(Q(2));
            snapshot.Economics.Cash.Should().ContainSingle().Which.Total.Should().Be(M(-200));
            snapshot.LedgerEvents.Select(item => item.Kind).Should().ContainInOrder(
                OrderEventKind.DraftCreated,
                OrderEventKind.RiskAccepted,
                OrderEventKind.SubmissionRecorded,
                OrderEventKind.VenueAcknowledged,
                OrderEventKind.FillReceived);
            snapshot.RiskDecisionFacts.Should().ContainSingle().Which.Should().Match<PaperRiskDecisionSnapshot>(item =>
                item.ClientOrderId == submit.ClientOrderId &&
                item.EventKind == OrderEventKind.RiskAccepted &&
                item.Observation.Decision.IsAllowed &&
                item.Observation.Decision.Code == RiskDecisionCode.Allowed &&
                item.Observation.CommandPayloadHashSha256 == submit.PayloadHashSha256);
            snapshot.QualityFacts.Should().Match<PaperExecutionQualitySnapshot>(quality =>
                quality.Orders == 1 &&
                quality.FilledOrders == 1 &&
                quality.Rejects == 0 &&
                quality.FillRatePercent == 100d &&
                quality.AcknowledgementObservationCount == 1 &&
                quality.SlippageObservationCount == 0);
            snapshot.LastOutboxSequence.Should().Be(snapshot.LedgerEvents.Count);
        });
    }

    [Fact]
    public async Task Client_replace_and_cancel_use_the_latest_verified_order_sequence()
    {
        await WithRuntime(async fixture =>
        {
            using var client = new PaperExecutionClient(fixture.Runtime.Service, fixture.Clock);
            var replaceOrder = fixture.Submit(
                "replace",
                new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(90)));
            (await client.SubmitAsync(new ExecutionSubmitRequest(replaceOrder, fixture.RiskContext())))
                .IsSuccess.Should().BeTrue();
            fixture.Advance();

            var replaced = await client.ReplaceAsync(
                replaceOrder.ClientOrderId,
                new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(101)),
                fixture.RiskContext());
            fixture.Advance();
            var cancelOrder = fixture.Submit(
                "cancel",
                new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(1), limitPrice: P(90)));
            (await client.SubmitAsync(new ExecutionSubmitRequest(cancelOrder, fixture.RiskContext())))
                .IsSuccess.Should().BeTrue();
            fixture.Advance();
            var cancelled = await client.CancelAsync(cancelOrder.ClientOrderId);
            var snapshot = client.GetSnapshot();

            replaced.IsSuccess.Should().BeTrue(replaced.Message);
            cancelled.IsSuccess.Should().BeTrue(cancelled.Message);
            snapshot.Orders.Single(item => item.ClientOrderId == replaceOrder.ClientOrderId)
                .State.Should().Be(OrderLifecycleState.Filled);
            snapshot.Orders.Single(item => item.ClientOrderId == cancelOrder.ClientOrderId)
                .State.Should().Be(OrderLifecycleState.Cancelled);
            snapshot.LedgerEvents.Select(item => item.Kind).Should().Contain(OrderEventKind.ReplaceConfirmed);
            snapshot.LedgerEvents.Select(item => item.Kind).Should().Contain(OrderEventKind.CancelConfirmed);
            snapshot.RiskDecisionFacts
                .Where(item => item.ClientOrderId == replaceOrder.ClientOrderId)
                .Select(item => item.EventKind)
                .Should().ContainInOrder(OrderEventKind.RiskAccepted, OrderEventKind.ReplaceRiskAccepted);
            snapshot.QualityFacts.Orders.Should().Be(2);
            snapshot.QualityFacts.FilledOrders.Should().Be(1);
            snapshot.QualityFacts.Cancels.Should().Be(1);
        });
    }

    [Fact]
    public async Task Rejected_submit_immediately_exposes_the_exact_committed_risk_decision()
    {
        await WithRuntime(async fixture =>
        {
            using var client = new PaperExecutionClient(fixture.Runtime.Service, fixture.Clock);
            var submit = fixture.Submit("risk-denied", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2)));

            var denied = await client.SubmitAsync(new ExecutionSubmitRequest(
                submit,
                fixture.RiskContext(maximumOrderQuantity: Q(1))));
            var snapshot = client.GetSnapshot();

            denied.IsSuccess.Should().BeFalse();
            snapshot.RiskDecisionFacts.Should().ContainSingle().Which.Should().Match<PaperRiskDecisionSnapshot>(item =>
                item.ClientOrderId == submit.ClientOrderId &&
                item.EventKind == OrderEventKind.RiskRejected &&
                !item.Observation.Decision.IsAllowed &&
                item.Observation.Decision.Code == RiskDecisionCode.MaximumOrderQuantityExceeded &&
                item.Observation.Evidence.Context.Limits.MaximumOrderQuantity == Q(1));
            snapshot.QualityFacts.Should().Match<PaperExecutionQualitySnapshot>(quality =>
                quality.Orders == 1 && quality.Rejects == 1 && quality.RejectRatePercent == 100d);
        });
    }

    [Fact]
    public async Task Kill_pauses_intake_cancels_working_orders_flattens_position_and_verifies_zero()
    {
        await WithRuntime(async fixture =>
        {
            var flattenFactory = new FlattenFactory(fixture);
            using var client = new PaperExecutionClient(
                fixture.Runtime.Service,
                fixture.Clock,
                flattenFactory);
            var filled = fixture.Submit("position", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2)));
            (await client.SubmitAsync(new ExecutionSubmitRequest(filled, fixture.RiskContext())))
                .IsSuccess.Should().BeTrue();
            fixture.Advance();
            var working = fixture.Submit(
                "working",
                new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(1), limitPrice: P(90)));
            (await client.SubmitAsync(new ExecutionSubmitRequest(working, fixture.RiskContext())))
                .IsSuccess.Should().BeTrue();
            fixture.Advance();

            var killed = await client.KillAsync();
            var snapshot = client.GetSnapshot();
            var blocked = await client.SubmitAsync(new ExecutionSubmitRequest(
                fixture.Submit("blocked", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1))),
                fixture.RiskContext()));

            killed.IsSuccess.Should().BeTrue(killed.Message);
            killed.Message.Should().Contain("flattened 1 position");
            snapshot.IntakePaused.Should().BeTrue();
            snapshot.AdmissionOpen.Should().BeFalse();
            snapshot.Economics.Positions.Should().ContainSingle().Which.Quantity.Should().Be(ScaledQuantity.Zero);
            snapshot.Orders.Single(item => item.ClientOrderId == working.ClientOrderId)
                .State.Should().Be(OrderLifecycleState.Cancelled);
            snapshot.Orders.Should().Contain(item =>
                item.SubmitCommand.Terms.ReduceOnly &&
                item.SubmitCommand.Terms.Side == OrderSide.Sell &&
                item.SubmitCommand.Terms.Quantity == Q(2) &&
                item.State == OrderLifecycleState.Filled);
            blocked.IsSuccess.Should().BeFalse();
            blocked.Message.Should().Contain("paused");
        });
    }

    [Fact]
    public async Task Kill_without_flatten_factory_cancels_orders_but_never_claims_a_nonflat_book_is_safe()
    {
        await WithRuntime(async fixture =>
        {
            using var client = new PaperExecutionClient(fixture.Runtime.Service, fixture.Clock);
            var filled = fixture.Submit("nonflat", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1)));
            (await client.SubmitAsync(new ExecutionSubmitRequest(filled, fixture.RiskContext())))
                .IsSuccess.Should().BeTrue();

            var killed = await client.KillAsync();
            var snapshot = client.GetSnapshot();

            killed.IsSuccess.Should().BeFalse();
            killed.Message.Should().Contain("no exact flatten-order factory");
            killed.Message.Should().Contain("intake remains paused");
            snapshot.IntakePaused.Should().BeTrue();
            snapshot.Economics.Positions.Single().Quantity.Should().Be(Q(1));
        });
    }

    [Fact]
    public async Task Lease_loss_preserves_read_only_resync_but_blocks_admission_claims()
    {
        await WithRuntime(async fixture =>
        {
            using var client = new PaperExecutionClient(fixture.Runtime.Service, fixture.Clock);
            var submit = fixture.Submit("before-loss", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1)));
            (await client.SubmitAsync(new ExecutionSubmitRequest(submit, fixture.RiskContext())))
                .IsSuccess.Should().BeTrue();
            fixture.Runtime.Ledger.Release(fixture.Runtime.LeaseGrant, fixture.Now).IsSuccess.Should().BeTrue();

            var refreshed = await client.RefreshAsync();
            var snapshot = client.GetSnapshot();

            refreshed.IsSuccess.Should().BeFalse();
            refreshed.Fault.Should().Be(ExecutionServiceFault.LeaseLost);
            snapshot.LeaseHeld.Should().BeFalse();
            snapshot.AdmissionOpen.Should().BeFalse();
            snapshot.Orders.Should().ContainSingle().Which.ClientOrderId.Should().Be(submit.ClientOrderId);
            snapshot.Economics.Positions.Single().Quantity.Should().Be(Q(1));
        });
    }

    [Fact]
    public async Task Reconciliation_case_blocks_submit_resolves_with_evidence_and_survives_restart()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "daxalgo-paper-reconciliation-client-tests",
            Guid.NewGuid().ToString("N"));
        var databasePath = Path.Combine(directory, "execution-ledger.db");
        Directory.CreateDirectory(directory);
        try
        {
            var caseId = new ReconciliationCaseId("client-case-restart");
            using (var fixture = new Fixture(databasePath))
            using (var client = new PaperExecutionClient(fixture.Runtime.Service, fixture.Clock))
            {
                fixture.OpenCase(caseId).Should().BeTrue();

                var refreshed = await client.RefreshAsync();
                var blockedSnapshot = client.GetSnapshot();
                var blockedSubmit = await client.SubmitAsync(new ExecutionSubmitRequest(
                    fixture.Submit("blocked-by-case", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1))),
                    fixture.RiskContext()));
                var resolved = await client.ResolveReconciliationCaseAsync(
                    caseId,
                    "desktop-operator",
                    "The immutable ledger and the reconstructed Paper venue now agree.");
                var resolvedSnapshot = client.GetSnapshot();

                refreshed.IsSuccess.Should().BeTrue(refreshed.Message);
                blockedSnapshot.ReconciliationAdmissionBlocked.Should().BeTrue();
                blockedSnapshot.AdmissionOpen.Should().BeFalse();
                blockedSnapshot.CaseFacts.Should().ContainSingle().Which.Status
                    .Should().Be(ReconciliationCaseStatus.Open);
                blockedSubmit.Fault.Should().Be(ExecutionServiceFault.ReconciliationFailed);
                resolved.IsSuccess.Should().BeTrue(resolved.Message);
                resolvedSnapshot.ReconciliationAdmissionBlocked.Should().BeFalse();
                resolvedSnapshot.AdmissionOpen.Should().BeTrue();
                resolvedSnapshot.CaseFacts.Should().ContainSingle().Which.Should().Match<ReconciliationCase>(item =>
                    item.CaseId == caseId &&
                    item.Status == ReconciliationCaseStatus.Resolved &&
                    item.ResolvedBy == "desktop-operator");
            }

            using (var reopened = new Fixture(databasePath, "reopened"))
            using (var client = new PaperExecutionClient(reopened.Runtime.Service, reopened.Clock))
            {
                (await client.RefreshAsync()).IsSuccess.Should().BeTrue();
                var snapshot = client.GetSnapshot();
                snapshot.CaseFacts.Should().ContainSingle().Which.Should().Match<ReconciliationCase>(item =>
                    item.CaseId == caseId &&
                    item.Status == ReconciliationCaseStatus.Resolved &&
                    item.ResolutionEvidence == "The immutable ledger and the reconstructed Paper venue now agree.");
                snapshot.ReconciliationAdmissionBlocked.Should().BeFalse();
                snapshot.AdmissionOpen.Should().BeTrue();
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task WithRuntime(Func<Fixture, Task> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "daxalgo-paper-execution-client-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            using var fixture = new Fixture(Path.Combine(directory, "execution-ledger.db"));
            await action(fixture);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ScaledQuantity Q(long value) => ScaledQuantity.FromWhole(value);
    private static ScaledPrice P(long value) => new(value, 0);
    private static ScaledMoney M(long value) => new(value, 0);

    private sealed class Fixture : IDisposable
    {
        private int _sequence;

        public Fixture(string databasePath, string runtimeSuffix = "primary")
        {
            Clock = new MutableClock(new DateTime(2026, 8, 24, 3, 0, 0, DateTimeKind.Utc));
            Runtime = PaperExecutionServiceRuntime.Create(
                databasePath,
                Resource,
                Clock,
                new ExecutionLeaseId($"desktop-client-lease-{runtimeSuffix}"),
                new RuntimeInstanceId($"desktop-client-runtime-{runtimeSuffix}"));
            Runtime.Venue.OnMarket(new PaperMarketSnapshot(Instrument, P(99), P(100), Q(100), Now));
        }

        public MutableClock Clock { get; }
        public PaperExecutionServiceRuntime Runtime { get; }
        public DateTimeOffset Now => new(Clock.UtcNow);

        public void Advance() => Clock.UtcNow = Clock.UtcNow.AddSeconds(1);

        public bool OpenCase(ReconciliationCaseId caseId) => Runtime.Ledger.TryAppend(
            new ReconciliationCase(
                caseId,
                Resource,
                ReconciliationSubjectKind.Position,
                $"instrument:{Instrument.Value}",
                null,
                ReconciliationCaseKind.PositionMismatch,
                ReconciliationCaseStatus.Open,
                "ledger quantity=2",
                "venue quantity=1",
                Now));

        public SubmitOrderCommand Submit(string suffix, OrderTerms terms)
        {
            var sequence = ++_sequence;
            var metadata = new ExecutionCommandMetadata(
                new CommandId($"desktop-command-{suffix}-{sequence}"),
                new CorrelationId("desktop-correlation"),
                new CausationId($"desktop-cause-{suffix}-{sequence}"),
                Resource.TradingAccountId,
                new StrategyId("desktop-strategy"),
                new StrategyVersion("1.0.0"),
                Resource.VenueId,
                Instrument,
                Resource.Environment,
                Now,
                0);
            var signed = terms.Side == OrderSide.Buy
                ? terms.Quantity
                : new ScaledQuantity(-terms.Quantity.Coefficient, terms.Quantity.Scale);
            var mapping = new CanonicalInstructionMappingContext(
                new IntentId($"desktop-intent-{suffix}-{sequence}"),
                null,
                new LegId($"desktop-leg-{suffix}-{sequence}"),
                Runtime.LeaseGrant.Claim.LeaseId,
                Runtime.LeaseGrant.Claim.FencingToken,
                TradeIntentQuantityMode.Delta,
                signed,
                ScaledQuantity.Zero,
                null,
                null,
                ScaledMoney.Zero,
                sequence,
                "desktop-policy",
                terms.LimitPrice,
                terms.StopPrice);
            CanonicalOrderInstructionMapper.TryCreate(
                metadata,
                new ClientOrderId($"desktop-client-{suffix}-{sequence}"),
                terms,
                mapping,
                out var instruction).Should().Be(OrderDomainFault.None);
            return new SubmitOrderCommand(
                metadata,
                new OrderId($"desktop-order-{suffix}-{sequence}"),
                new ClientOrderId($"desktop-client-{suffix}-{sequence}"),
                terms,
                instruction!);
        }

        public RiskEvaluationContext RiskContext(
            ScaledQuantity? currentPosition = null,
            bool reduceOnly = false,
            ScaledQuantity? maximumOrderQuantity = null) =>
            new(
                new RiskLimits(maximumOrderQuantity ?? Q(1_000), Q(1_000), M(10_000_000), ScaledMoney.Zero,
                    M(1_000_000), M(1_000_000), 1_000, TimeSpan.FromMinutes(1)),
                RiskControlMode.Active,
                false,
                currentPosition ?? ScaledQuantity.Zero,
                ScaledQuantity.Zero,
                ScaledQuantity.Zero,
                ScaledMoney.Zero,
                ScaledQuantity.Zero,
                ScaledMoney.Zero,
                ScaledQuantity.Zero,
                M(10_000_000),
                ScaledMoney.Zero,
                M(1_000_000),
                M(1_000_000),
                P(100),
                0,
                Now);

        public void Dispose() => Runtime.Dispose();
    }

    private sealed class FlattenFactory(Fixture fixture) : IPaperExecutionFlattenOrderFactory
    {
        private int _sequence;

        public bool TryCreateFlattenOrder(
            ReconciliationPositionSnapshot position,
            ExecutionLeaseGrant leaseGrant,
            DateTimeOffset createdAtUtc,
            out ExecutionSubmitRequest? request,
            out string? reason)
        {
            request = null;
            reason = null;
            if (position.InstrumentId != Instrument || position.Quantity.Coefficient == 0)
            {
                reason = "The configured Paper instrument does not match the open position.";
                return false;
            }

            var sequence = ++_sequence;
            var side = position.Quantity.Coefficient > 0 ? OrderSide.Sell : OrderSide.Buy;
            var absolute = new ScaledQuantity(Math.Abs(position.Quantity.Coefficient), position.Quantity.Scale);
            var signedDelta = new ScaledQuantity(-position.Quantity.Coefficient, position.Quantity.Scale);
            var terms = new OrderTerms(side, OrderType.Market, absolute, reduceOnly: true);
            var metadata = new ExecutionCommandMetadata(
                new CommandId($"kill-flatten-command-{sequence}"),
                new CorrelationId("kill-flatten-correlation"),
                new CausationId($"kill-flatten-cause-{sequence}"),
                Resource.TradingAccountId,
                new StrategyId("system-kill"),
                new StrategyVersion("1.0.0"),
                Resource.VenueId,
                Instrument,
                Resource.Environment,
                createdAtUtc,
                0);
            var mapping = new CanonicalInstructionMappingContext(
                new IntentId($"kill-flatten-intent-{sequence}"),
                null,
                new LegId($"kill-flatten-leg-{sequence}"),
                leaseGrant.Claim.LeaseId,
                leaseGrant.Claim.FencingToken,
                TradeIntentQuantityMode.Delta,
                signedDelta,
                position.Quantity,
                null,
                null,
                ScaledMoney.Zero,
                sequence,
                "system-kill-policy");
            var clientOrderId = new ClientOrderId($"kill-flatten-client-{sequence}");
            var fault = CanonicalOrderInstructionMapper.TryCreate(
                metadata,
                clientOrderId,
                terms,
                mapping,
                out var instruction);
            if (fault != OrderDomainFault.None || instruction is null)
            {
                reason = $"Canonical flatten instruction failed: {fault}.";
                return false;
            }

            request = new ExecutionSubmitRequest(
                new SubmitOrderCommand(
                    metadata,
                    new OrderId($"kill-flatten-order-{sequence}"),
                    clientOrderId,
                    terms,
                    instruction),
                fixture.RiskContext(position.Quantity, reduceOnly: true));
            return true;
        }
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
