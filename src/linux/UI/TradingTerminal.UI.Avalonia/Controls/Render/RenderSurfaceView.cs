using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using DaxAlgo.Sdk;

namespace TradingTerminal.UI.Avalonia.Controls.Render;

/// <summary>
/// Avalonia host for the platform-neutral <see cref="IRenderSurface"/> contract.
/// A discovery pass counts panels before the drawing pass so every panel receives stable bounds.
/// </summary>
public sealed class RenderSurfaceView : Control
{
    public const int MaximumOperationsPerFrame = 20_000;

    public static readonly DirectProperty<RenderSurfaceView, Action<IRenderSurface>?> DrawProperty =
        AvaloniaProperty.RegisterDirect<RenderSurfaceView, Action<IRenderSurface>?>(
            nameof(Draw), view => view.Draw, (view, value) => view.Draw = value);

    private Action<IRenderSurface>? _draw;
    private Point _cursor;
    private bool _cursorInside;
    private bool _cursorPressed;

    public Action<IRenderSurface>? Draw
    {
        get => _draw;
        set
        {
            SetAndRaise(DrawProperty, ref _draw, value);
            InvalidateVisual();
        }
    }

    /// <summary>Resolves semantic theme roles without exposing Avalonia to authored code.</summary>
    public Func<RenderThemeColor, Color>? ThemeResolver { get; set; }

    public int LastFrameOperationCount { get; private set; }

    public bool LastFrameWasTruncated { get; private set; }

    public RenderSurfaceView()
    {
        ClipToBounds = true;
        PointerMoved += OnPointerMoved;
        PointerExited += OnPointerExited;
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Draw is not { } draw || Bounds.Width <= 0d || Bounds.Height <= 0d)
        {
            LastFrameOperationCount = 0;
            LastFrameWasTruncated = false;
            return;
        }

        var scale = this.FindAncestorOfType<TopLevel>()?.RenderScaling ?? 1d;
        var size = Bounds.Size;
        var cursor = new RenderCursor(_cursor.X, _cursor.Y, _cursorInside, _cursorPressed);
        var theme = ThemeResolver ?? DefaultTheme;

        var discovery = new DrawingContextSurface(
            context, size, scale, cursor, theme, discovering: true);
        try
        {
            draw(discovery);
        }
        catch (Exception)
        {
            // Authored drawing code is isolated from the UI render loop.
        }

        var surface = new DrawingContextSurface(
            context, size, scale, cursor, theme, expectedPanels: discovery.PanelCount);
        try
        {
            draw(surface);
        }
        catch (Exception)
        {
            // Preserve the partial frame; the visualizer runtime reports the fault.
        }
        finally
        {
            surface.Close();
            LastFrameOperationCount = surface.OperationCount;
            LastFrameWasTruncated = surface.WasTruncated;
        }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs args)
    {
        var point = args.GetCurrentPoint(this);
        _cursor = point.Position;
        _cursorInside = true;
        _cursorPressed = point.Properties.IsLeftButtonPressed;
        InvalidateVisual();
    }

    private void OnPointerExited(object? sender, PointerEventArgs args)
    {
        _cursorInside = false;
        _cursorPressed = false;
        InvalidateVisual();
    }

    private static Color DefaultTheme(RenderThemeColor token) => token switch
    {
        RenderThemeColor.Text => Color.FromRgb(0xE6, 0xED, 0xF3),
        RenderThemeColor.TextSecondary => Color.FromRgb(0x8C, 0x9A, 0xB3),
        RenderThemeColor.Background => Color.FromRgb(0x0D, 0x11, 0x17),
        RenderThemeColor.Surface => Color.FromRgb(0x16, 0x1B, 0x22),
        RenderThemeColor.Grid => Color.FromRgb(0x22, 0x2A, 0x35),
        RenderThemeColor.Border => Color.FromRgb(0x30, 0x3A, 0x48),
        RenderThemeColor.Accent => Color.FromRgb(0x2F, 0x6B, 0xD4),
        RenderThemeColor.Bullish => Color.FromRgb(0x26, 0xA6, 0x69),
        RenderThemeColor.Bearish => Color.FromRgb(0xC0, 0x26, 0x26),
        RenderThemeColor.Warning => Color.FromRgb(0xD9, 0x8A, 0x0B),
        _ => Color.FromRgb(0x8C, 0x9A, 0xB3),
    };
}
