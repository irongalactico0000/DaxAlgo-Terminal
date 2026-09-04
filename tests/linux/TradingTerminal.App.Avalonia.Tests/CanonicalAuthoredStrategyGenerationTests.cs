using FluentAssertions;
using DaxAlgo.Sdk;
using Microsoft.Extensions.Logging.Abstractions;
using TradingTerminal.App.Authoring;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Core.Time;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.UI.Strategies;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class CanonicalAuthoredStrategyGenerationTests
{
    [Fact]
    public async Task Confirmed_strategy_builds_one_intent_bound_sdk_source_without_using_research_lanes()
    {
        var provider = new StubCodegenClient();
        var specificationGenerator = new RecordingSpecificationGenerator();
        var sourceGenerator = new RecordingSourceGenerator();
        var registry = new MemoryInstrumentRegistry();
        var spy = new Instrument(new InstrumentId(71), "SPY", AssetClass.Equity, "ARCX", "USD", 0.01, 1);
        registry.Add(spy, BrokerKind.Simulated, "SPY");

        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubBacktestRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            ai: new StubAiStrategyBuilder(provider),
            sessionRepository: new EmptySessionRepository(),
            authoredUnitSpecificationGenerator: specificationGenerator,
            instrumentRegistry: registry,
            authoredUnitSourceGenerator: sourceGenerator);
        ConfirmPositionIntent(viewModel);
        viewModel.ConfirmedStrategyIntent.Should().NotBeNull();
        viewModel.CanonicalPaperStrategyIntentSupported.Should().BeTrue();
        viewModel.CanGenerateCanonicalPaperStrategy.Should().BeTrue();

        await viewModel.GenerateCanonicalPaperStrategyCommand.ExecuteAsync(null);

        specificationGenerator.Request.Should().NotBeNull();
        specificationGenerator.Request!.ConfirmedStrategyIntent.Should().BeSameAs(viewModel.ConfirmedStrategyIntent);
        sourceGenerator.Request.Should().NotBeNull();
        sourceGenerator.Request!.Specification.ConfirmedStrategyIntent.Should().BeSameAs(viewModel.ConfirmedStrategyIntent);
        viewModel.AuthoredUnitSpecification!.Kind.Should().Be(AuthoredUnitKindV1.Strategy);
        viewModel.AuthoredUnitSpecification.ExecutionIntent.Should().Be(AuthoredUnitExecutionIntentV1.PaperTargets);
        viewModel.AuthoredUnitSpecification.Instruments.Should().ContainSingle(item =>
            item.InstrumentId == spy.Id && item.PreferredBroker == BrokerKind.Simulated);
        viewModel.Files.Should().ContainSingle(file => file.Name == "Strategy.cs");
        viewModel.Files[0].Content.Should().Contain("GeneratedPaperStrategy");
        viewModel.WorkbenchTab.Should().Be(0);
        viewModel.IsBuildScreen.Should().BeTrue();
        viewModel.AiStatus.Should().Contain("strategy source is ready");
    }

    [Fact]
    public async Task Generated_strategy_compiles_registers_and_emits_a_target_from_its_declared_bar_callback()
    {
        var provider = new StubCodegenClient();
        var specificationGenerator = new RecordingSpecificationGenerator();
        var sourceGenerator = new ExecutableSourceGenerator();
        var instruments = new MemoryInstrumentRegistry();
        var kernels = new StrategyKernelRegistry();
        var spy = new Instrument(new InstrumentId(71), "SPY", AssetClass.Equity, "ARCX", "USD", 0.01, 1);
        instruments.Add(spy, BrokerKind.Simulated, "SPY");

        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubBacktestRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            ai: new StubAiStrategyBuilder(provider),
            sessionRepository: new EmptySessionRepository(),
            authoredUnitSpecificationGenerator: specificationGenerator,
            instrumentRegistry: instruments,
            authoredUnitSourceGenerator: sourceGenerator,
            authoredUnitCompiler: new RoslynAuthoredUnitCompilerV1(),
            strategyKernelRegistry: kernels);
        ConfirmPositionIntent(viewModel);

        await viewModel.GenerateCanonicalPaperStrategyCommand.ExecuteAsync(null);
        viewModel.CompileCommand.Execute(null);

        viewModel.CompiledOk.Should().BeTrue(string.Join(" | ", viewModel.Diagnostics.Select(item =>
            $"{item.Id}:{item.Message}")));
        viewModel.ReviewOpen.Should().BeTrue();
        viewModel.ConfirmRegisterCommand.Execute(null);

        var registration = kernels.Find("spy-breakout-paper");
        registration.Should().NotBeNull();
        registration!.AuthoredSpecification.ConfirmedStrategyIntent.Should().Be(viewModel.ConfirmedStrategyIntent);

        var book = new TargetBook();
        var context = new TargetContext(spy.Id, book);
        var kernel = registration.Create();
        await kernel.OnStartAsync(context, CancellationToken.None);
        await kernel.OnBarAsync(
            new OhlcvBar(
                spy.Id,
                BarSize.FiveMinutes,
                new DateTime(2026, 9, 4, 1, 0, 0, DateTimeKind.Utc),
                100,
                103,
                99,
                102,
                1_000,
                BrokerKind.Simulated,
                true),
            context,
            CancellationToken.None);

        book.Targets.Should().ContainSingle().Which.Should().Be(
            new VirtualTargetIntent(spy.Id, 10));
    }

    private static void ConfirmPositionIntent(StrategyAuthoringViewModel viewModel)
    {
        viewModel.StrategyId = "spy-breakout-paper";
        viewModel.DisplayName = "SPY breakout Paper";
        viewModel.CurrentCandidate = PositionCandidate();
        viewModel.CandidateContentHash = StrategyCandidateCanonicalJsonV1.Hash(viewModel.CurrentCandidate);
        viewModel.SelectStrategyIntentProfile(StrategyStarterCatalog.All.Single(brief =>
            brief.Id == "starter.five-minute-momentum-breakout"));

        viewModel.BeginStrategyIntentReview();
        viewModel.StrategyResearchEvidence = "Completed SPY five-minute bars and volume.";
        viewModel.StrategyResearchPointInTimeRule = "Use only bars completed at or before the decision timestamp.";
        viewModel.StrategyResearchQualificationRule = "Act only after the reviewed breakout and volume threshold both pass.";
        viewModel.StrategyResearchFalsifier = "Reject if the result requires an incomplete or future bar.";
        foreach (var row in viewModel.StrategyIntentRequirements.Where(row => !row.MustBeNotApplicable))
            row.Answer = $"Reviewed executable rule: {row.Question}";

        viewModel.CanConfirmStrategyIntentReview.Should().BeTrue(
            "issues: {0}; questions: {1}",
            string.Join(" | ", viewModel.StrategyIntentIssues.Select(issue => $"{issue.Code}:{issue.Message}")),
            string.Join(" | ", viewModel.StrategyIntentQuestions.Select(question =>
                $"{question.RequirementId}:{question.Reason}")));
        viewModel.ConfirmStrategyIntentReviewCommand.Execute(null);
    }

    private static StrategyCandidateV1 PositionCandidate() => new(
        StrategyCandidateV1.CurrentSchemaVersion,
        "spy-breakout-candidate",
        1,
        null,
        "Trade SPY after a completed five-minute breakout and show the chart.",
        "SPY breakout",
        StrategyCandidateStatusV1.Confirmed,
        new StrategyCandidateInterpretationV1(
            "Produce a reviewed SPY position target from completed five-minute bars.",
            StrategyInterpretationConfidenceV1.High,
            []),
        [
            new StrategyCandidateGroupV1(
                "decision",
                StrategyCandidateGroupKindV1.SignalAndAlpha,
                "Decision",
                "Reviewed position-target behavior.",
                [
                    new StrategyCandidateStatementV1(
                        "rule-position",
                        StrategyCandidateStatementKindV1.Rule,
                        "Set the reviewed SPY target only after the qualified breakout.",
                        StrategyCandidateStatementSourceV1.User,
                        StrategyCandidateStatementStateV1.Confirmed,
                        true),
                ],
                []),
        ],
        [
            new StrategyBuildSupportItemV1(
                "support-position",
                "Canonical SDK position target",
                StrategyBuildSupportStatusV1.Supported,
                true,
                "The current SDK and Paper runner support per-instrument target positions.",
                ["rule-position"]),
        ]);

    private sealed class RecordingSpecificationGenerator : IAuthoredUnitSpecificationGeneratorV1
    {
        public AuthoredUnitSpecificationGenerationRequestV1? Request { get; private set; }

        public Task<AuthoredUnitSpecificationGenerationResultV1> GenerateAsync(
            IStrategyCodegenClient provider,
            AuthoredUnitSpecificationGenerationRequestV1 request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            var intent = request.ConfirmedStrategyIntent!;
            var instrument = request.AvailableInstruments.Single(item => item.CanonicalSymbol == "SPY");
            var specification = new AuthoredUnitSpecificationV1(
                AuthoredUnitSpecificationV1.CurrentSchemaVersion,
                request.UnitId,
                "SPY breakout Paper",
                request.RawRequest,
                AuthoredUnitSourceKindV1.Text,
                AuthoredUnitKindV1.Strategy,
                [new AuthoredInstrumentRequestV1(
                    "primary", "SPY", instrument.InstrumentId, instrument.AssetClass, BrokerKind.Simulated)],
                new AuthoredUnitTimeframeV1("5 minutes", TimeSpan.FromMinutes(5)),
                StrategyDataRequirement.Bars,
                [],
                new AuthoredChartCompositionV1(
                    [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0, "Price")],
                    [new AuthoredChartLayerV1(
                        "candles", "price", AuthoredChartLayerKindV1.Candles, "price.candles@1",
                        new Dictionary<string, string>())]),
                [],
                [],
                AuthoredUnitExecutionIntentV1.PaperTargets,
                intent.Classification,
                intent);
            return Task.FromResult(new AuthoredUnitSpecificationGenerationResultV1(
                specification,
                [],
                CodegenUsage.None));
        }
    }

    private sealed class RecordingSourceGenerator : IAuthoredUnitSourceGeneratorV1
    {
        public AuthoredUnitSourceGenerationRequestV1? Request { get; private set; }

        public Task<AuthoredUnitSourceGenerationResultV1> GenerateAsync(
            IStrategyCodegenClient provider,
            AuthoredUnitSourceGenerationRequestV1 request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            var script = new StrategyScript(
                request.Specification.UnitId,
                request.Specification.Name,
                [new StrategyFile("Strategy.cs", "public sealed class GeneratedPaperStrategy { }")]);
            return Task.FromResult(new AuthoredUnitSourceGenerationResultV1(
                script,
                AuthoredUnitSpecificationCanonicalJsonV1.Hash(request.Specification),
                [],
                CodegenUsage.None));
        }
    }

    private sealed class ExecutableSourceGenerator : IAuthoredUnitSourceGeneratorV1
    {
        public Task<AuthoredUnitSourceGenerationResultV1> GenerateAsync(
            IStrategyCodegenClient provider,
            AuthoredUnitSourceGenerationRequestV1 request,
            CancellationToken cancellationToken = default)
        {
            var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(request.Specification);
            var source = $$"""
                public sealed class GeneratedPaperStrategy : IStrategyKernel, IAuthoredDrawingManifest
                {
                    private double _lastClose;
                    private readonly List<OhlcvBar> _bars = new();

                    public static string SpecificationHashSha256 => "{{hash}}";
                    public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
                    public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
                    public IReadOnlyList<string> DrawingLayerTypeIds => new[] { "price.candles@1" };

                    public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) =>
                        Task.CompletedTask;

                    public Task OnBarAsync(OhlcvBar bar, IStrategyRuntimeContext context, CancellationToken ct)
                    {
                        _lastClose = bar.Close;
                        _bars.Add(bar);
                        if (_bars.Count > 256) _bars.RemoveAt(0);
                        context.Book.SetTargetPosition(bar.InstrumentId, 10);
                        return Task.CompletedTask;
                    }

                    public void Draw(IRenderSurface surface)
                    {
                        if (_bars.Count == 0)
                        {
                            surface.Text(0, 0, "Waiting for bars");
                            return;
                        }

                        using (surface.Layer("candles", "price.candles@1"))
                            Candles.Draw(surface, _bars);
                    }
                }
                """;
            return Task.FromResult(new AuthoredUnitSourceGenerationResultV1(
                new StrategyScript(
                    request.Specification.UnitId,
                    request.Specification.Name,
                    [new StrategyFile("Strategy.cs", source)]),
                hash,
                [],
                CodegenUsage.None));
        }
    }

    private sealed class TargetBook : IVirtualBook
    {
        public List<VirtualTargetIntent> Targets { get; } = [];
        public void SubmitTarget(VirtualTargetIntent intent) => Targets.Add(intent);
    }

    private sealed class TargetContext(InstrumentId instrument, IVirtualBook book) : IStrategyRuntimeContext
    {
        public IMarketDataView Data { get; } = new EmptyMarketData(instrument);
        public IClock Clock { get; } = new FixedClock();
        public IParameters Parameters { get; } = new EmptyParameters();
        public IVirtualBook Book { get; } = book;
        public IAlertSink Alerts { get; } = new NullAlerts();
    }

    private sealed class EmptyMarketData(InstrumentId instrument) : IMarketDataView
    {
        public IReadOnlySet<InstrumentId> Instruments { get; } = new HashSet<InstrumentId> { instrument };
        public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
        public IReadOnlyList<OhlcvBar> RecentBars(InstrumentId id, BarSize size, int maxCount) => [];
        public IReadOnlyList<Quote> RecentQuotes(InstrumentId id, int maxCount) => [];
        public DepthSnapshot? LatestDepth(InstrumentId id) => null;
        public IReadOnlyList<TradePrint> RecentTrades(InstrumentId id, int maxCount) => [];
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 4, 1, 5, 0, DateTimeKind.Utc);
    }

    private sealed class EmptyParameters : IParameters
    {
        public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
        public int GetInt(string name) => throw new KeyNotFoundException(name);
        public long GetLong(string name) => throw new KeyNotFoundException(name);
        public double GetDouble(string name) => throw new KeyNotFoundException(name);
        public bool GetBool(string name) => throw new KeyNotFoundException(name);
        public string GetString(string name) => throw new KeyNotFoundException(name);
        public string GetText(string name) => throw new KeyNotFoundException(name);
        public TEnum GetEnum<TEnum>(string name) where TEnum : struct, Enum => throw new KeyNotFoundException(name);
        public InstrumentId GetInstrument(string name) => throw new KeyNotFoundException(name);
    }

    private sealed class NullAlerts : IAlertSink
    {
        public void Alert(string message, AlertLevel level, string? dedupeKey = null) { }
    }

    private sealed class StubAiStrategyBuilder(IStrategyCodegenClient provider) : IAiStrategyBuilder
    {
        public IReadOnlyList<IStrategyCodegenClient> Providers => [provider];
        public IStrategyCodegenClient? DefaultProvider => provider;
        public IStrategyCodegenClient? WithSettings(string providerId, string? model, CodegenEffort effort) => provider;
        public IReadOnlyList<string> ModelsFor(string providerId) => [];
        public IReadOnlyList<AiModelChoice> AllModels() => [];
        public StrategyBuildSession StartSession(
            IStrategyCodegenClient selectedProvider,
            string strategyId,
            string displayName,
            IReadOnlyList<CodegenMessage>? history = null,
            CodegenUsage? priorUsage = null,
            StrategyBuildProfile? profile = null) =>
            throw new NotSupportedException("The canonical authored-unit generators own this workflow.");
        public Task<StrategyBuildLoopResult> BuildAsync(
            IStrategyCodegenClient selectedProvider,
            string instruction,
            string strategyId,
            string displayName,
            CancellationToken ct = default) =>
            Task.FromException<StrategyBuildLoopResult>(
                new NotSupportedException("The canonical authored-unit generators own this workflow."));
    }

    private sealed class StubCodegenClient : IStrategyCodegenClient
    {
        public string ProviderId => "stub";
        public string DisplayName => "Stub provider";
        public bool IsAvailable => true;
        public Task<StrategyCodegenResponse> GenerateAsync(
            StrategyCodegenRequest request,
            CancellationToken ct = default) =>
            Task.FromException<StrategyCodegenResponse>(new NotSupportedException());
    }

    private sealed class MemoryInstrumentRegistry : IInstrumentRegistry
    {
        private readonly Dictionary<InstrumentId, Instrument> _instruments = [];
        private readonly Dictionary<(BrokerKind Broker, string Symbol), InstrumentId> _aliases = [];

        public void Add(Instrument instrument, BrokerKind broker, string brokerSymbol)
        {
            _instruments[instrument.Id] = instrument;
            _aliases[(broker, brokerSymbol)] = instrument.Id;
        }

        public Instrument? Get(InstrumentId id) => _instruments.GetValueOrDefault(id);
        public InstrumentId? Resolve(BrokerKind broker, string brokerSymbol) =>
            _aliases.GetValueOrDefault((broker, brokerSymbol));
        public InstrumentId ResolveOrCreate(Contract contract, BrokerKind broker) =>
            Resolve(broker, contract.Symbol) ?? throw new InvalidOperationException();
        public string? ToBrokerSymbol(InstrumentId id, BrokerKind broker) =>
            _aliases.FirstOrDefault(item => item.Key.Broker == broker && item.Value == id).Key.Symbol;
        public void RegisterAlias(InstrumentAlias alias) =>
            _aliases[(alias.Broker, alias.BrokerSymbol)] = alias.InstrumentId;
        public IReadOnlyList<Instrument> All() => _instruments.Values.ToArray();
    }

    private sealed class EmptySessionRepository : IAuthoringSessionRepository
    {
        public IReadOnlyList<AuthoringSessionSnapshot> List() => [];
        public bool Save(AuthoringSessionSnapshot session) => true;
        public void Delete(string strategyId) { }
    }

    private sealed class StubCompiler : IStrategyCompiler
    {
        public StrategyCompileResult Compile(StrategyScript script) => StrategyCompileResult.Failed([]);
    }

    private sealed class StubBacktestRegistry : IBacktestStrategyRegistry
    {
        public IReadOnlyList<BacktestStrategyOption> All => [];
        public BacktestStrategyOption? Find(string id) => null;
        public void Register(BacktestStrategyOption option) { }
        public bool Remove(string id) => false;
        public event EventHandler? Changed { add { } remove { } }
    }
}
