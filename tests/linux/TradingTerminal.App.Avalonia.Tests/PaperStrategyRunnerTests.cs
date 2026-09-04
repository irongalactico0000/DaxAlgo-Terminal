using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Avalonia.Headless.XUnit;
using TradingTerminal.App.Avalonia.Execution;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.Infrastructure.Execution;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.UI.Execution;
using TradingTerminal.UI.Logging;
using TradingTerminal.UI.Strategies;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class PaperStrategyRunnerTests
{
    [AvaloniaFact]
    public async Task Compiler_verified_authored_strategy_owns_feed_fills_paper_oms_and_restores_account()
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-compiled-paper-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var ledgerPath = Path.Combine(directory, "orders.db");
        var clock = new MutableClock(new DateTime(2026, 9, 4, 2, 0, 0, DateTimeKind.Utc));
        var instruments = new MemoryRegistry();
        var hub = new TestMarketDataHub();
        var ingest = new RecordingIngest();
        var secretStore = new FixedSecretStore();
        PaperExecutionInstrumentChoice selected;
        AuthoredUnitSpecificationV1 specification;
        try
        {
            using (var paper = PaperExecutionDesktopSession.CreateForLedger(
                       ledgerPath,
                       clock,
                       instruments,
                       secretStore))
            {
                selected = paper.Instruments.Last();
                specification = AuthoredStrategySpecification(selected);
                var source = CompiledPaperStrategySource(specification);
                var script = new StrategyScript(
                    specification.UnitId,
                    specification.Name,
                    [new StrategyFile("GeneratedPaperStrategy.cs", source)]);
                var compilation = new RoslynAuthoredUnitCompilerV1().Compile(specification, script);

                Assert.True(compilation.Success, string.Join(Environment.NewLine, compilation.Diagnostics));
                var compiled = Assert.IsType<CompiledAuthoredUnitV1>(compilation.Unit);
                var registration = new StrategyKernelRegistration(
                    specification.UnitId,
                    specification.Name,
                    specification.RawRequest,
                    () => (DaxAlgo.Sdk.IStrategyKernel)Activator.CreateInstance(compiled.RuntimeType)!,
                    specification,
                    compiled.Schema);
                var canonicalRegistry = new StrategyKernelRegistry();
                canonicalRegistry.Register(registration);

                using var viewModel = new PaperStrategyRunnerViewModel(
                    new TestStrategyRegistry(),
                    hub,
                    clock,
                    new InMemoryLogSink(),
                    paper,
                    instruments,
                    strategyKernelRegistry: canonicalRegistry,
                    marketDataIngest: ingest,
                    brokerSelector: new FixedBrokerSelector(BrokerKind.Simulated),
                    initialStrategy: registration);

                await viewModel.StartCommand.ExecuteAsync(null);
                global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                Assert.True(viewModel.IsRunning, viewModel.LastMessage);
                Assert.Collection(
                    ingest.Opened,
                    call => Assert.Equal(("market", BrokerKind.Simulated, (BarSize?)null), call),
                    call => Assert.Equal(("bars", BrokerKind.Simulated, (BarSize?)BarSize.OneMinute), call));

                hub.PublishQuote(new Quote(
                    selected.InstrumentId,
                    clock.UtcNow,
                    clock.UtcNow,
                    99.5,
                    100.5,
                    100,
                    100,
                    BrokerKind.Simulated,
                    1,
                    false));
                hub.PublishBar(new OhlcvBar(
                    selected.InstrumentId,
                    BarSize.OneMinute,
                    clock.UtcNow,
                    100,
                    101,
                    99,
                    100,
                    1000,
                    BrokerKind.Simulated,
                    true));

                var filled = await WaitUntilAsync(
                    () => paper.Client.GetSnapshot().Orders.SingleOrDefault()?.State == OrderLifecycleState.Filled,
                    TimeSpan.FromSeconds(5));
                Assert.True(filled, viewModel.LastMessage);
                var liveSnapshot = paper.Client.GetSnapshot();
                var order = Assert.Single(liveSnapshot.Orders);
                Assert.Equal(specification.UnitId, order.SubmitCommand.Metadata.StrategyId.Value);
                Assert.Equal(selected.InstrumentId, order.SubmitCommand.Metadata.InstrumentId);
                Assert.Equal(
                    ExecutionNumericBoundary.QuantityFromDecimal(2m),
                    Assert.Single(liveSnapshot.Economics.Positions).Quantity);

                await viewModel.StopCommand.ExecuteAsync(null);
                Assert.Equal(2, ingest.Disposed);
            }

            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            using var restored = PaperExecutionDesktopSession.CreateForLedger(
                ledgerPath,
                clock,
                instruments,
                secretStore);
            await restored.Client.RefreshAsync();
            var restoredSnapshot = restored.Client.GetSnapshot();
            Assert.Equal(OrderLifecycleState.Filled, Assert.Single(restoredSnapshot.Orders).State);
            var restoredPosition = Assert.Single(restoredSnapshot.Economics.Positions);
            Assert.Equal(selected.InstrumentId, restoredPosition.InstrumentId);
            Assert.Equal(ExecutionNumericBoundary.QuantityFromDecimal(2m), restoredPosition.Quantity);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Authored_canonical_strategy_owns_specified_feed_and_reaches_paper_oms()
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-authored-paper-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var clock = new MutableClock(new DateTime(2026, 9, 4, 0, 0, 0, DateTimeKind.Utc));
        var instruments = new MemoryRegistry();
        var hub = new TestMarketDataHub();
        var ingest = new RecordingIngest();
        try
        {
            using var paper = PaperExecutionDesktopSession.CreateForLedger(
                Path.Combine(directory, "orders.db"),
                clock,
                instruments,
                new FixedSecretStore());
            var selected = paper.Instruments.Last();
            var specification = AuthoredStrategySpecification(selected);
            var registration = new StrategyKernelRegistration(
                specification.UnitId,
                specification.Name,
                specification.RawRequest,
                () => new AuthoredBuyOnceKernel(),
                specification,
                StrategyParameterSchema.Empty);
            var canonicalRegistry = new StrategyKernelRegistry();
            canonicalRegistry.Register(registration);

            using var viewModel = new PaperStrategyRunnerViewModel(
                new TestStrategyRegistry(),
                hub,
                clock,
                new InMemoryLogSink(),
                paper,
                instruments,
                strategyKernelRegistry: canonicalRegistry,
                marketDataIngest: ingest,
                brokerSelector: new FixedBrokerSelector(BrokerKind.Simulated),
                initialStrategy: registration);

            Assert.Equal(registration.Id, viewModel.SelectedStrategy?.Id);
            Assert.Equal(selected.InstrumentId, viewModel.SelectedInstrument?.InstrumentId);
            Assert.False(viewModel.CanSelectInstrument);

            await viewModel.StartCommand.ExecuteAsync(null);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(viewModel.IsRunning, viewModel.LastMessage);
            Assert.Collection(
                ingest.Opened,
                call =>
                {
                    Assert.Equal("market", call.Kind);
                    Assert.Equal(BrokerKind.Simulated, call.Broker);
                    Assert.Null(call.Size);
                },
                call =>
                {
                    Assert.Equal("bars", call.Kind);
                    Assert.Equal(BrokerKind.Simulated, call.Broker);
                    Assert.Equal(BarSize.OneMinute, call.Size);
                });

            hub.PublishQuote(new Quote(
                selected.InstrumentId,
                clock.UtcNow,
                clock.UtcNow,
                99.5,
                100.5,
                100,
                100,
                BrokerKind.Simulated,
                1,
                false));

            hub.PublishBar(new OhlcvBar(
                selected.InstrumentId,
                BarSize.OneMinute,
                clock.UtcNow,
                100,
                101,
                99,
                100,
                1000,
                BrokerKind.Simulated,
                true));

            var completed = await WaitUntilAsync(
                () => paper.Client.GetSnapshot().Orders.SingleOrDefault()?.State == OrderLifecycleState.Filled,
                TimeSpan.FromSeconds(5));
            Assert.True(completed, viewModel.LastMessage);
            var order = Assert.Single(paper.Client.GetSnapshot().Orders);
            Assert.Equal(specification.UnitId, order.SubmitCommand.Metadata.StrategyId.Value);
            Assert.Equal(selected.InstrumentId, order.SubmitCommand.Metadata.InstrumentId);

            await viewModel.StopCommand.ExecuteAsync(null);
            Assert.Equal(2, ingest.Disposed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Authored_pair_strategy_owns_both_feeds_and_fills_both_paper_legs()
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-authored-pair-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var clock = new MutableClock(new DateTime(2026, 9, 4, 1, 0, 0, DateTimeKind.Utc));
        var instruments = new MemoryRegistry();
        var hub = new TestMarketDataHub();
        var ingest = new RecordingIngest();
        try
        {
            using var paper = PaperExecutionDesktopSession.CreateForLedger(
                Path.Combine(directory, "orders.db"),
                clock,
                instruments,
                new FixedSecretStore());
            var legs = paper.Instruments.Take(2).ToArray();
            Assert.Equal(2, legs.Length);
            var specification = AuthoredPairSpecification(legs[0], legs[1]);
            var registration = new StrategyKernelRegistration(
                specification.UnitId,
                specification.Name,
                specification.RawRequest,
                () => new AuthoredPairKernel(legs[0].InstrumentId, legs[1].InstrumentId),
                specification,
                StrategyParameterSchema.Empty);
            var canonicalRegistry = new StrategyKernelRegistry();
            canonicalRegistry.Register(registration);

            using var viewModel = new PaperStrategyRunnerViewModel(
                new TestStrategyRegistry(),
                hub,
                clock,
                new InMemoryLogSink(),
                paper,
                instruments,
                strategyKernelRegistry: canonicalRegistry,
                marketDataIngest: ingest,
                brokerSelector: new FixedBrokerSelector(BrokerKind.Simulated),
                initialStrategy: registration);

            Assert.Single(viewModel.Strategies);
            Assert.Equal(2, viewModel.StrategyLegs.Count);
            Assert.True(viewModel.IsMultiAssetStrategy);
            Assert.Equal(0, viewModel.UnsupportedMultiAssetStrategyCount);
            Assert.Contains(legs[0].Symbol, viewModel.AssetSummary, StringComparison.Ordinal);
            Assert.Contains(legs[1].Symbol, viewModel.AssetSummary, StringComparison.Ordinal);

            await viewModel.StartCommand.ExecuteAsync(null);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(viewModel.IsRunning, viewModel.LastMessage);
            Assert.Equal(4, ingest.Opened.Count);
            Assert.Equal(2, ingest.Opened.Count(call => call.Kind == "market"));
            Assert.Equal(2, ingest.Opened.Count(call => call.Kind == "bars"));

            foreach (var (leg, sequence) in legs.Select((leg, index) => (leg, index + 1L)))
            {
                hub.PublishQuote(new Quote(
                    leg.InstrumentId,
                    clock.UtcNow,
                    clock.UtcNow,
                    sequence == 1 ? 99.5 : 49.5,
                    sequence == 1 ? 100.5 : 50.5,
                    100,
                    100,
                    BrokerKind.Simulated,
                    sequence,
                    false));
                hub.PublishBar(new OhlcvBar(
                    leg.InstrumentId,
                    BarSize.OneMinute,
                    clock.UtcNow.AddSeconds(sequence),
                    sequence == 1 ? 100 : 50,
                    sequence == 1 ? 101 : 51,
                    sequence == 1 ? 99 : 49,
                    sequence == 1 ? 100 : 50,
                    1000,
                    BrokerKind.Simulated,
                    true));
            }

            var completed = await WaitUntilAsync(
                () =>
                {
                    var snapshot = paper.Client.GetSnapshot();
                    return snapshot.Orders.Count == 2 &&
                           snapshot.Orders.All(order => order.State == OrderLifecycleState.Filled) &&
                           snapshot.Economics.Positions.Count == 2;
                },
                TimeSpan.FromSeconds(5));
            Assert.True(completed, viewModel.LastMessage);

            var snapshot = paper.Client.GetSnapshot();
            Assert.Equal(
                legs.Select(static leg => leg.InstrumentId).OrderBy(static id => id.Value),
                snapshot.Economics.Positions.Select(static position => position.InstrumentId)
                    .OrderBy(static id => id.Value));
            Assert.Contains(snapshot.Economics.Positions, position =>
                position.InstrumentId == legs[0].InstrumentId &&
                position.Quantity == ExecutionNumericBoundary.QuantityFromDecimal(2m));
            Assert.Contains(snapshot.Economics.Positions, position =>
                position.InstrumentId == legs[1].InstrumentId &&
                position.Quantity == ExecutionNumericBoundary.QuantityFromDecimal(-3m));
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.All(viewModel.StrategyLegs, static leg => Assert.NotEqual("0", leg.Position));

            await viewModel.StopCommand.ExecuteAsync(null);
            Assert.Equal(4, ingest.Disposed);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Catalog_strategy_reaches_durable_paper_oms_through_authenticated_ipc()
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-paper-strategy-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var clock = new MutableClock(new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc));
        var instruments = new MemoryRegistry();
        var hub = new TestMarketDataHub();
        try
        {
            using var paper = PaperExecutionDesktopSession.CreateForLedger(
                Path.Combine(directory, "orders.db"),
                clock,
                instruments,
                new FixedSecretStore());
            var option = new BacktestStrategyOption(
                "paper-strategy-test",
                "Paper strategy test",
                contract => new BuyOnceStrategy(contract));
            var pairOption = new BacktestStrategyOption(
                "pair-strategy-test",
                "Pair strategy test",
                contract => new BuyOnceStrategy(contract))
            {
                Schema = new StrategyParameterSchema(
                    StrategyParameter.Instrument("leg-a", "Leg A", new InstrumentId(1)),
                    StrategyParameter.Instrument("leg-b", "Leg B", new InstrumentId(2))),
            };
            var strategies = new TestStrategyRegistry(option, pairOption);
            using var viewModel = new PaperStrategyRunnerViewModel(
                strategies,
                hub,
                clock,
                new InMemoryLogSink(),
                paper,
                instruments);

            var selectedInstrument = viewModel.Instruments.Last();
            viewModel.SelectedInstrument = selectedInstrument;
            Assert.Same(selectedInstrument, viewModel.SelectedInstrument);
            Assert.Single(viewModel.Strategies);
            Assert.Equal(1, viewModel.UnsupportedMultiAssetStrategyCount);
            Assert.Contains("hidden", viewModel.StrategyEligibilitySummary, StringComparison.OrdinalIgnoreCase);
            await viewModel.StartCommand.ExecuteAsync(null);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.True(viewModel.IsRunning, viewModel.LastMessage);

            var instrument = viewModel.SelectedInstrument!.InstrumentId;
            hub.PublishQuote(new Quote(
                instrument,
                clock.UtcNow,
                clock.UtcNow,
                100,
                100,
                10,
                10,
                BrokerKind.Simulated,
                1,
                false));

            var completed = await WaitUntilAsync(
                () => paper.Client.GetSnapshot().Orders.SingleOrDefault()?.State == OrderLifecycleState.Filled,
                TimeSpan.FromSeconds(5));
            Assert.True(
                completed,
                $"{viewModel.LastMessage} Runtime={viewModel.RuntimeState}; " +
                $"model-position={viewModel.ModelPosition}; model-target={viewModel.ModelTarget}; " +
                $"alert={viewModel.LastAlert}; orders={paper.Client.GetSnapshot().Orders.Count}.");

            var snapshot = paper.Client.GetSnapshot();
            var order = Assert.Single(snapshot.Orders);
            Assert.Equal(selectedInstrument.InstrumentId, order.SubmitCommand.Metadata.InstrumentId);
            Assert.Equal("paper-strategy-test", order.SubmitCommand.Metadata.StrategyId.Value);
            Assert.Equal("paper-strategy-client-1", order.ClientOrderId.Value);
            Assert.Equal(TradeIntentQuantityMode.TargetPosition, order.Instruction.TradeIntent.QuantityMode);
            Assert.Equal(ExecutionNumericBoundary.QuantityFromDecimal(2m), snapshot.Economics.Positions.Single().Quantity);
            global::Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Contains("authenticated IPC", viewModel.LastMessage, StringComparison.OrdinalIgnoreCase);

            await viewModel.StopCommand.ExecuteAsync(null);
            Assert.True(viewModel.IsStopped);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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

    private sealed class BuyOnceStrategy(Contract contract) : IBacktestStrategy
    {
        private int _submitted;

        public Task OnStartAsync(IClock clock, IOrderRouter router, CancellationToken ct) => Task.CompletedTask;

        public async Task OnTickAsync(Tick tick, IClock clock, IOrderRouter router, CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _submitted, 1) != 0) return;
            await router.PlaceOrderAsync(new OrderRequest(
                "legacy-buy-once",
                contract,
                OrderSide.Buy,
                OrderType.Market,
                2), ct);
        }

        public Task OnOrderEventAsync(OrderEvent evt, CancellationToken ct) => Task.CompletedTask;
        public Task OnEndAsync(IClock clock, IOrderRouter router, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class AuthoredBuyOnceKernel : DaxAlgo.Sdk.IStrategyKernel
    {
        private int _submitted;
        public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
        public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
        public Task OnStartAsync(DaxAlgo.Sdk.IStrategyRuntimeContext context, CancellationToken ct) =>
            Task.CompletedTask;
        public Task OnBarAsync(OhlcvBar bar, DaxAlgo.Sdk.IStrategyRuntimeContext context, CancellationToken ct)
        {
            if (Interlocked.Exchange(ref _submitted, 1) == 0)
                context.Book.SetTargetPosition(bar.InstrumentId, 2);
            return Task.CompletedTask;
        }
    }

    private sealed class AuthoredPairKernel(InstrumentId legA, InstrumentId legB) : DaxAlgo.Sdk.IStrategyKernel
    {
        private int _submitted;
        public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
        public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
        public Task OnStartAsync(DaxAlgo.Sdk.IStrategyRuntimeContext context, CancellationToken ct) =>
            Task.CompletedTask;
        public Task OnBarAsync(OhlcvBar bar, DaxAlgo.Sdk.IStrategyRuntimeContext context, CancellationToken ct)
        {
            if (bar.InstrumentId == legB && Interlocked.Exchange(ref _submitted, 1) == 0)
            {
                context.Book.SetTargetPosition(legA, 2);
                context.Book.SetTargetPosition(legB, -3);
            }
            return Task.CompletedTask;
        }
    }

    private static AuthoredUnitSpecificationV1 AuthoredStrategySpecification(
        PaperExecutionInstrumentChoice instrument)
    {
        var classification = new StrategyClassificationBindingV1("buy-once", new string('a', 64));
        return new(
        AuthoredUnitSpecificationV1.CurrentSchemaVersion,
        "authored-buy-once",
        "Authored buy once",
        "Buy two units on the first one-minute bar and show the chart.",
        AuthoredUnitSourceKindV1.Text,
        AuthoredUnitKindV1.Strategy,
        [new AuthoredInstrumentRequestV1(
            "primary",
            instrument.Symbol,
            instrument.InstrumentId,
            ExpectedAssetClass: null,
            PreferredBroker: BrokerKind.Simulated)],
        new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
        StrategyDataRequirement.Bars,
        [],
        new AuthoredChartCompositionV1(
            [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0)],
            [new AuthoredChartLayerV1(
                "candles",
                "price",
                AuthoredChartLayerKindV1.Candles,
                "price.candles@1",
                new Dictionary<string, string>())]),
        [],
        [],
        AuthoredUnitExecutionIntentV1.PaperTargets,
        classification,
        AuthoredStrategyIntentFixture.Create(classification));
    }

    private static string CompiledPaperStrategySource(AuthoredUnitSpecificationV1 specification)
    {
        var specificationHash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        return $$"""
            public sealed class GeneratedPaperStrategy : IStrategyKernel, IAuthoredDrawingManifest
            {
                private int _submitted;
                private double? _lastClose;
                private readonly List<OhlcvBar> _bars = new();

                public static string SpecificationHashSha256 => "{{specificationHash}}";
                public IReadOnlyList<string> DrawingLayerTypeIds => new[] { "price.candles@1" };
                public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
                public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;

                public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) =>
                    Task.CompletedTask;

                public Task OnBarAsync(
                    OhlcvBar bar,
                    IStrategyRuntimeContext context,
                    CancellationToken ct)
                {
                    _lastClose = bar.Close;
                    _bars.Add(bar);
                    if (_bars.Count > 256) _bars.RemoveAt(0);
                    if (Interlocked.Exchange(ref _submitted, 1) == 0)
                        context.Book.SetTargetPosition(bar.InstrumentId, 2);
                    return Task.CompletedTask;
                }

                public void Draw(IRenderSurface surface)
                {
                    if (_bars.Count == 0)
                    {
                        surface.Text(8, 18, "Waiting for completed bars");
                        return;
                    }

                    using (surface.Layer("candles", "price.candles@1"))
                        Candles.Draw(surface, _bars);
                }
            }
            """;
    }

    private static AuthoredUnitSpecificationV1 AuthoredPairSpecification(
        PaperExecutionInstrumentChoice legA,
        PaperExecutionInstrumentChoice legB)
    {
        var classification = new StrategyClassificationBindingV1("pair", new string('b', 64));
        return new(
        AuthoredUnitSpecificationV1.CurrentSchemaVersion,
        "authored-pair",
        "Authored pair",
        "Trade a two-asset pair and show both legs.",
        AuthoredUnitSourceKindV1.Text,
        AuthoredUnitKindV1.Strategy,
        [
            new AuthoredInstrumentRequestV1(
                "long-leg",
                legA.Symbol,
                legA.InstrumentId,
                ExpectedAssetClass: null,
                PreferredBroker: BrokerKind.Simulated),
            new AuthoredInstrumentRequestV1(
                "short-leg",
                legB.Symbol,
                legB.InstrumentId,
                ExpectedAssetClass: null,
                PreferredBroker: BrokerKind.Simulated),
        ],
        new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
        StrategyDataRequirement.Bars,
        [],
        new AuthoredChartCompositionV1(
            [new AuthoredChartPaneV1("normalized", AuthoredChartPaneRoleV1.Price, 0)],
            [new AuthoredChartLayerV1(
                "pair-lines",
                "normalized",
                AuthoredChartLayerKindV1.IndicatorLine,
                "pair.normalized@1",
                new Dictionary<string, string>())]),
        [],
        [],
        AuthoredUnitExecutionIntentV1.PaperTargets,
        classification,
        AuthoredStrategyIntentFixture.Create(classification, StrategyIntentKindV1.MultiLegTarget));
    }

    private sealed class RecordingIngest : IMarketDataIngest
    {
        public List<(string Kind, BrokerKind Broker, BarSize? Size)> Opened { get; } = [];
        public int Disposed { get; private set; }
        public InstrumentId Resolve(Contract contract, BrokerKind broker) => throw new NotSupportedException();
        public IDisposable Subscribe(Contract contract, BrokerKind broker) => Add("market", broker, null);
        public IDisposable SubscribeBars(Contract contract, BrokerKind broker, BarSize size) => Add("bars", broker, size);
        public IDisposable SubscribeTrades(Contract contract, BrokerKind broker) => Add("trades", broker, null);
        private IDisposable Add(string kind, BrokerKind broker, BarSize? size)
        {
            Opened.Add((kind, broker, size));
            return new ActionDisposable(() => Disposed++);
        }
    }

    private sealed class ActionDisposable(Action action) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) action();
        }
    }

    private sealed class FixedBrokerSelector(BrokerKind kind) : IBrokerSelector
    {
        private readonly IBrokerClient _client = new FixedBrokerClient(kind);
        public IReadOnlyList<BrokerKind> AvailableKinds => [kind];
        public bool IsAvailable(BrokerKind candidate) => candidate == kind;
        public IReadOnlyList<BrokerKind> Connected => [kind];
        public bool IsConnected(BrokerKind candidate) => candidate == kind;
        public IBrokerClient Get(BrokerKind candidate) => candidate == kind ? _client : throw new KeyNotFoundException();
        public BrokerConnectionMode ModeOf(BrokerKind candidate) => new(candidate, false, "Test", "Test");
        public IObservable<ConnectionState> StateOf(BrokerKind candidate) =>
            new BehaviorSubject<ConnectionState>(CurrentStateOf(candidate));
        public ConnectionState CurrentStateOf(BrokerKind candidate) =>
            candidate == kind ? ConnectionState.Connected : ConnectionState.Disconnected;
        public event EventHandler<BrokerStateChangedEventArgs>? StateChanged;
        public Task ConnectAsync(BrokerKind candidate, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(BrokerKind candidate, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FixedBrokerClient(BrokerKind kind) : IBrokerClient
    {
        public BrokerKind Kind => kind;
        public IObservable<ConnectionState> ConnectionState { get; } =
            new BehaviorSubject<ConnectionState>(TradingTerminal.Core.Domain.ConnectionState.Connected);
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

    private sealed class TestStrategyRegistry(params BacktestStrategyOption[] options) : IBacktestStrategyRegistry
    {
        private readonly Dictionary<string, BacktestStrategyOption> _options = options.ToDictionary(item => item.Id);
        public IReadOnlyList<BacktestStrategyOption> All => _options.Values.ToArray();
        public BacktestStrategyOption? Find(string id) => _options.GetValueOrDefault(id);
        public void Register(BacktestStrategyOption option) { _options[option.Id] = option; Changed?.Invoke(this, EventArgs.Empty); }
        public bool Remove(string id) { var removed = _options.Remove(id); if (removed) Changed?.Invoke(this, EventArgs.Empty); return removed; }
        public event EventHandler? Changed;
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class FixedSecretStore : IExecutionServiceSecretStore
    {
        private readonly byte[] _secret = Enumerable.Range(0, ExecutionIpcProtocol.SecretSize)
            .Select(value => checked((byte)value))
            .ToArray();
        public byte[] LoadOrCreate() => (byte[])_secret.Clone();
    }

    private sealed class TestMarketDataHub : IMarketDataHub
    {
        private readonly ConcurrentDictionary<InstrumentId, Subject<Quote>> _quotes = [];
        private readonly ConcurrentDictionary<InstrumentId, Subject<TradePrint>> _trades = [];
        private readonly ConcurrentDictionary<(InstrumentId, BarSize), Subject<OhlcvBar>> _bars = [];
        private readonly ConcurrentDictionary<InstrumentId, Subject<DepthSnapshot>> _depth = [];

        public IObservable<Quote> Quotes(InstrumentId instrumentId) => _quotes.GetOrAdd(instrumentId, _ => new Subject<Quote>());
        public IObservable<TradePrint> Trades(InstrumentId instrumentId) => _trades.GetOrAdd(instrumentId, _ => new Subject<TradePrint>());
        public IObservable<OhlcvBar> Bars(InstrumentId instrumentId, BarSize size) => _bars.GetOrAdd((instrumentId, size), _ => new Subject<OhlcvBar>());
        public IObservable<DepthSnapshot> Depth(InstrumentId instrumentId) => _depth.GetOrAdd(instrumentId, _ => new Subject<DepthSnapshot>());
        public void PublishQuote(Quote quote) => _quotes.GetOrAdd(quote.InstrumentId, _ => new Subject<Quote>()).OnNext(quote);
        public void PublishTrade(TradePrint trade) => _trades.GetOrAdd(trade.InstrumentId, _ => new Subject<TradePrint>()).OnNext(trade);
        public void PublishBar(OhlcvBar bar) => _bars.GetOrAdd((bar.InstrumentId, bar.Size), _ => new Subject<OhlcvBar>()).OnNext(bar);
        public void PublishDepth(InstrumentId instrumentId, DepthSnapshot snapshot) => _depth.GetOrAdd(instrumentId, _ => new Subject<DepthSnapshot>()).OnNext(snapshot);
    }

    private sealed class MemoryRegistry : IInstrumentRegistry
    {
        private readonly Dictionary<int, Instrument> _instruments = [];
        private readonly Dictionary<(BrokerKind Broker, string Symbol), InstrumentId> _aliases = [];
        private int _next;

        public Instrument? Get(InstrumentId id) => _instruments.GetValueOrDefault(id.Value);
        public InstrumentId? Resolve(BrokerKind broker, string brokerSymbol) =>
            _aliases.TryGetValue((broker, brokerSymbol), out var value) ? value : null;
        public InstrumentId ResolveOrCreate(Contract contract, BrokerKind broker)
        {
            if (_aliases.TryGetValue((broker, contract.Symbol), out var existing)) return existing;
            var id = new InstrumentId(++_next);
            _instruments.Add(id.Value, new Instrument(
                id, contract.Symbol, AssetClass.Equity, contract.PrimaryExchange,
                contract.Currency, 0.01, 1));
            _aliases.Add((broker, contract.Symbol), id);
            return id;
        }
        public string? ToBrokerSymbol(InstrumentId id, BrokerKind broker) =>
            _aliases.FirstOrDefault(item => item.Key.Broker == broker && item.Value == id).Key.Symbol;
        public void RegisterAlias(InstrumentAlias alias) => _aliases[(alias.Broker, alias.BrokerSymbol)] = alias.InstrumentId;
        public IReadOnlyList<Instrument> All() => _instruments.Values.OrderBy(item => item.Id.Value).ToArray();
    }
}
