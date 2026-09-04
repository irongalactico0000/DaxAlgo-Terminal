using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Core.Strategies.Specification;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class AuthoredUnitSpecificationV1Tests
{
    [Fact]
    public void Text_visualizer_is_structurally_valid_and_launchable_after_instrument_resolution()
    {
        var specification = Visualizer();

        Assert.Empty(AuthoredUnitSpecificationValidatorV1.Validate(specification));
        Assert.Empty(AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification));
    }

    [Fact]
    public void Visualizer_cannot_gain_paper_target_authority()
    {
        var specification = Visualizer() with
        {
            ExecutionIntent = AuthoredUnitExecutionIntentV1.PaperTargets,
        };

        var issue = Assert.Single(AuthoredUnitSpecificationValidatorV1.Validate(specification));
        Assert.Equal("unit.visualizer.execution_forbidden", issue.Code);
    }

    [Fact]
    public void Reference_style_and_indicators_bind_to_the_exact_chart_and_layers()
    {
        var specification = Visualizer() with
        {
            SourceKind = AuthoredUnitSourceKindV1.TextAndReferenceChart,
            RawRequest = "Show BTC candles with indicators and styling like this chart.",
            References =
            [
                new AuthoredChartReferenceV1(
                    "chart-1",
                    new string('a', 64),
                    "image/png",
                    [ChartReferenceSimilarityV1.VisualStyle, ChartReferenceSimilarityV1.IndicatorComposition]),
            ],
            ReferenceResolutions =
            [
                new AuthoredChartReferenceResolutionV1(
                    "chart-1",
                    ["candles", "fast-ema", "slow-ema"],
                    [],
                    "Matched the candle presentation and the two EMA overlays."),
            ],
            Drawing = Drawing(withReference: true),
        };

        Assert.Empty(AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification));
        Assert.Equal(
            AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification),
            AuthoredUnitSpecificationCanonicalJsonV1.Hash(
                AuthoredUnitSpecificationCanonicalJsonV1.Deserialize(
                    AuthoredUnitSpecificationCanonicalJsonV1.Serialize(specification))));
    }

    [Fact]
    public void Market_pattern_similarity_cannot_launch_without_resolved_candidate_instruments()
    {
        var specification = Visualizer() with
        {
            SourceKind = AuthoredUnitSourceKindV1.ReferenceChart,
            RawRequest = "Find assets with a similar price pattern and show their charts.",
            References =
            [
                new AuthoredChartReferenceV1(
                    "pattern-1",
                    new string('b', 64),
                    "image/jpeg",
                    [ChartReferenceSimilarityV1.MarketPattern]),
            ],
            ReferenceResolutions =
            [
                new AuthoredChartReferenceResolutionV1(
                    "pattern-1",
                    ["candles"],
                    [],
                    "The visual pattern was extracted, but historical search has not returned instruments."),
            ],
            Drawing = Drawing(referenceId: "pattern-1"),
        };

        Assert.Empty(AuthoredUnitSpecificationValidatorV1.Validate(specification));
        Assert.Contains(
            AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification),
            issue => issue.Code == "unit.reference.search_result.required");
    }

    [Fact]
    public void Strategy_requires_classification_and_is_limited_to_paper_targets()
    {
        var visualizer = Visualizer();
        var invalid = visualizer with
        {
            Kind = AuthoredUnitKindV1.Strategy,
            ExecutionIntent = AuthoredUnitExecutionIntentV1.None,
        };

        var codes = AuthoredUnitSpecificationValidatorV1.Validate(invalid)
            .Select(static issue => issue.Code)
            .ToHashSet(StringComparer.Ordinal);
        Assert.Contains("unit.strategy.paper_required", codes);
        Assert.Contains("unit.strategy.classification_required", codes);

        var valid = invalid with
        {
            ExecutionIntent = AuthoredUnitExecutionIntentV1.PaperTargets,
            StrategyClassification = new StrategyClassificationBindingV1("ema-cross", new string('c', 64)),
        };
        valid = valid with
        {
            ConfirmedStrategyIntent = ConfirmedIntent(valid.StrategyClassification!),
        };
        Assert.Empty(AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(valid));
    }

    [Theory]
    [InlineData(AuthoredChartLayerKindV1.OrderBook, StrategyDataRequirement.Bars, StrategyDataRequirement.Depth)]
    [InlineData(AuthoredChartLayerKindV1.Footprint, StrategyDataRequirement.Bars, StrategyDataRequirement.TradeTape)]
    public void Drawing_layer_declares_its_actual_market_data(
        AuthoredChartLayerKindV1 layerKind,
        StrategyDataRequirement supplied,
        StrategyDataRequirement missing)
    {
        var specification = Visualizer() with
        {
            DataRequirement = supplied,
            Drawing = new AuthoredChartCompositionV1(
                [new AuthoredChartPaneV1("special", AuthoredChartPaneRoleV1.Custom, 0)],
                [new AuthoredChartLayerV1("special", "special", layerKind, "test.layer@1",
                    new Dictionary<string, string>())]),
        };

        var issue = Assert.Single(AuthoredUnitSpecificationValidatorV1.Validate(specification),
            candidate => candidate.Code == "unit.drawing.data_missing");
        Assert.Contains(missing.ToString(), issue.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Launch_rejects_natural_language_instrument_that_was_not_resolved()
    {
        var specification = Visualizer() with
        {
            Instruments = [new AuthoredInstrumentRequestV1("primary", "BTC", InstrumentId.None, AssetClass.Crypto)],
        };

        Assert.Empty(AuthoredUnitSpecificationValidatorV1.Validate(specification));
        Assert.Contains(
            AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification),
            issue => issue.Code == "unit.instrument.unresolved");
    }

    private static AuthoredUnitSpecificationV1 Visualizer() => new(
        AuthoredUnitSpecificationV1.CurrentSchemaVersion,
        "btc-ema-chart",
        "BTC EMA 9/21",
        "Show BTC one-minute candles with EMA 9 and EMA 21.",
        AuthoredUnitSourceKindV1.Text,
        AuthoredUnitKindV1.Visualizer,
        [new AuthoredInstrumentRequestV1("primary", "BTC", new InstrumentId(42), AssetClass.Crypto, BrokerKind.Binance)],
        new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
        StrategyDataRequirement.Bars,
        [
            new AuthoredUnitParameterV1("fast", "Fast EMA", ParameterKind.Integer, "9", "1", "500"),
            new AuthoredUnitParameterV1("slow", "Slow EMA", ParameterKind.Integer, "21", "2", "1000"),
        ],
        Drawing(),
        [],
        [],
        AuthoredUnitExecutionIntentV1.None);

    private static AuthoredChartCompositionV1 Drawing(bool withReference = false, string? referenceId = null) => new(
        [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0, "Price")],
        [
            new AuthoredChartLayerV1(
                "candles", "price", AuthoredChartLayerKindV1.Candles, "price.candles@1",
                new Dictionary<string, string>(), withReference ? "chart-1" : referenceId),
            new AuthoredChartLayerV1(
                "fast-ema", "price", AuthoredChartLayerKindV1.IndicatorLine, "indicator.ema@1",
                new Dictionary<string, string> { ["period"] = "${fast}" },
                withReference ? "chart-1" : referenceId),
            new AuthoredChartLayerV1(
                "slow-ema", "price", AuthoredChartLayerKindV1.IndicatorLine, "indicator.ema@1",
                new Dictionary<string, string> { ["period"] = "${slow}" },
                withReference ? "chart-1" : referenceId),
        ]);

    private static ConfirmedStrategyIntentV1 ConfirmedIntent(
        StrategyClassificationBindingV1 classification) => new(
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
                "Calculate the reviewed target position.",
                true,
                new StrategyRequirementProvenanceV1(
                    ["candidate-statement-1"],
                    ["research-evidence-1"],
                    "Required by the reviewed strategy.")),
        ],
        new string('3', 64));
}
