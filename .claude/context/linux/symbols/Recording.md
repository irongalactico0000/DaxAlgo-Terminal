# TradingTerminal.Recording — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Tools/TradingTerminal.Recording/AvaloniaUi/TickRecorderAvaloniaWindow.axaml.cs
```cs
    7: public partial class TickRecorderAvaloniaWindow : Window
    9: public TickRecorderAvaloniaWindow() => InitializeComponent();
```

## src/linux/Tools/TradingTerminal.Recording/RecorderEntry.cs
```cs
   17: public sealed partial class RecorderEntry : ObservableObject
   28: public RecorderEntry(SignalInstrument instrument, BrokerKind? pinnedBroker)
   34: public SignalInstrument Instrument { get; }
   37: public BrokerKind? PinnedBroker { get; }
   39: public string DisplayName => Instrument.DisplayName;
   40: public string Category => Instrument.Category;
   41: public string Symbol => Instrument.Contract.Symbol;
   44: public InstrumentId Id { get; internal set; }
   57: public long Quotes => Interlocked.Read(ref QuotesRaw);
   58: public long Trades => Interlocked.Read(ref TradesRaw);
   59: public long Bars => Interlocked.Read(ref BarsRaw);
   60: public long Depth => Interlocked.Read(ref DepthRaw);
   63: public bool SupportsQuotes => _activeCapabilities?.SupportsLevel1Quotes == true;
   66: public bool SupportsBars => _activeCapabilities?.SupportsLiveBars == true;
   69: public bool SupportsTape => _activeCapabilities?.SupportsLiveTrades == true;
   72: public bool SupportsDepth => _activeCapabilities?.SupportsLevel2Depth == true;
   77: public static bool SupportsL3 => false;
  128: public RecorderWatchlistItem ToWatchlistItem() => RecorderWatchlistItem.From(Instrument, PinnedBroker);
```

## src/linux/Tools/TradingTerminal.Recording/RecorderPanelView.axaml.cs
```cs
    6: public partial class RecorderPanelView : UserControl
    8: public RecorderPanelView() => InitializeComponent();
```

## src/linux/Tools/TradingTerminal.Recording/RecorderPanelViewModel.cs
```cs
   23: public partial class RecorderPanelViewModel : ViewModelBase, IDisposable
   33: public RecorderPanelViewModel(
   81: public TickRecordingService Service { get; }
   86: public ObservableCollection<SignalInstrument> Instruments { get; }
   88: public IReadOnlyList<SignalInstrument> AllInstruments { get; private set; }
  138: public void Dispose()
```

## src/linux/Tools/TradingTerminal.Recording/RecorderWatchlistStore.cs
```cs
   19: public sealed record RecorderWatchlistItem(
   29: public static RecorderWatchlistItem From(SignalInstrument instrument, BrokerKind? pinned)
   38: public SignalInstrument ToInstrument() => new(
   44: public BrokerKind? PinnedBroker => Enum.TryParse<BrokerKind>(Broker, out var b) ? b : null;
   50: public sealed record RecorderWatchlist(
   55: public static RecorderWatchlist Empty { get; } = new(Array.Empty<RecorderWatchlistItem>(), false, false);
   64: public static class RecorderWatchlistStore
   72: public static RecorderWatchlist Load()
   89: public static void Save(RecorderWatchlist watchlist)
```

## src/linux/Tools/TradingTerminal.Recording/RecordingServiceCollectionExtensions.cs
```cs
    6: public static class RecordingServiceCollectionExtensions
    8: public static IServiceCollection AddRecordingSurface(this IServiceCollection services)
```

## src/linux/Tools/TradingTerminal.Recording/TickRecorderViewModel.cs
```cs
   11: public sealed class TickRecorderViewModel : RecorderPanelViewModel
   13: public TickRecorderViewModel(
```

## src/linux/Tools/TradingTerminal.Recording/TickRecordingService.cs
```cs
   35: public sealed partial class TickRecordingService : ObservableObject, IHostedService, IDisposable
   62: public TickRecordingService(
   84: public ObservableCollection<RecorderEntry> Instruments { get; } = new();
  100: public bool HasInstruments => Instruments.Count > 0;
  104: public Task StartAsync(CancellationToken cancellationToken)
  121: public Task StopAsync(CancellationToken cancellationToken)
  131: public void Add(SignalInstrument instrument)
  144: public void Remove(RecorderEntry entry)
  155: public void ToggleRecording()
  161: public void StartRecording()
  189: public void StopRecording(string reason)
  410: public void RefreshElapsed() =>
  418: public void Dispose()
```
