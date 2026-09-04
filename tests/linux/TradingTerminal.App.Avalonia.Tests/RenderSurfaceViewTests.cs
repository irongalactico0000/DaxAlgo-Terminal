using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using DaxAlgo.Sdk;
using TradingTerminal.UI.Avalonia.Controls.Render;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class RenderSurfaceViewTests
{
    [AvaloniaFact]
    public void TwoPanelsReceiveEqualStableBounds()
    {
        var heights = new List<double>();
        var view = new RenderSurfaceView
        {
            Draw = surface =>
            {
                using (surface.Panel("price", RenderPanelKind.Chart))
                    heights.Add(surface.Viewport.Height);
                using (surface.Panel("depth", RenderPanelKind.Ladder))
                    heights.Add(surface.Viewport.Height);
            },
        };

        Render(view, width: 400d, height: 300d);

        Assert.True(heights.Count >= 2);
        Assert.Equal(150d, heights[^2]);
        Assert.Equal(150d, heights[^1]);
    }

    [AvaloniaFact]
    public void RunawayVisualizerIsTruncatedAtTheFrameBudget()
    {
        var view = new RenderSurfaceView
        {
            Draw = surface =>
            {
                using (surface.Panel("canvas", RenderPanelKind.Canvas))
                {
                    for (var index = 0; index < RenderSurfaceView.MaximumOperationsPerFrame * 2; index++)
                        surface.Line(0d, 0d, 1d, 1d);
                }
            },
        };

        Render(view);

        Assert.True(view.LastFrameWasTruncated);
        Assert.Equal(RenderSurfaceView.MaximumOperationsPerFrame, view.LastFrameOperationCount);
    }

    [AvaloniaFact]
    public void InvalidCoordinatesAreRejectedBeforeDrawing()
    {
        var view = new RenderSurfaceView
        {
            Draw = surface =>
            {
                using (surface.Panel("canvas", RenderPanelKind.Canvas))
                {
                    surface.Line(double.NaN, 0d, 1d, 1d);
                    surface.Rect(double.PositiveInfinity, 0d, 1d, 1d);
                    surface.Text(double.NaN, 0d, "bad");
                    surface.Marker(double.NaN, 0d, RenderMarkerShape.Circle);
                }
            },
        };

        Render(view);

        Assert.Equal(1, view.LastFrameOperationCount);
        Assert.False(view.LastFrameWasTruncated);
    }

    [AvaloniaFact]
    public void VisualizerExceptionDoesNotEscapeTheRenderLoop()
    {
        var view = new RenderSurfaceView
        {
            Draw = surface =>
            {
                using (surface.Panel("price", RenderPanelKind.Chart))
                {
                    surface.Line(0d, 0d, 1d, 1d);
                    throw new InvalidOperationException("authored visualizer fault");
                }
            },
        };

        var fault = Record.Exception(() => Render(view));

        Assert.Null(fault);
        Assert.Equal(2, view.LastFrameOperationCount);
    }

    private static void Render(RenderSurfaceView view, double width = 200d, double height = 100d)
    {
        var window = new Window
        {
            Width = width,
            Height = height,
            Content = view,
        };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            _ = window.CaptureRenderedFrame();
        }
        finally
        {
            window.Close();
        }
    }
}
