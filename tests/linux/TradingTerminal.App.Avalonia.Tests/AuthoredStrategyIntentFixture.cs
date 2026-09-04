using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.App.Avalonia.Tests;

internal static class AuthoredStrategyIntentFixture
{
    public static ConfirmedStrategyIntentV1 Create(
        StrategyClassificationBindingV1 classification,
        StrategyIntentKindV1 kind = StrategyIntentKindV1.PositionTarget) => new(
        ConfirmedStrategyIntentV1.CurrentSchemaVersion,
        "intent-1",
        "candidate-1",
        1,
        new string('1', 64),
        new string('2', 64),
        classification,
        new StrategyIntentModelV1(kind),
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
                    "Required by this executable test strategy.")),
        ],
        new string('3', 64));
}
