using FluentAssertions;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.Infrastructure.Execution;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class ExecutionLeaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 23, 12, 0, 0, TimeSpan.Zero);
    private static readonly ExecutionResource Resource = new(
        new VenueId("paper"),
        new TradingAccountId("lease-account"),
        ExecutionEnvironment.SimulatedPaper);

    [Fact]
    public void Durable_lease_renews_reopens_releases_and_advances_fence()
    {
        WithDatabase(path =>
        {
            ExecutionLeaseGrant renewed;
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var first = store.Acquire(
                    Resource,
                    new ExecutionLeaseId("lease-one"),
                    new RuntimeInstanceId("owner-one"),
                    Now,
                    Now.AddMinutes(5));
                var blocked = store.Acquire(
                    Resource,
                    new ExecutionLeaseId("lease-two-too-early"),
                    new RuntimeInstanceId("owner-two"),
                    Now.AddMinutes(1),
                    Now.AddMinutes(6));
                var renewal = store.Renew(first.Grant!.Value, Now.AddMinutes(2), Now.AddMinutes(10));

                first.IsSuccess.Should().BeTrue();
                first.Grant.Value.Claim.FencingToken.Should().Be(new FencingToken(1));
                blocked.Fault.Should().Be(ExecutionLeaseFault.HeldByAnotherOwner);
                renewal.IsSuccess.Should().BeTrue();
                renewal.Grant!.Value.Claim.FencingToken.Should().Be(new FencingToken(1));
                renewed = renewal.Grant.Value;
            }

            using (var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(3)))
            {
                reopened.Validate(renewed.Claim, Now.AddMinutes(3)).IsSuccess.Should().BeTrue();
                reopened.Release(renewed, Now.AddMinutes(4)).IsSuccess.Should().BeTrue();
                reopened.Validate(renewed.Claim, Now.AddMinutes(4)).Fault.Should().Be(ExecutionLeaseFault.Released);
                var second = reopened.Acquire(
                    Resource,
                    new ExecutionLeaseId("lease-two"),
                    new RuntimeInstanceId("owner-two"),
                    Now.AddMinutes(4),
                    Now.AddMinutes(9));
                second.IsSuccess.Should().BeTrue();
                second.Grant!.Value.Claim.FencingToken.Should().Be(new FencingToken(2));
                reopened.Validate(renewed.Claim, Now.AddMinutes(4)).Fault.Should().Be(ExecutionLeaseFault.NotCurrent);
            }
        });
    }

    [Fact]
    public void Expired_owner_is_replaced_but_its_claim_remains_stale()
    {
        var store = new InMemoryExecutionLeaseStore();
        var first = store.Acquire(
            Resource,
            new ExecutionLeaseId("expired-lease"),
            new RuntimeInstanceId("expired-owner"),
            Now,
            Now.AddSeconds(10));
        var second = store.Acquire(
            Resource,
            new ExecutionLeaseId("replacement-lease"),
            new RuntimeInstanceId("replacement-owner"),
            Now.AddSeconds(10),
            Now.AddMinutes(1));

        first.IsSuccess.Should().BeTrue();
        second.IsSuccess.Should().BeTrue();
        second.Grant!.Value.Claim.FencingToken.Should().Be(new FencingToken(2));
        store.Validate(first.Grant!.Value.Claim, Now.AddSeconds(11)).Fault
            .Should().Be(ExecutionLeaseFault.NotCurrent);
    }

    [Fact]
    public void Stale_instruction_cannot_submit_cancel_or_replace_at_dispatch_boundary()
    {
        var clock = new MutableClock(Now.UtcDateTime);
        var leases = new InMemoryExecutionLeaseStore();
        var first = leases.Acquire(
            Resource,
            new ExecutionLeaseId("oms-lease-one"),
            new RuntimeInstanceId("oms-owner-one"),
            Now,
            Now.AddHours(1)).Grant!.Value;
        var dispatcher = new CountingWorkingDispatcher(Now);
        var eventStore = new InMemoryOrderEventStore();
        var oms = new OrderManagementService(eventStore, dispatcher, leases, clock);
        var cancelOrder = Submit("cancel", first.Claim);
        var replaceOrder = Submit("replace", first.Claim);

        var cancelWorking = oms.Submit(cancelOrder, RiskContext(), Context("cancel-submit"));
        var replaceWorking = oms.Submit(replaceOrder, RiskContext(), Context("replace-submit"));
        cancelWorking.Projection!.State.Should().Be(OrderLifecycleState.Working);
        replaceWorking.Projection!.State.Should().Be(OrderLifecycleState.Working);
        dispatcher.SubmitCalls.Should().Be(2);

        leases.Release(first, Now.AddMinutes(1)).IsSuccess.Should().BeTrue();
        leases.Acquire(
            Resource,
            new ExecutionLeaseId("oms-lease-two"),
            new RuntimeInstanceId("oms-owner-two"),
            Now.AddMinutes(1),
            Now.AddHours(1)).IsSuccess.Should().BeTrue();
        clock.UtcNow = Now.AddMinutes(1).UtcDateTime;

        var cancel = oms.Cancel(
            new CancelOrderCommand(
                Metadata("cancel-command", cancelWorking.Projection.LastSequence),
                cancelOrder.OrderId),
            Context("cancel-command"));
        var replace = oms.Replace(
            new ReplaceOrderCommand(
                Metadata("replace-command", replaceWorking.Projection.LastSequence),
                replaceOrder.OrderId,
                new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(1), limitPrice: P(95))),
            RiskContext(),
            Context("replace-command"));
        var staleSubmit = oms.Submit(
            Submit("new-stale", first.Claim),
            RiskContext(),
            Context("new-stale"));

        cancel.Fault.Should().Be(OmsCommandFault.ExecutionLeaseRejected);
        replace.Fault.Should().Be(OmsCommandFault.ExecutionLeaseRejected);
        staleSubmit.Fault.Should().Be(OmsCommandFault.ExecutionLeaseRejected);
        dispatcher.CancelCalls.Should().Be(0);
        dispatcher.ReplaceCalls.Should().Be(0);
        dispatcher.SubmitCalls.Should().Be(2);
        eventStore.Read(cancelOrder.ClientOrderId).Select(e => e.Kind).Should().NotContain(OrderEventKind.CancelRequested);
        eventStore.Read(replaceOrder.ClientOrderId).Select(e => e.Kind).Should().NotContain(OrderEventKind.ReplaceRequested);
        eventStore.Read(new ClientOrderId("client-new-stale")).Last().Kind.Should().Be(OrderEventKind.Armed);
    }

    private static SubmitOrderCommand Submit(string suffix, ExecutionLeaseClaim claim)
    {
        var metadata = Metadata($"submit-{suffix}", 0);
        var terms = new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(1), limitPrice: P(90));
        var clientOrderId = new ClientOrderId($"client-{suffix}");
        var mapping = new CanonicalInstructionMappingContext(
            new IntentId($"intent-{suffix}"),
            null,
            new LegId($"leg-{suffix}"),
            claim.LeaseId,
            claim.FencingToken,
            TradeIntentQuantityMode.Delta,
            Q(1),
            ScaledQuantity.Zero,
            null,
            null,
            ScaledMoney.Zero,
            1,
            "lease-test-policy");
        CanonicalOrderInstructionMapper.TryCreate(metadata, clientOrderId, terms, mapping, out var instruction)
            .Should().Be(OrderDomainFault.None);
        return new SubmitOrderCommand(metadata, new OrderId($"order-{suffix}"), clientOrderId, terms, instruction!);
    }

    private static ExecutionCommandMetadata Metadata(string suffix, long expectedSequence) => new(
        new CommandId($"command-{suffix}"),
        new CorrelationId("lease-correlation"),
        new CausationId($"cause-{suffix}"),
        Resource.TradingAccountId,
        new StrategyId("lease-strategy"),
        new StrategyVersion("1"),
        Resource.VenueId,
        new InstrumentId(717),
        Resource.Environment,
        new DateTimeOffset(Now.UtcDateTime),
        expectedSequence);

    private static RiskEvaluationContext RiskContext() => new(
        new RiskLimits(Q(100), Q(100), M(100_000), ScaledMoney.Zero, M(10_000), M(10_000), 100, TimeSpan.FromMinutes(1)),
        RiskControlMode.Active,
        false,
        ScaledQuantity.Zero,
        ScaledQuantity.Zero,
        ScaledQuantity.Zero,
        ScaledMoney.Zero,
        ScaledQuantity.Zero,
        ScaledMoney.Zero,
        ScaledQuantity.Zero,
        M(100_000),
        ScaledMoney.Zero,
        M(100_000),
        M(100_000),
        P(100),
        0,
        Now);

    private static OrderCommandContext Context(string suffix) => new(
        new CausationId($"cause-{suffix}"),
        new DeduplicationKey($"dedupe-{suffix}"));
    private static ScaledQuantity Q(long value) => new(value, 0);
    private static ScaledPrice P(long value) => new(value, 0);
    private static ScaledMoney M(long value) => new(value, 0);

    private static void WithDatabase(Action<string> action)
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-lease-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try { action(Path.Combine(directory, "execution-ledger.db")); }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class CountingWorkingDispatcher(DateTimeOffset now) : IPaperExecutionDispatcher
    {
        private readonly Queue<PaperVenueEvent> _events = [];
        private long _sequence;
        public int SubmitCalls { get; private set; }
        public int CancelCalls { get; private set; }
        public int ReplaceCalls { get; private set; }

        public ExecutionDispatchResult Submit(SubmitOrderCommand command, OmsOrderProjection projection)
        {
            SubmitCalls++;
            var brokerId = new BrokerOrderId($"LEASE-PAPER-{++_sequence}");
            _events.Enqueue(new PaperVenueEvent(
                new ExecutionEventId($"lease-ack-{_sequence}"),
                PaperVenueEventKind.Acknowledged,
                command.ClientOrderId,
                now,
                command.CanonicalInstruction.Identity.CausationId,
                brokerId));
            return ExecutionDispatchResult.Dispatched(new ExecutionDispatchReceipt(
                new DispatchAttemptId($"lease-dispatch-{_sequence}"),
                now,
                brokerId));
        }

        public ExecutionDispatchResult Cancel(CancelOrderCommand command, OmsOrderProjection projection)
        {
            CancelCalls++;
            return ExecutionDispatchResult.Unknown("should not be called");
        }

        public ExecutionDispatchResult Replace(ReplaceOrderCommand command, OmsOrderProjection projection)
        {
            ReplaceCalls++;
            return ExecutionDispatchResult.Unknown("should not be called");
        }

        public IReadOnlyList<PaperVenueEvent> DrainEvents()
        {
            var result = _events.ToArray();
            _events.Clear();
            return result;
        }
    }
}
