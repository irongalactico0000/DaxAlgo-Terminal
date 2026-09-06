# macOS index / Core

Generated from source fingerprint `1ddf0170457d`. macOS/Avalonia source only.

| File | LOC | Tree | Project | Role | Public surface | Purpose |
|---|---:|---|---|---|---|---|
| `src/linux/Core/TradingTerminal.Core/Accounts/AccountContractGuards.cs` | 29 | linux | TradingTerminal.Core | product | Y |  |
| `src/linux/Core/TradingTerminal.Core/Accounts/AccountIdentity.cs` | 76 | linux | TradingTerminal.Core | product | Y | The exclusive session-expiry instant, or |
| `src/linux/Core/TradingTerminal.Core/Accounts/AccountServices.cs` | 22 | linux | TradingTerminal.Core | product | Y | Retrieves the normalized product entitlement for an authenticated session. |
| `src/linux/Core/TradingTerminal.Core/Accounts/EntitlementAccessEvaluator.cs` | 171 | linux | TradingTerminal.Core | product | Y | Stable, provider-neutral explanation for an edition-access decision. |
| `src/linux/Core/TradingTerminal.Core/Accounts/EntitlementLeaseWireDto.cs` | 143 | linux | TradingTerminal.Core | product | Y | Versioned transport contract emitted by the product platform for one device-bound offline |
| `src/linux/Core/TradingTerminal.Core/Accounts/OfflineEntitlementLease.cs` | 152 | linux | TradingTerminal.Core | product | Y | Device installation identifier authenticated as part of the signed payload. |
| `src/linux/Core/TradingTerminal.Core/Accounts/SubscriptionEntitlement.cs` | 96 | linux | TradingTerminal.Core | product | Y | Inclusive start of the entitlement window. |
| `src/linux/Core/TradingTerminal.Core/AiAnalyst/AiAnalystDecision.cs` | 9 | linux | TradingTerminal.Core | product | Y | No actionable verdict — the analyst either declined to call or was |
| `src/linux/Core/TradingTerminal.Core/AiAnalyst/AnalystBar.cs` | 13 | linux | TradingTerminal.Core | product | Y | Wire-format OHLCV bar handed to the Python analyst. Mirrors Bar but lives |
| `src/linux/Core/TradingTerminal.Core/AiAnalyst/AnalystReport.cs` | 68 | linux | TradingTerminal.Core | product | Y | One indicator agent's read on the tape — text summary plus the |
| `src/linux/Core/TradingTerminal.Core/AiAnalyst/AnalystRequest.cs` | 15 | linux | TradingTerminal.Core | product | Y | Payload sent to the Python sidecar's /analyst/run endpoint. Keeps provider / |
| `src/linux/Core/TradingTerminal.Core/AiAnalyst/IAiAnalystClient.cs` | 20 | linux | TradingTerminal.Core | product | Y | True when this client expects the sidecar to be reachable. The Null |
| `src/linux/Core/TradingTerminal.Core/Analytics/CorrelationCalculator.cs` | 126 | linux | TradingTerminal.Core | product | Y | Bar-to-bar log returns |
| `src/linux/Core/TradingTerminal.Core/Analytics/CorrelationResult.cs` | 18 | linux | TradingTerminal.Core | product | Y | A computed correlation matrix over a set of instruments. indexes both |
| `src/linux/Core/TradingTerminal.Core/Backtest/BacktestConfig.cs` | 73 | linux | TradingTerminal.Core | product | Y | Where the engine pulls tick data from for a single backtest run. |
| `src/linux/Core/TradingTerminal.Core/Backtest/BacktestInstrumentEvent.cs` | 32 | linux | TradingTerminal.Core | product | Y | A replay payload whose canonical instrument and broker contract remain attached. Exactly |
| `src/linux/Core/TradingTerminal.Core/Backtest/BacktestResult.cs` | 19 | linux | TradingTerminal.Core | product | Y | Output of a single backtest run. is null until Phase 4 wires |
| `src/linux/Core/TradingTerminal.Core/Backtest/BacktestStatistics.cs` | 35 | linux | TradingTerminal.Core | product | Y | Aggregate performance metrics derived from a 's trades and |
| `src/linux/Core/TradingTerminal.Core/Backtest/BacktestStrategyOption.cs` | 78 | linux | TradingTerminal.Core | product | Y | Declared tunables. |
| `src/linux/Core/TradingTerminal.Core/Backtest/EquityPoint.cs` | 4 | linux | TradingTerminal.Core | product | Y | Sample of the equity curve at a point in time. |
| `src/linux/Core/TradingTerminal.Core/Backtest/Fast/FastBacktestRequest.cs` | 26 | linux | TradingTerminal.Core | product | Y | Input to the out-of-process C++ tick backtester. Serialised to JSON, written to |
| `src/linux/Core/TradingTerminal.Core/Backtest/Fast/FastBacktestResult.cs` | 18 | linux | TradingTerminal.Core | product | Y | Result emitted by the C++ tick backtester on stdout as JSON. The |
| `src/linux/Core/TradingTerminal.Core/Backtest/Fast/IFastBacktestRunner.cs` | 25 | linux | TradingTerminal.Core | product | Y | Out-of-process replay engine. Runs in a separate subprocess (the C++20 |
| `src/linux/Core/TradingTerminal.Core/Backtest/FillRecord.cs` | 18 | linux | TradingTerminal.Core | product | Y | One fill captured during a backtest. Used by transaction-cost analysis |
| `src/linux/Core/TradingTerminal.Core/Backtest/IBacktestSession.cs` | 21 | linux | TradingTerminal.Core | product | Y | View-model-facing seam over the backtest engine. The Backtest tab's view-model injects |
| `src/linux/Core/TradingTerminal.Core/Backtest/IBacktestStrategy.cs` | 57 | linux | TradingTerminal.Core | product | Y | Called once before any ticks. Use to read initial state or schedule |
| `src/linux/Core/TradingTerminal.Core/Backtest/IParquetQueryService.cs` | 68 | linux | TradingTerminal.Core | product | Y | One resampled bar from |
| `src/linux/Core/TradingTerminal.Core/Backtest/MonteCarlo.cs` | 147 | linux | TradingTerminal.Core | product | Y | Trade-bootstrap Monte Carlo. Given a sequence of round-trip trade PnLs from a |
| `src/linux/Core/TradingTerminal.Core/Backtest/Trade.cs` | 18 | linux | TradingTerminal.Core | product | Y | A round-trip trade: an entry fill and the matching exit fill that |
| `src/linux/Core/TradingTerminal.Core/Backtest/TransactionCostAnalysis.cs` | 112 | linux | TradingTerminal.Core | product | Y | Transaction-cost analysis (TCA) — the standard post-trade report quant desks run to |
| `src/linux/Core/TradingTerminal.Core/Backtest/WalkForward.cs` | 34 | linux | TradingTerminal.Core | product | Y | All-empty axes with quantity 1 — every strategy grid falls back to |
| `src/linux/Core/TradingTerminal.Core/Backtesting/BacktestReport.cs` | 113 | linux | TradingTerminal.Core | product | Y | One sample of the account through time: mark-to-market |
| `src/linux/Core/TradingTerminal.Core/Backtesting/IStrategyContext.cs` | 31 | linux | TradingTerminal.Core | product | Y | Simulated clock in backtests, wall clock live. Never call |
| `src/linux/Core/TradingTerminal.Core/Backtesting/IStrategyKernel.cs` | 45 | linux | TradingTerminal.Core | product | Y | Called once before any market data. Read parameters, allocate per-instrument state. |
| `src/linux/Core/TradingTerminal.Core/Backtesting/Optimization.cs` | 111 | linux | TradingTerminal.Core | product | Y | What the optimizer ranks trials by. Every criterion is scored so that |
| `src/linux/Core/TradingTerminal.Core/Backtesting/Portfolio.cs` | 44 | linux | TradingTerminal.Core | product | Y | Free cash (realized PnL net of fees), excluding open-position marks. |
| `src/linux/Core/TradingTerminal.Core/Backtesting/RunSpec.cs` | 114 | linux | TradingTerminal.Core | product | Y | Where the engine pulls historical market data from. |
| `src/linux/Core/TradingTerminal.Core/Backtesting/StrategyKernelRegistry.cs` | 59 | linux | TradingTerminal.Core | product | Y | Discovers strategy kernels by id and builds instances. The Studio catalog, the |
| `src/linux/Core/TradingTerminal.Core/Backtesting/StrategyParameterSchema.cs` | 65 | linux | TradingTerminal.Core | product | Y | The domain of a tunable parameter — drives both UI controls and |
| `src/linux/Core/TradingTerminal.Core/Backtesting/StrategyParameters.cs` | 44 | linux | TradingTerminal.Core | product | Y | Reads a required parameter; throws |
| `src/linux/Core/TradingTerminal.Core/Backtesting/Universe.cs` | 39 | linux | TradingTerminal.Core | product | Y | The first instrument — convenient for single-instrument runs and as a default |
| `src/linux/Core/TradingTerminal.Core/Backtesting/VisualTimeline.cs` | 24 | linux | TradingTerminal.Core | product | Y | One OHLC candle of the charted instrument, aggregated from quote mids over |
| `src/linux/Core/TradingTerminal.Core/Brokers/BrokerApiUsage.cs` | 22 | linux | TradingTerminal.Core | product | Y | Snapshot of one broker's API-call activity, as reported by . |
| `src/linux/Core/TradingTerminal.Core/Brokers/BrokerCapabilities.cs` | 211 | linux | TradingTerminal.Core | product | Y | Describes where the instrument universe exposed by a broker client comes from. |
| `src/linux/Core/TradingTerminal.Core/Brokers/BrokerConnectionMode.cs` | 16 | linux | TradingTerminal.Core | product | Y | Resolved at DI registration so the UI can tell whether a real |
| `src/linux/Core/TradingTerminal.Core/Brokers/BrokerKind.cs` | 89 | linux | TradingTerminal.Core | product | Y | In-process synthetic / replay backend — no broker, no network. Streams either |
| `src/linux/Core/TradingTerminal.Core/Brokers/CTrader/CTraderDiscoveredAccount.cs` | 11 | linux | TradingTerminal.Core | product | Y | A single cTrader trading account associated with an OAuth access token, as |
| `src/linux/Core/TradingTerminal.Core/Brokers/CTrader/ICTraderAccountDiscovery.cs` | 28 | linux | TradingTerminal.Core | product | Y | One-shot helper used by the login form to enumerate the trading accounts |
| `src/linux/Core/TradingTerminal.Core/Brokers/IBrokerApiMeter.cs` | 19 | linux | TradingTerminal.Core | product | Y | Record one API call against the named broker. Cheap; runs on the |
| `src/linux/Core/TradingTerminal.Core/Brokers/IBrokerLoginForm.cs` | 61 | linux | TradingTerminal.Core | product | Y | True when all required fields are populated and the user may submit. |
| `src/linux/Core/TradingTerminal.Core/Brokers/IBrokerLoginFormFactory.cs` | 15 | linux | TradingTerminal.Core | product | Y | Every form whose broker is currently available (i.e. its SDK was present |
| `src/linux/Core/TradingTerminal.Core/Brokers/IBrokerSelector.cs` | 55 | linux | TradingTerminal.Core | product | Y | Brokers that have a registered |
| `src/linux/Core/TradingTerminal.Core/Brokers/Upstox/IUpstoxAuthService.cs` | 31 | linux | TradingTerminal.Core | product | Y | One-shot helper for the Upstox login form's OAuth2 authorization-code flow. The interface |
| `src/linux/Core/TradingTerminal.Core/Configuration/AiCodegenOptions.cs` | 76 | linux | TradingTerminal.Core | product | Y | Config for one BYO-key / local codegen provider. |
| `src/linux/Core/TradingTerminal.Core/Configuration/AlpacaOptions.cs` | 34 | linux | TradingTerminal.Core | product | Y | API key id (shown on the dashboard, prefixed with "PK" for paper |
| `src/linux/Core/TradingTerminal.Core/Configuration/AppEdition.cs` | 19 | linux | TradingTerminal.Core | product | Y | Keyless brokers only, core charts/tools/strategies. No ML / AI / LSE / |
| `src/linux/Core/TradingTerminal.Core/Configuration/ArchiveOptions.cs` | 51 | linux | TradingTerminal.Core | product | Y | Master switch — when false the schedule service idles and the offload |
| `src/linux/Core/TradingTerminal.Core/Configuration/BinanceOptions.cs` | 45 | linux | TradingTerminal.Core | product | Y | REST base for the ping/connectivity check and historical klines. No trailing slash. |
| `src/linux/Core/TradingTerminal.Core/Configuration/BrokerEditionPolicy.cs` | 49 | linux | TradingTerminal.Core | product | Y | Brokers usable with no API key or account. Available in every edition. |
| `src/linux/Core/TradingTerminal.Core/Configuration/BybitOptions.cs` | 36 | linux | TradingTerminal.Core | product | Y | REST base for historical kline + the connectivity check. No trailing slash. |
| `src/linux/Core/TradingTerminal.Core/Configuration/CTraderOptions.cs` | 35 | linux | TradingTerminal.Core | product | Y | OAuth application clientId (from connect.spotware.com/apps). |
| `src/linux/Core/TradingTerminal.Core/Configuration/CoinbaseOptions.cs` | 34 | linux | TradingTerminal.Core | product | Y | REST base for historical candles + the connectivity check. No trailing slash. |
| `src/linux/Core/TradingTerminal.Core/Configuration/DevOptions.cs` | 40 | linux | TradingTerminal.Core | product | Y | Developer-only switches, bound from the Dev configuration section. These are off by |
| `src/linux/Core/TradingTerminal.Core/Configuration/GoogleAuthOptions.cs` | 17 | linux | TradingTerminal.Core | product | Y | OAuth client id for the installed desktop application. |
| `src/linux/Core/TradingTerminal.Core/Configuration/InteractiveBrokersOptions.cs` | 24 | linux | TradingTerminal.Core | product | Y | IB market-data subscription mode applied to all reqMktData requests. |
| `src/linux/Core/TradingTerminal.Core/Configuration/IronBeamOptions.cs` | 42 | linux | TradingTerminal.Core | product | Y | Ironbeam account username. |
| `src/linux/Core/TradingTerminal.Core/Configuration/KrakenOptions.cs` | 34 | linux | TradingTerminal.Core | product | Y | REST base for historical OHLC + the connectivity check. No trailing slash. |
| `src/linux/Core/TradingTerminal.Core/Configuration/LondonStrategicEdgeOptions.cs` | 38 | linux | TradingTerminal.Core | product | Y | API key from londonstrategicedge.com/websockets (format |
| `src/linux/Core/TradingTerminal.Core/Configuration/MarketDataStoreOptions.cs` | 134 | linux | TradingTerminal.Core | product | Y | Which backend persists the canonical market-data store. |
| `src/linux/Core/TradingTerminal.Core/Configuration/MarketRegimeOptions.cs` | 51 | linux | TradingTerminal.Core | product | Y | Master switch. When false the service does not poll and the panel |
| `src/linux/Core/TradingTerminal.Core/Configuration/ModelRegistryOptions.cs` | 19 | linux | TradingTerminal.Core | product | Y | SQLite database file path. Empty → a default ( |
| `src/linux/Core/TradingTerminal.Core/Configuration/NinjaTraderOptions.cs` | 31 | linux | TradingTerminal.Core | product | Y | NinjaTrader account name (e.g. "Sim101" for the bundled simulation account). |
| `src/linux/Core/TradingTerminal.Core/Configuration/OkxOptions.cs` | 31 | linux | TradingTerminal.Core | product | Y | REST base for historical candles + the connectivity check. No trailing slash. |
| `src/linux/Core/TradingTerminal.Core/Configuration/OrderFlowPressureMapOptions.cs` | 129 | linux | TradingTerminal.Core | product | Y | No notable pressure this candle. |
| `src/linux/Core/TradingTerminal.Core/Configuration/ParquetLakeOptions.cs` | 34 | linux | TradingTerminal.Core | product | Y | Master switch. When false the export service idles (one cheap timer tick |
| `src/linux/Core/TradingTerminal.Core/Configuration/PluginsOptions.cs` | 67 | linux | TradingTerminal.Core | product | Y | How much the host trusts the plugins it finds in its plugins |
| `src/linux/Core/TradingTerminal.Core/Configuration/ResearchReproOptions.cs` | 36 | linux | TradingTerminal.Core | product | Y | Master switch. When false the ingest client is Null and no jobs |
| `src/linux/Core/TradingTerminal.Core/Configuration/SandboxOptions.cs` | 39 | linux | TradingTerminal.Core | product | Y | Which runner implementation |
| `src/linux/Core/TradingTerminal.Core/Configuration/SidecarOptions.cs` | 31 | linux | TradingTerminal.Core | product | Y | Master switch. When true, the app auto-launches the sidecar on startup if |
| `src/linux/Core/TradingTerminal.Core/Configuration/SimulatedBrokerOptions.cs` | 69 | linux | TradingTerminal.Core | product | Y | How the |
| `src/linux/Core/TradingTerminal.Core/Configuration/TelegramArchiveOptions.cs` | 37 | linux | TradingTerminal.Core | product | Y | From https://my.telegram.org/apps. Required for any Telegram operation. |
| `src/linux/Core/TradingTerminal.Core/Configuration/UpstoxOptions.cs` | 46 | linux | TradingTerminal.Core | product | Y | OAuth2 client id — the Upstox app's "API Key" from the developer |
| `src/linux/Core/TradingTerminal.Core/Domain/AssetClass.cs` | 17 | linux | TradingTerminal.Core | product | Y | Broker-neutral asset classification for a canonical instrument. Derived from a broker's |
| `src/linux/Core/TradingTerminal.Core/Domain/Bar.cs` | 10 | linux | TradingTerminal.Core | product | Y | An OHLCV bar at a specific UTC timestamp (the bar's open time). |
| `src/linux/Core/TradingTerminal.Core/Domain/BarSize.cs` | 59 | linux | TradingTerminal.Core | product | Y | The exact string the TWS API expects for |
| `src/linux/Core/TradingTerminal.Core/Domain/ConnectionState.cs` | 10 | linux | TradingTerminal.Core | product | Y |  |
| `src/linux/Core/TradingTerminal.Core/Domain/Contract.cs` | 13 | linux | TradingTerminal.Core | product | Y | An IB-style instrument descriptor (intentionally aligned with TWS API fields). |
| `src/linux/Core/TradingTerminal.Core/Domain/DepthLevel.cs` | 8 | linux | TradingTerminal.Core | product | Y | A single price level on one side of the order book. Sizes |
| `src/linux/Core/TradingTerminal.Core/Domain/DepthSnapshot.cs` | 30 | linux | TradingTerminal.Core | product | Y | Best (highest) bid, or 0 when the bid side is empty. |
| `src/linux/Core/TradingTerminal.Core/Domain/Instrument.cs` | 41 | linux | TradingTerminal.Core | product | Y | Builds an as-yet-unpersisted instrument (id = None) for the registry to insert. |
| `src/linux/Core/TradingTerminal.Core/Domain/InstrumentId.cs` | 18 | linux | TradingTerminal.Core | product | Y | The unset / unresolved id. Ingest treats a record carrying this as |
| `src/linux/Core/TradingTerminal.Core/Domain/MarketDataRecords.cs` | 81 | linux | TradingTerminal.Core | product | Y | Which side initiated a trade print, when the broker reports it. |
| `src/linux/Core/TradingTerminal.Core/Domain/Tick.cs` | 26 | linux | TradingTerminal.Core | product | Y | A single bid/ask quote update from IB's tick-by-tick BidAsk feed. Sizes are |
| `src/linux/Core/TradingTerminal.Core/Events/EventBus.cs` | 44 | linux | TradingTerminal.Core | product | Y |  |
| `src/linux/Core/TradingTerminal.Core/Events/IEventBus.cs` | 11 | linux | TradingTerminal.Core | product | Y | Lightweight in-process pub/sub. Use for cross-pane events (strategy opened, connection lost, etc.). |
| `src/linux/Core/TradingTerminal.Core/Execution/CanonicalOrderInstruction.cs` | 344 | linux | TradingTerminal.Core | product | Y | The strategy/operator-owned economics before account or venue routing. |
| `src/linux/Core/TradingTerminal.Core/Execution/DeterministicPaperVenue.cs` | 888 | linux | TradingTerminal.Core | product | Y | Exact quote and deterministic available quantity supplied to the Paper venue. |
| `src/linux/Core/TradingTerminal.Core/Execution/ExecutionCommands.cs` | 237 | linux | TradingTerminal.Core | product | Y |  |
| `src/linux/Core/TradingTerminal.Core/Execution/ExecutionFrameTransport.cs` | 122 | linux | TradingTerminal.Core | product | Y | Bounded typed framing used only after a local execution peer is connected. |
| `src/linux/Core/TradingTerminal.Core/Execution/ExecutionIdentifiers.cs` | 386 | linux | TradingTerminal.Core | product | Y | Shared contract implemented by the strongly typed execution identifiers. |
| `src/linux/Core/TradingTerminal.Core/Execution/ExecutionIpcAuthentication.cs` | 229 | linux | TradingTerminal.Core | product | Y | Mutual nonce/HMAC authentication. Direction-specific, version-bound transcripts prevent proof |
| `src/linux/Core/TradingTerminal.Core/Execution/ExecutionLease.cs` | 219 | linux | TradingTerminal.Core | product | Y | One independently fenced execution resource. |
| `src/linux/Core/TradingTerminal.Core/Execution/ExecutionSchema.cs` | 155 | linux | TradingTerminal.Core | product | Y | Deterministic JSON used for persisted payloads, hashes, and duplicate detection. |
| `src/linux/Core/TradingTerminal.Core/Execution/ExecutionServiceContract.cs` | 144 | linux | TradingTerminal.Core | product | Y | Versioned contract shared by a Paper execution host and its control-plane client. |
| `src/linux/Core/TradingTerminal.Core/Execution/ExecutionServiceEngine.cs` | 506 | linux | TradingTerminal.Core | product | Y | Captures both Paper truths and runs the existing exact reconciliation engine. |
| `src/linux/Core/TradingTerminal.Core/Execution/OrderEventStore.cs` | 257 | linux | TradingTerminal.Core | product | Y | Event and its monotonic process-local publication position. |
| `src/linux/Core/TradingTerminal.Core/Execution/OrderEvents.cs` | 474 | linux | TradingTerminal.Core | product | Y | One exact venue fill. Quantity, price, and fee retain coefficient/scale semantics. |
| `src/linux/Core/TradingTerminal.Core/Execution/OrderLifecycle.cs` | 365 | linux | TradingTerminal.Core | product | Y | One explicitly admitted directed lifecycle edge. |
| `src/linux/Core/TradingTerminal.Core/Execution/OrderManagementService.cs` | 711 | linux | TradingTerminal.Core | product | Y | Explicit causation and inbox identity shared by every event from one OMS |
| `src/linux/Core/TradingTerminal.Core/Execution/OrderProjection.cs` | 498 | linux | TradingTerminal.Core | product | Y | Verifies aggregate identity, sequence, lifecycle cause, timestamps, and hash binding. |
| `src/linux/Core/TradingTerminal.Core/Execution/PaperExecutionDispatch.cs` | 103 | linux | TradingTerminal.Core | product | Y | Local proof returned by the Paper adapter after one command crossed its |
| `src/linux/Core/TradingTerminal.Core/Execution/Reconciliation.cs` | 789 | linux | TradingTerminal.Core | product | Y | One append-only discrepancy or resolution fact. |
| `src/linux/Core/TradingTerminal.Core/Execution/RiskPolicy.cs` | 581 | linux | TradingTerminal.Core | product | Y | Stateless policy; callers persist a RiskObservation before applying the decision. |
| `src/linux/Core/TradingTerminal.Core/Execution/ScaledValues.cs` | 483 | linux | TradingTerminal.Core | product | Y | An exact signed quantity encoded as an integer coefficient and a base-10 |
| `src/linux/Core/TradingTerminal.Core/Hosting/ISidecarController.cs` | 17 | linux | TradingTerminal.Core | product | Y | True once the sidecar's health endpoint has answered. |
| `src/linux/Core/TradingTerminal.Core/Hosting/NullSidecarController.cs` | 15 | linux | TradingTerminal.Core | product | Y | No sidecar in this edition — returns false without starting anything, never |
| `src/linux/Core/TradingTerminal.Core/IndexKScore/IndexComponentCatalog.cs` | 123 | linux | TradingTerminal.Core | product | Y | A named index universe: its display metadata and weighted constituents. |
| `src/linux/Core/TradingTerminal.Core/IndexKScore/IndexKScoreAggregator.cs` | 174 | linux | TradingTerminal.Core | product | Y | Updates the component snapshot and returns the new index-level aggregate. Returns |
| `src/linux/Core/TradingTerminal.Core/IndexKScore/IndexKScoreCalculator.cs` | 456 | linux | TradingTerminal.Core | product | Y | One bar's worth of computed signals plus the composite K. Returned by |
| `src/linux/Core/TradingTerminal.Core/IndexKScore/IndexKScoreParameters.cs` | 79 | linux | TradingTerminal.Core | product | Y | Configuration for the per-component K-score computation. All windows / lookbacks / |
| `src/linux/Core/TradingTerminal.Core/IndexRegime/IndexRegimeAggregator.cs` | 115 | linux | TradingTerminal.Core | product | Y | Maps a signed score in |
| `src/linux/Core/TradingTerminal.Core/IndexRegime/IndexRegimeModels.cs` | 58 | linux | TradingTerminal.Core | product | Y | One constituent timeframe column collapsed to a signed score in |
| `src/linux/Core/TradingTerminal.Core/IndexRegime/RegimeHorizon.cs` | 72 | linux | TradingTerminal.Core | product | Y | Minutes — weights the fastest columns. |
| `src/linux/Core/TradingTerminal.Core/MarketData/AdvancedRegime/AdvancedRegimeBarIndicators.cs` | 314 | linux | TradingTerminal.Core | product | Y | SMA over the final |
| `src/linux/Core/TradingTerminal.Core/MarketData/AdvancedRegime/AdvancedRegimeCalculator.cs` | 346 | linux | TradingTerminal.Core | product | Y | Final-bar Wilder RSI via the shared streaming primitive; NaN if not warmed |
| `src/linux/Core/TradingTerminal.Core/MarketData/AdvancedRegime/AdvancedRegimeModels.cs` | 101 | linux | TradingTerminal.Core | product | Y | The eight default timeframe columns, all enabled. |
| `src/linux/Core/TradingTerminal.Core/MarketData/AdvancedRegime/AdvancedRegimeSettings.cs` | 106 | linux | TradingTerminal.Core | product | Y | Whether the given row is enabled, by enum value. |
| `src/linux/Core/TradingTerminal.Core/MarketData/AdvancedRegime/BarTimeframeAggregator.cs` | 87 | linux | TradingTerminal.Core | product | Y | Pure bar-to-bar timeframe aggregator. Resamples a time-ascending base bar series into wider |
| `src/linux/Core/TradingTerminal.Core/MarketData/AdvancedRegime/IAdvancedRegimeProvider.cs` | 24 | linux | TradingTerminal.Core | product | Y | Multi-timeframe regime dashboard provider. Mirrors IInstrumentRegimeProvider in shape: |
| `src/linux/Core/TradingTerminal.Core/MarketData/Archive/ArchiveModels.cs` | 149 | linux | TradingTerminal.Core | product | Y | How often the schedule rolls over and produces a new archive. |
| `src/linux/Core/TradingTerminal.Core/MarketData/Archive/IArchiveTransport.cs` | 35 | linux | TradingTerminal.Core | product | Y | Human-readable name of this transport, persisted into manifests so the right |
| `src/linux/Core/TradingTerminal.Core/MarketData/Archive/IMarketDataArchiver.cs` | 40 | linux | TradingTerminal.Core | product | Y | Export, upload, verify, and (per options) prune local rows for the range. |
| `src/linux/Core/TradingTerminal.Core/MarketData/Archive/ITelegramArchiveLogin.cs` | 36 | linux | TradingTerminal.Core | product | Y | Outcome of a |
| `src/linux/Core/TradingTerminal.Core/MarketData/CuratedInstrumentCatalog.cs` | 83 | linux | TradingTerminal.Core | product | Y | ETFs, large-cap stocks, continuous futures and FX — a broad starter set |
| `src/linux/Core/TradingTerminal.Core/MarketData/FootprintFeatures.cs` | 334 | linux | TradingTerminal.Core | product | Y | No usable feed. |
| `src/linux/Core/TradingTerminal.Core/MarketData/FootprintTimeBucketer.cs` | 82 | linux | TradingTerminal.Core | product | Y | Start of the bucket currently accumulating, or |
| `src/linux/Core/TradingTerminal.Core/MarketData/IBrokerClient.cs` | 110 | linux | TradingTerminal.Core | product | Y | Internal abstraction over a market-data + connection backend (IB, NinjaTrader, ...). |
| `src/linux/Core/TradingTerminal.Core/MarketData/IInstrumentRegistry.cs` | 38 | linux | TradingTerminal.Core | product | Y | Look up a canonical instrument by id, or null if unknown. |
| `src/linux/Core/TradingTerminal.Core/MarketData/IMarketDataHub.cs` | 27 | linux | TradingTerminal.Core | product | Y | The live, in-memory, broker-agnostic publish/subscribe bus for normalized market data. |
| `src/linux/Core/TradingTerminal.Core/MarketData/IMarketDataIngest.cs` | 37 | linux | TradingTerminal.Core | product | Y | Resolve (creating if needed) the canonical id for a broker contract on |
| `src/linux/Core/TradingTerminal.Core/MarketData/IMarketDataRepository.cs` | 73 | linux | TradingTerminal.Core | product | Y | The single facade for market data. Hides broker SDKs entirely — no |
| `src/linux/Core/TradingTerminal.Core/MarketData/IMarketDataStore.cs` | 84 | linux | TradingTerminal.Core | product | Y | Queue a quote for batched persistence. Returns immediately. |
| `src/linux/Core/TradingTerminal.Core/MarketData/IQuestDbLauncher.cs` | 24 | linux | TradingTerminal.Core | product | Y | True only when the QuestDB backend is configured — otherwise there's nothing |
| `src/linux/Core/TradingTerminal.Core/MarketData/Indicators.cs` | 145 | linux | TradingTerminal.Core | product | Y | EMA: y_t = α·x_t + (1-α)·y_{t-1}, with α = 2 / (period |
| `src/linux/Core/TradingTerminal.Core/MarketData/InstrumentDataView.cs` | 71 | linux | TradingTerminal.Core | product | Y | The instrument this facade is scoped to. |
| `src/linux/Core/TradingTerminal.Core/MarketData/Microstructure.cs` | 186 | linux | TradingTerminal.Core | product | Y | Half-spread in price units: |
| `src/linux/Core/TradingTerminal.Core/MarketData/OrderFlowImbalance.cs` | 65 | linux | TradingTerminal.Core | product | Y | Number of discrete OBI regimes (Section 3.4): nine ordered bins from strongly |
| `src/linux/Core/TradingTerminal.Core/MarketData/Sp100Sp500Catalog.cs` | 328 | linux | TradingTerminal.Core | product | Y | A single index constituent: its US-stock ticker and company name. |
| `src/linux/Core/TradingTerminal.Core/MarketData/StoredDataExtent.cs` | 24 | linux | TradingTerminal.Core | product | Y | True when there's at least one row, i.e. both bounds are set. |
| `src/linux/Core/TradingTerminal.Core/MarketData/TradableInstrument.cs` | 22 | linux | TradingTerminal.Core | product | Y | A broker-neutral, user-facing tradable instrument: a display label, a grouping |
| `src/linux/Core/TradingTerminal.Core/MarketData/VolumeTimeBucketer.cs` | 125 | linux | TradingTerminal.Core | product | Y | Total volume in the bucket (≈ the target size B, modulo the |
| `src/linux/Core/TradingTerminal.Core/Ml/DepthStepSampler.cs` | 67 | linux | TradingTerminal.Core | product | Y | The last step boundary a summary was emitted for — the caller's |
| `src/linux/Core/TradingTerminal.Core/Ml/EwmaForecaster.cs` | 67 | linux | TradingTerminal.Core | product | Y | Algorithm discriminator stored in |
| `src/linux/Core/TradingTerminal.Core/Ml/FactorComputation.cs` | 193 | linux | TradingTerminal.Core | product | Y | One aggregated bar over |
| `src/linux/Core/TradingTerminal.Core/Ml/FootprintForecastProvider.cs` | 488 | linux | TradingTerminal.Core | product | Y | Identifies one footprint series without coupling callers to a model implementation. |
| `src/linux/Core/TradingTerminal.Core/Ml/FootprintNextBarPredictor.cs` | 408 | linux | TradingTerminal.Core | product | Y | Model-family discriminator this predictor's artifacts are filed under in the registry. |
| `src/linux/Core/TradingTerminal.Core/Ml/FootprintPredictionModels.cs` | 89 | linux | TradingTerminal.Core | product | Y | Projects a sealed Core bar (plus the render-layer argmax POCs and value |
| `src/linux/Core/TradingTerminal.Core/Ml/Forecasters.cs` | 88 | linux | TradingTerminal.Core | product | Y | Recursive least squares with exponential forgetting — adaptive, second-order, |
| `src/linux/Core/TradingTerminal.Core/Ml/IModelRegistry.cs` | 58 | linux | TradingTerminal.Core | product | Y | What the registry hands back after a successful |
| `src/linux/Core/TradingTerminal.Core/Ml/IOnlineForecaster.cs` | 60 | linux | TradingTerminal.Core | product | Y | Discriminator identifying the algorithm — matches |
| `src/linux/Core/TradingTerminal.Core/Ml/ModelArtifact.cs` | 139 | linux | TradingTerminal.Core | product | Y | Current on-disk schema version. Increment on any breaking shape change. |
| `src/linux/Core/TradingTerminal.Core/Ml/ModelArtifactJson.cs` | 22 | linux | TradingTerminal.Core | product | Y | Canonical System.Text.Json settings for serializing (and its option |
| `src/linux/Core/TradingTerminal.Core/Ml/OnlineFeatureScaler.cs` | 116 | linux | TradingTerminal.Core | product | Y | Folds one raw feature vector into the running mean/variance estimates. |
| `src/linux/Core/TradingTerminal.Core/Ml/OnlineGradientDescent.cs` | 70 | linux | TradingTerminal.Core | product | Y | Algorithm discriminator stored in |
| `src/linux/Core/TradingTerminal.Core/Ml/OnlineLinearRegression.cs` | 120 | linux | TradingTerminal.Core | product | Y | Algorithm discriminator stored in |
| `src/linux/Core/TradingTerminal.Core/Ml/OnlineLogisticRegression.cs` | 73 | linux | TradingTerminal.Core | product | Y | Algorithm discriminator stored in |
| `src/linux/Core/TradingTerminal.Core/Ml/OrderBookEventLabeler.cs` | 30 | linux | TradingTerminal.Core | product | Y | Spread widened: the max spread observed over the window reached at least |
| `src/linux/Core/TradingTerminal.Core/Ml/OrderBookMicroPredictor.cs` | 527 | linux | TradingTerminal.Core | product | Y | Model-family discriminator this predictor's artifacts are filed under in the registry. |
| `src/linux/Core/TradingTerminal.Core/Ml/OrderBookPredictionModels.cs` | 178 | linux | TradingTerminal.Core | product | Y | Projects one depth snapshot (plus the accumulated flow) into the step shape, |
| `src/linux/Core/TradingTerminal.Core/Ml/RollingBrierScore.cs` | 60 | linux | TradingTerminal.Core | product | Y | Scores one realized event forecast. Non-finite probabilities are ignored; |
| `src/linux/Core/TradingTerminal.Core/Ml/RollingForecastMetrics.cs` | 63 | linux | TradingTerminal.Core | product | Y | Scores one realized forecast, both in ticks relative to the reference bar's |
| `src/linux/Core/TradingTerminal.Core/Ml/TripleBarrierLabeler.cs` | 79 | linux | TradingTerminal.Core | product | Y | López de Prado (2018) "Advances in Financial Machine Learning" — triple-barrier |
| `src/linux/Core/TradingTerminal.Core/Notifications/INotificationEnricher.cs` | 20 | linux | TradingTerminal.Core | product | Y | Whether this enricher should run for this notification. |
| `src/linux/Core/TradingTerminal.Core/Notifications/INotificationPublisher.cs` | 14 | linux | TradingTerminal.Core | product | Y | What strategies call to surface a signal/trade. Implementations buffer and dispatch |
| `src/linux/Core/TradingTerminal.Core/Notifications/INotificationTransport.cs` | 17 | linux | TradingTerminal.Core | product | Y | Stable name for diagnostics ("telegram", "discord"). |
| `src/linux/Core/TradingTerminal.Core/Notifications/ISignalGate.cs` | 15 | linux | TradingTerminal.Core | product | Y | True if this notification should be dropped before reaching the transports. |
| `src/linux/Core/TradingTerminal.Core/Notifications/NotificationKind.cs` | 25 | linux | TradingTerminal.Core | product | Y | An armed-and-fired signal — the strategy would (or did) act on it. |
| `src/linux/Core/TradingTerminal.Core/Notifications/StrategyNotification.cs` | 24 | linux | TradingTerminal.Core | product | Y | One unit of "something a strategy wants the user to know about." |
| `src/linux/Core/TradingTerminal.Core/Quant/CurveFitting.cs` | 276 | linux | TradingTerminal.Core | product | Y | Curve families available to chart overlay fits (e.g. the footprint POC regressions). |
| `src/linux/Core/TradingTerminal.Core/Quant/DeflatedSharpe.cs` | 128 | linux | TradingTerminal.Core | product | Y | Standard-normal CDF Φ(x) via the complementary error function. |
| `src/linux/Core/TradingTerminal.Core/Quant/EwRegression.cs` | 186 | linux | TradingTerminal.Core | product | Y | Fitted value ŷ = α + β·x at an arbitrary x. |
| `src/linux/Core/TradingTerminal.Core/Quant/FirstPassage.cs` | 80 | linux | TradingTerminal.Core | product | Y | The pure continuous-path first-passage probability (no gap adjustment). |
| `src/linux/Core/TradingTerminal.Core/Quant/HawkesProcess.cs` | 109 | linux | TradingTerminal.Core | product | Y | Current decayed excitation sum S at the last event time (diagnostic). |
| `src/linux/Core/TradingTerminal.Core/Quant/InformationCoefficient.cs` | 104 | linux | TradingTerminal.Core | product | Y | Fractional ranks with tie-averaging. |
| `src/linux/Core/TradingTerminal.Core/Quant/IsotonicCalibration.cs` | 139 | linux | TradingTerminal.Core | product | Y | g(C): the calibrated expected forward return at composite C (interpolated). |
| `src/linux/Core/TradingTerminal.Core/Quant/KalmanPocPredictor.cs` | 151 | linux | TradingTerminal.Core | product | Y | True once at least one observation has initialised the state. |
| `src/linux/Core/TradingTerminal.Core/Quant/KyleResidual.cs` | 154 | linux | TradingTerminal.Core | product | Y | Result of a rolling Kyle-lambda regression r = λ·Δ + ε over |
| `src/linux/Core/TradingTerminal.Core/Quant/LedoitWolf.cs` | 197 | linux | TradingTerminal.Core | product | Y | Converts a covariance matrix to a correlation matrix (unit diagonal; guards σ→0). |
| `src/linux/Core/TradingTerminal.Core/Quant/NeweyWest.cs` | 83 | linux | TradingTerminal.Core | product | Y | Default automatic bandwidth L = floor(4·(n/100)^(2/9)), at least 0. |
| `src/linux/Core/TradingTerminal.Core/Quant/SignalWeights.cs` | 57 | linux | TradingTerminal.Core | product | Y | Combination-weight solver for blending k signals into one composite. The mean-variance optimal |
| `src/linux/Core/TradingTerminal.Core/Quant/Surfaces/LiveBarSeries.cs` | 112 | linux | TradingTerminal.Core | product | Y | Committed bars + the forming bar (if any). |
| `src/linux/Core/TradingTerminal.Core/Quant/Surfaces/SurfaceAxes.cs` | 132 | linux | TradingTerminal.Core | product | Y | Top-level surface mode — decides what the X/Y axes can be bound |
| `src/linux/Core/TradingTerminal.Core/Quant/Surfaces/SurfaceFormulaParser.cs` | 260 | linux | TradingTerminal.Core | product | Y | Distinct identifiers referenced by the formula (lower-cased). |
| `src/linux/Core/TradingTerminal.Core/Quant/Surfaces/SurfaceGridBuilder.cs` | 486 | linux | TradingTerminal.Core | product | Y | One configured axis: the picked option id, the bin range (ignored when |
| `src/linux/Core/TradingTerminal.Core/Quant/Surfaces/SurfaceMetrics.cs` | 245 | linux | TradingTerminal.Core | product | Y | How a surface axis value should be rendered (tick labels, tooltips, stats). |
| `src/linux/Core/TradingTerminal.Core/Quant/TimeSeries/ArimaModel.cs` | 233 | linux | TradingTerminal.Core | product | Y | A fitted ARIMA(p,d,q) model over a (log-)price series. |
| `src/linux/Core/TradingTerminal.Core/Quant/TimeSeries/GarchModel.cs` | 102 | linux | TradingTerminal.Core | product | Y | Fits GARCH(1,1) to a return series (fractional returns, not %). Null when |
| `src/linux/Core/TradingTerminal.Core/Quant/TimeSeries/KalmanFilters.cs` | 235 | linux | TradingTerminal.Core | product | Y | Local level model. |
| `src/linux/Core/TradingTerminal.Core/Quant/TimeSeries/NelderMead.cs` | 90 | linux | TradingTerminal.Core | product | Y | x = centroid + coeff·(centroid − worst); coeff −0.5 gives the inside |
| `src/linux/Core/TradingTerminal.Core/Quant/TimeSeries/Ols.cs` | 125 | linux | TradingTerminal.Core | product | Y | Gauss-Jordan inverse with partial pivoting; null when singular. |
| `src/linux/Core/TradingTerminal.Core/Quant/TimeSeries/SeriesTransforms.cs` | 149 | linux | TradingTerminal.Core | product | Y | Stationarity-inducing transform applied to a price series before testing/modelling. |
| `src/linux/Core/TradingTerminal.Core/Quant/TimeSeries/StationarityTests.cs` | 195 | linux | TradingTerminal.Core | product | Y | KPSS level-stationarity critical values (Kwiatkowski et al. 1992, Table 1, η_μ). |
| `src/linux/Core/TradingTerminal.Core/QuantConnect/ILeanClient.cs` | 33 | linux | TradingTerminal.Core | product | Y | Which backend this instance drives. |
| `src/linux/Core/TradingTerminal.Core/QuantConnect/LeanModels.cs` | 68 | linux | TradingTerminal.Core | product | Y | How LEAN backtests are executed. The seam is engine-agnostic so the cloud |
| `src/linux/Core/TradingTerminal.Core/QuantConnect/LeanOptions.cs` | 35 | linux | TradingTerminal.Core | product | Y | Local CLI now; Cloud is reserved for the future REST client. |
| `src/linux/Core/TradingTerminal.Core/Regime/IMarketRegimeProvider.cs` | 22 | linux | TradingTerminal.Core | product | Y | The most recent snapshot, or |
| `src/linux/Core/TradingTerminal.Core/Regime/Instrument/IInstrumentRegimeProvider.cs` | 25 | linux | TradingTerminal.Core | product | Y | Pull recent bars + optional depth, compute and return a snapshot. Folds |
| `src/linux/Core/TradingTerminal.Core/Regime/Instrument/InstrumentRegimeBand.cs` | 38 | linux | TradingTerminal.Core | product | Y | Maps a signed composite score in |
| `src/linux/Core/TradingTerminal.Core/Regime/Instrument/InstrumentRegimeCalculator.cs` | 291 | linux | TradingTerminal.Core | product | Y | Percentile rank of the current ATR within the trailing |
| `src/linux/Core/TradingTerminal.Core/Regime/Instrument/InstrumentRegimeInputs.cs` | 20 | linux | TradingTerminal.Core | product | Y | Inputs to . Bars are required; |
| `src/linux/Core/TradingTerminal.Core/Regime/Instrument/InstrumentRegimeSignal.cs` | 33 | linux | TradingTerminal.Core | product | Y | Close vs N-period SMA — bullish above, bearish below. |
| `src/linux/Core/TradingTerminal.Core/Regime/Instrument/InstrumentRegimeSnapshot.cs` | 53 | linux | TradingTerminal.Core | product | Y | One per-instrument regime computation: the signed composite, its band, the breakdown of |
| `src/linux/Core/TradingTerminal.Core/Regime/Instrument/InstrumentSignalScore.cs` | 16 | linux | TradingTerminal.Core | product | Y | One scored sub-signal of the per-instrument composite. is normalised to |
| `src/linux/Core/TradingTerminal.Core/Regime/MarketRegimeCalculator.cs` | 320 | linux | TradingTerminal.Core | product | Y | Composite weights — sum to 1.0. Matches the upstream WEIGHTS table. |
| `src/linux/Core/TradingTerminal.Core/Regime/MarketRegimeSnapshot.cs` | 48 | linux | TradingTerminal.Core | product | Y | Sentinel for "we have no data yet" — shown before the first |
| `src/linux/Core/TradingTerminal.Core/Regime/RegimeCategory.cs` | 39 | linux | TradingTerminal.Core | product | Y | Survey + index sentiment (CNN Fear &amp; Greed, AAII bull/bear). |
| `src/linux/Core/TradingTerminal.Core/Regime/RegimeCategoryScore.cs` | 16 | linux | TradingTerminal.Core | product | Y | One scored sub-signal of the composite. is the points this |
| `src/linux/Core/TradingTerminal.Core/Regime/RegimeInputs.cs` | 53 | linux | TradingTerminal.Core | product | Y | Sector ETF close series keyed by symbol (XLK, XLF, …) for the |
| `src/linux/Core/TradingTerminal.Core/Regime/RegimeState.cs` | 42 | linux | TradingTerminal.Core | product | Y | Maps a 0–100 composite score to its band. |
| `src/linux/Core/TradingTerminal.Core/Research/EnvHash.cs` | 17 | linux | TradingTerminal.Core | product | Y | An unresolved/unknown environment (e.g. a reproduction that never built). |
| `src/linux/Core/TradingTerminal.Core/Research/EnvResolutionPlan.cs` | 28 | linux | TradingTerminal.Core | product | Y | An empty/unresolved plan carrying just an |
| `src/linux/Core/TradingTerminal.Core/Research/IEnvResolverClient.cs` | 24 | linux | TradingTerminal.Core | product | Y | True when this client expects the sidecar to be reachable (Http when |
| `src/linux/Core/TradingTerminal.Core/Research/IPaperIngestClient.cs` | 37 | linux | TradingTerminal.Core | product | Y | An empty/failed result carrying the reason. |
| `src/linux/Core/TradingTerminal.Core/Research/IReplicationConfidenceScorer.cs` | 15 | linux | TradingTerminal.Core | product | Y | Score a reproduction. A failed result or empty manifest scores low (never |
| `src/linux/Core/TradingTerminal.Core/Research/IReproJobStore.cs` | 33 | linux | TradingTerminal.Core | product | Y | Insert or update a job (keyed by |
| `src/linux/Core/TradingTerminal.Core/Research/IReproOrchestrator.cs` | 27 | linux | TradingTerminal.Core | product | Y | Submit a spec. Returns the cached succeeded job if one exists for |
| `src/linux/Core/TradingTerminal.Core/Research/IReproSignalBridge.cs` | 19 | linux | TradingTerminal.Core | product | Y | Map a succeeded result's declared artifact to a time-sorted, InstrumentId-keyed |
| `src/linux/Core/TradingTerminal.Core/Research/ISandboxRunner.cs` | 42 | linux | TradingTerminal.Core | product | Y | Which backend this runner implements. |
| `src/linux/Core/TradingTerminal.Core/Research/MinimalReproPlan.cs` | 24 | linux | TradingTerminal.Core | product | Y | An empty/failed plan carrying the reason. |
| `src/linux/Core/TradingTerminal.Core/Research/PaperRef.cs` | 9 | linux | TradingTerminal.Core | product | Y | A resolved research paper: its arXiv id, title, and canonical URL. The |
| `src/linux/Core/TradingTerminal.Core/Research/ReplicationConfidence.cs` | 15 | linux | TradingTerminal.Core | product | Y | No confidence (e.g. a failed or not-yet-scored reproduction). |
| `src/linux/Core/TradingTerminal.Core/Research/ReplicationCostEstimate.cs` | 14 | linux | TradingTerminal.Core | product | Y | A zero/unknown estimate (e.g. before any minimal run has completed). |
| `src/linux/Core/TradingTerminal.Core/Research/RepoRef.cs` | 9 | linux | TradingTerminal.Core | product | Y | A candidate code repository for a paper, pinned to an exact . |
| `src/linux/Core/TradingTerminal.Core/Research/ReproArtifact.cs` | 13 | linux | TradingTerminal.Core | product | Y | A single output file produced by a reproduction, identified by its declared |
| `src/linux/Core/TradingTerminal.Core/Research/ReproJob.cs` | 45 | linux | TradingTerminal.Core | product | Y | True when the job has reached a terminal state and will not |
| `src/linux/Core/TradingTerminal.Core/Research/ReproResult.cs` | 31 | linux | TradingTerminal.Core | product | Y | A failed result carrying the reason and (where known) the provenance triple. |
| `src/linux/Core/TradingTerminal.Core/Research/ReproSignalKind.cs` | 24 | linux | TradingTerminal.Core | product | Y | A continuous score/prediction (e.g. a forecasted return or alpha). Carried as |
| `src/linux/Core/TradingTerminal.Core/Research/ReproSignalManifest.cs` | 59 | linux | TradingTerminal.Core | product | Y | An empty manifest carrying whatever provenance is known — the never-throw failure |
| `src/linux/Core/TradingTerminal.Core/Research/ReproSpec.cs` | 44 | linux | TradingTerminal.Core | product | Y | Convenience for a spec with no extra config. |
| `src/linux/Core/TradingTerminal.Core/Research/ReproStatus.cs` | 36 | linux | TradingTerminal.Core | product | Y | Accepted and waiting for a sandbox slot. |
| `src/linux/Core/TradingTerminal.Core/Research/ReproducedSignal.cs` | 22 | linux | TradingTerminal.Core | product | Y | One timestamped output of a reproduction, mapped onto canonical identity: an |
| `src/linux/Core/TradingTerminal.Core/Research/SandboxKind.cs` | 18 | linux | TradingTerminal.Core | product | Y | A disposable Docker container (deny-by-default isolation). |
| `src/linux/Core/TradingTerminal.Core/Research/SandboxPolicy.cs` | 35 | linux | TradingTerminal.Core | product | Y | True when no egress host is allowed — the runner must pass |
| `src/linux/Core/TradingTerminal.Core/Research/SandboxQuota.cs` | 19 | linux | TradingTerminal.Core | product | Y | The strict default: 1 CPU, 1 GiB RAM, 256 pids, 1 GiB |
| `src/linux/Core/TradingTerminal.Core/Risk/IRiskManager.cs` | 27 | linux | TradingTerminal.Core | product | Y | Pre-trade risk check. Sits between the strategy and the broker / simulated |
| `src/linux/Core/TradingTerminal.Core/Risk/RiskManager.cs` | 120 | linux | TradingTerminal.Core | product | Y | Current net signed position per symbol — exposed for telemetry / tests. |
| `src/linux/Core/TradingTerminal.Core/Risk/RiskOptions.cs` | 31 | linux | TradingTerminal.Core | product | Y | Maximum absolute net position per symbol, in contracts/shares. 0 = disabled. |
| `src/linux/Core/TradingTerminal.Core/Session/SessionContext.cs` | 32 | linux | TradingTerminal.Core | product | Y | Mutable singleton populated by the login flow once the user is authenticated. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Authoring/AiModelChoice.cs` | 23 | linux | TradingTerminal.Core | product | Y | False when the provider isn't usable right now (CLI not installed, no |
| `src/linux/Core/TradingTerminal.Core/Strategies/Authoring/IAiKeyResolver.cs` | 37 | linux | TradingTerminal.Core | product | Y | The API key for |
| `src/linux/Core/TradingTerminal.Core/Strategies/Authoring/IAuthoredStrategyViewComposer.cs` | 24 | linux | TradingTerminal.Core | product | Y | Composes the default live view for |
| `src/linux/Core/TradingTerminal.Core/Strategies/Authoring/IStrategyCodegenClient.cs` | 256 | linux | TradingTerminal.Core | product | Y | Who is speaking in a codegen conversation. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Authoring/IStrategyCompiler.cs` | 21 | linux | TradingTerminal.Core | product | Y | Compiles a user-authored into a runnable |
| `src/linux/Core/TradingTerminal.Core/Strategies/Authoring/StrategyBuildEffort.cs` | 70 | linux | TradingTerminal.Core | product | Y | Sketch fast: one skill pack, one fix attempt, no review, no smoke. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Authoring/StrategyCompileResult.cs` | 73 | linux | TradingTerminal.Core | product | Y | True when the author supplied a complete hand-written window: the descriptor, a |
| `src/linux/Core/TradingTerminal.Core/Strategies/Authoring/StrategyDiagnostic.cs` | 36 | linux | TradingTerminal.Core | product | Y | Severity of a |
| `src/linux/Core/TradingTerminal.Core/Strategies/Authoring/StrategyScript.cs` | 36 | linux | TradingTerminal.Core | product | Y | The name given to a single unnamed file (the AI didn't label |
| `src/linux/Core/TradingTerminal.Core/Strategies/Authoring/TradeIrSimulatedBacktestContractsV1.cs` | 121 | linux | TradingTerminal.Core | product | Y | Host-owned public facts for the deterministic synthetic QuoteL1 smoke adapter. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/ExecutableStrategyDefinitionCanonicalJson.cs` | 269 | linux | TradingTerminal.Core | product | Y | RFC 8785 JSON Canonicalization Scheme used to identify executable strategy definitions. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/OperatorGraphModuleCanonicalJsonV1.cs` | 44 | linux | TradingTerminal.Core | product | Y | Canonical persisted identity for an . Serializing the concrete |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyCompilationAdmissionManifestV1.cs` | 209 | linux | TradingTerminal.Core | product | Y | Content pins for the one exact source binding admitted for a data |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyCompilationAdmissionV1.cs` | 178 | linux | TradingTerminal.Core | product | Y | No compiler may run until semantic, target, and exact data-binding gates pass. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyIntermediateRepresentationV1.cs` | 193 | linux | TradingTerminal.Core | product | Y | The only values a strategy definition may export across the host boundary. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyIrTargetAssessmentV1.cs` | 127 | linux | TradingTerminal.Core | product | Y | One concrete compiler/execution-host/data/runtime target. Semantic validity and target support are |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyIrValidatorV1.cs` | 722 | linux | TradingTerminal.Core | product | Y | Deterministic structural, causal, graph, type, and placement validation. It never asks the |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyOperatorRegistryV1.cs` | 685 | linux | TradingTerminal.Core | product | Y | Trusted product metadata for one operator version. The model supplies only the |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyValueTypeRulesV1.cs` | 105 | linux | TradingTerminal.Core | product | Y | Structural rules shared by trusted operator output validation and isolated extension-module |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/TradeIrDataAdmissionValidator.cs` | 1034 | linux | TradingTerminal.Core | product | Y | Stable fail-closed reasons emitted by |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/TradeIrDataContracts.cs` | 205 | linux | TradingTerminal.Core | product | Y | The closed set of canonical input families understood by TradeIR v1 authoring. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/TradeIrModuleValidatorV1.cs` | 193 | linux | TradingTerminal.Core | product | Y | Pure pre-compilation validation for module identity, interface, determinism, and requested |
| `src/linux/Core/TradingTerminal.Core/Strategies/Definition/TradeIrModules.cs` | 156 | linux | TradingTerminal.Core | product | Y | A location-independent content identity for source, model, schema, or runtime bytes. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/AuthoredUnitCompilationV1.cs` | 43 | linux | TradingTerminal.Core | product | Y | The verified emitted assembly for one exact authored-unit specification. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/AuthoredUnitSpecificationV1.cs` | 775 | linux | TradingTerminal.Core | product | Y | The runnable product artifact Hyperion is being asked to author. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/ChartPatternSimilarityV1.cs` | 232 | linux | TradingTerminal.Core | product | Y | The bounded request executed against real persisted OHLCV history. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/ConfirmedStrategyIntentV1.cs` | 1327 | linux | TradingTerminal.Core | product | Y | The shape of decision output. Families remain classification lenses over these topologies. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/HistoricalValidationEvidenceV1.cs` | 82 | linux | TradingTerminal.Core | product | Y | Immutable identity supplied to a historical runner. A completed run is admissible |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/ResearchDatasetV1.cs` | 206 | linux | TradingTerminal.Core | product | Y | Fail-closed policies required before a labeled dataset can drive feature research. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/ResearchExperimentV1.cs` | 137 | linux | TradingTerminal.Core | product | Y | Reproducible exploratory evidence produced from labeled event observations. It is deliberately |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyCandidateCompositionV1.cs` | 370 | linux | TradingTerminal.Core | product | Y | The intake result: a user-visible candidate plus only the specialist work it |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyCandidateConfirmationV1.cs` | 124 | linux | TradingTerminal.Core | product | Y | Result of confirming the exact candidate revision the user reviewed. Confirmation accepts |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyCandidateLoweringRequestV1.cs` | 60 | linux | TradingTerminal.Core | product | Y | Immutable handoff from strategy generation to the separately owned executable-definition |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyCandidateV1.cs` | 189 | linux | TradingTerminal.Core | product | Y | The lifecycle of one conversationally generated strategy candidate. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyCandidateValidatorV1.cs` | 518 | linux | TradingTerminal.Core | product | Y | Separates a well-formed draft from a confirmable meaning and an executable-ready candidate. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyWorkspaceV1.cs` | 409 | linux | TradingTerminal.Core | product | Y | The six user-visible stages of one authored chart or strategy. |
| `src/linux/Core/TradingTerminal.Core/Strategies/IPluginFaultAttribution.cs` | 10 | linux | TradingTerminal.Core | product | Y | Marker for an exception that can name the runtime plugin responsible for |
| `src/linux/Core/TradingTerminal.Core/Strategies/IStrategyFactory.cs` | 36 | linux | TradingTerminal.Core | product | Y | Fires when a strategy is added after startup, so a bound catalog |
| `src/linux/Core/TradingTerminal.Core/Strategies/ITradingStrategy.cs` | 80 | linux | TradingTerminal.Core | product | Y | Stable, unique identifier (e.g. "example.nvda.3m"). Used to dedupe tabs. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Parameters/ParameterKind.cs` | 34 | linux | TradingTerminal.Core | product | Y | Whole number. Backed by |
| `src/linux/Core/TradingTerminal.Core/Strategies/Parameters/StrategyParameter.cs` | 140 | linux | TradingTerminal.Core | product | Y | Stable machine key, used to read the value back. Unique within a |
| `src/linux/Core/TradingTerminal.Core/Strategies/Parameters/StrategyParameterSchema.cs` | 45 | linux | TradingTerminal.Core | product | Y | A schema with no tunables — the default for strategies that take |
| `src/linux/Core/TradingTerminal.Core/Strategies/Parameters/StrategyParameters.cs` | 220 | linux | TradingTerminal.Core | product | Y | Sets a value, coercing and clamping it against the parameter declaration. |
| `src/linux/Core/TradingTerminal.Core/Strategies/PluginFaultEvents.cs` | 21 | linux | TradingTerminal.Core | product | Y | Relays strategy callback failures that a host catches to keep its stream |
| `src/linux/Core/TradingTerminal.Core/Strategies/Specification/StrategyCapabilityProfile.cs` | 267 | linux | TradingTerminal.Core | product | Y | One runtime semantic required for faithful execution, with an audit-friendly reason. |
| `src/linux/Core/TradingTerminal.Core/Strategies/Specification/StrategySpec.cs` | 444 | linux | TradingTerminal.Core | product | Y | The primary job the strategy is intended to perform. |
| `src/linux/Core/TradingTerminal.Core/Strategies/StrategyAssetScope.cs` | 22 | linux | TradingTerminal.Core | product | Y | Whether a strategy operates on a single instrument at a time or |
| `src/linux/Core/TradingTerminal.Core/Strategies/StrategyBrokerCapability.cs` | 58 | linux | TradingTerminal.Core | product | Y | The broker capability matrix that backs 's default: |
| `src/linux/Core/TradingTerminal.Core/Strategies/StrategyDataRequirement.cs` | 38 | linux | TradingTerminal.Core | product | Y | No declared requirement. |
| `src/linux/Core/TradingTerminal.Core/Strategies/StrategyFactoryRegistration.cs` | 11 | linux | TradingTerminal.Core | product | Y | Pure-data record describing how to build the (view, view-model) pair for a |
| `src/linux/Core/TradingTerminal.Core/Strategies/StrategyHost.cs` | 12 | linux | TradingTerminal.Core | product | Y | A concrete (view, view-model) pair plus metadata. The view and view-model are |
| `src/linux/Core/TradingTerminal.Core/Strategies/StrategySignal.cs` | 33 | linux | TradingTerminal.Core | product | Y | A strategy's direction-only signal, independent of order execution. |
| `src/linux/Core/TradingTerminal.Core/Time/IClock.cs` | 13 | linux | TradingTerminal.Core | product | Y | Wall-clock abstraction. Real code uses SystemClock (); |
| `src/linux/Core/TradingTerminal.Core/Trading/IFeeModel.cs` | 70 | linux | TradingTerminal.Core | product | Y | Whether a fill took (crossed the spread) or made (rested) liquidity. |
| `src/linux/Core/TradingTerminal.Core/Trading/IOrderRouter.cs` | 22 | linux | TradingTerminal.Core | product | Y | Cancels a working order by its client-assigned id. Idempotent. |
| `src/linux/Core/TradingTerminal.Core/Trading/OrderEvent.cs` | 19 | linux | TradingTerminal.Core | product | Y | A single transition in the lifecycle of an order. and |
| `src/linux/Core/TradingTerminal.Core/Trading/OrderRequest.cs` | 20 | linux | TradingTerminal.Core | product | Y | A request to submit a single order. is a caller-generated |
| `src/linux/Core/TradingTerminal.Core/Trading/OrderResult.cs` | 12 | linux | TradingTerminal.Core | product | Y | Synchronous return value from PlaceOrderAsync. Reflects the order's state at |
| `src/linux/Core/TradingTerminal.Core/Trading/OrderSide.cs` | 7 | linux | TradingTerminal.Core | product | Y |  |
| `src/linux/Core/TradingTerminal.Core/Trading/OrderState.cs` | 17 | linux | TradingTerminal.Core | product | Y | Lifecycle of a single order. is the optimistic local state |
| `src/linux/Core/TradingTerminal.Core/Trading/OrderType.cs` | 9 | linux | TradingTerminal.Core | product | Y |  |
| `src/linux/Core/TradingTerminal.Core/Trading/TimeInForce.cs` | 9 | linux | TradingTerminal.Core | product | Y |  |
