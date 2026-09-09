using System.Text;
using DaxAlgo.Package;
using DaxAlgo.Sdk;
using TradingTerminal.App.Plugins;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.UI.Strategies;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class OpenPackageHostRegistrarTests
{
    [Fact]
    public void RegisterInstall_compiles_durable_open_package_into_strategy_kernel_registry()
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-open-package-host", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var instrumentId = new InstrumentId(42);
            var specification = LaunchValidStrategySpecification(instrumentId);
            var installDirectory = Path.Combine(directory, "pkg");
            var contentRoot = Path.Combine(installDirectory, OpenPackageDurableInstaller.ContentDirectoryName);
            var authoredDir = Path.Combine(contentRoot, "authored");
            Directory.CreateDirectory(authoredDir);
            File.WriteAllText(
                Path.Combine(authoredDir, "unit.specification.v1.json"),
                AuthoredUnitSpecificationCanonicalJsonV1.Serialize(specification));
            File.WriteAllText(
                Path.Combine(contentRoot, "GeneratedPaperStrategy.cs"),
                CompiledStrategySource(specification),
                Encoding.UTF8);

            var handoff = new OpenPackageMarketplaceHandoff(
                ArtifactExtension: DaxPackage.StrategyExtension,
                PackageId: "open.buy-once",
                Version: "1.0.0",
                DisplayName: specification.Name,
                Publisher: "tests",
                EntryTypeName: "GeneratedPaperStrategy",
                Kind: DaxPackageKind.Strategy,
                ManifestSha256: new string('a', 64),
                PackageSha256: new string('b', 64),
                PackageLength: 1,
                Payloads: [],
                StrategyBuilderConfirmedInputSha256: null);
            var installed = new OpenPackageInstallResult(
                true,
                "installed",
                handoff,
                installDirectory);

            var registry = new StrategyKernelRegistry();
            var result = OpenPackageHostRegistrar.RegisterInstall(
                installed,
                registry,
                new RoslynAuthoredUnitCompilerV1());

            Assert.True(result.Registered, result.Message);
            var found = registry.Find(specification.UnitId);
            Assert.NotNull(found);
            Assert.Equal(specification.Name, found!.DisplayName);
            Assert.Equal(AuthoredUnitKindV1.Strategy, found.AuthoredSpecification.Kind);
            Assert.NotNull(found.Create());
        }
        finally
        {
            try { Directory.Delete(directory, recursive: true); } catch { /* temp */ }
        }
    }

    private static AuthoredUnitSpecificationV1 LaunchValidStrategySpecification(InstrumentId instrumentId)
    {
        var classification = new StrategyClassificationBindingV1("buy-once", new string('a', 64));
        return new(
            AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            "open-package-buy-once",
            "Open package buy once",
            "Buy two units on the first one-minute bar.",
            AuthoredUnitSourceKindV1.Text,
            AuthoredUnitKindV1.Strategy,
            [new AuthoredInstrumentRequestV1(
                "primary",
                "AAPL",
                instrumentId,
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

    private static string CompiledStrategySource(AuthoredUnitSpecificationV1 specification)
    {
        var specificationHash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        return $$"""
            public sealed class GeneratedPaperStrategy : IStrategyKernel, IAuthoredDrawingManifest
            {
                private int _submitted;
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
}
