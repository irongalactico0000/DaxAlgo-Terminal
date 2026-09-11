using TradingTerminal.Charts;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class ChartPriceMapperTests
{
    [Fact]
    public void Maps_mid_price_pane_to_mid_price_and_rejects_oscillator_y()
    {
        Assert.True(ChartPriceMapper.TryMap(
            pointX: 100, pointY: 200,
            paneLeft: 0, paneTop: 100, paneWidth: 400, paneHeight: 200,
            priceMin: 90, priceMax: 110,
            out var mid));
        Assert.Equal(100m, mid);

        Assert.False(ChartPriceMapper.TryMap(
            pointX: 100, pointY: 350,
            paneLeft: 0, paneTop: 100, paneWidth: 400, paneHeight: 200,
            priceMin: 90, priceMax: 110,
            out _));
    }

    [Fact]
    public void Top_of_pane_is_high_price_bottom_is_low()
    {
        Assert.True(ChartPriceMapper.TryMap(
            50, 100, 0, 100, 200, 100, 80, 120, out var high));
        Assert.Equal(120m, high);

        Assert.True(ChartPriceMapper.TryMap(
            50, 200, 0, 100, 200, 100, 80, 120, out var low));
        Assert.Equal(80m, low);
    }
}
