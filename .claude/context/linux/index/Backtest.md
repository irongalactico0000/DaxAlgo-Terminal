# macOS index / Backtest

Generated from source fingerprint `1ddf0170457d`. macOS/Avalonia source only.

| File | LOC | Tree | Project | Role | Public surface | Purpose |
|---|---:|---|---|---|---|---|
| `src/linux/Backtest/TradingTerminal.Backtest.Cli/Args.cs` | 64 | linux | TradingTerminal.Backtest.Cli | product | Y | Tiny CLI parser: supports --name value and --name=value. No NuGet dependency |
| `src/linux/Backtest/TradingTerminal.Backtest.Cli/Output/ResultWriter.cs` | 67 | linux | TradingTerminal.Backtest.Cli | product | Y | Writes a finished to disk: summary.json (stats + |
| `src/linux/Backtest/TradingTerminal.Backtest.Cli/PluginStrategies.cs` | 50 | linux | TradingTerminal.Backtest.Cli | product | Y | Backtest options contributed by all loaded plugins. |
| `src/linux/Backtest/TradingTerminal.Backtest.Cli/Program.cs` | 686 | linux | TradingTerminal.Backtest.Cli | product | N |  |
| `src/linux/Backtest/TradingTerminal.Backtest.Cli/StoreFactory.cs` | 89 | linux | TradingTerminal.Backtest.Cli | product | Y | Look up the canonical id for a symbol by querying the |
| `src/linux/Backtest/TradingTerminal.Backtest.Client/AbandonedWorkerStagingCleaner.cs` | 131 | linux | TradingTerminal.Backtest.Client | product | Y | Removes only old, immediate worker-owned .staging-* directories beneath immediate job folders. |
| `src/linux/Backtest/TradingTerminal.Backtest.Client/BacktestJobClient.cs` | 1310 | linux | TradingTerminal.Backtest.Client | product | Y | Owns one-shot worker processes. Every control stream and captured diagnostic is bounded; |
| `src/linux/Backtest/TradingTerminal.Backtest.Client/BacktestWorkerExecutableResolver.cs` | 85 | linux | TradingTerminal.Backtest.Client | product | Y |  |
| `src/linux/Backtest/TradingTerminal.Backtest.Client/BacktestWorkerOptions.cs` | 37 | linux | TradingTerminal.Backtest.Client | product | Y | Arguments inserted before the client's mandatory |
| `src/linux/Backtest/TradingTerminal.Backtest.Client/BacktestWorkerServiceCollectionExtensions.cs` | 17 | linux | TradingTerminal.Backtest.Client | product | Y |  |
| `src/linux/Backtest/TradingTerminal.Backtest.Client/IBacktestJobClient.cs` | 12 | linux | TradingTerminal.Backtest.Client | product | Y | Runs one isolated worker process and returns a fully verified terminal outcome. |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Accounting/Portfolio.cs` | 153 | linux | TradingTerminal.Backtest.Engine | product | Y | Update the latest mark for an instrument and the open lots' favorable/adverse |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/BacktestEngine.cs` | 230 | linux | TradingTerminal.Backtest.Engine | product | Y | Drives one backtest end-to-end. Single-threaded by design: there is one logical timeline |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Cost/FeeModels.cs` | 16 | linux | TradingTerminal.Backtest.Engine | product | Y | Maps the serializable |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Execution/EngineOrderRouter.cs` | 69 | linux | TradingTerminal.Backtest.Engine | product | Y | The kernel-facing order seam for the backtester. Resolves each 's |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Execution/IFillModel.cs` | 73 | linux | TradingTerminal.Backtest.Engine | product | Y | Decides whether a working order fills against the current quote and at |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Execution/SimulatedOrderBook.cs` | 189 | linux | TradingTerminal.Backtest.Engine | product | Y | Evaluate fills for the orders resting on one instrument against its latest |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Execution/WorkingOrder.cs` | 19 | linux | TradingTerminal.Backtest.Engine | product | Y | A live order resting in the simulated book, tagged with the instrument |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/AsyncMerge.cs` | 56 | linux | TradingTerminal.Backtest.Engine | product | Y | K-way merge of already-ascending streams into one globally |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/IMarketDataFeed.cs` | 14 | linux | TradingTerminal.Backtest.Engine | product | Y | Produces the time-ordered event stream the engine replays for a run. Implementations |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/InMemoryMarketDataFeed.cs` | 28 | linux | TradingTerminal.Backtest.Engine | product | Y | A feed backed by an in-memory event list — the workhorse for |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/StoreMarketDataFeed.cs` | 58 | linux | TradingTerminal.Backtest.Engine | product | Y | Replays a run from the canonical market-data store — the primary data |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/SyntheticMarketDataFeed.cs` | 48 | linux | TradingTerminal.Backtest.Engine | product | Y | A deterministic synthetic feed: a mean-reverting (Ornstein-Uhlenbeck-ish) random walk of the mid |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Feeds/SyntheticTapeFeed.cs` | 95 | linux | TradingTerminal.Backtest.Engine | product | Y | Default anchor: a weekday in the London/NY overlap so session gates pass. |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Kernels/BacktestStrategyKernelAdapter.cs` | 73 | linux | TradingTerminal.Backtest.Engine | product | Y | Wrap an already-built legacy strategy (used by the parity test). |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Kernels/NativeKernels.cs` | 14 | linux | TradingTerminal.Backtest.Engine | product | Y | Intentionally empty: macOS loads kernels from authored or installed bundles. |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/MarketEvent.cs` | 41 | linux | TradingTerminal.Backtest.Engine | product | Y | Which market-data payload a |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Optimization/Criteria.cs` | 28 | linux | TradingTerminal.Backtest.Engine | product | Y | Scores a finished run by an |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Optimization/GeneticOptimizer.cs` | 123 | linux | TradingTerminal.Backtest.Engine | product | Y | Genetic parameter search for spaces too large to grid exhaustively. A genome |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Optimization/GridOptimizer.cs` | 64 | linux | TradingTerminal.Backtest.Engine | product | Y | Cartesian product of the axes into one dictionary per combination. |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Optimization/TrialRunner.cs` | 31 | linux | TradingTerminal.Backtest.Engine | product | Y | Runs one parameter combination through the engine and scores it — the |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Optimization/WalkForwardOptimizer.cs` | 79 | linux | TradingTerminal.Backtest.Engine | product | Y | Walk-forward analysis: splits the dataset into folds + 1 equal time chunks; |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Polyglot/PythonStrategyDescriptors.cs` | 26 | linux | TradingTerminal.Backtest.Engine | product | Y | Builds |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Polyglot/PythonStrategyKernel.cs` | 127 | linux | TradingTerminal.Backtest.Engine | product | Y | Runs a Python-authored strategy (daxalgo_bt) as a long-lived subprocess and bridges it |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/SimClock.cs` | 15 | linux | TradingTerminal.Backtest.Engine | product | Y | The backtest clock: is whatever the engine last advanced it to as |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Stats/ReportBuilder.cs` | 144 | linux | TradingTerminal.Backtest.Engine | product | Y | Turns a finished run's equity timeline + round-trip ledger into a . |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/Stats/VisualRecorder.cs` | 77 | linux | TradingTerminal.Backtest.Engine | product | Y | Captures the visual-replay backdrop while a run streams: aggregates the charted instrument's |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/StrategyContext.cs` | 41 | linux | TradingTerminal.Backtest.Engine | product | Y | The engine's |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/TradeIr/BacktestTradeIrTargetV1.cs` | 248 | linux | TradingTerminal.Backtest.Engine | product | Y | An immutable content identity for one installed target artifact. |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/TradeIr/TradeIrExecutionPlanCompilerV1.cs` | 572 | linux | TradingTerminal.Backtest.Engine | product | Y | Lowers the Engine-owned quote/EMA target into the dependency-free runtime plan. Target, |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/TradeIr/TradeIrRiskGatewayV1.cs` | 710 | linux | TradingTerminal.Backtest.Engine | product | Y | Product-owned risk settings for the closed TradeIR backtest lane. Strategy definitions and |
| `src/linux/Backtest/TradingTerminal.Backtest.Engine/TradeIr/TradeIrSimulatedBacktestRunnerV1.cs` | 703 | linux | TradingTerminal.Backtest.Engine | product | Y | Minimum honest product runner for a package-valid typed graph. It materializes one |
| `src/linux/Backtest/TradingTerminal.Backtest.Protocol/BacktestJobContracts.cs` | 373 | linux | TradingTerminal.Backtest.Protocol | product | Y | Publisher evidence accepted by the host for one exact installed archive. |
| `src/linux/Backtest/TradingTerminal.Backtest.Protocol/BacktestProtocolJson.cs` | 133 | linux | TradingTerminal.Backtest.Protocol | product | Y | Canonical JSON settings shared by request files, NDJSON progress, and result artifacts. |
| `src/linux/Backtest/TradingTerminal.Backtest.Protocol/BacktestProtocolValidator.cs` | 333 | linux | TradingTerminal.Backtest.Protocol | product | Y | Pure request validation shared by the client and worker; filesystem checks stay |
| `src/linux/Backtest/TradingTerminal.Backtest.Protocol/BacktestProtocolVersions.cs` | 41 | linux | TradingTerminal.Backtest.Protocol | product | Y | Independent compatibility versions for the worker control and artifact boundary. |
| `src/linux/Backtest/TradingTerminal.Backtest.Worker/BundleStrategyLoadContext.cs` | 103 | linux | TradingTerminal.Backtest.Worker | product | Y |  |
| `src/linux/Backtest/TradingTerminal.Backtest.Worker/BundleStrategyLoader.cs` | 388 | linux | TradingTerminal.Backtest.Worker | product | Y |  |
| `src/linux/Backtest/TradingTerminal.Backtest.Worker/ParquetMarketDataFeed.cs` | 61 | linux | TradingTerminal.Backtest.Worker | product | Y | P2's narrow, single-instrument immutable parquet adapter. |
| `src/linux/Backtest/TradingTerminal.Backtest.Worker/Program.cs` | 7 | linux | TradingTerminal.Backtest.Worker | product | N |  |
| `src/linux/Backtest/TradingTerminal.Backtest.Worker/WorkerApplication.cs` | 489 | linux | TradingTerminal.Backtest.Worker | product | Y |  |
| `src/linux/Backtest/TradingTerminal.Backtest.Worker/WorkerArtifactPublisher.cs` | 281 | linux | TradingTerminal.Backtest.Worker | product | Y | Writes private staging files, moves artifacts into place, then publishes the manifest |
| `src/linux/Backtest/TradingTerminal.Backtest.Worker/WorkerProgressEmitter.cs` | 75 | linux | TradingTerminal.Backtest.Worker | product | Y | Serializes a finite number of coarse progress records to stdout as NDJSON. |
| `src/linux/Backtest/TradingTerminal.TradeIr.Runtime/AssemblyInfo.cs` | 4 | linux | TradingTerminal.TradeIr.Runtime | product | N |  |
| `src/linux/Backtest/TradingTerminal.TradeIr.Runtime/TradeIrEvaluatorV1.cs` | 537 | linux | TradingTerminal.TradeIr.Runtime | product | Y | Deterministically evaluates one admitted, host-compiled graph. This type owns numeric and |
| `src/linux/Backtest/TradingTerminal.TradeIr.Runtime/TradeIrRuntimeContractsV1.cs` | 377 | linux | TradingTerminal.TradeIr.Runtime | product | Y | Closed numeric/resource limits shared by plan construction, compilation, and evaluation. |
| `src/linux/Backtest/TradingTerminal.TradeIr.Runtime/TradeIrRuntimeSemanticsV1.cs` | 29 | linux | TradingTerminal.TradeIr.Runtime | product | Y | Exact semantic contracts implemented by this runtime. Headless compatibility tests hash these |
