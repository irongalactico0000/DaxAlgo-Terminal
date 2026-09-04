# TradingTerminal.Charts — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Charts/TradingTerminal.Charts/AvaloniaInstrumentTagsConverter.cs
```cs
   10: public sealed class AvaloniaInstrumentTagsConverter : IValueConverter
   17: public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
   36: public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
   75: public sealed record AvaloniaInstrumentTag(string Text, IBrush Background, IBrush Foreground);
```

## src/linux/Charts/TradingTerminal.Charts/ChartsPanel.axaml.cs
```cs
   15: public partial class ChartsPanel : UserControl
   17: public static readonly StyledProperty<ChartsPanelFeatures> FeaturesProperty =
   21: public ChartsPanelFeatures Features
   37: public ChartsPanel()
```

## src/linux/Charts/TradingTerminal.Charts/ChartsPanelFeatures.cs
```cs
   15: public sealed record ChartsPanelFeatures
   20: public bool Toolbar { get; init; } = true;
   23: public bool OptionsRail { get; init; } = true;
   27: public bool Indicators { get; init; } = true;
   30: public bool Status { get; init; } = true;
   33: public static ChartsPanelFeatures Full { get; } = new();
   37: public static ChartsPanelFeatures ChartOnly { get; } = new()
   47: public static ChartsPanelFeatures Embedded { get; } = new()
```

## src/linux/Charts/TradingTerminal.Charts/ChartsServiceCollectionExtensions.cs
```cs
    7: public static class ChartsServiceCollectionExtensions
    9: public static IServiceCollection AddChartsSurface(this IServiceCollection services)
```

## src/linux/Charts/TradingTerminal.Charts/ChartsViewModel.cs
```cs
   24: public sealed partial class ChartsViewModel : ViewModelBase, IDisposable
   26: public const int MaxInstrumentsDisplayed = 500;
   63: public ChartsViewModel(
   96: public ObservableCollection<ChartTimeframe> Timeframes { get; }
   97: public ObservableCollection<TradableInstrument> Instruments { get; }
   98: public ObservableCollection<string> PresetNames { get; }
  101: public IReadOnlyList<string> ChartTypes { get; } = new[] { "Candles", "Bars", "Line", "Area" };
  126: public event EventHandler<ChartSnapshot>? SnapshotReady;
  129: public event EventHandler<ChartCandle>? CandleUpdated;
  167: public Task NotifyChartReadyAsync()
  506: public void Dispose()
  522: public sealed record ChartTimeframe(string Label, BarSize BarSize, TimeSpan Lookback);
  531: public sealed record ChartsEmbedOptions(TradableInstrument? Instrument = null, BarSize BarSize = BarSize.OneMinute);
  537: public sealed record ChartsPreset(
  547: public sealed record ChartCandle(long Time, double Open, double High, double Low, double Close);
  548: public sealed record ChartVolume(long Time, double Value, string Color);
  549: public sealed record ChartLinePoint(long Time, double Value);
  550: public sealed record MacdPoint(long Time, double Macd, double Signal, double Hist);
  551: public sealed record ChartSnapshot(
```

## src/linux/Charts/TradingTerminal.Charts/ChartsWindow.axaml.cs
```cs
    9: public partial class ChartsWindow : Window
   11: public ChartsWindow() => InitializeComponent();
```

## src/linux/Charts/TradingTerminal.Charts/NativeChartSurface.cs
```cs
   14: public sealed class NativeChartSurface : Control
   52: public NativeChartSurface()
   58: public ChartSnapshot? Snapshot
   70: public string Message
   81: public void UpdateCandle(ChartCandle candle)
  106: public override void Render(DrawingContext context)
  158: protected override void OnPointerMoved(PointerEventArgs e)
  174: protected override void OnPointerExited(PointerEventArgs e)
  182: protected override void OnPointerPressed(PointerPressedEventArgs e)
  194: protected override void OnPointerReleased(PointerReleasedEventArgs e)
  202: protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
```
