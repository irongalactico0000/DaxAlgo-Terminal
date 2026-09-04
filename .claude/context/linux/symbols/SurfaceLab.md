# TradingTerminal.SurfaceLab — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Charts/TradingTerminal.SurfaceLab/AxisConfigViewModel.cs
```cs
   14: public sealed partial class AxisConfigViewModel : ObservableObject
   18: public AxisConfigViewModel(SurfaceAxisRole role, string title)
   25: public SurfaceAxisRole Role { get; }
   26: public string Title { get; }
   29: public bool SupportsFormula { get; }
   33: public ObservableCollection<SurfaceAxisOption> Options { get; } = new();
   44: public bool UsesFormula => SupportsFormula && !string.IsNullOrWhiteSpace(CustomFormula);
   61: public void SetOptions(IReadOnlyList<SurfaceAxisOption> options, int preferredIndex = 0)
   73: public AxisPreset ToPreset() => new(SelectedOption?.Id, Min, Max, Step,
   79: public void ApplyPreset(AxisPreset preset)
   94: public SurfaceAxisSpec? ToSpec(out string? error)
```

## src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceLabConverters.cs
```cs
    6: public sealed class StringNotEmptyConverter : IValueConverter
    8: public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
   11: public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
```

## src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceLabServiceCollectionExtensions.cs
```cs
    7: public static class SurfaceLabServiceCollectionExtensions
    9: public static IServiceCollection AddSurfaceLabSurface(this IServiceCollection services)
```

## src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceLabViewModel.cs
```cs
   29: public sealed partial class SurfaceLabViewModel : ViewModelBase, IDisposable
   38: public const int MaxInstrumentsDisplayed = 500;
   63: public SurfaceLabViewModel(
  115: public ObservableCollection<SurfaceModeOption> Modes { get; }
  116: public AxisConfigViewModel XAxis { get; }
  117: public AxisConfigViewModel YAxis { get; }
  118: public AxisConfigViewModel ZAxis { get; }
  119: public AxisConfigViewModel WAxis { get; }
  121: public ObservableCollection<TimeframeOption> Timeframes { get; }
  122: public ObservableCollection<SignalInstrument> Instruments { get; private set; }
  123: public IReadOnlyList<SignalInstrument> AllInstruments { get; private set; }
  154: public bool CanGenerate => !IsRunning && !IsLive;
  161: public ObservableCollection<string> PresetNames { get; }
  189: public SurfaceGridResult? Result { get; private set; }
  191: public bool HasResult => Result is not null;
  208: public event EventHandler? SurfaceUpdated;
  212: public event EventHandler? SliceChanged;
  261: public async Task GenerateAsync()
  312: public async Task ToggleLiveAsync()
  677: public void Dispose()
  742: public sealed record SurfaceModeOption(string Label, SurfaceMode Mode, string Description)
  744: public override string ToString() => Label;
  749: public sealed record AxisPreset(string? OptionId, double Min, double Max, double Step, string? Formula);
  756: public sealed record SurfaceLabPreset(
  770: public sealed record TimeframeOption(string Label, BarSize BarSize, TimeSpan Interval)
  772: public override string ToString() => Label;
```

## src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceLabWindow.axaml.cs
```cs
   12: public partial class SurfaceLabWindow : Window
   19: public SurfaceLabWindow()
```

## src/linux/Charts/TradingTerminal.SurfaceLab/SurfacePlot3D.cs
```cs
   16: public sealed class SurfacePlot3D : Control
   78: public SurfacePlot3D()
   84: public SurfaceLabViewModel? ViewModel
  186: public override void Render(DrawingContext context)
  331: protected override void OnPointerPressed(PointerPressedEventArgs e)
  351: protected override void OnPointerMoved(PointerEventArgs e)
  374: protected override void OnPointerReleased(PointerReleasedEventArgs e)
  384: protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
```

## src/linux/Charts/TradingTerminal.SurfaceLab/SurfaceSlicePlot.cs
```cs
    9: public sealed class SurfaceSlicePlot : Control
   26: public SurfaceSlicePlot() => ClipToBounds = true;
   28: public void SetData(double[] axis, double[] values, string title)
   36: public void Clear()
   44: public override void Render(DrawingContext context)
```
