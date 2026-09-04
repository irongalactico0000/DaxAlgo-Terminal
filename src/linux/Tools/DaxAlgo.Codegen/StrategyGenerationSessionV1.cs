using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.Infrastructure.Strategies.Authoring;

/// <summary>
/// One user-facing strategy-generation conversation. It persists accepted candidate revisions, not
/// agent sessions. The first message establishes immutable RawIntent; later messages revise the
/// current candidate. Confirmation is a separate hash-bound action and never calls a model.
/// </summary>
public sealed class StrategyGenerationSessionV1
{
    private readonly IStrategyCandidateGeneratorV1 _generator;
    private readonly IStrategyCodegenClient _provider;
    private readonly List<StrategyCandidateV1> _revisions = [];
    private string? _rawIntent;

    public StrategyGenerationSessionV1(
        IStrategyCandidateGeneratorV1 generator,
        IStrategyCodegenClient provider,
        string workspaceId,
        string workspaceName,
        string candidateId,
        IReadOnlyList<StrategyCandidateV1>? revisions = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workspaceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(candidateId);

        _generator = generator;
        _provider = provider;
        WorkspaceId = workspaceId;
        WorkspaceName = workspaceName;
        CandidateId = candidateId;

        foreach (var revision in revisions ?? [])
        {
            if (!string.Equals(revision.CandidateId, candidateId, StringComparison.Ordinal))
                throw new ArgumentException("Seed revisions must belong to the session candidate id.", nameof(revisions));
            if (!StrategyCandidateValidatorV1.Assess(revision).IsStructurallyValid)
                throw new ArgumentException("Seed revisions must be structurally valid.", nameof(revisions));
            AddRevision(revision);
        }

        CurrentCandidate = _revisions.OrderBy(static revision => revision.Revision).LastOrDefault();
        _rawIntent = CurrentCandidate?.RawIntent;
    }

    public string WorkspaceId { get; }
    public string WorkspaceName { get; }
    public string CandidateId { get; }
    public StrategyCandidateV1? CurrentCandidate { get; private set; }
    public IReadOnlyList<StrategyCandidateV1> Revisions => _revisions;

    public StrategyGenerationWorkspaceV1 Workspace => new(
        StrategyGenerationWorkspaceV1.CurrentSchemaVersion,
        WorkspaceId,
        WorkspaceName,
        _revisions.ToArray(),
        CurrentCandidate?.CandidateId,
        CurrentCandidate?.Revision);

    public async Task<StrategyCandidateGenerationResultV1> SendAsync(
        string userMessage,
        CancellationToken ct = default,
        IReadOnlyList<AuthoredChartReferenceV1>? chartReferences = null,
        IReadOnlyList<AuthoredChartReferenceInspectionV1>? chartReferenceInspections = null,
        IReadOnlyList<AuthoredChartReferenceResolutionV1>? chartReferenceResolutions = null,
        IReadOnlyList<ChartPatternSelectionV1>? chartPatternSelections = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userMessage);
        var request = CurrentCandidate is null
            ? new StrategyCandidateGenerationRequestV1(
                CandidateId,
                userMessage,
                ChartReferences: chartReferences,
                ChartReferenceInspections: chartReferenceInspections,
                ChartReferenceResolutions: chartReferenceResolutions,
                ChartPatternSelections: chartPatternSelections)
            : new StrategyCandidateGenerationRequestV1(
                CandidateId,
                _rawIntent!,
                CurrentCandidate,
                userMessage,
                chartReferences,
                chartReferenceInspections,
                chartReferenceResolutions,
                chartPatternSelections);

        var result = await _generator.GenerateAsync(_provider, request, ct).ConfigureAwait(false);
        if (!result.Success) return result;

        _rawIntent ??= result.Candidate!.RawIntent;
        foreach (var revision in result.ProducedRevisions) AddRevision(revision);
        CurrentCandidate = result.Candidate;
        return result;
    }

    public StrategyCandidateConfirmationResultV1 Confirm(string expectedContentHashSha256)
    {
        var result = StrategyCandidateConfirmationV1.Confirm(CurrentCandidate, expectedContentHashSha256);
        if (!result.Success) return result;

        AddRevision(result.Candidate!);
        CurrentCandidate = result.Candidate;
        return result;
    }

    private void AddRevision(StrategyCandidateV1 candidate)
    {
        var existing = _revisions.FirstOrDefault(revision =>
            string.Equals(revision.CandidateId, candidate.CandidateId, StringComparison.Ordinal) &&
            revision.Revision == candidate.Revision);
        if (existing is null)
        {
            _revisions.Add(candidate);
            return;
        }

        if (!string.Equals(
                StrategyCandidateCanonicalJsonV1.Hash(existing),
                StrategyCandidateCanonicalJsonV1.Hash(candidate),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Candidate '{candidate.CandidateId}' revision {candidate.Revision} has two different contents.");
        }
    }
}
