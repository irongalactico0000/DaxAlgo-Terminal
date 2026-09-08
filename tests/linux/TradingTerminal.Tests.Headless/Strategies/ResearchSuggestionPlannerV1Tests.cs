using TradingTerminal.Core.Strategies.Generation;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class ResearchSuggestionPlannerV1Tests
{
    [Fact]
    public void Idle_shows_default_capture_conditions_not_experiment()
    {
        var chips = ResearchSuggestionPlannerV1.Plan(
            sampleCount: 0,
            canRunExperiment: false,
            hasExperimentEvidence: false,
            hasFocusedGalleryMatch: false,
            composerOrNeedText: null);

        Assert.Contains(chips, static c => c.ScanId == "next-day-plus-5");
        Assert.DoesNotContain(chips, static c => c.Kind == ResearchQuickSuggestionKindV1.RunResearchExperiment);
        Assert.DoesNotContain(chips, static c => c.Id == "famous");
    }

    [Fact]
    public void Typed_crash_need_only_surfaces_crash_condition()
    {
        var chips = ResearchSuggestionPlannerV1.Plan(
            sampleCount: 1,
            canRunExperiment: false,
            hasExperimentEvidence: false,
            hasFocusedGalleryMatch: false,
            composerOrNeedText: "I need before crash -5% dumps");

        Assert.Contains(chips, static c => c.ScanId == "pre-crash");
        Assert.DoesNotContain(chips, static c => c.ScanId == "next-day-plus-5");
        Assert.DoesNotContain(chips, static c => c.ScanId == "pre-breakout");
    }

    [Fact]
    public void Four_samples_ready_puts_experiment_first()
    {
        var chips = ResearchSuggestionPlannerV1.Plan(
            sampleCount: 4,
            canRunExperiment: true,
            hasExperimentEvidence: false,
            hasFocusedGalleryMatch: true,
            composerOrNeedText: string.Empty);

        Assert.Equal(ResearchQuickSuggestionKindV1.RunResearchExperiment, chips[0].Kind);
        Assert.Contains(chips, static c => c.Id == "indicators-on-focus");
    }

    [Fact]
    public void Indicator_need_adds_indicator_chip()
    {
        var chips = ResearchSuggestionPlannerV1.Plan(
            sampleCount: 0,
            canRunExperiment: false,
            hasExperimentEvidence: false,
            hasFocusedGalleryMatch: false,
            composerOrNeedText: "show rsi before jump +5%");

        Assert.Contains(chips, static c => c.ScanId == "next-day-plus-5");
        Assert.Contains(chips, static c => c.Id == "indicators-on-focus");
    }
}
