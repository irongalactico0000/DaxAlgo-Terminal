using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;

namespace TradingTerminal.Sandbox.Runtime.Tests;

public sealed class PaperExecutionBookTargetIntakeTests
{
    private static readonly InstrumentId Instrument = new(42);

    [Fact]
    public async Task CommittedSnapshotTraversesReplicatorAndOmsIntoPaperFill()
    {
        using var fixture = new Fixture();
        var source = new ManualPortfolioSource();
        await using var replicator = new SandboxExecutionReplicator(
            source,
            fixture.Intake,
            new SandboxExecutionReplicationOptions(Fixture.BookId, Fixture.Strategy.Value));
        var completion = new TaskCompletionSource<SandboxExecutionReplicationOutcome>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        replicator.SubmissionCompleted += outcome => completion.TrySetResult(outcome);

        source.Publish(Snapshot(2d));
        var outcome = await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(outcome.Result.IsSuccess, outcome.Result.Message);
        Assert.Equal(2m, fixture.Position());
        var projection = Assert.Single(fixture.Projections());
        Assert.Equal(OrderLifecycleState.Filled, projection.State);
        Assert.Equal(TradeIntentQuantityMode.TargetPosition, projection.Instruction.TradeIntent.QuantityMode);
        Assert.Equal(Fixture.Strategy.Value, projection.Instruction.TradeIntent.StrategyId);
    }

    [Fact]
    public async Task WorkingReservationCoalescesSameTargetAndCancelReplansConflictingTarget()
    {
        using var fixture = new Fixture();
        var pending = Target(2, entryLimit: new ScaledPrice(90, 0));

        var first = await fixture.Intake.SubmitTargetAsync(Fixture.BookId, pending);
        var duplicate = await fixture.Intake.SubmitTargetAsync(Fixture.BookId, pending);
        var conflict = await fixture.Intake.SubmitTargetAsync(Fixture.BookId, Target(3));

        Assert.True(first.IsSuccess, first.Message);
        Assert.True(duplicate.IsSuccess, duplicate.Message);
        Assert.Contains("already has a working order", duplicate.Message, StringComparison.Ordinal);
        Assert.True(conflict.IsSuccess, conflict.Message);
        Assert.Contains("cancelled the conflicting order", conflict.Message, StringComparison.Ordinal);
        var projections = fixture.Projections();
        Assert.Equal(2, projections.Length);
        Assert.Equal(OrderLifecycleState.Cancelled, Assert.Single(projections, item => item.Terms.Type == OrderType.Limit).State);
        Assert.Equal(OrderLifecycleState.Filled, Assert.Single(projections, item => item.Terms.Type == OrderType.Market).State);
        Assert.Equal(3m, fixture.Position());
    }

    [Fact]
    public async Task FlatRetargetCancelsRestingOrderWithoutCreatingAnotherOrder()
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Intake.SubmitTargetAsync(
            Fixture.BookId,
            Target(2, entryLimit: new ScaledPrice(90, 0)))).IsSuccess);

        var flatten = await fixture.Intake.SubmitTargetAsync(Fixture.BookId, Target(0));

        Assert.True(flatten.IsSuccess, flatten.Message);
        Assert.Contains("cancelled", flatten.Message, StringComparison.OrdinalIgnoreCase);
        var projection = Assert.Single(fixture.Projections());
        Assert.Equal(OrderLifecycleState.Cancelled, projection.State);
        Assert.Equal(0m, fixture.Position());
    }

    [Fact]
    public async Task ReversalUsesTargetMinusCurrentAndConvergesWithoutRounding()
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Intake.SubmitTargetAsync(Fixture.BookId, Target(2))).IsSuccess);
        fixture.Advance();

        var reversal = await fixture.Intake.SubmitTargetAsync(Fixture.BookId, Target(-3));

        Assert.True(reversal.IsSuccess, reversal.Message);
        Assert.Equal(-3m, fixture.Position());
        var second = fixture.Projections().OrderBy(item => item.SubmitCommand.Metadata.CreatedAtUtc).Last();
        Assert.Equal(OrderSide.Sell, second.Terms.Side);
        Assert.Equal(5m, ExecutionNumericBoundary.ToDecimal(second.Terms.Quantity));
        Assert.False(second.Terms.ReduceOnly);
    }

    [Fact]
    public async Task NonCrossingFlattenIsExplicitlyReduceOnly()
    {
        using var fixture = new Fixture();
        Assert.True((await fixture.Intake.SubmitTargetAsync(Fixture.BookId, Target(2))).IsSuccess);
        fixture.Advance();

        var flatten = await fixture.Intake.SubmitTargetAsync(Fixture.BookId, Target(0));

        Assert.True(flatten.IsSuccess, flatten.Message);
        Assert.Equal(0m, fixture.Position());
        var second = fixture.Projections().OrderBy(item => item.SubmitCommand.Metadata.CreatedAtUtc).Last();
        Assert.True(second.Terms.ReduceOnly);
        Assert.Equal(OrderSide.Sell, second.Terms.Side);
        Assert.Equal(2m, ExecutionNumericBoundary.ToDecimal(second.Terms.Quantity));
    }

    [Fact]
    public async Task WrongBookStrategyAndStalePriceFailBeforeCreatingOrder()
    {
        using var fixture = new Fixture();

        var wrongBook = await fixture.Intake.SubmitTargetAsync("other-book", Target(2));
        var wrongStrategy = await fixture.Intake.SubmitTargetAsync(
            Fixture.BookId,
            Target(2) with { StrategyId = "other-strategy" });
        fixture.Advance(TimeSpan.FromMinutes(1));
        var stale = await fixture.Intake.SubmitTargetAsync(Fixture.BookId, Target(2));

        Assert.False(wrongBook.IsSuccess);
        Assert.False(wrongStrategy.IsSuccess);
        Assert.False(stale.IsSuccess);
        Assert.Empty(fixture.Projections());
    }

    [Fact]
    public async Task RiskRejectionIsLedgeredAndNeverReachesPaperVenue()
    {
        using var fixture = new Fixture(maximumOrderUnits: 2);

        var result = await fixture.Intake.SubmitTargetAsync(Fixture.BookId, Target(3));

        Assert.False(result.IsSuccess);
        var projection = Assert.Single(fixture.Projections());
        Assert.Equal(OrderLifecycleState.Rejected, projection.State);
        Assert.Equal(RiskDecisionCode.MaximumOrderQuantityExceeded, projection.RiskObservation!.Decision.Code);
        Assert.Empty(fixture.Venue.CaptureReconciliationSnapshot(Fixture.Resource, fixture.Now).Orders);
    }

    private static TradeIntent Target(
        long units,
        ScaledPrice? entryLimit = null,
        ScaledPrice? entryStop = null) =>
        new(
            Instrument,
            TradeIntentQuantityMode.TargetPosition,
            ScaledQuantity.FromWhole(units),
            ProtectiveStopPrice: null,
            ProfitTargetPrice: null,
            ScaledMoney.Zero,
            Fixture.Strategy.Value,
            StrategyNoteId: 0,
            SandboxExecutionReplicator.DefaultPolicyVersion,
            entryLimit,
            entryStop);

    private static SandboxPortfolioSnapshot Snapshot(double units) =>
        new(
            Instrument,
            units,
            Math.Abs(units),
            100d,
            1,
            100_000d,
            0d,
            0d,
            0d,
            100_000d,
            0d,
            0,
            0,
            0,
            0,
            0,
            false);

    private sealed class ManualPortfolioSource : IModelPortfolioSource
    {
        public IModelPortfolio? CurrentSnapshot { get; private set; }
        public event Action<IModelPortfolio>? SnapshotChanged;

        public void Publish(IModelPortfolio snapshot)
        {
            CurrentSnapshot = snapshot;
            SnapshotChanged?.Invoke(snapshot);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public const string BookId = "paper-book-1";
        public static readonly StrategyId Strategy = new("sandbox-42");
        public static readonly ExecutionResource Resource = new(
            new VenueId("paper"),
            new TradingAccountId("paper-account"),
            ExecutionEnvironment.SimulatedPaper);

        private readonly MutableClock _clock = new(new DateTime(2026, 8, 24, 2, 0, 0, DateTimeKind.Utc));
        private readonly InMemoryOrderEventStore _store = new();
        private readonly InMemoryExecutionLeaseStore _leases = new();
        private DateTimeOffset _referenceObservedAt;
        private readonly ScaledPrice _referencePrice = new(9_950, 2);

        public Fixture(long maximumOrderUnits = 100)
        {
            var acquired = _leases.Acquire(
                Resource,
                new ExecutionLeaseId("paper-strategy-lease"),
                new RuntimeInstanceId("paper-strategy-runtime"),
                Now,
                Now.AddHours(1));
            Assert.True(acquired.IsSuccess, acquired.Reason);
            Venue = new DeterministicPaperVenue();
            Venue.OnMarket(new PaperMarketSnapshot(
                Instrument,
                new ScaledPrice(99, 0),
                new ScaledPrice(100, 0),
                ScaledQuantity.FromWhole(100),
                ScaledQuantity.FromWhole(100),
                Now));
            _referenceObservedAt = Now;
            var oms = new OrderManagementService(_store, Venue, _leases, _clock);
            var options = new PaperExecutionBookTargetOptions(
                BookId,
                Strategy,
                new StrategyVersion("1.0.0"),
                Resource,
                acquired.Grant!.Value.Claim,
                new RiskLimits(
                    ScaledQuantity.FromWhole(maximumOrderUnits),
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
            Intake = new PaperExecutionBookTargetIntake(options, oms, _store, _clock, ResolvePrice);
        }

        public PaperExecutionBookTargetIntake Intake { get; }
        public DeterministicPaperVenue Venue { get; }
        public DateTimeOffset Now => new(_clock.UtcNow);

        public void Advance() => Advance(TimeSpan.FromSeconds(1));

        public void Advance(TimeSpan by) => _clock.UtcNow = _clock.UtcNow.Add(by);

        public decimal Position()
        {
            decimal position = 0;
            foreach (var projection in Projections())
            {
                foreach (var orderEvent in _store.Read(projection.ClientOrderId))
                {
                    if (orderEvent.Fill is not { } fill) continue;
                    var quantity = ExecutionNumericBoundary.ToDecimal(fill.Quantity);
                    position += projection.Terms.Side == OrderSide.Buy ? quantity : -quantity;
                }
            }
            return position;
        }

        public OmsOrderProjection[] Projections() =>
            _store.ReadOutbox()
                .Select(item => item.Event.AggregateId)
                .Distinct()
                .Select(item => _store.ReadProjection(item)!)
                .ToArray();

        public void Dispose() => Intake.Dispose();

        private bool ResolvePrice(
            InstrumentId instrumentId,
            out ScaledPrice price,
            out DateTimeOffset observedAtUtc)
        {
            price = _referencePrice;
            observedAtUtc = _referenceObservedAt;
            return instrumentId == Instrument;
        }
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
