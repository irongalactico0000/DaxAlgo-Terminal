using FluentAssertions;
using System.Runtime.CompilerServices;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.Research;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class StrategyResearchExperimentRunnerV1Tests
{
    [Fact]
    public async Task Experiment_reads_only_observation_windows_and_fits_transforms_on_training_rows()
    {
        var dataset = Dataset();
        var store = new RecordingStore(dataset);
        var runner = new StrategyResearchExperimentRunnerV1(store);

        var evidence = await runner.RunAsync(dataset);

        evidence.Split.Should().BeEquivalentTo(new
        {
            TrainingCount = 3,
            ValidationCount = 1,
            TestCount = 1,
        });
        evidence.SamplesChronological.Select(row => row.EventSampleId)
            .Should().Equal("sample-0", "sample-1", "sample-2", "sample-3", "sample-4");
        store.ReadWindows.Should().HaveCount(20);
        foreach (var window in store.ReadWindows)
        {
            dataset.Samples.Should().Contain(sample =>
                sample.Selection.ObservationFromUtc.UtcDateTime == window.FromUtc &&
                sample.Selection.ObservationToUtc.UtcDateTime == window.ToUtc);
            dataset.Samples.Should().NotContain(sample =>
                sample.Selection.OutcomeToUtc.UtcDateTime == window.ToUtc);
        }

        var returnIndex = evidence.Features.Select((feature, index) => (feature.Name, index))
            .Single(item => item.Name == "bar_return").index;
        var trainingMean = evidence.SamplesChronological.Take(3)
            .Average(row => row.RawValues[returnIndex]);
        evidence.TrainingTransforms.Single(transform => transform.FeatureName == "bar_return")
            .TrainingMean.Should().BeApproximately(trainingMean, 1e-10);
        evidence.PromotionStatement.Should().Be(ResearchExperimentEvidenceV1.NonPromotionalStatement);
    }

    [Fact]
    public async Task Same_dataset_and_store_rows_produce_identical_canonical_evidence()
    {
        var dataset = Dataset();
        var runner = new StrategyResearchExperimentRunnerV1(new RecordingStore(dataset));

        var first = await runner.RunAsync(dataset);
        var second = await runner.RunAsync(dataset);

        ResearchExperimentCanonicalJsonV1.Serialize(first)
            .Should().Be(ResearchExperimentCanonicalJsonV1.Serialize(second));
        ResearchExperimentCanonicalJsonV1.Hash(first)
            .Should().Be(ResearchExperimentCanonicalJsonV1.Hash(second));
    }

    [Fact]
    public async Task Required_missing_depth_stops_instead_of_manufacturing_zero_features()
    {
        var dataset = Dataset();
        var runner = new StrategyResearchExperimentRunnerV1(new RecordingStore(dataset, includeDepth: false));

        var action = () => runner.RunAsync(dataset);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*missing*Level-2 depth*instead of substituting zeros*");
    }

    private static ResearchDatasetDefinitionV1 Dataset()
    {
        var samples = Enumerable.Range(0, 5).Select(index =>
        {
            var start = new DateTimeOffset(2026, 9, 1 + index, 10, 0, 0, TimeSpan.Zero);
            var selection = new ResearchChartSelectionV1(
                new InstrumentId(42),
                "BTC-USD",
                BarSize.OneMinute,
                start,
                start.AddMinutes(10),
                start.AddMinutes(10),
                start.AddMinutes(15),
                StrategyDataRequirement.L1 | StrategyDataRequirement.Bars |
                StrategyDataRequirement.Depth | StrategyDataRequirement.TradeTape);
            return new ResearchEventSampleV1(
                ResearchEventSampleV1.CurrentSchemaVersion,
                $"sample-{index}",
                selection,
                (index % 3) switch
                {
                    0 => ResearchEventLabelKindV1.PreBreakout,
                    1 => ResearchEventLabelKindV1.PreCrash,
                    _ => ResearchEventLabelKindV1.Neutral,
                },
                null,
                ResearchEventLabelSourceV1.Manual);
        }).ToArray();
        return new ResearchDatasetDefinitionV1(
            ResearchDatasetDefinitionV1.CurrentSchemaVersion,
            "btc-events",
            new string('a', 64),
            StrategyDataRequirement.L1 | StrategyDataRequirement.Bars |
            StrategyDataRequirement.Depth | StrategyDataRequirement.TradeTape,
            ResearchLeakagePolicyV1.SafeDefault,
            samples);
    }

    private sealed class RecordingStore : IMarketDataStore
    {
        private readonly IReadOnlyList<OhlcvBar> _bars;
        private readonly IReadOnlyList<Quote> _quotes;
        private readonly IReadOnlyList<TradePrint> _trades;
        private readonly IReadOnlyList<DepthSnapshot> _depth;

        public RecordingStore(ResearchDatasetDefinitionV1 dataset, bool includeDepth = true)
        {
            var bars = new List<OhlcvBar>();
            var quotes = new List<Quote>();
            var trades = new List<TradePrint>();
            var depth = new List<DepthSnapshot>();
            for (var index = 0; index < dataset.Samples.Count; index++)
            {
                var selection = dataset.Samples[index].Selection;
                var start = selection.ObservationFromUtc.UtcDateTime;
                var basePrice = 100 + index * 10;
                bars.Add(new(selection.InstrumentId, selection.Timeframe, start, basePrice, basePrice + 1,
                    basePrice - 1, basePrice, 100 + index, BrokerKind.Simulated, true));
                bars.Add(new(selection.InstrumentId, selection.Timeframe, start.AddMinutes(9), basePrice,
                    basePrice + 2, basePrice - 1, basePrice + index + 1, 200 + index, BrokerKind.Simulated, true));
                quotes.Add(new(selection.InstrumentId, start.AddMinutes(2), start.AddMinutes(2),
                    basePrice - .05, basePrice + .05, 20 + index, 10 + index,
                    BrokerKind.Simulated, index, false));
                trades.Add(new(selection.InstrumentId, start.AddMinutes(3), start.AddMinutes(3), basePrice,
                    5 + index, index % 2 == 0 ? AggressorSide.Buy : AggressorSide.Sell,
                    BrokerKind.Simulated, index, false));
                if (includeDepth)
                    depth.Add(new(start.AddMinutes(4),
                        [new DepthLevel(basePrice - .05, 30 + index), new DepthLevel(basePrice - .10, 20)],
                        [new DepthLevel(basePrice + .05, 15 + index), new DepthLevel(basePrice + .10, 10)]));

                bars.Add(new(selection.InstrumentId, selection.Timeframe, selection.OutcomeFromUtc.UtcDateTime,
                    basePrice, basePrice * 2, basePrice / 2, basePrice * 1.8, 9_999, BrokerKind.Simulated, true));
            }
            _bars = bars;
            _quotes = quotes;
            _trades = trades;
            _depth = depth;
        }

        public List<(DateTime FromUtc, DateTime ToUtc)> ReadWindows { get; } = [];

        public void EnqueueQuote(Quote quote) { }
        public void EnqueueTrade(TradePrint trade) { }
        public void EnqueueBar(OhlcvBar bar) { }
        public void EnqueueDepth(InstrumentId instrumentId, DepthSnapshot snapshot, BrokerKind source) { }
        public Task FlushAsync(CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<OhlcvBar>> GetRecentBarsAsync(InstrumentId instrumentId, BarSize size, int count,
            BrokerKind? source = null, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<OhlcvBar>>([]);

        public IAsyncEnumerable<Quote> ReadQuotesAsync(InstrumentId instrumentId, DateTime fromUtc, DateTime toUtc,
            BrokerKind? source = null, CancellationToken ct = default)
        {
            ReadWindows.Add((fromUtc, toUtc));
            return Stream(_quotes.Where(value => value.InstrumentId == instrumentId && value.EventTimeUtc >= fromUtc && value.EventTimeUtc < toUtc), ct);
        }

        public IAsyncEnumerable<TradePrint> ReadTradesAsync(InstrumentId instrumentId, DateTime fromUtc, DateTime toUtc,
            BrokerKind? source = null, CancellationToken ct = default)
        {
            ReadWindows.Add((fromUtc, toUtc));
            return Stream(_trades.Where(value => value.InstrumentId == instrumentId && value.EventTimeUtc >= fromUtc && value.EventTimeUtc < toUtc), ct);
        }

        public IAsyncEnumerable<DepthSnapshot> ReadDepthAsync(InstrumentId instrumentId, DateTime fromUtc, DateTime toUtc,
            CancellationToken ct = default)
        {
            ReadWindows.Add((fromUtc, toUtc));
            return Stream(_depth.Where(value => value.TimestampUtc >= fromUtc && value.TimestampUtc < toUtc), ct);
        }

        public IAsyncEnumerable<OhlcvBar> ReadBarsAsync(InstrumentId instrumentId, BarSize size, DateTime fromUtc,
            DateTime toUtc, BrokerKind? source = null, CancellationToken ct = default)
        {
            ReadWindows.Add((fromUtc, toUtc));
            return Stream(_bars.Where(value => value.InstrumentId == instrumentId && value.Size == size &&
                                               value.OpenTimeUtc >= fromUtc && value.OpenTimeUtc < toUtc), ct);
        }

        public Task<long> DeleteQuotesInRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<long> DeleteTradesInRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<long> DeleteBarsInRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => Task.FromResult(0L);
        public Task<long> DeleteDepthInRangeAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default) => Task.FromResult(0L);

        private static async IAsyncEnumerable<T> Stream<T>(
            IEnumerable<T> values,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            foreach (var value in values)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return value;
            }
        }
    }
}
