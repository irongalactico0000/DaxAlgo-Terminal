using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using DaxAlgo.Sdk;
using TradingTerminal.App.Avalonia.Execution;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Time;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.Infrastructure.Execution;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.UI.Execution;
using TradingTerminal.UI.Logging;
using TradingTerminal.UI.Strategies;

namespace TradingTerminal.App.Avalonia.Diagnostics;

/// <summary>
/// Real Mac / CI smoke: Paper Strategy Runner Start → simulated bar → visible BOOK POSITION qty.
/// Invoke with <c>--smoke-paper-handoff</c> (optional <c>--smoke-paper-handoff=/path/report.txt</c>).
/// </summary>
internal static class PaperHandoffSmoke
{
    public static async Task<int> RunAsync(IServiceProvider services, string reportPath)
    {
        var lines = new List<string>
        {
            $"Paper handoff smoke — {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "Flow: Paper runner window → Start → fill → visible BookPosition",
            string.Empty,
        };

        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-paper-handoff-smoke", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var exitCode = 1;
        PaperStrategyRunnerViewModel? viewModel = null;
        PaperStrategyRunnerWindow? window = null;
        PaperExecutionDesktopSession? paper = null;
        try
        {
            var clock = new FixedClock(new DateTime(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc));
            var instruments = new SmokeInstrumentRegistry();
            instruments.ResolveOrCreate(Contract.UsStock("AAPL", "NASDAQ"), BrokerKind.Simulated);
            var hub = services.GetRequiredService<IMarketDataHub>();
            var log = services.GetRequiredService<InMemoryLogSink>();
            var secretStore = services.GetService<IExecutionServiceSecretStore>()
                ?? new MacExecutionServiceSecretStore();
            paper = PaperExecutionDesktopSession.CreateForLedger(
                Path.Combine(directory, "orders.db"),
                clock,
                instruments,
                secretStore);

            var selected = paper.Instruments.Single(item =>
                string.Equals(item.Symbol, "AAPL", StringComparison.OrdinalIgnoreCase));
            var specification = BuildSpecification(selected);
            var source = BuildSource(specification);
            var script = new StrategyScript(
                specification.UnitId,
                specification.Name,
                [new StrategyFile("GeneratedPaperStrategy.cs", source)]);
            var compilation = new RoslynAuthoredUnitCompilerV1().Compile(specification, script);
            if (!compilation.Success || compilation.Unit is not CompiledAuthoredUnitV1 compiled)
            {
                lines.Add("FAIL  smoke strategy compile: " +
                          (compilation.Diagnostics.FirstOrDefault()?.Message ?? "unknown"));
            }
            else
            {
                var registration = new StrategyKernelRegistration(
                    specification.UnitId,
                    specification.Name,
                    specification.RawRequest,
                    () => (IStrategyKernel)Activator.CreateInstance(compiled.RuntimeType)!,
                    specification,
                    compiled.Schema);
                var kernelRegistry = new StrategyKernelRegistry();
                kernelRegistry.Register(registration);

                viewModel = new PaperStrategyRunnerViewModel(
                    services.GetRequiredService<IBacktestStrategyRegistry>(),
                    hub,
                    clock,
                    log,
                    paper,
                    instruments,
                    strategyKernelRegistry: kernelRegistry,
                    marketDataIngest: new SmokeMarketDataIngest(),
                    brokerSelector: new SmokeBrokerSelector(BrokerKind.Simulated),
                    initialStrategy: registration);

                window = new PaperStrategyRunnerWindow { DataContext = viewModel };
                window.Show();
                await PumpAsync().ConfigureAwait(true);

                await viewModel.StartCommand.ExecuteAsync(null).ConfigureAwait(true);
                await PumpAsync().ConfigureAwait(true);

                hub.PublishQuote(new Quote(
                    selected.InstrumentId, clock.UtcNow, clock.UtcNow,
                    99.5, 100.5, 100, 100, BrokerKind.Simulated, 1, false));
                hub.PublishBar(new OhlcvBar(
                    selected.InstrumentId, BarSize.OneMinute, clock.UtcNow,
                    100, 101, 99, 100, 1000, BrokerKind.Simulated, true));

                var deadline = DateTime.UtcNow.AddSeconds(12);
                while (DateTime.UtcNow < deadline)
                {
                    await PumpAsync().ConfigureAwait(true);
                    if (string.Equals(viewModel.BookPosition, "2", StringComparison.Ordinal))
                        break;
                    await Task.Delay(150).ConfigureAwait(true);
                }

                var qtyOk = string.Equals(viewModel.BookPosition, "2", StringComparison.Ordinal);
                lines.Add(qtyOk
                    ? $"PASS  BOOK POSITION visible qty={viewModel.BookPosition}"
                    : $"FAIL  BookPosition={viewModel.BookPosition}; LastMessage={viewModel.LastMessage}");
                exitCode = qtyOk ? 0 : 1;

                // Optional hold so an owning agent can screenshot BOOK POSITION before teardown.
                if (int.TryParse(
                        Environment.GetEnvironmentVariable("DAXALGO_SMOKE_HOLD_MS"),
                        out var holdMs) &&
                    holdMs > 0)
                {
                    var holdDeadline = DateTime.UtcNow.AddMilliseconds(Math.Min(holdMs, 60_000));
                    while (DateTime.UtcNow < holdDeadline)
                    {
                        await PumpAsync().ConfigureAwait(true);
                        await Task.Delay(200).ConfigureAwait(true);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL  {exception.GetType().Name}: {exception.Message}");
            exitCode = 1;
        }
        finally
        {
            try { window?.Close(); } catch { /* smoke */ }
            try { viewModel?.Dispose(); } catch { /* smoke */ }
            try { paper?.Dispose(); } catch { /* smoke */ }
            try { Directory.Delete(directory, recursive: true); } catch { /* temp */ }
        }

        lines.Add(string.Empty);
        lines.Add(exitCode == 0 ? "RESULT: PASS" : "RESULT: FAIL");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllLinesAsync(reportPath, lines).ConfigureAwait(false);
        return exitCode;
    }

    private static async Task PumpAsync()
    {
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Loaded);
        await Dispatcher.UIThread.InvokeAsync(static () => { }, DispatcherPriority.Render);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private static AuthoredUnitSpecificationV1 BuildSpecification(PaperExecutionInstrumentChoice instrument)
    {
        var classification = new StrategyClassificationBindingV1("buy-once", new string('a', 64));
        var intent = new ConfirmedStrategyIntentV1(
            ConfirmedStrategyIntentV1.CurrentSchemaVersion,
            "intent-1",
            "candidate-1",
            1,
            new string('1', 64),
            new string('2', 64),
            classification,
            new StrategyIntentModelV1(StrategyIntentKindV1.PositionTarget),
            "strategy-requirements/v1",
            [
                new StrategySemanticRequirementV1(
                    "decide-target",
                    StrategySemanticStageV1.DecideIntent,
                    StrategySemanticDispositionV1.Applicable,
                    "Calculate and publish the reviewed target exposure.",
                    true,
                    new StrategyRequirementProvenanceV1(
                        ["candidate-statement-1"],
                        ["research-evidence-1"],
                        "Required by this executable smoke strategy.")),
            ],
            new string('3', 64));
        return new(
            AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            "smoke-buy-once",
            "Smoke buy once",
            "Buy two units on the first one-minute bar.",
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
            intent);
    }

    private static string BuildSource(AuthoredUnitSpecificationV1 specification)
    {
        var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        return $$"""
            public sealed class GeneratedPaperStrategy : IStrategyKernel, IAuthoredDrawingManifest
            {
                private int _submitted;
                private readonly List<OhlcvBar> _bars = new();
                public static string SpecificationHashSha256 => "{{hash}}";
                public IReadOnlyList<string> DrawingLayerTypeIds => new[] { "price.candles@1" };
                public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
                public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
                public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;
                public Task OnBarAsync(OhlcvBar bar, IStrategyRuntimeContext context, CancellationToken ct)
                {
                    _bars.Add(bar);
                    if (Interlocked.Exchange(ref _submitted, 1) == 0)
                        context.Book.SetTargetPosition(bar.InstrumentId, 2);
                    return Task.CompletedTask;
                }
                public void Draw(IRenderSurface surface)
                {
                    if (_bars.Count == 0) { surface.Text(8, 18, "Waiting for completed bars"); return; }
                    using (surface.Layer("candles", "price.candles@1")) Candles.Draw(surface, _bars);
                }
            }
            """;
    }

    private sealed class SmokeMarketDataIngest : IMarketDataIngest
    {
        public InstrumentId Resolve(Contract contract, BrokerKind broker) => throw new NotSupportedException();
        public IDisposable Subscribe(Contract contract, BrokerKind broker) => new NoopDisposable();
        public IDisposable SubscribeBars(Contract contract, BrokerKind broker, BarSize size) => new NoopDisposable();
        public IDisposable SubscribeTrades(Contract contract, BrokerKind broker) => new NoopDisposable();
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private sealed class SmokeBrokerSelector(BrokerKind kind) : IBrokerSelector
    {
        private readonly IBrokerClient _client = new SmokeBrokerClient(kind);
        public IReadOnlyList<BrokerKind> AvailableKinds => [kind];
        public bool IsAvailable(BrokerKind candidate) => candidate == kind;
        public IReadOnlyList<BrokerKind> Connected => [kind];
        public bool IsConnected(BrokerKind candidate) => candidate == kind;
        public IBrokerClient Get(BrokerKind candidate) =>
            candidate == kind ? _client : throw new KeyNotFoundException();
        public BrokerConnectionMode ModeOf(BrokerKind candidate) => new(candidate, false, "Smoke", "Smoke");
        public IObservable<ConnectionState> StateOf(BrokerKind candidate) =>
            new ImmediateObservable<ConnectionState>(CurrentStateOf(candidate));
        public ConnectionState CurrentStateOf(BrokerKind candidate) =>
            candidate == kind ? ConnectionState.Connected : ConnectionState.Disconnected;
        public event EventHandler<BrokerStateChangedEventArgs>? StateChanged;
        public Task ConnectAsync(BrokerKind candidate, CancellationToken ct = default) => Task.CompletedTask;
        public Task DisconnectAsync(BrokerKind candidate, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class SmokeBrokerClient(BrokerKind kind) : IBrokerClient
    {
        public BrokerKind Kind => kind;
        public IObservable<ConnectionState> ConnectionState { get; } =
            new ImmediateObservable<ConnectionState>(TradingTerminal.Core.Domain.ConnectionState.Connected);
        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<TradableInstrument>> ListInstrumentsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TradableInstrument>>([]);
        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Bar>> RequestHistoricalBarsAsync(
            Contract contract, BarSize barSize, TimeSpan duration, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Bar>>([]);
        public IAsyncEnumerable<Bar> SubscribeBarsAsync(
            Contract contract, BarSize barSize, CancellationToken ct = default) => EmptyAsync<Bar>();
        public IAsyncEnumerable<Tick> SubscribeTicksAsync(
            Contract contract, CancellationToken ct = default) => EmptyAsync<Tick>();
        public IAsyncEnumerable<DepthSnapshot> SubscribeDepthAsync(
            Contract contract, int levels = 10, CancellationToken ct = default) => EmptyAsync<DepthSnapshot>();
        public IAsyncEnumerable<TradeTick> SubscribeTradesAsync(
            Contract contract, CancellationToken ct = default) => EmptyAsync<TradeTick>();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        private static async IAsyncEnumerable<T> EmptyAsync<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }

    private sealed class ImmediateObservable<T>(T value) : IObservable<T>
    {
        public IDisposable Subscribe(IObserver<T> observer)
        {
            observer.OnNext(value);
            observer.OnCompleted();
            return new NoopDisposable();
        }
    }

    private sealed class SmokeInstrumentRegistry : IInstrumentRegistry
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
        public void RegisterAlias(InstrumentAlias alias) =>
            _aliases[(alias.Broker, alias.BrokerSymbol)] = alias.InstrumentId;
        public IReadOnlyList<Instrument> All() =>
            _instruments.Values.OrderBy(item => item.Id.Value).ToArray();
    }
}
