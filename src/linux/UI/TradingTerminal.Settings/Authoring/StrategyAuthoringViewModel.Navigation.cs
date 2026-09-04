using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TradingTerminal.App.Authoring;

public enum StrategyAuthoringScreen
{
    Design = 0,
    Build = 1,
}

public sealed partial class StrategyAuthoringViewModel
{
    [ObservableProperty]
    private StrategyAuthoringScreen _activeScreen = StrategyAuthoringScreen.Design;

    [ObservableProperty]
    private bool _hasDetachedImplementationSource;

    public bool IsDesignScreen => ActiveScreen == StrategyAuthoringScreen.Design;
    public bool IsBuildScreen => ActiveScreen == StrategyAuthoringScreen.Build;

    public int WorkbenchGridColumn => IsDesignScreen ? 3 : 1;
    public int WorkbenchGridColumnSpan => IsDesignScreen ? 1 : 3;
    public bool ShowImplementationTabs => IsBuildScreen || !GenerateCandidateFirst || AuthoredUnitSpecification is not null;
    public bool ShowScreenNavigation => GenerateCandidateFirst;
    public bool ShowDesignRequestHeader => IsDesignScreen && GenerateCandidateFirst;
    public bool ShowImplementationHeader =>
        (IsBuildScreen && !ShowNativeStrategyRunPanel) || !GenerateCandidateFirst;
    public bool ShowNativeImplementationHeader => ShowNativeStrategyRunPanel;
    public bool HasAuthoredUnitCSharpFiles =>
        AuthoredUnitSpecification is not null &&
        Files.Count > 0 &&
        Files.All(static file => file.Name.EndsWith(".cs", StringComparison.OrdinalIgnoreCase));

    public bool CanCompileCurrentSource =>
        (HasExpertCSharpFiles || HasAuthoredUnitCSharpFiles) &&
        !HasDetachedImplementationSource &&
        !IsGenerating;

    public bool CanOpenDesignScreen => GenerateCandidateFirst && IsBuildScreen && !IsGenerating;
    public bool CanOpenBuildScreen =>
        IsDesignScreen &&
        (IsNativeStrategyAgentWired || CanEnterFourLaneConformance) &&
        !IsGenerating;

    public bool ShowDesignCandidateReview => IsDesignScreen && HasCandidate;
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
        : IsDesignScreen
            ? "Design & Confirm"
            : "Build, Test & Compare";

    public string ActiveScreenDescription => !GenerateCandidateFirst
        ? "Direct C# authoring is a separate legacy path; it does not inherit the confirmed Strategy Builder request or its lane results."
        : IsDesignScreen
            ? "Define the strategy in chat, review every material decision, then confirm the request."
            : ShowNativeStrategyRunPanel
                ? "Load one confirmed native run and inspect its retained Research, VibeQuant/AKQuant, CSP, and comparison evidence."
            : "Inspect generated artifacts, the validation actually available for each lane, exact failures, and any available test results.";

    public string CandidateTabHeader => IsDesignScreen ? "Request" : "Compare";

    public string CandidateEmptyTitle => IsDesignScreen
        ? "No strategy request yet"
        : "No implementation run yet";

    public string CandidateEmptyText => IsDesignScreen
        ? "Describe the idea in chat. The strategy meaning and required decisions will appear here for review."
        : "The confirmed strategy request is ready. Start implementation generation when you want the backend workers to run.";

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

        ActiveScreen = StrategyAuthoringScreen.Build;
        WorkbenchTab = 3;
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

        ActiveScreen = StrategyAuthoringScreen.Design;
        WorkbenchTab = 3;
        Status = "Design & Confirm is open. Review or revise the strategy request before returning to implementation.";
    }

    private bool CanOpenDesignScreenAction() => CanOpenDesignScreen;

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
        OnPropertyChanged(nameof(WorkbenchGridColumn));
        OnPropertyChanged(nameof(WorkbenchGridColumnSpan));
        OnPropertyChanged(nameof(ShowImplementationTabs));
        OnPropertyChanged(nameof(ShowScreenNavigation));
        OnPropertyChanged(nameof(ShowDesignRequestHeader));
        OnPropertyChanged(nameof(ShowImplementationHeader));
        OnPropertyChanged(nameof(ShowNativeImplementationHeader));
        OnPropertyChanged(nameof(CanCompileCurrentSource));
        OnPropertyChanged(nameof(CanOpenDesignScreen));
        OnPropertyChanged(nameof(CanOpenBuildScreen));
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
    }

    partial void OnHasDetachedImplementationSourceChanged(bool value)
    {
        OnPropertyChanged(nameof(CanCompileCurrentSource));
        CompileCommand.NotifyCanExecuteChanged();
    }
}
