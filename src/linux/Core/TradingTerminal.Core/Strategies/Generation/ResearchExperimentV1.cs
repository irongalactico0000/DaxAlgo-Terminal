using TradingTerminal.Core.Strategies.Definition;

namespace TradingTerminal.Core.Strategies.Generation;

public sealed record ResearchFeatureDefinitionV1(
    string Name,
    string Source,
    string Description);

public sealed record ResearchFeatureTransformV1(
    string FeatureName,
    double TrainingMean,
    double TrainingStandardDeviation);

public sealed record ResearchFeatureWeightV1(
    string FeatureName,
    double TrainingCorrelation,
    double FormulaWeight);

public sealed record ResearchFeatureVectorV1(
    string EventSampleId,
    DateTimeOffset ObservationFromUtc,
    DateTimeOffset ObservationToUtc,
    ResearchEventLabelKindV1 Label,
    IReadOnlyList<double> RawValues,
    IReadOnlyList<double> NormalizedValues,
    int BarCount,
    int QuoteCount,
    int TradeCount,
    int DepthSnapshotCount);

public sealed record ResearchChronologicalSplitV1(
    int TrainingCount,
    int ValidationCount,
    int TestCount,
    DateTimeOffset TrainingEndUtc,
    DateTimeOffset ValidationEndUtc);

/// <summary>
/// Reproducible exploratory evidence produced from labeled event observations. It is deliberately
/// not historical strategy validation: its holdout metrics rank a simple feature formula and cannot
/// populate the workspace validation or Paper bindings.
/// </summary>
public sealed record ResearchExperimentEvidenceV1(
    string SchemaVersion,
    string DatasetHashSha256,
    IReadOnlyList<ResearchFeatureDefinitionV1> Features,
    IReadOnlyList<ResearchFeatureTransformV1> TrainingTransforms,
    IReadOnlyList<ResearchFeatureWeightV1> RankedFeatures,
    IReadOnlyList<ResearchFeatureVectorV1> SamplesChronological,
    ResearchChronologicalSplitV1 Split,
    string Formula,
    double TrainingAccuracy,
    double ValidationAccuracy,
    double TestAccuracy,
    string PromotionStatement)
{
    public const string CurrentSchemaVersion = "research-experiment/v1";
    public const string NonPromotionalStatement =
        "Exploratory labeled-event evidence only; run exact historical validation before Paper promotion.";
}

public interface IResearchExperimentRunnerV1
{
    Task<ResearchExperimentEvidenceV1> RunAsync(
        ResearchDatasetDefinitionV1 dataset,
        CancellationToken cancellationToken = default);
}

public static class ResearchExperimentCanonicalJsonV1
{
    public static string Serialize(ResearchExperimentEvidenceV1 value) =>
        ExecutableStrategyDefinitionCanonicalJson.Serialize(value);

    public static ResearchExperimentEvidenceV1 Deserialize(string json)
    {
        var value = ExecutableStrategyDefinitionCanonicalJson.Deserialize<ResearchExperimentEvidenceV1>(json);
        ResearchExperimentValidatorV1.RequireValid(value);
        return value;
    }

    public static string Hash(ResearchExperimentEvidenceV1 value) =>
        ExecutableStrategyDefinitionCanonicalJson.Hash(value);
}

public static class ResearchExperimentValidatorV1
{
    public static void RequireValid(ResearchExperimentEvidenceV1 evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        var errors = new List<string>();
        if (evidence.SchemaVersion != ResearchExperimentEvidenceV1.CurrentSchemaVersion)
            errors.Add("RESEARCH_EXPERIMENT_SCHEMA_UNSUPPORTED");
        if (!IsSha256(evidence.DatasetHashSha256))
            errors.Add("RESEARCH_EXPERIMENT_DATASET_HASH_INVALID");
        if (evidence.Features is null || evidence.Features.Count == 0)
            errors.Add("RESEARCH_EXPERIMENT_FEATURES_REQUIRED");
        if (evidence.TrainingTransforms is null || evidence.Features is null ||
            evidence.TrainingTransforms.Count != evidence.Features.Count)
            errors.Add("RESEARCH_EXPERIMENT_TRANSFORMS_MISMATCH");
        if (evidence.SamplesChronological is null || evidence.SamplesChronological.Count < 4)
            errors.Add("RESEARCH_EXPERIMENT_SAMPLES_INSUFFICIENT");
        if (evidence.Split is null || evidence.SamplesChronological is null ||
            evidence.Split.TrainingCount < 2 || evidence.Split.ValidationCount < 1 || evidence.Split.TestCount < 1 ||
            evidence.Split.TrainingCount + evidence.Split.ValidationCount + evidence.Split.TestCount != evidence.SamplesChronological.Count)
            errors.Add("RESEARCH_EXPERIMENT_SPLIT_INVALID");
        if (evidence.SamplesChronological is not null &&
            !evidence.SamplesChronological.SequenceEqual(evidence.SamplesChronological
                .OrderBy(sample => sample.ObservationToUtc)
                .ThenBy(sample => sample.EventSampleId, StringComparer.Ordinal)))
            errors.Add("RESEARCH_EXPERIMENT_NOT_CHRONOLOGICAL");
        if (evidence.Features is not null && evidence.SamplesChronological is not null &&
            evidence.SamplesChronological.Any(sample =>
                sample.ObservationFromUtc >= sample.ObservationToUtc ||
                sample.RawValues.Count != evidence.Features.Count ||
                sample.NormalizedValues.Count != evidence.Features.Count ||
                sample.RawValues.Concat(sample.NormalizedValues).Any(value => !double.IsFinite(value))))
            errors.Add("RESEARCH_EXPERIMENT_VECTOR_INVALID");
        if (evidence.TrainingTransforms is not null && evidence.TrainingTransforms.Any(transform =>
                !double.IsFinite(transform.TrainingMean) ||
                !double.IsFinite(transform.TrainingStandardDeviation) ||
                transform.TrainingStandardDeviation <= 0))
            errors.Add("RESEARCH_EXPERIMENT_TRANSFORM_INVALID");
        if (string.IsNullOrWhiteSpace(evidence.Formula))
            errors.Add("RESEARCH_EXPERIMENT_FORMULA_REQUIRED");
        if (evidence.PromotionStatement != ResearchExperimentEvidenceV1.NonPromotionalStatement)
            errors.Add("RESEARCH_EXPERIMENT_PROMOTION_STATEMENT_INVALID");
        if (new[] { evidence.TrainingAccuracy, evidence.ValidationAccuracy, evidence.TestAccuracy }
            .Any(value => !double.IsFinite(value) || value is < 0 or > 1))
            errors.Add("RESEARCH_EXPERIMENT_METRIC_INVALID");
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(evidence));
    }

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
