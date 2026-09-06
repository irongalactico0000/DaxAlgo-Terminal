using FluentAssertions;
using TradingTerminal.Charts;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class ChartResearchSelectionTests
{
    [Fact]
    public void Mapper_is_direction_independent_and_returns_a_half_open_bar_range()
    {
        var candles = Candles(0, 60, 120, 180);

        var forward = ChartRangeSelectionMapper.Map(candles, 0, 4, 400, 110, 390);
        var reverse = ChartRangeSelectionMapper.Map(candles, 0, 4, 400, 390, 110);

        forward.Should().Be(reverse);
        forward.StartUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(60));
        forward.EndUtcExclusive.Should().Be(DateTimeOffset.FromUnixTimeSeconds(240));
    }

    [Fact]
    public void Mapper_does_not_extend_a_last_bar_across_a_market_gap()
    {
        var friday = DateTimeOffset.Parse("2026-09-04T19:58:00Z").ToUnixTimeSeconds();
        var monday = DateTimeOffset.Parse("2026-09-07T13:30:00Z").ToUnixTimeSeconds();
        var candles = Candles(friday, friday + 60, monday, monday + 60);

        var range = ChartRangeSelectionMapper.Map(candles, 0, 4, 400, 205, 205);

        range.StartUtc.Should().Be(DateTimeOffset.FromUnixTimeSeconds(monday));
        range.EndUtcExclusive.Should().Be(DateTimeOffset.FromUnixTimeSeconds(monday + 60));
    }

    private static ChartCandle[] Candles(params long[] times) =>
        times.Select(time => new ChartCandle(time, 1, 2, 0.5, 1.5)).ToArray();
}
