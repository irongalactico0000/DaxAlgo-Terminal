using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies.Generation;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class ResearchOutcomeEventFinderV1Tests
{
    [Fact]
    public void Next_day_plus_five_finds_setup_before_jump()
    {
        var bars = BuildDailyCloses(100, 100.5, 101, 101.2, 101.5, 102, 102.2, 110, 111);
        var matches = ResearchOutcomeEventFinderV1.FindEvents(
            "next-day-plus-5",
            new InstrumentId(7),
            "AAPL",
            "Apple Inc.",
            bars);

        Assert.Contains(matches, static match => match.OutcomeReturn >= 0.05);
        var jump = matches.First(static match => match.OutcomeReturn >= 0.05);
        Assert.Equal("AAPL", jump.CanonicalSymbol);
        Assert.Equal(bars[6].OpenTimeUtc, jump.ObservationFromUtc);
        Assert.Equal(bars[7].OpenTimeUtc, jump.OutcomeFromUtc);
    }

    [Fact]
    public void Pre_crash_finds_setup_before_drop()
    {
        var bars = BuildDailyCloses(100, 101, 100.5, 100.8, 101, 100.2, 100, 90, 91);
        var matches = ResearchOutcomeEventFinderV1.FindEvents(
            "pre-crash",
            new InstrumentId(8),
            "XYZ",
            "Example",
            bars);

        Assert.Contains(matches, static match => match.OutcomeReturn <= -0.05);
    }

    [Fact]
    public void Pre_breakout_requires_tight_prior_range()
    {
        // Flat range then +4% jump — need ≥8 bars for the finder floor.
        var bars = BuildDailyCloses(100, 100.2, 100.1, 100.3, 100.0, 100.15, 100.05, 104.5);
        var matches = ResearchOutcomeEventFinderV1.FindEvents(
            "pre-breakout",
            new InstrumentId(9),
            "ABC",
            "Example",
            bars);

        Assert.NotEmpty(matches);
        Assert.Contains(matches, static match => match.OutcomeReturn >= 0.03);
    }

    [Fact]
    public void Unknown_scan_returns_empty()
    {
        var bars = BuildDailyCloses(100, 110);
        Assert.Empty(ResearchOutcomeEventFinderV1.FindEvents(
            "unknown",
            new InstrumentId(1),
            "A",
            "A",
            bars));
    }

    [Fact]
    public void Next_bar_plus_five_works_on_hourly_bars()
    {
        var start = new DateTime(2024, 6, 3, 14, 0, 0, DateTimeKind.Utc);
        var closes = new[] { 100.0, 100.2, 100.1, 100.3, 100.0, 100.15, 100.05, 106.0, 106.2 };
        var bars = new List<OhlcvBar>(closes.Length);
        for (var i = 0; i < closes.Length; i++)
        {
            var close = closes[i];
            bars.Add(new OhlcvBar(
                new InstrumentId(1),
                BarSize.OneHour,
                start.AddHours(i),
                close,
                close,
                close,
                close,
                10_000,
                BrokerKind.Simulated,
                IsFinal: true));
        }

        var matches = ResearchOutcomeEventFinderV1.FindEvents(
            "next-day-plus-5",
            new InstrumentId(1),
            "AAPL",
            "Apple Inc.",
            bars);

        Assert.Contains(matches, static match => match.OutcomeReturn >= 0.05 && match.Timeframe == BarSize.OneHour);
    }

    private static IReadOnlyList<OhlcvBar> BuildDailyCloses(params double[] closes)
    {
        var start = new DateTime(2024, 1, 2, 14, 30, 0, DateTimeKind.Utc);
        var bars = new List<OhlcvBar>(closes.Length);
        for (var i = 0; i < closes.Length; i++)
        {
            var close = closes[i];
            // Keep High/Low tight so pre-breakout range tests are not polluted by synthetic wicks.
            bars.Add(new OhlcvBar(
                new InstrumentId(1),
                BarSize.OneDay,
                start.AddDays(i),
                close,
                close,
                close,
                close,
                1_000_000,
                BrokerKind.Simulated,
                IsFinal: true));
        }

        return bars;
    }
}
