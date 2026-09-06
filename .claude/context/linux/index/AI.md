# macOS index / AI

Generated from source fingerprint `1ddf0170457d`. macOS/Avalonia source only.

| File | LOC | Tree | Project | Role | Public surface | Purpose |
|---|---:|---|---|---|---|---|
| `src/linux/AI/DaxAlgo.Daxq.Contracts/DaxqFormat.cs` | 50 | linux | DaxAlgo.Daxq.Contracts | product | Y | Frozen file, format, cryptographic, and VM ABI constants for DAXQ package version |
| `src/linux/AI/DaxAlgo.Daxq.Contracts/DaxqManifest.cs` | 138 | linux | DaxAlgo.Daxq.Contracts | product | Y | The frozen cleartext |
| `src/linux/AI/DaxAlgo.Daxq.Contracts/ExecutionClass.cs` | 20 | linux | DaxAlgo.Daxq.Contracts | product | Y | Buyer-visible marketplace execution classes and their frozen wire values. |
| `src/linux/AI/DaxAlgo.Daxq.Contracts/ExecutionClassJsonConverter.cs` | 39 | linux | DaxAlgo.Daxq.Contracts | product | Y | Enforces the exact lowercase, string-only ExecutionClass wire contract. |
| `src/linux/AI/DaxAlgo.Daxq.Contracts/HostFn.cs` | 32 | linux | DaxAlgo.Daxq.Contracts | product | Y | Canonical host callback IDs for VM ABI 3. Numeric assignments are immutable. |
| `src/linux/AI/DaxAlgo.Daxq.Contracts/Opcode.cs` | 107 | linux | DaxAlgo.Daxq.Contracts | product | Y | Canonical opcode IDs for VM ABI 3. Numeric assignments are immutable. |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqCryptography.cs` | 314 | linux | DaxAlgo.Daxq.Host | product | Y |  |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqDevelopmentLicensing.cs` | 471 | linux | DaxAlgo.Daxq.Host | product | Y | Development-only entitlement adapter. It deliberately implements the same nonce, device-proof, |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqDeviceIdentity.cs` | 347 | linux | DaxAlgo.Daxq.Host | product | Y |  |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqLicensingContracts.cs` | 284 | linux | DaxAlgo.Daxq.Host | product | Y |  |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqLicensingRuntime.cs` | 1025 | linux | DaxAlgo.Daxq.Host | product | Y |  |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqLiveSignalStrategyViewModel.cs` | 49 | linux | DaxAlgo.Daxq.Host | product | Y |  |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqNativeVmIntegrity.cs` | 319 | linux | DaxAlgo.Daxq.Host | product | Y |  |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqPackageReader.cs` | 411 | linux | DaxAlgo.Daxq.Host | product | Y | Strict official-installer reader for the frozen DAXQ v1 development package. |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqPlatformDataProtection.cs` | 204 | linux | DaxAlgo.Daxq.Host | product | Y | Preserves the Windows DPAPI wire format on Windows and uses an AES-256 |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqProtectedStrategyEngine.cs` | 247 | linux | DaxAlgo.Daxq.Host | product | Y | Runtime and licensing controls for the official protected-strategy host. |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqRemoteSignalStrategy.cs` | 341 | linux | DaxAlgo.Daxq.Host | product | Y | Buyer-visible metadata for one licensed Tier-3 release. Discovery is deliberately external to |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqSignalSessionClient.cs` | 658 | linux | DaxAlgo.Daxq.Host | product | Y | Bounds and transport policy for the Tier-3 signal-session client. |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqStrategyDefinition.cs` | 118 | linux | DaxAlgo.Daxq.Host | product | Y |  |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqStrategyInstaller.cs` | 201 | linux | DaxAlgo.Daxq.Host | product | Y | Result of importing a protected strategy into the running Pro terminal. |
| `src/linux/AI/DaxAlgo.Daxq.Host/DaxqStrategyKernel.cs` | 359 | linux | DaxAlgo.Daxq.Host | product | Y | Legacy backtest seam adapter for one verified DAXQ program. |
| `src/linux/AI/DaxAlgo.Daxq.Vm/DaxqNativeVm.cs` | 764 | linux | DaxAlgo.Daxq.Vm | product | Y | Cached-delegate P/Invoke bridge for the native |
| `src/linux/AI/DaxAlgo.Daxq.Vm/DaxqProgram.cs` | 1366 | linux | DaxAlgo.Daxq.Vm | product | Y | A fully parsed and statically verified DQXP v1 program. |
| `src/linux/AI/DaxAlgo.Daxq.Vm/DaxqReferenceVm.cs` | 890 | linux | DaxAlgo.Daxq.Vm | product | Y | Signals from the most recent successful callback; invalidated by the next invocation. |
| `src/linux/AI/DaxAlgo.Daxq.Vm/DaxqSdkAbi3FrameHost.cs` | 408 | linux | DaxAlgo.Daxq.Vm | product | Y | One finite, normalized canonical OHLCV bar supplied to the DAXQ SDK ABI |
| `src/linux/AI/DaxAlgo.Daxq.Vm/DaxqTypes.cs` | 255 | linux | DaxAlgo.Daxq.Vm | product | Y | Stable native and managed fault codes for DAXQ VM ABI 3. |
| `src/linux/AI/DaxAlgo.FootprintTransformer/FootprintModelContract.cs` | 88 | linux | DaxAlgo.FootprintTransformer | product | Y |  |
| `src/linux/AI/DaxAlgo.FootprintTransformer/FootprintTransformerEncoding.cs` | 292 | linux | DaxAlgo.FootprintTransformer | product | Y |  |
| `src/linux/AI/DaxAlgo.FootprintTransformer/FootprintTransformerForecastProvider.cs` | 217 | linux | DaxAlgo.FootprintTransformer | product | Y |  |
| `src/linux/AI/DaxAlgo.FootprintTransformer/OnnxFootprintInferenceSession.cs` | 301 | linux | DaxAlgo.FootprintTransformer | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai/Analyst/AiAnalystEnricher.cs` | 111 | linux | TradingTerminal.Ai | product | Y | Notification enricher that appends a one-line AI Analyst verdict to every signal |
| `src/linux/AI/TradingTerminal.Ai/Analyst/AiAnalystServiceCollectionExtensions.cs` | 63 | linux | TradingTerminal.Ai | product | Y | Registers the AI Analyst seam. The single registered |
| `src/linux/AI/TradingTerminal.Ai/Analyst/HttpAiAnalystClient.cs` | 154 | linux | TradingTerminal.Ai | product | Y | HTTP client for the Python daxalgo-ml sidecar's /analyst/run endpoint. |
| `src/linux/AI/TradingTerminal.Ai/Analyst/NullAiAnalystClient.cs` | 17 | linux | TradingTerminal.Ai | product | Y | Stand-in registered when AiAnalystOptions.Enabled is false (no Python sidecar |
| `src/linux/AI/TradingTerminal.Ai.BacktestAnalysis/AvaloniaUi/BacktestAnalysisAvaloniaWindow.axaml.cs` | 10 | linux | TradingTerminal.Ai.BacktestAnalysis | product | Y | Avalonia (cross-platform) view for the Backtest Analysis tool — net9.0-leg counterpart to |
| `src/linux/AI/TradingTerminal.Ai.BacktestAnalysis/AvaloniaUi/BacktestAnalysisAvaloniaWindow.axaml` | 56 | linux | TradingTerminal.Ai.BacktestAnalysis | product | N | UI |
| `src/linux/AI/TradingTerminal.Ai.BacktestAnalysis/BacktestAnalysisServiceCollectionExtensions.cs` | 16 | linux | TradingTerminal.Ai.BacktestAnalysis | product | Y | DI registration for the backtest analysis tab (walk-forward + Monte-Carlo). |
| `src/linux/AI/TradingTerminal.Ai.BacktestAnalysis/BacktestAnalysisView.xaml.cs` | 11 | linux | TradingTerminal.Ai.BacktestAnalysis | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.BacktestAnalysis/BacktestAnalysisView.xaml` | 217 | linux | TradingTerminal.Ai.BacktestAnalysis | product | N | UI |
| `src/linux/AI/TradingTerminal.Ai.BacktestAnalysis/BacktestAnalysisViewModel.cs` | 317 | linux | TradingTerminal.Ai.BacktestAnalysis | product | Y | Backtest analysis tab: combines two pre-deployment diagnostics every quant runs before |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Datasets/CoordinatorDatasetTools.cs` | 692 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Datasets/ExpertModelDatasetTools.cs` | 1138 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Models/DeterministicMockLlmProvider.cs` | 79 | linux | TradingTerminal.Ai.Coordinator | product | Y | Network-free provider used by smoke tests and manual workflow rehearsal. It always |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Models/ILlmProvider.cs` | 72 | linux | TradingTerminal.Ai.Coordinator | product | Y | Allows a durable coordinator to align an ordered replay after process restart. |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Models/LlmProviderValidation.cs` | 59 | linux | TradingTerminal.Ai.Coordinator | product | Y | Validates model-provider endpoints without ever echoing a possibly sensitive URL. |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Models/OpenAiCompatibleLlmProvider.cs` | 271 | linux | TradingTerminal.Ai.Coordinator | product | Y | Text completion over the OpenAI-compatible |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Models/ReplayLlmProvider.cs` | 316 | linux | TradingTerminal.Ai.Coordinator | product | Y | One ordered, immutable completion or failure in a provider replay JSONL file. |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/CoordinatorInvocationStillActiveException.cs` | 4 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/CoordinatorPromptCatalog.cs` | 97 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/CoordinatorPromptRenderer.cs` | 65 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/CoordinatorValidation.cs` | 318 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/ResearchCoordinator.cs` | 814 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/RolePromptBuilder.cs` | 34 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Persistence/ContentAddressedArtifactStore.cs` | 99 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Persistence/CoordinatorPersistence.cs` | 48 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Persistence/SqliteCoordinatorStore.cs` | 400 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Retrieval/ExpertContextContracts.cs` | 136 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator/Retrieval/ExpertContextPackTools.cs` | 1445 | linux | TradingTerminal.Ai.Coordinator | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator.Client/Client/VibeQuantApiClient.cs` | 469 | linux | TradingTerminal.Ai.Coordinator.Client | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator.Client/Client/VibeQuantApiContracts.cs` | 92 | linux | TradingTerminal.Ai.Coordinator.Client | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator.Contracts/Contracts/CoordinatorContracts.cs` | 220 | linux | TradingTerminal.Ai.Coordinator.Contracts | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator.Contracts/Security/ContentHasher.cs` | 17 | linux | TradingTerminal.Ai.Coordinator.Contracts | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.Coordinator.Contracts/Serialization/CoordinatorJson.cs` | 21 | linux | TradingTerminal.Ai.Coordinator.Contracts | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.FactorResearch/AvaloniaUi/FactorResearchAvaloniaWindow.axaml.cs` | 11 | linux | TradingTerminal.Ai.FactorResearch | product | Y | Avalonia (cross-platform) view for the Factor Research tool — net9.0-leg counterpart to |
| `src/linux/AI/TradingTerminal.Ai.FactorResearch/AvaloniaUi/FactorResearchAvaloniaWindow.axaml` | 34 | linux | TradingTerminal.Ai.FactorResearch | product | N | UI |
| `src/linux/AI/TradingTerminal.Ai.FactorResearch/FactorResearchServiceCollectionExtensions.cs` | 16 | linux | TradingTerminal.Ai.FactorResearch | product | Y | DI registration for the factor research notebook tab. |
| `src/linux/AI/TradingTerminal.Ai.FactorResearch/FactorResearchView.xaml.cs` | 8 | linux | TradingTerminal.Ai.FactorResearch | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.FactorResearch/FactorResearchView.xaml` | 137 | linux | TradingTerminal.Ai.FactorResearch | product | N | UI |
| `src/linux/AI/TradingTerminal.Ai.FactorResearch/FactorResearchViewModel.cs` | 125 | linux | TradingTerminal.Ai.FactorResearch | product | Y | Factor research tab. Loads a parquet tick file (from the live recorder |
| `src/linux/AI/TradingTerminal.Ai.MarketAnalyst/AiAnalystView.xaml.cs` | 11 | linux | TradingTerminal.Ai.MarketAnalyst | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.MarketAnalyst/AiAnalystView.xaml` | 228 | linux | TradingTerminal.Ai.MarketAnalyst | product | N | UI |
| `src/linux/AI/TradingTerminal.Ai.MarketAnalyst/AiAnalystViewModel.cs` | 185 | linux | TradingTerminal.Ai.MarketAnalyst | product | Y | View-model for the AI Market Analyst dock pane. Fetches a window of |
| `src/linux/AI/TradingTerminal.Ai.MarketAnalyst/AvaloniaUi/AiAnalystAvaloniaWindow.axaml.cs` | 11 | linux | TradingTerminal.Ai.MarketAnalyst | product | Y | Avalonia (cross-platform) view for the AI Market Analyst — net9.0-leg counterpart to |
| `src/linux/AI/TradingTerminal.Ai.MarketAnalyst/AvaloniaUi/AiAnalystAvaloniaWindow.axaml` | 53 | linux | TradingTerminal.Ai.MarketAnalyst | product | N | UI |
| `src/linux/AI/TradingTerminal.Ai.MarketAnalyst/MarketAnalystServiceCollectionExtensions.cs` | 17 | linux | TradingTerminal.Ai.MarketAnalyst | product | Y | DI registration for the AI Market Analyst dock pane. The analyst client |
| `src/linux/AI/TradingTerminal.Ai.MlFeatures/AvaloniaUi/MlFeaturesAvaloniaWindow.axaml.cs` | 10 | linux | TradingTerminal.Ai.MlFeatures | product | Y | Avalonia (cross-platform) view for the ML Features tool — net9.0-leg counterpart to |
| `src/linux/AI/TradingTerminal.Ai.MlFeatures/AvaloniaUi/MlFeaturesAvaloniaWindow.axaml` | 39 | linux | TradingTerminal.Ai.MlFeatures | product | N | UI |
| `src/linux/AI/TradingTerminal.Ai.MlFeatures/MlFeaturesServiceCollectionExtensions.cs` | 16 | linux | TradingTerminal.Ai.MlFeatures | product | Y | DI registration for the ML features tab (triple-barrier labelling + feature export). |
| `src/linux/AI/TradingTerminal.Ai.MlFeatures/MlFeaturesView.xaml.cs` | 11 | linux | TradingTerminal.Ai.MlFeatures | product | Y |  |
| `src/linux/AI/TradingTerminal.Ai.MlFeatures/MlFeaturesView.xaml` | 135 | linux | TradingTerminal.Ai.MlFeatures | product | N | UI |
| `src/linux/AI/TradingTerminal.Ai.MlFeatures/MlFeaturesViewModel.cs` | 189 | linux | TradingTerminal.Ai.MlFeatures | product | Y | ML Features tab. Loads a parquet tick file, aggregates into N-tick bars |
| `src/linux/AI/TradingTerminal.Ai.PaperLab/AvaloniaUi/PaperLabAvaloniaWindow.axaml.cs` | 11 | linux | TradingTerminal.Ai.PaperLab | product | Y | Avalonia (cross-platform) view for Paper Lab — net9.0-leg counterpart to the WPF |
| `src/linux/AI/TradingTerminal.Ai.PaperLab/AvaloniaUi/PaperLabAvaloniaWindow.axaml` | 53 | linux | TradingTerminal.Ai.PaperLab | product | N | UI |
| `src/linux/AI/TradingTerminal.Ai.PaperLab/PaperLabServiceCollectionExtensions.cs` | 24 | linux | TradingTerminal.Ai.PaperLab | product | Y | Register the Paper Lab view and view-model as transient services. Mirrors |
| `src/linux/AI/TradingTerminal.Ai.PaperLab/PaperLabView.xaml.cs` | 13 | linux | TradingTerminal.Ai.PaperLab | product | Y | Code-behind for |
| `src/linux/AI/TradingTerminal.Ai.PaperLab/PaperLabView.xaml` | 369 | linux | TradingTerminal.Ai.PaperLab | product | N | UI |
| `src/linux/AI/TradingTerminal.Ai.PaperLab/PaperLabViewModel.cs` | 476 | linux | TradingTerminal.Ai.PaperLab | product | Y | True when any async operation is in-flight. Drives the progress ring. |
| `src/linux/AI/TradingTerminal.Ai.PaperLab/ReproJobRowViewModel.cs` | 113 | linux | TradingTerminal.Ai.PaperLab | product | Y | Apply a fresh job snapshot, updating only the fields that may have |
