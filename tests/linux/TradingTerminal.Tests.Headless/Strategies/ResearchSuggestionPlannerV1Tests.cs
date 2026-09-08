using TradingTerminal.Core.Strategies.Generation;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class ResearchSuggestionPlannerV1Tests
{
    [Fact]
    public void Idle_unrelated_turn_shows_no_follow_ups()
    {
        var chips = ResearchSuggestionPlannerV1.PlanForTurn(new ResearchSuggestionPlannerV1.TurnContext(
            LastUserText: null,
            SampleCount: 0,
            CanRunExperiment: false,
            HasExperimentEvidence: false,
            HasGalleryMatches: false,
            HasFocusedGalleryMatch: false,
            CanRunHistoricalValidation: false,
            CanOpenPaperScreen: false,
            IsRegistered: false));

        Assert.Empty(chips);
    }

    [Fact]
    public void Indicator_turn_offers_apply_overlay_follow_up()
    {
        var chips = ResearchSuggestionPlannerV1.PlanForTurn(new ResearchSuggestionPlannerV1.TurnContext(
            LastUserText: "show RSI and EMA on the chart",
            SampleCount: 0,
            CanRunExperiment: false,
            HasExperimentEvidence: false,
            HasGalleryMatches: false,
            HasFocusedGalleryMatch: false,
            CanRunHistoricalValidation: false,
            CanOpenPaperScreen: false,
            IsRegistered: false));

        Assert.Contains(chips, static c => c.Id == "apply-ema-rsi");
        Assert.True(chips.Count <= ResearchSuggestionPlannerV1.MaxFollowUps);
    }

    [Fact]
    public void Before_jump_turn_offers_capture_and_optional_gallery()
    {
        var chips = ResearchSuggestionPlannerV1.PlanForTurn(new ResearchSuggestionPlannerV1.TurnContext(
            LastUserText: "charts before a big +5% jump",
            SampleCount: 0,
            CanRunExperiment: false,
            HasExperimentEvidence: false,
            HasGalleryMatches: true,
            HasFocusedGalleryMatch: false,
            CanRunHistoricalValidation: false,
            CanOpenPaperScreen: false,
            IsRegistered: false));

        Assert.Contains(chips, static c => c.ScanId == "next-day-plus-5");
        Assert.Contains(chips, static c => c.Kind == ResearchQuickSuggestionKindV1.FocusFirstGalleryMatch);
    }

    [Fact]
    public void Four_samples_ready_puts_experiment_follow_up()
    {
        var chips = ResearchSuggestionPlannerV1.PlanForTurn(new ResearchSuggestionPlannerV1.TurnContext(
            LastUserText: null,
            SampleCount: 4,
            CanRunExperiment: true,
            HasExperimentEvidence: false,
            HasGalleryMatches: true,
            HasFocusedGalleryMatch: true,
            CanRunHistoricalValidation: false,
            CanOpenPaperScreen: false,
            IsRegistered: false));

        Assert.Equal(ResearchQuickSuggestionKindV1.RunResearchExperiment, chips[0].Kind);
    }

    [Fact]
    public void Registered_with_validation_ready_offers_backtest_style_gate()
    {
        var chips = ResearchSuggestionPlannerV1.PlanForTurn(new ResearchSuggestionPlannerV1.TurnContext(
            LastUserText: "ready for backtest",
            SampleCount: 4,
            CanRunExperiment: false,
            HasExperimentEvidence: true,
            HasGalleryMatches: false,
            HasFocusedGalleryMatch: false,
            CanRunHistoricalValidation: true,
            CanOpenPaperScreen: false,
            IsRegistered: true));

        Assert.Contains(chips, static c => c.Kind == ResearchQuickSuggestionKindV1.RunHistoricalValidation);
    }
}
