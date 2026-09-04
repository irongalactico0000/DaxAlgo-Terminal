using System.Reactive.Subjects;
using DaxAlgo.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using TradingTerminal.Backtest;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Risk;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.Sandbox;
using TradingTerminal.UI.Strategies;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class CanonicalQuickBacktestTests
{
    private static readonly InstrumentId InstrumentId = new(73);
    private static readonly InstrumentId PairInstrumentId = new(74);

    [Fact]
    public async Task Catalog_sdk_strategy_keeps_reviewed_market_identity_and_enters_risk_gated_backtest_session()
    {
        var kernel = new ParameterCaptureBarKernel();
        var registration = Registration(kernel);
        var kernels = new StrategyKernelRegistry();
        kernels.Register(registration);
        var session = new CapturingSession();
        var registry = new MemoryInstrumentRegistry(new Instrument(
            InstrumentId, "SPY", AssetClass.Equity, "ARCA", "USD", 0.01d, 1d));
        var viewModel = new QuickBacktestViewModel(
            new EmptyLegacyRegistry(),
            session,
            new FixedBrokerSelector(new HistoricalBarClient()),
            kernels,
            registry,
            NullLogger<QuickBacktestViewModel>.Instance);

        var initialized = viewModel.Initialize(registration);
        Assert.True(initialized);
        Assert.True(viewModel.IsAuthoredStrategy);
        Assert.False(viewModel.CanSelectInstrument);
        Assert.False(viewModel.CanSelectBarSize);
        Assert.Equal(BarSize.OneMinute, viewModel.SelectedBarSize);
        Assert.Equal(BrokerKind.Simulated, viewModel.SelectedBroker);
        Assert.Equal("SPY", viewModel.SelectedInstrument?.Contract.Symbol);
        var parameter = Assert.Single(viewModel.EditableParameters);
        Assert.Equal("fastPeriod", parameter.Key);
        parameter.NumberValue = 21d;

        await viewModel.RunAsync();
        var invocation = await session.Invocation.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(BarSize.OneMinute, invocation.Config.ReplayBarSize);
        Assert.Equal(2, invocation.Config.ReplayBars?.Count);
        Assert.IsType<SdkStrategyBacktestAdapter>(invocation.Strategy);
        Assert.NotNull(invocation.Risk);
        Assert.Equal(21, kernel.FastPeriodAtStart);
    }

    [Fact]
    public async Task Catalog_pair_strategy_fetches_every_reviewed_history_and_builds_one_multi_series_replay()
    {
        var registration = PairRegistration(new EmptyPairKernel());
        Assert.True(new StrategyCatalogItemViewModel(registration).HasQuickBacktest);
        var kernels = new StrategyKernelRegistry();
        kernels.Register(registration);
        var session = new CapturingSession();
        var client = new HistoricalBarClient();
        var registry = new MultiInstrumentRegistry([
            new Instrument(InstrumentId, "SPY", AssetClass.Equity, "ARCA", "USD", 0.01d, 1d),
            new Instrument(PairInstrumentId, "QQQ", AssetClass.Equity, "NASDAQ", "USD", 0.01d, 1d),
        ]);
        var viewModel = new QuickBacktestViewModel(
            new EmptyLegacyRegistry(),
            session,
            new FixedBrokerSelector(client),
            kernels,
            registry,
            NullLogger<QuickBacktestViewModel>.Instance);

        Assert.True(viewModel.Initialize(registration));
        Assert.Equal("SPY · QQQ", viewModel.ReviewedInstrumentSummary);
        Assert.Equal(2, viewModel.Instruments.Count);
        Assert.False(viewModel.CanSelectInstrument);

        await viewModel.RunAsync();
        var invocation = await session.Invocation.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Null(invocation.Config.ReplayBars);
        Assert.Equal(2, invocation.Config.ReplayBarSeries?.Count);
        Assert.Equal(["QQQ", "SPY"], invocation.Config.ReplayBarSeries!
            .Select(series => series.Contract.Symbol).Order().ToArray());
        Assert.Equal(["QQQ", "SPY"], client.RequestedSymbols.Order().ToArray());
        Assert.Contains("2/2 UTC bar boundaries", viewModel.FeedQuality, StringComparison.Ordinal);
        Assert.Contains("not forward-filled", viewModel.FeedQuality, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Catalog_pair_strategy_stops_when_one_history_has_a_missing_boundary()
    {
        var registration = PairRegistration(new EmptyPairKernel());
        var kernels = new StrategyKernelRegistry();
        kernels.Register(registration);
        var session = new CapturingSession();
        var client = new HistoricalBarClient { OmitSecondQqqBar = true };
        var registry = new MultiInstrumentRegistry([
            new Instrument(InstrumentId, "SPY", AssetClass.Equity, "ARCA", "USD", 0.01d, 1d),
            new Instrument(PairInstrumentId, "QQQ", AssetClass.Equity, "NASDAQ", "USD", 0.01d, 1d),
        ]);
        var viewModel = new QuickBacktestViewModel(
            new EmptyLegacyRegistry(),
            session,
            new FixedBrokerSelector(client),
            kernels,
            registry,
            NullLogger<QuickBacktestViewModel>.Instance);

        Assert.True(viewModel.Initialize(registration));
        await viewModel.RunAsync();

        Assert.False(session.Invocation.Task.IsCompleted);
        Assert.Contains("stopped instead of forward-filling", viewModel.Status, StringComparison.Ordinal);
        Assert.Contains("1/2 UTC bar boundaries", viewModel.FeedQuality, StringComparison.Ordinal);
    }

    private static StrategyKernelRegistration Registration(ParameterCaptureBarKernel kernel)
    {
        var schema = new StrategyParameterSchema(
            StrategyParameter.Int("fastPeriod", "Fast EMA", 9, min: 2, max: 100));
        var classification = new StrategyClassificationBindingV1("bar-signal", new string('a', 64));
        var specification = new AuthoredUnitSpecificationV1(
            AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            "spy-bars-paper",
            "SPY Bars Paper",
            "Trade SPY from completed one-minute bars.",
            AuthoredUnitSourceKindV1.Text,
            AuthoredUnitKindV1.Strategy,
            [new AuthoredInstrumentRequestV1(
                "primary", "SPY", InstrumentId, AssetClass.Equity, BrokerKind.Simulated)],
            new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
            StrategyDataRequirement.Bars,
            [new AuthoredUnitParameterV1(
                "fastPeriod", "Fast EMA", ParameterKind.Integer, "9", "2", "100", [], null, null)],
            new AuthoredChartCompositionV1([], []),
            [],
            [],
            AuthoredUnitExecutionIntentV1.PaperTargets,
            classification,
            AuthoredStrategyIntentFixture.Create(classification));
        return new StrategyKernelRegistration(
            specification.UnitId,
            specification.Name,
            specification.RawRequest,
            () => kernel,
            specification,
            schema);
    }

    private static StrategyKernelRegistration PairRegistration(IStrategyKernel kernel)
    {
        var classification = new StrategyClassificationBindingV1("pair", new string('b', 64));
        var specification = new AuthoredUnitSpecificationV1(
            AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            "spy-qqq-pair",
            "SPY QQQ Pair",
            "Trade a SPY and QQQ pair from completed one-minute bars.",
            AuthoredUnitSourceKindV1.Text,
            AuthoredUnitKindV1.Strategy,
            [
                new AuthoredInstrumentRequestV1("first", "SPY", InstrumentId, AssetClass.Equity, BrokerKind.Simulated),
                new AuthoredInstrumentRequestV1("second", "QQQ", PairInstrumentId, AssetClass.Equity, BrokerKind.Simulated),
            ],
            new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
            StrategyDataRequirement.Bars,
            [],
            new AuthoredChartCompositionV1([], []),
            [],
            [],
            AuthoredUnitExecutionIntentV1.PaperTargets,
            classification,
            AuthoredStrategyIntentFixture.Create(classification, StrategyIntentKindV1.MultiLegTarget));
        return new StrategyKernelRegistration(
            specification.UnitId,
            specification.Name,
            specification.RawRequest,
            () => kernel,
            specification,
            StrategyParameterSchema.Empty);
    }

    private sealed class ParameterCaptureBarKernel : IStrategyKernel
    {
        public StrategyParameterSchema Schema { get; } = new(
            StrategyParameter.Int("fastPeriod", "Fast EMA", 9, min: 2, max: 100));
        public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
        public int FastPeriodAtStart { get; private set; }
        public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct)
        {
            FastPeriodAtStart = context.Parameters.GetInt("fastPeriod");
            return Task.CompletedTask;
        }
    }

    private sealed class EmptyPairKernel : IStrategyKernel
    {
        public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
        public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
        public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class CapturingSession : IBacktestSession
    {
        public TaskCompletionSource<Invocation> Invocation { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<BacktestResult> RunAsync(
            BacktestConfig config,
            IBacktestStrategy strategy,
            IRiskManager? risk = null,
            CancellationToken ct = default)
        {
            Invocation.TrySetResult(new Invocation(config, strategy, risk));
            return await new BacktestSession().RunAsync(config, strategy, risk, ct);
        }
    }

    private sealed record Invocation(
        BacktestConfig Config,
        IBacktestStrategy Strategy,
        IRiskManager? Risk);

    private sealed class HistoricalBarClient : IBrokerClient
    {
        public List<string> RequestedSymbols { get; } = [];
        public bool OmitSecondQqqBar { get; init; }
        public BrokerKind Kind => BrokerKind.Simulated;
        public IObservable<ConnectionState> ConnectionState { get; } =
            new BehaviorSubject<ConnectionState>(TradingTerminal.Core.Domain.ConnectionState.Connected);
        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TradableInstrument>> ListInstrumentsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TradableInstrument>>([]);
        public Task<IReadOnlyList<Bar>> RequestHistoricalBarsAsync(
            Contract contract, BarSize barSize, TimeSpan duration, CancellationToken ct = default)
        {
            RequestedSymbols.Add(contract.Symbol);
            var start = new DateTime(2026, 1, 2, 14, 30, 0, DateTimeKind.Utc);
            var basis = contract.Symbol == "QQQ" ? 300d : 100d;
            IReadOnlyList<Bar> bars = [
                new Bar(start, basis, basis + 1d, basis - 1d, basis + 0.5d, 1_000L),
                new Bar(start.AddMinutes(1), basis + 0.5d, basis + 2d, basis, basis + 1.5d, 1_200L),
            ];
            if (OmitSecondQqqBar && contract.Symbol == "QQQ")
                bars = [bars[0]];
            return Task.FromResult(bars);
        }
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

    private sealed class FixedBrokerSelector(IBrokerClient client) : IBrokerSelector
    {
        public IReadOnlyList<BrokerKind> AvailableKinds => [client.Kind];
        public bool IsAvailable(BrokerKind kind) => kind == client.Kind;
        public IReadOnlyList<BrokerKind> Connected => [client.Kind];
        public bool IsConnected(BrokerKind kind) => kind == client.Kind;
        public IBrokerClient Get(BrokerKind kind) => kind == client.Kind
            ? client
            : throw new KeyNotFoundException(kind.ToString());
        public BrokerConnectionMode ModeOf(BrokerKind kind) => new(kind, false, "Test", "Test");
        public IObservable<ConnectionState> StateOf(BrokerKind kind) =>
            new BehaviorSubject<ConnectionState>(CurrentStateOf(kind));
        public ConnectionState CurrentStateOf(BrokerKind kind) => kind == client.Kind
            ? TradingTerminal.Core.Domain.ConnectionState.Connected
            : TradingTerminal.Core.Domain.ConnectionState.Disconnected;
        public event EventHandler<BrokerStateChangedEventArgs>? StateChanged;
        public Task ConnectAsync(BrokerKind kind, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(BrokerKind kind, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class MemoryInstrumentRegistry(Instrument instrument) : IInstrumentRegistry
    {
        public Instrument? Get(InstrumentId id) => id == instrument.Id ? instrument : null;
        public InstrumentId? Resolve(BrokerKind broker, string brokerSymbol) =>
            broker == BrokerKind.Simulated && brokerSymbol == instrument.CanonicalSymbol ? instrument.Id : null;
        public InstrumentId ResolveOrCreate(Contract contract, BrokerKind broker) => instrument.Id;
        public string? ToBrokerSymbol(InstrumentId id, BrokerKind broker) =>
            id == instrument.Id && broker == BrokerKind.Simulated ? instrument.CanonicalSymbol : null;
        public void RegisterAlias(InstrumentAlias alias) { }
        public IReadOnlyList<Instrument> All() => [instrument];
    }

    private sealed class MultiInstrumentRegistry(IReadOnlyList<Instrument> instruments) : IInstrumentRegistry
    {
        public Instrument? Get(InstrumentId id) => instruments.SingleOrDefault(item => item.Id == id);
        public InstrumentId? Resolve(BrokerKind broker, string brokerSymbol) =>
            instruments.SingleOrDefault(item => item.CanonicalSymbol == brokerSymbol)?.Id;
        public InstrumentId ResolveOrCreate(Contract contract, BrokerKind broker) =>
            instruments.Single(item => item.CanonicalSymbol == contract.Symbol).Id;
        public string? ToBrokerSymbol(InstrumentId id, BrokerKind broker) => Get(id)?.CanonicalSymbol;
        public void RegisterAlias(InstrumentAlias alias) { }
        public IReadOnlyList<Instrument> All() => instruments;
    }

    private sealed class EmptyLegacyRegistry : IBacktestStrategyRegistry
    {
        public IReadOnlyList<BacktestStrategyOption> All => [];
        public BacktestStrategyOption? Find(string id) => null;
        public void Register(BacktestStrategyOption option) { }
        public bool Remove(string id) => false;
        public event EventHandler? Changed;
    }
}
