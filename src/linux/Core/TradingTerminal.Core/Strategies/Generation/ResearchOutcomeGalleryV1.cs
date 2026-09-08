using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;

namespace TradingTerminal.Core.Strategies.Generation;

/// <summary>
/// Host research gallery: scan known S&amp;P-universe daily history for labeled outcome events
/// (next-day +5%, pre-crash, simple pre-breakout). Results are research evidence only — not Paper unlocks.
/// </summary>
public sealed record ResearchOutcomeGalleryScanRequestV1(
    string ScanId,
    int LookbackBars = 1_500,
    int MaxResults = 12,
    bool HydrateMissingHistory = true,
    int MaxRemoteHydrations = 40,
    BrokerKind? PreferredSource = null);

public sealed record ResearchOutcomeGalleryMatchV1(
    InstrumentId InstrumentId,
    string CanonicalSymbol,
    string CompanyName,
    BarSize Timeframe,
    BrokerKind Source,
    DateTime ObservationFromUtc,
    DateTime ObservationToUtcExclusive,
    DateTime OutcomeFromUtc,
    DateTime OutcomeToUtcExclusive,
    double OutcomeReturn,
    string LabelHint);

public sealed record ResearchOutcomeGalleryResultV1(
    string ScanId,
    string DisplayName,
    IReadOnlyList<ResearchOutcomeGalleryMatchV1> Matches,
    int InstrumentsScanned,
    int InstrumentsWithEnoughHistory,
    string Explanation,
    int RemoteHydrationAttempts = 0,
    int RemoteHydrationSuccesses = 0,
    int RemoteHydrationFailures = 0);

public interface IResearchOutcomeGalleryScanV1
{
    Task<ResearchOutcomeGalleryResultV1> ScanAsync(
        ResearchOutcomeGalleryScanRequestV1 request,
        CancellationToken cancellationToken = default);
}

/// <summary>Pure deterministic event finder shared by the store-backed scan and contract tests.</summary>
public static class ResearchOutcomeEventFinderV1
{
    public const double NextDayPlusFiveThreshold = 0.05;
    public const double PreCrashThreshold = -0.05;
    public const double PreBreakoutOutcomeThreshold = 0.03;
    public const double PreBreakoutPriorRangeMax = 0.02;
    public const int PreBreakoutLookback = 5;
    public const int MinimumBars = 8;

    public static bool TryDescribeScan(string scanId, out string displayName, out string explanationPrefix)
    {
        switch (scanId)
        {
            case "next-day-plus-5":
                displayName = "Next-day +5% gallery";
                explanationPrefix = "Daily closes with next-day return ≥ +5%";
                return true;
            case "pre-crash":
                displayName = "Pre-crash windows";
                explanationPrefix = "Daily closes with next-day return ≤ −5%";
                return true;
            case "pre-breakout":
                displayName = "Pre-breakout windows";
                explanationPrefix =
                    $"Prior {PreBreakoutLookback}-day range ≤ {PreBreakoutPriorRangeMax:P0} then next-day return ≥ +{PreBreakoutOutcomeThreshold:P0}";
                return true;
            default:
                displayName = string.Empty;
                explanationPrefix = string.Empty;
                return false;
        }
    }

    public static IReadOnlyList<ResearchOutcomeGalleryMatchV1> FindEvents(
        string scanId,
        InstrumentId instrumentId,
        string canonicalSymbol,
        string companyName,
        IReadOnlyList<OhlcvBar> bars)
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (bars.Count < MinimumBars)
            return Array.Empty<ResearchOutcomeGalleryMatchV1>();

        return scanId switch
        {
            "next-day-plus-5" => FindNextDayReturnEvents(
                instrumentId, canonicalSymbol, companyName, bars,
                minReturnInclusive: NextDayPlusFiveThreshold,
                maxReturnInclusive: null,
                labelHint: "next-day +5%"),
            "pre-crash" => FindNextDayReturnEvents(
                instrumentId, canonicalSymbol, companyName, bars,
                minReturnInclusive: null,
                maxReturnInclusive: PreCrashThreshold,
                labelHint: "next-day ≤ −5%"),
            "pre-breakout" => FindPreBreakoutEvents(instrumentId, canonicalSymbol, companyName, bars),
            _ => Array.Empty<ResearchOutcomeGalleryMatchV1>(),
        };
    }

    private static IReadOnlyList<ResearchOutcomeGalleryMatchV1> FindNextDayReturnEvents(
        InstrumentId instrumentId,
        string canonicalSymbol,
        string companyName,
        IReadOnlyList<OhlcvBar> bars,
        double? minReturnInclusive,
        double? maxReturnInclusive,
        string labelHint)
    {
        var matches = new List<ResearchOutcomeGalleryMatchV1>();
        for (var i = 0; i < bars.Count - 1; i++)
        {
            var setup = bars[i];
            var outcome = bars[i + 1];
            if (setup.Close <= 0 || !double.IsFinite(setup.Close) || !double.IsFinite(outcome.Close))
                continue;

            var ret = outcome.Close / setup.Close - 1.0;
            if (!double.IsFinite(ret))
                continue;
            if (minReturnInclusive is { } min && ret < min)
                continue;
            if (maxReturnInclusive is { } max && ret > max)
                continue;

            matches.Add(CreateMatch(
                instrumentId, canonicalSymbol, companyName, setup, outcome, ret, labelHint, bars, i));
        }

        return matches;
    }

    private static IReadOnlyList<ResearchOutcomeGalleryMatchV1> FindPreBreakoutEvents(
        InstrumentId instrumentId,
        string canonicalSymbol,
        string companyName,
        IReadOnlyList<OhlcvBar> bars)
    {
        var matches = new List<ResearchOutcomeGalleryMatchV1>();
        for (var i = PreBreakoutLookback - 1; i < bars.Count - 1; i++)
        {
            var setup = bars[i];
            var outcome = bars[i + 1];
            if (setup.Close <= 0 || !double.IsFinite(setup.Close) || !double.IsFinite(outcome.Close))
                continue;

            var windowStart = i - (PreBreakoutLookback - 1);
            var high = double.NegativeInfinity;
            var low = double.PositiveInfinity;
            for (var j = windowStart; j <= i; j++)
            {
                high = Math.Max(high, bars[j].High);
                low = Math.Min(low, bars[j].Low);
            }

            if (!double.IsFinite(high) || !double.IsFinite(low) || setup.Close <= 0)
                continue;
            var priorRange = (high - low) / setup.Close;
            if (priorRange > PreBreakoutPriorRangeMax)
                continue;

            var ret = outcome.Close / setup.Close - 1.0;
            if (!double.IsFinite(ret) || ret < PreBreakoutOutcomeThreshold)
                continue;

            matches.Add(CreateMatch(
                instrumentId, canonicalSymbol, companyName, setup, outcome, ret,
                "pre-breakout (tight range → +3%+)", bars, i));
        }

        return matches;
    }

    private static ResearchOutcomeGalleryMatchV1 CreateMatch(
        InstrumentId instrumentId,
        string canonicalSymbol,
        string companyName,
        OhlcvBar setup,
        OhlcvBar outcome,
        double ret,
        string labelHint,
        IReadOnlyList<OhlcvBar> bars,
        int setupIndex)
    {
        var outcomeEnd = setupIndex + 2 < bars.Count
            ? bars[setupIndex + 2].OpenTimeUtc
            : outcome.OpenTimeUtc.AddDays(1);

        return new ResearchOutcomeGalleryMatchV1(
            instrumentId,
            canonicalSymbol,
            companyName,
            setup.Size,
            setup.Source,
            setup.OpenTimeUtc,
            outcome.OpenTimeUtc,
            outcome.OpenTimeUtc,
            outcomeEnd,
            ret,
            labelHint);
    }
}
