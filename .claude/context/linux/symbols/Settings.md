# TradingTerminal.Settings — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/UI/TradingTerminal.Settings/Archive/ArchiveActivityViewModel.cs
```cs
   15: public sealed partial class ArchiveActivityViewModel : ViewModelBase
   20: public ArchiveActivityViewModel(
   31: public ObservableCollection<ArchiveRow> Rows { get; }
   34: public ObservableCollection<CoverageRow> Coverage { get; }
   43: public bool HasPending => PendingCount > 0;
   47: public async Task RefreshAsync()
   78: public async Task InstantOffloadAsync()
  126: public sealed class ArchiveRow
  128: public required ArchiveManifestEntry Entry { get; init; }
  130: public long Id => Entry.Id;
  131: public string PeriodLabel => Entry.PeriodLabel;
  132: public string Range => $"{Entry.FromUtc:yyyy-MM-dd} → {Entry.ToUtc:yyyy-MM-dd}";
  133: public int Parts => Entry.Parts.Count;
  134: public string TotalBytesPretty => Fmt(Entry.TotalBytes);
  135: public string Target => Entry.Target.IsSavedMessages ? "Saved Messages" : (Entry.Target.ChatRef ?? "(unknown)");
  136: public long RowsQuotes => Entry.RowsQuotes;
  137: public long RowsBars => Entry.RowsBars;
  138: public string Uploaded => Entry.UploadedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
  139: public string LocalDeleted => Entry.DeletedLocal ? "yes" : "no";
  141: public static ArchiveRow From(ArchiveManifestEntry e) => new() { Entry = e };
  152: public sealed class CoverageRow
  155: public CoverageRow(ArchiveCoverageWindow w) => _w = w;
  157: public string PeriodLabel => _w.PeriodLabel;
  158: public string Range => $"{_w.FromUtc:yyyy-MM-dd} → {_w.ToUtc:yyyy-MM-dd}";
  159: public bool Offloaded => _w.Offloaded;
  160: public string Status => _w.Offloaded ? "Offloaded" : "Pending";
  161: public string ArchiveRef => _w.ArchiveId is { } id ? $"#{id}" : "—";
```

## src/linux/UI/TradingTerminal.Settings/Archive/ArchiveSettingsViewModel.cs
```cs
   18: public sealed partial class ArchiveSettingsViewModel : ViewModelBase
   26: public ArchiveSettingsViewModel(
   68: public bool DefaultTargetIsChat => string.Equals(DefaultTargetKind, "chat", StringComparison.OrdinalIgnoreCase);
   76: public bool ManualTargetIsChat => string.Equals(ManualTargetKind, "chat", StringComparison.OrdinalIgnoreCase);
   83: public IReadOnlyList<string> PeriodOptions { get; } = new[] { "Weekly", "Monthly" };
   84: public IReadOnlyList<string> TargetKindOptions { get; } = new[] { "saved", "chat" };
```

## src/linux/UI/TradingTerminal.Settings/Archive/ArchiveUserFile.cs
```cs
   14: public static class ArchiveUserFile
   16: public static string Path { get; } = System.IO.Path.Combine(
   20: public static void Save(ArchiveOptions archive, TelegramArchiveOptions telegram)
```

## src/linux/UI/TradingTerminal.Settings/Archive/TelegramArchiveCredentialProtection.cs
```cs
   12: public static class TelegramArchiveCredentialProtection
   34: public static string? Encrypt(string? plaintext)
   89: public static string? Decrypt(string? cipherBase64)
```

## src/linux/UI/TradingTerminal.Settings/Authoring/AiCodegenUserFile.cs
```cs
   15: public static class AiCodegenUserFile
   19: public static string Path { get; } = System.IO.Path.Combine(
   31: public static void SaveSelection(
```

## src/linux/UI/TradingTerminal.Settings/Authoring/AiProvidersSettingsViewModel.cs
```cs
   16: public sealed partial class AiProvidersSettingsViewModel : ViewModelBase
   20: public AiProvidersSettingsViewModel(IAiStrategyBuilder? builder = null, IAiKeyStore? keys = null)
   33: public ObservableCollection<AiProviderRow> Providers { get; }
   69: public sealed partial class AiProviderRow : ObservableObject
   73: public AiProviderRow(IStrategyCodegenClient client, IAiKeyStore? keys)
   81: public string ProviderId => _client.ProviderId;
   82: public string DisplayName => _client.DisplayName;
   83: public bool IsAvailable => _client.IsAvailable;
   84: public bool NeedsKey { get; }
   91: public string StatusText => IsAvailable
   95: public void MarkStored(bool stored)
```

## src/linux/UI/TradingTerminal.Settings/Authoring/AuthoringChartReferenceStore.cs
```cs
   11: public sealed record AuthoringChartReferenceSnapshot(
   17: public interface IAuthoringChartReferenceRepository
   19:     Task<AuthoringChartReferenceSnapshot> ImportAsync(
   20:     string sourcePath,
   21:     ChartReferenceSimilarityV1 similarity,
   22:     CancellationToken cancellationToken = default);
   24:     bool Exists(AuthoringChartReferenceSnapshot snapshot);
   27: public sealed class FileAuthoringChartReferenceRepository : IAuthoringChartReferenceRepository
   44: public FileAuthoringChartReferenceRepository(string? directory = null) =>
   47: public async Task<AuthoringChartReferenceSnapshot> ImportAsync(
  113: public bool Exists(AuthoringChartReferenceSnapshot snapshot)
```

## src/linux/UI/TradingTerminal.Settings/Authoring/AuthoringSessionStore.cs
```cs
   12: public sealed record AuthoringChatEntry(
   22: public const string User = "user";
   23: public const string Assistant = "assistant";
   24: public const string System = "system";
   32: public sealed record AuthoringSessionSnapshot(
   76: public const int CurrentAuthoringUxVersion = 3;
   79: public bool FourLaneGenerationEnabled =>
   83: public string Age
   95: public string Label => $"{DisplayName} ({StrategyId}) · {Age}";
  101: public interface IAuthoringSessionRepository
  103:     IReadOnlyList<AuthoringSessionSnapshot> List();
  104:     bool Save(AuthoringSessionSnapshot session);
  105:     void Delete(string strategyId);
  110: public static FileAuthoringSessionRepository Instance { get; } = new();
  116: public IReadOnlyList<AuthoringSessionSnapshot> List() => AuthoringSessionStore.List();
  117: public bool Save(AuthoringSessionSnapshot session) => AuthoringSessionStore.Save(session);
  118: public void Delete(string strategyId) => AuthoringSessionStore.Delete(strategyId);
  131: public static class AuthoringSessionStore
  139: public static string Directory { get; } = Path.Combine(
  146: public static bool Save(AuthoringSessionSnapshot session)
  167: public static IReadOnlyList<AuthoringSessionSnapshot> List()
  180: public static AuthoringSessionSnapshot? Load(string strategyId) =>
  183: public static void Delete(string strategyId)
```

## src/linux/UI/TradingTerminal.Settings/Authoring/LineDiff.cs
```cs
    5: public sealed record DiffLine(string Kind, string Text);
   14: public static class LineDiff
   19: public static (int Added, int Removed) Count(string before, string after)
   34: public static IReadOnlyList<DiffLine> Build(string before, string after)
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.AuthoredStrategy.cs
```cs
    7: public sealed partial class StrategyAuthoringViewModel
   14: public bool CanonicalPaperStrategyIntentSupported => ConfirmedStrategyIntent?.IntentModel.Kind is
   19: public string CanonicalPaperStrategyAvailabilityText => !ResearchEvidenceReadyForGeneration
   34: public bool CanGenerateCanonicalPaperStrategy =>
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.NativeStrategyRun.cs
```cs
   11: public sealed partial class StrategyAuthoringViewModel
   23: public ObservableCollection<NativeStrategyEvidencePanel> NativeStrategyEvidencePanels { get; } =
   31: public bool IsNativeStrategyAgentWired => _strategyAgentClient is not null;
   32: public bool ShowNativeStrategyRunPanel =>
   34: public bool ShowLegacyCandidateBoundary =>
   87: public string NativeRunIdentityText => LoadedNativeRunId is null
   91: public string NativeResearchAvailabilityText => NativeResearchSessionUnavailable
   95: public bool HasNativeRunFailure => !string.IsNullOrWhiteSpace(NativeRunFailureDetail);
   96: public bool HasNativeArtifactContent => NativeArtifactContent.Length > 0;
  450: public sealed class NativeStrategyEvidencePanel : ObservableObject
  458: public NativeStrategyEvidencePanel(string key, string title, string authority)
  465: public string Key { get; }
  466: public string Title { get; }
  467: public string Authority { get; }
  468: public ObservableCollection<NativeStrategyEventRow> Events { get; } = [];
  470: public string Status
  476: public string Stage
  482: public string Summary
  488: public string? ExactFailure
  498: public bool HasExactFailure => !string.IsNullOrWhiteSpace(ExactFailure);
  500: public string Evidence
  537: public sealed record NativeStrategyEventRow(
  545: public string TimestampText => OccurredAtUtc.ToString("u");
  546: public string StageStatusText => $"#{Sequence} · {Stage} · {Status}";
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.Navigation.cs
```cs
    7: public enum StrategyAuthoringScreen
   17: public sealed partial class StrategyAuthoringViewModel
   25: public bool IsBriefStage => ActiveScreen == StrategyAuthoringScreen.Brief;
   26: public bool IsResearchStage => ActiveScreen == StrategyAuthoringScreen.Research;
   27: public bool IsChartDesignStage => ActiveScreen == StrategyAuthoringScreen.Design;
   28: public bool IsBuildStage => ActiveScreen == StrategyAuthoringScreen.Build;
   29: public bool IsValidateStage => ActiveScreen == StrategyAuthoringScreen.Validate;
   30: public bool IsPaperStage => ActiveScreen == StrategyAuthoringScreen.Paper;
   34: public bool IsDesignScreen => ActiveScreen is StrategyAuthoringScreen.Brief or
   36: public bool IsBuildScreen => ActiveScreen is StrategyAuthoringScreen.Build or
   39: public int WorkbenchGridColumn => IsDesignScreen ? 3 : 1;
   40: public int WorkbenchGridColumnSpan => IsDesignScreen ? 1 : 3;
   41: public bool ShowImplementationTabs => IsBuildScreen || !GenerateCandidateFirst || AuthoredUnitSpecification is not null;
   42: public bool ShowScreenNavigation => GenerateCandidateFirst;
   43: public bool ShowDesignRequestHeader => IsDesignScreen && GenerateCandidateFirst;
   44: public bool ShowImplementationHeader =>
   46: public bool ShowNativeImplementationHeader => ShowNativeStrategyRunPanel;
   47: public bool ShowResearchWorkspace => GenerateCandidateFirst && IsResearchStage;
   48: public bool HasAuthoredUnitCSharpFiles =>
   53: public bool CanCompileCurrentSource =>
   58: public bool CanOpenBriefScreen => GenerateCandidateFirst && !IsBriefStage && !IsGenerating;
   59: public bool CanOpenResearchScreen =>
   64: public bool CanOpenDesignScreen =>
   69: public bool CanOpenBuildScreen =>
   73: public bool CanOpenValidateScreen =>
   78: public bool CanOpenPaperScreen =>
   84: public bool ShowDesignCandidateReview =>
   86: public bool ShowBuildGenerationProgress =>
   88: public bool ShowBuildBusyStop =>
   90: public bool ShowBuildCandidateResults =>
   92: public bool ShowCandidateEmptyState => IsDesignScreen
   96: public bool ShowStartImplementationAction =>
  102: public bool ShowCliWorkspaceFooter =>
  105: public string ActiveScreenTitle => !GenerateCandidateFirst
  118: public string ActiveScreenDescription => !GenerateCandidateFirst
  139: public string CandidateTabHeader => IsDesignScreen ? "Request" : "Compare";
  141: public string CandidateEmptyTitle => IsDesignScreen
  145: public string CandidateEmptyText => IsDesignScreen
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.ResearchDataset.cs
```cs
   10: public sealed partial class StrategyAuthoringViewModel
   24: public bool HasResearchDataset => ResearchDatasetDefinition is not null;
   25: public bool HasResearchChartSelection => PendingResearchChartSelection is not null;
   26: public int ResearchEventSampleCount => ResearchDatasetDefinition?.Samples.Count ?? 0;
   27: public bool HasResearchExperimentEvidence => ResearchExperimentEvidence is not null;
   28: public bool CanRunResearchExperiment =>
   30: public bool ResearchEvidenceReadyForGeneration => ResearchDatasetDefinition is null ||
   35: public IReadOnlyList<ResearchEventSampleV1> ResearchEventSamples =>
   37: public string ResearchDatasetStatusText => ResearchDatasetDefinition is null
   40: public string ResearchChartSelectionText => PendingResearchChartSelection is null
   45: public string ResearchExperimentStatusText => ResearchExperimentEvidence is null
   54: public string ResearchFormulaText => ResearchExperimentEvidence?.Formula ?? "No research formula yet.";
   60: public void SetResearchChartSelection(ResearchChartSelectionV1 selection)
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.StrategyIntent.cs
```cs
   11: public sealed partial class StrategyAuthoringViewModel
   39: public ObservableCollection<StrategyIntentProfileOption> StrategyIntentProfiles { get; } =
   44: public IReadOnlyList<StrategyIntentShapeOption> StrategyIntentShapes { get; } =
   60: public ObservableCollection<StrategyIntentRequirementRow> StrategyIntentRequirements { get; } = [];
   61: public ObservableCollection<StrategyResearchEvidenceRow> StrategyResearchEvidenceRows { get; } = [];
   62: public ObservableCollection<StrategyResearchFalsifierRow> StrategyResearchFalsifierRows { get; } = [];
   63: public ObservableCollection<StrategyResearchUnresolvedRow> StrategyResearchUnresolvedRows { get; } = [];
   64: public ObservableCollection<StrategyResearchResolvedRow> StrategyResearchResolvedRows { get; } = [];
   65: public ObservableCollection<StrategyIntentQuestionV1> StrategyIntentQuestions { get; } = [];
   66: public ObservableCollection<StrategyIntentIssueV1> StrategyIntentIssues { get; } = [];
   68: public bool HasStrategyIntentReview => _strategyIntentReviewStarted || StrategyIntentDraft is not null;
   69: public bool HasConfirmedStrategyIntent => ConfirmedStrategyIntent is not null;
   70: public string StrategyIntentFamilyText =>
   79: public bool CanConfirmStrategyIntentReview =>
   92: public bool CanEnterFourLaneConformance =>
  111: public bool CanGenerateStrategyImplementations => CanEnterFourLaneConformance;
  114: public void AddStrategyIntentProfile(StrategyStarterBrief brief)
  125: public void SelectStrategyIntentProfile(StrategyStarterBrief brief)
  137: public void BeginStrategyIntentReview()
  204: public void ReviewStrategyIntent(
 1009: public sealed record StrategyIntentProfileOption(
 1015: public static StrategyIntentProfileOption FromBrief(StrategyStarterBrief brief) =>
 1018: public static StrategyIntentProfileOption FromRestoredClassification(
 1026: public static StrategyIntentProfileOption CreateSignalOnly()
 1054: public override string ToString() => Title;
 1057: public sealed record StrategyIntentShapeOption(
 1063: public override string ToString() => Title;
 1066: public sealed record StrategyIntentApplicabilityOption(
 1070: public override string ToString() => Label;
 1073: public sealed partial class StrategyResearchEvidenceRow : ObservableObject
 1092: public string EvidenceId { get; }
 1093: public bool IsMaterial { get; }
 1094: public string MaterialityLabel => IsMaterial ? "Material evidence" : "Supporting evidence";
 1095: public IReadOnlyList<string> CandidateStatementIds { get; }
 1097: public static StrategyResearchEvidenceRow FromEvidence(
 1101: public ResearchEvidenceRequirementV1 ToEvidence() => new(
 1114: public sealed partial class StrategyResearchFalsifierRow : ObservableObject
 1129: public string FalsifierId { get; }
 1130: public bool IsMaterial { get; }
 1131: public string MaterialityLabel => IsMaterial ? "Material falsifier" : "Supporting falsifier";
 1132: public IReadOnlyList<string> CandidateStatementIds { get; }
 1134: public static StrategyResearchFalsifierRow FromFalsifier(
 1138: public ResearchFalsifierV1 ToFalsifier() => new(
 1147: public sealed partial class StrategyResearchUnresolvedRow : ObservableObject
 1168: public string ItemId { get; }
 1169: public bool IsMaterial { get; }
 1170: public string MaterialityLabel => IsMaterial ? "Material unresolved item — launch remains locked" : "Non-material open item";
 1171: public IReadOnlyList<string> CandidateStatementIds { get; }
 1172: public bool CanResolve => !string.IsNullOrWhiteSpace(Resolution);
 1174: public static StrategyResearchUnresolvedRow FromUnresolvedItem(
 1179: public ResearchUnresolvedItemV1 ToUnresolvedItem() => new(
 1185: public ResearchResolvedItemV1 ToResolvedItem(string resolutionProvenance) => new(
 1205: public sealed record StrategyResearchResolvedRow(
 1213: public string MaterialityLabel => IsMaterial ? "Material research choice resolved" : "Supporting research choice resolved";
 1215: public static StrategyResearchResolvedRow FromResolvedItem(ResearchResolvedItemV1 item) => new(
 1223: public ResearchResolvedItemV1 ToResolvedItem() => new(
 1232: public sealed partial class StrategyIntentRequirementRow : ObservableObject
 1295: public string RequirementId { get; }
 1296: public StrategySemanticStageV1 Stage { get; }
 1297: public string StageLabel { get; }
 1298: public string Question { get; }
 1299: public string Description { get; }
 1300: public bool IsMaterial { get; }
 1301: public string MaterialityLabel => IsMaterial ? "Material requirement" : "Supporting requirement";
 1302: public StrategyRequirementProvenanceV1? Provenance { get; }
 1303: public string ValueTypeId { get; }
 1304: public string? ValueUnit { get; }
 1305: public bool MustBeNotApplicable { get; }
 1306: public bool CanChangeApplicability { get; }
 1307: public IReadOnlyList<StrategyIntentApplicabilityOption> ApplicabilityOptions { get; }
 1308: public string AnswerWatermark => SelectedApplicability.Disposition switch
 1316: public static StrategyIntentRequirementRow FromQuestion(
 1336: public static StrategyIntentRequirementRow FromRequirement(
 1355: public StrategySemanticRequirementV1 ToRequirement(
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.TradeIrBacktest.cs
```cs
   15: public sealed partial class StrategyAuthoringViewModel
   27: public bool HasBacktestReadinessContext => HasChosenGeneratedCandidate || HasLoadedCombinedTradeIr;
   29: public bool HasTradeIrBacktestResult => TradeIrBacktestResult is not null;
   31: public bool CanPrepareGeneratedCandidateForBacktest =>
   39: public string BacktestActionText => IsRunningTradeIrBacktest
   45: public string CandidateBacktestAvailabilityText
  156: public string BacktestReadinessTitle
  168: public string BacktestReadinessText
  208: public IReadOnlyList<CandidateReadinessStageRow> BacktestReadinessStages
  292: public string TradeIrBacktestStatusText => TradeIrBacktestResult switch
  302: public string TradeIrBacktestSummary
  318: public string TradeIrBacktestIssueText => TradeIrBacktestResult is null
  323: public string TradeIrBacktestBoundaryText =>
  508: public sealed partial class StrategyGenerationCandidateOption
  510: public string SyntheticTestCapabilityText => Result.Lane switch
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.TradeIrSynthesis.cs
```cs
   15: public sealed partial class StrategyAuthoringViewModel
   31: public bool HasCombinedTradeIrSynthesis => CombinedTradeIrSynthesis is not null;
   33: public bool HasCurrentPackageValidCombinedTradeIr =>
   38: public bool HasLoadedCombinedTradeIr =>
   45: public bool CanSynthesizeTradeIr =>
   54: public bool CanUseCombinedTradeIr =>
   61: public string CombinedTradeIrStatusText => CombinedTradeIrSynthesis switch
   70: public string CombinedTradeIrActionText => HasLoadedCombinedTradeIr
   74: public string CombinedTradeIrSourceSummary
   86: public string CombinedTradeIrTargetHash =>
   89: public string CombinedTradeIrReceiptHash =>
   92: public string CombinedTradeIrIssueText => CombinedTradeIrSynthesis is null
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.Validation.cs
```cs
    6: public sealed partial class StrategyAuthoringViewModel
   13: public bool HasHistoricalValidationEvidence => HistoricalValidationEvidence is { } evidence &&
   18: public bool CanRunHistoricalValidation =>
   23: public string HistoricalValidationStatusText => !HasHistoricalValidationEvidence
   28: public bool TryCreateHistoricalValidationContext(
   62: public bool AcceptHistoricalValidationEvidence(
  103: public IReadOnlyDictionary<string, object?>? ValidatedPaperParameters =>
  106: public bool BindValidatedPaperBook(string bookId, string accountId, out string reason)
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.Workspace.cs
```cs
    7: public sealed partial class StrategyAuthoringViewModel
   13: public string WorkspaceRevisionText =>
   16: public string BriefStageState => StageStateText(StrategyWorkspaceStageV1.Brief);
   17: public string ResearchStageState => StageStateText(StrategyWorkspaceStageV1.Research);
   18: public string DesignStageState => StageStateText(StrategyWorkspaceStageV1.Design);
   19: public string BuildStageState => StageStateText(StrategyWorkspaceStageV1.Build);
   20: public string ValidateStageState => StageStateText(StrategyWorkspaceStageV1.Validate);
   21: public string PaperStageState => StageStateText(StrategyWorkspaceStageV1.Paper);
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.cs
```cs
   42: public sealed partial class StrategyAuthoringViewModel : ViewModelBase, IDisposable
   95: public StrategyAuthoringViewModel(
  215: public bool AiEnabled => _ai is not null;
  216: public bool AiHasProvider => AiProviders.Any(p => p.IsAvailable);
  220: public bool HasConversation => Messages.Count > 0;
  233: public IReadOnlyList<StrategyStarterBrief> AllStarterBriefs { get; }
  234: public ObservableCollection<StrategyStarterBrief> VisibleStarterBriefs { get; }
  235: public IReadOnlyList<string> StarterFamilyOptions { get; }
  236: public IReadOnlyList<string> StarterHorizonOptions { get; }
  237: public IReadOnlyList<string> StarterDataOptions { get; }
  244: public string StarterResultText =>
  324: public ObservableCollection<StrategyCandidateGroupRow> CandidateGroups { get; }
  325: public ObservableCollection<StrategyCandidateStatementV1> CandidateOpenQuestions { get; }
  326: public ObservableCollection<StrategyBuildSupportRow> CandidateBuildSupport { get; }
  327: public ObservableCollection<StrategyCandidateIssueV1> CandidateIssues { get; }
  328: public ObservableCollection<StrategyGenerationCandidateOption> GeneratedCandidateOptions { get; }
  329: public ObservableCollection<StrategyGenerationLaneProgressRow> GenerationLaneProgressRows { get; }
  336: public ObservableCollection<AuthoringChartReferenceSnapshot> ChartReferences { get; }
  337: public ObservableCollection<AuthoredChartReferenceInspectionV1> ChartReferenceInspections { get; }
  338: public ObservableCollection<ChartPatternSelectionV1> ChartPatternSelections { get; }
  339: public IReadOnlyList<BarSize> ChartPatternTimeframeOptions { get; } = Enum.GetValues<BarSize>();
  347: public bool HasChartReferences => ChartReferences.Count > 0;
  348: public bool HasChartReferenceInspections => ChartReferenceInspections.Count > 0;
  349: public bool HasSearchableChartReference => ChartReferences.Any(reference =>
  356: public bool HasChartPatternSearchResult => ChartPatternSearchResult is not null;
  357: public bool HasChartPatternMatches => ChartPatternSearchResult?.Matches.Count > 0;
  358: public bool HasSelectedChartPattern => ChartPatternSelections.Count > 0;
  359: public string SelectedChartPatternText => ChartPatternSelections.LastOrDefault() is { } selection
  362: public bool HasUninspectedChartReferences => ChartReferences.Any(reference =>
  366: public bool HasUnresolvedChartReferences
  380: public string ChartReferenceSummary => ChartReferences.Count switch
  386: public string ChartReferenceReadinessText => HasUninspectedChartReferences
  699: public bool HasCandidate => CurrentCandidate is not null;
  700: public bool HasGeneratedCandidates => GeneratedCandidateOptions.Count > 0;
  701: public bool HasCandidateContent => HasCandidate || HasGeneratedCandidates;
  702: public bool HasCandidateRestoreWarning => !string.IsNullOrWhiteSpace(CandidateRestoreWarning);
  703: public int SelectableGeneratedCandidateCount =>
  705: public int BlockedGeneratedCandidateCount =>
  707: public bool HasBlockedGeneratedCandidates => BlockedGeneratedCandidateCount > 0;
  708: public StrategyGenerationCandidateOption? FirstBlockedGeneratedCandidateOption =>
  710: public string CandidateBatchHeadline
  722: public bool HasSelectedGeneratedCandidate => SelectedGeneratedCandidateOption?.Candidate is not null;
  723: public bool HasChosenGeneratedCandidate => ChosenGeneratedCandidateOption is not null;
  724: public bool HasPendingFourLanePrompt => !string.IsNullOrWhiteSpace(_pendingFourLanePrompt);
  725: public bool IsGeneratingCandidates => IsGenerating && GenerateCandidateFirst && !IsSynthesizingTradeIr;
  726: public bool HasRetainedCandidateBatchDuringGeneration => IsGeneratingCandidates && HasGeneratedCandidates;
  727: public bool CanChooseGeneratedCandidate =>
  739: public bool CanRevalidateGeneratedCandidate =>
  748: public bool CanConfirmCandidate => CurrentCandidate is not null && CandidateContentHash is not null &&
  750: public string GenerationModeLabel => GenerateCandidateFirst ? "STRATEGY RESEARCH" : "EXPERT C#";
  751: public string GenerationModeActionText => GenerateCandidateFirst
  754: public string GenerationLaneText => GenerateCandidateFirst ? "Research, confirm, then implement" : "Expert code";
  755: public string SendButtonText => GenerateCandidateFirst ? "Check strategy  ⌘↵" : "Generate code  ⌘↵";
  756: public string AuthoringBoundaryText => GenerateCandidateFirst
  761: public bool HasExpertCSharpFiles =>
  765: public bool HasNonCSharpExpertArtifact =>
  768: public string CandidateActionText => SelectedGeneratedCandidateOption is { CandidateHashSha256: { } selectedHash } &&
  774: public string ChosenGeneratedCandidateSummary => ChosenGeneratedCandidateOption is { } chosen
  777: public string GenerationProgressSummary
  951: public ObservableCollection<StrategyDiagnostic> Diagnostics { get; }
  966: public ObservableCollection<AuthoredFile> Files { get; }
  999: public ObservableCollection<AiProviderChoice> AiProviders { get; }
 1005: public ObservableCollection<string> Models { get; } = [];
 1012: public IReadOnlyList<CodegenEffort> Efforts { get; } =
 1019: public bool EffortSupported => SelectedAiProvider is { } choice && AiModelCatalog.SupportsEffort(choice.ProviderId);
 1057: public string ModelPillText =>
 1082: public ObservableCollection<AiModelChoice> AllModels { get; }
 1150: public IReadOnlyList<StrategyBuildEffort> BuildEfforts { get; } =
 1171: public IReadOnlyList<AgentCliAdapter> AvailableClis => _cliLauncher?.AvailableClis() ?? [];
 1240: public ObservableCollection<AuthoringMessage> Messages { get; }
 1244: public ObservableCollection<string> Activity { get; }
 1253: public ObservableCollection<BuildTask> Tasks { get; }
 1466: public string UsageText => InputTokens + OutputTokens == 0
 1528: public bool CanGenerateFourCandidates =>
 3028: public ObservableCollection<AuthoringSessionSnapshot> SavedSessions { get; } = [];
 3553: public ObservableCollection<ReviewFileEntry> ReviewFiles { get; } = [];
 4318: public void Dispose()
 4342: public sealed class MyStrategy : IBacktestStrategy
 4344: public static StrategyParameterSchema Schema { get; } = new(
 4348: public static IBacktestStrategy Create(Contract contract, StrategyParameters p) =>
 4355: public MyStrategy(Contract contract) : this(contract, 20, 1.5) { }
 4357: public MyStrategy(Contract contract, int lookback, double threshold)
 4364: public Task OnStartAsync(IClock clock, IOrderRouter router, CancellationToken ct)
 4367: public Task OnTickAsync(Tick tick, IClock clock, IOrderRouter router, CancellationToken ct)
 4375: public Task OnOrderEventAsync(OrderEvent evt, CancellationToken ct) => Task.CompletedTask;
 4377: public Task OnEndAsync(IClock clock, IOrderRouter router, CancellationToken ct)
 4385: public sealed partial class AuthoredFile(string name, string content) : ObservableObject
 4399: public sealed partial class AuthoringMessage : ObservableObject
 4401: public const string KindUser = "User";
 4402: public const string KindAssistant = "Assistant";
 4403: public const string KindNote = "Note";
 4404: public const string KindTool = "Tool";
 4405: public const string KindPlan = "Plan";
 4406: public const string KindPlanText = "PlanText";
 4407: public const string KindFiles = "Files";
 4409: public AuthoringMessage(CodegenRole role, string text)
 4425: public static AuthoringMessage System(string? text) => new(KindNote, text ?? string.Empty);
 4430: public static AuthoringMessage Tool(string state, string title, string detail, string? more = null) =>
 4441: public static AuthoringMessage Plan(IReadOnlyList<BuildTask> tasks) =>
 4445: public static AuthoringMessage PlanText(string text) => new(KindPlanText, text);
 4447: public static AuthoringMessage FilesChanged(IReadOnlyList<FileChangeSummary> changes) =>
 4453: public CodegenRole Role { get; }
 4454: public bool IsSystem { get; }
 4455: public string Kind { get; }
 4456: public bool IsUser => !IsSystem && Role == CodegenRole.User;
 4457: public bool IsAssistant => !IsSystem && Role == CodegenRole.Assistant;
 4459: public string? ToolState { get; private init; }
 4460: public string? ToolTitle { get; private init; }
 4461: public string? ToolDetail { get; private init; }
 4462: public string? ToolMore { get; private init; }
 4463: public bool HasMore => !string.IsNullOrEmpty(ToolMore);
 4465: public IReadOnlyList<BuildTask>? PlanTasks { get; private init; }
 4466: public IReadOnlyList<FileChangeSummary>? FileChanges { get; private init; }
 4469: public string PlanSnapshotText() => PlanTasks is null
 4482: public DateTime TimestampLocal { get; } = DateTime.Now;
 4486: public sealed record StrategyCandidateGroupRow(
 4493: public string Location => Depth == 0 ? Kind : $"{new string('·', Depth)} {Kind}";
 4497: public sealed record StrategyBuildSupportRow(
 4504: public sealed partial class StrategyGenerationCandidateOption : ObservableObject
 4506: public StrategyGenerationCandidateOption(StrategyGenerationLaneResultV1 result) => Result = result;
 4508: public StrategyGenerationLaneResultV1 Result { get; }
 4515: public StrategyGenerationCandidateV1? Candidate => Result.Candidate;
 4516: public string? CandidateHashSha256 => Result.CandidateHashSha256;
 4517: public bool IsGenerated => Result.Generated;
 4518: public bool IsFailed => Result.Readiness is StrategyGenerationReadinessV1.Invalid
 4521: public bool PackageValidationAvailable => Result.PackageValidationAvailable;
 4522: public string LaneName => StrategyGenerationLaneCatalogV1.DisplayName(Result.Lane);
 4523: public string Representation => Result.Lane switch
 4531: public string ContractVersion => Candidate?.PackageBinding.ArtifactContractVersion ?? "no contract";
 4532: public string ContractAuthority => Candidate?.PackageBinding.Authority.AuthorityId ?? "no authority";
 4533: public string ContractRole => Candidate?.PackageBinding.Authority.SemanticRole switch
 4539: public string LoweringBoundary => Candidate?.PackageBinding.Authority.LoweringMode switch
 4547: public string CompatibilityBoundary => Candidate?.PackageBinding.Authority.ExternalCompatibility switch
 4553: public string SpecificationReference =>
 4555: public string StatusText => Result.Readiness switch
 4564: public string FailureHeading => Result.Readiness switch
 4571: public string ArtifactName => Candidate?.Artifact.FileName ?? "no artifact";
 4572: public string Summary => Candidate?.Interpretation ?? ErrorText;
 4573: public StrategyCandidateGenerationIssueV1? FirstIssue =>
 4577: public string FirstIssueCode => FirstIssue?.Code ?? "No issue code reported";
 4578: public string FirstIssuePath => FirstIssue?.Path ?? "No issue path reported";
 4579: public string FirstIssueMessage => FirstIssue?.Message
 4582: public string ErrorText => string.Join(Environment.NewLine, Result.Issues.Select(issue =>
 4584: public string RecoveryText => FirstIssueCode switch
 4596: public string ArtifactPreview => Candidate?.Artifact.Source
 4600: public string InspectablePreview => !string.IsNullOrWhiteSpace(ArtifactPreview)
 4605: public string PreviewHeading => !string.IsNullOrWhiteSpace(ArtifactPreview) && Candidate?.Artifact is { } artifact
 4610: public string PreviewStateText => IsChosen
 4613: public string FlexibilityText => Candidate is null
 4619: public sealed partial class StrategyGenerationLaneProgressRow : ObservableObject
 4621: public StrategyGenerationLaneProgressRow(StrategyGenerationLaneV1 lane) => Lane = lane;
 4623: public StrategyGenerationLaneV1 Lane { get; }
 4624: public string LaneName => StrategyGenerationLaneCatalogV1.DisplayName(Lane);
 4625: public string AgentName => Lane switch
 4633: public string ArtifactName => Lane switch
 4641: public string PurposeText => Lane switch
 4649: public string ValidationPlanText => Lane switch
 4676: public bool HasResult => ResultOption is not null;
 4677: public string InspectablePreview => ResultOption?.InspectablePreview ?? string.Empty;
 4678: public string PreviewHeading => ResultOption?.PreviewHeading ?? $"{ArtifactName} · waiting for result";
 4680: public void Apply(StrategyGenerationLaneProgressV1 progress)
 4711: public string StateLabel => State switch
 4724: public string StateDetail => State switch
 4740: public string PipelineText => State switch
 4781: public sealed record CandidateReadinessStageRow(
 4788: public sealed record FileChangeSummary(string Name, int Added, int Removed)
 4790: public string Counts => Removed > 0 ? $"+{Added} −{Removed}" : $"+{Added}";
 4793: public static string Pack(IReadOnlyList<FileChangeSummary> changes) =>
 4796: public static IReadOnlyList<FileChangeSummary>? Unpack(string? packed)
 4814: public sealed class ReviewFileEntry(string name, IReadOnlyList<DiffLine> lines)
 4816: public string Name { get; } = name;
 4817: public IReadOnlyList<DiffLine> Lines { get; } = lines;
 4818: public int Added { get; } = lines.Count(l => l.Kind == "add");
 4819: public int Removed { get; } = lines.Count(l => l.Kind == "del");
 4820: public string Counts => Removed > 0 ? $"+{Added} −{Removed}" : $"+{Added}";
 4825: public sealed class AiProviderChoice(IStrategyCodegenClient client)
 4827: public IStrategyCodegenClient Client { get; } = client;
 4828: public string ProviderId => Client.ProviderId;
 4829: public string DisplayName => Client.DisplayName;
 4830: public bool IsAvailable => Client.IsAvailable;
 4831: public string Label => IsAvailable ? DisplayName : $"{DisplayName} — not set up";
 4835: public enum BuildTaskState
 4845: public sealed partial class BuildTask(string title) : ObservableObject
 4847: public string Title { get; } = title;
```

## src/linux/UI/TradingTerminal.Settings/Authoring/StrategyStarterCatalog.cs
```cs
   11: public sealed record StrategyStarterBrief(
   20: public IReadOnlyList<string> FamilyLabels =>
   24: public StrategyStarterAxisLabels AxisLabels =>
   29: public static class StrategyStarterFamilies
   31: public const string TrendAndMomentum = "Trend & momentum";
   32: public const string ReversionAndRelativeValue = "Reversion & relative value";
   33: public const string ValueCarryAndQuality = "Value, carry & quality";
   34: public const string OrderFlowAndLiquidity = "Order flow & liquidity";
   35: public const string EventsAndCatalysts = "Events & catalysts";
   36: public const string VolatilityAndDerivatives = "Volatility & derivatives";
   37: public const string AllocationAndHedging = "Allocation & hedging";
   38: public const string Execution = "Execution";
   39: public const string AdaptiveAndMl = "Adaptive & ML";
   41: public static IReadOnlyList<string> All { get; } =
   56: public static class StrategyStarterTaxonomy
   58: public static IReadOnlyList<string> GetFamilyLabels(StrategySpec specification)
  137: public sealed record StrategyStarterAxisLabels(
  154: public static StrategyStarterAxisLabels From(StrategySpec specification)
  180: public static class StrategyStarterLabels
  182: public static string For(AssetClass value) => value switch
  193: public static string For(StrategyObjectiveKind value) => value switch
  204: public static string For(ReturnHypothesisKind value) => value switch
  223: public static string For(StrategyTriggerKind value) => value switch
  237: public static string For(StrategyHorizonKind value) => value switch
  247: public static string For(MarketTopologyKind value) => value switch
  260: public static string For(ExposureGeometryKind value) => value switch
  273: public static string For(StrategyInformationKind value) => value switch
  288: public static string For(SignalModelKind value) => value switch
  302: public static string For(PortfolioConstructionKind value) => value switch
  316: public static string For(StrategyExecutionPolicyKind value) => value switch
  333: public static string For(StrategyStateKind value) => value switch
  345: public static string For(StrategyRiskExitKind value) => value switch
  361: public static string For(StrategyAdaptationKind value) => value switch
  374: public sealed record StrategyStarterCatalogIssue(
  381: public static class StrategyStarterCatalog
  383: public const string QuoteL1EmaSmokePrompt =
  386: public const string LiquiditySweepFadePrompt =
  389: public const string FiveMinuteMomentumBreakoutPrompt =
  392: public const string CumulativeDeltaDivergencePrompt =
  395: public static IReadOnlyList<StrategyStarterBrief> All { get; } =
  855: public static bool MatchesSearch(StrategyStarterBrief brief, string? query)
  869: public static IReadOnlyList<StrategyStarterBrief> Filter(string? query) =>
  873: public static IReadOnlyList<StrategyStarterBrief> Filter(
  882: public static IReadOnlyList<StrategyStarterCatalogIssue> ValidateAll()
```

## src/linux/UI/TradingTerminal.Settings/Notifications/NotificationsSettingsViewModel.cs
```cs
   17: public sealed partial class NotificationsSettingsViewModel : ViewModelBase
   23: public NotificationsSettingsViewModel(
   61: public IReadOnlyList<string> AiAnalystProviders { get; } =
```

## src/linux/UI/TradingTerminal.Settings/Notifications/NotificationsUserFile.cs
```cs
   13: public static class NotificationsUserFile
   19: public static string Path { get; } = System.IO.Path.Combine(
   25: public static void Save(NotificationsOptions options)
```

## src/linux/UI/TradingTerminal.Settings/Research/ResearchSettingsViewModel.cs
```cs
   17: public sealed partial class ResearchSettingsViewModel : ViewModelBase
   23: public ResearchSettingsViewModel(
```

## src/linux/UI/TradingTerminal.Settings/Research/ResearchUserFile.cs
```cs
   14: public static class ResearchUserFile
   18: public static string Path { get; } = System.IO.Path.Combine(
   25: public static void Save(ResearchReproOptions options, bool autoLaunchSidecar, int sidecarPort)
```

## src/linux/UI/TradingTerminal.Settings/Support/SupportInfo.cs
```cs
   15: public const string DeveloperEmail = "dhruvsha.info@gmail.com";
   17: public const string ProductName = "DaxAlgo Terminal";
   19: public const string GitHubUrl = "https://github.com/dhruuvsharma/DaxAlgo-Terminal";
   23: public static string DisplayVersion
```

## src/linux/UI/TradingTerminal.Settings/Support/SupportViewModel.cs
```cs
   19: public sealed partial class SupportViewModel : ViewModelBase
   23: public SupportViewModel(ILogger<SupportViewModel> logger)
   28: public string ProductName => SupportInfo.ProductName;
   30: public string Version => SupportInfo.DisplayVersion;
   32: public string DeveloperEmail => SupportInfo.DeveloperEmail;
   34: public string ThankYouMessage =>
   39: public string DonateMessage =>
   53: public event EventHandler? CloseRequested;
```
