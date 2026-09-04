using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Avalonia.Headless.XUnit;
using DaxAlgo.Sdk;
using Microsoft.Extensions.DependencyInjection;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Time;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.Infrastructure.Plugins;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.Sandbox;
using TradingTerminal.UI.Avalonia.Controls.Render;
using TradingTerminal.UI.Logging;
using TradingTerminal.UI.Strategies;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class AuthoredVisualizerFeedLeaseTests
{
    [AvaloniaFact]
    public async Task Persisted_compiler_verified_visualizer_restores_and_opens_the_feed_owned_window()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "daxalgo-tests",
            "authored-visualizer-restart-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var specification = CandleSpecification(BrokerKind.Simulated, new InstrumentId(7));
            var script = new StrategyScript(
                specification.UnitId,
                specification.Name,
                [new StrategyFile("GeneratedBitcoinCandles.cs", CompiledCandleVisualizerSource(specification))]);
            var compilation = new RoslynAuthoredUnitCompilerV1().Compile(specification, script);
            Assert.True(compilation.Success, string.Join(Environment.NewLine, compilation.Diagnostics));

            var pluginHost = new PluginHostContext(root, PluginTrustPolicy.Permissive, []);
            var installer = new AuthoredStrategyInstaller(
                new ServiceCollection().BuildServiceProvider(),
                new EmptyBacktestRegistry(),
                new EmptyStrategyFactory(),
                pluginHost);
            var persisted = installer.PersistAuthoredUnit(script, compilation.Unit!);
            Assert.True(persisted.Persisted, persisted.Message);

            var restartedServices = new ServiceCollection();
            var report = PluginLoader.LoadWithReport(
                restartedServices,
                root,
                SdkInfo.Version,
                new PluginStateStore(root));
            Assert.Empty(report.Problems);
            Assert.Single(report.Loaded);

            using var restartedProvider = restartedServices.BuildServiceProvider();
            var restoredPlugins = restartedProvider
                .GetServices<AuthoredVisualizerPluginRegistration>()
                .ToArray();
            var restored = Assert.Single(new VisualizerRegistry(authoredPlugins: restoredPlugins).All);
            Assert.Equal(specification.UnitId, restored.Id);

            var ingest = new RecordingIngest();
            var hub = new TestMarketDataHub();
            var session = await AuthoredVisualizerSession.OpenAsync(
                restored.Descriptor.DisplayName,
                restored.Create,
                hub,
                new FixedClock(),
                new InMemoryLogSink(),
                specification: restored.AuthoredSpecification,
                ingest: ingest,
                instrumentRegistry: Registry((new InstrumentId(7), "BTC-USD", BrokerKind.Simulated)),
                brokerSelector: new FixedSelector(BrokerKind.Simulated, connected: true));

            try
            {
                global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                hub.PublishBar(new OhlcvBar(
                    new InstrumentId(7),
                    BarSize.OneMinute,
                    new DateTime(2026, 9, 4, 3, 0, 0, DateTimeKind.Utc),
                    100,
                    106,
                    99,
                    105.25,
                    42,
                    BrokerKind.Simulated,
                    IsFinal: true));

                Assert.True(await WaitUntilAsync(() =>
                {
                    var frame = new TextRecordingSurface();
                    return session.Runtime.TryDraw(frame) &&
                           frame.Texts.Any(text => text.Contains("105.25", StringComparison.Ordinal));
                }, TimeSpan.FromSeconds(2)));
            }
            finally
            {
                session.Window.Close();
                global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }

            Assert.True(await WaitUntilAsync(
                () => session.Runtime.State == SandboxVisualizerRuntimeState.Stopped && ingest.Disposed == 1,
                TimeSpan.FromSeconds(2)));
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); }
            catch { /* best-effort test cleanup */ }
        }
    }

    [AvaloniaFact]
    public async Task Compiler_verified_visualizer_opens_real_session_renders_live_bar_and_releases_feed()
    {
        var specification = CandleSpecification(BrokerKind.Simulated, new InstrumentId(7));
        var script = new StrategyScript(
            specification.UnitId,
            specification.Name,
            [new StrategyFile("GeneratedBitcoinCandles.cs", CompiledCandleVisualizerSource(specification))]);
        var compilation = new RoslynAuthoredUnitCompilerV1().Compile(specification, script);

        Assert.True(compilation.Success, string.Join(Environment.NewLine, compilation.Diagnostics));
        var compiled = Assert.IsType<CompiledAuthoredUnitV1>(compilation.Unit);
        var ingest = new RecordingIngest();
        var hub = new TestMarketDataHub();
        var registry = Registry((new InstrumentId(7), "BTC-USD", BrokerKind.Simulated));

        var session = await AuthoredVisualizerSession.OpenAsync(
            specification.Name,
            () => (IVisualizer)Activator.CreateInstance(compiled.RuntimeType)!,
            hub,
            new FixedClock(),
            new InMemoryLogSink(),
            specification: specification,
            ingest: ingest,
            instrumentRegistry: registry,
            brokerSelector: new FixedSelector(BrokerKind.Simulated, connected: true));

        try
        {
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(session.Window.IsVisible);
            Assert.Equal(SandboxVisualizerRuntimeState.Running, session.Runtime.State);
            Assert.Collection(
                ingest.Opened,
                call => Assert.Equal(("bars", "BTC-USD", BrokerKind.Simulated, (BarSize?)BarSize.OneMinute), call));

            var waitingFrame = new TextRecordingSurface();
            Assert.True(session.Runtime.TryDraw(waitingFrame));
            Assert.Contains("Waiting for bars", Assert.Single(waitingFrame.Texts), StringComparison.Ordinal);

            hub.PublishBar(new OhlcvBar(
                new InstrumentId(7),
                BarSize.OneMinute,
                new DateTime(2026, 9, 4, 3, 0, 0, DateTimeKind.Utc),
                100,
                106,
                99,
                105.25,
                42,
                BrokerKind.Simulated,
                IsFinal: true));

            var renderedLiveBar = await WaitUntilAsync(() =>
            {
                var liveFrame = new TextRecordingSurface();
                return session.Runtime.TryDraw(liveFrame) &&
                       liveFrame.Texts.Any(text => text.Contains("105.25", StringComparison.Ordinal));
            }, TimeSpan.FromSeconds(2));

            Assert.True(renderedLiveBar, "The authored window never rendered the broker-fed completed bar.");
        }
        finally
        {
            session.Window.Close();
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        }

        Assert.True(await WaitUntilAsync(
            () => session.Runtime.State == SandboxVisualizerRuntimeState.Stopped && ingest.Disposed == 1,
            TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public void Acquire_opens_exact_required_streams_and_dispose_releases_each_once()
    {
        var ingest = new RecordingIngest();
        var registry = Registry((new InstrumentId(7), "BTC-USD", BrokerKind.Simulated));
        var selector = new FixedSelector(BrokerKind.Simulated, connected: true);

        var lease = AuthoredUnitFeedLease.Acquire(
            Specification(BrokerKind.Simulated, new InstrumentId(7)),
            ingest,
            registry,
            selector,
            AuthoredUnitKindV1.Visualizer);

        Assert.Equal([new InstrumentId(7)], lease.AuthorizedInstruments);
        Assert.Collection(
            ingest.Opened,
            call => Assert.Equal(("market", "BTC-USD", BrokerKind.Simulated, (BarSize?)null), call),
            call => Assert.Equal(("bars", "BTC-USD", BrokerKind.Simulated, BarSize.OneMinute), call),
            call => Assert.Equal(("trades", "BTC-USD", BrokerKind.Simulated, (BarSize?)null), call));
        Assert.Equal(0, ingest.Disposed);

        lease.Dispose();
        lease.Dispose();

        Assert.Equal(3, ingest.Disposed);
    }

    [Fact]
    public void Acquire_rejects_disconnected_broker_before_opening_any_stream()
    {
        var ingest = new RecordingIngest();

        var error = Assert.Throws<InvalidOperationException>(() =>
            AuthoredUnitFeedLease.Acquire(
                Specification(BrokerKind.Binance, new InstrumentId(7)),
                ingest,
                Registry((new InstrumentId(7), "BTCUSDT", BrokerKind.Binance)),
                new FixedSelector(BrokerKind.Binance, connected: false),
                AuthoredUnitKindV1.Visualizer));

        Assert.Contains("Connect Binance", error.Message, StringComparison.Ordinal);
        Assert.Empty(ingest.Opened);
    }

    [Fact]
    public void Acquire_rejects_channel_the_selected_broker_does_not_implement()
    {
        var ingest = new RecordingIngest();

        var error = Assert.Throws<NotSupportedException>(() =>
            AuthoredUnitFeedLease.Acquire(
                Specification(BrokerKind.Alpaca, new InstrumentId(7)),
                ingest,
                Registry((new InstrumentId(7), "BTC/USD", BrokerKind.Alpaca)),
                new FixedSelector(BrokerKind.Alpaca, connected: true),
                AuthoredUnitKindV1.Visualizer));

        Assert.Contains("L2", error.Message, StringComparison.Ordinal);
        Assert.Empty(ingest.Opened);
    }

    [Fact]
    public void Acquire_is_atomic_when_a_later_instrument_has_no_broker_alias()
    {
        var ingest = new RecordingIngest();
        var registry = Registry((new InstrumentId(7), "BTC-USD", BrokerKind.Simulated));
        var specification = Specification(BrokerKind.Simulated, new InstrumentId(7)) with
        {
            Instruments =
            [
                new AuthoredInstrumentRequestV1(
                    "primary", "BTC", new InstrumentId(7), AssetClass.Crypto, BrokerKind.Simulated),
                new AuthoredInstrumentRequestV1(
                    "comparison", "ETH", new InstrumentId(8), AssetClass.Crypto, BrokerKind.Simulated),
            ],
        };

        Assert.Throws<InvalidOperationException>(() => AuthoredUnitFeedLease.Acquire(
            specification,
            ingest,
            registry,
            new FixedSelector(BrokerKind.Simulated, connected: true),
            AuthoredUnitKindV1.Visualizer));

        Assert.Equal(3, ingest.Opened.Count);
        Assert.Equal(3, ingest.Disposed);
    }

    private static AuthoredUnitSpecificationV1 Specification(BrokerKind broker, InstrumentId instrument) => new(
        AuthoredUnitSpecificationV1.CurrentSchemaVersion,
        "live-btc-chart",
        "Live BTC chart",
        "Show BTC one-minute candles, quotes, order book, and trades.",
        AuthoredUnitSourceKindV1.Text,
        AuthoredUnitKindV1.Visualizer,
        [new AuthoredInstrumentRequestV1("primary", "BTC", instrument, AssetClass.Crypto, broker)],
        new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
        StrategyDataRequirement.Bars |
        StrategyDataRequirement.L1 |
        StrategyDataRequirement.Depth |
        StrategyDataRequirement.TradeTape,
        [],
        new AuthoredChartCompositionV1(
            [new AuthoredChartPaneV1("main", AuthoredChartPaneRoleV1.Price, 0, "Market")],
            [
                new AuthoredChartLayerV1("candles", "main", AuthoredChartLayerKindV1.Candles,
                    "price.candles@1", new Dictionary<string, string>()),
                new AuthoredChartLayerV1("book", "main", AuthoredChartLayerKindV1.OrderBook,
                    "market.depth_ladder@1", new Dictionary<string, string>()),
                new AuthoredChartLayerV1("tape", "main", AuthoredChartLayerKindV1.TradeTape,
                    "market.trade_tape@1", new Dictionary<string, string>()),
            ]),
        [],
        [],
        AuthoredUnitExecutionIntentV1.None);

    private static AuthoredUnitSpecificationV1 CandleSpecification(
        BrokerKind broker,
        InstrumentId instrument) => new(
        AuthoredUnitSpecificationV1.CurrentSchemaVersion,
        "live-btc-candles",
        "Live BTC candles",
        "Show BTC one-minute candles.",
        AuthoredUnitSourceKindV1.Text,
        AuthoredUnitKindV1.Visualizer,
        [new AuthoredInstrumentRequestV1("primary", "BTC", instrument, AssetClass.Crypto, broker)],
        new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
        StrategyDataRequirement.Bars,
        [],
        new AuthoredChartCompositionV1(
            [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0, "BTC · 1m")],
            [new AuthoredChartLayerV1(
                "candles",
                "price",
                AuthoredChartLayerKindV1.Candles,
                "price.candles@1",
                new Dictionary<string, string>())]),
        [],
        [],
        AuthoredUnitExecutionIntentV1.None);

    private static string CompiledCandleVisualizerSource(AuthoredUnitSpecificationV1 specification)
    {
        var specificationHash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        return $$"""
            public sealed class GeneratedBitcoinCandles : IVisualizer, IAuthoredDrawingManifest
            {
                private double? _lastClose;
                private readonly List<OhlcvBar> _bars = new();

                public static string SpecificationHashSha256 => "{{specificationHash}}";
                public IReadOnlyList<string> DrawingLayerTypeIds => new[] { "price.candles@1" };
                public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
                public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;

                public Task OnStartAsync(IVisualizerContext context, CancellationToken ct) =>
                    Task.CompletedTask;

                public Task OnBarAsync(
                    OhlcvBar bar,
                    IVisualizerContext context,
                    CancellationToken ct)
                {
                    _lastClose = bar.Close;
                    _bars.Add(bar);
                    if (_bars.Count > 256) _bars.RemoveAt(0);
                    return Task.CompletedTask;
                }

                public void Draw(IRenderSurface surface)
                {
                    if (_bars.Count == 0)
                    {
                        surface.Text(8, 18, "Waiting for bars");
                        return;
                    }

                    using (surface.Layer("candles", "price.candles@1"))
                    {
                        Candles.Draw(surface, _bars);
                        surface.Text(8, 18, $"Close {_lastClose!.Value:R}");
                    }
                }
            }
            """;
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (predicate()) return true;
            await Task.Delay(25);
        }
        return predicate();
    }

    private static MemoryRegistry Registry(params (InstrumentId Id, string Symbol, BrokerKind Broker)[] rows)
    {
        var registry = new MemoryRegistry();
        foreach (var row in rows)
        {
            registry.Add(
                new Instrument(row.Id, row.Symbol, AssetClass.Crypto, "CRYPTO", "USD", 0.01, 1),
                row.Broker,
                row.Symbol);
        }
        return registry;
    }

    private sealed class RecordingIngest : IMarketDataIngest
    {
        public List<(string Kind, string Symbol, BrokerKind Broker, BarSize? Size)> Opened { get; } = [];
        public int Disposed { get; private set; }

        public InstrumentId Resolve(Contract contract, BrokerKind broker) => throw new NotSupportedException();

        public IDisposable Subscribe(Contract contract, BrokerKind broker) =>
            Add("market", contract, broker, null);

        public IDisposable SubscribeBars(Contract contract, BrokerKind broker, BarSize size) =>
            Add("bars", contract, broker, size);

        public IDisposable SubscribeTrades(Contract contract, BrokerKind broker) =>
            Add("trades", contract, broker, null);

        private IDisposable Add(string kind, Contract contract, BrokerKind broker, BarSize? size)
        {
            Opened.Add((kind, contract.Symbol, broker, size));
            return new CallbackDisposable(() => Disposed++);
        }
    }

    private sealed class CallbackDisposable(Action dispose) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                dispose();
        }
    }

    private sealed class MemoryRegistry : IInstrumentRegistry
    {
        private readonly Dictionary<InstrumentId, Instrument> _instruments = [];
        private readonly Dictionary<(InstrumentId, BrokerKind), string> _aliases = [];

        public void Add(Instrument instrument, BrokerKind broker, string symbol)
        {
            _instruments[instrument.Id] = instrument;
            _aliases[(instrument.Id, broker)] = symbol;
        }

        public Instrument? Get(InstrumentId id) => _instruments.GetValueOrDefault(id);
        public InstrumentId? Resolve(BrokerKind broker, string brokerSymbol) =>
            _aliases.FirstOrDefault(row => row.Key.Item2 == broker && row.Value == brokerSymbol).Key.Item1;
        public InstrumentId ResolveOrCreate(Contract contract, BrokerKind broker) => throw new NotSupportedException();
        public string? ToBrokerSymbol(InstrumentId id, BrokerKind broker) => _aliases.GetValueOrDefault((id, broker));
        public void RegisterAlias(InstrumentAlias alias) => _aliases[(alias.InstrumentId, alias.Broker)] = alias.BrokerSymbol;
        public IReadOnlyList<Instrument> All() => [.. _instruments.Values];
    }

    private sealed class FixedSelector(BrokerKind kind, bool connected) : IBrokerSelector
    {
        private readonly IBrokerClient _client = new CapabilityOnlyBrokerClient(kind);
        public IReadOnlyList<BrokerKind> AvailableKinds => [kind];
        public bool IsAvailable(BrokerKind candidate) => candidate == kind;
        public IReadOnlyList<BrokerKind> Connected => connected ? [kind] : [];
        public bool IsConnected(BrokerKind candidate) => candidate == kind && connected;
        public IBrokerClient Get(BrokerKind candidate) => candidate == kind
            ? _client
            : throw new KeyNotFoundException();
        public BrokerConnectionMode ModeOf(BrokerKind candidate) =>
            new(candidate, false, "Test", "Capability-only test client");
        public IObservable<ConnectionState> StateOf(BrokerKind candidate) => new FixedObservable(
            IsConnected(candidate) ? ConnectionState.Connected : ConnectionState.Disconnected);
        public ConnectionState CurrentStateOf(BrokerKind candidate) =>
            IsConnected(candidate) ? ConnectionState.Connected : ConnectionState.Disconnected;
        public event EventHandler<BrokerStateChangedEventArgs>? StateChanged;
        public Task ConnectAsync(BrokerKind candidate, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(BrokerKind candidate, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class CapabilityOnlyBrokerClient(BrokerKind kind) : IBrokerClient
    {
        public BrokerKind Kind => kind;
        public IObservable<ConnectionState> ConnectionState { get; } =
            new FixedObservable(TradingTerminal.Core.Domain.ConnectionState.Connected);
        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TradableInstrument>> ListInstrumentsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TradableInstrument>>([]);
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Bar>> RequestHistoricalBarsAsync(
            Contract contract, BarSize barSize, TimeSpan duration, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Bar>>([]);
        public IAsyncEnumerable<Bar> SubscribeBarsAsync(
            Contract contract, BarSize barSize, CancellationToken ct = default) => Empty<Bar>();
        public IAsyncEnumerable<Tick> SubscribeTicksAsync(
            Contract contract, CancellationToken ct = default) => Empty<Tick>();
        public IAsyncEnumerable<DepthSnapshot> SubscribeDepthAsync(
            Contract contract, int levels = 10, CancellationToken ct = default) => Empty<DepthSnapshot>();
        public IAsyncEnumerable<TradeTick> SubscribeTradesAsync(
            Contract contract, CancellationToken ct = default) => Empty<TradeTick>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static async IAsyncEnumerable<T> Empty<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class FixedObservable(ConnectionState value) : IObservable<ConnectionState>
    {
        public IDisposable Subscribe(IObserver<ConnectionState> observer)
        {
            observer.OnNext(value);
            return new CallbackDisposable(static () => { });
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 4, 3, 0, 0, DateTimeKind.Utc);
    }

    private sealed class EmptyBacktestRegistry : IBacktestStrategyRegistry
    {
        public IReadOnlyList<BacktestStrategyOption> All => [];
        public BacktestStrategyOption? Find(string id) => null;
        public void Register(BacktestStrategyOption option) { }
        public bool Remove(string id) => false;
        public event EventHandler? Changed { add { } remove { } }
    }

    private sealed class EmptyStrategyFactory : IStrategyFactory
    {
        public IReadOnlyList<ITradingStrategy> All => [];
        public StrategyHost Create(string strategyId) => throw new KeyNotFoundException(strategyId);
        public void Register(ITradingStrategy strategy, StrategyFactoryRegistration registration) { }
        public event EventHandler<StrategyCatalogChange>? Changed { add { } remove { } }
    }

    private sealed class TestMarketDataHub : IMarketDataHub
    {
        private readonly ConcurrentDictionary<InstrumentId, Subject<Quote>> _quotes = [];
        private readonly ConcurrentDictionary<InstrumentId, Subject<TradePrint>> _trades = [];
        private readonly ConcurrentDictionary<(InstrumentId, BarSize), Subject<OhlcvBar>> _bars = [];
        private readonly ConcurrentDictionary<InstrumentId, Subject<DepthSnapshot>> _depth = [];

        public IObservable<Quote> Quotes(InstrumentId instrumentId) =>
            _quotes.GetOrAdd(instrumentId, static _ => new Subject<Quote>());
        public IObservable<TradePrint> Trades(InstrumentId instrumentId) =>
            _trades.GetOrAdd(instrumentId, static _ => new Subject<TradePrint>());
        public IObservable<OhlcvBar> Bars(InstrumentId instrumentId, BarSize size) =>
            _bars.GetOrAdd((instrumentId, size), static _ => new Subject<OhlcvBar>());
        public IObservable<DepthSnapshot> Depth(InstrumentId instrumentId) =>
            _depth.GetOrAdd(instrumentId, static _ => new Subject<DepthSnapshot>());
        public void PublishQuote(Quote quote) =>
            _quotes.GetOrAdd(quote.InstrumentId, static _ => new Subject<Quote>()).OnNext(quote);
        public void PublishTrade(TradePrint trade) =>
            _trades.GetOrAdd(trade.InstrumentId, static _ => new Subject<TradePrint>()).OnNext(trade);
        public void PublishBar(OhlcvBar bar) =>
            _bars.GetOrAdd((bar.InstrumentId, bar.Size), static _ => new Subject<OhlcvBar>()).OnNext(bar);
        public void PublishDepth(InstrumentId instrumentId, DepthSnapshot snapshot) =>
            _depth.GetOrAdd(instrumentId, static _ => new Subject<DepthSnapshot>()).OnNext(snapshot);
    }

    private sealed class TextRecordingSurface : IRenderSurface
    {
        public List<string> Texts { get; } = [];
        public RenderViewport Viewport => new(960, 640, 1);
        public RenderCursor Cursor => new(0, 0, false, false);
        public RenderColor Theme(RenderThemeColor token) => new(128, 128, 128);
        public void SetStyle(RenderStyle style) { }
        public IDisposable Panel(string title, RenderPanelKind kind) => NoopDisposable.Instance;
        public void AxisX(double minimum, double maximum, string? format = null) { }
        public void AxisY(double minimum, double maximum, string? format = null) { }
        public IDisposable Series(string name, RenderSeriesKind kind) => NoopDisposable.Instance;
        public void Push(double x, double y) { }
        public void Line(double x1, double y1, double x2, double y2) { }
        public void Rect(double x, double y, double width, double height, bool filled = true) { }
        public void Text(double x, double y, string text) => Texts.Add(text);
        public void Marker(double x, double y, RenderMarkerShape shape) { }

        private sealed class NoopDisposable : IDisposable
        {
            public static NoopDisposable Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
