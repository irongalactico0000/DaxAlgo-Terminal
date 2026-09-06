using System.Globalization;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.Infrastructure.Research;

/// <summary>
/// Reads only each sample's observation interval from the local store, fits normalization on the
/// chronological training partition, and emits deterministic exploratory feature evidence.
/// </summary>
public sealed class StrategyResearchExperimentRunnerV1(IMarketDataStore store) : IResearchExperimentRunnerV1
{
    private static readonly ResearchFeatureDefinitionV1[] FeatureDefinitions =
    [
        new("bar_return", "bars", "Close-to-close return across the observation window."),
        new("bar_realized_volatility", "bars", "Root sum of squared bar log returns."),
        new("bar_range_bps", "bars", "Observed high-low range relative to the first close, in basis points."),
        new("log_bar_volume", "bars", "Natural log of one plus observed bar volume."),
        new("quote_spread_bps", "l1", "Mean quoted spread relative to mid, in basis points."),
        new("l1_queue_imbalance", "l1", "Mean top-of-book size imbalance."),
        new("trade_imbalance", "trade_tape", "Buyer- versus seller-initiated trade-count imbalance."),
        new("log_trade_volume", "trade_tape", "Natural log of one plus printed trade volume."),
        new("depth_imbalance_3", "depth", "Mean cumulative book imbalance across the top three levels."),
        new("depth_imbalance_10", "depth", "Mean cumulative book imbalance across the top ten levels."),
        new("log_visible_depth", "depth", "Natural log of one plus mean visible top-ten depth."),
    ];

    public async Task<ResearchExperimentEvidenceV1> RunAsync(
        ResearchDatasetDefinitionV1 dataset,
        CancellationToken cancellationToken = default)
    {
        ResearchDatasetValidatorV1.RequireStructurallyValid(dataset);
        if (dataset.Samples.Count < 4)
            throw new InvalidOperationException("At least four labeled event samples are required for chronological train/validation/test evidence.");

        var extracted = new List<ExtractedSample>(dataset.Samples.Count);
        foreach (var sample in dataset.Samples
                     .OrderBy(item => item.Selection.ObservationToUtc)
                     .ThenBy(item => item.EventSampleId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            extracted.Add(await ExtractAsync(sample, cancellationToken).ConfigureAwait(false));
        }

        var split = Split(extracted);
        var transforms = FitTransforms(extracted.Take(split.TrainingCount).ToArray());
        var normalized = extracted.Select(sample => Normalize(sample, transforms)).ToArray();
        var correlations = Correlations(normalized.Take(split.TrainingCount).ToArray());
        var selected = correlations
            .OrderByDescending(item => Math.Abs(item.Correlation))
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .Take(6)
            .ToArray();
        var denominator = selected.Sum(item => Math.Abs(item.Correlation));
        var weights = selected
            .Select(item => new ResearchFeatureWeightV1(
                item.Name,
                Round(item.Correlation),
                Round(denominator <= double.Epsilon ? 0 : item.Correlation / denominator)))
            .ToArray();
        var formula = weights.Length == 0
            ? "score = 0"
            : "score = " + string.Join(" ", weights.Select((weight, index) =>
                string.Create(CultureInfo.InvariantCulture,
                    $"{(index == 0 ? weight.FormulaWeight < 0 ? "-" : string.Empty : weight.FormulaWeight >= 0 ? "+ " : "- ")}{Math.Abs(weight.FormulaWeight):0.######}·z({weight.FeatureName})")));

        var evidence = new ResearchExperimentEvidenceV1(
            ResearchExperimentEvidenceV1.CurrentSchemaVersion,
            ResearchDatasetCanonicalJsonV1.Hash(dataset),
            FeatureDefinitions,
            transforms,
            weights,
            normalized.Select(ToContract).ToArray(),
            split,
            formula,
            Accuracy(normalized.Take(split.TrainingCount), weights),
            Accuracy(normalized.Skip(split.TrainingCount).Take(split.ValidationCount), weights),
            Accuracy(normalized.Skip(split.TrainingCount + split.ValidationCount), weights),
            ResearchExperimentEvidenceV1.NonPromotionalStatement);
        ResearchExperimentValidatorV1.RequireValid(evidence);
        return evidence;
    }

    private async Task<ExtractedSample> ExtractAsync(
        ResearchEventSampleV1 sample,
        CancellationToken cancellationToken)
    {
        var selection = sample.Selection;
        var from = selection.ObservationFromUtc.UtcDateTime;
        var to = selection.ObservationToUtc.UtcDateTime;
        var bars = selection.RequiredData.HasFlag(StrategyDataRequirement.Bars)
            ? await ReadAll(store.ReadBarsAsync(selection.InstrumentId, selection.Timeframe, from, to, null, cancellationToken), cancellationToken)
                .ConfigureAwait(false)
            : [];
        var quotes = selection.RequiredData.HasFlag(StrategyDataRequirement.L1)
            ? await ReadAll(store.ReadQuotesAsync(selection.InstrumentId, from, to, null, cancellationToken), cancellationToken)
                .ConfigureAwait(false)
            : [];
        var trades = selection.RequiredData.HasFlag(StrategyDataRequirement.TradeTape)
            ? await ReadAll(store.ReadTradesAsync(selection.InstrumentId, from, to, null, cancellationToken), cancellationToken)
                .ConfigureAwait(false)
            : [];
        var depth = selection.RequiredData.HasFlag(StrategyDataRequirement.Depth)
            ? await ReadAll(store.ReadDepthAsync(selection.InstrumentId, from, to, cancellationToken), cancellationToken)
                .ConfigureAwait(false)
            : [];

        EnsureRequiredDataPresent(sample, bars.Count, quotes.Count, trades.Count, depth.Count);

        var values = new[]
        {
            BarReturn(bars),
            BarVolatility(bars),
            BarRangeBps(bars),
            Math.Log(1 + bars.Sum(bar => Math.Max(0d, bar.Volume))),
            Average(quotes, quote => quote.Mid > 0 ? quote.Spread / quote.Mid * 10_000 : 0),
            Average(quotes, quote => Microstructure.QueueImbalance(quote.BidSize, quote.AskSize)),
            OrderFlowImbalance.TradeImbalance(
                trades.LongCount(trade => trade.Aggressor == AggressorSide.Buy),
                trades.LongCount(trade => trade.Aggressor == AggressorSide.Sell)),
            Math.Log(1 + trades.Sum(trade => Math.Max(0d, trade.Size))),
            Average(depth, snapshot => Microstructure.CumulativeImbalance(snapshot, 3)),
            Average(depth, snapshot => Microstructure.CumulativeImbalance(snapshot, 10)),
            Math.Log(1 + Average(depth, snapshot =>
                (double)Microstructure.SideDepth(snapshot.Bids, 10) + Microstructure.SideDepth(snapshot.Asks, 10))),
        };
        return new ExtractedSample(sample, values, bars.Count, quotes.Count, trades.Count, depth.Count, []);
    }

    private static void EnsureRequiredDataPresent(
        ResearchEventSampleV1 sample,
        int barCount,
        int quoteCount,
        int tradeCount,
        int depthCount)
    {
        var missing = new List<string>();
        var required = sample.Selection.RequiredData;
        if (required.HasFlag(StrategyDataRequirement.Bars) && barCount < 2) missing.Add("at least two completed bars");
        if (required.HasFlag(StrategyDataRequirement.L1) && quoteCount == 0) missing.Add("L1 quotes");
        if (required.HasFlag(StrategyDataRequirement.TradeTape) && tradeCount == 0) missing.Add("trade prints");
        if (required.HasFlag(StrategyDataRequirement.Depth) && depthCount == 0) missing.Add("Level-2 depth");
        if (missing.Count != 0)
            throw new InvalidOperationException(
                $"Research sample '{sample.EventSampleId}' is missing {string.Join(", ", missing)} inside its observation window. " +
                "The feature experiment stopped instead of substituting zeros for unavailable market data.");
    }

    private static ResearchChronologicalSplitV1 Split(IReadOnlyList<ExtractedSample> samples)
    {
        var trainingCount = Math.Max(2, (int)Math.Floor(samples.Count * 0.6));
        var validationCount = Math.Max(1, (int)Math.Floor(samples.Count * 0.2));
        if (trainingCount + validationCount >= samples.Count)
            validationCount = 1;
        if (trainingCount + validationCount >= samples.Count)
            trainingCount = samples.Count - 2;
        var testCount = samples.Count - trainingCount - validationCount;
        return new ResearchChronologicalSplitV1(
            trainingCount,
            validationCount,
            testCount,
            samples[trainingCount - 1].Sample.Selection.ObservationToUtc,
            samples[trainingCount + validationCount - 1].Sample.Selection.ObservationToUtc);
    }

    private static ResearchFeatureTransformV1[] FitTransforms(IReadOnlyList<ExtractedSample> training)
    {
        var result = new ResearchFeatureTransformV1[FeatureDefinitions.Length];
        for (var feature = 0; feature < FeatureDefinitions.Length; feature++)
        {
            var mean = training.Average(sample => sample.Raw[feature]);
            var variance = training.Average(sample => Math.Pow(sample.Raw[feature] - mean, 2));
            var standardDeviation = Math.Sqrt(variance);
            if (!double.IsFinite(standardDeviation) || standardDeviation <= 1e-12)
                standardDeviation = 1;
            result[feature] = new ResearchFeatureTransformV1(
                FeatureDefinitions[feature].Name,
                Round(mean),
                Round(standardDeviation));
        }
        return result;
    }

    private static ExtractedSample Normalize(ExtractedSample sample, IReadOnlyList<ResearchFeatureTransformV1> transforms)
    {
        var values = new double[sample.Raw.Length];
        for (var index = 0; index < values.Length; index++)
            values[index] = Round((sample.Raw[index] - transforms[index].TrainingMean) /
                                  transforms[index].TrainingStandardDeviation);
        return sample with { Normalized = values };
    }

    private static (string Name, double Correlation)[] Correlations(IReadOnlyList<ExtractedSample> training)
    {
        var labels = training.Select(sample => (double)Label(sample.Sample.Label)).ToArray();
        return FeatureDefinitions.Select((definition, index) =>
            (definition.Name, Correlation(training.Select(sample => sample.Normalized[index]).ToArray(), labels)))
            .ToArray();
    }

    private static double Correlation(IReadOnlyList<double> x, IReadOnlyList<double> y)
    {
        var meanX = x.Average();
        var meanY = y.Average();
        var numerator = 0d;
        var sumX = 0d;
        var sumY = 0d;
        for (var index = 0; index < x.Count; index++)
        {
            var dx = x[index] - meanX;
            var dy = y[index] - meanY;
            numerator += dx * dy;
            sumX += dx * dx;
            sumY += dy * dy;
        }
        var denominator = Math.Sqrt(sumX * sumY);
        return denominator <= double.Epsilon ? 0 : numerator / denominator;
    }

    private static double Accuracy(IEnumerable<ExtractedSample> samples, IReadOnlyList<ResearchFeatureWeightV1> weights)
    {
        var rows = samples.ToArray();
        if (rows.Length == 0) return 0;
        var featureIndex = FeatureDefinitions
            .Select((feature, index) => (feature.Name, index))
            .ToDictionary(item => item.Name, item => item.index, StringComparer.Ordinal);
        var correct = rows.Count(row =>
        {
            var score = weights.Sum(weight => weight.FormulaWeight * row.Normalized[featureIndex[weight.FeatureName]]);
            var predicted = score > 0.25 ? 1 : score < -0.25 ? -1 : 0;
            return predicted == Label(row.Sample.Label);
        });
        return Round(correct / (double)rows.Length);
    }

    private static ResearchFeatureVectorV1 ToContract(ExtractedSample sample) => new(
        sample.Sample.EventSampleId,
        sample.Sample.Selection.ObservationFromUtc,
        sample.Sample.Selection.ObservationToUtc,
        sample.Sample.Label,
        sample.Raw.Select(Round).ToArray(),
        sample.Normalized,
        sample.BarCount,
        sample.QuoteCount,
        sample.TradeCount,
        sample.DepthCount);

    private static double BarReturn(IReadOnlyList<OhlcvBar> bars) =>
        bars.Count < 2 || bars[0].Close == 0 ? 0 : bars[^1].Close / bars[0].Close - 1;

    private static double BarVolatility(IReadOnlyList<OhlcvBar> bars)
    {
        var sum = 0d;
        for (var index = 1; index < bars.Count; index++)
        {
            if (bars[index - 1].Close <= 0 || bars[index].Close <= 0) continue;
            var value = Math.Log(bars[index].Close / bars[index - 1].Close);
            sum += value * value;
        }
        return Math.Sqrt(sum);
    }

    private static double BarRangeBps(IReadOnlyList<OhlcvBar> bars)
    {
        if (bars.Count == 0 || bars[0].Close <= 0) return 0;
        return (bars.Max(bar => bar.High) - bars.Min(bar => bar.Low)) / bars[0].Close * 10_000;
    }

    private static double Average<T>(IReadOnlyList<T> values, Func<T, double> selector) =>
        values.Count == 0 ? 0 : values.Average(selector);

    private static int Label(ResearchEventLabelKindV1 label) => label switch
    {
        ResearchEventLabelKindV1.PreBreakout => 1,
        ResearchEventLabelKindV1.PreCrash => -1,
        _ => 0,
    };

    private static double Round(double value) => Math.Round(value, 10);

    private static async Task<List<T>> ReadAll<T>(
        IAsyncEnumerable<T> source,
        CancellationToken cancellationToken)
    {
        var values = new List<T>();
        await foreach (var value in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            values.Add(value);
        return values;
    }

    private sealed record ExtractedSample(
        ResearchEventSampleV1 Sample,
        double[] Raw,
        int BarCount,
        int QuoteCount,
        int TradeCount,
        int DepthCount,
        double[] Normalized);
}
