using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Infrastructure.Execution;

namespace TradingTerminal.Sandbox.Runtime.Tests;

public sealed class DurablePaperExecutionBookTargetIntakeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 2, 30, 0, TimeSpan.Zero);
    private static readonly InstrumentId Instrument = new(4242);
    private static readonly ExecutionResource Resource = new(
        new VenueId("paper"),
        new TradingAccountId("durable-strategy-account"),
        ExecutionEnvironment.SimulatedPaper);

    [Fact]
    public async Task StrategyTargetFillPersistsExactPositionAndCashAcrossLedgerReopen()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"daxalgo-strategy-ledger-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "paper.db");
        Directory.CreateDirectory(directory);
        try
        {
            using (var store = new SqliteOrderEventStore(path, Now))
            {
                var grant = store.Acquire(
                    Resource,
                    new ExecutionLeaseId("durable-strategy-lease"),
                    new RuntimeInstanceId("durable-strategy-runtime"),
                    Now,
                    Now.AddHours(1));
                Assert.True(grant.IsSuccess, grant.Reason);
                var venue = new DeterministicPaperVenue();
                venue.OnMarket(new PaperMarketSnapshot(
                    Instrument,
                    new ScaledPrice(99, 0),
                    new ScaledPrice(100, 0),
                    ScaledQuantity.FromWhole(10),
                    ScaledQuantity.FromWhole(10),
                    Now));
                var clock = new FixedClock(Now.UtcDateTime);
                var oms = new OrderManagementService(store, venue, store, clock);
                using var intake = new PaperExecutionBookTargetIntake(
                    Options(grant.Grant!.Value.Claim),
                    oms,
                    store,
                    clock,
                    ResolvePrice);

                var result = await intake.SubmitTargetAsync(
                    "durable-book",
                    new TradeIntent(
                        Instrument,
                        TradeIntentQuantityMode.TargetPosition,
                        ScaledQuantity.FromWhole(2),
                        null,
                        null,
                        ScaledMoney.Zero,
                        "durable-strategy",
                        0,
                        SandboxExecutionReplicator.DefaultPolicyVersion));

                Assert.True(result.IsSuccess, result.Message);
                Assert.Equal(
                    ScaledQuantity.FromWhole(2),
                    Assert.Single(store.ReadPositionProjections(Resource)).Quantity);
                Assert.Equal(
                    new ScaledMoney(-200, 0),
                    Assert.Single(store.ReadCashProjections(Resource)).Total);
            }

            using var reopened = new SqliteOrderEventStore(path, Now.AddMinutes(1));
            Assert.True(reopened.Integrity.IsValid, reopened.Integrity.Detail);
            Assert.Equal(
                ScaledQuantity.FromWhole(2),
                Assert.Single(reopened.ReadPositionProjections(Resource)).Quantity);
            Assert.Equal(
                new ScaledMoney(-200, 0),
                Assert.Single(reopened.ReadCashProjections(Resource)).Total);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static PaperExecutionBookTargetOptions Options(ExecutionLeaseClaim claim) =>
        new(
            "durable-book",
            new StrategyId("durable-strategy"),
            new StrategyVersion("1.0.0"),
            Resource,
            claim,
            new RiskLimits(
                ScaledQuantity.FromWhole(100),
                ScaledQuantity.FromWhole(1_000),
                new ScaledMoney(10_000_000, 0),
                ScaledMoney.Zero,
                new ScaledMoney(100_000, 0),
                new ScaledMoney(100_000, 0),
                1_000,
                TimeSpan.FromMinutes(1)),
            new ScaledMoney(10_000_000, 0),
            ScaledMoney.Zero,
            new ScaledMoney(1_000_000, 0),
            new ScaledMoney(1_000_000, 0));

    private static bool ResolvePrice(
        InstrumentId instrumentId,
        out ScaledPrice price,
        out DateTimeOffset observedAtUtc)
    {
        price = new ScaledPrice(9_950, 2);
        observedAtUtc = Now;
        return instrumentId == Instrument;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }
}
