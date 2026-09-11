using System.Text;
using DaxAlgo.Package;
using DaxAlgo.Sdk;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;

namespace TradingTerminal.App.Plugins;

/// <summary>
/// Lane 3 sample: open-package content with <c>authored/unit.specification.v1.json</c> + matching
/// <c>.cs</c>. Used by fixtures/tests and as the reference layout for Marketplace install.
/// </summary>
public static class OpenPackageBuyOnceSample
{
    public const string PackageId = "open.buy-once";
    public const string UnitId = "open-package-buy-once";
    public const string DisplayName = "Open package buy once";
    public const string EntryTypeName = "GeneratedPaperStrategy";

    public static AuthoredUnitSpecificationV1 CreateSpecification(InstrumentId instrumentId) =>
        new(
            AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            UnitId,
            DisplayName,
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
            CreateClassification(),
            CreateIntent(CreateClassification()));

    public static string CreateSource(AuthoredUnitSpecificationV1 specification)
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

    /// <summary>
    /// Writes content files under <paramref name="contentRoot"/>
    /// (<c>authored/unit.specification.v1.json</c> + <c>GeneratedPaperStrategy.cs</c>).
    /// </summary>
    public static AuthoredUnitSpecificationV1 WriteContentTree(
        string contentRoot,
        InstrumentId instrumentId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRoot);
        var specification = CreateSpecification(instrumentId);
        var authoredDir = Path.Combine(contentRoot, "authored");
        Directory.CreateDirectory(authoredDir);
        File.WriteAllText(
            Path.Combine(authoredDir, "unit.specification.v1.json"),
            AuthoredUnitSpecificationCanonicalJsonV1.Serialize(specification),
            Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(contentRoot, "GeneratedPaperStrategy.cs"),
            CreateSource(specification),
            Encoding.UTF8);
        return specification;
    }

    /// <summary>Builds a <c>.daxalgostrategy</c> with the required authored payload.</summary>
    public static DaxPackageWriteResult WritePackage(
        string outputPath,
        InstrumentId instrumentId)
    {
        var specification = CreateSpecification(instrumentId);
        var specBytes = Encoding.UTF8.GetBytes(
            AuthoredUnitSpecificationCanonicalJsonV1.Serialize(specification));
        var sourceBytes = Encoding.UTF8.GetBytes(CreateSource(specification));
        var request = new DaxPackageRequest
        {
            Id = PackageId,
            Version = "1.0.0",
            DisplayName = DisplayName,
            Publisher = "daxalgo-sample",
            EntryTypeName = EntryTypeName,
            Kind = DaxPackageKind.Strategy,
            Payloads =
            [
                DaxPayloadSource.FromBytes(
                    OpenPackageHostRegistrar.SpecificationRelativePath,
                    DaxPayloadRole.Resource,
                    specBytes),
                DaxPayloadSource.FromBytes(
                    "GeneratedPaperStrategy.cs",
                    DaxPayloadRole.Source,
                    sourceBytes),
            ],
        };
        return DaxPackage.Write(outputPath, request);
    }

    private static StrategyClassificationBindingV1 CreateClassification() =>
        new("buy-once", new string('a', 64));

    private static ConfirmedStrategyIntentV1 CreateIntent(StrategyClassificationBindingV1 classification) =>
        new(
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
                        "Required by this executable sample strategy.")),
            ],
            new string('3', 64));
}
