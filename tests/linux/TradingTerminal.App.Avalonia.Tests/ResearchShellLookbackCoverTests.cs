using TradingTerminal.Charts;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class ResearchShellLookbackCoverTests
{
    [Fact]
    public void IBrokerClient_exposes_from_to_historical_bars_overload()
    {
        // Explicit Charts From/To must use the range overload — not duration-cover leftovers.
        var method = typeof(IBrokerClient).GetMethod(
            nameof(IBrokerClient.RequestHistoricalBarsAsync),
            [typeof(Contract), typeof(BarSize), typeof(DateTime), typeof(DateTime), typeof(CancellationToken)]);

        Assert.NotNull(method);
    }

    [Fact]
    public void Place_and_pan_modes_remain_orthogonal_to_capture()
    {
        Assert.Contains(ChartInteractionMode.PlaceStop, Enum.GetValues<ChartInteractionMode>());
        Assert.Contains(ChartInteractionMode.PlaceTarget, Enum.GetValues<ChartInteractionMode>());
        Assert.Contains(ChartInteractionMode.SelectResearchRange, Enum.GetValues<ChartInteractionMode>());
    }
}
