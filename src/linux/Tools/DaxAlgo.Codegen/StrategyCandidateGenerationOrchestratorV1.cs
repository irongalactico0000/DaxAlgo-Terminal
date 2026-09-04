using System.Text.Json;
using Microsoft.Extensions.Logging;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Definition;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.Infrastructure.Strategies.Authoring;

public enum StrategyCandidateGenerationIssueSeverityV1
{
    Warning,
    Error,
}

public sealed record StrategyCandidateGenerationIssueV1(
    StrategyCandidateGenerationIssueSeverityV1 Severity,
    string Code,
    string Path,
    string Message);

public sealed record StrategyGenerationAgentRunV1(
    string AgentId,
    string ProviderId,
    string? RequestId,
    bool Success,
    string? Error,
    string? RawResponse,
    CodegenUsage Usage);

/// <summary>
/// One intake or revision request. CurrentCandidate is null for the original idea; otherwise
/// UserMessage explains what the user answered or changed while RawIntent remains the original text.
/// </summary>
public sealed record StrategyCandidateGenerationRequestV1(
    string CandidateId,
    string RawIntent,
    StrategyCandidateV1? CurrentCandidate = null,
    string? UserMessage = null,
    IReadOnlyList<AuthoredChartReferenceV1>? ChartReferences = null,
    IReadOnlyList<AuthoredChartReferenceInspectionV1>? ChartReferenceInspections = null,
    IReadOnlyList<AuthoredChartReferenceResolutionV1>? ChartReferenceResolutions = null,
    IReadOnlyList<ChartPatternSelectionV1>? ChartPatternSelections = null);

public sealed record StrategyCandidateGenerationResultV1(
    StrategyCandidateV1? Candidate,
    StrategyCandidateAssessmentV1? Assessment,
    IReadOnlyList<StrategyCandidateV1> ProducedRevisions,
    IReadOnlyList<StrategyCandidateGenerationIssueV1> Issues,
    IReadOnlyList<StrategyGenerationAgentRunV1> AgentRuns,
    CodegenUsage Usage)
{
    public bool Success => Candidate is not null && Assessment is not null &&
        Issues.All(static issue => issue.Severity != StrategyCandidateGenerationIssueSeverityV1.Error);
}

public interface IStrategyCandidateGeneratorV1
{
    Task<StrategyCandidateGenerationResultV1> GenerateAsync(
        IStrategyCodegenClient provider,
        StrategyCandidateGenerationRequestV1 request,
        CancellationToken ct = default);
}

/// <summary>
/// Optional routing seam for specialist roles. The default keeps the user's selected provider; a
/// deployment may route a namespaced specialist id to a different model or service without changing
/// Candidate, Group, Statement, or amendment schemas.
/// </summary>
public interface IStrategyGenerationAgentRouterV1
{
    IStrategyCodegenClient ResolveSpecialist(
        StrategySpecialistRequestV1 request,
        IStrategyCodegenClient selectedProvider);
}

public sealed class SameProviderStrategyGenerationAgentRouterV1 : IStrategyGenerationAgentRouterV1
{
    public IStrategyCodegenClient ResolveSpecialist(
        StrategySpecialistRequestV1 request,
        IStrategyCodegenClient selectedProvider) => selectedProvider;
}

/// <summary>
/// Model-facing strategy-generation coordinator. The intake agent proposes a candidate and bounded
/// specialist assignments; specialists can amend only their assigned group; deterministic validators
/// and the composer decide what is accepted. No call in this class compiles source or touches runtime
/// execution.
/// </summary>
public sealed class StrategyCandidateGenerationOrchestratorV1(
    ILogger<StrategyCandidateGenerationOrchestratorV1>? logger = null,
    IStrategyGenerationAgentRouterV1? router = null) : IStrategyCandidateGeneratorV1
{
    public const int MaxSpecialists = 4;
    public const int MaxUserInputCharacters = 100_000;
    public const int MaxModelResponseCharacters = 1_000_000;

    private readonly ILogger? _logger = logger;
    private readonly IStrategyGenerationAgentRouterV1 _router =
        router ?? new SameProviderStrategyGenerationAgentRouterV1();

    public async Task<StrategyCandidateGenerationResultV1> GenerateAsync(
        IStrategyCodegenClient provider,
        StrategyCandidateGenerationRequestV1 request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(request);

        var issues = new List<StrategyCandidateGenerationIssueV1>();
        var runs = new List<StrategyGenerationAgentRunV1>();
        if (!ValidateRequest(request, issues)) return Failed(issues, runs);

        var expectedRevision = checked((request.CurrentCandidate?.Revision ?? 0) + 1);
        var expectedParent = request.CurrentCandidate is null
            ? null
            : StrategyCandidateCanonicalJsonV1.Hash(request.CurrentCandidate);
        var intakeRequest = new StrategyCodegenRequest(
            StrategyCandidateGenerationPromptV1.IntakeSystemContext,
            [new CodegenMessage(
                CodegenRole.User,
                StrategyCandidateGenerationPromptV1.CreateIntakeUserMessage(
                    request, expectedRevision, expectedParent))])
        {
            OutputContract = StrategyCodegenOutputContract.RawJsonObject,
        };

        StrategyCodegenResponse intakeResponse;
        try
        {
            intakeResponse = await provider.GenerateAsync(intakeRequest, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            issues.Add(Error("GENERATION_PROVIDER_EXCEPTION", "intake", exception.Message));
            runs.Add(new StrategyGenerationAgentRunV1(
                StrategyCandidateGenerationPromptV1.IntakeAgentId,
                provider.ProviderId,
                null,
                false,
                exception.Message,
                null,
                CodegenUsage.None));
            return Failed(issues, runs);
        }
        var intakeUsage = intakeResponse.Usage ?? CodegenUsage.None;
        var intakeRaw = intakeResponse.RawText ?? intakeResponse.Code;
        runs.Add(new StrategyGenerationAgentRunV1(
            StrategyCandidateGenerationPromptV1.IntakeAgentId,
            provider.ProviderId,
            null,
            intakeResponse.Success,
            intakeResponse.Error,
            intakeRaw,
            intakeUsage));

        if (!intakeResponse.Success)
        {
            issues.Add(Error("GENERATION_PROVIDER_FAILED", "intake",
                intakeResponse.Error ?? "The intake provider returned no result."));
            return Failed(issues, runs, intakeUsage);
        }

        if (!StrategyModelJsonV1.TryDeserialize<StrategyCandidateDraftV1>(
                intakeRaw, MaxModelResponseCharacters, out var draft, out var parseError))
        {
            issues.Add(Error("GENERATION_INTAKE_JSON_INVALID", "intake.response", parseError));
            return Failed(issues, runs, intakeUsage);
        }

        ValidateDraft(draft!, request, expectedRevision, expectedParent, issues);
        if (issues.Any(IsError))
        {
            var rejectedAssessment = StrategyCandidateValidatorV1.Assess(draft!.Candidate);
            return rejectedAssessment.IsStructurallyValid
                ? new StrategyCandidateGenerationResultV1(
                    draft.Candidate,
                    rejectedAssessment,
                    [draft.Candidate],
                    issues,
                    runs,
                    intakeUsage)
                : Failed(issues, runs, intakeUsage);
        }

        var orderedRequests = draft!.SpecialistRequests
            .OrderBy(static item => item.RequestId, StringComparer.Ordinal)
            .ToArray();
        var specialistTasks = orderedRequests.Select(specialist =>
            ResolveAndInvokeSpecialistAsync(provider, draft.Candidate, specialist, ct)).ToArray();
        var specialistResults = specialistTasks.Length == 0
            ? []
            : await Task.WhenAll(specialistTasks).ConfigureAwait(false);

        var amendments = new List<StrategyCandidateAmendmentV1>();
        var totalUsage = intakeUsage;
        foreach (var result in specialistResults.OrderBy(static result => result.Request.RequestId, StringComparer.Ordinal))
        {
            runs.Add(result.Run);
            totalUsage = totalUsage.Add(result.Run.Usage);
            if (result.Amendment is not null)
            {
                amendments.Add(result.Amendment);
                continue;
            }

            issues.Add(new StrategyCandidateGenerationIssueV1(
                result.Request.Required
                    ? StrategyCandidateGenerationIssueSeverityV1.Error
                    : StrategyCandidateGenerationIssueSeverityV1.Warning,
                result.Code,
                $"specialists[{result.Request.RequestId}]",
                result.Error));
        }

        StrategyCandidateV1 candidate = draft.Candidate;
        StrategyCandidateAssessmentV1 assessment = StrategyCandidateValidatorV1.Assess(candidate);
        if (!issues.Any(IsError) && amendments.Count > 0)
        {
            var composed = StrategyCandidateComposerV1.Compose(draft, amendments);
            if (!composed.Success)
            {
                foreach (var issue in composed.Issues)
                    issues.Add(Error(issue.Code, issue.Path, issue.Message));
            }
            else
            {
                candidate = composed.Candidate!;
                assessment = composed.Assessment!;
            }
        }

        _logger?.LogInformation(
            "Strategy candidate {CandidateId} revision {Revision}: {Specialists} specialist(s), {Errors} error(s)",
            candidate.CandidateId,
            candidate.Revision,
            orderedRequests.Length,
            issues.Count(IsError));

        IReadOnlyList<StrategyCandidateV1> produced = ReferenceEquals(candidate, draft.Candidate)
            ? [draft.Candidate]
            : [draft.Candidate, candidate];
        return new StrategyCandidateGenerationResultV1(candidate, assessment, produced, issues, runs, totalUsage);
    }

    private async Task<SpecialistInvocationResult> ResolveAndInvokeSpecialistAsync(
        IStrategyCodegenClient selectedProvider,
        StrategyCandidateV1 candidate,
        StrategySpecialistRequestV1 specialist,
        CancellationToken ct)
    {
        try
        {
            var specialistProvider = _router.ResolveSpecialist(specialist, selectedProvider);
            if (specialistProvider is null)
            {
                const string message = "The specialist router returned no provider.";
                return new SpecialistInvocationResult(
                    specialist,
                    null,
                    new StrategyGenerationAgentRunV1(
                        specialist.SpecialistId, "unresolved", specialist.RequestId,
                        false, message, null, CodegenUsage.None),
                    "GENERATION_SPECIALIST_ROUTE_FAILED",
                    message);
            }

            return await InvokeSpecialistAsync(specialistProvider, candidate, specialist, ct)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new SpecialistInvocationResult(
                specialist,
                null,
                new StrategyGenerationAgentRunV1(
                    specialist.SpecialistId, "routing", specialist.RequestId,
                    false, exception.Message, null, CodegenUsage.None),
                "GENERATION_SPECIALIST_ROUTE_FAILED",
                exception.Message);
        }
    }

    private static bool ValidateRequest(
        StrategyCandidateGenerationRequestV1 request,
        ICollection<StrategyCandidateGenerationIssueV1> issues)
    {
        if (string.IsNullOrWhiteSpace(request.CandidateId))
            issues.Add(Error("GENERATION_CANDIDATE_ID_REQUIRED", "candidateId", "A candidate id is required."));
        if (string.IsNullOrWhiteSpace(request.RawIntent))
            issues.Add(Error("GENERATION_RAW_INTENT_REQUIRED", "rawIntent", "The original user intent is required."));
        else if (request.RawIntent.Length > MaxUserInputCharacters)
            issues.Add(Error("GENERATION_RAW_INTENT_TOO_LARGE", "rawIntent",
                $"The original user intent exceeds {MaxUserInputCharacters:N0} characters."));

        if (request.CurrentCandidate is null)
        {
            if (!string.IsNullOrWhiteSpace(request.UserMessage))
                issues.Add(Error("GENERATION_USER_MESSAGE_UNEXPECTED", "userMessage",
                    "The initial request uses RawIntent; UserMessage is only for a candidate revision."));
        }
        else
        {
            var current = request.CurrentCandidate;
            var assessment = StrategyCandidateValidatorV1.Assess(current);
            if (!assessment.IsStructurallyValid)
                issues.Add(Error("GENERATION_CURRENT_CANDIDATE_INVALID", "currentCandidate",
                    "The current candidate must be structurally valid before it can be revised."));
            if (!string.Equals(current.CandidateId, request.CandidateId, StringComparison.Ordinal))
                issues.Add(Error("GENERATION_CANDIDATE_ID_MISMATCH", "candidateId",
                    "The request candidate id must match the current candidate."));
            if (!string.Equals(current.RawIntent, request.RawIntent, StringComparison.Ordinal))
                issues.Add(Error("GENERATION_RAW_INTENT_MISMATCH", "rawIntent",
                    "RawIntent is immutable across candidate revisions."));
            if (string.IsNullOrWhiteSpace(request.UserMessage))
                issues.Add(Error("GENERATION_USER_MESSAGE_REQUIRED", "userMessage",
                    "A revision requires the user's clarification or requested change."));
            else if (request.UserMessage.Length > MaxUserInputCharacters)
                issues.Add(Error("GENERATION_USER_MESSAGE_TOO_LARGE", "userMessage",
                    $"The clarification exceeds {MaxUserInputCharacters:N0} characters."));
            if (current.Status is StrategyCandidateStatusV1.Rejected or StrategyCandidateStatusV1.Superseded)
                issues.Add(Error("GENERATION_CURRENT_STATUS_INVALID", "currentCandidate.status",
                    $"Candidate status '{current.Status}' cannot be revised."));
            if (current.Revision == int.MaxValue)
                issues.Add(Error("GENERATION_REVISION_EXHAUSTED", "currentCandidate.revision",
                    "The candidate revision cannot be incremented."));
        }

        ValidateChartReferences(request, issues);

        return !issues.Any(IsError);
    }

    private static void ValidateChartReferences(
        StrategyCandidateGenerationRequestV1 request,
        ICollection<StrategyCandidateGenerationIssueV1> issues)
    {
        var references = request.ChartReferences ?? [];
        var inspections = request.ChartReferenceInspections ?? [];
        var resolutions = request.ChartReferenceResolutions ?? [];
        var selections = request.ChartPatternSelections ?? [];
        if (references.Count > 8)
            issues.Add(Error("GENERATION_CHART_REFERENCE_LIMIT", "chartReferences",
                "At most eight reference charts may be supplied to one request."));

        var duplicateId = references
            .Where(static item => item is not null && !string.IsNullOrWhiteSpace(item.ReferenceId))
            .GroupBy(static item => item.ReferenceId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateId is not null)
            issues.Add(Error("GENERATION_CHART_REFERENCE_DUPLICATE", "chartReferences",
                $"Reference id '{duplicateId.Key}' is duplicated."));

        for (var index = 0; index < references.Count; index++)
        {
            var reference = references[index];
            var path = $"chartReferences[{index}]";
            if (reference is null)
            {
                issues.Add(Error("GENERATION_CHART_REFERENCE_REQUIRED", path,
                    "Reference chart entries cannot be null."));
                continue;
            }
            if (string.IsNullOrWhiteSpace(reference.ReferenceId))
                issues.Add(Error("GENERATION_CHART_REFERENCE_ID_REQUIRED", $"{path}.referenceId",
                    "A reference id is required."));
            if (reference.ContentHashSha256 is not { Length: 64 } hash ||
                !hash.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f'))
                issues.Add(Error("GENERATION_CHART_REFERENCE_HASH_INVALID", $"{path}.contentHashSha256",
                    "The reference must carry a lowercase SHA-256 content hash."));
            if (reference.Similarity is null || reference.Similarity.Count == 0 ||
                reference.Similarity.Any(static value => !Enum.IsDefined(value)))
                issues.Add(Error("GENERATION_CHART_REFERENCE_SIMILARITY_REQUIRED", $"{path}.similarity",
                    "The user must state what should be similar about this chart."));
        }

        var referenceIds = references.Where(static item => item is not null)
            .Select(static item => item.ReferenceId).ToHashSet(StringComparer.Ordinal);
        foreach (var inspection in inspections)
        {
            var reference = references.FirstOrDefault(item => item is not null &&
                string.Equals(item.ReferenceId, inspection?.ReferenceId, StringComparison.Ordinal));
            if (inspection is null || reference is null ||
                !string.Equals(
                    inspection.ContentHashSha256,
                    reference.ContentHashSha256,
                    StringComparison.Ordinal))
            {
                issues.Add(Error("GENERATION_CHART_REFERENCE_INSPECTION_ORPHAN", "chartReferenceInspections",
                    "Every chart inspection must match one supplied reference id and SHA-256 hash."));
            }
        }
        foreach (var resolution in resolutions)
        {
            if (resolution is null || !referenceIds.Contains(resolution.ReferenceId))
                issues.Add(Error("GENERATION_CHART_REFERENCE_RESOLUTION_ORPHAN", "chartReferenceResolutions",
                    "Every chart analysis must identify one of the supplied reference artifacts."));
        }
        foreach (var selection in selections)
        {
            var reference = references.FirstOrDefault(item => item is not null &&
                string.Equals(item.ReferenceId, selection?.ReferenceId, StringComparison.Ordinal));
            if (selection is null || reference is null ||
                !string.Equals(selection.ReferenceContentHashSha256, reference.ContentHashSha256, StringComparison.Ordinal) ||
                selection.Match is null || selection.Match.InstrumentId.IsNone)
            {
                issues.Add(Error("GENERATION_CHART_PATTERN_SELECTION_ORPHAN", "chartPatternSelections",
                    "Every historical-pattern selection must carry a resolved instrument and match one supplied reference id and hash."));
            }
        }
    }

    private static void ValidateDraft(
        StrategyCandidateDraftV1 draft,
        StrategyCandidateGenerationRequestV1 request,
        int expectedRevision,
        string? expectedParent,
        ICollection<StrategyCandidateGenerationIssueV1> issues)
    {
        if (draft.Candidate is null)
        {
            issues.Add(Error("GENERATION_CANDIDATE_REQUIRED", "candidate", "The intake response requires a candidate."));
            return;
        }
        if (draft.SpecialistRequests is null)
        {
            issues.Add(Error("GENERATION_SPECIALIST_REQUESTS_REQUIRED", "specialistRequests",
                "The intake response requires a specialistRequests array, even when empty."));
            return;
        }

        var candidate = draft.Candidate;
        Exact(candidate.CandidateId, request.CandidateId, "candidate.candidateId", "GENERATION_IDENTITY_CHANGED", issues);
        Exact(candidate.RawIntent, request.RawIntent, "candidate.rawIntent", "GENERATION_RAW_INTENT_CHANGED", issues);
        if (candidate.Revision != expectedRevision)
            issues.Add(Error("GENERATION_REVISION_CHANGED", "candidate.revision",
                $"The model returned revision {candidate.Revision}; the host assigned revision {expectedRevision}."));
        if (!string.Equals(candidate.ParentContentHashSha256, expectedParent, StringComparison.Ordinal))
            issues.Add(Error("GENERATION_PARENT_CHANGED", "candidate.parentContentHashSha256",
                "The model changed the host-assigned parent candidate hash."));
        if (candidate.Status != StrategyCandidateStatusV1.AwaitingConfirmation)
            issues.Add(Error("GENERATION_STATUS_INVALID", "candidate.status",
                "A generated candidate must await user confirmation."));

        var assessment = StrategyCandidateValidatorV1.Assess(candidate);
        foreach (var issue in assessment.Issues.Where(static issue =>
                     issue.Scope == StrategyCandidateIssueScopeV1.Structure))
            issues.Add(Error("GENERATION_CANDIDATE_INVALID", $"candidate.{issue.Path}", issue.Message));

        if (!assessment.IsStructurallyValid) return;

        RejectModelStatementAuthority(
            EnumerateGroups(candidate.Groups).SelectMany(static group => group.Statements),
            "candidate.groups",
            issues);
        RejectModelSupported(candidate.BuildSupport, "candidate.buildSupport", issues);

        if (draft.SpecialistRequests.Count > MaxSpecialists)
            issues.Add(Error("GENERATION_SPECIALIST_LIMIT", "specialistRequests",
                $"At most {MaxSpecialists} specialists may be requested for one generation turn."));

        var requestIds = new HashSet<string>(StringComparer.Ordinal);
        var targets = new HashSet<string>(StringComparer.Ordinal);
        var groupIds = EnumerateGroups(candidate.Groups).Select(static group => group.GroupId)
            .ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < draft.SpecialistRequests.Count; index++)
        {
            var specialist = draft.SpecialistRequests[index];
            var path = $"specialistRequests[{index}]";
            if (specialist is null || string.IsNullOrWhiteSpace(specialist.RequestId) ||
                !IsNamespacedId(specialist.SpecialistId) || string.IsNullOrWhiteSpace(specialist.TargetGroupId) ||
                string.IsNullOrWhiteSpace(specialist.Goal))
            {
                issues.Add(Error("GENERATION_SPECIALIST_REQUEST_INVALID", path,
                    "A specialist request needs a unique id, namespaced versioned specialist id, target group, and goal."));
                continue;
            }
            if (!requestIds.Add(specialist.RequestId))
                issues.Add(Error("GENERATION_SPECIALIST_REQUEST_DUPLICATE", $"{path}.requestId",
                    $"Specialist request id '{specialist.RequestId}' is duplicated."));
            if (!targets.Add(specialist.TargetGroupId))
                issues.Add(Error("GENERATION_SPECIALIST_TARGET_CONFLICT", $"{path}.targetGroupId",
                    $"Only one specialist may own group '{specialist.TargetGroupId}' in a turn."));
            if (!groupIds.Contains(specialist.TargetGroupId))
                issues.Add(Error("GENERATION_SPECIALIST_TARGET_UNKNOWN", $"{path}.targetGroupId",
                    $"Specialist target group '{specialist.TargetGroupId}' does not exist in the candidate."));
        }
    }

    private static async Task<SpecialistInvocationResult> InvokeSpecialistAsync(
        IStrategyCodegenClient provider,
        StrategyCandidateV1 candidate,
        StrategySpecialistRequestV1 specialist,
        CancellationToken ct)
    {
        try
        {
            var response = await provider.GenerateAsync(new StrategyCodegenRequest(
                StrategyCandidateGenerationPromptV1.SpecialistSystemContext(),
                [new CodegenMessage(
                    CodegenRole.User,
                    StrategyCandidateGenerationPromptV1.CreateSpecialistUserMessage(specialist, candidate))])
                {
                    OutputContract = StrategyCodegenOutputContract.RawJsonObject,
                }, ct)
                .ConfigureAwait(false);
            var usage = response.Usage ?? CodegenUsage.None;
            var raw = response.RawText ?? response.Code;
            var run = new StrategyGenerationAgentRunV1(
                specialist.SpecialistId, provider.ProviderId, specialist.RequestId,
                response.Success, response.Error, raw, usage);
            if (!response.Success)
                return new SpecialistInvocationResult(specialist, null, run,
                    "GENERATION_SPECIALIST_PROVIDER_FAILED",
                    response.Error ?? "The specialist provider returned no result.");

            if (!StrategyModelJsonV1.TryDeserialize<StrategyCandidateAmendmentV1>(
                    raw, MaxModelResponseCharacters, out var amendment, out var parseError))
                return new SpecialistInvocationResult(specialist, null, run,
                    "GENERATION_SPECIALIST_JSON_INVALID", parseError);

            if (amendment!.BuildSupportUpserts is null)
                return new SpecialistInvocationResult(specialist, null, run,
                    "GENERATION_SPECIALIST_SUPPORT_REQUIRED",
                    "The specialist response requires a buildSupportUpserts array, even when empty.");

            var supportIssues = new List<StrategyCandidateGenerationIssueV1>();
            RejectModelStatementAuthority(
                EnumerateGroups([amendment.ReplacementGroup])
                    .SelectMany(static group => group.Statements ?? []),
                "replacementGroup",
                supportIssues);
            RejectModelSupported(amendment.BuildSupportUpserts, "buildSupportUpserts", supportIssues);
            if (supportIssues.Count > 0)
                return new SpecialistInvocationResult(specialist, null, run,
                    supportIssues[0].Code, supportIssues[0].Message);

            return new SpecialistInvocationResult(specialist, amendment, run, string.Empty, string.Empty);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var run = new StrategyGenerationAgentRunV1(
                specialist.SpecialistId, provider.ProviderId, specialist.RequestId,
                false, exception.Message, null, CodegenUsage.None);
            return new SpecialistInvocationResult(specialist, null, run,
                "GENERATION_SPECIALIST_EXCEPTION", exception.Message);
        }
    }

    private static void RejectModelSupported(
        IReadOnlyList<StrategyBuildSupportItemV1> support,
        string path,
        ICollection<StrategyCandidateGenerationIssueV1> issues)
    {
        for (var index = 0; index < support.Count; index++)
        {
            if (support[index] is { Status: StrategyBuildSupportStatusV1.Supported })
                issues.Add(Error("GENERATION_SUPPORT_AUTHORITY_VIOLATION", $"{path}[{index}].status",
                    "A model cannot mark build support as Supported; only the deterministic capability service may do that."));
        }
    }

    private static void RejectModelStatementAuthority(
        IEnumerable<StrategyCandidateStatementV1> statements,
        string path,
        ICollection<StrategyCandidateGenerationIssueV1> issues)
    {
        foreach (var statement in statements)
        {
            if (statement.Source == StrategyCandidateStatementSourceV1.DeterministicSystem)
            {
                issues.Add(Error("GENERATION_STATEMENT_AUTHORITY_VIOLATION", path,
                    $"A model cannot attribute statement '{statement.StatementId}' to the deterministic system."));
            }
        }
    }

    private static IEnumerable<StrategyCandidateGroupV1> EnumerateGroups(
        IReadOnlyList<StrategyCandidateGroupV1>? groups)
    {
        if (groups is null) yield break;
        foreach (var group in groups)
        {
            if (group is null) continue;
            yield return group;
            foreach (var child in EnumerateGroups(group.Children)) yield return child;
        }
    }

    private static void Exact(
        string actual,
        string expected,
        string path,
        string code,
        ICollection<StrategyCandidateGenerationIssueV1> issues)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            issues.Add(Error(code, path, "The model changed a host-owned value."));
    }

    private static bool IsNamespacedId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var at = value.LastIndexOf('@');
        return at > 1 && at < value.Length - 1 && value[..at].Contains('.', StringComparison.Ordinal) &&
               int.TryParse(value[(at + 1)..], out var version) && version > 0;
    }

    private static bool IsError(StrategyCandidateGenerationIssueV1 issue) =>
        issue.Severity == StrategyCandidateGenerationIssueSeverityV1.Error;

    private static StrategyCandidateGenerationIssueV1 Error(string code, string path, string message) =>
        new(StrategyCandidateGenerationIssueSeverityV1.Error, code, path, message);

    private static StrategyCandidateGenerationResultV1 Failed(
        IReadOnlyList<StrategyCandidateGenerationIssueV1> issues,
        IReadOnlyList<StrategyGenerationAgentRunV1> runs,
        CodegenUsage? usage = null) =>
        new(null, null, [], issues, runs, usage ?? CodegenUsage.None);

    private sealed record SpecialistInvocationResult(
        StrategySpecialistRequestV1 Request,
        StrategyCandidateAmendmentV1? Amendment,
        StrategyGenerationAgentRunV1 Run,
        string Code,
        string Error);
}

internal static class StrategyModelJsonV1
{
    public static bool TryDeserialize<T>(
        string? raw,
        int maxCharacters,
        out T? value,
        out string error)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw))
        {
            error = "The model returned no JSON.";
            return false;
        }
        if (raw.Length > maxCharacters)
        {
            error = $"The model response exceeds the {maxCharacters:N0}-character limit.";
            return false;
        }

        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = text.IndexOf('\n');
            if (firstLineEnd < 0)
            {
                error = "The JSON code fence is not closed.";
                return false;
            }
            var closing = text.IndexOf("```", firstLineEnd + 1, StringComparison.Ordinal);
            if (closing < 0 || !string.IsNullOrWhiteSpace(text[(closing + 3)..]))
            {
                error = "Return one JSON object only; text after the JSON fence is not allowed.";
                return false;
            }
            text = text[(firstLineEnd + 1)..closing].Trim();
        }

        if (!text.StartsWith('{') || !text.EndsWith('}'))
        {
            error = "Return exactly one JSON object with no prose or markdown.";
            return false;
        }

        try
        {
            value = ExecutableStrategyDefinitionCanonicalJson.Deserialize<T>(text);
            error = string.Empty;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or FormatException or InvalidOperationException)
        {
            error = $"The model JSON does not match the strategy-generation contract: {exception.Message}";
            return false;
        }
    }
}
