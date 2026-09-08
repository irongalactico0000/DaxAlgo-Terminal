namespace TradingTerminal.Core.Strategies.Generation;

/// <summary>
/// One clickable research/chat suggestion shown under the Strategy Builder composer.
/// </summary>
public sealed record ResearchQuickSuggestionV1(
    string Id,
    string Title,
    string Hint,
    ResearchQuickSuggestionKindV1 Kind,
    string? Prompt = null,
    string? ScanId = null);

public enum ResearchQuickSuggestionKindV1
{
    /// <summary>Fill composer and send through the normal host catalog path.</summary>
    SendPrompt,

    /// <summary>Scan local Simulated history and auto-label gallery hits for capture.</summary>
    AutoCollectLocalGallery,
}
