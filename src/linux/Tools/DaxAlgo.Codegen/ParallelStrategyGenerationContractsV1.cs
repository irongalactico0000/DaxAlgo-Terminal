using System.Text.Json;
using System.Text.Json.Serialization;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Definition;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.Infrastructure.Strategies.Authoring;

/// <summary>The four authoring representations offered for every strategy brief.</summary>
public enum StrategyGenerationLaneV1
{
    VibePython = 1,
    DeclarativeSpec = 2,
    TypedGraph = 3,
    CspPython = 4,
}

/// <summary>Transient lifecycle state for one lane in a concurrent four-lane generation call.</summary>
public enum StrategyGenerationLaneProgressStateV1
{
    Queued = 1,
    PreparingRequest = 2,
    WaitingForModel = 3,
    ParsingResponse = 4,
    ValidatingArtifact = 5,
    Completed = 6,
    Failed = 7,
    Canceled = 8,
    RepairingResponse = 9,
}

public sealed record StrategyGenerationLaneProgressV1(
    StrategyGenerationLaneV1 Lane,
    StrategyGenerationLaneProgressStateV1 State,
    string? Detail = null,
    StrategyGenerationLaneResultV1? Result = null);

public enum StrategyGenerationArtifactKindV1
{
    VibePythonSource = 1,
    DeclarativeStrategyJson = 2,
    TradeIrModuleJson = 3,
    CspPythonSource = 4,
}

/// <summary>
/// Honest handoff state for a generated lane. Generation, package validation, and package tests are
/// separate facts; the strategy builder never promotes one into another by implication.
/// </summary>
public enum StrategyGenerationReadinessV1
{
    Failed = 1,
    Unsupported = 2,
    Generated = 3,
    PackageValid = 4,

    /// <summary>Reserved for a future package-owned, hash-bound test-evidence contract.</summary>
    TestPassed = 5,

    /// <summary>The provider returned output, but deterministic authoring validation rejected it.</summary>
    Invalid = 6,
}

/// <summary>
/// Host-owned decision about whether an invalid lane result is safe to send back to the model for
/// one bounded formatting/shape repair. This is intentionally separate from readiness: missing facts
/// and unsupported semantics remain inspectable invalid drafts, but another model call cannot make
/// either condition true.
/// </summary>
public enum StrategyGenerationAutomaticRepairDispositionV1
{
    /// <summary>The lane is not invalid, so automatic model repair does not apply.</summary>
    NotApplicable = 1,

    /// <summary>The model response is malformed or violates a deterministically repairable draft shape.</summary>
    RepairableModelOutput = 2,

    /// <summary>Host-confirmed strategy/data facts are required before generation can continue.</summary>
    NeedsSharedFacts = 3,

    /// <summary>The requested semantics are not represented by the installed authoritative catalog.</summary>
    UnsupportedSemantic = 4,

    /// <summary>The failure is not proven safe for one bounded model shape-regeneration request.</summary>
    NonRepairable = 5,
}

/// <summary>The meaning assigned to an artifact by its normative contract.</summary>
public enum StrategyGenerationSemanticRoleV1
{
    SourceReview = 1,
    CanonicalExecutableIr = 2,
}

/// <summary>How an artifact can reach the canonical TradeIR target.</summary>
public enum StrategyGenerationLoweringModeV1
{
    /// <summary>
    /// Legacy proposal mode. A model-authored replacement is a separate review artifact and is not a
    /// semantic lowerer or equivalence proof.
    /// </summary>
    ReviewedAiSynthesis = 1,

    /// <summary>The artifact is already the canonical target representation.</summary>
    Identity = 2,

    /// <summary>
    /// Canonical execution requires a deterministic, versioned, fail-closed lowerer. Until one is
    /// registered, this artifact remains a source-review representation only.
    /// </summary>
    DeterministicLowererRequired = 3,
}

/// <summary>Evidence state for compatibility with a separately governed external format or runtime.</summary>
public enum StrategyGenerationExternalCompatibilityV1
{
    NotApplicable = 1,
    Unverified = 2,
    Verified = 3,
}

/// <summary>
/// Normative meaning and transformation boundary for one authoring contract. This is deliberately
/// separate from the validator implementation and package hash: structural validation never grants
/// runtime compatibility by implication.
/// </summary>
public sealed record StrategyGenerationContractAuthorityV1(
    string AuthorityId,
    string SpecificationReference,
    StrategyGenerationSemanticRoleV1 SemanticRole,
    string CanonicalTargetContract,
    StrategyGenerationLoweringModeV1 LoweringMode,
    string? ExternalReference,
    StrategyGenerationExternalCompatibilityV1 ExternalCompatibility);

/// <summary>
/// Exact host-owned contract against which an artifact was generated. The operator catalog remains
/// separate because its semantic hash changes independently from the module schema.
/// </summary>
public sealed record StrategyGenerationPackageBindingV1(
    string PackageId,
    string PackageVersion,
    string PackageImplementationHashSha256,
    string ArtifactContract,
    string ArtifactContractVersion,
    string ValidatorId,
    string? ImporterId,
    StrategyOperatorCatalogReferenceV1? OperatorCatalog,
    StrategyGenerationContractAuthorityV1 Authority);

public enum StrategyVariationAxisKindV1
{
    Parameter = 1,
    Indicator = 2,
    Rule = 3,
    Exit = 4,
    Structure = 5,
}

/// <summary>
/// One user-adjustable value. Values remain strings because this is an authoring proposal; the
/// selected package owns the authoritative parameter type and conversion rules.
/// </summary>
public sealed record StrategyGenerationParameterV1(
    string Name,
    string ValueType,
    [property: JsonConverter(typeof(StrategyGenerationParameterDefaultValueJsonConverter))]
    string DefaultValue,
    string? Unit,
    string Description);

/// <summary>
/// Model providers commonly preserve a parameter's native JSON scalar type even though the shared
/// comparison envelope stores proposed defaults as strings. Accept only scalar tokens and normalize
/// them to their invariant JSON spelling; serialization remains a string so hashes and saved batches
/// retain the existing candidate-v2 representation.
/// </summary>
internal sealed class StrategyGenerationParameterDefaultValueJsonConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString();
        if (reader.TokenType == JsonTokenType.True)
            return bool.TrueString.ToLowerInvariant();
        if (reader.TokenType == JsonTokenType.False)
            return bool.FalseString.ToLowerInvariant();
        if (reader.TokenType == JsonTokenType.Number)
        {
            using var value = JsonDocument.ParseValue(ref reader);
            return ExecutableStrategyDefinitionCanonicalJson.Canonicalize(
                value.RootElement.GetRawText());
        }

        throw new JsonException(
            "A strategy parameter defaultValue must be a JSON string, number, or boolean scalar.");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}

/// <summary>A named way to fork the generated strategy without rewriting the whole prompt.</summary>
public sealed record StrategyVariationAxisV1(
    string AxisId,
    StrategyVariationAxisKindV1 Kind,
    string Description,
    IReadOnlyList<string> Choices);

/// <summary>
/// Exactly one editable lane-native artifact. Code lanes use <see cref="Source"/> and set
/// <see cref="Document"/> to null; JSON lanes do the reverse so the model does not have to embed an
/// escaped JSON document inside another JSON string.
/// </summary>
public sealed record StrategyGenerationArtifactV1(
    StrategyGenerationArtifactKindV1 Kind,
    string FileName,
    string Language,
    string? Source,
    JsonElement? Document);

/// <summary>
/// Canonical host-owned proposal envelope assembled after a lane agent returns review metadata and
/// artifact content. Agents never own or bind its identity, provenance, package, or artifact metadata.
/// </summary>
public sealed record StrategyGenerationCandidateV1(
    string SchemaVersion,
    string CandidateId,
    StrategyGenerationLaneV1 Lane,
    string RequestHashSha256,
    StrategyGenerationPackageBindingV1 PackageBinding,
    string Title,
    string Interpretation,
    IReadOnlyList<string> UnresolvedQuestions,
    IReadOnlyList<string> Assumptions,
    IReadOnlyList<StrategyGenerationParameterV1> Parameters,
    IReadOnlyList<StrategyVariationAxisV1> VariationAxes,
    StrategyGenerationArtifactV1 Artifact,
    string Explanation,
    IReadOnlyList<string> ProposedTests,
    string? ConfirmedIntentHashSha256 = null)
{
    public const string CurrentSchemaVersion = "strategy-generation-candidate/v3";
}

/// <summary>
/// Exact host-side evidence used to re-run confirmation before any provider request. These bytes
/// are validation context only; lane prompts receive the smaller confirmed-intent payload.
/// </summary>
public sealed record StrategyGenerationConfirmedIntentContextV1(
    string CandidateCanonicalJson,
    string ResearchCaseCanonicalJson,
    string ClassificationCanonicalJson,
    string? ResearchExperimentCanonicalJson = null);

public sealed record ParallelStrategyGenerationRequestV1(
    string StrategyId,
    string UserPrompt,
    string? ConfirmedIntentCanonicalJson = null,
    string? ConfirmedIntentHashSha256 = null,
    StrategyGenerationConfirmedIntentContextV1? ConfirmedIntentContext = null);

public sealed record StrategyGenerationLaneResultV1(
    StrategyGenerationLaneV1 Lane,
    StrategyGenerationReadinessV1 Readiness,
    StrategyGenerationCandidateV1? Candidate,
    string? CandidateHashSha256,
    IReadOnlyList<StrategyCandidateGenerationIssueV1> Issues,
    StrategyGenerationAgentRunV1 AgentRun)
{
    public StrategyGenerationAutomaticRepairDispositionV1 AutomaticRepairDisposition =>
        StrategyGenerationAutomaticRepairClassifierV1.Classify(this);

    public bool Generated => Candidate is not null && CandidateHashSha256 is not null &&
        AgentRun is { Success: true };

    /// <summary>
    /// True only when the lane's exact generated candidate, authoring binding, and content hash are
    /// present and that binding names an installed package validator. Structural validators for the
    /// other three lanes intentionally do not make this true.
    /// </summary>
    public bool PackageValidationAvailable =>
        HasExactCandidateHashAndBinding() &&
        StrategyGenerationPackageCatalogV1.PackageValidationAvailable(Lane);

    /// <summary>
    /// Recomputed local selectability. The enclosing batch validator additionally binds candidate id
    /// and request hash to the batch prompt before selection is allowed.
    /// </summary>
    public bool Selectable
    {
        get
        {
            if (Readiness is not (StrategyGenerationReadinessV1.Generated or
                    StrategyGenerationReadinessV1.PackageValid) ||
                !HasExactCandidateHashAndBinding() || Candidate is null || Issues is null ||
                Issues.Any(static issue => issue is null ||
                    !Enum.IsDefined(issue.Severity) ||
                    issue.Severity == StrategyCandidateGenerationIssueSeverityV1.Error))
                return false;

            IReadOnlyList<StrategyCandidateGenerationIssueV1> validation;
            try
            {
                validation = StrategyGenerationCandidateValidatorV1.Validate(
                    Candidate,
                    Lane,
                    Candidate.CandidateId ?? string.Empty,
                    Candidate.RequestHashSha256 ?? string.Empty);
            }
            catch (Exception exception) when (IsDeterministicValidationException(exception))
            {
                return false;
            }
            if (validation.Any(static issue =>
                    issue.Severity == StrategyCandidateGenerationIssueSeverityV1.Error))
                return false;

            var expectedReadiness = StrategyGenerationPackageCatalogV1.PackageValidationAvailable(Lane)
                ? StrategyGenerationReadinessV1.PackageValid
                : StrategyGenerationReadinessV1.Generated;
            return Readiness == expectedReadiness;
        }
    }

    /// <summary>
    /// Recomputed local package/module validity. Request provenance and selectability require the
    /// enclosing batch validator because this lane result intentionally does not duplicate the prompt.
    /// </summary>
    public bool PackageValid
    {
        get
        {
            return Readiness == StrategyGenerationReadinessV1.PackageValid &&
                   PackageValidationAvailable &&
                   Selectable;
        }
    }

    private bool HasExactCandidateHashAndBinding()
    {
        if (!Generated || Candidate is null || Candidate.PackageBinding is null ||
            !StrategyGenerationPackageCatalogV1.IsSupported(Lane))
            return false;

        return Candidate.Lane == Lane &&
               Candidate.PackageBinding == StrategyGenerationPackageCatalogV1.RequireBinding(Lane) &&
               StrategyGenerationCandidateCanonicalJsonV1.TryHash(Candidate, out var actualHash, out _) &&
               string.Equals(CandidateHashSha256, actualHash, StringComparison.Ordinal);
    }

    private static bool IsDeterministicValidationException(Exception exception) =>
        exception is JsonException or ArgumentException or FormatException or InvalidOperationException or
            NotSupportedException or OverflowException;
}

public sealed record ParallelStrategyGenerationResultV1(
    string StrategyId,
    string UserPrompt,
    string PromptHashSha256,
    IReadOnlyList<StrategyGenerationLaneResultV1> Lanes,
    CodegenUsage Usage,
    string? ConfirmedIntentCanonicalJson = null,
    string? ConfirmedIntentHashSha256 = null)
{
    public bool HasPackageValidCandidate =>
        StrategyGenerationBatchValidationV1.Validate(this).Count == 0 &&
        Lanes.Any(static lane => lane.PackageValid);
}

public interface IStrategyGenerationLaneAgentV1
{
    StrategyGenerationLaneV1 Lane { get; }

    Task<StrategyGenerationLaneResultV1> GenerateAsync(
        IStrategyCodegenClient provider,
        ParallelStrategyGenerationRequestV1 request,
        string expectedCandidateId,
        CancellationToken ct = default,
        IProgress<StrategyGenerationLaneProgressV1>? progress = null);
}

public interface IParallelStrategyCandidateGeneratorV1
{
    Task<ParallelStrategyGenerationResultV1> GenerateAsync(
        IStrategyCodegenClient provider,
        ParallelStrategyGenerationRequestV1 request,
        CancellationToken ct = default,
        IProgress<StrategyGenerationLaneProgressV1>? progress = null);
}

public static class StrategyGenerationCandidateCanonicalJsonV1
{
    public static string Serialize(StrategyGenerationCandidateV1 candidate) =>
        ExecutableStrategyDefinitionCanonicalJson.Serialize(candidate);

    public static StrategyGenerationCandidateV1 Deserialize(string json) =>
        ExecutableStrategyDefinitionCanonicalJson.Deserialize<StrategyGenerationCandidateV1>(json);

    public static string Hash(StrategyGenerationCandidateV1 candidate) =>
        ExecutableStrategyDefinitionCanonicalJson.Hash(candidate);

    internal static bool TryHash(
        StrategyGenerationCandidateV1 candidate,
        out string hashSha256,
        out string error)
    {
        try
        {
            hashSha256 = Hash(candidate);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or
            FormatException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            hashSha256 = string.Empty;
            error = exception.Message;
            return false;
        }
    }

    public static string SerializeBatch(ParallelStrategyGenerationResultV1 result) =>
        ExecutableStrategyDefinitionCanonicalJson.Serialize(result);

    public static ParallelStrategyGenerationResultV1 DeserializeBatch(string json) =>
        ExecutableStrategyDefinitionCanonicalJson.Deserialize<ParallelStrategyGenerationResultV1>(json);

    public static string PromptHash(string strategyId, string userPrompt) =>
        ExecutableStrategyDefinitionCanonicalJson.Hash(BuildPromptIdentity(strategyId, userPrompt));

    public static string PromptHash(
        string strategyId,
        string userPrompt,
        string confirmedIntentCanonicalJson,
        string confirmedIntentHashSha256) =>
        ExecutableStrategyDefinitionCanonicalJson.Hash(BuildPromptIdentity(
            strategyId,
            userPrompt,
            confirmedIntentCanonicalJson,
            confirmedIntentHashSha256));

    public static string RequestHash(
        string strategyId,
        string userPrompt,
        StrategyGenerationLaneV1 lane) =>
        ExecutableStrategyDefinitionCanonicalJson.Hash(new StrategyLaneRequestIdentityV1(
            StrategyGenerationCandidateV1.CurrentSchemaVersion,
            strategyId.Trim(),
            userPrompt,
            lane,
            $"{strategyId.Trim()}/{StrategyGenerationLaneCatalogV1.WireName(lane)}",
            ParallelStrategyGenerationPromptV1.AgentId(lane),
            ParallelStrategyGenerationPromptV1.SystemContext(lane)));

    public static string RequestHash(
        string strategyId,
        string userPrompt,
        string confirmedIntentCanonicalJson,
        string confirmedIntentHashSha256,
        StrategyGenerationLaneV1 lane) =>
        ExecutableStrategyDefinitionCanonicalJson.Hash(new StrategyLaneRequestIdentityV1(
            StrategyGenerationCandidateV1.CurrentSchemaVersion,
            strategyId.Trim(),
            userPrompt,
            lane,
            $"{strategyId.Trim()}/{StrategyGenerationLaneCatalogV1.WireName(lane)}",
            ParallelStrategyGenerationPromptV1.AgentId(lane),
            ParallelStrategyGenerationPromptV1.SystemContext(lane),
            confirmedIntentCanonicalJson,
            confirmedIntentHashSha256));

    private static StrategyPromptIdentityV1 BuildPromptIdentity(string strategyId, string userPrompt)
    {
        var trimmedId = strategyId.Trim();
        var request = new ParallelStrategyGenerationRequestV1(trimmedId, userPrompt);
        var lanes = StrategyGenerationLaneCatalogV1.Ordered.Select(lane =>
        {
            return new StrategyLanePromptIdentityV1(
                lane,
                ParallelStrategyGenerationPromptV1.AgentId(lane),
                ParallelStrategyGenerationPromptV1.SystemContext(lane),
                ParallelStrategyGenerationPromptV1.UserMessage(request));
        }).ToArray();
        return new StrategyPromptIdentityV1(trimmedId, userPrompt, lanes);
    }

    private static StrategyPromptIdentityV1 BuildPromptIdentity(
        string strategyId,
        string userPrompt,
        string confirmedIntentCanonicalJson,
        string confirmedIntentHashSha256)
    {
        var trimmedId = strategyId.Trim();
        var request = new ParallelStrategyGenerationRequestV1(
            trimmedId,
            userPrompt,
            confirmedIntentCanonicalJson,
            confirmedIntentHashSha256);
        var lanes = StrategyGenerationLaneCatalogV1.Ordered.Select(lane =>
        {
            return new StrategyLanePromptIdentityV1(
                lane,
                ParallelStrategyGenerationPromptV1.AgentId(lane),
                ParallelStrategyGenerationPromptV1.SystemContext(lane),
                ParallelStrategyGenerationPromptV1.UserMessage(request));
        }).ToArray();
        return new StrategyPromptIdentityV1(trimmedId, userPrompt, lanes);
    }

    private sealed record StrategyPromptIdentityV1(
        string StrategyId,
        string UserPrompt,
        IReadOnlyList<StrategyLanePromptIdentityV1> Lanes);

    private sealed record StrategyLanePromptIdentityV1(
        StrategyGenerationLaneV1 Lane,
        string AgentId,
        string SystemContext,
        string UserMessage);

    private sealed record StrategyLaneRequestIdentityV1(
        string SchemaVersion,
        string StrategyId,
        string UserPrompt,
        StrategyGenerationLaneV1 Lane,
        string CandidateId,
        string AgentId,
        string SystemContext,
        string? ConfirmedIntentCanonicalJson = null,
        string? ConfirmedIntentHashSha256 = null);
}

/// <summary>
/// Validates the immutable semantic payload before it can enter a provider prompt or a persisted
/// four-lane batch. Full strategy-intent confirmation remains owned by Core and the authoring host;
/// this boundary proves that every lane received the exact same canonical confirmed-intent bytes.
/// </summary>
internal static class StrategyGenerationConfirmedIntentBindingV1
{
    public const int MaxCanonicalJsonCharacters = 750_000;

    public static bool TryValidate(
        string? canonicalJson,
        string? expectedHashSha256,
        out ConfirmedStrategyIntentV1? intent,
        out string error)
    {
        intent = null;
        if (string.IsNullOrWhiteSpace(canonicalJson))
        {
            error = "The canonical confirmed-strategy intent is required.";
            return false;
        }
        if (canonicalJson.Length > MaxCanonicalJsonCharacters)
        {
            error = $"The canonical confirmed-strategy intent exceeds {MaxCanonicalJsonCharacters:N0} characters.";
            return false;
        }
        if (!IsSha256(expectedHashSha256))
        {
            error = "The confirmed-strategy intent hash must be a lowercase SHA-256 value.";
            return false;
        }

        try
        {
            var parsed = StrategyIntentCanonicalJsonV1.DeserializeConfirmed(canonicalJson);
            if (!string.Equals(parsed.SchemaVersion, ConfirmedStrategyIntentV1.CurrentSchemaVersion,
                    StringComparison.Ordinal))
            {
                error = "The confirmed-strategy intent schema version is not supported.";
                return false;
            }
            var exactCanonicalJson = StrategyIntentCanonicalJsonV1.Serialize(parsed);
            if (!string.Equals(canonicalJson, exactCanonicalJson, StringComparison.Ordinal))
            {
                error = "The confirmed-strategy intent payload is not canonical JSON.";
                return false;
            }
            var actualHash = StrategyIntentCanonicalJsonV1.Hash(parsed);
            if (!string.Equals(expectedHashSha256, actualHash, StringComparison.Ordinal))
            {
                error = "The confirmed-strategy intent hash does not match its canonical payload.";
                return false;
            }

            intent = parsed;
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or
            FormatException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            error = $"The confirmed-strategy intent cannot be decoded: {exception.Message}";
            return false;
        }
    }

    public static void ValidateOrThrow(
        ParallelStrategyGenerationRequestV1 request,
        IStrategyIntentExtensionRegistryV1? extensionRegistry = null)
    {
        if (!TryValidate(
                request.ConfirmedIntentCanonicalJson,
                request.ConfirmedIntentHashSha256,
                out var intent,
                out var error))
            throw new ArgumentException(error, nameof(request));

        if (!TryValidateConfirmationContext(
                request.ConfirmedIntentContext,
                intent!,
                extensionRegistry,
                out error))
            throw new ArgumentException(error, nameof(request));
    }

    private static bool TryValidateConfirmationContext(
        StrategyGenerationConfirmedIntentContextV1? context,
        ConfirmedStrategyIntentV1 intent,
        IStrategyIntentExtensionRegistryV1? extensionRegistry,
        out string error)
    {
        if (context is null)
        {
            error = "The exact candidate, research case, and classification confirmation context is required.";
            return false;
        }

        if (!HasBoundedCanonicalPayload(context.CandidateCanonicalJson) ||
            !HasBoundedCanonicalPayload(context.ResearchCaseCanonicalJson) ||
            !HasBoundedCanonicalPayload(context.ClassificationCanonicalJson) ||
            context.ResearchExperimentCanonicalJson is not null &&
            !HasBoundedCanonicalPayload(context.ResearchExperimentCanonicalJson))
        {
            error = "Every confirmed-intent context payload must be present and within the canonical size limit.";
            return false;
        }

        try
        {
            var candidate = StrategyCandidateCanonicalJsonV1.Deserialize(context.CandidateCanonicalJson);
            var researchCase = ResearchCaseCanonicalJsonV1.Deserialize(context.ResearchCaseCanonicalJson);
            var classification = StrategySpecCanonicalJsonV1.Deserialize(context.ClassificationCanonicalJson);
            var researchExperiment = context.ResearchExperimentCanonicalJson is null
                ? null
                : ResearchExperimentCanonicalJsonV1.Deserialize(context.ResearchExperimentCanonicalJson);
            if (!string.Equals(
                    context.CandidateCanonicalJson,
                    StrategyCandidateCanonicalJsonV1.Serialize(candidate),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    context.ResearchCaseCanonicalJson,
                    ResearchCaseCanonicalJsonV1.Serialize(researchCase),
                    StringComparison.Ordinal) ||
                !string.Equals(
                    context.ClassificationCanonicalJson,
                    StrategySpecCanonicalJsonV1.Serialize(classification),
                    StringComparison.Ordinal) ||
                researchExperiment is not null && !string.Equals(
                    context.ResearchExperimentCanonicalJson,
                    ResearchExperimentCanonicalJsonV1.Serialize(researchExperiment),
                    StringComparison.Ordinal))
            {
                error = "The confirmed-intent validation context is not canonical JSON.";
                return false;
            }

            var reconstructedDraft = new StrategyIntentDraftV1(
                StrategyIntentDraftV1.CurrentSchemaVersion,
                intent.IntentId,
                intent.CandidateId,
                intent.CandidateRevision,
                intent.CandidateContentHashSha256,
                intent.ResearchCaseHashSha256,
                intent.Classification,
                intent.IntentModel,
                intent.RequirementCatalogVersion,
                intent.Requirements);
            var issues = StrategyIntentConfirmationV1.ValidateConfirmed(
                intent,
                candidate,
                researchCase,
                classification,
                reconstructedDraft,
                extensionRegistry);
            if (issues.Count > 0)
            {
                var first = issues[0];
                error = $"The confirmed strategy failed host confirmation: {first.Code} at {first.Path}: {first.Message}";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or
            FormatException or InvalidOperationException or NotSupportedException or OverflowException)
        {
            error = $"The confirmed-intent validation context cannot be decoded: {exception.Message}";
            return false;
        }
    }

    private static bool HasBoundedCanonicalPayload(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= MaxCanonicalJsonCharacters;

    private static bool IsSha256(string? value) =>
        value is { Length: 64 } && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public sealed record StrategyGenerationSelectionResultV1(
    StrategyGenerationCandidateV1? Candidate,
    string? CandidateHashSha256,
    IReadOnlyList<StrategyCandidateGenerationIssueV1> Issues)
{
    public bool Success => Candidate is not null && CandidateHashSha256 is not null &&
        Issues is not null && Issues.All(static issue => issue is not null &&
            issue.Severity != StrategyCandidateGenerationIssueSeverityV1.Error);
}

public sealed record StrategyGenerationRevalidationResultV1(
    ParallelStrategyGenerationResultV1? Batch,
    StrategyGenerationLaneResultV1? LaneResult,
    IReadOnlyList<StrategyCandidateGenerationIssueV1> Issues)
{
    public bool Applied => Batch is not null && LaneResult is not null && Issues is not null &&
        Issues.All(static issue => issue is not null &&
            issue.Severity != StrategyCandidateGenerationIssueSeverityV1.Error);
}

/// <summary>Pure batch verification and selection used by both persistence restore and the UI.</summary>
public static class StrategyGenerationBatchValidationV1
{
    public static IReadOnlyList<StrategyCandidateGenerationIssueV1> Validate(
        ParallelStrategyGenerationResultV1? batch)
    {
        var issues = new List<StrategyCandidateGenerationIssueV1>();
        if (batch is null)
        {
            issues.Add(Error("BATCH_REQUIRED", "$", "A parallel strategy-generation batch is required."));
            return issues;
        }
        var normalizedStrategyId = batch.StrategyId?.Trim() ?? string.Empty;
        var userPrompt = batch.UserPrompt ?? string.Empty;
        var intentBindingValid = StrategyGenerationConfirmedIntentBindingV1.TryValidate(
            batch.ConfirmedIntentCanonicalJson,
            batch.ConfirmedIntentHashSha256,
            out _,
            out var intentBindingError);
        if (!intentBindingValid)
            issues.Add(Error(
                "BATCH_CONFIRMED_INTENT_BINDING_INVALID",
                "confirmedIntentHashSha256",
                intentBindingError));
        if (string.IsNullOrWhiteSpace(normalizedStrategyId) || string.IsNullOrWhiteSpace(userPrompt))
            issues.Add(Error("BATCH_IDENTITY_INVALID", "$", "The batch strategy id and user prompt are required."));
        else if (intentBindingValid && !string.Equals(
                     batch.PromptHashSha256,
                     StrategyGenerationCandidateCanonicalJsonV1.PromptHash(
                         normalizedStrategyId,
                         userPrompt,
                         batch.ConfirmedIntentCanonicalJson!,
                         batch.ConfirmedIntentHashSha256!),
                     StringComparison.Ordinal))
            issues.Add(Error("BATCH_PROMPT_HASH_INVALID", "promptHashSha256", "The persisted prompt hash is stale."));

        if (batch.Lanes is null || batch.Lanes.Count != StrategyGenerationLaneCatalogV1.Ordered.Count)
        {
            issues.Add(Error("BATCH_LANE_COUNT_INVALID", "lanes", "The batch must contain exactly four lane results."));
            return issues;
        }

        for (var index = 0; index < StrategyGenerationLaneCatalogV1.Ordered.Count; index++)
        {
            var expectedLane = StrategyGenerationLaneCatalogV1.Ordered[index];
            var result = batch.Lanes[index];
            var path = $"lanes[{index}]";
            if (result is null)
            {
                issues.Add(Error("BATCH_LANE_NULL", path, "Lane results cannot be null."));
                continue;
            }
            if (result.Lane != expectedLane)
                issues.Add(Error("BATCH_LANE_ORDER_INVALID", $"{path}.lane",
                    "Lane results must remain in Vibe, Spec, Graph, CSP order."));
            if (result.AgentRun is null)
                issues.Add(Error("BATCH_AGENT_RUN_REQUIRED", $"{path}.agentRun",
                    "Every lane result requires its agent-run record."));
            else if (result.AgentRun.Usage is null)
                issues.Add(Error("BATCH_AGENT_USAGE_REQUIRED", $"{path}.agentRun.usage",
                    "Every lane agent run requires usage metadata."));
            if (result.Issues is null)
                issues.Add(Error("BATCH_LANE_ISSUES_REQUIRED", $"{path}.issues",
                    "Every lane result requires an issues array."));
            else if (result.Issues.Any(static issue => issue is null))
                issues.Add(Error("BATCH_LANE_ISSUE_NULL", $"{path}.issues",
                    "Lane issues cannot contain null entries."));
            else if (result.Issues.Any(static issue => !Enum.IsDefined(issue.Severity)))
                issues.Add(Error("BATCH_LANE_ISSUE_SEVERITY_INVALID", $"{path}.issues",
                    "Lane issues must use a known severity."));
            if (result.Readiness == StrategyGenerationReadinessV1.Unsupported)
            {
                issues.Add(Error("BATCH_UNSUPPORTED_LANE_STATE_INVALID", $"{path}.readiness",
                    "All four known lanes have generation-authoring contracts and must contain a model-run result."));
                continue;
            }

            if (result.Readiness == StrategyGenerationReadinessV1.Failed)
            {
                if (result.Candidate is not null || result.CandidateHashSha256 is not null)
                    issues.Add(Error("BATCH_FAILED_LANE_ARTIFACT_UNEXPECTED", path,
                        "A lane that failed before generation cannot expose an artifact or hash."));
                if (result.AgentRun is { Success: true })
                    issues.Add(Error("BATCH_FAILED_LANE_RUN_SUCCESS", $"{path}.agentRun.success",
                        "A lane that failed before generation cannot claim a successful agent run."));
                if (result.Issues is not null && !result.Issues.Any(static issue =>
                        issue is not null && issue.Severity == StrategyCandidateGenerationIssueSeverityV1.Error))
                    issues.Add(Error("BATCH_FAILED_LANE_ERROR_REQUIRED", $"{path}.issues",
                        "A failed lane requires an explicit blocking error."));
                continue;
            }

            if (result.Readiness == StrategyGenerationReadinessV1.Invalid &&
                result.Candidate is null && result.CandidateHashSha256 is null)
            {
                if (result.AgentRun is { Success: false })
                    issues.Add(Error("BATCH_INVALID_LANE_RUN_FAILED", $"{path}.agentRun.success",
                        "Malformed provider output is invalid generation, not a provider failure."));
                if (result.Issues is not null && !result.Issues.Any(static issue =>
                        issue is not null && issue.Severity == StrategyCandidateGenerationIssueSeverityV1.Error))
                    issues.Add(Error("BATCH_INVALID_LANE_ERROR_REQUIRED", $"{path}.issues",
                        "An invalid generated lane requires an explicit deterministic validation error."));
                continue;
            }

            if (result.Candidate is null || result.CandidateHashSha256 is null)
            {
                issues.Add(Error("BATCH_GENERATED_ARTIFACT_REQUIRED", path,
                    "A parsed generated artifact must preserve its candidate and exact hash together."));
                continue;
            }
            if (!intentBindingValid)
                continue;

            var expectedId = $"{normalizedStrategyId}/{StrategyGenerationLaneCatalogV1.WireName(expectedLane)}";
            var expectedRequestHash = StrategyGenerationCandidateCanonicalJsonV1.RequestHash(
                normalizedStrategyId,
                userPrompt,
                batch.ConfirmedIntentCanonicalJson!,
                batch.ConfirmedIntentHashSha256!,
                expectedLane);
            IReadOnlyList<StrategyCandidateGenerationIssueV1> candidateIssues;
            try
            {
                candidateIssues = StrategyGenerationCandidateValidatorV1.Validate(
                    result.Candidate,
                    expectedLane,
                    expectedId,
                    expectedRequestHash,
                    batch.ConfirmedIntentHashSha256);
            }
            catch (Exception exception) when (IsDeterministicValidationException(exception))
            {
                issues.Add(Error("BATCH_CANDIDATE_VALIDATION_FAILED", $"{path}.candidate",
                    $"The persisted candidate cannot be deterministically validated: {exception.Message}"));
                continue;
            }
            if (result.Issues is not null && !result.Issues.SequenceEqual(candidateIssues))
            {
                var first = candidateIssues.FirstOrDefault();
                issues.Add(Error(
                    "BATCH_LANE_VALIDATION_ISSUES_STALE",
                    $"{path}.issues",
                    first is null
                        ? "Persisted lane issues do not match current deterministic validation."
                        : $"Persisted lane issues do not match recomputed validation: {first.Code} at {first.Path}."));
            }
            var candidateValid = candidateIssues.All(static issue =>
                issue.Severity != StrategyCandidateGenerationIssueSeverityV1.Error);
            var expectedReadiness = candidateValid
                ? StrategyGenerationPackageCatalogV1.PackageValidationAvailable(expectedLane)
                    ? StrategyGenerationReadinessV1.PackageValid
                    : StrategyGenerationReadinessV1.Generated
                : StrategyGenerationReadinessV1.Invalid;
            if (result.Readiness != expectedReadiness)
                issues.Add(Error("BATCH_READINESS_STALE", $"{path}.readiness",
                    $"The persisted readiness must be '{expectedReadiness}' for this artifact. " +
                    "This generation batch contains no package-test evidence."));

            if (result.AgentRun is { Success: false })
                issues.Add(Error("BATCH_GENERATED_RUN_FAILED", $"{path}.agentRun.success",
                    "A preserved generated artifact requires a successful generation run."));
            if (candidateValid && result.Issues is not null && result.Issues.Any(static issue =>
                    issue is not null && issue.Severity == StrategyCandidateGenerationIssueSeverityV1.Error))
                issues.Add(Error("BATCH_VALID_LANE_ERROR_UNEXPECTED", $"{path}.issues",
                    "A valid generated lane cannot retain blocking deterministic-validation errors."));
            if (!candidateValid && result.Issues is not null && !result.Issues.Any(static issue =>
                    issue is not null && issue.Severity == StrategyCandidateGenerationIssueSeverityV1.Error))
                issues.Add(Error("BATCH_INVALID_LANE_ERROR_REQUIRED", $"{path}.issues",
                    "An invalid generated lane must preserve its deterministic validation error."));
            if (!StrategyGenerationCandidateCanonicalJsonV1.TryHash(
                    result.Candidate,
                    out var actualHash,
                    out var hashError))
                issues.Add(Error("BATCH_CANDIDATE_CANONICAL_JSON_INVALID", $"{path}.candidate",
                    $"The persisted candidate cannot be canonically hashed: {hashError}"));
            else if (!string.Equals(result.CandidateHashSha256, actualHash, StringComparison.Ordinal))
                issues.Add(Error("BATCH_CANDIDATE_HASH_INVALID", $"{path}.candidateHashSha256",
                    "The persisted candidate hash is stale."));
        }
        return issues;
    }

    public static StrategyGenerationSelectionResultV1 Select(
        ParallelStrategyGenerationResultV1? batch,
        string? candidateHashSha256)
    {
        var issues = Validate(batch).ToList();
        if (string.IsNullOrWhiteSpace(candidateHashSha256))
            issues.Add(Error("BATCH_SELECTION_HASH_REQUIRED", "candidateHashSha256",
                "Choose a candidate hash from the current batch."));
        if (issues.Count > 0) return new StrategyGenerationSelectionResultV1(null, null, issues);

        var matches = batch!.Lanes.Where(lane => lane.Selectable &&
            string.Equals(lane.CandidateHashSha256, candidateHashSha256, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        {
            issues.Add(Error("BATCH_SELECTION_NOT_FOUND", "candidateHashSha256",
                "The chosen hash does not identify exactly one valid selectable candidate in this batch."));
            return new StrategyGenerationSelectionResultV1(null, null, issues);
        }

        return new StrategyGenerationSelectionResultV1(matches[0].Candidate, matches[0].CandidateHashSha256, []);
    }

    /// <summary>
    /// Rebinds one locally edited artifact to its unchanged proposal/request/package envelope and
    /// reruns the same lane structural validation and any available package validator. This never
    /// compiles, tests, imports, or executes the artifact.
    /// </summary>
    public static StrategyGenerationRevalidationResultV1 RevalidateArtifact(
        ParallelStrategyGenerationResultV1? batch,
        string? priorCandidateHashSha256,
        StrategyGenerationArtifactV1? artifact)
    {
        var issues = Validate(batch).ToList();
        if (string.IsNullOrWhiteSpace(priorCandidateHashSha256))
            issues.Add(Error("BATCH_REVALIDATION_HASH_REQUIRED", "candidateHashSha256",
                "Revalidation requires the candidate hash that was loaded into the editor."));
        if (artifact is null)
            issues.Add(Error("BATCH_REVALIDATION_ARTIFACT_REQUIRED", "artifact",
                "Revalidation requires the edited artifact."));
        if (issues.Count > 0)
            return new StrategyGenerationRevalidationResultV1(null, null, issues);

        var matches = batch!.Lanes.Where(lane => lane.Candidate is not null &&
            string.Equals(lane.CandidateHashSha256, priorCandidateHashSha256, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        {
            issues.Add(Error("BATCH_REVALIDATION_CANDIDATE_NOT_FOUND", "candidateHashSha256",
                "The prior hash does not identify exactly one generated candidate in this batch."));
            return new StrategyGenerationRevalidationResultV1(null, null, issues);
        }

        var prior = matches[0];
        var candidate = prior.Candidate! with { Artifact = artifact! };
        var expectedId = $"{batch.StrategyId.Trim()}/{StrategyGenerationLaneCatalogV1.WireName(prior.Lane)}";
        var expectedRequestHash = StrategyGenerationCandidateCanonicalJsonV1.RequestHash(
            batch.StrategyId,
            batch.UserPrompt,
            batch.ConfirmedIntentCanonicalJson!,
            batch.ConfirmedIntentHashSha256!,
            prior.Lane);
        IReadOnlyList<StrategyCandidateGenerationIssueV1> candidateIssues;
        try
        {
            candidateIssues = StrategyGenerationCandidateValidatorV1.Validate(
                candidate,
                prior.Lane,
                expectedId,
                expectedRequestHash,
                batch.ConfirmedIntentHashSha256);
        }
        catch (Exception exception) when (IsDeterministicValidationException(exception))
        {
            issues.Add(Error("BATCH_REVALIDATION_ARTIFACT_INVALID", "artifact",
                $"The edited artifact cannot be deterministically validated: {exception.Message}"));
            return new StrategyGenerationRevalidationResultV1(null, null, issues);
        }
        var candidateValid = candidateIssues.All(static issue =>
            issue.Severity != StrategyCandidateGenerationIssueSeverityV1.Error);
        if (!StrategyGenerationCandidateCanonicalJsonV1.TryHash(candidate, out var hash, out var hashError))
        {
            issues.Add(Error("BATCH_REVALIDATION_CANONICAL_JSON_INVALID", "artifact",
                $"The edited artifact cannot be canonically hashed: {hashError}"));
            return new StrategyGenerationRevalidationResultV1(null, null, issues);
        }
        var laneResult = new StrategyGenerationLaneResultV1(
            prior.Lane,
            candidateValid
                ? StrategyGenerationPackageCatalogV1.PackageValidationAvailable(prior.Lane)
                    ? StrategyGenerationReadinessV1.PackageValid
                    : StrategyGenerationReadinessV1.Generated
                : StrategyGenerationReadinessV1.Invalid,
            candidate,
            hash,
            candidateIssues,
            new StrategyGenerationAgentRunV1(
                "strategy.local_revalidation@1",
                "local",
                null,
                true,
                null,
                null,
                CodegenUsage.None));
        var lanes = batch.Lanes.Select(lane => lane.Lane == prior.Lane ? laneResult : lane).ToArray();
        var updated = batch with { Lanes = lanes };
        var updatedIssues = Validate(updated);
        if (updatedIssues.Count > 0)
            return new StrategyGenerationRevalidationResultV1(null, null, updatedIssues);

        return new StrategyGenerationRevalidationResultV1(updated, laneResult, []);
    }

    private static StrategyCandidateGenerationIssueV1 Error(string code, string path, string message) =>
        new(StrategyCandidateGenerationIssueSeverityV1.Error, code, path, message);

    private static bool IsDeterministicValidationException(Exception exception) =>
        exception is JsonException or ArgumentException or FormatException or InvalidOperationException or
            NotSupportedException or OverflowException;
}

public static class StrategyGenerationLaneCatalogV1
{
    public static IReadOnlyList<StrategyGenerationLaneV1> Ordered { get; } = Array.AsReadOnly(
    new[]
    {
        StrategyGenerationLaneV1.VibePython,
        StrategyGenerationLaneV1.DeclarativeSpec,
        StrategyGenerationLaneV1.TypedGraph,
        StrategyGenerationLaneV1.CspPython,
    });

    public static string WireName(StrategyGenerationLaneV1 lane) => lane switch
    {
        StrategyGenerationLaneV1.VibePython => "vibe-python",
        StrategyGenerationLaneV1.DeclarativeSpec => "declarative-spec",
        StrategyGenerationLaneV1.TypedGraph => "typed-graph",
        StrategyGenerationLaneV1.CspPython => "csp-python",
        _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unknown strategy generation lane."),
    };

    public static string DisplayName(StrategyGenerationLaneV1 lane) => lane switch
    {
        StrategyGenerationLaneV1.VibePython => "Vibe · Python",
        StrategyGenerationLaneV1.DeclarativeSpec => "Spec · Rules",
        StrategyGenerationLaneV1.TypedGraph => "Graph · Typed",
        StrategyGenerationLaneV1.CspPython => "CSP · Events",
        _ => lane.ToString(),
    };

    public static StrategyGenerationArtifactKindV1 ArtifactKind(StrategyGenerationLaneV1 lane) => lane switch
    {
        StrategyGenerationLaneV1.VibePython => StrategyGenerationArtifactKindV1.VibePythonSource,
        StrategyGenerationLaneV1.DeclarativeSpec => StrategyGenerationArtifactKindV1.DeclarativeStrategyJson,
        StrategyGenerationLaneV1.TypedGraph => StrategyGenerationArtifactKindV1.TradeIrModuleJson,
        StrategyGenerationLaneV1.CspPython => StrategyGenerationArtifactKindV1.CspPythonSource,
        _ => throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unknown strategy generation lane."),
    };
}
