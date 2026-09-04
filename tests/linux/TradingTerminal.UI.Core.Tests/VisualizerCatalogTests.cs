using TradingTerminal.UI.Strategies;
using DaxAlgo.Sdk;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;
using Xunit;

namespace TradingTerminal.UI.Core.Tests;

public sealed class VisualizerCatalogTests
{
    [Fact]
    public void VisualizerCardCarriesKindActionAndDataTags()
    {
        var descriptor = new VisualizerDescriptor(
            "depth-map",
            "Depth Map",
            "Shows resting liquidity.",
            ImagePath: "depth.png",
            DataRequirementTags: ["L1", "DEPTH"]);

        var item = new StrategyCatalogItemViewModel(descriptor);

        Assert.Equal(CatalogItemKind.Visualizer, item.Kind);
        Assert.Null(item.Strategy);
        Assert.Same(descriptor, item.Visualizer);
        Assert.Equal("VISUALIZER", item.KindLabel);
        Assert.Equal("Open visualizer", item.PrimaryActionLabel);
        Assert.False(item.HasQuickBacktest);
        Assert.Equal(["L1", "DEPTH"], item.DataRequirementTags);
        Assert.Equal("depth.png", item.ImagePath);
    }

    [Fact]
    public void RegistryReplacementKeepsOneRunnableEntryPerId()
    {
        var registry = new VisualizerRegistry();
        var changed = 0;
        registry.Changed += (_, _) => changed++;
        var first = new VisualizerRegistration(
            new VisualizerDescriptor("book", "Book", "first"),
            () => throw new NotSupportedException());
        var replacement = new VisualizerRegistration(
            new VisualizerDescriptor("book", "Book v2", "second"),
            () => throw new NotSupportedException());

        registry.Register(first);
        registry.Register(replacement);

        Assert.Single(registry.All);
        Assert.Same(replacement, registry.Find("book"));
        Assert.Equal(2, changed);
    }

    [Fact]
    public void RegistryRemovalUpdatesTheRunnableCatalog()
    {
        var registration = new VisualizerRegistration(
            new VisualizerDescriptor("tape", "Tape", "trades"),
            () => throw new NotSupportedException());
        var registry = new VisualizerRegistry([registration]);
        var changed = 0;
        registry.Changed += (_, _) => changed++;

        var removed = registry.Remove("tape");

        Assert.True(removed);
        Assert.Empty(registry.All);
        Assert.Null(registry.Find("tape"));
        Assert.Equal(1, changed);
    }

    [Fact]
    public void PersistedAuthoredPluginRegistrationBecomesRunnableCatalogEntry()
    {
        var specification = Specification();
        var plugin = new AuthoredVisualizerPluginRegistration(
            "btc-candles",
            "BTC Candles",
            "Live one-minute candles.",
            typeof(TestVisualizer),
            AuthoredUnitSpecificationCanonicalJsonV1.Serialize(specification),
            AuthoredPluginBootstrap.CurrentVerificationContractVersion);

        var registry = new VisualizerRegistry(authoredPlugins: [plugin]);
        var registration = Assert.Single(registry.All);

        Assert.Equal(plugin.Id, registration.Id);
        Assert.Equal(plugin.DisplayName, registration.Descriptor.DisplayName);
        Assert.IsType<TestVisualizer>(registration.Create());
    }

    [Fact]
    public void PersistedAuthoredUnitsFromAnOlderVerifierStayOutOfRunnableRegistries()
    {
        var visualizerSpecification = Specification();
        var strategySpecification = AuthoredStrategyTestFixtures.Specification();

        var visualizers = new VisualizerRegistry(authoredPlugins:
        [
            new AuthoredVisualizerPluginRegistration(
                visualizerSpecification.UnitId,
                visualizerSpecification.Name,
                visualizerSpecification.RawRequest,
                typeof(TestVisualizer),
                AuthoredUnitSpecificationCanonicalJsonV1.Serialize(visualizerSpecification),
                VerificationContractVersion: AuthoredPluginBootstrap.CurrentVerificationContractVersion - 1),
        ]);
        var strategies = new StrategyKernelRegistry(
        [
            new AuthoredStrategyKernelPluginRegistration(
                strategySpecification.UnitId,
                strategySpecification.Name,
                strategySpecification.RawRequest,
                typeof(PersistedAuthoredStrategyKernel),
                AuthoredUnitSpecificationCanonicalJsonV1.Serialize(strategySpecification),
                VerificationContractVersion: AuthoredPluginBootstrap.CurrentVerificationContractVersion - 1),
        ]);

        Assert.Empty(visualizers.All);
        Assert.Empty(strategies.All);
    }

    [Fact]
    public void CanonicalStrategyCardOffersSdkBacktestAndPaperWithoutPretendingToBeLegacy()
    {
        var specification = AuthoredStrategyTestFixtures.Specification();
        var registration = new StrategyKernelRegistration(
            specification.UnitId,
            specification.Name,
            specification.RawRequest,
            () => new PersistedAuthoredStrategyKernel(),
            specification,
            StrategyParameterSchema.Empty);

        var item = new StrategyCatalogItemViewModel(registration);

        Assert.Equal(CatalogItemKind.Strategy, item.Kind);
        Assert.Same(registration, item.StrategyKernel);
        Assert.Null(item.Strategy);
        Assert.Equal("Run strategy in Paper", item.PrimaryActionLabel);
        Assert.True(item.HasQuickBacktest);
        Assert.False(item.HasLegacyStrategy);
        Assert.True(item.HasDataRequirementTags);
        Assert.Contains("Bars", item.DataRequirementTags);
        Assert.Contains("Paper only", item.DataRequirementTags);
    }

    [Fact]
    public void PersistedCanonicalStrategyPluginRestoresRunnableRegistryEntry()
    {
        var specification = AuthoredStrategyTestFixtures.Specification();
        var plugin = new AuthoredStrategyKernelPluginRegistration(
            specification.UnitId,
            specification.Name,
            specification.RawRequest,
            typeof(PersistedAuthoredStrategyKernel),
            AuthoredUnitSpecificationCanonicalJsonV1.Serialize(specification),
            AuthoredPluginBootstrap.CurrentVerificationContractVersion);

        var registry = new StrategyKernelRegistry([plugin]);
        var registration = Assert.Single(registry.All);

        Assert.Equal(specification.UnitId, registration.Id);
        Assert.Equal(
            AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification),
            AuthoredUnitSpecificationCanonicalJsonV1.Hash(registration.AuthoredSpecification));
        Assert.IsType<PersistedAuthoredStrategyKernel>(registration.Create());
    }

    private static AuthoredUnitSpecificationV1 Specification() => new(
        AuthoredUnitSpecificationV1.CurrentSchemaVersion,
        "btc-candles",
        "BTC Candles",
        "Show BTC one-minute candles.",
        AuthoredUnitSourceKindV1.Text,
        AuthoredUnitKindV1.Visualizer,
        [new AuthoredInstrumentRequestV1(
            "primary", "BTC", new InstrumentId(42), AssetClass.Crypto, BrokerKind.Coinbase)],
        new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
        StrategyDataRequirement.Bars,
        [],
        new AuthoredChartCompositionV1(
            [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0)],
            [new AuthoredChartLayerV1(
                "candles", "price", AuthoredChartLayerKindV1.Candles, "price.candles@1",
                new Dictionary<string, string>())]),
        [],
        [],
        AuthoredUnitExecutionIntentV1.None);

    private sealed class TestVisualizer : IVisualizer, IAuthoredDrawingManifest
    {
        public static string SpecificationHashSha256 =>
            AuthoredUnitSpecificationCanonicalJsonV1.Hash(Specification());
        public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
        public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
        public IReadOnlyList<string> DrawingLayerTypeIds => ["price.candles@1"];
        public Task OnStartAsync(IVisualizerContext context, CancellationToken ct) => Task.CompletedTask;
    }
}

public static class AuthoredStrategyTestFixtures
{
    public static AuthoredUnitSpecificationV1 Specification()
    {
        var classification = new StrategyClassificationBindingV1("ema-cross", new string('d', 64));
        return new(
        AuthoredUnitSpecificationV1.CurrentSchemaVersion,
        "btc-ema-paper",
        "BTC EMA Paper",
        "Trade a BTC EMA crossover and show the chart.",
        AuthoredUnitSourceKindV1.Text,
        AuthoredUnitKindV1.Strategy,
        [new AuthoredInstrumentRequestV1(
            "primary", "BTC", new InstrumentId(42), AssetClass.Crypto, BrokerKind.Coinbase)],
        new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
        StrategyDataRequirement.Bars,
        [],
        new AuthoredChartCompositionV1(
            [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0)],
            [new AuthoredChartLayerV1(
                "candles", "price", AuthoredChartLayerKindV1.Candles, "price.candles@1",
                new Dictionary<string, string>())]),
        [],
        [],
        AuthoredUnitExecutionIntentV1.PaperTargets,
        classification,
        AuthoredStrategyIntentFixture.Create(classification));
    }
}

public sealed class PersistedAuthoredStrategyKernel : IStrategyKernel, IAuthoredDrawingManifest
{
    public static string SpecificationHashSha256 =>
        AuthoredUnitSpecificationCanonicalJsonV1.Hash(AuthoredStrategyTestFixtures.Specification());
    public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
    public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
    public IReadOnlyList<string> DrawingLayerTypeIds => ["price.candles@1"];
    public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;
}
