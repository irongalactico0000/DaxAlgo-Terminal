using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingTerminal.Core.Strategies.Generation;

/// <summary>
/// Kind of user-authored chart indicator. Kept small so every kind can render on the native Charts
/// surface and appear in the chat catalog without inventing unsupported type ids.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UserChartIndicatorKindV1
{
    Sma,
    Ema,
    Rsi,
    Atr,
}

/// <summary>
/// One user-defined indicator loaded from <c>user-indicators.json</c>. Users add entries; the host
/// merges them into Charts toggles and the chat overlay catalog.
/// </summary>
public sealed record UserChartIndicatorDefinitionV1(
    string Id,
    string DisplayName,
    UserChartIndicatorKindV1 Kind,
    int Period = 14,
    string? Alias = null)
{
    public string NormalizedId =>
        string.IsNullOrWhiteSpace(Id) ? string.Empty : Id.Trim().ToLowerInvariant();
}

/// <summary>Loads and validates user indicator definitions from a JSON file.</summary>
public static class UserChartIndicatorCatalogV1
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static IReadOnlyList<UserChartIndicatorDefinitionV1> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<UserChartIndicatorDefinitionV1>();

        var parsed = JsonSerializer.Deserialize<UserChartIndicatorFileV1>(json, JsonOptions);
        return Validate(parsed?.Indicators ?? Array.Empty<UserChartIndicatorDefinitionV1>());
    }

    public static IReadOnlyList<UserChartIndicatorDefinitionV1> Validate(
        IEnumerable<UserChartIndicatorDefinitionV1> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var result = new List<UserChartIndicatorDefinitionV1>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in definitions)
        {
            if (item is null) continue;
            var id = item.NormalizedId;
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id))
                continue;
            if (string.IsNullOrWhiteSpace(item.DisplayName))
                continue;
            if (item.Period is < 2 or > 500)
                continue;
            if (!Enum.IsDefined(item.Kind))
                continue;
            result.Add(item with { Id = id, DisplayName = item.DisplayName.Trim() });
        }

        return result;
    }

    public static string ToAuthoredTypeId(UserChartIndicatorKindV1 kind) => kind switch
    {
        UserChartIndicatorKindV1.Sma => "indicator.sma@1",
        UserChartIndicatorKindV1.Ema => "indicator.ema@1",
        UserChartIndicatorKindV1.Rsi => "indicator.rsi@1",
        UserChartIndicatorKindV1.Atr => "indicator.atr@1",
        _ => "indicator.sma@1",
    };

    private sealed record UserChartIndicatorFileV1(
        IReadOnlyList<UserChartIndicatorDefinitionV1>? Indicators);
}
