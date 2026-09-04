# TradingTerminal.Backtest — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Tools/TradingTerminal.Backtest/AvaloniaUi/BacktestAvaloniaWindow.axaml.cs
```cs
   12: public partial class BacktestAvaloniaWindow : Window
   16: public BacktestAvaloniaWindow()
```

## src/linux/Tools/TradingTerminal.Backtest/AvaloniaUi/QuickBacktestAvaloniaWindow.axaml.cs
```cs
    7: public partial class QuickBacktestAvaloniaWindow : Window
   11: public QuickBacktestAvaloniaWindow()
```

## src/linux/Tools/TradingTerminal.Backtest/BacktestServiceCollectionExtensions.cs
```cs
    9: public static class BacktestServiceCollectionExtensions
   11: public static IServiceCollection AddBacktestSurface(this IServiceCollection services)
```

## src/linux/Tools/TradingTerminal.Backtest/BacktestView.xaml.cs
```cs
    6: public partial class BacktestView : UserControl
    8: public BacktestView()
```

## src/linux/Tools/TradingTerminal.Backtest/BacktestViewModel.cs
```cs
   24: public sealed partial class BacktestViewModel : ViewModelBase
   31: public BacktestViewModel(
   46: public ObservableCollection<BacktestStrategyOption> Strategies { get; }
   47: public ObservableCollection<Trade> Trades { get; }
   48: public ObservableCollection<EquityPoint> EquityCurve { get; }
   75: public bool IsFastAvailable =>
   79: public event EventHandler? EquityCurveUpdated;
   82: public async Task BrowseDataPath()
   90: public async Task RunAsync()
  177: public void Cancel()
```

## src/linux/Tools/TradingTerminal.Backtest/QuickBacktestView.xaml.cs
```cs
    5: public partial class QuickBacktestView : UserControl
    7: public QuickBacktestView()
```

## src/linux/Tools/TradingTerminal.Backtest/QuickBacktestViewModel.cs
```cs
   27: public enum QuickBacktestDataMode
   50: public sealed partial class QuickBacktestViewModel : ViewModelBase, IDisposable
   64: public QuickBacktestViewModel(
  112: public ObservableCollection<SignalInstrument> Instruments { get; }
  113: public ObservableCollection<BarSize> BarSizes { get; }
  114: public ObservableCollection<LookbackOption> Lookbacks { get; }
  115: public ObservableCollection<QuickBacktestDataMode> DataModes { get; }
  116: public ObservableCollection<BrokerKind> Brokers { get; }
  117: public ObservableCollection<Trade> Trades { get; }
  118: public ObservableCollection<EquityPoint> EquityCurve { get; }
  152: public bool IsFullTape => SelectedDataMode == QuickBacktestDataMode.FullTapeRealTrades;
  153: public bool IsBarSynthetic => SelectedDataMode == QuickBacktestDataMode.BarSynthetic;
  154: public bool IsAuthoredStrategy => _kernelOption is not null;
  155: public bool CanSelectInstrument => !IsAuthoredStrategy && !IsRunning;
  156: public bool CanSelectBarSize => !IsAuthoredStrategy && !IsRunning;
  157: public IReadOnlyList<ParameterEditorItem> EditableParameters => Parameters?.Items
  160: public bool HasStrategyParameters => EditableParameters.Count != 0;
  161: public bool CanEditParameters => HasStrategyParameters && !IsRunning;
  162: public string ReviewedInstrumentSummary => _canonicalSelections.Count == 0
  191: public event EventHandler? EquityCurveUpdated;
  199: public bool Initialize(string? backtestStrategyId, string displayName, bool preferFullTape)
  238: public bool Initialize(StrategyKernelRegistration registration)
  395: public async Task RunAsync()
  605: public void Cancel() => _runCts?.Cancel();
  608: public void Dispose()
  677: public sealed record LookbackOption(string Label, TimeSpan Duration)
  679: public override string ToString() => Label;
```
