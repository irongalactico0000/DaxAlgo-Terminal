using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using TradingTerminal.Core.Strategies.Definition;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class ChartReferenceInspectorV1Tests
{
    [Fact]
    public async Task Inspector_sends_exact_image_and_accepts_hash_bound_observations()
    {
        var bytes = Encoding.UTF8.GetBytes("chart-image-fixture");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var reference = new AuthoredChartReferenceV1(
            "chart-ref-fixture",
            hash,
            "image/png",
            [ChartReferenceSimilarityV1.IndicatorComposition]);
        var observation = new AuthoredChartReferenceInspectionV1(
            reference.ReferenceId,
            hash,
            "Candlesticks with two moving-average overlays.",
            "candles",
            "BTCUSD",
            "1 minute",
            ["EMA 9", "EMA 21"],
            "Dark background with green/red candles.",
            null);
        var provider = new FakeCodegenClient(
            ExecutableStrategyDefinitionCanonicalJson.Serialize(observation));

        var result = await new ChartReferenceInspectorV1().InspectAsync(
            provider,
            new ChartReferenceInspectionRequestV1(reference, bytes));

        result.Success.Should().BeTrue(result.Error);
        result.Inspection.Should().BeEquivalentTo(observation);
        var image = provider.LastRequest!.Messages.Single().Images.Should().ContainSingle().Subject;
        image.MediaType.Should().Be("image/png");
        image.ContentHashSha256.Should().Be(hash);
        Convert.FromBase64String(image.Base64Data).Should().Equal(bytes);
    }

    [Fact]
    public async Task Market_similarity_requires_a_query_for_the_later_real_history_search()
    {
        var bytes = Encoding.UTF8.GetBytes("pattern-chart-fixture");
        var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var reference = new AuthoredChartReferenceV1(
            "chart-ref-pattern",
            hash,
            "image/png",
            [ChartReferenceSimilarityV1.MarketPattern]);
        var incomplete = new AuthoredChartReferenceInspectionV1(
            reference.ReferenceId,
            hash,
            "Visible rounded consolidation.",
            "candles",
            null,
            null,
            [],
            null,
            null);
        var provider = new FakeCodegenClient(
            ExecutableStrategyDefinitionCanonicalJson.Serialize(incomplete));

        var result = await new ChartReferenceInspectorV1().InspectAsync(
            provider,
            new ChartReferenceInspectionRequestV1(reference, bytes));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("historical-search query");
    }

    [Fact]
    public async Task Modified_reference_bytes_never_reach_the_provider()
    {
        var expected = Encoding.UTF8.GetBytes("original");
        var reference = new AuthoredChartReferenceV1(
            "chart-ref-tampered",
            Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant(),
            "image/png",
            [ChartReferenceSimilarityV1.VisualStyle]);
        var provider = new FakeCodegenClient("{}");

        var result = await new ChartReferenceInspectorV1().InspectAsync(
            provider,
            new ChartReferenceInspectionRequestV1(reference, Encoding.UTF8.GetBytes("tampered")));

        result.Success.Should().BeFalse();
        result.Error.Should().Contain("do not match");
        provider.CallCount.Should().Be(0);
    }
}
