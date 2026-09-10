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

    /// <summary>
    /// Actionable blocker for Charts shell ⑤ / Validate. Lock draft ≠ historical-ready.
    /// </summary>
    public string DescribeHistoricalValidationBlocker()
    {
        if (IsGenerating)
            return "Wait for generation to finish before historical validation.";
        if (IsRegistered &&
            AuthoredUnitSpecification is not null &&
            StrategyWorkspace.Bindings.BuildArtifactHashSha256 is not null &&
            StrategyWorkspace.Bindings.AuthoredUnitSpecificationHashSha256 is not null)
        {
            var registration = _strategyKernelRegistry?.Find(AuthoredUnitSpecification.UnitId);
            if (registration is null)
                return "The compiled unit is not in the strategy registry. Confirm Register again, then retry Historical BT.";
            return string.Empty;
        }

        if (!CanCompileCurrentSource && AuthoredUnitSpecification is null)
            return "Historical BT needs a compiled authored unit. In Builder: generate/lower to C# → Compile → Register, then retry Historical BT. (TradeIR smoke ≠ historical.)";
        if (CanCompileCurrentSource && !IsRegistered)
            return "Compile and Confirm Register in Builder (Build/Review), then retry Historical BT from Charts.";
        if (IsRegistered && StrategyWorkspace.Bindings.BuildArtifactHashSha256 is null)
            return "Registration is incomplete (missing build artifact hash). Re-compile and Register, then retry.";

        return "Compile, review, and register this exact authored strategy before historical validation.";
    }

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
            reason = DescribeHistoricalValidationBlocker();
            if (string.IsNullOrWhiteSpace(reason))
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

    /// <summary>
    /// Opens the Builder screen that unblocks historical validation (Build or Validate).
    /// </summary>
    public void FocusHistoricalValidationPrep()
    {
        if (OpenValidateScreenCommand.CanExecute(null))
            OpenValidateScreenCommand.Execute(null);
        else if (OpenBuildScreenCommand.CanExecute(null))
            OpenBuildScreenCommand.Execute(null);
        else if (OpenBriefScreenCommand.CanExecute(null))
            OpenBriefScreenCommand.Execute(null);
    }

    /// <summary>
    /// Charts shell ⑤ assist: open Build, auto-Compile when source is ready, leave Register
    /// for the user. Never calls <see cref="ConfirmRegisterCommand"/>.
    /// </summary>
    /// <returns>
    /// <c>true</c> when review overlay is open and waiting for Register;
    /// <c>false</c> when compile could not run or registration is still incomplete.
    /// </returns>
    public bool PrepareHistoricalValidationAssist()
    {
        if (CanRunHistoricalValidation &&
            TryCreateHistoricalValidationContext(out _, out _))
            return false;

        // Prefer Build when Compile/Register is still required; Validate is only useful after register.
        if (!IsRegistered || StrategyWorkspace.Bindings.BuildArtifactHashSha256 is null)
        {
            if (OpenBuildScreenCommand.CanExecute(null))
                OpenBuildScreenCommand.Execute(null);
            else
                FocusHistoricalValidationPrep();
        }
        else
        {
            FocusHistoricalValidationPrep();
        }

        if (CanCompileCurrentSource &&
            CompileCommand.CanExecute(null) &&
            !ReviewOpen)
        {
            CompileCommand.Execute(null);
            if (ReviewOpen)
            {
                Status =
                    "Compiled for Historical BT — review the code, then press Register. Retry Charts ⑤ after Register.";
                return true;
            }
        }

        if (ReviewOpen)
        {
            Status =
                "Review the compiled code, then press Register. Retry Charts ⑤ after Register.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(Status))
            Status = DescribeHistoricalValidationBlocker();
        return false;
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
