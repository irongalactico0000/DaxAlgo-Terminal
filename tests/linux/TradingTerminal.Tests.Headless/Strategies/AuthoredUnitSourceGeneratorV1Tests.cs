using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class AuthoredUnitSourceGeneratorV1Tests
{
    [Fact]
    public async Task Candle_visualizer_source_is_bound_to_exact_specification_hash()
    {
        var specification = Visualizer("Show BTC one-minute candles.");
        var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        var provider = new FixedSourceProvider($$"""
            ```csharp:BitcoinCandles.cs
            public sealed class BitcoinCandles : IVisualizer, IAuthoredDrawingManifest
            {
                public static string SpecificationHashSha256 => "{{hash}}";
                public IReadOnlyList<string> DrawingLayerTypeIds => new[] { "price.candles@1" };
            }
            ```
            """);

        var result = await new AuthoredUnitSourceGeneratorV1().GenerateAsync(
            provider,
            new AuthoredUnitSourceGenerationRequestV1(specification));

        Assert.True(result.Success);
        Assert.Equal(hash, result.SpecificationHashSha256);
        Assert.Equal(specification.UnitId, result.Script!.Id);
        Assert.Contains("Ordinary candle requests render actual authorized bars", provider.LastRequest!.SystemContext);
        Assert.Contains("surface.Layer", provider.LastRequest.SystemContext);
        Assert.Contains("LayerId", provider.LastRequest.SystemContext);
        Assert.Equal(StrategyCodegenOutputContract.CSharpPluginFiles, provider.LastRequest.OutputContract);
    }

    [Fact]
    public async Task Visual_reference_prompt_separates_appearance_from_market_prediction()
    {
        var reference = new AuthoredChartReferenceV1(
            "chart-1",
            new string('a', 64),
            "image/png",
            [ChartReferenceSimilarityV1.VisualStyle, ChartReferenceSimilarityV1.IndicatorComposition]);
        var specification = Visualizer("Show BTC like this chart.") with
        {
            SourceKind = AuthoredUnitSourceKindV1.TextAndReferenceChart,
            References = [reference],
            Drawing = new AuthoredChartCompositionV1(
                [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0)],
                [new AuthoredChartLayerV1(
                    "candles", "price", AuthoredChartLayerKindV1.Candles, "price.candles@1",
                    new Dictionary<string, string>(), reference.ReferenceId)]),
            ReferenceResolutions =
            [
                new AuthoredChartReferenceResolutionV1(
                    reference.ReferenceId, ["candles"], [], "Copy candle presentation only."),
            ],
        };
        var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        var provider = new FixedSourceProvider(Source(hash));

        var result = await new AuthoredUnitSourceGeneratorV1().GenerateAsync(
            provider,
            new AuthoredUnitSourceGenerationRequestV1(specification));

        Assert.True(result.Success);
        Assert.Contains("copies presentation only", provider.LastRequest!.SystemContext);
        Assert.Contains("never infers that the referenced market will repeat", provider.LastRequest.SystemContext);
    }

    [Fact]
    public async Task Similar_index_prompt_uses_selected_history_and_forbids_symbol_guessing()
    {
        var specification = Visualizer("Show an index whose historical chart is similar to this chart.");
        var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        var provider = new FixedSourceProvider(Source(hash));

        var result = await new AuthoredUnitSourceGeneratorV1().GenerateAsync(
            provider,
            new AuthoredUnitSourceGenerationRequestV1(specification));

        Assert.True(result.Success);
        Assert.Contains("user-selected real-history match", provider.LastRequest!.SystemContext);
        Assert.Contains("do not search again", provider.LastRequest.SystemContext);
        Assert.Contains("do not replace it with a guessed symbol", provider.LastRequest.SystemContext);
    }

    [Fact]
    public async Task Source_without_exact_specification_binding_is_rejected()
    {
        var specification = Visualizer("Show BTC candles.");
        var provider = new FixedSourceProvider("""
            ```csharp:BitcoinCandles.cs
            public sealed class BitcoinCandles : IVisualizer { }
            ```
            """);

        var result = await new AuthoredUnitSourceGeneratorV1().GenerateAsync(
            provider,
            new AuthoredUnitSourceGenerationRequestV1(specification));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "UNIT_SOURCE_SPECIFICATION_BINDING_MISSING");
    }

    private static AuthoredUnitSpecificationV1 Visualizer(string rawRequest) => new(
        AuthoredUnitSpecificationV1.CurrentSchemaVersion,
        "btc-candles",
        "BTC Candles",
        rawRequest,
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

    private static string Source(string hash) => $$"""
        ```csharp:BitcoinCandles.cs
        public sealed class BitcoinCandles : IVisualizer, IAuthoredDrawingManifest
        {
            public static string SpecificationHashSha256 => "{{hash}}";
            public IReadOnlyList<string> DrawingLayerTypeIds => new[] { "price.candles@1" };
        }
        ```
        """;

    private sealed class FixedSourceProvider(string response) : IStrategyCodegenClient
    {
        public string ProviderId => "fixed-source";
        public string DisplayName => "Fixed source";
        public bool IsAvailable => true;
        public StrategyCodegenRequest? LastRequest { get; private set; }

        public Task<StrategyCodegenResponse> GenerateAsync(
            StrategyCodegenRequest request,
            CancellationToken ct = default)
        {
            LastRequest = request;
            var files = CodegenCodeExtractor.ExtractFiles(response);
            return Task.FromResult(StrategyCodegenResponse.Ok(files, response, new CodegenUsage(10, 20)));
        }
    }
}
