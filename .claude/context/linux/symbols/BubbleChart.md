# TradingTerminal.BubbleChart — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Charts/TradingTerminal.BubbleChart/BubbleChartServiceCollectionExtensions.cs
```cs
    8: public static class BubbleChartServiceCollectionExtensions
   10: public static IServiceCollection AddBubbleChartSurface(this IServiceCollection services)
```

## src/linux/Charts/TradingTerminal.BubbleChart/BubbleChartViewModel.cs
```cs
   20: public readonly record struct HeatTrade(DateTime Time, double Price, long Size, int Side, bool Large);
   23: public sealed record HeatTimeframe(string Label, double Seconds)
   25: public override string ToString() => Label;
   38: public sealed partial class BubbleChartViewModel : ViewModelBase, IDisposable
   40: public const int MaxInstrumentsDisplayed = 500;
   42: public const int MaxRetained = 360;
   82: public BubbleChartViewModel(
  120: public ObservableCollection<SignalInstrument> Instruments { get; }
  121: public ObservableCollection<HeatTimeframe> Timeframes { get; }
  124: public ObservableCollection<int> RetainedOptions { get; }
  127: public ObservableCollection<string> PresetNames { get; }
  170: public event EventHandler? SurfaceChanged;
  173: public IReadOnlyList<DepthSnapshot> Columns => _columns;
  177: public HeatTrade[] RecentTrades()
  513: public void Dispose()
  526: public sealed record BubbleHeatmapPreset(
```

## src/linux/Charts/TradingTerminal.BubbleChart/BubbleChartWindow.axaml.cs
```cs
   14: public partial class BubbleChartWindow : Window
   19: public BubbleChartWindow()
```

## src/linux/Charts/TradingTerminal.BubbleChart/HeatmapBubbleSurface.cs
```cs
   14: public sealed class HeatmapBubbleSurface : Control
   40: public HeatmapBubbleSurface() => ClipToBounds = true;
   42: public BubbleChartViewModel? ViewModel
   59: public void Detach() => ViewModel = null;
   63: public override void Render(DrawingContext context)
```
