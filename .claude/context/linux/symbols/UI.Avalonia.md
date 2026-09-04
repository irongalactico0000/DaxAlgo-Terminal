# TradingTerminal.UI.Avalonia — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/UI/TradingTerminal.UI.Avalonia/Controls/BusyOverlay.axaml.cs
```cs
   18: public partial class BusyOverlay : UserControl
   20: public static readonly StyledProperty<bool> IsActiveProperty =
   27: public static readonly StyledProperty<bool> IsBusyProperty = IsActiveProperty;
   29: public static readonly StyledProperty<string> TitleProperty =
   32: public static readonly StyledProperty<string> MessageProperty =
   35: public static readonly StyledProperty<double?> ProgressProperty =
   40: public BusyOverlay()
   49: public bool IsActive
   56: public bool IsBusy
   63: public string Title
   70: public string Message
   77: public double? Progress
   83: protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
```

## src/linux/UI/TradingTerminal.UI.Avalonia/Controls/Render/AuthoredUnitView.axaml.cs
```cs
   15: public partial class AuthoredUnitView : UserControl, IDisposable
   20: public AuthoredUnitView()
   87: public void Invalidate()
   93: public void Dispose()
```

## src/linux/UI/TradingTerminal.UI.Avalonia/Controls/Render/AuthoredVisualizerSession.cs
```cs
   21: public static class AuthoredVisualizerSession
   23: public static async Task<(Window Window, AuthoredUnitHost Host, SandboxVisualizerRuntime Runtime)> OpenAsync(
  130: public sealed class AuthoredUnitFeedLease : IDisposable
  144: public IReadOnlySet<InstrumentId> AuthorizedInstruments { get; }
  146: public static AuthoredUnitFeedLease Acquire(
  227: public void Dispose()
```

## src/linux/UI/TradingTerminal.UI.Avalonia/Controls/Render/DrawingContextSurface.cs
```cs
   57: public RenderViewport Viewport => _panel is { } panel
   61: public RenderCursor Cursor
   78: public RenderColor Theme(RenderThemeColor token)
   84: public void SetStyle(RenderStyle style)
   91: public IDisposable Panel(string title, RenderPanelKind kind)
  107: public void AxisX(double minimum, double maximum, string? format = null)
  113: public void AxisY(double minimum, double maximum, string? format = null)
  119: public IDisposable Series(string name, RenderSeriesKind kind)
  126: public void Push(double x, double y)
  134: public void Line(double x1, double y1, double x2, double y2)
  145: public void Rect(double x, double y, double width, double height, bool filled = true)
  160: public void Text(double x, double y, string text)
  179: public void Marker(double x, double y, RenderMarkerShape shape)
  441: public void Dispose()
  455: public void Dispose()
```

## src/linux/UI/TradingTerminal.UI.Avalonia/Controls/Render/RenderSurfaceView.cs
```cs
   14: public sealed class RenderSurfaceView : Control
   16: public const int MaximumOperationsPerFrame = 20_000;
   18: public static readonly DirectProperty<RenderSurfaceView, Action<IRenderSurface>?> DrawProperty =
   27: public Action<IRenderSurface>? Draw
   38: public Func<RenderThemeColor, Color>? ThemeResolver { get; set; }
   40: public int LastFrameOperationCount { get; private set; }
   42: public bool LastFrameWasTruncated { get; private set; }
   44: public RenderSurfaceView()
   51: public override void Render(DrawingContext context)
```

## src/linux/UI/TradingTerminal.UI.Avalonia/GenericStrategyWindow.axaml.cs
```cs
   12: public partial class GenericStrategyWindow : Window
   14: public GenericStrategyWindow() => InitializeComponent();
```
