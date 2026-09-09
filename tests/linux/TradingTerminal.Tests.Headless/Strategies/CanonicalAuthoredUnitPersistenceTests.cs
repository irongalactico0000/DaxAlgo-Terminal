using DaxAlgo.Sdk;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.Infrastructure.Plugins;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.UI.Strategies;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class CanonicalAuthoredUnitPersistenceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "daxalgo-tests",
        "authored-unit-persistence-" + Guid.NewGuid().ToString("N"));

    public CanonicalAuthoredUnitPersistenceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void Compiler_verified_strategy_persists_and_restores_through_the_real_plugin_loader()
    {
        var specification = Specification();
        var script = new StrategyScript(
            specification.UnitId,
            specification.Name,
            [new StrategyFile("RestartableStrategy.cs", Source(specification))]);
        var compiled = new RoslynAuthoredUnitCompilerV1().Compile(specification, script);
        Assert.True(compiled.Success, string.Join(Environment.NewLine, compiled.Diagnostics));

        var pluginHost = new PluginHostContext(
            _root,
            PluginTrustPolicy.Permissive,
            []);
        var installer = new AuthoredStrategyInstaller(
            new ServiceCollection().BuildServiceProvider(),
            Substitute.For<IBacktestStrategyRegistry>(),
            Substitute.For<IStrategyFactory>(),
            pluginHost);

        var persisted = installer.PersistAuthoredUnit(script, compiled.Unit!);

        Assert.True(persisted.Persisted, persisted.Message);
        Assert.NotNull(persisted.Path);
        Assert.True(File.Exists(Path.Combine(persisted.Path!, "restartable_strategy.dll")));
        Assert.True(File.Exists(Path.Combine(persisted.Path!, PluginManifest.FileName)));

        var restartedServices = new ServiceCollection();
        var report = PluginLoader.LoadWithReport(
            restartedServices,
            _root,
            SdkInfo.Version,
            new PluginStateStore(_root));

        Assert.Empty(report.Problems);
        Assert.Single(report.Loaded);
        using var restartedProvider = restartedServices.BuildServiceProvider();
        var pluginRegistrations = restartedProvider
            .GetServices<AuthoredStrategyKernelPluginRegistration>()
            .ToArray();
        var registry = new StrategyKernelRegistry(pluginRegistrations);
        var restored = registry.Find(specification.UnitId);

        Assert.NotNull(restored);
        Assert.Equal(
            AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification),
            AuthoredUnitSpecificationCanonicalJsonV1.Hash(restored!.AuthoredSpecification));
        Assert.Equal(StrategyDataRequirement.Bars, restored.DataRequirement);
        Assert.IsAssignableFrom<IStrategyKernel>(restored.Create());
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* best-effort test cleanup */ }
    }

    private static AuthoredUnitSpecificationV1 Specification()
    {
        var classification = new StrategyClassificationBindingV1("position-target", new string('a', 64));
        var intent = new ConfirmedStrategyIntentV1(
            ConfirmedStrategyIntentV1.CurrentSchemaVersion,
            "intent-restartable",
            "candidate-restartable",
            1,
            new string('b', 64),
            new string('c', 64),
            classification,
            new StrategyIntentModelV1(StrategyIntentKindV1.PositionTarget),
            "strategy-requirements/v1",
            [
                new StrategySemanticRequirementV1(
                    "target-after-bar",
                    StrategySemanticStageV1.DecideIntent,
                    StrategySemanticDispositionV1.Applicable,
                    "Set the reviewed target after a completed bar.",
                    true,
                    new StrategyRequirementProvenanceV1(
                        ["candidate-statement"],
                        ["research-evidence"],
                        "Required by the persisted strategy.")),
            ],
            new string('d', 64));

        return StrategyInteractionBindingsFactoryV1.UpgradeTrustedSpecification(new AuthoredUnitSpecificationV1(
            AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            "restartable-strategy",
            "Restartable Strategy",
            "Trade SPY from completed bars and show the chart.",
            AuthoredUnitSourceKindV1.Text,
            AuthoredUnitKindV1.Strategy,
            [new AuthoredInstrumentRequestV1(
                "primary",
                "SPY",
                new InstrumentId(71),
                AssetClass.Equity,
                BrokerKind.Simulated)],
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
            intent));
    }

    private static string Source(AuthoredUnitSpecificationV1 specification)
    {
        var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        return $$"""
            public sealed class RestartableStrategy : IStrategyKernel, IAuthoredDrawingManifest
            {
                private double? _lastClose;
                private readonly List<OhlcvBar> _bars = new();
                public static string SpecificationHashSha256 => "{{hash}}";
                public IReadOnlyList<string> DrawingLayerTypeIds => new[] { "price.candles@1" };
                public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
                public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
                public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) =>
                    Task.CompletedTask;
                public Task OnBarAsync(OhlcvBar bar, IStrategyRuntimeContext context, CancellationToken ct)
                {
                    _lastClose = bar.Close;
                    _bars.Add(bar);
                    if (_bars.Count > 256) _bars.RemoveAt(0);
                    context.Book.SetTargetPosition(bar.InstrumentId, 1);
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
                        Candles.Draw(surface, _bars);
                }
            }
            """;
    }
}
