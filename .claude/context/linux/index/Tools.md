# macOS index / Tools

Generated from source fingerprint `e91d50e75733`. macOS/Avalonia source only.

| File | LOC | Tree | Project | Role | Public surface | Purpose |
|---|---:|---|---|---|---|---|
| `src/linux/Tools/DaxAlgo.Codegen/AgentCliCodegenClient.cs` | 405 | linux | DaxAlgo.Codegen | product | Y | Per-CLI details, isolated so one vendor's output-format drift doesn't touch the others. |
| `src/linux/Tools/DaxAlgo.Codegen/AiModelCatalog.cs` | 60 | linux | DaxAlgo.Codegen | product | Y | Anthropic model ids — the same strings Claude Code's |
| `src/linux/Tools/DaxAlgo.Codegen/AiStrategyBuilder.cs` | 106 | linux | DaxAlgo.Codegen | product | Y | Every provider the app knows how to build, available or not — |
| `src/linux/Tools/DaxAlgo.Codegen/AnthropicCodegenClient.cs` | 314 | linux | DaxAlgo.Codegen | product | Y | The models this key can actually call. A failure here is not |
| `src/linux/Tools/DaxAlgo.Codegen/AnthropicStreamParser.cs` | 111 | linux | DaxAlgo.Codegen | product | Y | Everything the model has written so far. |
| `src/linux/Tools/DaxAlgo.Codegen/AuthoredUnitIntentClassifierV1.cs` | 119 | linux | DaxAlgo.Codegen | product | Y | Chooses the product lane before semantic strategy research begins. It cannot create |
| `src/linux/Tools/DaxAlgo.Codegen/AuthoredUnitSourceGeneratorV1.cs` | 221 | linux | DaxAlgo.Codegen | product | Y | Converts one reviewed authored-unit specification into SDK source. The model may choose |
| `src/linux/Tools/DaxAlgo.Codegen/AuthoredUnitSpecificationGeneratorV1.cs` | 419 | linux | DaxAlgo.Codegen | product | Y | AI-assisted lowering from reviewed language/reference evidence into a launchable authored-unit |
| `src/linux/Tools/DaxAlgo.Codegen/ChartReferenceInspectorV1.cs` | 169 | linux | DaxAlgo.Codegen | product | Y | Sends the exact hash-verified image to a multimodal provider and accepts only |
| `src/linux/Tools/DaxAlgo.Codegen/CliWorkspaceLauncher.cs` | 371 | linux | DaxAlgo.Codegen | product | Y | What |
| `src/linux/Tools/DaxAlgo.Codegen/CodegenCodeExtractor.cs` | 148 | linux | DaxAlgo.Codegen | product | Y | A bare file name mentioned in prose/info strings — |
| `src/linux/Tools/DaxAlgo.Codegen/FakeCodegenClient.cs` | 82 | linux | DaxAlgo.Codegen | product | Y | How many times the loop asked this client to generate — the |
| `src/linux/Tools/DaxAlgo.Codegen/OpenAiCompatibleCodegenClient.cs` | 298 | linux | DaxAlgo.Codegen | product | Y | Every OpenAI-compatible endpoint (including Ollama) exposes |
| `src/linux/Tools/DaxAlgo.Codegen/ParallelStrategyCandidateGeneratorV1.cs` | 1026 | linux | DaxAlgo.Codegen | product | Y | Conservative host-owned gate for the single optional repair call. It prefers stopping |
| `src/linux/Tools/DaxAlgo.Codegen/ParallelStrategyGenerationContractsV1.cs` | 1063 | linux | DaxAlgo.Codegen | product | Y | The four authoring representations offered for every strategy brief. |
| `src/linux/Tools/DaxAlgo.Codegen/ParallelStrategyGenerationPromptV1.cs` | 615 | linux | DaxAlgo.Codegen | product | Y |  |
| `src/linux/Tools/DaxAlgo.Codegen/StrategyBacktestSmoke.cs` | 112 | linux | DaxAlgo.Codegen | product | Y | Ticks fed through |
| `src/linux/Tools/DaxAlgo.Codegen/StrategyBuildSession.cs` | 469 | linux | DaxAlgo.Codegen | product | Y | What one turn of the conversation produced. |
| `src/linux/Tools/DaxAlgo.Codegen/StrategyCandidateGenerationOrchestratorV1.cs` | 696 | linux | DaxAlgo.Codegen | product | Y | One intake or revision request. CurrentCandidate is null for the original idea; |
| `src/linux/Tools/DaxAlgo.Codegen/StrategyCandidateGenerationPromptV1.cs` | 187 | linux | DaxAlgo.Codegen | product | Y |  |
| `src/linux/Tools/DaxAlgo.Codegen/StrategyCodegenClientFactory.cs` | 161 | linux | DaxAlgo.Codegen | product | Y | Every provider the app knows how to build — installed agent CLIs, |
| `src/linux/Tools/DaxAlgo.Codegen/StrategyCodegenOrchestrator.cs` | 80 | linux | DaxAlgo.Codegen | product | Y | The result of a one-shot build: whether it produced a compiling strategy, |
| `src/linux/Tools/DaxAlgo.Codegen/StrategyCodegenServiceCollectionExtensions.cs` | 95 | linux | DaxAlgo.Codegen | product | Y | Wires the AI Strategy Builder into DI. Called once per shell from |
| `src/linux/Tools/DaxAlgo.Codegen/StrategyContextPack.cs` | 31 | linux | DaxAlgo.Codegen | product | Y | The pack text — the codegen system prompt. |
| `src/linux/Tools/DaxAlgo.Codegen/StrategyGenerationPackageCatalogV1.cs` | 622 | linux | DaxAlgo.Codegen | product | Y | Compatibility text for consumers that explain the absent runtime boundary. |
| `src/linux/Tools/DaxAlgo.Codegen/StrategyGenerationSessionV1.cs` | 131 | linux | DaxAlgo.Codegen | product | Y | One user-facing strategy-generation conversation. It persists accepted candidate revisions, not |
| `src/linux/Tools/DaxAlgo.Codegen/StrategySkillLibrary.cs` | 163 | linux | DaxAlgo.Codegen | product | Y | One on-demand domain pack: what it knows, and the words that mean |
| `src/linux/Tools/DaxAlgo.Codegen/TradeIrCandidateSynthesisV1.cs` | 596 | linux | DaxAlgo.Codegen | product | Y |  |
| `src/linux/Tools/DaxAlgo.Codegen/VibeQuantDeclarativeRulesContractV1.cs` | 785 | linux | DaxAlgo.Codegen | product | Y | Deterministic structural enforcement for the closed Vibe Quant Declarative Rules v1 document. |
| `src/linux/Tools/DaxAlgo.Coordinator.Cli/CliApplication.cs` | 641 | linux | DaxAlgo.Coordinator.Cli | product | Y |  |
| `src/linux/Tools/DaxAlgo.Coordinator.Cli/CliArguments.cs` | 54 | linux | DaxAlgo.Coordinator.Cli | product | Y |  |
| `src/linux/Tools/DaxAlgo.Coordinator.Cli/CoordinatorCliConfig.cs` | 134 | linux | DaxAlgo.Coordinator.Cli | product | Y |  |
| `src/linux/Tools/DaxAlgo.Coordinator.Cli/CoordinatorRuntime.cs` | 64 | linux | DaxAlgo.Coordinator.Cli | product | Y |  |
| `src/linux/Tools/DaxAlgo.Coordinator.Cli/Program.cs` | 17 | linux | DaxAlgo.Coordinator.Cli | product | Y |  |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Authoring/DaxqAuthoringContracts.cs` | 73 | linux | DaxAlgo.Daxq.Compiler | product | Y | Frozen SDK ABI 3 indicator identifiers available to protected strategies. |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Compilation/DaxqRoslynCompiler.cs` | 105 | linux | DaxAlgo.Daxq.Compiler | product | Y | Deterministic Roslyn front-end followed by the blocking DAXQ IL subset gate. |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/DaxqCompiler.cs` | 98 | linux | DaxAlgo.Daxq.Compiler | product | Y | Complete server-side Roslyn-to-signed-DAXQ compiler pipeline. |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/DaxqCompilerModels.cs` | 101 | linux | DaxAlgo.Daxq.Compiler | product | Y | One compiler diagnostic suitable for a submission response. |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Lowering/DaxqIlLowerer.cs` | 1461 | linux | DaxAlgo.Daxq.Compiler | product | Y | Blocking ECMA-335 subset verifier and deterministic IL-to-DAXQ lowerer. |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Lowering/DaxqPlaintextBuilder.cs` | 714 | linux | DaxAlgo.Daxq.Compiler | product | Y | One encoded-to-canonical opcode-map entry written to DQXP section 3. |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Packaging/DaxqCanonicalJson.cs` | 166 | linux | DaxAlgo.Daxq.Compiler | product | Y |  |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Packaging/DaxqPackageModels.cs` | 65 | linux | DaxAlgo.Daxq.Compiler | product | Y | Inputs required to seal and release-sign one format-v1 DAXQ package. |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Packaging/DaxqPackageValidation.cs` | 291 | linux | DaxAlgo.Daxq.Compiler | product | Y |  |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Packaging/DaxqPackageWriter.cs` | 175 | linux | DaxAlgo.Daxq.Compiler | product | Y | Seals authenticated DQXP plaintext into a canonical, release-signed DAXQ v1 package. |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqBacktestParityGate.cs` | 747 | linux | DaxAlgo.Daxq.Compiler | product | Y | Server-only publication gate comparing a reviewed managed build with released DAXQ. |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqBacktestParityModels.cs` | 148 | linux | DaxAlgo.Daxq.Compiler | product | Y | The publication decision produced by the server-side backtest-parity gate. |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqBacktestParityOutputJson.cs` | 24 | linux | DaxAlgo.Daxq.Compiler | product | Y |  |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqBacktestStatisticsJson.cs` | 31 | linux | DaxAlgo.Daxq.Compiler | product | Y |  |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqListingMetricsAccumulator.cs` | 200 | linux | DaxAlgo.Daxq.Compiler | product | Y | Implements daxq-listing-metrics-v1. The last signal emitted by one callback becomes a fixed |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqListingMetricsJson.cs` | 36 | linux | DaxAlgo.Daxq.Compiler | product | Y |  |
| `src/linux/Tools/DaxAlgo.Daxq.Compiler/Program.cs` | 425 | linux | DaxAlgo.Daxq.Compiler | product | Y |  |
| `src/linux/Tools/DaxAlgo.Strategy.BundleTool/Program.cs` | 772 | linux | DaxAlgo.Strategy.BundleTool | product | Y |  |
| `src/linux/Tools/DaxAlgo.StrategyTool/ProcessRunner.cs` | 64 | linux | DaxAlgo.StrategyTool | product | Y | Thin subprocess helper — runs a command, streams its output to the |
| `src/linux/Tools/DaxAlgo.StrategyTool/Program.cs` | 237 | linux | DaxAlgo.StrategyTool | product | N |  |
| `src/linux/Tools/TradingTerminal.AdvancedMarketRegime/AdvancedMarketRegimeServiceCollectionExtensions.cs` | 21 | linux | TradingTerminal.AdvancedMarketRegime | product | Y | DI registration for the Advanced Live Market Regime dashboard, including the |
| `src/linux/Tools/TradingTerminal.AdvancedMarketRegime/AdvancedMarketRegimeView.xaml.cs` | 11 | linux | TradingTerminal.AdvancedMarketRegime | product | Y |  |
| `src/linux/Tools/TradingTerminal.AdvancedMarketRegime/AdvancedMarketRegimeView.xaml` | 251 | linux | TradingTerminal.AdvancedMarketRegime | product | N | UI |
| `src/linux/Tools/TradingTerminal.AdvancedMarketRegime/AdvancedMarketRegimeViewModel.cs` | 360 | linux | TradingTerminal.AdvancedMarketRegime | product | Y | Rebuild the bindable header + row grid from the cached snapshot, applying |
| `src/linux/Tools/TradingTerminal.AdvancedMarketRegime/AvaloniaUi/AdvancedMarketRegimeAvaloniaWindow.axaml.cs` | 11 | linux | TradingTerminal.AdvancedMarketRegime | product | Y | Avalonia (cross-platform) view for the Advanced Market Regime dashboard — net9.0-leg |
| `src/linux/Tools/TradingTerminal.AdvancedMarketRegime/AvaloniaUi/AdvancedMarketRegimeAvaloniaWindow.axaml` | 28 | linux | TradingTerminal.AdvancedMarketRegime | product | N | UI |
| `src/linux/Tools/TradingTerminal.Backtest/AvaloniaUi/BacktestAvaloniaWindow.axaml.cs` | 58 | linux | TradingTerminal.Backtest | product | Y | Avalonia (cross-platform) view for the Backtest tool — net9.0-leg counterpart to the |
| `src/linux/Tools/TradingTerminal.Backtest/AvaloniaUi/BacktestAvaloniaWindow.axaml` | 40 | linux | TradingTerminal.Backtest | product | N | UI |
| `src/linux/Tools/TradingTerminal.Backtest/AvaloniaUi/QuickBacktestAvaloniaWindow.axaml.cs` | 50 | linux | TradingTerminal.Backtest | product | Y |  |
| `src/linux/Tools/TradingTerminal.Backtest/AvaloniaUi/QuickBacktestAvaloniaWindow.axaml` | 126 | linux | TradingTerminal.Backtest | product | N | UI |
| `src/linux/Tools/TradingTerminal.Backtest/BacktestServiceCollectionExtensions.cs` | 27 | linux | TradingTerminal.Backtest | product | Y | DI registration for the Backtest tab. |
| `src/linux/Tools/TradingTerminal.Backtest/BacktestView.xaml.cs` | 41 | linux | TradingTerminal.Backtest | product | Y |  |
| `src/linux/Tools/TradingTerminal.Backtest/BacktestView.xaml` | 188 | linux | TradingTerminal.Backtest | product | N | UI |
| `src/linux/Tools/TradingTerminal.Backtest/BacktestViewModel.cs` | 181 | linux | TradingTerminal.Backtest | product | Y | Raised after a run completes so the view can redraw the ScottPlot |
| `src/linux/Tools/TradingTerminal.Backtest/QuickBacktestView.xaml.cs` | 37 | linux | TradingTerminal.Backtest | product | Y |  |
| `src/linux/Tools/TradingTerminal.Backtest/QuickBacktestView.xaml` | 213 | linux | TradingTerminal.Backtest | product | N | UI |
| `src/linux/Tools/TradingTerminal.Backtest/QuickBacktestViewModel.cs` | 680 | linux | TradingTerminal.Backtest | product | Y | How the Quick-backtest sources its replay data. |
| `src/linux/Tools/TradingTerminal.BacktestStudio/AvaloniaUi/BacktestStudioAvaloniaWindow.axaml.cs` | 11 | linux | TradingTerminal.BacktestStudio | product | Y | Avalonia (cross-platform) view for Backtest Studio — net9.0-leg counterpart to the WPF |
| `src/linux/Tools/TradingTerminal.BacktestStudio/AvaloniaUi/BacktestStudioAvaloniaWindow.axaml` | 83 | linux | TradingTerminal.BacktestStudio | product | N | UI |
| `src/linux/Tools/TradingTerminal.BacktestStudio/AxisRowViewModel.cs` | 28 | linux | TradingTerminal.BacktestStudio | product | Y | One row in the optimization axis editor: a parameter the user can |
| `src/linux/Tools/TradingTerminal.BacktestStudio/BacktestStudioServiceCollectionExtensions.cs` | 42 | linux | TradingTerminal.BacktestStudio | product | Y | DI registration for the Backtest Studio. Seeds the kernel registry from the |
| `src/linux/Tools/TradingTerminal.BacktestStudio/BacktestStudioView.xaml.cs` | 135 | linux | TradingTerminal.BacktestStudio | product | Y | Code-behind for the Studio. Pure view concern: it listens for the VM's |
| `src/linux/Tools/TradingTerminal.BacktestStudio/BacktestStudioView.xaml` | 355 | linux | TradingTerminal.BacktestStudio | product | N | UI |
| `src/linux/Tools/TradingTerminal.BacktestStudio/BacktestStudioViewModel.cs` | 825 | linux | TradingTerminal.BacktestStudio | product | Y | Exports the round-trip trades of the last single run. |
| `src/linux/Tools/TradingTerminal.BacktestStudio/DataSourceKind.cs` | 14 | linux | TradingTerminal.BacktestStudio | product | Y | Where the Studio pulls market data from for a run. |
| `src/linux/Tools/TradingTerminal.BacktestStudio/LegacyKernelDescriptors.cs` | 32 | linux | TradingTerminal.BacktestStudio | product | Y | Bridges the 12 legacy engine strategies (the catalog) into |
| `src/linux/Tools/TradingTerminal.BacktestStudio/ParamRowViewModel.cs` | 24 | linux | TradingTerminal.BacktestStudio | product | Y | One editable row in the parameter panel, generated from a kernel's |
| `src/linux/Tools/TradingTerminal.BacktestStudio/ParquetMarketDataFeed.cs` | 36 | linux | TradingTerminal.BacktestStudio | product | Y | A feed that replays a recorded parquet tick file through the new |
| `src/linux/Tools/TradingTerminal.BacktestStudio/TrialRowViewModel.cs` | 21 | linux | TradingTerminal.BacktestStudio | product | Y | A flattened optimization trial for the results grid — the parameter dictionary |
| `src/linux/Tools/TradingTerminal.BacktestStudio/WalkForwardRowViewModel.cs` | 25 | linux | TradingTerminal.BacktestStudio | product | Y | A walk-forward fold flattened for the results grid: the in-sample-chosen parameters and |
| `src/linux/Tools/TradingTerminal.Correlation/AvaloniaUi/LiveCorrelationAvaloniaWindow.axaml.cs` | 57 | linux | TradingTerminal.Correlation | product | Y | Avalonia (cross-platform) view for the Live Correlation Matrix — net9.0-leg counterpart to |
| `src/linux/Tools/TradingTerminal.Correlation/AvaloniaUi/LiveCorrelationAvaloniaWindow.axaml` | 48 | linux | TradingTerminal.Correlation | product | N | UI |
| `src/linux/Tools/TradingTerminal.Correlation/CorrelationMatrixControl.cs` | 189 | linux | TradingTerminal.Correlation | product | Y | Single source of the diverging red/grey/green heat colours (cached, frozen). |
| `src/linux/Tools/TradingTerminal.Correlation/CorrelationMatrixViewModel.cs` | 243 | linux | TradingTerminal.Correlation | product | Y | Per-instrument fetch outcome. |
| `src/linux/Tools/TradingTerminal.Correlation/CorrelationMatrixWindow.xaml.cs` | 28 | linux | TradingTerminal.Correlation | product | Y | Standalone window hosting the Correlation Matrix tool. Pure view — all behaviour |
| `src/linux/Tools/TradingTerminal.Correlation/CorrelationMatrixWindow.xaml` | 257 | linux | TradingTerminal.Correlation | product | N | UI |
| `src/linux/Tools/TradingTerminal.Correlation/CorrelationPickerViewModelBase.cs` | 340 | linux | TradingTerminal.Correlation | product | Y | Hard cap on how many rows the checklist shows at once. A |
| `src/linux/Tools/TradingTerminal.Correlation/CorrelationServiceCollectionExtensions.cs` | 19 | linux | TradingTerminal.Correlation | product | Y | DI registration for the Correlation Matrix tools (historical + live). Transient so |
| `src/linux/Tools/TradingTerminal.Correlation/LiveCorrelationMatrixViewModel.cs` | 246 | linux | TradingTerminal.Correlation | product | Y | Changing the cadence live just re-paces the running sampler; the rolling window |
| `src/linux/Tools/TradingTerminal.Correlation/LiveCorrelationMatrixWindow.xaml.cs` | 28 | linux | TradingTerminal.Correlation | product | Y | Standalone window hosting the Live Correlation Matrix tool. Pure view — all |
| `src/linux/Tools/TradingTerminal.Correlation/LiveCorrelationMatrixWindow.xaml` | 250 | linux | TradingTerminal.Correlation | product | N | UI |
| `src/linux/Tools/TradingTerminal.LseBacktest/AvaloniaUi/LseBacktestAvaloniaWindow.axaml.cs` | 11 | linux | TradingTerminal.LseBacktest | product | Y | Avalonia (cross-platform) view for the LSE backtester — net9.0-leg counterpart to the |
| `src/linux/Tools/TradingTerminal.LseBacktest/AvaloniaUi/LseBacktestAvaloniaWindow.axaml` | 36 | linux | TradingTerminal.LseBacktest | product | N | UI |
| `src/linux/Tools/TradingTerminal.LseBacktest/LseBacktestServiceCollectionExtensions.cs` | 22 | linux | TradingTerminal.LseBacktest | product | Y | DI registration for the LSE Tools -> LSE backtester window. Shares the |
| `src/linux/Tools/TradingTerminal.LseBacktest/LseBacktestView.xaml.cs` | 41 | linux | TradingTerminal.LseBacktest | product | Y |  |
| `src/linux/Tools/TradingTerminal.LseBacktest/LseBacktestView.xaml` | 176 | linux | TradingTerminal.LseBacktest | product | N | UI |
| `src/linux/Tools/TradingTerminal.LseBacktest/LseBacktestViewModel.cs` | 213 | linux | TradingTerminal.LseBacktest | product | Y | Raised after a run completes so the view can redraw the ScottPlot |
| `src/linux/Tools/TradingTerminal.QuantConnect/AvaloniaUi/QuantConnectAvaloniaWindow.axaml.cs` | 56 | linux | TradingTerminal.QuantConnect | product | Y | Avalonia (cross-platform) view for the QuantConnect / LEAN tool — net9.0-leg counterpart |
| `src/linux/Tools/TradingTerminal.QuantConnect/AvaloniaUi/QuantConnectAvaloniaWindow.axaml` | 89 | linux | TradingTerminal.QuantConnect | product | N | UI |
| `src/linux/Tools/TradingTerminal.QuantConnect/LeanProcessRunner.cs` | 89 | linux | TradingTerminal.QuantConnect | product | Y | Outcome of a subprocess run: exit code (null = could not start |
| `src/linux/Tools/TradingTerminal.QuantConnect/LeanRuntimeSettings.cs` | 18 | linux | TradingTerminal.QuantConnect | product | Y | Mutable, process-wide LEAN settings shared by the client and the Settings panel. |
| `src/linux/Tools/TradingTerminal.QuantConnect/LocalCliLeanClient.cs` | 225 | linux | TradingTerminal.QuantConnect | product | Y | Locates the newest |
| `src/linux/Tools/TradingTerminal.QuantConnect/NullLeanClient.cs` | 36 | linux | TradingTerminal.QuantConnect | product | Y | No-op client used when an engine mode isn't wired yet (currently ). |
| `src/linux/Tools/TradingTerminal.QuantConnect/QuantConnectServiceCollectionExtensions.cs` | 50 | linux | TradingTerminal.QuantConnect | product | Y | DI registration for the QuantConnect / LEAN tool. Binds , seeds the |
| `src/linux/Tools/TradingTerminal.QuantConnect/QuantConnectViewModel.cs` | 255 | linux | TradingTerminal.QuantConnect | product | Y | 0=Backtest, 1=Projects, 2=Data, 3=Settings — driven by the menu deep-links. |
| `src/linux/Tools/TradingTerminal.QuantConnect/QuantConnectWindow.xaml.cs` | 17 | linux | TradingTerminal.QuantConnect | product | Y | View for the QuantConnect / LEAN tool window. Pure view concerns only |
| `src/linux/Tools/TradingTerminal.QuantConnect/QuantConnectWindow.xaml` | 299 | linux | TradingTerminal.QuantConnect | product | N | UI |
| `src/linux/Tools/TradingTerminal.Recording/AvaloniaUi/TickRecorderAvaloniaWindow.axaml.cs` | 10 | linux | TradingTerminal.Recording | product | Y | Avalonia (cross-platform) view for the live tick recorder — net9.0-leg counterpart to |
| `src/linux/Tools/TradingTerminal.Recording/AvaloniaUi/TickRecorderAvaloniaWindow.axaml` | 10 | linux | TradingTerminal.Recording | product | N | UI |
| `src/linux/Tools/TradingTerminal.Recording/RecorderEntry.cs` | 129 | linux | TradingTerminal.Recording | product | Y | Live subscriptions: the ingest pumps (which do the persisting) plus the hub |
| `src/linux/Tools/TradingTerminal.Recording/RecorderPanelView.axaml.cs` | 18 | linux | TradingTerminal.Recording | product | Y |  |
| `src/linux/Tools/TradingTerminal.Recording/RecorderPanelView.axaml` | 304 | linux | TradingTerminal.Recording | product | N | UI |
| `src/linux/Tools/TradingTerminal.Recording/RecorderPanelViewModel.cs` | 146 | linux | TradingTerminal.Recording | product | Y | The recording service the whole panel binds to. |
| `src/linux/Tools/TradingTerminal.Recording/RecorderWatchlistStore.cs` | 105 | linux | TradingTerminal.Recording | product | Y | The whole persisted recorder state — what to record and the upload |
| `src/linux/Tools/TradingTerminal.Recording/RecordingServiceCollectionExtensions.cs` | 20 | linux | TradingTerminal.Recording | product | Y | DI registration for the live market-data recorder. |
| `src/linux/Tools/TradingTerminal.Recording/TickRecorderViewModel.cs` | 22 | linux | TradingTerminal.Recording | product | Y | Compatibility name used by the first Avalonia shell. It now exposes the |
| `src/linux/Tools/TradingTerminal.Recording/TickRecordingService.cs` | 423 | linux | TradingTerminal.Recording | product | Y | How often auto-upload asks the archiver to ship whatever is pending. The |
