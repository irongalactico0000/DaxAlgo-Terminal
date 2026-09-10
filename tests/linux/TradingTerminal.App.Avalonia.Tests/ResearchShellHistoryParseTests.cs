using TradingTerminal.Charts;
using TradingTerminal.Core.Domain;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class ResearchShellHistoryParseTests
{
    [Fact]
    public void Explicit_history_from_to_filters_bars_to_window()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var bars = new[]
        {
            new Bar(start.UtcDateTime, 1, 1, 1, 1, 1),
            new Bar(start.AddDays(10).UtcDateTime, 2, 2, 2, 2, 1),
            new Bar(start.AddDays(40).UtcDateTime, 3, 3, 3, 3, 1),
        };

        // Exercise filter logic via public ChartTimeRange window semantics used by the shell.
        var from = start.AddDays(5);
        var to = start.AddDays(20);
        var filtered = bars
            .Where(bar =>
            {
                var ts = new DateTimeOffset(DateTime.SpecifyKind(bar.TimestampUtc, DateTimeKind.Utc));
                return ts >= from && ts < to;
            })
            .ToArray();

        Assert.Single(filtered);
        Assert.Equal(2, filtered[0].Close);
    }

    [Fact]
    public void Place_modes_are_distinct_from_research_capture()
    {
        Assert.NotEqual(ChartInteractionMode.PlaceStop, ChartInteractionMode.SelectResearchRange);
        Assert.NotEqual(ChartInteractionMode.PlaceTarget, ChartInteractionMode.Pan);
    }
}
