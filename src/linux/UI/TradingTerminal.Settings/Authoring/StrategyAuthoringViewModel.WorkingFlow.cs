namespace TradingTerminal.App.Authoring;

/// <summary>
/// Always-visible map of the path that actually runs a strategy on Mac:
/// Brief → Build/Register → Historical Validate → Paper → Harness
/// (optional Lane 3 export is software install, not part of the run path).
/// </summary>
public sealed partial class StrategyAuthoringViewModel
{
    public bool ShowWorkingFlowMap => GenerateCandidateFirst;

    /// <summary>
    /// How a strategy enters this Mac. OMS/Harness only run a unit that already exists.
    /// </summary>
    public string HowStrategyGetsInText =>
        "MAKE a strategy here: type the idea in Brief → AI writes C# → Compile & Register. " +
        "OR Charts: place STOP/TARGET → Send draft. " +
        "OR someone already made one: they send a .daxalgostrategy file → Strategy Manager → Install open package… (skip generate).";

    /// <summary>One-line map with checkmarks for completed gates.</summary>
    public string WorkingFlowMapText
    {
        get
        {
            var brief = HasCandidate || AuthoredUnitSpecification is not null ||
                        StrategyWorkspace.Bindings.BriefHashSha256 is not null
                ? "① Brief ✓"
                : "① Brief";
            var build = IsRegistered || StrategyWorkspace.Bindings.BuildArtifactHashSha256 is not null
                ? "② Build·Register ✓"
                : "② Build·Register";
            var validate = HasHistoricalValidationEvidence
                ? "③ Historical Validate ✓"
                : "③ Historical Validate";
            var paper = StrategyWorkspace.Bindings.PaperBindingHashSha256 is not null
                ? "④ Paper book ✓"
                : "④ Paper book";
            var harness = StrategyWorkspace.Bindings.PaperBindingHashSha256 is not null
                ? "⑤ Harness ✓"
                : "⑤ Harness";
            return $"{brief} → {build} → {validate} → {paper} → {harness}";
        }
    }

    /// <summary>What the operator should do next on this working path.</summary>
    public string WorkingFlowNextActionText
    {
        get
        {
            if (!GenerateCandidateFirst)
                return "Expert C# path: Compile & Register, then use Validate / Paper when available.";

            if (IsGenerating)
                return "Wait for generation to finish, then continue the map above.";

            if (!(IsRegistered || StrategyWorkspace.Bindings.BuildArtifactHashSha256 is not null))
            {
                if (AuthoredUnitSpecification is null && !HasCandidate)
                    return "Next: finish Brief (confirm request) → open Build → generate → Compile & Register.";
                return "Next: open Build → Compile & Register this unit (that unlocks Validate).";
            }

            if (!HasHistoricalValidationEvidence)
                return "Next: open Validate → Run historical validation (exact revision). Optional: Export open package… for another Mac.";

            if (StrategyWorkspace.Bindings.PaperBindingHashSha256 is null)
                return "Next: open Paper → Bind selected Paper book → Harness (your book only; not live money).";

            return "Path complete for this revision: Harness can run Paper. Optional: Export open package… → Strategy Manager → Install.";
        }
    }

    /// <summary>Where you are in the UI vs the working path.</summary>
    public string WorkingFlowYouAreHereText => ActiveScreen switch
    {
        StrategyAuthoringScreen.Brief => "You are here: Brief (define what to build).",
        StrategyAuthoringScreen.Research => "You are here: Research (optional discovery) — not required for every strategy.",
        StrategyAuthoringScreen.Design => "You are here: Chart Design (optional) — hiding layers does not change rules.",
        StrategyAuthoringScreen.Build => "You are here: Build — generate / compile / register so Validate can run.",
        StrategyAuthoringScreen.Validate => "You are here: Validate — historical proof for this exact revision.",
        StrategyAuthoringScreen.Paper => "You are here: Paper → Harness handoff (run on your Paper book).",
        _ => "You are here: Strategy Builder.",
    };

    private void NotifyWorkingFlowMapChanged()
    {
        OnPropertyChanged(nameof(ShowWorkingFlowMap));
        OnPropertyChanged(nameof(HowStrategyGetsInText));
        OnPropertyChanged(nameof(WorkingFlowMapText));
        OnPropertyChanged(nameof(WorkingFlowNextActionText));
        OnPropertyChanged(nameof(WorkingFlowYouAreHereText));
    }
}
