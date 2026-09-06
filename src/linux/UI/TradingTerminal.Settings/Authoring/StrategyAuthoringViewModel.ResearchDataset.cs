using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.App.Authoring;

public sealed partial class StrategyAuthoringViewModel
{
    [ObservableProperty]
    private ResearchDatasetDefinitionV1? _researchDatasetDefinition;

    [ObservableProperty]
    private ResearchChartSelectionV1? _pendingResearchChartSelection;

    [ObservableProperty]
    private ResearchExperimentEvidenceV1? _researchExperimentEvidence;

    [ObservableProperty]
    private bool _isResearchExperimentRunning;

    public bool HasResearchDataset => ResearchDatasetDefinition is not null;
    public bool HasResearchChartSelection => PendingResearchChartSelection is not null;
    public int ResearchEventSampleCount => ResearchDatasetDefinition?.Samples.Count ?? 0;
    public bool HasResearchExperimentEvidence => ResearchExperimentEvidence is not null;
    public bool CanRunResearchExperiment =>
        _researchExperimentRunner is not null && ResearchEventSampleCount >= 4 && !IsResearchExperimentRunning;
    public bool ResearchEvidenceReadyForGeneration => ResearchDatasetDefinition is null ||
        ResearchExperimentEvidence is not null && string.Equals(
            ResearchExperimentEvidence.DatasetHashSha256,
            ResearchDatasetCanonicalJsonV1.Hash(ResearchDatasetDefinition),
            StringComparison.Ordinal);
    public IReadOnlyList<ResearchEventSampleV1> ResearchEventSamples =>
        ResearchDatasetDefinition?.Samples ?? [];
    public string ResearchDatasetStatusText => ResearchDatasetDefinition is null
        ? "No event samples yet. Brush an observation window on the host chart, then extend the shaded future outcome window."
        : $"{ResearchEventSampleCount} labeled event sample(s) · {ResearchDatasetDefinition.RequiredData} · future outcome excluded from features";
    public string ResearchChartSelectionText => PendingResearchChartSelection is null
        ? "No chart window selected"
        : $"{PendingResearchChartSelection.CanonicalSymbol} · {PendingResearchChartSelection.Timeframe.ToDisplayString()} · " +
          $"observe {PendingResearchChartSelection.ObservationFromUtc:u} → {PendingResearchChartSelection.ObservationToUtc:u} · " +
          $"outcome {PendingResearchChartSelection.OutcomeFromUtc:u} → {PendingResearchChartSelection.OutcomeToUtc:u}";
    public string ResearchExperimentStatusText => ResearchExperimentEvidence is null
        ? ResearchEventSampleCount < 4
            ? $"Add {4 - ResearchEventSampleCount} more labeled sample(s) for chronological train/validation/test evidence."
            : _researchExperimentRunner is null
                ? "The local research runner is unavailable."
                : "Ready to extract observation-only bars, quotes, trades, and depth."
        : $"{ResearchExperimentEvidence.Features.Count} features · " +
          $"split {ResearchExperimentEvidence.Split.TrainingCount}/{ResearchExperimentEvidence.Split.ValidationCount}/{ResearchExperimentEvidence.Split.TestCount} · " +
          $"holdout accuracy {ResearchExperimentEvidence.TestAccuracy:P0} · exploratory only";
    public string ResearchFormulaText => ResearchExperimentEvidence?.Formula ?? "No research formula yet.";

    /// <summary>
    /// Called only by the trusted host chart overlay. Authored visualizer/strategy code never receives
    /// this mutation seam or an Avalonia pointer event.
    /// </summary>
    public void SetResearchChartSelection(ResearchChartSelectionV1 selection)
    {
        ResearchDatasetValidatorV1.RequireValidSelection(selection);
        PendingResearchChartSelection = selection;
        ActiveScreen = StrategyAuthoringScreen.Research;
        Status = "Observation and future outcome windows selected. Label the event B, C, or N.";
    }

    [RelayCommand(CanExecute = nameof(CanLabelResearchSelection))]
    private void MarkPreBreakout() => CommitResearchSelection(ResearchEventLabelKindV1.PreBreakout);

    [RelayCommand(CanExecute = nameof(CanLabelResearchSelection))]
    private void MarkPreCrash() => CommitResearchSelection(ResearchEventLabelKindV1.PreCrash);

    [RelayCommand(CanExecute = nameof(CanLabelResearchSelection))]
    private void MarkNeutral() => CommitResearchSelection(ResearchEventLabelKindV1.Neutral);

    private bool CanLabelResearchSelection() => PendingResearchChartSelection is not null && !IsGenerating;

    [RelayCommand]
    private void ClearResearchChartSelection()
    {
        PendingResearchChartSelection = null;
        Status = "Chart research selection cleared. No dataset sample was changed.";
    }

    [RelayCommand]
    private void RemoveResearchEventSample(ResearchEventSampleV1? sample)
    {
        if (sample is null || ResearchDatasetDefinition is null ||
            !ResearchDatasetDefinition.Samples.Contains(sample)) return;

        var remaining = ResearchDatasetDefinition.Samples.Where(item => item != sample).ToArray();
        if (remaining.Length == 0)
        {
            ResearchDatasetDefinition = null;
            ApplyResearchDatasetWorkspaceChange(null, "Removed final research event sample");
            Status = "The final event sample was removed; the research dataset is empty.";
            Save();
            return;
        }

        var requiredData = remaining.Aggregate(
            StrategyDataRequirement.None,
            static (value, item) => value | item.Selection.RequiredData);
        var updated = ResearchDatasetDefinition with
        {
            WorkspaceRevisionHashSha256 = StrategyWorkspaceCanonicalJsonV1.Hash(StrategyWorkspace),
            RequiredData = requiredData,
            Samples = remaining,
        };
        ResearchDatasetValidatorV1.RequireStructurallyValid(updated);
        ResearchDatasetDefinition = updated;
        ApplyResearchDatasetWorkspaceChange(updated, "Removed research event sample");
        Status = $"Removed one event sample; {remaining.Length} remain.";
        Save();
    }

    [RelayCommand(CanExecute = nameof(CanRunResearchExperimentAction))]
    private async Task RunResearchExperimentAsync()
    {
        if (!CanRunResearchExperiment || ResearchDatasetDefinition is not { } dataset ||
            _researchExperimentRunner is null) return;

        var datasetHash = ResearchDatasetCanonicalJsonV1.Hash(dataset);
        IsResearchExperimentRunning = true;
        Status = "Extracting observation-only market features and fitting training-only transforms…";
        try
        {
            var evidence = await _researchExperimentRunner.RunAsync(dataset);
            if (ResearchDatasetDefinition is null || !string.Equals(
                    datasetHash,
                    ResearchDatasetCanonicalJsonV1.Hash(ResearchDatasetDefinition),
                    StringComparison.Ordinal))
            {
                Status = "The research dataset changed while the experiment was running; the stale result was discarded.";
                return;
            }

            ResearchExperimentValidatorV1.RequireValid(evidence);
            if (!string.Equals(evidence.DatasetHashSha256, datasetHash, StringComparison.Ordinal))
                throw new InvalidOperationException("The research result did not bind the exact labeled dataset.");
            ResearchExperimentEvidence = evidence;
            ApplyResearchDatasetWorkspaceChange(dataset, "Produced chronological research feature evidence");
            Status = $"Research evidence ready: {evidence.Formula}. Historical validation is still required before Paper.";
            Save();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(exception, "Research experiment failed for {Id}", StrategyId);
            Status = $"Research experiment failed: {exception.Message}";
        }
        finally
        {
            IsResearchExperimentRunning = false;
        }
    }

    private bool CanRunResearchExperimentAction() => CanRunResearchExperiment;

    private void CommitResearchSelection(ResearchEventLabelKindV1 label)
    {
        if (PendingResearchChartSelection is not { } selection || IsGenerating) return;

        var identityPayload = new ResearchSampleIdentity(selection, label);
        var sampleId = "event-" + StrategyWorkspaceCanonicalJsonV1.HashArtifact(identityPayload)[..20];
        var samples = ResearchDatasetDefinition?.Samples.ToList() ?? [];
        if (samples.Any(sample => string.Equals(sample.EventSampleId, sampleId, StringComparison.Ordinal)))
        {
            Status = "That exact chart window and label already exist in the dataset.";
            return;
        }

        samples.Add(new ResearchEventSampleV1(
            ResearchEventSampleV1.CurrentSchemaVersion,
            sampleId,
            selection,
            label,
            null,
            ResearchEventLabelSourceV1.Manual));
        var requiredData = samples.Aggregate(
            StrategyDataRequirement.None,
            static (value, item) => value | item.Selection.RequiredData);
        var updated = new ResearchDatasetDefinitionV1(
            ResearchDatasetDefinitionV1.CurrentSchemaVersion,
            $"{StrategyId.Trim()}.research",
            StrategyWorkspaceCanonicalJsonV1.Hash(StrategyWorkspace),
            requiredData,
            ResearchDatasetDefinition?.LeakagePolicy ?? ResearchLeakagePolicyV1.SafeDefault,
            samples);
        ResearchDatasetValidatorV1.RequireStructurallyValid(updated);

        ResearchDatasetDefinition = updated;
        PendingResearchChartSelection = null;
        ApplyResearchDatasetWorkspaceChange(updated, $"Added {label} research event sample");
        Status = $"Added {label} sample. Future outcome data is excluded from feature computation.";
        Save();
    }

    private void ApplyResearchDatasetWorkspaceChange(
        ResearchDatasetDefinitionV1? dataset,
        string reason)
    {
        var requested = BuildCurrentWorkspaceBindings(StrategyWorkspace.Bindings) with
        {
            DatasetDefinitionHashSha256 = dataset is null
                ? null
                : ResearchDatasetCanonicalJsonV1.Hash(dataset),
        };
        StrategyWorkspace = StrategyWorkspaceRevisionPolicyV1.Revise(
            StrategyWorkspace,
            StrategyWorkspaceChangeKindV1.DatasetOrFeatureDefinition,
            requested,
            StrategyWorkspaceStageV1.Research,
            revisionReason: reason);
    }

    private void RestoreResearchDataset(AuthoringSessionSnapshot session, ref string? restoreWarning)
    {
        ResearchDatasetDefinition = null;
        PendingResearchChartSelection = null;
        if (string.IsNullOrWhiteSpace(session.ResearchDatasetJson)) return;

        try
        {
            ResearchDatasetDefinition = ResearchDatasetCanonicalJsonV1.Deserialize(session.ResearchDatasetJson);
        }
        catch (Exception exception) when (exception is ArgumentException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(exception, "Could not restore research dataset for {Id}", session.StrategyId);
            restoreWarning = "The session was restored, but its research dataset failed structural or leakage validation and was detached.";
        }
    }

    private void RestoreResearchExperiment(AuthoringSessionSnapshot session, ref string? restoreWarning)
    {
        ResearchExperimentEvidence = null;
        if (ResearchDatasetDefinition is null || string.IsNullOrWhiteSpace(session.ResearchExperimentJson)) return;

        try
        {
            var evidence = ResearchExperimentCanonicalJsonV1.Deserialize(session.ResearchExperimentJson);
            var datasetHash = ResearchDatasetCanonicalJsonV1.Hash(ResearchDatasetDefinition);
            if (!string.Equals(evidence.DatasetHashSha256, datasetHash, StringComparison.Ordinal))
                throw new InvalidOperationException("The saved research evidence belongs to another dataset revision.");
            ResearchExperimentEvidence = evidence;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {
            _logger.LogWarning(exception, "Could not restore research evidence for {Id}", session.StrategyId);
            restoreWarning = "The session was restored, but stale or invalid research experiment evidence was detached.";
        }
    }

    private void InvalidateResearchDatasetForBriefChange()
    {
        if (_restoring || ResearchDatasetDefinition is null) return;
        ResearchDatasetDefinition = null;
        PendingResearchChartSelection = null;
        ResearchExperimentEvidence = null;
    }

    partial void OnResearchDatasetDefinitionChanged(ResearchDatasetDefinitionV1? value)
    {
        if (!_restoring && ResearchExperimentEvidence is not null &&
            (value is null || !string.Equals(
                ResearchExperimentEvidence.DatasetHashSha256,
                ResearchDatasetCanonicalJsonV1.Hash(value),
                StringComparison.Ordinal)))
            ResearchExperimentEvidence = null;
        OnPropertyChanged(nameof(HasResearchDataset));
        OnPropertyChanged(nameof(ResearchEventSampleCount));
        OnPropertyChanged(nameof(ResearchEventSamples));
        OnPropertyChanged(nameof(ResearchDatasetStatusText));
        OnPropertyChanged(nameof(CanRunResearchExperiment));
        OnPropertyChanged(nameof(ResearchExperimentStatusText));
        OnPropertyChanged(nameof(ResearchEvidenceReadyForGeneration));
        OnPropertyChanged(nameof(CanGenerateFourCandidates));
        OnPropertyChanged(nameof(CanGenerateCanonicalPaperStrategy));
        RunResearchExperimentCommand.NotifyCanExecuteChanged();
        GenerateFourCandidatesCommand.NotifyCanExecuteChanged();
        GenerateCanonicalPaperStrategyCommand.NotifyCanExecuteChanged();
    }

    partial void OnPendingResearchChartSelectionChanged(ResearchChartSelectionV1? value)
    {
        OnPropertyChanged(nameof(HasResearchChartSelection));
        OnPropertyChanged(nameof(ResearchChartSelectionText));
        MarkPreBreakoutCommand.NotifyCanExecuteChanged();
        MarkPreCrashCommand.NotifyCanExecuteChanged();
        MarkNeutralCommand.NotifyCanExecuteChanged();
    }

    partial void OnResearchExperimentEvidenceChanged(ResearchExperimentEvidenceV1? value)
    {
        OnPropertyChanged(nameof(HasResearchExperimentEvidence));
        OnPropertyChanged(nameof(ResearchExperimentStatusText));
        OnPropertyChanged(nameof(ResearchFormulaText));
        OnPropertyChanged(nameof(ResearchEvidenceReadyForGeneration));
        OnPropertyChanged(nameof(CanGenerateFourCandidates));
        OnPropertyChanged(nameof(CanGenerateCanonicalPaperStrategy));
        GenerateFourCandidatesCommand.NotifyCanExecuteChanged();
        GenerateCanonicalPaperStrategyCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsResearchExperimentRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanRunResearchExperiment));
        OnPropertyChanged(nameof(ResearchExperimentStatusText));
        RunResearchExperimentCommand.NotifyCanExecuteChanged();
    }

    private sealed record ResearchSampleIdentity(
        ResearchChartSelectionV1 Selection,
        ResearchEventLabelKindV1 Label);
}
