using TradingTerminal.Core.Strategies.Definition;

namespace TradingTerminal.Core.Strategies.Generation;

/// <summary>
/// Immutable identity supplied to a historical runner. A completed run is admissible only while
/// these exact authored artifacts remain current.
/// </summary>
public sealed record HistoricalValidationContextV1(
    string WorkspaceId,
    string AuthoredUnitSpecificationHashSha256,
    string BuildArtifactHashSha256,
    string? FeatureSetHashSha256);

/// <summary>
/// Historical replay evidence for one exact compiled authored strategy. This is deliberately
/// separate from exploratory research evidence and synthetic TradeIR smoke results.
/// </summary>
public sealed record HistoricalValidationEvidenceV1(
    string SchemaVersion,
    HistoricalValidationContextV1 Context,
    string ParametersHashSha256,
    DateTime FromUtc,
    DateTime ToUtc,
    string DataMode,
    string FeedQuality,
    int TradeCount,
    double StartingCash,
    double EndingCash,
    double TotalFees,
    DateTime CompletedUtc)
{
    public const string CurrentSchemaVersion = "historical-validation-evidence/v1";
}

public static class HistoricalValidationEvidenceCanonicalJsonV1
{
    public static string Serialize(HistoricalValidationEvidenceV1 value) =>
        ExecutableStrategyDefinitionCanonicalJson.Serialize(value);

    public static HistoricalValidationEvidenceV1 Deserialize(string json)
    {
        var value = ExecutableStrategyDefinitionCanonicalJson.Deserialize<HistoricalValidationEvidenceV1>(json);
        HistoricalValidationEvidenceValidatorV1.RequireValid(value);
        return value;
    }

    public static string Hash(HistoricalValidationEvidenceV1 value) =>
        ExecutableStrategyDefinitionCanonicalJson.Hash(value);
}

public static class HistoricalValidationEvidenceValidatorV1
{
    public static void RequireValid(HistoricalValidationEvidenceV1 value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.SchemaVersion != HistoricalValidationEvidenceV1.CurrentSchemaVersion)
            throw new ArgumentException("Unsupported historical validation evidence schema.", nameof(value));
        ArgumentNullException.ThrowIfNull(value.Context);
        if (string.IsNullOrWhiteSpace(value.Context.WorkspaceId))
            throw new ArgumentException("A workspace id is required.", nameof(value));
        RequireHash(value.Context.AuthoredUnitSpecificationHashSha256, "specification");
        RequireHash(value.Context.BuildArtifactHashSha256, "build artifact");
        if (value.Context.FeatureSetHashSha256 is not null)
            RequireHash(value.Context.FeatureSetHashSha256, "feature set");
        RequireHash(value.ParametersHashSha256, "parameters");
        if (value.FromUtc.Kind != DateTimeKind.Utc || value.ToUtc.Kind != DateTimeKind.Utc ||
            value.CompletedUtc.Kind != DateTimeKind.Utc || value.FromUtc >= value.ToUtc)
            throw new ArgumentException("Historical validation timestamps must be ordered UTC values.", nameof(value));
        if (string.IsNullOrWhiteSpace(value.DataMode) || string.IsNullOrWhiteSpace(value.FeedQuality))
            throw new ArgumentException("Historical validation data mode and feed quality are required.", nameof(value));
        if (value.TradeCount < 0 || !double.IsFinite(value.StartingCash) ||
            !double.IsFinite(value.EndingCash) || !double.IsFinite(value.TotalFees) || value.TotalFees < 0d)
            throw new ArgumentException("Historical validation metrics must be finite and non-negative where applicable.", nameof(value));
    }

    private static void RequireHash(string value, string name)
    {
        if (value is not { Length: 64 } || value.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')))
            throw new ArgumentException($"The {name} binding must be a lowercase SHA-256 value.");
    }
}
