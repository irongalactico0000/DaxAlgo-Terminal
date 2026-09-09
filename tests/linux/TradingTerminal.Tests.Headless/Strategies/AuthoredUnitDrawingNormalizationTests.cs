using System.Text.Json.Nodes;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class AuthoredUnitDrawingNormalizationTests
{
    [Fact]
    public void Normalize_coerces_stringified_and_array_drawing_into_object_shape()
    {
        var asString = """
            {"unitId":"u1","confirmedStrategyIntent":"null","drawing":"{\"panes\":[{\"paneId\":\"price\",\"role\":\"price\",\"order\":0}],\"layers\":[{\"layerId\":\"candles\",\"paneId\":\"price\",\"kind\":\"candles\",\"typeId\":\"price.candles@1\",\"parameters\":{}}]}"}
            """;
        var normalizedString = AuthoredUnitSpecificationGeneratorV1.NormalizeCommonModelJsonMistakes(asString);
        Assert.NotNull(normalizedString);
        var rootFromString = JsonNode.Parse(normalizedString!)!.AsObject();
        Assert.Null(rootFromString["confirmedStrategyIntent"]);
        Assert.True(rootFromString["drawing"] is JsonObject);

        var asArray = """
            {"unitId":"u1","drawing":[{"layerId":"ema-20","paneId":"price","kind":"indicatorLine","typeId":"indicator.ema@1","parameters":{"period":"20"}}]}
            """;
        var normalizedArray = AuthoredUnitSpecificationGeneratorV1.NormalizeCommonModelJsonMistakes(asArray);
        Assert.NotNull(normalizedArray);
        var drawing = JsonNode.Parse(normalizedArray!)!["drawing"]!.AsObject();
        Assert.True(drawing["panes"] is JsonArray);
        Assert.True(drawing["layers"] is JsonArray { Count: >= 2 });
    }
}
