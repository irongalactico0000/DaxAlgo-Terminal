using System.Text.RegularExpressions;

namespace TradingTerminal.Core.Strategies.Generation;

/// <summary>
/// One capture workflow with selectable outcome conditions — filters on the same
/// before-move capture path, not separate strategies.
/// </summary>
public sealed record ResearchCaptureConditionV1(
    string Id,
    string DisplayName,
    string ScanId,
    string ShortHint);

/// <summary>
/// ChatGPT-style follow-ups: 0 chips when idle; 1–3 after a turn that mentions
/// chart/indicator/capture/next-step work (including validation / Paper when ready).
/// </summary>
public static partial class ResearchSuggestionPlannerV1
{
    public const int MaxFollowUps = 3;

    public static IReadOnlyList<ResearchCaptureConditionV1> AllConditions { get; } =
    [
        new("before-jump", "Capture before +5% jump", "next-day-plus-5",
            "Observation on the bar before a ≥+5% next move"),
        new("before-crash", "Capture before −5% crash", "pre-crash",
            "Observation on the bar before a ≤−5% next move"),
        new("before-breakout", "Capture before breakout", "pre-breakout",
            "Tight range then ≥+3% — capture the pre-breakout window"),
    ];

    public sealed record TurnContext(
        string? LastUserText,
        int SampleCount,
        bool CanRunExperiment,
        bool HasExperimentEvidence,
        bool HasGalleryMatches,
        bool HasFocusedGalleryMatch,
        bool CanRunHistoricalValidation,
        bool CanOpenPaperScreen,
        bool IsRegistered);

    public static IReadOnlyList<ResearchQuickSuggestionV1> PlanForTurn(TurnContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var need = context.LastUserText?.Trim() ?? string.Empty;
        var suggestions = new List<ResearchQuickSuggestionV1>(MaxFollowUps);

        // Pipeline next-steps first when the workspace is already there (backtest/validate/Paper).
        if (context.SampleCount >= 4 &&
            context.CanRunExperiment &&
            !context.HasExperimentEvidence)
        {
            Add(suggestions, new ResearchQuickSuggestionV1(
                "run-experiment",
                "Run chronological experiment",
                "Next step: observation-only features from your labeled before-move samples",
                ResearchQuickSuggestionKindV1.RunResearchExperiment));
        }

        if (context.HasExperimentEvidence &&
            context.IsRegistered &&
            context.CanRunHistoricalValidation)
        {
            Add(suggestions, new ResearchQuickSuggestionV1(
                "run-validation",
                "Run historical validation",
                "Exact-hash replay for this registered build — the backtest-style gate before Paper",
                ResearchQuickSuggestionKindV1.RunHistoricalValidation));
        }

        if (context.CanOpenPaperScreen)
        {
            Add(suggestions, new ResearchQuickSuggestionV1(
                "paper-handoff",
                "Bind Paper book",
                "Validation is bound — hand this revision to Simulated Paper",
                ResearchQuickSuggestionKindV1.OpenPaperHandoff));
        }

        // Chart / indicator / capture intents from this user turn only.
        if (!string.IsNullOrWhiteSpace(need))
        {
            foreach (var condition in MatchConditions(need))
            {
                Add(suggestions, new ResearchQuickSuggestionV1(
                    "capture-" + condition.Id,
                    condition.DisplayName,
                    condition.ShortHint,
                    ResearchQuickSuggestionKindV1.AutoCollectLocalGallery,
                    ScanId: condition.ScanId));
            }

            if (MentionsIndicators(need) || MentionsFamousCatalog(need))
            {
                Add(suggestions, new ResearchQuickSuggestionV1(
                    "apply-ema-rsi",
                    "Apply EMA + RSI",
                    "Open the chart with EMA and RSI overlays",
                    ResearchQuickSuggestionKindV1.SendPrompt,
                    Prompt: "chart with EMA and RSI"));
            }

            if (MentionsFamousCatalog(need))
            {
                Add(suggestions, new ResearchQuickSuggestionV1(
                    "famous",
                    "List famous indicators",
                    "Host catalog — reply with names or numbers",
                    ResearchQuickSuggestionKindV1.SendPrompt,
                    Prompt: "show famous indicators"));
            }

            if (context.HasGalleryMatches &&
                (MatchConditions(need).Count > 0 || MentionsGallery(need)))
            {
                Add(suggestions, new ResearchQuickSuggestionV1(
                    "gallery-first",
                    "Open gallery hit #1",
                    "Focus the top gallery event in one Charts window",
                    ResearchQuickSuggestionKindV1.FocusFirstGalleryMatch));
            }
        }

        if (context.HasFocusedGalleryMatch && suggestions.Count < MaxFollowUps)
        {
            Add(suggestions, new ResearchQuickSuggestionV1(
                "indicators-on-focus",
                "Show indicators on focused chart",
                "EMA + RSI + ATR on the selected observation window",
                ResearchQuickSuggestionKindV1.SendPrompt,
                Prompt: "chart with EMA RSI ATR"));
        }

        return suggestions;
    }

    /// <summary>Legacy name — prefer <see cref="PlanForTurn"/>.</summary>
    public static IReadOnlyList<ResearchQuickSuggestionV1> Plan(
        int sampleCount,
        bool canRunExperiment,
        bool hasExperimentEvidence,
        bool hasFocusedGalleryMatch,
        string? composerOrNeedText) =>
        PlanForTurn(new TurnContext(
            composerOrNeedText,
            sampleCount,
            canRunExperiment,
            hasExperimentEvidence,
            HasGalleryMatches: false,
            hasFocusedGalleryMatch,
            CanRunHistoricalValidation: false,
            CanOpenPaperScreen: false,
            IsRegistered: false));

    public static IReadOnlyList<ResearchCaptureConditionV1> MatchConditions(string needText)
    {
        if (string.IsNullOrWhiteSpace(needText))
            return Array.Empty<ResearchCaptureConditionV1>();

        var normalized = needText.Trim().ToLowerInvariant();
        var hits = new List<ResearchCaptureConditionV1>();

        if (JumpPattern().IsMatch(normalized) ||
            normalized.Contains("슈팅", StringComparison.Ordinal) ||
            (normalized.Contains("돌파", StringComparison.Ordinal) &&
             !normalized.Contains("breakout", StringComparison.Ordinal)))
            hits.Add(AllConditions[0]);

        if (CrashPattern().IsMatch(normalized) ||
            normalized.Contains("떡락", StringComparison.Ordinal) ||
            normalized.Contains("crash", StringComparison.Ordinal) ||
            normalized.Contains("dump", StringComparison.Ordinal))
            hits.Add(AllConditions[1]);

        if (BreakoutPattern().IsMatch(normalized) ||
            normalized.Contains("breakout", StringComparison.Ordinal) ||
            normalized.Contains("pre-breakout", StringComparison.Ordinal))
            hits.Add(AllConditions[2]);

        return hits
            .GroupBy(static c => c.Id, StringComparer.Ordinal)
            .Select(static g => g.First())
            .ToArray();
    }

    private static void Add(List<ResearchQuickSuggestionV1> target, ResearchQuickSuggestionV1 item)
    {
        if (target.Count >= MaxFollowUps) return;
        if (target.Any(existing => string.Equals(existing.Id, item.Id, StringComparison.Ordinal)))
            return;
        target.Add(item);
    }

    private static bool MentionsIndicators(string need) =>
        need.Contains("rsi", StringComparison.OrdinalIgnoreCase) ||
        need.Contains("ema", StringComparison.OrdinalIgnoreCase) ||
        need.Contains("atr", StringComparison.OrdinalIgnoreCase) ||
        need.Contains("macd", StringComparison.OrdinalIgnoreCase) ||
        need.Contains("indicator", StringComparison.OrdinalIgnoreCase) ||
        need.Contains("지표", StringComparison.Ordinal);

    private static bool MentionsFamousCatalog(string need) =>
        need.Contains("famous", StringComparison.OrdinalIgnoreCase) ||
        need.Contains("catalog", StringComparison.OrdinalIgnoreCase) ||
        need.Contains("지표 목록", StringComparison.Ordinal);

    private static bool MentionsGallery(string need) =>
        need.Contains("gallery", StringComparison.OrdinalIgnoreCase) ||
        need.Contains("similar", StringComparison.OrdinalIgnoreCase) ||
        need.Contains("setup", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"(\+\s*5\s*%|jump|rally|shoot|before\s+up)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JumpPattern();

    [GeneratedRegex(@"(\-\s*5\s*%|crash|dump|drop|before\s+down)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CrashPattern();

    [GeneratedRegex(@"(break\s*out|pre[\s\-]?breakout|tight\s+range)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakoutPattern();
}
