# TradingTerminal.Backtest — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
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
   46: public sealed record QuickBacktestPaperLaunchRequest(
   62: public sealed partial class QuickBacktestViewModel : ViewModelBase, IDisposable
   78: public QuickBacktestViewModel(
  126: public ObservableCollection<SignalInstrument> Instruments { get; }
  127: public ObservableCollection<BarSize> BarSizes { get; }
  128: public ObservableCollection<LookbackOption> Lookbacks { get; }
  129: public ObservableCollection<QuickBacktestDataMode> DataModes { get; }
  130: public ObservableCollection<BrokerKind> Brokers { get; }
  131: public ObservableCollection<Trade> Trades { get; }
  132: public ObservableCollection<EquityPoint> EquityCurve { get; }
  166: public bool IsFullTape => SelectedDataMode == QuickBacktestDataMode.FullTapeRealTrades;
  167: public bool IsBarSynthetic => SelectedDataMode == QuickBacktestDataMode.BarSynthetic;
  168: public bool IsAuthoredStrategy => _kernelOption is not null;
  169: public bool CanSelectInstrument => !IsAuthoredStrategy && !IsRunning;
  170: public bool CanSelectBarSize => !IsAuthoredStrategy && !IsRunning;
  171: public IReadOnlyList<ParameterEditorItem> EditableParameters => Parameters?.Items
  174: public bool HasStrategyParameters => EditableParameters.Count != 0;
  175: public bool CanEditParameters => HasStrategyParameters && !IsRunning;
  176: public bool CanRunTestedStrategyInPaper => _paperLaunchRequest is not null && !IsRunning;
  177: public string ReviewedInstrumentSummary => _canonicalSelections.Count == 0
  206: public event EventHandler? EquityCurveUpdated;
  207: public event Action<QuickBacktestPaperLaunchRequest>? PaperLaunchRequested;
  208: public event Action<QuickBacktestPaperLaunchRequest>? HistoricalValidationCompleted;
  216: public bool Initialize(string? backtestStrategyId, string displayName, bool preferFullTape)
  257: public bool Initialize(
  420: public async Task RunAsync()
  669: public void Cancel() => _runCts?.Cancel();
  686: public void Dispose()
  759: public sealed record LookbackOption(string Label, TimeSpan Duration)
  761: public override string ToString() => Label;
```
