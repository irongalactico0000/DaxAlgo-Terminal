using DaxAlgo.Sdk;
using TradingTerminal.UI.Controls.Render;
using TradingTerminal.UI.Logging;
using Xunit;

namespace TradingTerminal.UI.Core.Tests;

/// <summary>
/// Headless composition checks for the AuthoredUnitHost path used by Avalonia
/// <c>AuthoredVisualizerSession</c>. Frame-pacing coverage lives in
/// <see cref="AuthoredUnitHostTests"/> (do not share its static timer seam across classes).
/// </summary>
public sealed class AuthoredVisualizerCompositionTests
{
    [Fact]
    public void Host_draw_delegate_forwards_to_supplied_tryDraw()
    {
        var drawn = new List<IRenderSurface>();
        using var host = new AuthoredUnitHost(
            "Probe",
            surface =>
            {
                drawn.Add(surface);
                return true;
            });

        Assert.NotNull(host.Presenter.Draw);
        host.Presenter.Draw!(new NullRenderSurface());
        Assert.Single(drawn);
    }

    [Fact]
    public void Log_mirror_matches_windows_authored_unit_filter()
    {
        var log = new InMemoryLogSink();
        using var host = new AuthoredUnitHost("Probe", _ => true, log: log);
        log.Append("Probe", "Information", "alive");
        log.Append("Other", "Information", "noise");
        Assert.Equal(["alive"], host.Presenter.Log.Select(line => line.Message));
    }
}
