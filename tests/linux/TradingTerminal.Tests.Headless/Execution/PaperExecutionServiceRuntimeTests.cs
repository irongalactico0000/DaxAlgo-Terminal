using FluentAssertions;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.Infrastructure.Execution;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class PaperExecutionServiceRuntimeTests
{
    private static readonly ExecutionResource Resource = new(
        new VenueId("paper"),
        new TradingAccountId("durable-service-account"),
        ExecutionEnvironment.SimulatedPaper);
    private static readonly InstrumentId Instrument = new(821);

    [Fact]
    public void Runtime_reopens_the_same_service_ledger_and_resyncs_recovered_order_economics()
    {
        WithDatabase(path =>
        {
            var clock = new MutableClock(new DateTime(2026, 8, 24, 2, 0, 0, DateTimeKind.Utc));
            SubmitOrderCommand command;
            long cursor;
            using (var first = PaperExecutionServiceRuntime.Create(
                       path,
                       Resource,
                       clock,
                       new ExecutionLeaseId("durable-service-lease-1"),
                       new RuntimeInstanceId("durable-service-owner-1")))
            {
                first.Venue.OnMarket(new PaperMarketSnapshot(Instrument, P(99), P(100), Q(5), Now(clock)));
                command = Submit(first.LeaseGrant.Claim, Now(clock));
                var submitted = first.Service.Handle(Request(
                    first,
                    "durable-submit",
                    ExecutionServiceRequestKind.Submit,
                    submit: new ExecutionSubmitRequest(command, RiskContext(Now(clock)))));
                submitted.Response.IsSuccess.Should().BeTrue(submitted.Response.Reason);
                submitted.Response.State.Should().Be(OrderLifecycleState.Filled);
                cursor = submitted.Response.LastOutboxSequence;
                first.Ledger.ReadPositionProjections(Resource).Should().ContainSingle()
                    .Which.Quantity.Should().Be(Q(2));
                first.Ledger.ReadCashProjections(Resource).Should().ContainSingle()
                    .Which.Total.Should().Be(M(-200));
            }

            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            using var reopened = PaperExecutionServiceRuntime.Create(
                path,
                Resource,
                clock,
                new ExecutionLeaseId("durable-service-lease-2"),
                new RuntimeInstanceId("durable-service-owner-2"));
            var status = reopened.Service.Handle(ReadRequest(reopened, "reopened-status", ExecutionServiceRequestKind.Status));
            var resync = reopened.Service.Handle(ReadRequest(reopened, "reopened-resync", ExecutionServiceRequestKind.Resync));
            var tail = reopened.Service.Handle(ReadRequest(
                reopened,
                "reopened-tail",
                ExecutionServiceRequestKind.Resync,
                cursor));

            status.Response.IsSuccess.Should().BeTrue(status.Response.Reason);
            reopened.Oms.Query(command.ClientOrderId)!.State.Should().Be(OrderLifecycleState.Filled);
            reopened.Ledger.VerifyIntegrity().IsValid.Should().BeTrue();
            reopened.Ledger.ReadPositionProjections(Resource).Single().Quantity.Should().Be(Q(2));
            reopened.Ledger.ReadCashProjections(Resource).Single().Total.Should().Be(M(-200));
            resync.Events.Should().Contain(item =>
                item.Event.AggregateId == command.ClientOrderId &&
                item.Event.Kind == OrderEventKind.FillReceived);
            tail.Events.Should().BeEmpty();
        });
    }

    [Fact]
    public void Runtime_renews_and_releases_the_same_fencing_generation()
    {
        WithDatabase(path =>
        {
            var clock = new MutableClock(new DateTime(2026, 8, 24, 2, 0, 0, DateTimeKind.Utc));
            FencingToken firstToken;
            using (var runtime = PaperExecutionServiceRuntime.Create(
                       path,
                       Resource,
                       clock,
                       new ExecutionLeaseId("renew-service-lease-1"),
                       new RuntimeInstanceId("renew-service-owner-1"),
                       TimeSpan.FromMinutes(1)))
            {
                firstToken = runtime.LeaseGrant.Claim.FencingToken;
                clock.UtcNow = clock.UtcNow.AddSeconds(30);
                var renewed = runtime.RenewLease(TimeSpan.FromMinutes(2));
                renewed.IsSuccess.Should().BeTrue(renewed.Reason);
                runtime.LeaseGrant.Claim.FencingToken.Should().Be(firstToken);
                runtime.LeaseGrant.ExpiresAtUtc.Should().Be(Now(clock).AddMinutes(2));
            }

            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            using var successor = PaperExecutionServiceRuntime.Create(
                path,
                Resource,
                clock,
                new ExecutionLeaseId("renew-service-lease-2"),
                new RuntimeInstanceId("renew-service-owner-2"));
            successor.LeaseGrant.Claim.FencingToken.Value.Should().BeGreaterThan(firstToken.Value);
        });
    }

    private static ExecutionServiceRequest Request(
        PaperExecutionServiceRuntime runtime,
        string requestId,
        ExecutionServiceRequestKind kind,
        ExecutionSubmitRequest? submit = null) =>
        new(
            ExecutionServiceProtocol.CurrentVersion,
            requestId,
            kind,
            Resource,
            runtime.LeaseGrant.Claim.LeaseId,
            runtime.LeaseGrant.Claim.FencingToken,
            Submit: submit);

    private static ExecutionServiceRequest ReadRequest(
        PaperExecutionServiceRuntime runtime,
        string requestId,
        ExecutionServiceRequestKind kind,
        long afterOutboxSequence = 0) =>
        new(
            ExecutionServiceProtocol.CurrentVersion,
            requestId,
            kind,
            Resource,
            runtime.LeaseGrant.Claim.LeaseId,
            runtime.LeaseGrant.Claim.FencingToken,
            afterOutboxSequence);

    private static SubmitOrderCommand Submit(ExecutionLeaseClaim claim, DateTimeOffset now)
    {
        var metadata = new ExecutionCommandMetadata(
            new CommandId("durable-service-command"),
            new CorrelationId("durable-service-correlation"),
            new CausationId("durable-service-cause"),
            Resource.TradingAccountId,
            new StrategyId("durable-service-strategy"),
            new StrategyVersion("1.0.0"),
            Resource.VenueId,
            Instrument,
            Resource.Environment,
            now,
            0);
        var terms = new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2));
        var mapping = new CanonicalInstructionMappingContext(
            new IntentId("durable-service-intent"),
            null,
            new LegId("durable-service-leg"),
            claim.LeaseId,
            claim.FencingToken,
            TradeIntentQuantityMode.Delta,
            Q(2),
            ScaledQuantity.Zero,
            null,
            null,
            ScaledMoney.Zero,
            1,
            "durable-service-policy");
        CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            new ClientOrderId("durable-service-client-order"),
            terms,
            mapping,
            out var instruction).Should().Be(OrderDomainFault.None);
        return new SubmitOrderCommand(
            metadata,
            new OrderId("durable-service-order"),
            new ClientOrderId("durable-service-client-order"),
            terms,
            instruction!);
    }

    private static RiskEvaluationContext RiskContext(DateTimeOffset now) =>
        new(
            new RiskLimits(Q(100), Q(100), M(1_000_000), ScaledMoney.Zero,
                M(100_000), M(100_000), 100, TimeSpan.FromMinutes(1)),
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
            now);

    private static DateTimeOffset Now(IClock clock) => new(clock.UtcNow);
    private static ScaledQuantity Q(long value) => ScaledQuantity.FromWhole(value);
    private static ScaledPrice P(long value) => new(value, 0);
    private static ScaledMoney M(long value) => new(value, 0);

    private static void WithDatabase(Action<string> action)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "daxalgo-paper-execution-service-tests",
            Guid.NewGuid().ToString("N"));
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

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
