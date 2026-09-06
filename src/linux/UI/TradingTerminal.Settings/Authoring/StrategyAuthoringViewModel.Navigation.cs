using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.App.Authoring;

public enum StrategyAuthoringScreen
{
    Design = 0,
    Build = 1,
    Brief = 2,
    Research = 3,
    Validate = 4,
    Paper = 5,
}

public sealed partial class StrategyAuthoringViewModel
{
    [ObservableProperty]
    private StrategyAuthoringScreen _activeScreen = StrategyAuthoringScreen.Brief;

    [ObservableProperty]
    private bool _hasDetachedImplementationSource;

    public bool IsBriefStage => ActiveScreen == StrategyAuthoringScreen.Brief;
    public bool IsResearchStage => ActiveScreen == StrategyAuthoringScreen.Research;
    public bool IsChartDesignStage => ActiveScreen == StrategyAuthoringScreen.Design;
    public bool IsBuildStage => ActiveScreen == StrategyAuthoringScreen.Build;
    public bool IsValidateStage => ActiveScreen == StrategyAuthoringScreen.Validate;
    public bool IsPaperStage => ActiveScreen == StrategyAuthoringScreen.Paper;

    // Compatibility layout groups: Brief/Research/Design reuse the current request canvas;
    // Build/Validate/Paper reuse the current artifact workbench until their dedicated panels land.
    public bool IsDesignScreen => ActiveScreen is StrategyAuthoringScreen.Brief or
        StrategyAuthoringScreen.Research or StrategyAuthoringScreen.Design;
    public bool IsBuildScreen => ActiveScreen is StrategyAuthoringScreen.Build or
        StrategyAuthoringScreen.Validate or StrategyAuthoringScreen.Paper;

    public int WorkbenchGridColumn => IsDesignScreen ? 3 : 1;
    public int WorkbenchGridColumnSpan => IsDesignScreen ? 1 : 3;
    public bool ShowImplementationTabs => IsBuildScreen || !GenerateCandidateFirst || AuthoredUnitSpecification is not null;
    public bool ShowScreenNavigation => GenerateCandidateFirst;
    public bool ShowDesignRequestHeader => IsDesignScreen && GenerateCandidateFirst;
    public bool ShowImplementationHeader =>
        (IsBuildScreen && !ShowNativeStrategyRunPanel) || !GenerateCandidateFirst;
    public bool ShowNativeImplementationHeader => ShowNativeStrategyRunPanel;
    public bool ShowResearchWorkspace => GenerateCandidateFirst && IsResearchStage;
    public bool HasAuthoredUnitCSharpFiles =>
        AuthoredUnitSpecification is not null &&
        Files.Count > 0 &&
        Files.All(static file => file.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));

    public bool CanCompileCurrentSource =>
        (HasExpertCSharpFiles || HasAuthoredUnitCSharpFiles) &&
        !HasDetachedImplementationSource &&
        !IsGenerating;

    public bool CanOpenBriefScreen => GenerateCandidateFirst && !IsBriefStage && !IsGenerating;
    public bool CanOpenResearchScreen =>
        GenerateCandidateFirst &&
        !IsResearchStage &&
        (HasCandidate || AuthoredUnitSpecification is not null) &&
        !IsGenerating;
    public bool CanOpenDesignScreen =>
        GenerateCandidateFirst &&
        !IsChartDesignStage &&
        (HasCandidate || HasChartReferences || AuthoredUnitSpecification is not null) &&
        !IsGenerating;
    public bool CanOpenBuildScreen =>
        !IsBuildStage &&
        (IsNativeStrategyAgentWired || CanEnterFourLaneConformance) &&
        !IsGenerating;
    public bool CanOpenValidateScreen =>
        GenerateCandidateFirst &&
        !IsValidateStage &&
        (IsRegistered || StrategyWorkspace.Bindings.BuildArtifactHashSha256 is not null) &&
        !IsGenerating;
    public bool CanOpenPaperScreen =>
        GenerateCandidateFirst &&
        !IsPaperStage &&
        StrategyWorkspace.Bindings.ValidationEvidenceHashSha256 is not null &&
        !IsGenerating;

    public bool ShowDesignCandidateReview =>
        (IsBriefStage || IsChartDesignStage) && HasCandidate;
    public bool ShowBuildGenerationProgress =>
        IsBuildScreen && !ShowNativeStrategyRunPanel && IsGeneratingCandidates;
    public bool ShowBuildBusyStop =>
        IsBuildScreen && !ShowNativeStrategyRunPanel && IsGenerating && !IsGeneratingCandidates;
    public bool ShowBuildCandidateResults =>
        IsBuildScreen && !ShowNativeStrategyRunPanel && HasGeneratedCandidates;
    public bool ShowCandidateEmptyState => IsDesignScreen
        ? !HasCandidate
        : !ShowNativeStrategyRunPanel && !HasGeneratedCandidates &&
          !HasAuthoredUnitCSharpFiles && !IsGeneratingCandidates;
    public bool ShowStartImplementationAction =>
        IsBuildScreen &&
        !ShowNativeStrategyRunPanel &&
        !HasGeneratedCandidates &&
        !HasAuthoredUnitCSharpFiles &&
        !IsGeneratingCandidates;
    public bool ShowCliWorkspaceFooter =>
        IsBuildScreen && !ShowNativeStrategyRunPanel && AvailableClis.Count > 0;

    public string ActiveScreenTitle => !GenerateCandidateFirst
        ? "Expert Code"
        : ActiveScreen switch
        {
            StrategyAuthoringScreen.Brief => "Brief",
            StrategyAuthoringScreen.Research => "Research",
            StrategyAuthoringScreen.Design => "Chart Design",
            StrategyAuthoringScreen.Build => "Build",
            StrategyAuthoringScreen.Validate => "Validate",
            StrategyAuthoringScreen.Paper => "Paper",
            _ => "Brief",
        };

    public string ActiveScreenDescription => !GenerateCandidateFirst
        ? "Direct C# authoring is a separate legacy path; it does not inherit the confirmed Strategy Builder request or its lane results."
        : ActiveScreen switch
        {
            StrategyAuthoringScreen.Brief =>
                "Describe a visualizer, research question, or strategy; confirm instruments, timeframe, data, and product type.",
            StrategyAuthoringScreen.Research =>
                "Capture separate observation and future-outcome windows, label events, and run leakage-safe chronological feature experiments.",
            StrategyAuthoringScreen.Design =>
                "Design is optional. Review references and typed drawing layers without changing strategy rules by hiding a layer.",
            StrategyAuthoringScreen.Build when ShowNativeStrategyRunPanel =>
                "Load one confirmed native run and inspect its retained Research, VibeQuant/AKQuant, CSP, and comparison evidence.",
            StrategyAuthoringScreen.Build =>
                "Generate, verify, compile, review, and install the exact typed unit.",
            StrategyAuthoringScreen.Validate =>
                "Inspect only evidence bound to this exact workspace revision; synthetic smoke is not historical performance.",
            StrategyAuthoringScreen.Paper =>
                "Select a Paper book only after exact validation evidence exists. Real-money routing remains unavailable.",
            _ => "Define and confirm the request before implementation.",
        };

    public string CandidateTabHeader => IsDesignScreen ? "Request" : "Compare";

    public string CandidateEmptyTitle => IsDesignScreen
        ? "No strategy request yet"
        : "No implementation run yet";

    public string CandidateEmptyText => IsDesignScreen
        ? "Describe the idea in chat. The strategy meaning and required decisions will appear here for review."
        : "The confirmed strategy request is ready. Start implementation generation when you want the backend workers to run.";

    [RelayCommand(CanExecute = nameof(CanOpenBriefScreenAction))]
    private void OpenBriefScreen()
    {
        if (!CanOpenBriefScreen) return;
        OpenStage(StrategyAuthoringScreen.Brief,
            "Brief is open. Confirm the product, instruments, timeframe, data, and requested outcome.");
    }

    private bool CanOpenBriefScreenAction() => CanOpenBriefScreen;

    [RelayCommand(CanExecute = nameof(CanOpenResearchScreenAction))]
    private void OpenResearchScreen()
    {
        if (!CanOpenResearchScreen) return;
        OpenStage(StrategyAuthoringScreen.Research,
            "Research is open. Brush an observation window and a separate future outcome window on the host chart, then label the event.");
    }

    private bool CanOpenResearchScreenAction() => CanOpenResearchScreen;

    [RelayCommand(CanExecute = nameof(CanOpenBuildScreenAction))]
    private void OpenBuildScreen()
    {
        if (!CanOpenBuildScreen)
        {
            Status = IsNativeStrategyAgentWired
                ? "Stop the active task before opening Build, Test & Compare."
                : "Confirm the complete strategy request before opening Build, Test & Compare.";
            return;
        }

        OpenStage(StrategyAuthoringScreen.Build, string.Empty);
        Status = ShowNativeStrategyRunPanel
            ? "Build, Test & Compare is ready to load a retained native run ID. Chart-to-run creation is not connected here yet."
            : HasGeneratedCandidates
                ? "Build, Test & Compare is open on the retained implementation results."
                : "Build, Test & Compare is ready. Start implementation generation when you are ready.";
    }

    private bool CanOpenBuildScreenAction() => CanOpenBuildScreen;

    [RelayCommand(CanExecute = nameof(CanOpenDesignScreenAction))]
    private void OpenDesignScreen()
    {
        if (!CanOpenDesignScreen) return;

        OpenStage(StrategyAuthoringScreen.Design,
            "Chart Design is open. Appearance remains separate from computational strategy meaning.");
    }

    private bool CanOpenDesignScreenAction() => CanOpenDesignScreen;

    [RelayCommand(CanExecute = nameof(CanOpenValidateScreenAction))]
    private void OpenValidateScreen()
    {
        if (!CanOpenValidateScreen) return;
        OpenStage(StrategyAuthoringScreen.Validate,
            "Validate is open. Only exact-revision evidence may be promoted; synthetic smoke is labeled separately.");
    }

    private bool CanOpenValidateScreenAction() => CanOpenValidateScreen;

    [RelayCommand(CanExecute = nameof(CanOpenPaperScreenAction))]
    private void OpenPaperScreen()
    {
        if (!CanOpenPaperScreen) return;
        OpenStage(StrategyAuthoringScreen.Paper,
            "Paper is open for the validated revision. Real-money routing remains unavailable.");
    }

    private bool CanOpenPaperScreenAction() => CanOpenPaperScreen;

    private void OpenStage(StrategyAuthoringScreen stage, string status)
    {
        ActiveScreen = stage;
        WorkbenchTab = 3;
        if (!string.IsNullOrWhiteSpace(status)) Status = status;
    }

    partial void OnActiveScreenChanged(StrategyAuthoringScreen value)
    {
        // Request/Compare is the only tab shared by both screens. Selecting it here avoids a blank
        // workbench when Design hides the implementation-only Code, Parameters, and Activity tabs.
        WorkbenchTab = 3;
        NotifyAuthoringScreenStateChanged();
        if (_ready && !_restoring) Save();
    }

    private void RefreshAuthoringScreenGate()
    {
        if (GenerateCandidateFirst &&
            IsBuildScreen &&
            !IsNativeStrategyAgentWired &&
            !CanEnterFourLaneConformance)
        {
            ActiveScreen = StrategyAuthoringScreen.Design;
            Status = "The strategy request changed or lost confirmation. Review it again before implementation.";
            return;
        }

        NotifyAuthoringScreenStateChanged();
    }

    private void NotifyAuthoringScreenStateChanged()
    {
        OnPropertyChanged(nameof(IsDesignScreen));
        OnPropertyChanged(nameof(IsBuildScreen));
        OnPropertyChanged(nameof(IsBriefStage));
        OnPropertyChanged(nameof(IsResearchStage));
        OnPropertyChanged(nameof(IsChartDesignStage));
        OnPropertyChanged(nameof(IsBuildStage));
        OnPropertyChanged(nameof(IsValidateStage));
        OnPropertyChanged(nameof(IsPaperStage));
        OnPropertyChanged(nameof(WorkbenchGridColumn));
        OnPropertyChanged(nameof(WorkbenchGridColumnSpan));
        OnPropertyChanged(nameof(ShowImplementationTabs));
        OnPropertyChanged(nameof(ShowScreenNavigation));
        OnPropertyChanged(nameof(ShowDesignRequestHeader));
        OnPropertyChanged(nameof(ShowImplementationHeader));
        OnPropertyChanged(nameof(ShowNativeImplementationHeader));
        OnPropertyChanged(nameof(ShowResearchWorkspace));
        OnPropertyChanged(nameof(CanCompileCurrentSource));
        OnPropertyChanged(nameof(CanOpenDesignScreen));
        OnPropertyChanged(nameof(CanOpenBuildScreen));
        OnPropertyChanged(nameof(CanOpenBriefScreen));
        OnPropertyChanged(nameof(CanOpenResearchScreen));
        OnPropertyChanged(nameof(CanOpenValidateScreen));
        OnPropertyChanged(nameof(CanOpenPaperScreen));
        OnPropertyChanged(nameof(ShowDesignCandidateReview));
        OnPropertyChanged(nameof(ShowBuildGenerationProgress));
        OnPropertyChanged(nameof(ShowBuildBusyStop));
        OnPropertyChanged(nameof(ShowBuildCandidateResults));
        OnPropertyChanged(nameof(ShowCandidateEmptyState));
        OnPropertyChanged(nameof(ShowStartImplementationAction));
        OnPropertyChanged(nameof(ShowCliWorkspaceFooter));
        OnPropertyChanged(nameof(ShowNativeStrategyRunPanel));
        OnPropertyChanged(nameof(ShowLegacyCandidateBoundary));
        OnPropertyChanged(nameof(ActiveScreenTitle));
        OnPropertyChanged(nameof(ActiveScreenDescription));
        OnPropertyChanged(nameof(CandidateTabHeader));
        OnPropertyChanged(nameof(CandidateEmptyTitle));
        OnPropertyChanged(nameof(CandidateEmptyText));
        OpenDesignScreenCommand.NotifyCanExecuteChanged();
        OpenBuildScreenCommand.NotifyCanExecuteChanged();
        OpenBriefScreenCommand.NotifyCanExecuteChanged();
        OpenResearchScreenCommand.NotifyCanExecuteChanged();
        OpenValidateScreenCommand.NotifyCanExecuteChanged();
        OpenPaperScreenCommand.NotifyCanExecuteChanged();
    }

    private static StrategyWorkspaceStageV1 ToWorkspaceStage(StrategyAuthoringScreen screen) => screen switch
    {
        StrategyAuthoringScreen.Brief => StrategyWorkspaceStageV1.Brief,
        StrategyAuthoringScreen.Research => StrategyWorkspaceStageV1.Research,
        StrategyAuthoringScreen.Design => StrategyWorkspaceStageV1.Design,
        StrategyAuthoringScreen.Build => StrategyWorkspaceStageV1.Build,
        StrategyAuthoringScreen.Validate => StrategyWorkspaceStageV1.Validate,
        StrategyAuthoringScreen.Paper => StrategyWorkspaceStageV1.Paper,
        _ => StrategyWorkspaceStageV1.Brief,
    };

    partial void OnHasDetachedImplementationSourceChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCompileCurrentSource));
        CompileCommand.NotifyCanExecuteChanged();
    }
}
