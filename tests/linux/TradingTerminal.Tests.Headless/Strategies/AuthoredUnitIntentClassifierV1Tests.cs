using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class AuthoredUnitIntentClassifierV1Tests
{
    [Theory]
    [InlineData("Show BTC candles with EMA 9/21", AuthoredUnitKindV1.Visualizer)]
    [InlineData("Show an index with a similar historical chart", AuthoredUnitKindV1.Visualizer)]
    [InlineData("Buy BTC when EMA 9 crosses EMA 21 and show the chart", AuthoredUnitKindV1.Strategy)]
    public async Task Routes_chart_only_and_position_changing_requests_to_different_workflows(
        string request,
        AuthoredUnitKindV1 expected)
    {
        var provider = new FixedClassifierProvider(new AuthoredUnitIntentClassificationV1(
            expected,
            "high",
            expected == AuthoredUnitKindV1.Visualizer
                ? "The request only asks to draw data."
                : "The request explicitly asks to buy."));

        var result = await new AuthoredUnitIntentClassifierV1().ClassifyAsync(provider, request);

        Assert.True(result.Success);
        Assert.Equal(expected, result.Classification!.Kind);
        Assert.Contains("Historical chart", provider.LastRequest!.SystemContext, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ambiguous_execution_intent_returns_a_question_instead_of_guessing()
    {
        var provider = new FixedClassifierProvider(new AuthoredUnitIntentClassificationV1(
            AuthoredUnitKindV1.Visualizer,
            "low",
            "The word crossover can mean a drawn indicator or a trade trigger.",
            "Should the crossover only be shown, or should it change a Paper position?"));

        var result = await new AuthoredUnitIntentClassifierV1().ClassifyAsync(
            provider,
            "EMA crossover like this chart");

        Assert.False(result.Success);
        Assert.NotNull(result.Classification?.ClarificationQuestion);
        Assert.Empty(result.Issues);
    }

    private sealed class FixedClassifierProvider(AuthoredUnitIntentClassificationV1 classification)
        : IStrategyCodegenClient
    {
        public string ProviderId => "fixed-classifier";
        public string DisplayName => "Fixed classifier";
        public bool IsAvailable => true;
        public StrategyCodegenRequest? LastRequest { get; private set; }

        public Task<StrategyCodegenResponse> GenerateAsync(
            StrategyCodegenRequest request,
            CancellationToken ct = default)
        {
            LastRequest = request;
            var json = TradingTerminal.Core.Strategies.Definition.ExecutableStrategyDefinitionCanonicalJson
                .Serialize(classification);
            return Task.FromResult(new StrategyCodegenResponse(true, null, json, null, [], CodegenUsage.None));
        }
    }
}
