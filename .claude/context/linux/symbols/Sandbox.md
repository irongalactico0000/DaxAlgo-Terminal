# TradingTerminal.Sandbox — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Pipeline/TradingTerminal.Sandbox/LegacyStrategyKernelAdapter.cs
```cs
   18: public sealed class LegacyStrategyKernelAdapter : DaxAlgo.Sdk.IStrategyKernel, IAsyncDisposable
   32: public LegacyStrategyKernelAdapter(
   40: public LegacyStrategyKernelAdapter(
   56: public StrategyParameterSchema Schema { get; }
   58: public StrategyDataRequirement DataRequirement { get; }
   60: public async Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct)
  120: public async Task OnQuoteAsync(
  141: public async Task OnBarAsync(
  156: public async Task OnTradeAsync(
  171: public async Task OnDepthAsync(
  187: public async Task OnStopAsync(IStrategyRuntimeContext context, CancellationToken ct)
  221: public async ValueTask DisposeAsync()
```

## src/linux/Pipeline/TradingTerminal.Sandbox/MediatedAlertSink.cs
```cs
    7: public sealed record AlertRecord(
   19: public sealed class MediatedAlertSink : IAlertSink
   21: public const int DefaultMaxAlertsPerWindow = 20;
   23: public static TimeSpan DefaultWindow { get; } = TimeSpan.FromSeconds(10);
   37: public MediatedAlertSink(
   66: public void Alert(string message, AlertLevel level, string? dedupeKey = null)
  113: public void AlertIf(bool condition, string message, AlertLevel level, string? dedupeKey = null)
```

## src/linux/Pipeline/TradingTerminal.Sandbox/PaperMarketDataExecutionBridge.cs
```cs
    7: public enum PaperMarketDataBridgeFaultKind : byte
   17: public sealed record PaperMarketDataBridgeFault(
   28: public sealed class PaperMarketDataExecutionBridge : IDisposable
   30: public const byte DefaultPriceScale = 8;
   47: public PaperMarketDataExecutionBridge(
   94: public long ProcessedQuoteCount => Interlocked.Read(ref _processedQuoteCount);
   95: public long RejectedQuoteCount => Interlocked.Read(ref _rejectedQuoteCount);
   96: public bool IsExecutionFaulted => Volatile.Read(ref _executionFaulted) != 0;
   97: public PaperMarketDataBridgeFault? LastFault => Volatile.Read(ref _lastFault);
  103: public bool TryGetLatestReferencePrice(
  121: public void Dispose()
  304: public void OnCompleted() { }
  305: public void OnError(Exception error) => onError(error);
  306: public void OnNext(Quote value) => onNext(value);
```

## src/linux/Pipeline/TradingTerminal.Sandbox/PositionTrackingOrderRouter.cs
```cs
   30: public sealed class PositionTrackingOrderRouter : IOrderRouter, IDisposable
   32: public const int DefaultMaxTrackedOrders = 4096;
   33: public const int MaxClientOrderIdLength = 128;
   34: public const long MaxExactTargetUnits = 9_007_199_254_740_992L;
   51: public PositionTrackingOrderRouter(
   79: public InstrumentId Instrument => _instrument;
   82: public long NetPosition
   92: public double? ReferencePrice
  102: public IObservable<OrderEvent> OrderEvents => _events;
  108: public bool TryUpdateReferencePrice(double referencePrice)
  135: public async Task<OrderResult> PlaceOrderAsync(
  260: public async Task CancelOrderAsync(string clientOrderId, CancellationToken ct = default)
  331: public void Dispose()
  466: public OrderResult Result { get; } = result;
  467: public OrderSide Side { get; } = side;
  468: public long SignedQuantity { get; } = signedQuantity;
  469: public long Quantity { get; } = quantity;
  470: public double? FillPrice { get; } = fillPrice;
  471: public double? ProtectiveStopPrice { get; } = protectiveStopPrice;
  472: public double? ProfitTargetPrice { get; } = profitTargetPrice;
  473: public long Sequence { get; } = sequence;
  474: public bool Cancelled { get; set; }
```

## src/linux/Pipeline/TradingTerminal.Sandbox/SandboxParameters.cs
```cs
   11: public sealed class SandboxParameters : IParameters
   15: public SandboxParameters(
   26: public SandboxParameters(StrategyParameters currentValues)
   32: public StrategyParameterSchema Schema => _values.Schema;
   34: public int GetInt(string name)
   40: public long GetLong(string name)
   46: public double GetDouble(string name)
   52: public bool GetBool(string name)
   58: public string GetString(string name)
   67: public string GetText(string name)
   73: public TEnum GetEnum<TEnum>(string name) where TEnum : struct, Enum
   79: public InstrumentId GetInstrument(string name)
```

## src/linux/Pipeline/TradingTerminal.Sandbox/SandboxVisualizerContext.cs
```cs
   11: public sealed class SandboxVisualizerContext : IVisualizerContext, IDisposable
   16: public SandboxVisualizerContext(
   44: public IMarketDataView Data { get; }
   46: public IClock Clock { get; }
   48: public IParameters Parameters { get; }
   50: public IAlertSink Alerts { get; }
   53: public void Dispose()
   64: public static class SandboxVisualizerContextFactory
   66: public static SandboxVisualizerContext Create(
```

## src/linux/Pipeline/TradingTerminal.Sandbox/SandboxVisualizerRuntime.cs
```cs
   13: public enum SandboxVisualizerRuntimeState
   27: public sealed class SandboxVisualizerRuntime :
   34: public const int DefaultRetentionBound = ScopedMarketDataView.DefaultRetentionBound;
   67: public SandboxVisualizerRuntime(
  106: public SandboxVisualizerRuntimeState State =>
  110: public bool IsRunning =>
  115: public bool IsPaused => State == SandboxVisualizerRuntimeState.Paused;
  118: public int QueueCapacity => _retentionBound;
  121: public long DroppedEventCount => Interlocked.Read(ref _droppedEventCount);
  124: public long DrawFaultCount => Interlocked.Read(ref _drawFaultCount);
  127: public long SkippedFrameCount => Interlocked.Read(ref _skippedFrameCount);
  141: public bool TryDraw(IRenderSurface surface)
  175: public void SetParameter(string key, object? value)
  193: public async Task StartAsync(CancellationToken ct = default)
  223: public void Pause() => PauseAsync().GetAwaiter().GetResult();
  226: public async Task PauseAsync(CancellationToken ct = default)
  251: public void Resume() => ResumeAsync().GetAwaiter().GetResult();
  254: public async Task ResumeAsync(CancellationToken ct = default)
  291: public Task StopAsync(CancellationToken ct = default)
  298: public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
  300: public ValueTask DisposeAsync()
  912: public bool IsActiveFor(SandboxVisualizerRuntime candidate) =>
  915: public void Clear() => Volatile.Write(ref _runtime, null);
  925: public IVisualizer Visualizer { get; } = visualizer;
  926: public SandboxVisualizerContext Context { get; } = context;
  927: public IReadOnlySet<InstrumentId> Instruments { get; } = instruments;
  928: public StrategyDataRequirement DataRequirement { get; } = dataRequirement;
  929: public Channel<MarketEventEnvelope> Queue { get; } = queue;
  930: public CancellationTokenSource PumpCancellation { get; } = new();
  931: public List<IDisposable> Subscriptions { get; } = new();
  932: public Task PumpTask { get; set; } = Task.CompletedTask;
  933: public bool VisualizerStarted { get; set; }
  934: public int PumpStopped;
  939: public void OnCompleted() { }
  941: public void OnError(Exception error) => onError();
  943: public void OnNext(T value) => onNext(value);
  960: public static MarketEventEnvelope Quote(SandboxVisualizerRuntime owner, Quote quote) =>
  963: public static MarketEventEnvelope Trade(SandboxVisualizerRuntime owner, TradePrint trade) =>
  966: public static MarketEventEnvelope Depth(
  972: public static MarketEventEnvelope Bar(SandboxVisualizerRuntime owner, OhlcvBar bar) =>
```

## src/linux/Pipeline/TradingTerminal.Sandbox/ScopedMarketDataView.cs
```cs
   15: public sealed class ScopedMarketDataView : IMarketDataView, IDisposable
   17: public const int DefaultRetentionBound = 512;
   31: public ScopedMarketDataView(
   65: public IReadOnlySet<InstrumentId> Instruments { get; }
   67: public StrategyDataRequirement DataRequirement { get; }
   69: public IReadOnlyList<OhlcvBar> RecentBars(InstrumentId instrument, BarSize size, int maxCount) =>
   75: public IReadOnlyList<Quote> RecentQuotes(InstrumentId instrument, int maxCount) =>
   81: public DepthSnapshot? LatestDepth(InstrumentId instrument) =>
   87: public IReadOnlyList<TradePrint> RecentTrades(InstrumentId instrument, int maxCount) =>
   93: public void Dispose()
  231: public void OnCompleted() { }
  233: public void OnError(Exception error) { }
  235: public void OnNext(T value) => onNext(value);
  242: public DepthSnapshot? Read() => Volatile.Read(ref _value);
  244: public void Write(DepthSnapshot value) => Volatile.Write(ref _value, value);
  254: public BoundedRingBuffer(int capacity) => _items = new T[capacity];
  256: public void Add(T item)
  272: public IReadOnlyList<T> Snapshot(int maxCount)
```

## src/linux/Pipeline/TradingTerminal.Sandbox/SdkStrategyBacktestAdapter.cs
```cs
   14: public sealed record SdkBacktestInstrument(InstrumentId InstrumentId, Contract Contract);
   21: public sealed class SdkStrategyBacktestAdapter :
   46: public SdkStrategyBacktestAdapter(
   57: public SdkStrategyBacktestAdapter(
   84: public long Position => _positions.Values.Sum();
   86: public long PositionFor(InstrumentId instrument) =>
   91: public async Task OnStartAsync(IClock clock, IOrderRouter router, CancellationToken ct)
  110: public Task OnTickAsync(Tick tick, IClock clock, IOrderRouter router, CancellationToken ct) =>
  115: public Task OnBarAsync(Bar bar, IClock clock, IOrderRouter router, CancellationToken ct) =>
  121: public Task OnTradeAsync(TradePrint trade, IClock clock, IOrderRouter router, CancellationToken ct) =>
  126: public async Task OnMarketEventBatchAsync(
  207: public Task OnOrderEventAsync(OrderEvent evt, CancellationToken ct)
  233: public async Task OnEndAsync(IClock clock, IOrderRouter router, CancellationToken ct)
  249: public async ValueTask DisposeAsync()
  369: public void SubmitTarget(VirtualTargetIntent intent)
  377: public IReadOnlyList<VirtualTargetIntent> TakeAll()
  400: public ReplayMarketDataView(
  410: public IReadOnlySet<InstrumentId> Instruments { get; }
  411: public StrategyDataRequirement DataRequirement { get; }
  412: public long NextSequence() => _sequence++;
  414: public IReadOnlyList<OhlcvBar> RecentBars(InstrumentId instrument, BarSize size, int maxCount)
  420: public IReadOnlyList<Quote> RecentQuotes(InstrumentId instrument, int maxCount)
  426: public DepthSnapshot? LatestDepth(InstrumentId instrument)
  432: public IReadOnlyList<TradePrint> RecentTrades(InstrumentId instrument, int maxCount)
  438: public void Add(OhlcvBar bar) => AddBounded(_bars, bar);
  439: public void Add(Quote quote) => AddBounded(_quotes, quote);
  440: public void Add(TradePrint trade) => AddBounded(_trades, trade);
  442: public OhlcvBar LatestBar(InstrumentId instrument, BarSize size, DateTime timestamp) =>
  445: public Quote LatestQuote(InstrumentId instrument, DateTime timestamp) =>
  448: public TradePrint LatestTrade(InstrumentId instrument, DateTime timestamp) =>
```
