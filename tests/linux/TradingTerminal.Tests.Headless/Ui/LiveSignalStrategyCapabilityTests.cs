using System.Reactive.Subjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Notifications;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.UI;
using TradingTerminal.UI.Logging;
using Xunit;

namespace TradingTerminal.Tests.Ui;

/// <summary>
/// Pins the live strategy start boundary to the selected broker client's immutable capability
/// descriptor. These are interaction tests: rejection must happen before any strategy, ingest,
/// store, or hub work, while accepted starts must open exactly the declared channels.
/// </summary>
public sealed class LiveSignalStrategyCapabilityTests
{
    [Theory]
    [InlineData(StrategyDataRequirement.Depth, "L2 depth")]
    [InlineData(StrategyDataRequirement.TradeTape, "trade tape")]
    public async Task Required_unsupported_feed_rejects_before_any_start_side_effect(
        StrategyDataRequirement extraRequirement,
        string missingFeed)
    {
        var rig = new Rig(
            BrokerKind.Alpaca,
            BrokerCapabilityCatalog.MarketDataFor(BrokerKind.Alpaca),
            Baseline | extraRequirement);

        await rig.StartAsync();

        rig.ViewModel.IsConfigured.Should().BeFalse();
        rig.ViewModel.IsStreaming.Should().BeFalse();
        rig.ViewModel.IsStarting.Should().BeFalse();
        rig.ViewModel.TradeTapeAvailable.Should().BeFalse();
        rig.ViewModel.LatestDepth.Should().BeNull();
        rig.ViewModel.ValidationError.Should().Contain(nameof(BrokerKind.Alpaca)).And.Contain(missingFeed);
        rig.ViewModel.BuildCount.Should().Be(0);

        rig.Ingest.DidNotReceiveWithAnyArgs().Resolve(default!, default);
        rig.Ingest.DidNotReceiveWithAnyArgs().Subscribe(default!, default);
        rig.Ingest.DidNotReceiveWithAnyArgs().SubscribeTrades(default!, default);
        _ = rig.Store.DidNotReceiveWithAnyArgs()
            .GetRecentBarsAsync(default, default, default, default, default);
        rig.Hub.DidNotReceiveWithAnyArgs().Quotes(default);
        rig.Hub.DidNotReceiveWithAnyArgs().Depth(default);
        rig.Hub.DidNotReceiveWithAnyArgs().Trades(default);
        rig.RouterFactory.DidNotReceive().Create();
        await rig.Strategy.DidNotReceiveWithAnyArgs().OnStartAsync(default!, default!, default);
        _ = rig.Client.Received(1).MarketDataCapabilities;
        rig.Client.DidNotReceiveWithAnyArgs().SubscribeTradesAsync(default!, default);
    }

    [Fact]
    public async Task Unknown_requirement_flag_fails_closed_before_resolution()
    {
        var rig = new Rig(
            BrokerKind.Simulated,
            BrokerCapabilityCatalog.MarketDataFor(BrokerKind.Simulated),
            Baseline | (StrategyDataRequirement)0x40);

        await rig.StartAsync();

        rig.ViewModel.ValidationError.Should().Contain("unknown market-data requirement flags 0x40");
        rig.ViewModel.IsConfigured.Should().BeFalse();
        rig.ViewModel.BuildCount.Should().Be(0);
        rig.Ingest.DidNotReceiveWithAnyArgs().Resolve(default!, default);
        rig.RouterFactory.DidNotReceive().Create();
    }

    [Fact]
    public async Task Baseline_strategy_on_L1_only_broker_starts_quotes_without_empty_depth_or_tape_readers()
    {
        var rig = new Rig(
            BrokerKind.Alpaca,
            BrokerCapabilityCatalog.MarketDataFor(BrokerKind.Alpaca),
            Baseline);

        await rig.StartAsync();

        rig.ViewModel.IsConfigured.Should().BeTrue();
        rig.ViewModel.IsStreaming.Should().BeTrue();
        rig.ViewModel.BuildCount.Should().Be(1);
        rig.ViewModel.TradeTapeAvailable.Should().BeFalse();
        rig.ViewModel.LatestDepth.Should().BeNull();
        rig.Ingest.Received(1).Resolve(rig.Contract, BrokerKind.Alpaca);
        rig.Ingest.Received(1).Subscribe(rig.Contract, BrokerKind.Alpaca);
        rig.Hub.Received(1).Quotes(rig.InstrumentId);
        rig.Hub.DidNotReceiveWithAnyArgs().Depth(default);
        rig.Hub.DidNotReceiveWithAnyArgs().Trades(default);
        rig.Ingest.DidNotReceiveWithAnyArgs().SubscribeTrades(default!, default);
        _ = rig.Client.Received(1).MarketDataCapabilities;

        await rig.StopAsync();

        rig.ViewModel.IsStreaming.Should().BeFalse();
        rig.IngestHandle.Received(1).Dispose();
    }

    [Fact]
    public async Task Supported_depth_and_required_tape_open_only_the_canonical_ingest_and_hub_paths()
    {
        var rig = new Rig(
            BrokerKind.Simulated,
            BrokerCapabilityCatalog.MarketDataFor(BrokerKind.Simulated),
            Baseline | StrategyDataRequirement.Depth | StrategyDataRequirement.TradeTape);

        await rig.StartAsync();

        rig.ViewModel.IsStreaming.Should().BeTrue();
        rig.ViewModel.BuildCount.Should().Be(1);
        rig.ViewModel.TradeTapeAvailable.Should().BeTrue();
        rig.Ingest.Received(1).Resolve(rig.Contract, BrokerKind.Simulated);
        rig.Ingest.Received(1).Subscribe(rig.Contract, BrokerKind.Simulated);
        rig.Ingest.Received(1).SubscribeTrades(rig.Contract, BrokerKind.Simulated);
        rig.Hub.Received(1).Quotes(rig.InstrumentId);
        rig.Hub.Received(1).Depth(rig.InstrumentId);
        rig.Hub.Received(1).Trades(rig.InstrumentId);
        rig.Client.DidNotReceiveWithAnyArgs().SubscribeTradesAsync(default!, default);
        _ = rig.Client.Received(1).MarketDataCapabilities;
        await rig.Strategy.Received(1).OnStartAsync(rig.Clock, Arg.Any<IOrderRouter>(), Arg.Any<CancellationToken>());

        var depth = new DepthSnapshot(
            DateTime.UtcNow,
            [new DepthLevel(100, 5)],
            [new DepthLevel(101, 7)]);
        rig.Depth.OnNext(depth);
        await EventuallyAsync(() => ReferenceEquals(rig.ViewModel.LatestDepth, depth));

        await rig.StopAsync();

        rig.ViewModel.IsStreaming.Should().BeFalse();
        rig.ViewModel.TradeTapeAvailable.Should().BeFalse();
        rig.ViewModel.LatestDepth.Should().BeNull();
        rig.IngestHandle.Received(1).Dispose();
        rig.TradeIngestHandle.Received(1).Dispose();
        await rig.Strategy.Received(1).OnEndAsync(rig.Clock, Arg.Any<IOrderRouter>(), CancellationToken.None);
    }

    [Fact]
    public async Task Required_tape_runtime_failure_stops_the_run_and_clears_availability()
    {
        var rig = new Rig(
            BrokerKind.Simulated,
            BrokerCapabilityCatalog.MarketDataFor(BrokerKind.Simulated),
            Baseline | StrategyDataRequirement.TradeTape,
            tradeStartFailure: new InvalidOperationException("feed authorization expired"));

        await rig.StartAsync();
        await EventuallyAsync(() => !rig.ViewModel.IsStreaming);

        rig.ViewModel.IsConfigured.Should().BeFalse();
        rig.ViewModel.TradeTapeAvailable.Should().BeFalse();
        rig.ViewModel.ValidationError.Should().Contain("Required trade tape").And.Contain("authorization expired");
        rig.Ingest.Received(1).SubscribeTrades(rig.Contract, BrokerKind.Simulated);
        rig.Hub.DidNotReceiveWithAnyArgs().Trades(default);
        rig.Client.DidNotReceiveWithAnyArgs().SubscribeTradesAsync(default!, default);
        rig.IngestHandle.Received(1).Dispose();
        await rig.Strategy.Received(1).OnEndAsync(rig.Clock, Arg.Any<IOrderRouter>(), CancellationToken.None);
    }

    [Fact]
    public async Task Required_depth_runtime_failure_stops_the_run_and_clears_depth()
    {
        var rig = new Rig(
            BrokerKind.Simulated,
            BrokerCapabilityCatalog.MarketDataFor(BrokerKind.Simulated),
            Baseline | StrategyDataRequirement.Depth,
            depthStartFailure: new InvalidOperationException("depth channel rejected"));

        await rig.StartAsync();
        await EventuallyAsync(() => !rig.ViewModel.IsStreaming);

        rig.ViewModel.IsConfigured.Should().BeFalse();
        rig.ViewModel.LatestDepth.Should().BeNull();
        rig.ViewModel.ValidationError.Should().Contain("Required L2 depth").And.Contain("channel rejected");
        rig.Hub.Received(1).Depth(rig.InstrumentId);
        rig.IngestHandle.Received(1).Dispose();
        await rig.Strategy.Received(1).OnEndAsync(rig.Clock, Arg.Any<IOrderRouter>(), CancellationToken.None);
    }

    private const StrategyDataRequirement Baseline =
        StrategyDataRequirement.L1 | StrategyDataRequirement.Bars;

    private static async Task EventuallyAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow.AddSeconds(2);
        while (!predicate() && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        predicate().Should().BeTrue("the fire-and-forget stream pump should settle promptly");
    }

    private sealed class Rig
    {
        public Rig(
            BrokerKind broker,
            MarketDataCapabilities capabilities,
            StrategyDataRequirement requirement,
            Exception? tradeStartFailure = null,
            Exception? depthStartFailure = null)
        {
            Broker = broker;
            Contract = Contract.UsStock("AAPL");
            InstrumentId = new InstrumentId(42);

            Repository = Substitute.For<IMarketDataRepository>();
            Hub = Substitute.For<IMarketDataHub>();
            Ingest = Substitute.For<IMarketDataIngest>();
            Store = Substitute.For<IMarketDataStore>();
            Selector = Substitute.For<IBrokerSelector>();
            Registry = Substitute.For<IInstrumentRegistry>();
            Client = Substitute.For<IBrokerClient>();
            Notifications = Substitute.For<INotificationPublisher>();
            Clock = Substitute.For<IClock>();
            RouterFactory = Substitute.For<ISignalGeneratorRouterFactory>();
            Strategy = Substitute.For<IBacktestStrategy>();
            IngestHandle = Substitute.For<IDisposable>();
            TradeIngestHandle = Substitute.For<IDisposable>();

            var row = new TradableInstrument("Apple", "Equity", Contract, broker);
            Repository.ListInstrumentsAsync(Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<TradableInstrument>>([row]));
            Registry.All().Returns(Array.Empty<Instrument>());
            Selector.Connected.Returns([broker]);
            Selector.IsConnected(broker).Returns(true);
            Selector.Get(broker).Returns(Client);
            Client.Kind.Returns(broker);
            Client.MarketDataCapabilities.Returns(capabilities);

            Ingest.Resolve(Contract, broker).Returns(InstrumentId);
            Ingest.Subscribe(Contract, broker).Returns(IngestHandle);
            if (tradeStartFailure is null)
                Ingest.SubscribeTrades(Contract, broker).Returns(TradeIngestHandle);
            else
                Ingest.When(x => x.SubscribeTrades(Contract, broker)).Do(_ => throw tradeStartFailure);

            Quote = new Subject<Quote>();
            Depth = new Subject<DepthSnapshot>();
            Trades = new Subject<TradePrint>();
            Hub.Quotes(InstrumentId).Returns(Quote);
            if (depthStartFailure is null)
                Hub.Depth(InstrumentId).Returns(Depth);
            else
                Hub.When(x => x.Depth(InstrumentId)).Do(_ => throw depthStartFailure);
            Hub.Trades(InstrumentId).Returns(Trades);

            Store.GetRecentBarsAsync(
                    InstrumentId,
                    Arg.Any<BarSize>(),
                    Arg.Any<int>(),
                    broker,
                    Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IReadOnlyList<OhlcvBar>>([]));
            RouterFactory.Create().Returns(_ => new SignalGeneratorRouter());
            Strategy.OnStartAsync(Clock, Arg.Any<IOrderRouter>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);
            Strategy.OnEndAsync(Clock, Arg.Any<IOrderRouter>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var services = new LiveStrategyHostServices(
                Repository,
                Hub,
                Ingest,
                Store,
                Selector,
                new InMemoryLogSink(),
                Registry);
            ViewModel = new TestViewModel(
                services,
                Notifications,
                Clock,
                RouterFactory,
                Strategy,
                requirement);
            ViewModel.SelectedInstrument = new SignalInstrument("Apple", "Equity", Contract, broker);
        }

        public BrokerKind Broker { get; }
        public Contract Contract { get; }
        public InstrumentId InstrumentId { get; }
        public IMarketDataRepository Repository { get; }
        public IMarketDataHub Hub { get; }
        public IMarketDataIngest Ingest { get; }
        public IMarketDataStore Store { get; }
        public IBrokerSelector Selector { get; }
        public IInstrumentRegistry Registry { get; }
        public IBrokerClient Client { get; }
        public INotificationPublisher Notifications { get; }
        public IClock Clock { get; }
        public ISignalGeneratorRouterFactory RouterFactory { get; }
        public IBacktestStrategy Strategy { get; }
        public IDisposable IngestHandle { get; }
        public IDisposable TradeIngestHandle { get; }
        public Subject<Quote> Quote { get; }
        public Subject<DepthSnapshot> Depth { get; }
        public Subject<TradePrint> Trades { get; }
        public TestViewModel ViewModel { get; }

        public Task StartAsync() => ViewModel.ContinueCommand.ExecuteAsync(null);
        public Task StopAsync() => ViewModel.StopCommand.ExecuteAsync(null);
    }

    private sealed class TestViewModel : LiveSignalStrategyViewModelBase
    {
        private readonly IBacktestStrategy _strategy;
        private readonly StrategyDataRequirement _requirement;

        public TestViewModel(
            LiveStrategyHostServices services,
            INotificationPublisher notifications,
            IClock clock,
            ISignalGeneratorRouterFactory routerFactory,
            IBacktestStrategy strategy,
            StrategyDataRequirement requirement)
            : base(
                "capability-test",
                "Capability Test",
                services,
                notifications,
                clock,
                routerFactory,
                NullLogger.Instance)
        {
            _strategy = strategy;
            _requirement = requirement;
        }

        public int BuildCount { get; private set; }

        protected override StrategyDataRequirement DataRequirement => _requirement;

        protected override IBacktestStrategy BuildStrategy(Contract contract)
        {
            BuildCount++;
            return _strategy;
        }
    }
}
