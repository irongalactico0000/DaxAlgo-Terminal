using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class ResearchDatasetV1Tests
{
    [Fact]
    public void Observation_and_future_outcome_are_separate_and_canonical()
    {
        var dataset = Dataset(Selection());

        Assert.Empty(ResearchDatasetValidatorV1.Validate(dataset, requireSamples: true));
        var json = ResearchDatasetCanonicalJsonV1.Serialize(dataset);
        var restored = ResearchDatasetCanonicalJsonV1.Deserialize(json);
        Assert.Equal(dataset.DatasetId, restored.DatasetId);
        Assert.Equal(dataset.Samples, restored.Samples);
        Assert.Equal(ResearchDatasetCanonicalJsonV1.Hash(dataset), ResearchDatasetCanonicalJsonV1.Hash(restored));
    }

    [Fact]
    public void Outcome_overlap_is_rejected_as_feature_leakage()
    {
        var selection = Selection() with
        {
            OutcomeFromUtc = Selection().ObservationToUtc.AddSeconds(-1),
        };

        var issues = ResearchDatasetValidatorV1.Validate(Dataset(selection), requireSamples: true);

        Assert.Contains(issues, issue => issue.Code == "RESEARCH_OUTCOME_OVERLAP");
    }

    [Fact]
    public void Unsafe_normalization_or_random_split_is_rejected()
    {
        var dataset = Dataset(Selection()) with
        {
            LeakagePolicy = new ResearchLeakagePolicyV1(
                ExcludeOutcomeWindowFromFeatures: true,
                FitTransformsOnTrainingOnly: false,
                UseChronologicalSplits: false,
                PurgeOrEmbargo: TimeSpan.Zero),
        };

        var issues = ResearchDatasetValidatorV1.Validate(dataset);

        Assert.Contains(issues, issue => issue.Code == "RESEARCH_TRANSFORM_LEAKAGE_FORBIDDEN");
        Assert.Contains(issues, issue => issue.Code == "RESEARCH_RANDOM_SPLIT_FORBIDDEN");
    }

    private static ResearchDatasetDefinitionV1 Dataset(ResearchChartSelectionV1 selection) => new(
        ResearchDatasetDefinitionV1.CurrentSchemaVersion,
        "breakout-events",
        new string('a', 64),
        StrategyDataRequirement.Bars | StrategyDataRequirement.TradeTape,
        ResearchLeakagePolicyV1.SafeDefault,
        [
            new ResearchEventSampleV1(
                ResearchEventSampleV1.CurrentSchemaVersion,
                "event-1",
                selection,
                ResearchEventLabelKindV1.PreBreakout,
                null,
                ResearchEventLabelSourceV1.Manual),
        ]);

    private static ResearchChartSelectionV1 Selection()
    {
        var start = new DateTimeOffset(2026, 9, 5, 1, 0, 0, TimeSpan.Zero);
        return new ResearchChartSelectionV1(
            new InstrumentId(42),
            "BTC-USD",
            BarSize.OneMinute,
            start,
            start.AddMinutes(10),
            start.AddMinutes(10),
            start.AddMinutes(15),
            StrategyDataRequirement.Bars | StrategyDataRequirement.TradeTape);
    }
}
