# TradingTerminal.Charts — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Charts/TradingTerminal.Charts/AvaloniaInstrumentTagsConverter.cs
```cs
   10: public sealed class AvaloniaInstrumentTagsConverter : IValueConverter
   17: public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
   36: public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
   75: public sealed record AvaloniaInstrumentTag(string Text, IBrush Background, IBrush Foreground);
```

## src/linux/Charts/TradingTerminal.Charts/ChartResearchSelection.cs
```cs
    6: public enum ChartInteractionMode
   13: public sealed record ChartTimeRange
   15: public ChartTimeRange(DateTimeOffset startUtc, DateTimeOffset endUtcExclusive)
   23: public DateTimeOffset StartUtc { get; }
   24: public DateTimeOffset EndUtcExclusive { get; }
   27: public sealed class ChartRangeSelectedEventArgs(ChartTimeRange range) : EventArgs
   29: public ChartTimeRange Range { get; } = range ?? throw new ArgumentNullException(nameof(range));
   32: public sealed class ResearchChartSelectionRequestedEventArgs(ResearchChartSelectionV1 selection) : EventArgs
   34: public ResearchChartSelectionV1 Selection { get; } = selection ?? throw new ArgumentNullException(nameof(selection));
   38: public static class ChartRangeSelectionMapper
   40: public static ChartTimeRange Map(
```

## src/linux/Charts/TradingTerminal.Charts/ChartsPanel.axaml.cs
```cs
   16: public partial class ChartsPanel : UserControl
   18: public static readonly StyledProperty<ChartsPanelFeatures> FeaturesProperty =
   22: public ChartsPanelFeatures Features
   38: public ChartsPanel()
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

## src/linux/Charts/TradingTerminal.Charts/ChartsViewModel.Research.cs
```cs
    9: public enum ChartResearchSelectionStep
   16: public sealed partial class ChartsViewModel
   22: public bool IsResearchRangeSelectionEnabled => ResearchSelectionStep != ChartResearchSelectionStep.None;
   23: public bool HasResearchObservationRange => ResearchObservationRange is not null;
   24: public bool HasResearchOutcomeRange => ResearchOutcomeRange is not null;
   25: public bool CanSendResearchSelection =>
   28: public string ResearchSelectionSummary => ResearchSelectionStep switch
   36: public event EventHandler<ResearchChartSelectionRequestedEventArgs>? ResearchSelectionRequested;
   88: public void SelectResearchRange(ChartTimeRange range)
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
  176: public Task NotifyChartReadyAsync()
  515: public void Dispose()
  531: public sealed record ChartTimeframe(string Label, BarSize BarSize, TimeSpan Lookback);
  540: public sealed record ChartsEmbedOptions(TradableInstrument? Instrument = null, BarSize BarSize = BarSize.OneMinute);
  546: public sealed record ChartsPreset(
  556: public sealed record ChartCandle(long Time, double Open, double High, double Low, double Close);
  557: public sealed record ChartVolume(long Time, double Value, string Color);
  558: public sealed record ChartLinePoint(long Time, double Value);
  559: public sealed record MacdPoint(long Time, double Macd, double Signal, double Hist);
  560: public sealed record ChartSnapshot(
```

## src/linux/Charts/TradingTerminal.Charts/ChartsWindow.axaml.cs
```cs
    9: public partial class ChartsWindow : Window
   11: public ChartsWindow() => InitializeComponent();
```

## src/linux/Charts/TradingTerminal.Charts/NativeChartSurface.cs
```cs
   14: public sealed class NativeChartSurface : Control
   62: public NativeChartSurface()
   69: public ChartInteractionMode InteractionMode
   82: public ChartTimeRange? ObservationRange
   88: public ChartTimeRange? OutcomeRange
   94: public event EventHandler<ChartRangeSelectedEventArgs>? ResearchRangeSelected;
   96: public ChartSnapshot? Snapshot
  108: public string Message
  119: public void UpdateCandle(ChartCandle candle)
  144: public override void Render(DrawingContext context)
  199: protected override void OnPointerMoved(PointerEventArgs e)
  219: protected override void OnPointerExited(PointerEventArgs e)
  227: protected override void OnPointerPressed(PointerPressedEventArgs e)
  250: protected override void OnPointerReleased(PointerReleasedEventArgs e)
  272: protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
```
