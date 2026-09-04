using System.Threading.Channels;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;

namespace TradingTerminal.Sandbox.Runtime.Tests;

public sealed class SandboxExecutionReplicatorTests
{
    private static readonly InstrumentId Instrument = new(42);

    [Fact]
    public async Task MapsCommittedWholeUnitTargetAndPricesWithoutRounding()
    {
        var source = new ManualPortfolioSource();
        var intake = new RecordingIntake();
        await using var replicator = new SandboxExecutionReplicator(
            source,
            intake,
            new SandboxExecutionReplicationOptions(
                "book-1",
                "sandbox-42",
                PolicyVersion: "sandbox-policy-v7"));

        source.Publish(Snapshot(2d, 90.25d, 110.5d));
        var outcome = await intake.NextAsync();

        Assert.True(outcome.Result.IsSuccess, outcome.Result.Message);
        var intent = Assert.Single(intake.Intents);
        Assert.Equal(Instrument, intent.Instrument);
        Assert.Equal(TradeIntentQuantityMode.TargetPosition, intent.QuantityMode);
        Assert.True(intent.SignedUnits.TryGetWholeUnits(out var units));
        Assert.Equal(2, units);
        Assert.Equal(new ScaledPrice(9_025, 2), intent.ProtectiveStopPrice);
        Assert.Equal(new ScaledPrice(1_105, 1), intent.ProfitTargetPrice);
        Assert.Equal("sandbox-42", intent.StrategyId);
        Assert.Equal("sandbox-policy-v7", intent.PolicyVersion);
    }

    [Theory]
    [InlineData(95d, +2d, false, true)]
    [InlineData(105d, -2d, false, true)]
    [InlineData(105d, +2d, true, false)]
    [InlineData(95d, -2d, true, false)]
    public async Task MapsRestingEntryToExactVenueTrigger(
        double triggerPrice,
        double signedTargetUnits,
        bool isStop,
        bool expectLimit)
    {
        var source = new ManualPortfolioSource();
        var intake = new RecordingIntake();
        await using var replicator = new SandboxExecutionReplicator(
            source,
            intake,
            new SandboxExecutionReplicationOptions("book-1", "sandbox-42"));

        source.Publish(Snapshot(0d, null, null) with
        {
            PendingEntry = new PendingEntryState(triggerPrice, signedTargetUnits, isStop),
        });
        Assert.True((await intake.NextAsync()).Result.IsSuccess);

        var intent = Assert.Single(intake.Intents);
        Assert.True(intent.SignedUnits.TryGetWholeUnits(out var units));
        Assert.Equal((long)signedTargetUnits, units);
        var expected = new ScaledPrice((long)triggerPrice, 0);
        Assert.Equal(expectLimit ? expected : null, intent.EntryLimitPrice);
        Assert.Equal(expectLimit ? null : expected, intent.EntryStopPrice);
    }

    [Fact]
    public async Task CoalescesAcceptedDuplicateButExplicitRetryReachesIntake()
    {
        var source = new ManualPortfolioSource();
        var intake = new RecordingIntake();
        await using var replicator = new SandboxExecutionReplicator(
            source,
            intake,
            new SandboxExecutionReplicationOptions("book-1", "sandbox-42"));
        var snapshot = Snapshot(2d, null, null);

        source.Publish(snapshot);
        Assert.True((await intake.NextAsync()).Result.IsSuccess);
        source.Publish(snapshot);
        await Task.Delay(100);
        Assert.Single(intake.Intents);

        Assert.True(replicator.ReplicateCurrent());
        Assert.True((await intake.NextAsync()).Result.IsSuccess);
        Assert.Equal(2, intake.Intents.Count);
    }

    [Fact]
    public async Task RejectedTargetIsNotMarkedAcceptedAndCanRetryOnNextSnapshot()
    {
        var source = new ManualPortfolioSource();
        var intake = new RecordingIntake(failFirst: true);
        await using var replicator = new SandboxExecutionReplicator(
            source,
            intake,
            new SandboxExecutionReplicationOptions("book-1", "sandbox-42"));
        var snapshot = Snapshot(2d, null, null);

        source.Publish(snapshot);
        Assert.False((await intake.NextAsync()).Result.IsSuccess);
        source.Publish(snapshot);
        Assert.True((await intake.NextAsync()).Result.IsSuccess);

        Assert.Equal(2, intake.Intents.Count);
    }

    [Fact]
    public async Task DisabledBindingNeverObservesTargets()
    {
        var source = new ManualPortfolioSource();
        var intake = new RecordingIntake();
        await using var replicator = new SandboxExecutionReplicator(
            source,
            intake,
            new SandboxExecutionReplicationOptions("book-1", "sandbox-42", Enabled: false));

        source.Publish(Snapshot(2d, null, null));
        await Task.Delay(100);

        Assert.False(replicator.IsEnabled);
        Assert.Empty(intake.Intents);
        Assert.Null(replicator.LastOutcome);
    }

    [Fact]
    public async Task CoalescingPreservesLatestTargetForEveryInstrument()
    {
        var legA = new InstrumentId(1);
        var legB = new InstrumentId(2);
        var source = new ManualPortfolioSource();
        var intake = new RecordingIntake();
        await using var replicator = new SandboxExecutionReplicator(
            source,
            intake,
            new SandboxExecutionReplicationOptions("book-1", "pair-strategy"));

        source.PublishMany(
            Snapshot(2d, null, null, legA),
            Snapshot(-3d, null, null, legB));

        await intake.NextAsync();
        await intake.NextAsync();
        Assert.Collection(
            intake.Intents.OrderBy(static intent => intent.Instrument.Value),
            intent =>
            {
                Assert.Equal(legA, intent.Instrument);
                Assert.True(intent.SignedUnits.TryGetWholeUnits(out var units));
                Assert.Equal(2, units);
            },
            intent =>
            {
                Assert.Equal(legB, intent.Instrument);
                Assert.True(intent.SignedUnits.TryGetWholeUnits(out var units));
                Assert.Equal(-3, units);
            });
    }

    private static SandboxPortfolioSnapshot Snapshot(
        double units,
        double? protectiveStop,
        double? profitTarget,
        InstrumentId? instrument = null) =>
        new(
            instrument ?? Instrument,
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
            false,
            protectiveStop,
            profitTarget);

    private sealed class ManualPortfolioSource : IModelPortfolioSource
    {
        public IModelPortfolio? CurrentSnapshot { get; private set; }

        public IReadOnlyList<IModelPortfolio> CurrentSnapshots { get; private set; } = [];

        public event Action<IModelPortfolio>? SnapshotChanged;

        public void Publish(IModelPortfolio snapshot)
        {
            CurrentSnapshot = snapshot;
            CurrentSnapshots = [snapshot];
            SnapshotChanged?.Invoke(snapshot);
        }

        public void PublishMany(params IModelPortfolio[] snapshots)
        {
            CurrentSnapshots = snapshots;
            CurrentSnapshot = snapshots.LastOrDefault();
            foreach (var snapshot in snapshots)
                SnapshotChanged?.Invoke(snapshot);
        }
    }

    private sealed class RecordingIntake(bool failFirst = false) : IExecutionBookTargetIntake
    {
        private readonly Channel<SandboxExecutionReplicationOutcome> _outcomes =
            Channel.CreateUnbounded<SandboxExecutionReplicationOutcome>();
        private int _calls;

        public List<TradeIntent> Intents { get; } = [];

        public Task<SandboxExecutionReplicationOutcome> NextAsync() =>
            _outcomes.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public ValueTask<ExecutionTargetSubmissionResult> SubmitTargetAsync(
            string bookId,
            TradeIntent intent,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (Intents)
                Intents.Add(intent);
            var result = failFirst && Interlocked.Increment(ref _calls) == 1
                ? ExecutionTargetSubmissionResult.Failure("closed")
                : ExecutionTargetSubmissionResult.Success("accepted");
            _outcomes.Writer.TryWrite(new SandboxExecutionReplicationOutcome(intent, result));
            return ValueTask.FromResult(result);
        }
    }
}
