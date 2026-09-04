using DaxAlgo.Sdk;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Risk;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.Sandbox;
using Xunit;

namespace TradingTerminal.Tests.Headless.Backtest;

public sealed class SdkStrategyBacktestAdapterTests
{
    private static readonly InstrumentId Instrument = new(42);
    private static readonly Contract Contract = Contract.UsStock("SPY", "ARCA");
    private static readonly InstrumentId PairInstrument = new(43);
    private static readonly Contract PairContract = Contract.UsStock("QQQ", "NASDAQ");

    [Fact]
    public async Task Completed_bars_drive_the_sdk_kernel_and_virtual_targets_fill_through_the_existing_router()
    {
        var kernel = new RoundTripBarKernel(Instrument);
        var adapter = new SdkStrategyBacktestAdapter(
            kernel,
            Instrument,
            Contract,
            BarSize.OneMinute,
            BrokerKind.Simulated);

        var result = await new BacktestSession().RunAsync(
            Config(Bars(100d, 110d, 120d)),
            adapter,
            new RiskManager(new RiskOptions
            {
                MaxPositionPerSymbol = 10,
                MaxDailyLoss = 10_000d,
            }));

        Assert.Equal(3, kernel.CompletedBarCount);
        Assert.Equal(3, kernel.RecentBarCountAtStop);
        Assert.Single(result.Trades);
        Assert.Equal(2, result.Trades[0].Quantity);
        Assert.True(result.Trades[0].ExitUtc > result.Trades[0].EntryUtc);
        Assert.Equal(0, adapter.Position);
        Assert.NotNull(result.Stats);
    }

    [Fact]
    public async Task Sdk_virtual_target_is_rejected_before_dispatch_when_it_exceeds_replay_risk()
    {
        var kernel = new EnterAndHoldBarKernel(Instrument, targetUnits: 2d);
        var adapter = new SdkStrategyBacktestAdapter(
            kernel,
            Instrument,
            Contract,
            BarSize.OneMinute,
            BrokerKind.Simulated);

        var result = await new BacktestSession().RunAsync(
            Config(Bars(100d, 110d)),
            adapter,
            new RiskManager(new RiskOptions
            {
                MaxPositionPerSymbol = 1,
                MaxDailyLoss = 10_000d,
            }));

        Assert.Empty(result.Fills ?? []);
        Assert.Empty(result.Trades);
        Assert.Equal(0, adapter.Position);
    }

    [Fact]
    public async Task Completed_bar_replay_rejects_out_of_order_history_instead_of_changing_causality()
    {
        var bars = Bars(100d, 110d).Reverse().ToArray();
        var adapter = new SdkStrategyBacktestAdapter(
            new EnterAndHoldBarKernel(Instrument, targetUnits: 0d),
            Instrument,
            Contract,
            BarSize.OneMinute,
            BrokerKind.Simulated);

        var run = () => new BacktestSession().RunAsync(Config(bars), adapter);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(run);
        Assert.Contains("strictly increasing", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pair_bars_share_one_timestamp_boundary_and_fill_only_their_own_contract()
    {
        var start = new DateTime(2026, 1, 2, 14, 30, 0, DateTimeKind.Utc);
        var first = BarsFrom(start, 100d, 101d, 102d);
        var second = BarsFrom(start, 300d, 301d, 302d);
        var kernel = new PairRoundTripKernel(Instrument, PairInstrument, start);
        var adapter = new SdkStrategyBacktestAdapter(
            kernel,
            [
                new SdkBacktestInstrument(Instrument, Contract),
                new SdkBacktestInstrument(PairInstrument, PairContract),
            ],
            BarSize.OneMinute,
            BrokerKind.Simulated);
        var config = new BacktestConfig(
            Contract,
            TickDataPath: string.Empty,
            StartingCash: 100_000d,
            ReplayBarSize: BarSize.OneMinute,
            ReplayBarSeries:
            [
                new BacktestBarSeries(Instrument, Contract, BarSize.OneMinute, first, 0.01d),
                new BacktestBarSeries(PairInstrument, PairContract, BarSize.OneMinute, second, 0.01d),
            ]);

        var result = await new BacktestSession().RunAsync(
            config,
            adapter,
            new RiskManager(new RiskOptions
            {
                MaxPositionPerSymbol = 10,
                MaxDailyLoss = 100_000d,
            }));

        Assert.True(kernel.SameBoundaryWasVisible);
        Assert.Equal(0, adapter.PositionFor(Instrument));
        Assert.Equal(0, adapter.PositionFor(PairInstrument));
        Assert.Equal(4, result.Fills?.Count);
        Assert.All(result.Fills!.Where(fill => fill.Symbol == "SPY"), fill => Assert.InRange(fill.Price, 99d, 103d));
        Assert.All(result.Fills!.Where(fill => fill.Symbol == "QQQ"), fill => Assert.InRange(fill.Price, 299d, 303d));
        Assert.Equal(["QQQ", "SPY"], result.Trades.Select(trade => trade.Symbol!).Order().ToArray());
        Assert.Equal(0, result.EndingPositions!["SPY"]);
        Assert.Equal(0, result.EndingPositions!["QQQ"]);
    }

    [Fact]
    public async Task Missing_pair_bar_remains_missing_instead_of_being_forward_filled()
    {
        var start = new DateTime(2026, 1, 2, 14, 30, 0, DateTimeKind.Utc);
        var first = BarsFrom(start, 100d, 101d, 102d);
        var second = new[] { BarsFrom(start, 300d)[0], BarsFrom(start.AddMinutes(2), 302d)[0] };
        var kernel = new MissingBarObservationKernel(Instrument, PairInstrument, start.AddMinutes(1));
        var adapter = new SdkStrategyBacktestAdapter(
            kernel,
            [
                new SdkBacktestInstrument(Instrument, Contract),
                new SdkBacktestInstrument(PairInstrument, PairContract),
            ],
            BarSize.OneMinute,
            BrokerKind.Simulated);

        await new BacktestSession().RunAsync(
            new BacktestConfig(
                Contract,
                TickDataPath: string.Empty,
                ReplayBarSize: BarSize.OneMinute,
                ReplayBarSeries:
                [
                    new BacktestBarSeries(Instrument, Contract, BarSize.OneMinute, first, 0.01d),
                    new BacktestBarSeries(PairInstrument, PairContract, BarSize.OneMinute, second, 0.01d),
                ]),
            adapter);

        Assert.Equal(start, kernel.SecondLegLatestAtMissingBoundary);
    }

    private static BacktestConfig Config(IReadOnlyList<Bar> bars) => new(
        Contract,
        TickDataPath: string.Empty,
        TickSize: 0.01d,
        SlippageTicks: 0,
        StartingCash: 100_000d,
        ReplayBars: bars,
        ReplayBarSize: BarSize.OneMinute);

    private static IReadOnlyList<Bar> Bars(params double[] closes)
    {
        var start = new DateTime(2026, 1, 2, 14, 30, 0, DateTimeKind.Utc);
        return BarsFrom(start, closes);
    }

    private static IReadOnlyList<Bar> BarsFrom(DateTime start, params double[] closes)
    {
        return closes.Select((close, index) => new Bar(
            start.AddMinutes(index),
            close,
            close + 1d,
            close - 1d,
            close,
            1_000L)).ToArray();
    }

    private sealed class PairRoundTripKernel(
        InstrumentId first,
        InstrumentId second,
        DateTime start) : IStrategyKernel
    {
        private readonly HashSet<DateTime> _acted = [];
        public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
        public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
        public bool SameBoundaryWasVisible { get; private set; }
        public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;

        public Task OnBarAsync(OhlcvBar bar, IStrategyRuntimeContext context, CancellationToken ct)
        {
            var firstLatest = context.Data.RecentBars(first, BarSize.OneMinute, 1).SingleOrDefault();
            var secondLatest = context.Data.RecentBars(second, BarSize.OneMinute, 1).SingleOrDefault();
            if (bar.OpenTimeUtc == start)
                SameBoundaryWasVisible |= firstLatest?.OpenTimeUtc == start && secondLatest?.OpenTimeUtc == start;
            if (!_acted.Add(bar.OpenTimeUtc)) return Task.CompletedTask;

            if (bar.OpenTimeUtc == start)
            {
                context.Book.SetTargetPosition(first, 2d);
                context.Book.SetTargetPosition(second, -3d);
            }
            else if (bar.OpenTimeUtc == start.AddMinutes(1))
            {
                context.Book.SetTargetPosition(first, 0d);
                context.Book.SetTargetPosition(second, 0d);
            }
            return Task.CompletedTask;
        }
    }

    private sealed class MissingBarObservationKernel(
        InstrumentId first,
        InstrumentId second,
        DateTime missingBoundary) : IStrategyKernel
    {
        public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
        public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
        public DateTime? SecondLegLatestAtMissingBoundary { get; private set; }
        public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;

        public Task OnBarAsync(OhlcvBar bar, IStrategyRuntimeContext context, CancellationToken ct)
        {
            if (bar.InstrumentId == first && bar.OpenTimeUtc == missingBoundary)
                SecondLegLatestAtMissingBoundary = context.Data.RecentBars(second, BarSize.OneMinute, 1).Single().OpenTimeUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class RoundTripBarKernel(InstrumentId instrument) : IStrategyKernel
    {
        private int _barCount;

        public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
        public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
        public int CompletedBarCount => _barCount;
        public int RecentBarCountAtStop { get; private set; }

        public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;

        public Task OnBarAsync(OhlcvBar bar, IStrategyRuntimeContext context, CancellationToken ct)
        {
            _barCount++;
            if (_barCount == 1)
                context.Book.SetTargetPosition(instrument, 2d);
            else if (_barCount == 2)
                context.Book.SetTargetPosition(instrument, 0d);
            return Task.CompletedTask;
        }

        public Task OnStopAsync(IStrategyRuntimeContext context, CancellationToken ct)
        {
            RecentBarCountAtStop = context.Data.RecentBars(instrument, BarSize.OneMinute, 10).Count;
            return Task.CompletedTask;
        }
    }

    private sealed class EnterAndHoldBarKernel(InstrumentId instrument, double targetUnits) : IStrategyKernel
    {
        public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
        public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
        public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;

        public Task OnBarAsync(OhlcvBar bar, IStrategyRuntimeContext context, CancellationToken ct)
        {
            context.Book.SetTargetPosition(instrument, targetUnits);
            return Task.CompletedTask;
        }
    }
}
