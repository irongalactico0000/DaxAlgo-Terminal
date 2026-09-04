# TradingTerminal.UI.Core — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/UI/TradingTerminal.UI.Core/BarIndicators.cs
```cs
   14: public static class BarIndicators
   16: public static double[] Sma(IReadOnlyList<Bar> bars, int period)
   29: public static double[] Ema(IReadOnlyList<Bar> bars, int period)
   43: public static (double[] mean, double[] sd, double[] upper, double[] lower) Bollinger(
   66: public static double[] Rsi(IReadOnlyList<Bar> bars, int period)
   79: public static (double[] macd, double[] signal) Macd(
  102: public static double[] ZScore(IReadOnlyList<Bar> bars, int window)
  118: public static double[] RealisedVol(IReadOnlyList<Bar> bars, int window)
  132: public static double[] Atr(IReadOnlyList<Bar> bars, int period)
  144: public static double[] BarTimestamps(IReadOnlyList<Bar> bars)
  151: public static double[] BarCloses(IReadOnlyList<Bar> bars)
```

## src/linux/UI/TradingTerminal.UI.Core/BrokerInstrumentUniverse.cs
```cs
   24: public static class BrokerInstrumentUniverse
   33: public static async Task<IReadOnlyList<SignalInstrument>> LoadAsync(
   66: public static string BrokerLabel(BrokerKind broker) => broker switch
   78: public static SignalInstrument? Reselect(
```

## src/linux/UI/TradingTerminal.UI.Core/BusyState.cs
```cs
   28: public sealed partial class BusyState : ObservableObject
   43: public IDisposable Begin(string title, string? message = null)
   53: public void Report(string message) => Message = message;
   67: public void Dispose()
```

## src/linux/UI/TradingTerminal.UI.Core/Catalog/StrategyCatalogViewModel.cs
```cs
   13: public sealed partial class StrategyCatalogViewModel : ObservableObject
   17: public StrategyCatalogViewModel(IEnumerable<BacktestStrategyOption> options, Action<string>? onLog = null)
   29: public ObservableCollection<StrategyCatalogItem> Items { get; }
   31: public int Count => Items.Count;
   37: public string Details => SelectedItem is { } s
   52: public sealed record StrategyCatalogItem(string Id, string DisplayName, int ParameterCount, bool Fast);
```

## src/linux/UI/TradingTerminal.UI.Core/Controls/Render/AuthoredUnitHost.cs
```cs
   22: public sealed class AuthoredUnitHost : IDisposable
   25: public static readonly TimeSpan DefaultFrameInterval = TimeSpan.FromMilliseconds(33);
   40: public AuthoredUnitHost(
   77: public AuthoredUnitPresenter Presenter { get; }
   83: public void Freeze()
   88: public void Dispose()
```

## src/linux/UI/TradingTerminal.UI.Core/Controls/Render/AuthoredUnitPresenter.cs
```cs
   11: public readonly record struct AuthoredUnitLogLine(DateTime TimestampUtc, string Source, string Message);
   19: public readonly record struct AuthoredUnitBook(
   37: public sealed partial class AuthoredUnitPresenter : ObservableObject
   40: public const int MaximumLogLines = 500;
   65: public ObservableCollection<AuthoredUnitParameter> Parameters { get; } = [];
   74: public event EventHandler? FrameRequested;
   77: public void RequestFrame() => FrameRequested?.Invoke(this, EventArgs.Empty);
   80: public ObservableCollection<AuthoredUnitLogLine> Log { get; } = [];
   87: public void Append(AuthoredUnitLogLine line)
   96: public sealed partial class AuthoredUnitParameter : ObservableObject
```

## src/linux/UI/TradingTerminal.UI.Core/Diagnostics/PluginFaultTracker.cs
```cs
   10: public sealed class PluginFaultTracker(int strikeLimit)
   16: public int StrikeLimit { get; } = strikeLimit;
   20: public (int Strikes, bool StruckOutNow) RecordFault(string plugin)
```

## src/linux/UI/TradingTerminal.UI.Core/Execution/PaperExecutionClient.cs
```cs
    7: public readonly record struct PaperExecutionClientResult(
   12: public static PaperExecutionClientResult Success(string message) => new(true, message);
   13: public static PaperExecutionClientResult Failure(ExecutionServiceFault fault, string message) =>
   18: public sealed record PaperRiskDecisionSnapshot(
   26: public sealed record PaperExecutionQualitySnapshot(
   38: public double FillRatePercent => Orders == 0 ? 0d : FilledOrders * 100d / Orders;
   39: public double RejectRatePercent => Orders == 0 ? 0d : Rejects * 100d / Orders;
   40: public double AverageSlippageTicks =>
   42: public double AverageAcknowledgementLatencyMilliseconds =>
   49: public sealed record PaperExecutionClientSnapshot(
   67: public IReadOnlyList<ReconciliationCase> CaseFacts =>
   69: public int UnresolvedMaterialCaseCount => CaseFacts.Count(item =>
   71: public IReadOnlyList<PaperRiskDecisionSnapshot> RiskDecisionFacts =>
   73: public PaperExecutionQualitySnapshot QualityFacts => ExecutionQuality ?? new(
   75: public bool AdmissionOpen => LeaseHeld && !IntakePaused && !ReconciliationAdmissionBlocked;
   79: public interface IPaperExecutionFlattenOrderFactory
   81:     bool TryCreateFlattenOrder(
   82:     ReconciliationPositionSnapshot position,
   83:     ExecutionLeaseGrant leaseGrant,
   84:     DateTimeOffset createdAtUtc,
   85:     out ExecutionSubmitRequest? request,
   86:     out string? reason);
   89: public interface IPaperExecutionClient : IDisposable
   91:     event EventHandler? SnapshotInvalidated;
   92:     PaperExecutionClientSnapshot GetSnapshot();
   93:     ValueTask<PaperExecutionClientResult> RefreshAsync(CancellationToken cancellationToken = default);
   94:     ValueTask<PaperExecutionClientResult> SetIntakePausedAsync(bool paused, CancellationToken cancellationToken = default);
   95:     ValueTask<PaperExecutionClientResult> SubmitAsync(ExecutionSubmitRequest request, CancellationToken cancellationToken = default);
   96:     ValueTask<PaperExecutionClientResult> CancelAsync(ClientOrderId clientOrderId, CancellationToken cancellationToken = default);
   97:     ValueTask<PaperExecutionClientResult> ReplaceAsync(ClientOrderId clientOrderId, OrderTerms replacementTerms, RiskEvaluationContext riskContext, CancellationToken...
   98:     ValueTask<PaperExecutionClientResult> ReconcileAsync(CancellationToken cancellationToken = default);
   99:     ValueTask<PaperExecutionClientResult> ResolveReconciliationCaseAsync(
  100:     ReconciliationCaseId caseId,
  101:     string resolvedBy,
  102:     string resolutionEvidence,
  103:     CancellationToken cancellationToken = default);
  104:     ValueTask<PaperExecutionClientResult> KillAsync(CancellationToken cancellationToken = default);
  111: public sealed class PaperExecutionClient : IPaperExecutionClient
  131: public PaperExecutionClient(
  148: public event EventHandler? SnapshotInvalidated;
  150: public PaperExecutionClientSnapshot GetSnapshot()
  159: public ValueTask<PaperExecutionClientResult> RefreshAsync(CancellationToken cancellationToken = default)
  173: public ValueTask<PaperExecutionClientResult> SetIntakePausedAsync(
  188: public ValueTask<PaperExecutionClientResult> SubmitAsync(
  213: public ValueTask<PaperExecutionClientResult> CancelAsync(
  229: public ValueTask<PaperExecutionClientResult> ReplaceAsync(
  280: public ValueTask<PaperExecutionClientResult> ReconcileAsync(CancellationToken cancellationToken = default)
  299: public ValueTask<PaperExecutionClientResult> ResolveReconciliationCaseAsync(
  336: public ValueTask<PaperExecutionClientResult> KillAsync(CancellationToken cancellationToken = default)
  353: public void Dispose()
  759: public OrderEventAppendResult Append(OrderEventDraft draft, DateTimeOffset recordedAtUtc) =>
  762: public IReadOnlyList<OmsOrderEvent> Read(ClientOrderId aggregateId) =>
  767: public OmsOrderProjection? ReadProjection(ClientOrderId aggregateId) =>
  770: public IReadOnlyList<OrderEventOutboxEntry> ReadOutbox(long afterExclusiveSequence = 0) =>
```

## src/linux/UI/TradingTerminal.UI.Core/Execution/PaperExecutionConsoleViewModel.cs
```cs
   11: public sealed record PaperExecutionInstrumentChoice(
   18: public string DisplayName => string.IsNullOrWhiteSpace(Exchange)
   23: public sealed record PaperOrderTicketDraft(
   38: public interface IPaperExecutionOrderFactory
   40:     IReadOnlyList<PaperExecutionInstrumentChoice> Instruments { get; }
   42:     bool TryCreateSubmit(
   43:     PaperOrderTicketDraft draft,
   44:     PaperExecutionClientSnapshot snapshot,
   45:     out ExecutionSubmitRequest? request,
   46:     out string? reason);
   48:     bool TryCreateReplacementTerms(
   49:     PaperOrderTicketDraft draft,
   50:     out OrderTerms? terms,
   51:     out string? reason);
   53:     RiskEvaluationContext CreateReplacementRisk(
   54:     PaperOrderTicketDraft draft,
   55:     OmsOrderProjection order,
   56:     PaperExecutionClientSnapshot snapshot);
   59: public sealed record PaperOrderRow(
   74: public sealed record PaperPositionRow(string Instrument, string Quantity, string Direction, string ObservedUtc);
   75: public sealed record PaperFillRow(string TradeId, string Instrument, string Side, string Quantity, string Price, string Fee, string OccurredUtc);
   76: public sealed record PaperCashRow(string Currency, string Total, string Available, string ObservedUtc);
   77: public sealed record PaperPortfolioSummaryRow(
   88: public sealed record PaperPerformancePeriodRow(
   97: public sealed record PaperExposureRow(
  105: public sealed record PaperEventRow(string Sequence, string ClientId, string Kind, string State, string Source, string RecordedUtc);
  106: public sealed record PaperExecutionQualityRow(
  118: public sealed record PaperRiskDecisionRow(
  140: public sealed record PaperReconciliationCaseRow(
  156: public sealed partial class PaperExecutionConsoleViewModel : ObservableObject, IDisposable
  163: public PaperExecutionConsoleViewModel(
  178: public IReadOnlyList<PaperExecutionInstrumentChoice> Instruments { get; }
  179: public IReadOnlyList<OrderSide> Sides { get; } = Enum.GetValues<OrderSide>();
  180: public IReadOnlyList<OrderType> OrderTypes { get; } = Enum.GetValues<OrderType>();
  181: public IReadOnlyList<TimeInForce> TimeInForces { get; } = Enum.GetValues<TimeInForce>();
  183: public ObservableCollection<PaperOrderRow> Orders { get; } = [];
  184: public ObservableCollection<PaperPositionRow> Positions { get; } = [];
  185: public ObservableCollection<PaperFillRow> Fills { get; } = [];
  186: public ObservableCollection<PaperCashRow> Cash { get; } = [];
  187: public ObservableCollection<PaperPerformancePeriodRow> PerformancePeriods { get; } = [];
  188: public ObservableCollection<PaperExposureRow> PortfolioExposures { get; } = [];
  189: public ObservableCollection<PaperEventRow> Events { get; } = [];
  190: public ObservableCollection<PaperRiskDecisionRow> RiskDecisions { get; } = [];
  191: public ObservableCollection<PaperReconciliationCaseRow> ReconciliationCases { get; } = [];
  254: public bool IsMarketOrder => SelectedOrderType == OrderType.Market;
  255: public bool NeedsLimitPrice => SelectedOrderType is OrderType.Limit or OrderType.StopLimit;
  256: public bool NeedsStopPrice => SelectedOrderType is OrderType.Stop or OrderType.StopLimit;
  257: public bool CanIssueCommands => LeaseHeld && !IntakePaused && !ReconciliationAdmissionBlocked && !IsBusy;
  258: public string IntakeActionText => IntakePaused ? "Resume intake" : "Pause intake";
  259: public string KillActionText => KillConfirmationArmed ? "CONFIRM KILL + FLATTEN" : "Arm kill + flatten";
  642: public void Dispose()
```

## src/linux/UI/TradingTerminal.UI.Core/Execution/PaperPortfolioAnalytics.cs
```cs
    7: public enum PaperExecutionTimeRange : byte
   15: public enum PaperMarkBasis : byte
   21: public sealed record PaperClosedTradeSnapshot(
   27: public sealed record PaperInstrumentExposureSnapshot(
   36: public sealed record PaperDailyProfitAndLossSnapshot(DateTime DateUtc, decimal RealizedProfitAndLoss);
   38: public sealed record PaperEquityPointSnapshot(DateTime DateUtc, decimal Equity);
   40: public sealed record PaperPerformancePeriodSnapshot(
   60: public sealed record PaperPortfolioAnalyticsSnapshot(
   76: public PaperPerformancePeriodSnapshot Period(PaperExecutionTimeRange range) =>
   80: public static class PaperPortfolioAnalyticsCalculator
   91: public static PaperPortfolioAnalyticsSnapshot Calculate(
  209: public static double AnnualizedSharpe(IReadOnlyList<double> periodicReturns)
```

## src/linux/UI/TradingTerminal.UI.Core/ISignalGeneratorRouterFactory.cs
```cs
    9: public interface ISignalGeneratorRouterFactory
   11:     SignalGeneratorRouter Create();
   15: public sealed class SignalGeneratorRouterFactory : ISignalGeneratorRouterFactory
   17: public SignalGeneratorRouter Create() => new();
```

## src/linux/UI/TradingTerminal.UI.Core/InstrumentPickerFilter.cs
```cs
   20: public static class InstrumentPickerFilter
   26: public bool IsApplying { get; set; }
   27: public object? PendingDesired { get; set; }
   31: public static List<SignalInstrument> Visible(
   51: public static List<T> Visible<T>(
   71: public static List<SignalInstrument> Visible(
   91: public static void Apply<T>(ObservableCollection<T> target, IReadOnlyList<T> desired)
  166: public static SignalInstrument? Remembered(string key, IReadOnlyList<SignalInstrument> all)
  172: public static T? Remembered<T>(string key, IReadOnlyList<T> all, Func<T, string> symbolOf) where T : class
  184: public static SignalInstrument? InitialSelection(
  189: public static T? InitialSelection<T>(
```

## src/linux/UI/TradingTerminal.UI.Core/LastInstrumentStore.cs
```cs
   17: public static class LastInstrumentStore
   28: public static string? Load(string key)
   40: public static void Save(string key, string? symbol)
```

## src/linux/UI/TradingTerminal.UI.Core/LiveSignalStrategyViewModelBase.cs
```cs
   41: public abstract partial class LiveSignalStrategyViewModelBase : ViewModelBase, IDisposable
   43: public const int MaxSignalsRetained = 200;
   44: public const int MaxBarsRetained = 300;
   48: public const int MaxInstrumentsDisplayed = 500;
   58: protected virtual int WarmupBarCount => 120;
   92: protected LiveSignalStrategyViewModelBase(
  148: public string StrategyId { get; }
  149: public string StrategyDisplayName { get; }
  153: public IReadOnlyList<SignalInstrument> AllInstruments { get; private set; }
  155: public ObservableCollection<SignalEntry> Signals { get; }
  158: public ObservableCollection<Bar> Bars { get; }
  160: public event EventHandler? BarsChanged;
  166: public event EventHandler? TickProcessed;
  192: protected virtual StrategyDataRequirement DataRequirement =>
  277: protected virtual void OnPauseReleased() { }
  396: protected abstract IBacktestStrategy BuildStrategy(Contract contract);
  400: protected virtual string? ValidateSetup() => null;
  404: protected virtual void OnBarsUpdated() { }
  410: protected virtual Task OnWarmupBarsLoadedAsync(IReadOnlyList<Bar> bars) => Task.CompletedTask;
  414: protected void Log(string category, string message) =>
  969: public ObservableCollection<string> PresetNames { get; }
  984: protected virtual Dictionary<string, string>? CaptureExtraPreset() => null;
  988: protected virtual void ApplyExtraPreset(IReadOnlyDictionary<string, string> extras) { }
 1080: public void Dispose()
```

## src/linux/UI/TradingTerminal.UI.Core/LiveStrategyHostServices.cs
```cs
   34: public sealed record LiveStrategyHostServices(
```

## src/linux/UI/TradingTerminal.UI.Core/Logging/InMemoryLogSink.cs
```cs
   23: public sealed class InMemoryLogSink : INotifyPropertyChanged
   31: public static Action<Action> UiPost { get; set; } = static action => action();
   37: public InMemoryLogSink(int capacity = CapacityDefault)
   43: public int Capacity { get; }
   44: public ObservableCollection<LogEntry> Entries { get; }
   46: public event PropertyChangedEventHandler? PropertyChanged;
   48: public void Append(LogEntry entry)
   66: public void Append(string source, string level, string message) =>
   89: public sealed record LogEntry(DateTime TimestampUtc, string Source, string Level, string Message);
```

## src/linux/UI/TradingTerminal.UI.Core/Presets/StrategyViewPreset.cs
```cs
   13: public sealed record StrategyViewPreset(
```

## src/linux/UI/TradingTerminal.UI.Core/Presets/ToolPresetStore.cs
```cs
   16: public sealed class ToolPresetStore<T> where T : class
   24: public ToolPresetStore(string toolKey)
   32: public ToolPresetStore(string toolKey, string directory)
   39: public IReadOnlyList<string> Names
   47: public T? Get(string name)
   53: public void Save(string name, T preset)
   65: public bool Delete(string name)
```

## src/linux/UI/TradingTerminal.UI.Core/SignalEntry.cs
```cs
   11: public sealed record SignalEntry(
   21: public string SideText => DirectSignal?.Kind switch
   29: public string TypeText => DirectSignal is null ? OrderType.ToString() : "Signal";
   31: public string QuantityText => DirectSignal is { } signal
   35: public string TimeText => TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff");
```

## src/linux/UI/TradingTerminal.UI.Core/SignalGeneratorRouter.cs
```cs
   24: public sealed class SignalGeneratorRouter : IOrderRouter, IStrategySignalSink
   30: public IObservable<OrderEvent> OrderEvents => _events.AsObservable();
   32: public event Action<SignalEntry>? SignalEmitted;
   35: public void UpdateMarketContext(Tick tick) => _lastTick = tick;
   37: public Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct = default)
   70: public Task EmitSignalAsync(StrategySignal signal, CancellationToken ct = default)
  103: public Task CancelOrderAsync(string clientOrderId, CancellationToken ct = default)
```

## src/linux/UI/TradingTerminal.UI.Core/SimulatedDataState.cs
```cs
    7: public static class SimulatedDataState
   11: public static bool IsActive => _isActive;
   13: public static event EventHandler? Changed;
   15: public static void Set(bool active)
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/CatalogItemKind.cs
```cs
    3: public enum CatalogItemKind
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/IStrategyKernelRegistry.cs
```cs
    9: public sealed record StrategyKernelRegistration(
   17: public StrategyDataRequirement DataRequirement => AuthoredSpecification.DataRequirement;
   24: public interface IStrategyKernelRegistry
   26:     IReadOnlyList<StrategyKernelRegistration> All { get; }
   27:     StrategyKernelRegistration? Find(string id);
   28:     void Register(StrategyKernelRegistration registration);
   29:     bool Remove(string id);
   30:     event EventHandler? Changed;
   33: public sealed class StrategyKernelRegistry : IStrategyKernelRegistry
   38: public StrategyKernelRegistry(IEnumerable<AuthoredStrategyKernelPluginRegistration>? authoredPlugins = null)
   82: public IReadOnlyList<StrategyKernelRegistration> All
   87: public StrategyKernelRegistration? Find(string id)
   93: public void Register(StrategyKernelRegistration registration)
  100: public bool Remove(string id)
  108: public event EventHandler? Changed;
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/IVisualizerRegistry.cs
```cs
   15: public sealed record VisualizerRegistration(
   20: public string Id => Descriptor.Id;
   31: public interface IVisualizerRegistry
   33:     IReadOnlyList<VisualizerRegistration> All { get; }
   36:     VisualizerRegistration? Find(string id);
   39:     void Register(VisualizerRegistration registration);
   42:     bool Remove(string id);
   45:     event EventHandler? Changed;
   49: public sealed class VisualizerRegistry : IVisualizerRegistry
   54: public VisualizerRegistry(
  109: public IReadOnlyList<VisualizerRegistration> All
  114: public VisualizerRegistration? Find(string id)
  122: public void Register(VisualizerRegistration registration)
  129: public bool Remove(string id)
  141: public event EventHandler? Changed;
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/ParameterEditorItem.cs
```cs
   15: public sealed class ParameterEditorItem : ObservableObject
   17: public ParameterEditorItem(StrategyParameters bag, StrategyParameter parameter)
   25: public StrategyParameter Parameter { get; }
   27: public string Key => Parameter.Key;
   28: public string DisplayName => Parameter.DisplayName;
   29: public string? Description => Parameter.Description;
   30: public string? Group => Parameter.Group;
   31: public string? Unit => Parameter.Unit;
   32: public ParameterKind Kind => Parameter.Kind;
   34: public bool HasRange => Parameter.Min.HasValue && Parameter.Max.HasValue;
   35: public double Min => Parameter.Min ?? double.MinValue;
   36: public double Max => Parameter.Max ?? double.MaxValue;
   37: public double Step => Parameter.Step ?? (Kind == ParameterKind.Integer ? 1 : 0.1);
   38: public bool IsInteger => Kind == ParameterKind.Integer;
   39: public bool IsNumeric => Kind is ParameterKind.Integer or ParameterKind.Number;
   40: public bool IsBoolean => Kind == ParameterKind.Boolean;
   41: public bool IsChoice => Kind == ParameterKind.Choice;
   42: public bool IsText => Kind == ParameterKind.Text;
   43: public bool IsInstrument => Kind == ParameterKind.Instrument;
   44: public IReadOnlyList<string> Choices => Parameter.Choices ?? Array.Empty<string>();
   47: public double NumberValue
   58: public bool BoolValue
   64: public string TextValue
   71: public string SelectedChoice
   78: public string DisplayValue => Kind switch
   89: public void Refresh()
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/StrategyCatalogItemViewModel.cs
```cs
   12: public sealed partial class StrategyCatalogItemViewModel : ViewModelBase
   14: public StrategyCatalogItemViewModel(ITradingStrategy strategy)
   17: public StrategyCatalogItemViewModel(ITradingStrategy strategy, StrategyPresentation presentation)
   25: public StrategyCatalogItemViewModel(VisualizerDescriptor visualizer)
   28: public StrategyCatalogItemViewModel(VisualizerDescriptor visualizer, StrategyPresentation presentation)
   37: public StrategyCatalogItemViewModel(StrategyKernelRegistration strategyKernel)
   40: public StrategyCatalogItemViewModel(
   52: public CatalogItemKind Kind { get; } = CatalogItemKind.Strategy;
   53: public ITradingStrategy? Strategy { get; }
   54: public VisualizerDescriptor? Visualizer { get; }
   55: public StrategyKernelRegistration? StrategyKernel { get; }
   56: public bool HasLegacyStrategy => Strategy is not null;
   58: public string Id => Strategy?.Id ?? Visualizer?.Id ?? StrategyKernel!.Id;
   59: public string KindLabel => Kind == CatalogItemKind.Strategy ? "STRATEGY" : "VISUALIZER";
   60: public string PrimaryActionLabel => StrategyKernel is not null
   63: public bool HasQuickBacktest => Strategy is not null ||
   69: public IReadOnlyList<string> DataRequirementTags => Visualizer?.DataRequirementTags
   71: public bool HasDataRequirementTags => DataRequirementTags.Count != 0;
   80: public ObservableCollection<string> CustomTags { get; } = [];
   82: public bool HasFormula => !string.IsNullOrWhiteSpace(Formula);
   83: public bool HasCustomTags => CustomTags.Count > 0;
   84: public Uri? LinkUri => Uri.TryCreate(LinkUrl, UriKind.Absolute, out var uri)
   88: public bool HasLink => LinkUri is not null;
   98: public void Apply(StrategyPresentation presentation)
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/StrategyFactory.cs
```cs
   19: public sealed class StrategyFactory : IStrategyFactory
   26: public static Action<object, object> BindViewModel { get; set; } = DefaultBind;
   33: public StrategyFactory(
   43: public IReadOnlyList<ITradingStrategy> All
   48: public event EventHandler<StrategyCatalogChange>? Changed;
   50: public void Register(ITradingStrategy strategy, StrategyFactoryRegistration registration)
   74: public StrategyHost Create(string strategyId)
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/StrategyParametersViewModel.cs
```cs
   19: public sealed partial class StrategyParametersViewModel : ObservableObject
   22: public static StrategyParametersViewModel FromSchema(StrategyParameterSchema schema) =>
   25: public StrategyParametersViewModel(StrategyParameters parameters)
   33: public StrategyParameters Parameters { get; }
   35: public ObservableCollection<ParameterEditorItem> Items { get; }
   37: public bool HasParameters => Items.Count > 0;
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/StrategyPresentation.cs
```cs
   12: public sealed record StrategyPresentation(
   20: public static readonly StrategyPresentation Empty = new();
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/StrategyPresentationStore.cs
```cs
   14: public static class StrategyPresentationStore
   24: public static StrategyPresentation Get(string strategyId) =>
   29: public static void Save(string strategyId, StrategyPresentation presentation)
   40: public static void Remove(string strategyId)
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/VisualizerDescriptor.cs
```cs
    5: public sealed record VisualizerDescriptor(
```

## src/linux/UI/TradingTerminal.UI.Core/Strategies/VisualizerDescriptors.cs
```cs
   17: public static class VisualizerDescriptors
   20: public const string IdProperty = "Id";
   23: public const string DisplayNameProperty = "DisplayName";
   26: public const string DescriptionProperty = "Description";
   34: public static VisualizerRegistration FromType(Type type, string? id = null)
   63: public static IReadOnlyList<VisualizerRegistration> DiscoverIn(Assembly assembly)
   98: public static bool CanHost(Type type)
```

## src/linux/UI/TradingTerminal.UI.Core/TaskExtensions.cs
```cs
   10: public static class TaskExtensions
   13: public static void FireAndForgetSafe(this Task task, ILogger logger, string? context = null)
   24: public static void FireAndForgetSafe(this ValueTask task, ILogger logger, string? context = null)
```

## src/linux/UI/TradingTerminal.UI.Core/TradeableInstrument.cs
```cs
   15: public sealed record SignalInstrument(string DisplayName, string Category, Contract Contract, BrokerKind? Broker = null);
   30: public static class SignalInstrumentCatalog
   35: public static Func<IReadOnlyList<SignalInstrument>>? Source { get; set; }
   39: public static IReadOnlyList<SignalInstrument> All =>
   45: public static IReadOnlyList<SignalInstrument> FromRegistry(IInstrumentRegistry registry) =>
```

## src/linux/UI/TradingTerminal.UI.Core/UiFile.cs
```cs
    9: public static class UiFile
   14: public static Func<string, IReadOnlyList<string>, Task<string?>> OpenAsync { get; set; }
   19: public static Func<string, IReadOnlyList<string>, string, Task<string?>> SaveAsync { get; set; }
```

## src/linux/UI/TradingTerminal.UI.Core/UiThread.cs
```cs
   11: public static class UiThread
   17: public static Func<Func<Task>, Task> Marshal { get; set; } = static action => action();
   20: public static Task RunAsync(Func<Task> action) => Marshal(action);
   23: public static Task RunAsync(Action action) => Marshal(() => { action(); return Task.CompletedTask; });
   33: public static Func<TimeSpan, Action, IDisposable> CreateRenderTimer { get; set; } = DefaultRenderTimer;
   50: public CoalescingRenderTimer(TimeSpan interval, Action tick)
   85: public void Dispose()
```

## src/linux/UI/TradingTerminal.UI.Core/ViewModelBase.cs
```cs
    6: public abstract class ViewModelBase : ObservableObject
```
