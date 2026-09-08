using System.Text.RegularExpressions;

namespace TradingTerminal.Core.Strategies.Generation;

/// <summary>
/// One capture workflow with selectable outcome conditions. Not separate strategies —
/// Dolpago-style “capture state before the move,” parameterized by what the user needs.
/// </summary>
public sealed record ResearchCaptureConditionV1(
    string Id,
    string DisplayName,
    string ScanId,
    string ShortHint);

/// <summary>
/// Builds composer chips from current workspace state + optional free-text need.
/// Only surfaces conditions that match; the primary chip is always the next action.
/// </summary>
public static partial class ResearchSuggestionPlannerV1
{
    public static IReadOnlyList<ResearchCaptureConditionV1> AllConditions { get; } =
    [
        new("before-jump", "Before +5% jump", "next-day-plus-5",
            "Capture observation on the bar before a ≥+5% next move"),
        new("before-crash", "Before −5% crash", "pre-crash",
            "Capture observation on the bar before a ≤−5% next move"),
        new("before-breakout", "Before breakout", "pre-breakout",
            "Capture observation after a tight range before a ≥+3% move"),
    ];

    public static IReadOnlyList<ResearchQuickSuggestionV1> Plan(
        int sampleCount,
        bool canRunExperiment,
        bool hasExperimentEvidence,
        bool hasFocusedGalleryMatch,
        string? composerOrNeedText)
    {
        var suggestions = new List<ResearchQuickSuggestionV1>(6);
        var need = composerOrNeedText?.Trim() ?? string.Empty;

        // Next action first: experiment when the capture loop already has enough samples.
        if (sampleCount >= 4 && canRunExperiment && !hasExperimentEvidence)
        {
            suggestions.Add(new ResearchQuickSuggestionV1(
                "run-experiment",
                "▶ Run chronological experiment",
                "One workflow next step: observation-only features from your labeled before-move samples",
                ResearchQuickSuggestionKindV1.RunResearchExperiment));
        }

        var matched = MatchConditions(need);
        if (matched.Count == 0)
        {
            // No specific need yet → one default capture path (generalized strategy entry).
            var primary = AllConditions[0];
            suggestions.Add(ToCaptureChip(primary, isPrimary: true));
            if (string.IsNullOrWhiteSpace(need))
            {
                // Soft alternatives only when idle — user can type a need to filter further.
                suggestions.Add(ToCaptureChip(AllConditions[1], isPrimary: false));
                suggestions.Add(ToCaptureChip(AllConditions[2], isPrimary: false));
            }
        }
        else
        {
            // User expressed conditions → only those chips (still one capture workflow).
            for (var i = 0; i < matched.Count; i++)
                suggestions.Add(ToCaptureChip(matched[i], isPrimary: i == 0));
        }

        if (hasFocusedGalleryMatch || MentionsIndicators(need))
        {
            suggestions.Add(new ResearchQuickSuggestionV1(
                "indicators-on-focus",
                "Show indicators on focused chart",
                "EMA + RSI + ATR on the selected before-move observation (scores already on the box)",
                ResearchQuickSuggestionKindV1.SendPrompt,
                Prompt: "chart with EMA RSI ATR"));
        }

        if (MentionsFamousCatalog(need))
        {
            suggestions.Add(new ResearchQuickSuggestionV1(
                "famous",
                "Famous indicators list",
                "Host catalog overlays — pick by number",
                ResearchQuickSuggestionKindV1.SendPrompt,
                Prompt: "show famous indicators"));
        }

        return suggestions;
    }

    public static IReadOnlyList<ResearchCaptureConditionV1> MatchConditions(string needText)
    {
        if (string.IsNullOrWhiteSpace(needText))
            return Array.Empty<ResearchCaptureConditionV1>();

        var normalized = needText.Trim().ToLowerInvariant();
        var hits = new List<ResearchCaptureConditionV1>();

        if (JumpPattern().IsMatch(normalized) ||
            normalized.Contains("슈팅", StringComparison.Ordinal) ||
            normalized.Contains("돌파", StringComparison.Ordinal) && !normalized.Contains("breakout", StringComparison.Ordinal))
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

        // De-dupe while preserving order.
        return hits
            .GroupBy(static c => c.Id, StringComparer.Ordinal)
            .Select(static g => g.First())
            .ToArray();
    }

    private static ResearchQuickSuggestionV1 ToCaptureChip(ResearchCaptureConditionV1 condition, bool isPrimary) =>
        new(
            "capture-" + condition.Id,
            isPrimary ? "▶ " + condition.DisplayName : condition.DisplayName,
            condition.ShortHint + " — same capture workflow, different outcome condition",
            ResearchQuickSuggestionKindV1.AutoCollectLocalGallery,
            ScanId: condition.ScanId);

    private static bool MentionsIndicators(string need) =>
        !string.IsNullOrWhiteSpace(need) &&
        (need.Contains("rsi", StringComparison.OrdinalIgnoreCase) ||
         need.Contains("ema", StringComparison.OrdinalIgnoreCase) ||
         need.Contains("atr", StringComparison.OrdinalIgnoreCase) ||
         need.Contains("macd", StringComparison.OrdinalIgnoreCase) ||
         need.Contains("indicator", StringComparison.OrdinalIgnoreCase) ||
         need.Contains("지표", StringComparison.Ordinal));

    private static bool MentionsFamousCatalog(string need) =>
        !string.IsNullOrWhiteSpace(need) &&
        (need.Contains("famous", StringComparison.OrdinalIgnoreCase) ||
         need.Contains("catalog", StringComparison.OrdinalIgnoreCase) ||
         need.Contains("지표 목록", StringComparison.Ordinal));

    [GeneratedRegex(@"(\+\s*5\s*%|jump|rally|shoot|before\s+up)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex JumpPattern();

    [GeneratedRegex(@"(\-\s*5\s*%|crash|dump|drop|before\s+down)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CrashPattern();

    [GeneratedRegex(@"(break\s*out|pre[\s\-]?breakout|tight\s+range)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BreakoutPattern();
}
