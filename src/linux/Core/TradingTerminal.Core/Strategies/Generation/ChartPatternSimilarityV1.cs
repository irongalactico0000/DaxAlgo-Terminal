using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;

namespace TradingTerminal.Core.Strategies.Generation;

/// <summary>
/// A scale-free path read from a reference chart. Values describe shape only; they are not prices
/// and must not be interpreted as a forecast. Image inspection produces an approximate path while
/// a future native DaxAlgo chart parser can supply an exact path from embedded bars.
/// </summary>
public sealed record ChartPatternFingerprintV1(
    IReadOnlyList<double> NormalizedClosePath,
    IReadOnlyList<double>? NormalizedVolumePath = null);

public enum ChartPatternCandidateScopeV1
{
    AllKnownInstruments,
    IndexesOnly,
    SameAssetClass,
}

/// <summary>The bounded request executed against real persisted OHLCV history.</summary>
public sealed record ChartPatternSearchRequestV1(
    string ReferenceId,
    string ReferenceContentHashSha256,
    ChartPatternFingerprintV1 Fingerprint,
    BarSize Timeframe,
    int CandidateBarCount = 120,
    int MaxResults = 8,
    ChartPatternCandidateScopeV1 Scope = ChartPatternCandidateScopeV1.AllKnownInstruments,
    AssetClass? ReferenceAssetClass = null,
    InstrumentId? ExcludeInstrumentId = null,
    BrokerKind? PreferredSource = null,
    bool HydrateMissingHistory = true,
    int MaxRemoteHydrations = 40);

/// <summary>
/// One evidence-backed match. The component values are exposed so the UI never hides a weak match
/// behind one opaque score. Similarity is descriptive and carries no expected-return claim.
/// </summary>
public sealed record ChartPatternMatchV1(
    InstrumentId InstrumentId,
    string CanonicalSymbol,
    AssetClass AssetClass,
    string Exchange,
    BarSize Timeframe,
    BrokerKind Source,
    int BarCount,
    DateTime FromUtc,
    DateTime ToUtc,
    double Score,
    double ShapeSimilarity,
    double ReturnCorrelation,
    double VolatilitySimilarity,
    double DrawdownSimilarity);

public sealed record ChartPatternSearchResultV1(
    string ReferenceId,
    string ReferenceContentHashSha256,
    BarSize Timeframe,
    ChartPatternCandidateScopeV1 Scope,
    IReadOnlyList<ChartPatternMatchV1> Matches,
    int InstrumentsScanned,
    int InstrumentsWithEnoughHistory,
    string Explanation,
    int RemoteHydrationAttempts = 0,
    int RemoteHydrationSuccesses = 0,
    int RemoteHydrationFailures = 0,
    bool RemoteHydrationLimitReached = false);

/// <summary>The user's explicit choice from a real-history result set.</summary>
public sealed record ChartPatternSelectionV1(
    string ReferenceId,
    string ReferenceContentHashSha256,
    ChartPatternMatchV1 Match);

public interface IChartPatternSearchV1
{
    Task<ChartPatternSearchResultV1> SearchAsync(
        ChartPatternSearchRequestV1 request,
        CancellationToken cancellationToken = default);
}

/// <summary>Pure deterministic scoring shared by the store-backed search and its contract tests.</summary>
public static class ChartPatternSimilarityCalculatorV1
{
    public const int MinimumPathPoints = 8;
    public const int MaximumPathPoints = 64;

    public static void ValidateFingerprint(ChartPatternFingerprintV1 fingerprint)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        var closes = fingerprint.NormalizedClosePath;
        if (closes is null || closes.Count is < MinimumPathPoints or > MaximumPathPoints)
            throw new ArgumentException(
                $"A chart pattern requires {MinimumPathPoints} to {MaximumPathPoints} close-path points.",
                nameof(fingerprint));
        if (closes.Any(static value => !double.IsFinite(value)))
            throw new ArgumentException("Chart-pattern close values must be finite.", nameof(fingerprint));
        if (Range(closes) <= 1e-12)
            throw new ArgumentException("A flat close path cannot identify a chart pattern.", nameof(fingerprint));

        if (fingerprint.NormalizedVolumePath is { } volumes)
        {
            if (volumes.Count != closes.Count)
                throw new ArgumentException("Close and volume paths must have the same point count.", nameof(fingerprint));
            if (volumes.Any(static value => !double.IsFinite(value) || value < 0))
                throw new ArgumentException("Chart-pattern volume values must be finite and non-negative.", nameof(fingerprint));
        }
    }

    public static ChartPatternScoreV1 Score(
        ChartPatternFingerprintV1 fingerprint,
        IReadOnlyList<OhlcvBar> candidateBars)
    {
        ValidateFingerprint(fingerprint);
        ArgumentNullException.ThrowIfNull(candidateBars);
        if (candidateBars.Count < MinimumPathPoints)
            throw new ArgumentException(
                $"Candidate history requires at least {MinimumPathPoints} bars.",
                nameof(candidateBars));

        var reference = ZScore(fingerprint.NormalizedClosePath);
        var candidate = ZScore(Resample(candidateBars.Select(static bar => bar.Close).ToArray(), reference.Length));
        var rmse = Math.Sqrt(reference.Zip(candidate, static (left, right) =>
            (left - right) * (left - right)).Average());
        var shape = Math.Exp(-rmse);

        var referenceReturns = Differences(reference);
        var candidateReturns = Differences(candidate);
        var correlation = Correlation(referenceReturns, candidateReturns);
        var positiveCorrelation = Math.Clamp((correlation + 1d) * 0.5d, 0d, 1d);

        var referenceVolatility = StandardDeviation(referenceReturns);
        var candidateVolatility = StandardDeviation(candidateReturns);
        var volatility = RatioSimilarity(referenceVolatility, candidateVolatility);

        var referenceDrawdown = MaxDrawdownFromNormalizedPath(reference);
        var candidateDrawdown = MaxDrawdownFromNormalizedPath(candidate);
        var drawdown = Math.Exp(-Math.Abs(referenceDrawdown - candidateDrawdown));

        var score = 100d * ((0.55d * shape) + (0.25d * positiveCorrelation) +
                            (0.10d * volatility) + (0.10d * drawdown));
        return new ChartPatternScoreV1(
            Round(score), Round(shape), Round(correlation), Round(volatility), Round(drawdown));
    }

    private static double[] Resample(IReadOnlyList<double> values, int count)
    {
        if (values.Count == count) return values.ToArray();
        var result = new double[count];
        var scale = (values.Count - 1d) / (count - 1d);
        for (var index = 0; index < count; index++)
        {
            var position = index * scale;
            var left = (int)Math.Floor(position);
            var right = Math.Min(left + 1, values.Count - 1);
            var fraction = position - left;
            result[index] = values[left] + ((values[right] - values[left]) * fraction);
        }
        return result;
    }

    private static double[] ZScore(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        var deviation = StandardDeviation(values);
        if (deviation <= 1e-12)
            throw new ArgumentException("A flat series cannot be compared as a chart pattern.", nameof(values));
        return values.Select(value => (value - mean) / deviation).ToArray();
    }

    private static double[] Differences(IReadOnlyList<double> values)
    {
        var result = new double[values.Count - 1];
        for (var index = 1; index < values.Count; index++) result[index - 1] = values[index] - values[index - 1];
        return result;
    }

    private static double Correlation(IReadOnlyList<double> left, IReadOnlyList<double> right)
    {
        var leftMean = left.Average();
        var rightMean = right.Average();
        var numerator = 0d;
        var leftSquared = 0d;
        var rightSquared = 0d;
        for (var index = 0; index < left.Count; index++)
        {
            var leftDelta = left[index] - leftMean;
            var rightDelta = right[index] - rightMean;
            numerator += leftDelta * rightDelta;
            leftSquared += leftDelta * leftDelta;
            rightSquared += rightDelta * rightDelta;
        }
        var denominator = Math.Sqrt(leftSquared * rightSquared);
        return denominator <= 1e-12 ? 0d : Math.Clamp(numerator / denominator, -1d, 1d);
    }

    private static double StandardDeviation(IReadOnlyList<double> values)
    {
        var mean = values.Average();
        return Math.Sqrt(values.Select(value => (value - mean) * (value - mean)).Average());
    }

    private static double RatioSimilarity(double left, double right)
    {
        if (left <= 1e-12 || right <= 1e-12) return left <= 1e-12 && right <= 1e-12 ? 1d : 0d;
        return Math.Exp(-Math.Abs(Math.Log(left / right)));
    }

    private static double MaxDrawdownFromNormalizedPath(IReadOnlyList<double> values)
    {
        var peak = values[0];
        var maximum = 0d;
        for (var index = 1; index < values.Count; index++)
        {
            peak = Math.Max(peak, values[index]);
            maximum = Math.Max(maximum, peak - values[index]);
        }
        return maximum;
    }

    private static double Range(IReadOnlyList<double> values) => values.Max() - values.Min();
    private static double Round(double value) => Math.Round(value, 6, MidpointRounding.AwayFromZero);
}

public sealed record ChartPatternScoreV1(
    double Score,
    double ShapeSimilarity,
    double ReturnCorrelation,
    double VolatilitySimilarity,
    double DrawdownSimilarity);
