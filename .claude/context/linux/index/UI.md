# macOS index / UI

Generated from source fingerprint `1ddf0170457d`. macOS/Avalonia source only.

| File | LOC | Tree | Project | Role | Public surface | Purpose |
|---|---:|---|---|---|---|---|
| `src/linux/UI/TradingTerminal.Settings/Archive/ArchiveActivityViewModel.cs` | 162 | linux | TradingTerminal.Settings | product | Y | Period-by-period coverage — each window labelled Offloaded or Pending. |
| `src/linux/UI/TradingTerminal.Settings/Archive/ArchiveSettingsViewModel.cs` | 243 | linux | TradingTerminal.Settings | product | Y | Settings tab for the market-data archive. Three sections: Telegram credentials + login, |
| `src/linux/UI/TradingTerminal.Settings/Archive/ArchiveUserFile.cs` | 69 | linux | TradingTerminal.Settings | product | Y | Per-user JSON persistence for the archive settings tab. Layered into host configuration |
| `src/linux/UI/TradingTerminal.Settings/Archive/TelegramArchiveCredentialProtection.cs` | 272 | linux | TradingTerminal.Settings | product | Y | Protects Telegram archive credentials with the current user's platform secret store. Windows |
| `src/linux/UI/TradingTerminal.Settings/Authoring/AiCodegenUserFile.cs` | 92 | linux | TradingTerminal.Settings | product | Y | Absolute path to |
| `src/linux/UI/TradingTerminal.Settings/Authoring/AiProvidersSettingsViewModel.cs` | 100 | linux | TradingTerminal.Settings | product | Y | Store (or clear, when blank) the pasted key in the platform credential |
| `src/linux/UI/TradingTerminal.Settings/Authoring/AuthoringChartReferenceStore.cs` | 133 | linux | TradingTerminal.Settings | product | Y | Durable host-owned copy of a chart supplied to Hyperion. Generated specifications carry |
| `src/linux/UI/TradingTerminal.Settings/Authoring/AuthoringSessionStore.cs` | 220 | linux | TradingTerminal.Settings | product | Y | One bubble as the user saw it. Kept separately from the model |
| `src/linux/UI/TradingTerminal.Settings/Authoring/LineDiff.cs` | 94 | linux | TradingTerminal.Settings | product | Y | One line of a rendered diff: |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.AuthoredStrategy.cs` | 163 | linux | TradingTerminal.Settings | product | Y | True when the reviewed intent can be represented by the current SDK |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.NativeStrategyRun.cs` | 547 | linux | TradingTerminal.Settings | product | Y |  |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.Navigation.cs` | 315 | linux | TradingTerminal.Settings | product | Y |  |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.ResearchDataset.cs` | 315 | linux | TradingTerminal.Settings | product | Y | Called only by the trusted host chart overlay. Authored visualizer/strategy code never |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.StrategyIntent.cs` | 1408 | linux | TradingTerminal.Settings | product | Y | Adds a host-owned strategy classification choice without selecting it. |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.TradeIrBacktest.cs` | 524 | linux | TradingTerminal.Settings | product | Y | Exact-hash bridge from an active, package-valid TradeIR candidate to the deliberately narrow |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.TradeIrSynthesis.cs` | 275 | linux | TradingTerminal.Settings | product | Y | Review-bound bridge from the four independently generated authoring drafts to one new |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.Validation.cs` | 146 | linux | TradingTerminal.Settings | product | Y |  |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.Workspace.cs` | 208 | linux | TradingTerminal.Settings | product | Y |  |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyAuthoringViewModel.cs` | 4850 | linux | TradingTerminal.Settings | product | Y | Keeps the activity strip and the chat from growing without bound over |
| `src/linux/UI/TradingTerminal.Settings/Authoring/StrategyStarterCatalog.cs` | 1061 | linux | TradingTerminal.Settings | product | Y | Overlapping navigation lenses derived from the normalized specification. |
| `src/linux/UI/TradingTerminal.Settings/Notifications/NotificationsSettingsViewModel.cs` | 226 | linux | TradingTerminal.Settings | product | Y | Per-provider default text/vision model ids, pre-filled when the user picks a provider |
| `src/linux/UI/TradingTerminal.Settings/Notifications/NotificationsUserFile.cs` | 84 | linux | TradingTerminal.Settings | product | Y | Writes the notifications section, preserving any other keys that may exist. |
| `src/linux/UI/TradingTerminal.Settings/Research/ResearchSettingsViewModel.cs` | 106 | linux | TradingTerminal.Settings | product | Y | When on, the app launches the Python sidecar itself on startup (no |
| `src/linux/UI/TradingTerminal.Settings/Research/ResearchUserFile.cs` | 61 | linux | TradingTerminal.Settings | product | Y | Absolute path to |
| `src/linux/UI/TradingTerminal.Settings/Support/SupportInfo.cs` | 41 | linux | TradingTerminal.Settings | product | Y | The developer's inbox. Feedback is delivered via a |
| `src/linux/UI/TradingTerminal.Settings/Support/SupportViewModel.cs` | 99 | linux | TradingTerminal.Settings | product | Y | The note the user types to the developer. |
| `src/linux/UI/TradingTerminal.StrategyComposer/AuthoredStrategyViewComposer.cs` | 33 | linux | TradingTerminal.StrategyComposer | product | Y | Must run on the UI thread because it constructs Avalonia controls. |
| `src/linux/UI/TradingTerminal.StrategyComposer/ComposedStrategyView.axaml.cs` | 443 | linux | TradingTerminal.StrategyComposer | product | Y | Parameterless constructor retained for the Avalonia runtime loader and designer. |
| `src/linux/UI/TradingTerminal.StrategyComposer/ComposedStrategyView.axaml` | 250 | linux | TradingTerminal.StrategyComposer | product | N | UI |
| `src/linux/UI/TradingTerminal.StrategyComposer/EmbeddedOrderBookPanel.axaml.cs` | 43 | linux | TradingTerminal.StrategyComposer | product | Y | Embedded form of the destination's native Avalonia order-book surface. Its toolbar is |
| `src/linux/UI/TradingTerminal.StrategyComposer/EmbeddedOrderBookPanel.axaml` | 125 | linux | TradingTerminal.StrategyComposer | product | N | UI |
| `src/linux/UI/TradingTerminal.StrategyComposer/EmbeddedVolumeFootprintPanel.axaml.cs` | 43 | linux | TradingTerminal.StrategyComposer | product | Y | Embedded form of the destination's native Avalonia footprint surface. It intentionally exposes |
| `src/linux/UI/TradingTerminal.StrategyComposer/EmbeddedVolumeFootprintPanel.axaml` | 124 | linux | TradingTerminal.StrategyComposer | product | N | UI |
| `src/linux/UI/TradingTerminal.StrategyComposer/IEmbeddedPausable.cs` | 7 | linux | TradingTerminal.StrategyComposer | product | Y | Visual-freeze compatibility seam for destination panels whose VM predates IsPaused. |
| `src/linux/UI/TradingTerminal.UI.Avalonia/Controls/BusyOverlay.axaml.cs` | 112 | linux | TradingTerminal.UI.Avalonia | product | Y | When true the curtain is shown and blocks input; when false it |
| `src/linux/UI/TradingTerminal.UI.Avalonia/Controls/BusyOverlay.axaml` | 80 | linux | TradingTerminal.UI.Avalonia | product | N | UI |
| `src/linux/UI/TradingTerminal.UI.Avalonia/Controls/Render/AuthoredUnitView.axaml.cs` | 101 | linux | TradingTerminal.UI.Avalonia | product | Y | Avalonia host chrome for an authored strategy or visualizer: parameters, picture, optional |
| `src/linux/UI/TradingTerminal.UI.Avalonia/Controls/Render/AuthoredUnitView.axaml` | 73 | linux | TradingTerminal.UI.Avalonia | product | N | UI |
| `src/linux/UI/TradingTerminal.UI.Avalonia/Controls/Render/AuthoredVisualizerSession.cs` | 276 | linux | TradingTerminal.UI.Avalonia | product | Y | The exact canonical instruments the runtime may observe for this window. |
| `src/linux/UI/TradingTerminal.UI.Avalonia/Controls/Render/DrawingContextSurface.cs` | 464 | linux | TradingTerminal.UI.Avalonia | product | Y | Immediate-mode Avalonia implementation of with data-to-pixel |
| `src/linux/UI/TradingTerminal.UI.Avalonia/Controls/Render/RenderSurfaceView.cs` | 125 | linux | TradingTerminal.UI.Avalonia | product | Y | Resolves semantic theme roles without exposing Avalonia to authored code. |
| `src/linux/UI/TradingTerminal.UI.Avalonia/GenericStrategyWindow.axaml.cs` | 15 | linux | TradingTerminal.UI.Avalonia | product | Y | Generic Avalonia window for any LiveSignalStrategyViewModelBase: binds the common surface |
| `src/linux/UI/TradingTerminal.UI.Avalonia/GenericStrategyWindow.axaml` | 59 | linux | TradingTerminal.UI.Avalonia | product | N | UI |
| `src/linux/UI/TradingTerminal.UI.Core/BarIndicators.cs` | 157 | linux | TradingTerminal.UI.Core | product | Y | Returns (mean, stdev, upper, lower) arrays aligned with bars. |
| `src/linux/UI/TradingTerminal.UI.Core/BrokerInstrumentUniverse.cs` | 83 | linux | TradingTerminal.UI.Core | product | Y | Short broker label appended to instrument rows so users can disambiguate the |
| `src/linux/UI/TradingTerminal.UI.Core/BusyState.cs` | 74 | linux | TradingTerminal.UI.Core | product | Y | True while at least one |
| `src/linux/UI/TradingTerminal.UI.Core/Catalog/StrategyCatalogViewModel.cs` | 52 | linux | TradingTerminal.UI.Core | product | Y | Human-readable detail block for the currently selected strategy. |
| `src/linux/UI/TradingTerminal.UI.Core/Controls/Render/AuthoredUnitHost.cs` | 155 | linux | TradingTerminal.UI.Core | product | Y | Roughly 30fps. Fast enough for a live book, cheap enough to leave |
| `src/linux/UI/TradingTerminal.UI.Core/Controls/Render/AuthoredUnitPresenter.cs` | 103 | linux | TradingTerminal.UI.Core | product | Y | One line in the unit's activity log. |
| `src/linux/UI/TradingTerminal.UI.Core/Diagnostics/PluginFaultTracker.cs` | 31 | linux | TradingTerminal.UI.Core | product | Y | Records one fault for |
| `src/linux/UI/TradingTerminal.UI.Core/Execution/PaperExecutionClient.cs` | 776 | linux | TradingTerminal.UI.Core | product | Y | One immutable risk fact projected from a verified order-event stream. |
| `src/linux/UI/TradingTerminal.UI.Core/Execution/PaperExecutionConsoleViewModel.cs` | 648 | linux | TradingTerminal.UI.Core | product | Y | App boundary that resolves ticket text into exact OMS values and binds |
| `src/linux/UI/TradingTerminal.UI.Core/Execution/PaperPortfolioAnalytics.cs` | 294 | linux | TradingTerminal.UI.Core | product | Y | Account analytics derived from an explicit opening balance plus immutable Paper fills. |
| `src/linux/UI/TradingTerminal.UI.Core/ISignalGeneratorRouterFactory.cs` | 18 | linux | TradingTerminal.UI.Core | product | Y | Default impl — vanilla |
| `src/linux/UI/TradingTerminal.UI.Core/InstrumentPickerFilter.cs` | 194 | linux | TradingTerminal.UI.Core | product | Y | Rows to show for a |
| `src/linux/UI/TradingTerminal.UI.Core/LastInstrumentStore.cs` | 75 | linux | TradingTerminal.UI.Core | product | Y | The canonical symbol last selected under |
| `src/linux/UI/TradingTerminal.UI.Core/LiveSignalStrategyViewModelBase.cs` | 1138 | linux | TradingTerminal.UI.Core | product | Y | Cap on how many instruments the picker shows at once. The broker |
| `src/linux/UI/TradingTerminal.UI.Core/LiveStrategyHostServices.cs` | 41 | linux | TradingTerminal.UI.Core | product | Y | Bundle of canonical-pipeline dependencies that every live strategy host needs. Passed as |
| `src/linux/UI/TradingTerminal.UI.Core/Logging/InMemoryLogSink.cs` | 89 | linux | TradingTerminal.UI.Core | product | Y | Convenience append used by strategy/tab view-models — stamps the entry with the |
| `src/linux/UI/TradingTerminal.UI.Core/Presets/StrategyViewPreset.cs` | 18 | linux | TradingTerminal.UI.Core | product | Y | A named snapshot of a strategy window's view options, persisted per user |
| `src/linux/UI/TradingTerminal.UI.Core/Presets/ToolPresetStore.cs` | 98 | linux | TradingTerminal.UI.Core | product | Y | Test seam: redirect the store directory. |
| `src/linux/UI/TradingTerminal.UI.Core/SignalEntry.cs` | 36 | linux | TradingTerminal.UI.Core | product | Y | One signal row in the live signal log. Produced every time the |
| `src/linux/UI/TradingTerminal.UI.Core/SignalGeneratorRouter.cs` | 136 | linux | TradingTerminal.UI.Core | product | Y | Most recent live tick; used to price synthetic fills. |
| `src/linux/UI/TradingTerminal.UI.Core/SimulatedDataState.cs` | 21 | linux | TradingTerminal.UI.Core | product | Y | App-wide flag indicating whether a synthetic Simulated-broker feed is connected. The shell |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/CatalogItemKind.cs` | 7 | linux | TradingTerminal.UI.Core | product | Y |  |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/IStrategyKernelRegistry.cs` | 114 | linux | TradingTerminal.UI.Core | product | Y | One runnable canonical SDK strategy and its immutable authored specification. |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/IVisualizerRegistry.cs` | 142 | linux | TradingTerminal.UI.Core | product | Y | Looks one up by id, or null when nothing is registered under |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/ParameterEditorItem.cs` | 97 | linux | TradingTerminal.UI.Core | product | Y | Numeric value for both |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/StrategyCatalogItemViewModel.cs` | 137 | linux | TradingTerminal.UI.Core | product | Y | The underlying strategy — the catalog's pill converters, Open and Quick-backtest all |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/StrategyFactory.cs` | 93 | linux | TradingTerminal.UI.Core | product | Y | DI-backed catalog. Each strategy registered in DI must also register a |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/StrategyParametersViewModel.cs` | 48 | linux | TradingTerminal.UI.Core | product | Y | Builds an editor panel from a schema, seeded with defaults. |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/StrategyPresentation.cs` | 21 | linux | TradingTerminal.UI.Core | product | Y | User-authored presentation overrides for a strategy's catalog card — how it is |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/StrategyPresentationStore.cs` | 82 | linux | TradingTerminal.UI.Core | product | Y | The overrides for a strategy, or |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/VisualizerDescriptor.cs` | 10 | linux | TradingTerminal.UI.Core | product | Y |  |
| `src/linux/UI/TradingTerminal.UI.Core/Strategies/VisualizerDescriptors.cs` | 183 | linux | TradingTerminal.UI.Core | product | Y | Reads |
| `src/linux/UI/TradingTerminal.UI.Core/TaskExtensions.cs` | 29 | linux | TradingTerminal.UI.Core | product | Y | Fires the task and logs any exception via |
| `src/linux/UI/TradingTerminal.UI.Core/TradeableInstrument.cs` | 145 | linux | TradingTerminal.UI.Core | product | Y | App sets this once at startup to a registry-backed provider. When null |
| `src/linux/UI/TradingTerminal.UI.Core/UiFile.cs` | 21 | linux | TradingTerminal.UI.Core | product | Y | Show an open-file picker. |
| `src/linux/UI/TradingTerminal.UI.Core/UiThread.cs` | 91 | linux | TradingTerminal.UI.Core | product | Y | Runs |
| `src/linux/UI/TradingTerminal.UI.Core/ViewModelBase.cs` | 8 | linux | TradingTerminal.UI.Core | product | Y | Base class for all view-models. Inherits CommunityToolkit's |
