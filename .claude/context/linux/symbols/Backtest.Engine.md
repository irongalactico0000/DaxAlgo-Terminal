# TradingTerminal.Backtest.Engine — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Accounting/Portfolio.cs
```cs
   23: public Portfolio(double startingCash, IReadOnlyDictionary<InstrumentId, double> multipliers, IFeeModel feeModel)
   30: public double Cash { get; private set; }
   31: public double TotalFees { get; private set; }
   32: public double TotalRealizedPnl => _books.Values.Sum(static book => book.RealizedPnl);
   33: public IReadOnlyList<RoundTripTrade> Trades => _trades;
   38: public void OnFill(InstrumentId id, DateTime utc, OrderSide side, long qty, double price, LiquidityFlag liquidity)
   87: public void OnMark(InstrumentId id, double mark)
  107: public double Equity()
  115: public Position SnapshotOf(InstrumentId id)
  126: public IReadOnlyCollection<Position> OpenPositions() =>
  134: public Queue<Lot> Lots = new();
  135: public long NetPosition;
  136: public double Mark;
  137: public double RealizedPnl;
  139: public double AveragePrice
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/BacktestEngine.cs
```cs
   23: public sealed class BacktestEngine
   27: public BacktestEngine(IMarketDataFeed feed) => _feed = feed;
   29: public async Task<BacktestReport> RunAsync(RunSpec spec, IStrategyKernel kernel, CancellationToken ct = default)
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Cost/FeeModels.cs
```cs
   10: public static IFeeModel From(CostSpec cost) => cost.Model switch
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Execution/EngineOrderRouter.cs
```cs
   26: public EngineOrderRouter(SimulatedOrderBook book, Universe universe, IClock clock)
   33: public IObservable<OrderEvent> OrderEvents => _events;
   35: public IReadOnlyList<StrategySignalEvent> Signals => _signals;
   43: public Task EmitSignalAsync(StrategySignal signal, CancellationToken ct = default)
   53: public Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct = default) =>
   56: public Task CancelOrderAsync(string clientOrderId, CancellationToken ct = default)
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Execution/IFillModel.cs
```cs
   27: public L1TouchFillModel(int slippageTicks)
   33: public bool TryFill(WorkingOrder o, Tick tick, double tickSize, out double fillPrice, out long fillQty)
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Execution/SimulatedOrderBook.cs
```cs
   25: public SimulatedOrderBook(SimClock clock, IFillModel fillModel, Func<InstrumentId, double> tickSizeOf)
   37: public event Action<InstrumentId, OrderEvent>? Event;
   39: public IReadOnlyList<SimulatedOrderBookDiagnosticFailure> DiagnosticFailures =>
   46: public IDisposable BindRequiredTransitionSink(Action<InstrumentId, OrderEvent> sink)
   55: public OrderResult Submit(OrderRequest request, InstrumentId instrument)
   72: public void Cancel(string clientOrderId)
   87: public void OnQuote(InstrumentId instrument, Tick tick)
  166: public RequiredTransitionSinkBinding(
  174: public void Dispose()
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Execution/WorkingOrder.cs
```cs
   10: public required OrderRequest Request { get; init; }
   11: public required InstrumentId Instrument { get; init; }
   12: public required string BrokerOrderId { get; init; }
   14: public long FilledQuantity { get; set; }
   15: public double TotalFillValue { get; set; }
   16: public OrderState State { get; set; } = OrderState.Working;
   18: public double? AveragePrice => FilledQuantity == 0 ? null : TotalFillValue / FilledQuantity;
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/AsyncMerge.cs
```cs
   14: public static async IAsyncEnumerable<MarketEvent> ByEventTime(
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/IMarketDataFeed.cs
```cs
   11: public interface IMarketDataFeed
   13:     IAsyncEnumerable<MarketEvent> StreamAsync(RunSpec spec, CancellationToken ct);
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/InMemoryMarketDataFeed.cs
```cs
   11: public sealed class InMemoryMarketDataFeed : IMarketDataFeed
   15: public InMemoryMarketDataFeed(IEnumerable<MarketEvent> events) =>
   18: public async IAsyncEnumerable<MarketEvent> StreamAsync(
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/StoreMarketDataFeed.cs
```cs
   15: public sealed class StoreMarketDataFeed : IMarketDataFeed
   19: public StoreMarketDataFeed(IMarketDataStore store) => _store = store;
   21: public IAsyncEnumerable<MarketEvent> StreamAsync(RunSpec spec, CancellationToken ct)
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/SyntheticMarketDataFeed.cs
```cs
   12: public sealed class SyntheticMarketDataFeed : IMarketDataFeed
   20: public SyntheticMarketDataFeed(InstrumentId instrument, int count, int seed = 1, double startPrice = 100.0, double spread = 0.02)
   29: public async IAsyncEnumerable<MarketEvent> StreamAsync(RunSpec spec, [EnumeratorCancellation] CancellationToken ct)
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/SyntheticTapeFeed.cs
```cs
   28: public sealed class SyntheticTapeFeed : IMarketDataFeed
   40: public SyntheticTapeFeed(InstrumentId instrument, int steps, int seed = 1, double startPrice = 5_000.0, double tickSize = 0.25)
   49: public async IAsyncEnumerable<MarketEvent> StreamAsync(RunSpec spec, [EnumeratorCancellation] CancellationToken ct)
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Kernels/BacktestStrategyKernelAdapter.cs
```cs
   17: public sealed class BacktestStrategyKernelAdapter : IStrategyKernel, IAsyncDisposable
   24: public BacktestStrategyKernelAdapter(IBacktestStrategy inner) => _inner = inner;
   28: public BacktestStrategyKernelAdapter(Func<Contract, IBacktestStrategy> build) => _build = build;
   39: public Task OnStartAsync(IStrategyContext ctx, CancellationToken ct)
   46: public Task OnQuoteAsync(InstrumentId instrument, Tick quote, IStrategyContext ctx, CancellationToken ct) =>
   49: public Task OnTradeAsync(InstrumentId instrument, TradePrint trade, IStrategyContext ctx, CancellationToken ct) =>
   52: public Task OnDepthAsync(InstrumentId instrument, DepthSnapshot depth, IStrategyContext ctx, CancellationToken ct) =>
   55: public Task OnBarAsync(InstrumentId instrument, OhlcvBar bar, IStrategyContext ctx, CancellationToken ct) =>
   58: public Task OnOrderEventAsync(OrderEvent evt, IStrategyContext ctx, CancellationToken ct) =>
   61: public Task OnEndAsync(IStrategyContext ctx, CancellationToken ct) =>
   64: public async ValueTask DisposeAsync()
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Kernels/NativeKernels.cs
```cs
   10: public static class NativeKernels
   13: public static IReadOnlyList<StrategyKernelDescriptor> All { get; } = [];
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/MarketEvent.cs
```cs
    6: public enum MarketEventKind
   21: public readonly record struct MarketEvent(
   30: public static MarketEvent OfQuote(InstrumentId id, Tick quote) =>
   33: public static MarketEvent OfTrade(InstrumentId id, TradePrint trade) =>
   36: public static MarketEvent OfDepth(InstrumentId id, DateTime tsUtc, DepthSnapshot depth) =>
   39: public static MarketEvent OfBar(InstrumentId id, OhlcvBar bar) =>
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Optimization/Criteria.cs
```cs
   10: public static double Score(OptimizationCriterion criterion, BacktestReport report)
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Optimization/GeneticOptimizer.cs
```cs
   15: public sealed class GeneticOptimizer
   20: public GeneticOptimizer(Func<IMarketDataFeed> feedFactory, Func<IStrategyKernel> kernelFactory)
   26: public async Task<OptimizationResult> RunAsync(
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Optimization/GridOptimizer.cs
```cs
   14: public sealed class GridOptimizer
   19: public GridOptimizer(Func<IMarketDataFeed> feedFactory, Func<IStrategyKernel> kernelFactory)
   25: public async Task<OptimizationResult> RunAsync(
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Optimization/TrialRunner.cs
```cs
   11: public static async Task<OptimizationTrial> EvaluateAsync(
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Optimization/WalkForwardOptimizer.cs
```cs
   13: public sealed class WalkForwardOptimizer
   18: public WalkForwardOptimizer(IReadOnlyList<MarketEvent> events, Func<IStrategyKernel> kernelFactory)
   24: public async Task<WalkForwardResult> RunAsync(OptimizationSpec spec, int folds, CancellationToken ct = default)
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Polyglot/PythonStrategyDescriptors.cs
```cs
   10: public static class PythonStrategyDescriptors
   12: public static StrategyKernelDescriptor For(
   17: public static IEnumerable<StrategyKernelDescriptor> Discover(string folder, string pythonExe = "python")
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Polyglot/PythonStrategyKernel.cs
```cs
   20: public sealed class PythonStrategyKernel : IStrategyKernel, IDisposable
   30: public PythonStrategyKernel(string pythonExe, string scriptPath)
   36: public async Task OnStartAsync(IStrategyContext ctx, CancellationToken ct)
   60: public async Task OnQuoteAsync(InstrumentId instrument, Tick quote, IStrategyContext ctx, CancellationToken ct)
   74: public async Task OnEndAsync(IStrategyContext ctx, CancellationToken ct)
  111: public void Dispose()
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/SimClock.cs
```cs
   10: public sealed class SimClock : IClock
   12: public DateTime UtcNow { get; private set; } = DateTime.UnixEpoch;
   14: public void SetTo(DateTime utc) => UtcNow = utc;
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Stats/ReportBuilder.cs
```cs
   17: public static BacktestReport Build(
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/Stats/VisualRecorder.cs
```cs
   22: public VisualRecorder(InstrumentId instrument, TimeSpan interval)
   28: public void OnMid(InstrumentId instrument, DateTime ts, double mid)
   51: public VisualTimeline Build(IReadOnlyList<RoundTripTrade> trades)
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/StrategyContext.cs
```cs
   13: public StrategyContext(IClock clock, IOrderRouter router, IPortfolioView portfolio, Universe universe, StrategyParameters parameters)
   22: public IClock Clock { get; }
   23: public IOrderRouter Router { get; }
   24: public IPortfolioView Portfolio { get; }
   25: public Universe Universe { get; }
   26: public StrategyParameters Parameters { get; }
   35: public PortfolioView(Portfolio portfolio) => _portfolio = portfolio;
   37: public double Cash => _portfolio.Cash;
   38: public double Equity => _portfolio.Equity();
   39: public Position PositionOf(InstrumentId instrument) => _portfolio.SnapshotOf(instrument);
   40: public IReadOnlyCollection<Position> OpenPositions => _portfolio.OpenPositions();
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/TradeIr/BacktestTradeIrTargetV1.cs
```cs
    6: public sealed record BacktestTradeIrArtifactIdentityV1(
   16: public sealed record BacktestTradeIrArtifactSetV1(
   21: public static class BacktestTradeIrAdmissionIssueCodesV1
   23: public const string CompilerIdentityMismatch = "BACKTEST_TRADEIR_COMPILER_IDENTITY_MISMATCH";
   24: public const string RuntimeIdentityMismatch = "BACKTEST_TRADEIR_RUNTIME_IDENTITY_MISMATCH";
   25: public const string ExecutionHostIdentityMismatch = "BACKTEST_TRADEIR_EXECUTION_HOST_IDENTITY_MISMATCH";
   32: public sealed class BacktestTradeIrTargetV1
   34: public const string ProfileId = "backtest.tradeir.quote-ema-v1";
   35: public const int ProfileRevision = 1;
   36: public const string CompilerArtifactId = "daxalgo.tradeir.plan-compiler";
   37: public const string RuntimeArtifactId = "daxalgo.tradeir.pure-evaluator";
   38: public const string ExecutionHostArtifactId = "daxalgo.backtest-engine.tradeir-host";
   39: public const string ArtifactVersion = "1.0.0";
   94: public IStrategyOperatorRegistryV1 Registry => _registry;
  100: public StrategyIrTargetProfileV1 Profile { get; }
  102: public BacktestTradeIrArtifactSetV1 PinnedArtifacts { get; }
  104: public static BacktestTradeIrTargetV1 Create(BacktestTradeIrArtifactSetV1 pinnedArtifacts)
  129: public StrategyCompilationAdmissionResultV1 Assess(
  154: public StrategyCompilationAdmissionOutcomeV1 AssessAndFreeze(
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/TradeIr/TradeIrExecutionPlanCompilerV1.cs
```cs
    6: public static class TradeIrExecutionPlanIssueCodesV1
    8: public const string DataRequirementCount = "TRADEIR_PLAN_DATA_REQUIREMENT_COUNT";
    9: public const string DataRequirementKind = "TRADEIR_PLAN_DATA_REQUIREMENT_KIND";
   10: public const string PortableInstrumentCount = "TRADEIR_PLAN_PORTABLE_INSTRUMENT_COUNT";
   11: public const string PortableInstrumentKeyInvalid = "TRADEIR_PLAN_PORTABLE_INSTRUMENT_KEY_INVALID";
   12: public const string OrderIntentOutputCount = "TRADEIR_PLAN_ORDER_INTENT_OUTPUT_COUNT";
   13: public const string OperatorUnsupported = "TRADEIR_PLAN_OPERATOR_UNSUPPORTED";
   14: public const string QuoteMidCount = "TRADEIR_PLAN_QUOTE_MID_COUNT";
   15: public const string MarketIntentCount = "TRADEIR_PLAN_MARKET_INTENT_COUNT";
   16: public const string NodeOutsideOrderPath = "TRADEIR_PLAN_NODE_OUTSIDE_ORDER_PATH";
   17: public const string InstructionCount = "TRADEIR_PLAN_INSTRUCTION_COUNT";
   18: public const string GraphInvalid = "TRADEIR_PLAN_GRAPH_INVALID";
   19: public const string ParameterInvalid = "TRADEIR_PLAN_PARAMETER_INVALID";
   20: public const string FixedQuantityNonIntegral = "TRADEIR_PLAN_FIXED_QUANTITY_NON_INTEGRAL";
   21: public const string FixedQuantityOutOfRange = "TRADEIR_PLAN_FIXED_QUANTITY_OUT_OF_RANGE";
   24: public sealed record TradeIrExecutionPlanIssueV1(string Code, string Path, string Message);
   26: public sealed record TradeIrExecutionPlanCompilationResultV1(
   32: public bool Succeeded =>
   40: public static class TradeIrExecutionPlanCompilerV1
   56: public static TradeIrExecutionPlanCompilationResultV1 Compile(
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/TradeIr/TradeIrRiskGatewayV1.cs
```cs
   27: public const string Allowed = "TRADEIR_INTENT_ALLOWED";
   28: public const string DefinitionMismatch = "TRADEIR_INTENT_DEFINITION_MISMATCH";
   29: public const string AdmissionManifestMismatch = "TRADEIR_INTENT_ADMISSION_MANIFEST_MISMATCH";
   30: public const string OutputMismatch = "TRADEIR_INTENT_OUTPUT_MISMATCH";
   31: public const string InstrumentMismatch = "TRADEIR_INTENT_INSTRUMENT_MISMATCH";
   32: public const string SourceSequenceMismatch = "TRADEIR_INTENT_SOURCE_SEQUENCE_MISMATCH";
   33: public const string SequenceMismatch = "TRADEIR_INTENT_SEQUENCE_MISMATCH";
   34: public const string EventTimeMismatch = "TRADEIR_INTENT_EVENT_TIME_MISMATCH";
   35: public const string DeltaMismatch = "TRADEIR_INTENT_DELTA_MISMATCH";
   36: public const string RiskDenied = "TRADEIR_INTENT_RISK_DENIED";
   59: public bool IsAdmitted => Decision.IsAllowed && Command is not null && Submission is not null;
   93: public TradeIrRiskGatewayV1(
  151: public IReadOnlyList<TradeIrGatewayDecisionTraceV1> Decisions => _readOnlyDecisions;
  153: public int SubmittedOrderCount { get; private set; }
  159: public void ObserveQuote(TradingTerminal.Core.Domain.Tick quote, long sourceSequence)
  199: public TradeIrGatewayAdmissionV1 Admit(TradeIrOrderIntentV1 intent)
  287: public IReadOnlyList<TradeIrOrderFeedbackV1> DrainFeedback()
  295: public void Dispose()
  708: public long RemainingQuantity => Quantity - FilledQuantity;
```

## src/linux/Backtest/TradingTerminal.Backtest.Engine/TradeIr/TradeIrSimulatedBacktestRunnerV1.cs
```cs
   24: public sealed class TradeIrSimulatedBacktestRunnerV1 : ITradeIrSimulatedBacktestRunnerV1
   36: public async Task<TradeIrSimulatedBacktestResultV1> RunAsync(
  690: public StableRandomV1(int seed) =>
  693: public double NextUnit()
```
