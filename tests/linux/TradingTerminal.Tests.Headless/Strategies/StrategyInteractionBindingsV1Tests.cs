using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class StrategyInteractionBindingsV1Tests
{
    [Fact]
    public void Existing_v1_unit_without_interaction_graph_remains_loadable()
    {
        var legacy = Visualizer(withBindings: false);

        Assert.Empty(AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(legacy));
        Assert.Null(legacy.InteractionBindings);
    }

    [Fact]
    public void Trusted_upgrade_builds_stable_parameter_feature_and_layer_graph()
    {
        var upgraded = Visualizer(withBindings: true);

        var graph = Assert.IsType<StrategyInteractionBindingsV1>(upgraded.InteractionBindings);
        Assert.Contains(graph.Parameters, item =>
            item.ParameterId == "parameter.fast" && item.ParameterKey == "fast");
        Assert.Contains(graph.Features, item =>
            item.FeatureId == "feature.fast-ema" &&
            item.ParameterIds.SequenceEqual(["parameter.fast"]));
        var layer = Assert.Single(graph.Layers, item => item.LayerId == "fast-ema");
        Assert.Equal("parameter.fast", layer.ParameterBindings["period"]);
        Assert.Empty(graph.Rules);
        Assert.Empty(StrategyInteractionBindingsValidatorV1.Validate(upgraded));
    }

    [Fact]
    public void Parameter_edit_updates_one_semantic_value_and_both_chart_and_rule_keep_same_identity()
    {
        var strategy = Strategy();
        var graphBefore = strategy.InteractionBindings!;
        var ruleBefore = Assert.Single(graphBefore.Rules);

        var updated = StrategyInteractionBindingEditorV1.ApplyCanonicalParameter(
            strategy,
            "parameter.fast",
            "12");

        Assert.Equal("12", Assert.Single(updated.Parameters, item => item.Key == "fast").CanonicalDefault);
        Assert.Equal("${fast}", Assert.Single(updated.Drawing.Layers, item => item.LayerId == "fast-ema").Parameters["period"]);
        Assert.Equal("12", StrategyInteractionBindingEditorV1.ResolveLayerParameters(updated, "fast-ema")["period"]);
        var ruleAfter = Assert.Single(updated.InteractionBindings!.Rules);
        Assert.Equal(ruleBefore.RuleId, ruleAfter.RuleId);
        Assert.Contains("parameter.fast", ruleAfter.ParameterIds);
        Assert.Equal(graphBefore, updated.InteractionBindings);
    }

    [Fact]
    public void Broken_cross_component_reference_fails_closed()
    {
        var strategy = Strategy();
        var graph = strategy.InteractionBindings!;
        var broken = strategy with
        {
            InteractionBindings = graph with
            {
                Rules =
                [
                    graph.Rules[0] with { FeatureIds = ["feature.not-installed"] },
                ],
            },
        };

        var issues = StrategyInteractionBindingsValidatorV1.Validate(broken);

        Assert.Contains(issues, issue =>
            issue.Code == "unit.interaction.reference.unknown" &&
            issue.Message.Contains("feature.not-installed", StringComparison.Ordinal));
    }

    [Fact]
    public void Display_only_visualizer_cannot_smuggle_a_strategy_rule()
    {
        var visualizer = Visualizer(withBindings: true);
        var graph = visualizer.InteractionBindings!;
        var invalid = visualizer with
        {
            InteractionBindings = graph with
            {
                Rules =
                [
                    new StrategyRuleBindingV1(
                        "rule.hidden-order",
                        ["feature.fast-ema"],
                        ["parameter.fast"],
                        ["primary"],
                        [],
                        "Produce an order while presented as a chart."),
                ],
            },
        };

        Assert.Contains(
            StrategyInteractionBindingsValidatorV1.Validate(invalid),
            issue => issue.Code == "unit.interaction.visualizer.rule_forbidden");
    }

    [Fact]
    public void Canonical_hash_changes_when_semantic_binding_changes()
    {
        var strategy = Strategy();
        var graph = strategy.InteractionBindings!;
        var changed = graph with
        {
            Rules =
            [
                graph.Rules[0] with { Description = "Use the same features with changed reviewed semantics." },
            ],
        };

        Assert.Equal(
            StrategyInteractionBindingsCanonicalJsonV1.Hash(graph),
            StrategyInteractionBindingsCanonicalJsonV1.Hash(graph));
        Assert.NotEqual(
            StrategyInteractionBindingsCanonicalJsonV1.Hash(graph),
            StrategyInteractionBindingsCanonicalJsonV1.Hash(changed));
    }

    private static AuthoredUnitSpecificationV1 Visualizer(bool withBindings)
    {
        var specification = new AuthoredUnitSpecificationV1(
            AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            "btc-ema-chart",
            "BTC EMA chart",
            "Show BTC candles with EMA 9.",
            AuthoredUnitSourceKindV1.Text,
            AuthoredUnitKindV1.Visualizer,
            [new AuthoredInstrumentRequestV1(
                "primary", "BTC", new InstrumentId(42), AssetClass.Crypto, BrokerKind.Binance)],
            new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
            StrategyDataRequirement.Bars,
            [new AuthoredUnitParameterV1("fast", "Fast EMA", ParameterKind.Integer, "9", "1", "500")],
            new AuthoredChartCompositionV1(
                [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0)],
                [
                    new AuthoredChartLayerV1(
                        "candles", "price", AuthoredChartLayerKindV1.Candles, "price.candles@1",
                        new Dictionary<string, string>()),
                    new AuthoredChartLayerV1(
                        "fast-ema", "price", AuthoredChartLayerKindV1.IndicatorLine, "indicator.ema@1",
                        new Dictionary<string, string> { ["period"] = "${fast}" }),
                ]),
            [],
            [],
            AuthoredUnitExecutionIntentV1.None);
        return withBindings
            ? StrategyInteractionBindingsFactoryV1.UpgradeTrustedSpecification(specification)
            : specification;
    }

    private static AuthoredUnitSpecificationV1 Strategy()
    {
        var classification = new StrategyClassificationBindingV1("ema-cross", new string('c', 64));
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
                    "ema-cross-target",
                    StrategySemanticStageV1.DecideIntent,
                    StrategySemanticDispositionV1.Applicable,
                    "Set the reviewed target when the fast EMA crosses the slow EMA.",
                    true,
                    new StrategyRequirementProvenanceV1(
                        ["candidate-statement-1"],
                        ["research-evidence-1"],
                        "Confirmed by the user.")),
            ],
            new string('3', 64));
        return StrategyInteractionBindingsFactoryV1.UpgradeTrustedSpecification(
            Visualizer(withBindings: false) with
            {
                Kind = AuthoredUnitKindV1.Strategy,
                ExecutionIntent = AuthoredUnitExecutionIntentV1.PaperTargets,
                StrategyClassification = classification,
                ConfirmedStrategyIntent = intent,
            });
    }
}
