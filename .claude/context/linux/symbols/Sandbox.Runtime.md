# TradingTerminal.Sandbox.Runtime — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/ModelPortfolioAccount.cs
```cs
   12: public sealed class ModelPortfolioAccount : IModelPortfolioAccount
   27: public ModelPortfolioAccount(
   39: public ModelPortfolioAccount(
   68: public IVirtualBook Book => _book;
   75: public ModelPortfolioFault LastFault { get; private set; }
   78: public SandboxPortfolioSnapshot Snapshot => Project(_simulator.CommittedSnapshot);
   81: public void BeginBar(double close)
   92: public void BeginTick(double bid, double ask, double last)
  103: public void ReconcileToTargets()
  181: public void Commit()
  202: public void Rollback()
  211: public void Complete()
```

## src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/ModelPortfolioContracts.cs
```cs
    8: public interface IModelPortfolio
   10:     InstrumentId Instrument { get; }
   11:     double PositionUnits { get; }
   12:     double PositionQuantity { get; }
   13:     double AverageEntryPrice { get; }
   14:     long BarsHeld { get; }
   15:     double Equity { get; }
   16:     double RealizedGrossProfitLoss { get; }
   17:     double CommissionTotal { get; }
   18:     double SlippageTotal { get; }
   19:     double EquityPeak { get; }
   20:     double MaximumDrawdown { get; }
   21:     long LifetimeClosedTripCount { get; }
   22:     long LifetimeWinningTripCount { get; }
   23:     long LifetimeLosingTripCount { get; }
   24:     long RetainedTradeCount { get; }
   25:     long Streak { get; }
   26:     bool IsComplete { get; }
   27:     double? ProtectiveStopPrice => null;
   28:     double? ProfitTargetPrice => null;
   31:     PendingEntryState? PendingEntry => null;
   38: public readonly record struct PendingEntryState(
   44: public interface IModelPortfolioSource
   46:     IModelPortfolio? CurrentSnapshot { get; }
   52:     IReadOnlyList<IModelPortfolio> CurrentSnapshots =>
   53:     CurrentSnapshot is { } snapshot ? [snapshot] : [];
   55:     event Action<IModelPortfolio>? SnapshotChanged;
   59: public readonly record struct SandboxPortfolioSnapshot(
   82: public sealed record ModelPortfolioAccountConfig(
   87: public interface IModelPortfolioAccount
   89:     IVirtualBook Book { get; }
   90:     ModelPortfolioFault LastFault { get; }
   91:     SandboxPortfolioSnapshot Snapshot { get; }
   94:     IReadOnlyList<SandboxPortfolioSnapshot> Snapshots => [Snapshot];
   96:     void BeginBar(double close);
   97:     void BeginTick(double bid, double ask, double last);
  103:     void BeginBar(InstrumentId instrument, double close) => BeginBar(close);
  109:     void BeginTick(InstrumentId instrument, double bid, double ask, double last) =>
  110:     BeginTick(bid, ask, last);
  112:     void ReconcileToTargets();
  113:     void Commit();
  114:     void Rollback();
  115:     void Complete();
```

## src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/MultiInstrumentModelPortfolioAccount.cs
```cs
   12: public sealed class MultiInstrumentModelPortfolioAccount : IModelPortfolioAccount
   22: public MultiInstrumentModelPortfolioAccount(
   44: public IVirtualBook Book => _book;
   46: public ModelPortfolioFault LastFault { get; private set; }
   52: public SandboxPortfolioSnapshot Snapshot => _accounts[_currentInstrument].Snapshot;
   54: public IReadOnlyList<SandboxPortfolioSnapshot> Snapshots =>
   57: public void BeginBar(double close) => BeginBar(_currentInstrument, close);
   59: public void BeginTick(double bid, double ask, double last) =>
   62: public void BeginBar(InstrumentId instrument, double close) =>
   65: public void BeginTick(InstrumentId instrument, double bid, double ask, double last) =>
   68: public void ReconcileToTargets()
  106: public void Commit()
  125: public void Rollback()
  133: public void Complete()
```

## src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/PaperExecutionBookTargetIntake.cs
```cs
    9: public delegate bool TryResolvePaperReferencePrice(
   15: public sealed record PaperExecutionBookTargetOptions
   17: public PaperExecutionBookTargetOptions(
   74: public string BookId { get; }
   75: public StrategyId StrategyId { get; }
   76: public StrategyVersion StrategyVersion { get; }
   77: public ExecutionResource Resource { get; }
   78: public ExecutionLeaseClaim LeaseClaim { get; }
   79: public RiskLimits RiskLimits { get; }
   80: public ScaledMoney AvailableBuyingPower { get; }
   81: public ScaledMoney DailyNetRealizedPnl { get; }
   82: public ScaledMoney CurrentEquity { get; }
   83: public ScaledMoney PeakEquity { get; }
   84: public TimeSpan MaximumReferencePriceAge { get; }
   85: public RiskControlMode ControlMode { get; }
   86: public bool KillSwitchActive { get; }
   87: public ScaledRatio ContractMultiplier { get; }
   88: public string AccountCurrency { get; }
   99: public sealed class PaperExecutionBookTargetIntake : IExecutionBookTargetIntake, IDisposable
  110: public PaperExecutionBookTargetIntake(
  124: public async ValueTask<ExecutionTargetSubmissionResult> SubmitTargetAsync(
  162: public void Dispose()
```

## src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/RecordingVirtualBook.cs
```cs
   10: public sealed class RecordingVirtualBook : IVirtualBook
   16: public RecordingVirtualBook(IReadOnlySet<InstrumentId> declaredInstruments)
   26: public IReadOnlyCollection<VirtualTargetIntent> RecordedIntents => _recordedIntents.Values;
   29: public void SubmitTarget(VirtualTargetIntent intent)
   38: public void Reset() => _recordedIntents.Clear();
```

## src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/SandboxExecutionReplicator.cs
```cs
    8: public readonly record struct ExecutionTargetSubmissionResult(bool IsSuccess, string Message)
   10: public static ExecutionTargetSubmissionResult Success(string message) => new(true, message);
   12: public static ExecutionTargetSubmissionResult Failure(string message) => new(false, message);
   19: public interface IExecutionBookTargetIntake
   21:     ValueTask<ExecutionTargetSubmissionResult> SubmitTargetAsync(
   22:     string bookId,
   23:     TradeIntent intent,
   24:     CancellationToken cancellationToken = default);
   28: public sealed record SandboxExecutionReplicationOptions(
   36: public readonly record struct SandboxExecutionReplicationOutcome(
   45: public sealed class SandboxExecutionReplicator : IDisposable, IAsyncDisposable
   47: public const string DefaultPolicyVersion = "sandbox-model-portfolio-v1";
   64: public SandboxExecutionReplicator(
  113: public bool IsEnabled => _options.Enabled;
  115: public SandboxExecutionReplicationOutcome? LastOutcome
  124: public event Action<SandboxExecutionReplicationOutcome>? SubmissionCompleted;
  127: public bool ReplicateCurrent()
  135: public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
  137: public async ValueTask DisposeAsync()
```

## src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/SandboxStrategyContext.cs
```cs
    7: public sealed class SandboxStrategyContext : IStrategyRuntimeContext, IDisposable
   11: public SandboxStrategyContext(
   25: public IMarketDataView Data { get; }
   27: public IClock Clock { get; }
   29: public IParameters Parameters { get; }
   31: public IVirtualBook Book { get; }
   33: public IAlertSink Alerts { get; }
   36: public void Dispose()
```

## src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/SandboxStrategyRuntime.cs
```cs
   14: public enum SandboxStrategyRuntimeState
   31: public sealed class SandboxStrategyRuntime :
   40: public const int DefaultRetentionBound = ScopedMarketDataView.DefaultRetentionBound;
   76: public SandboxStrategyRuntime(
  118: public SandboxStrategyRuntimeState State =>
  122: public bool IsRunning => State is SandboxStrategyRuntimeState.Running or SandboxStrategyRuntimeState.Paused;
  125: public bool IsPaused => State == SandboxStrategyRuntimeState.Paused;
  128: public int QueueCapacity => _retentionBound;
  131: public long DroppedEventCount => Interlocked.Read(ref _droppedEventCount);
  134: public IModelPortfolio? CurrentSnapshot => Volatile.Read(ref _currentSnapshot);
  137: public IReadOnlyList<IModelPortfolio> CurrentSnapshots => Volatile.Read(ref _currentSnapshots);
  143: public event Action<IModelPortfolio>? SnapshotChanged;
  150: public bool TryDraw(IRenderSurface surface)
  183: public void SetParameter(string key, object? value)
  199: public async Task RunAsync(CancellationToken ct = default)
  229: public void Pause() => PauseAsync().GetAwaiter().GetResult();
  232: public async Task PauseAsync(CancellationToken ct = default)
  256: public async Task ResumeAsync(CancellationToken ct = default)
  291: public Task StopAsync(CancellationToken ct = default)
  297: public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();
  299: public async ValueTask DisposeAsync()
 1012: public IStrategyKernel Kernel { get; } = kernel;
 1013: public IModelPortfolioAccount Account { get; } = account;
 1014: public SandboxStrategyContext Context { get; } = context;
 1015: public DeferredVirtualBook Book { get; } = book;
 1016: public IReadOnlySet<InstrumentId> Instruments { get; } = instruments;
 1017: public Channel<MarketEventEnvelope> Queue { get; } = queue;
 1018: public CancellationTokenSource PumpCancellation { get; } = new();
 1019: public List<IDisposable> Subscriptions { get; } = new();
 1020: public Task PumpTask { get; set; } = Task.CompletedTask;
 1021: public bool KernelStarted { get; set; }
 1022: public int PumpStopped;
 1040: public DeferredVirtualBook(IReadOnlySet<InstrumentId> instruments, IVirtualBook inner)
 1048: public void SubmitTarget(VirtualTargetIntent intent)
 1062: public void OpenWindow()
 1072: public void CommitWindow()
 1081: public void RejectWindow()
 1090: public bool TryRollbackWindow()
 1102: public void BeginDeferredCallback()
 1113: public void CommitDeferredCallback()
 1122: public void RollbackDeferredCallback()
 1137: public void DiscardPending()
 1151: public void OnCompleted() { }
 1153: public void OnError(Exception error) => onError();
 1155: public void OnNext(T value) => onNext(value);
 1172: public static MarketEventEnvelope Quote(SandboxStrategyRuntime owner, Quote quote) =>
 1175: public static MarketEventEnvelope Trade(SandboxStrategyRuntime owner, TradePrint trade) =>
 1178: public static MarketEventEnvelope Depth(
 1184: public static MarketEventEnvelope Bar(SandboxStrategyRuntime owner, OhlcvBar bar) =>
```
