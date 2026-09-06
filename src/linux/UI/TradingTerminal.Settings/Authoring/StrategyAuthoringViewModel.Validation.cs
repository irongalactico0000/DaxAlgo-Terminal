using CommunityToolkit.Mvvm.ComponentModel;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.App.Authoring;

public sealed partial class StrategyAuthoringViewModel
{
    private IReadOnlyDictionary<string, object?>? _historicalValidationParameters;

    [ObservableProperty]
    private HistoricalValidationEvidenceV1? _historicalValidationEvidence;

    public bool HasHistoricalValidationEvidence => HistoricalValidationEvidence is { } evidence &&
        string.Equals(
            StrategyWorkspace.Bindings.ValidationEvidenceHashSha256,
            HistoricalValidationEvidenceCanonicalJsonV1.Hash(evidence),
            StringComparison.Ordinal);
    public bool CanRunHistoricalValidation =>
        IsRegistered &&
        AuthoredUnitSpecification is not null &&
        StrategyWorkspace.Bindings.BuildArtifactHashSha256 is not null &&
        !IsGenerating;
    public string HistoricalValidationStatusText => !HasHistoricalValidationEvidence
        ? "No exact historical replay is bound to this compiled revision."
        : $"Validated {HistoricalValidationEvidence!.FromUtc:u} → {HistoricalValidationEvidence.ToUtc:u} · " +
          $"{HistoricalValidationEvidence.TradeCount} trades · {HistoricalValidationEvidence.DataMode}.";

    public bool TryCreateHistoricalValidationContext(
        out HistoricalValidationContextV1? context,
        out string reason)
    {
        SynchronizeStrategyWorkspace();
        var specificationHash = StrategyWorkspace.Bindings.AuthoredUnitSpecificationHashSha256;
        var buildHash = StrategyWorkspace.Bindings.BuildArtifactHashSha256;
        if (!IsRegistered || AuthoredUnitSpecification is null || specificationHash is null || buildHash is null)
        {
            context = null;
            reason = "Compile, review, and register this exact authored strategy before historical validation.";
            return false;
        }

        var registration = _strategyKernelRegistry?.Find(AuthoredUnitSpecification.UnitId);
        if (registration is null || !string.Equals(
                AuthoredUnitSpecificationCanonicalJsonV1.Hash(registration.AuthoredSpecification),
                specificationHash,
                StringComparison.Ordinal))
        {
            context = null;
            reason = "The exact compiled strategy is no longer present in the canonical strategy registry.";
            return false;
        }

        context = new HistoricalValidationContextV1(
            StrategyWorkspace.WorkspaceId,
            specificationHash,
            buildHash,
            StrategyWorkspace.Bindings.FeatureSetHashSha256);
        reason = string.Empty;
        return true;
    }

    public bool AcceptHistoricalValidationEvidence(
        HistoricalValidationEvidenceV1 evidence,
        IReadOnlyDictionary<string, object?> testedParameters,
        out string reason)
    {
        try
        {
            HistoricalValidationEvidenceValidatorV1.RequireValid(evidence);
        }
        catch (ArgumentException exception)
        {
            reason = exception.Message;
            return false;
        }

        if (!TryCreateHistoricalValidationContext(out var current, out reason) || current != evidence.Context)
        {
            reason = string.IsNullOrWhiteSpace(reason)
                ? "The workspace changed while historical validation was running. The stale result was discarded."
                : reason;
            return false;
        }

        HistoricalValidationEvidence = evidence;
        _historicalValidationParameters = testedParameters.ToDictionary(
            item => item.Key,
            item => item.Value,
            StringComparer.Ordinal);
        var evidenceHash = HistoricalValidationEvidenceCanonicalJsonV1.Hash(evidence);
        StrategyWorkspace = StrategyWorkspaceRevisionPolicyV1.Revise(
            StrategyWorkspace,
            StrategyWorkspaceChangeKindV1.ValidationEvidence,
            StrategyWorkspace.Bindings with { ValidationEvidenceHashSha256 = evidenceHash },
            StrategyWorkspaceStageV1.Validate,
            revisionReason: "Exact historical validation completed");
        Status = "Historical validation is bound to this exact compiled revision. Review the selected Paper book before handoff.";
        Save();
        reason = string.Empty;
        return true;
    }

    public IReadOnlyDictionary<string, object?>? ValidatedPaperParameters =>
        HasHistoricalValidationEvidence ? _historicalValidationParameters : null;

    public bool BindValidatedPaperBook(string bookId, string accountId, out string reason)
    {
        if (HistoricalValidationEvidence is not { } evidence ||
            StrategyWorkspace.Bindings.ValidationEvidenceHashSha256 is not { } validationHash ||
            !string.Equals(validationHash, HistoricalValidationEvidenceCanonicalJsonV1.Hash(evidence), StringComparison.Ordinal))
        {
            reason = "Run exact historical validation for the current revision before selecting a Paper book.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(bookId) || string.IsNullOrWhiteSpace(accountId))
        {
            reason = "A selected Paper book and account are required.";
            return false;
        }

        var paperHash = StrategyWorkspaceCanonicalJsonV1.HashArtifact(new PaperBindingV1(
            validationHash,
            bookId.Trim(),
            accountId.Trim()));
        StrategyWorkspace = StrategyWorkspaceRevisionPolicyV1.Revise(
            StrategyWorkspace,
            StrategyWorkspaceChangeKindV1.PaperBinding,
            StrategyWorkspace.Bindings with { PaperBindingHashSha256 = paperHash },
            StrategyWorkspaceStageV1.Paper,
            revisionReason: "Validated strategy approved for selected Paper book");
        Status = "The validated revision is bound to the selected Paper book. Real-money routing remains unavailable.";
        Save();
        reason = string.Empty;
        return true;
    }

    partial void OnHistoricalValidationEvidenceChanged(HistoricalValidationEvidenceV1? value)
    {
        OnPropertyChanged(nameof(HasHistoricalValidationEvidence));
        OnPropertyChanged(nameof(HistoricalValidationStatusText));
        OnPropertyChanged(nameof(CanRunHistoricalValidation));
        NotifyAuthoringScreenStateChanged();
    }

    private sealed record PaperBindingV1(string ValidationEvidenceHashSha256, string BookId, string AccountId);
}
