# macOS index / Shell

Generated from source fingerprint `e91d50e75733`. macOS/Avalonia source only.

| File | LOC | Tree | Project | Role | Public surface | Purpose |
|---|---:|---|---|---|---|---|
| `src/linux/Shell/TradingTerminal.Accounts/AccountGateCoordinator.cs` | 228 | linux | TradingTerminal.Accounts | product | Y |  |
| `src/linux/Shell/TradingTerminal.Accounts/AccountGateDiagnostics.cs` | 51 | linux | TradingTerminal.Accounts | product | Y |  |
| `src/linux/Shell/TradingTerminal.Accounts/AccountGateEditionProfile.cs` | 25 | linux | TradingTerminal.Accounts | product | Y |  |
| `src/linux/Shell/TradingTerminal.Accounts/AccountGateRunner.cs` | 73 | linux | TradingTerminal.Accounts | product | Y | Creates the macOS account gate while retaining the Windows gate's service policy. |
| `src/linux/Shell/TradingTerminal.Accounts/AccountGateServices.cs` | 266 | linux | TradingTerminal.Accounts | product | Y |  |
| `src/linux/Shell/TradingTerminal.Accounts/AccountGateViewModel.cs` | 255 | linux | TradingTerminal.Accounts | product | Y |  |
| `src/linux/Shell/TradingTerminal.Accounts/AccountGateWindow.axaml.cs` | 43 | linux | TradingTerminal.Accounts | product | Y |  |
| `src/linux/Shell/TradingTerminal.Accounts/AccountGateWindow.axaml` | 143 | linux | TradingTerminal.Accounts | product | N | UI |
| `src/linux/Shell/TradingTerminal.Accounts/DevelopmentAccountSessionStore.cs` | 377 | linux | TradingTerminal.Accounts | product | Y | Protects the local development session with a key held by the user's |
| `src/linux/Shell/TradingTerminal.Accounts/GoogleOAuthClient.cs` | 610 | linux | TradingTerminal.Accounts | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/App.axaml.cs` | 350 | linux | TradingTerminal.App.Avalonia | product | Y | The composed DI graph for the macOS terminal. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/App.axaml` | 16 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Archive/AvaloniaTelegramAuthPrompt.cs` | 101 | linux | TradingTerminal.App.Avalonia | product | Y | Avalonia bridge for WTelegramClient's synchronous configuration callback. The transport runs on |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Archive/TelegramArchiveLogin.cs` | 83 | linux | TradingTerminal.App.Avalonia | product | Y | App-layer implementation of the seam used by the login |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Archive/TelegramArchiveOptionsPostConfigure.cs` | 26 | linux | TradingTerminal.App.Avalonia | product | Y | Rehydrates the platform-protected Telegram values after configuration binding. Legacy plaintext |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Archive/TelegramPromptDialog.axaml.cs` | 47 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Archive/TelegramPromptDialog.axaml` | 45 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/AvaloniaUiDispatcher.cs` | 19 | linux | TradingTerminal.App.Avalonia | product | Y | backed by Avalonia's UI-thread dispatcher. Registered in the Avalonia |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Charts/LineChartControl.cs` | 89 | linux | TradingTerminal.App.Avalonia | product | Y | Optional overlay series (cyan), e.g. a filtered/forecast trace over the raw series. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Composition/ObservableCollectionLogSink.cs` | 21 | linux | TradingTerminal.App.Avalonia | product | Y | Forwards Serilog events into the app-wide Activity Log. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Composition/ServiceConfiguration.cs` | 319 | linux | TradingTerminal.App.Avalonia | product | Y | Composition root for the Avalonia shell. Mirrors the Windows Professional Generic Host |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Diagnostics/CrashGuard.cs` | 160 | linux | TradingTerminal.App.Avalonia | product | Y | Last-line crash reporting shared by the macOS shell's UI and background work. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Diagnostics/PluginFaultWatchdog.cs` | 101 | linux | TradingTerminal.App.Avalonia | product | Y | Attributes repeated unhandled faults to their collectible plugin load context. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Diagnostics/StrategyWindowSmoke.cs` | 118 | linux | TradingTerminal.App.Avalonia | product | Y | Dev/CI sweep that constructs, renders, and closes every plugin strategy view. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/AuthenticatedPaperExecutionBookTargetIntake.cs` | 238 | linux | TradingTerminal.App.Avalonia | product | Y | Constructs a canonical strategy-target request without dispatching it. The desktop session owns |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperExecutionBooks.cs` | 756 | linux | TradingTerminal.App.Avalonia | product | Y | Atomic, versioned per-user persistence for Paper execution-book configuration. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperExecutionBooksWindow.axaml.cs` | 8 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperExecutionBooksWindow.axaml` | 116 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperExecutionConsoleWindow.axaml.cs` | 22 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperExecutionConsoleWindow.axaml` | 434 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperExecutionDesktopSession.cs` | 768 | linux | TradingTerminal.App.Avalonia | product | Y | Creates the same Paper-only desktop composition at an explicitly isolated ledger. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperExecutionUnavailableWindow.axaml.cs` | 22 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperExecutionUnavailableWindow.axaml` | 34 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperStrategyRunnerViewModel.cs` | 699 | linux | TradingTerminal.App.Avalonia | product | Y | Native desktop owner for one canonical SDK kernel, its bounded per-instrument model |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperStrategyRunnerWindow.axaml.cs` | 50 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/PaperStrategyRunnerWindow.axaml` | 220 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/MachineLearning/ArimaGarchViewModel.cs` | 63 | linux | TradingTerminal.App.Avalonia | product | Y | Avalonia "ARIMA &amp; GARCH" window VM. Fits the broker-neutral Core |
| `src/linux/Shell/TradingTerminal.App.Avalonia/MachineLearning/ArimaGarchWindow.axaml.cs` | 8 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/MachineLearning/ArimaGarchWindow.axaml` | 48 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/MachineLearning/KalmanViewModel.cs` | 60 | linux | TradingTerminal.App.Avalonia | product | Y | Avalonia "Kalman Filter" window VM. Runs the broker-neutral Core |
| `src/linux/Shell/TradingTerminal.App.Avalonia/MachineLearning/KalmanWindow.axaml.cs` | 8 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/MachineLearning/KalmanWindow.axaml` | 41 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/MachineLearning/StationarityViewModel.cs` | 61 | linux | TradingTerminal.App.Avalonia | product | Y | Avalonia "Stationarity &amp; Differencing" window VM. Runs the broker-neutral Core time-series |
| `src/linux/Shell/TradingTerminal.App.Avalonia/MachineLearning/StationarityWindow.axaml.cs` | 8 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/MachineLearning/StationarityWindow.axaml` | 48 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Plugins/PluginConsentDialog.axaml.cs` | 147 | linux | TradingTerminal.App.Avalonia | product | Y | Informed consent for an unsigned, unpinned plugin. The safe/default answer is rejection |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Plugins/PluginConsentDialog.axaml` | 52 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Plugins/PluginManagerView.axaml.cs` | 11 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Plugins/PluginManagerView.axaml` | 202 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Plugins/PluginManagerViewModel.cs` | 442 | linux | TradingTerminal.App.Avalonia | product | Y | One row in the plugins list — a loaded plugin OR one |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Program.cs` | 16 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/AiProvidersSettingsWindow.axaml.cs` | 8 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/AiProvidersSettingsWindow.axaml` | 54 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/ArchiveActivityWindow.axaml.cs` | 8 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/ArchiveActivityWindow.axaml` | 23 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/ArchiveSettingsWindow.axaml.cs` | 8 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/ArchiveSettingsWindow.axaml` | 43 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/NotificationsSettingsWindow.axaml.cs` | 8 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/NotificationsSettingsWindow.axaml` | 28 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/ResearchSettingsWindow.axaml.cs` | 8 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/ResearchSettingsWindow.axaml` | 24 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/StrategyAuthoringWindow.axaml.cs` | 117 | linux | TradingTerminal.App.Avalonia | product | Y | Lets the Avalonia parameter workbench select the correct editor without UI-specific VM |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/StrategyAuthoringWindow.axaml` | 2346 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/SupportWindow.axaml.cs` | 18 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Settings/SupportWindow.axaml` | 55 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/BrokerApiChipViewModel.cs` | 84 | linux | TradingTerminal.App.Avalonia | product | Y | Drives the chip's background colour bucket. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/BrokerApiMeterViewModel.cs` | 72 | linux | TradingTerminal.App.Avalonia | product | Y | Header-strip API meter — one chip per broker being talked to. Avalonia |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/MainWindow.axaml.cs` | 750 | linux | TradingTerminal.App.Avalonia | product | Y | Shows a tool/strategy window and — matching the WPF shell — disposes |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/MainWindow.axaml` | 628 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/MainWindowViewModel.cs` | 392 | linux | TradingTerminal.App.Avalonia | product | Y | Design-time ctor — empty graph so the previewer has something to render. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/ShellConverters.cs` | 115 | linux | TradingTerminal.App.Avalonia | product | Y | Shell colour converters — Avalonia has no WPF-style DataTriggers, so the status |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/StrategyPillConverters.cs` | 142 | linux | TradingTerminal.App.Avalonia | product | Y | One coloured catalog pill: label + background/foreground brushes. Avalonia mirror of the |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Strategies/StrategyImageTile.axaml.cs` | 90 | linux | TradingTerminal.App.Avalonia | product | Y | Displays a strategy screenshot without retaining a file handle, falling back to |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Strategies/StrategyImageTile.axaml` | 24 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Strategies/StrategyPresentationEditorViewModel.cs` | 87 | linux | TradingTerminal.App.Avalonia | product | Y | Edits a strategy card's presentation overrides. The reusable Windows behavior is retained; |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Strategies/StrategyPresentationEditorWindow.axaml.cs` | 13 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Strategies/StrategyPresentationEditorWindow.axaml` | 71 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Support/ISupportPrompt.cs` | 19 | linux | TradingTerminal.App.Avalonia | product | Y | Unconditionally shows or re-activates the support window. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Support/SupportPrompt.cs` | 99 | linux | TradingTerminal.App.Avalonia | product | Y | Shows the support window at most once automatically per launch, after a |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Themes/Controls.axaml` | 117 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Themes/Palette.Light.axaml` | 93 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Themes/Palette.axaml` | 94 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Theming/AvaloniaThemeFilePicker.cs` | 95 | linux | TradingTerminal.App.Avalonia | product | Y | A selected theme file and its open stream. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Theming/ThemeManager.cs` | 502 | linux | TradingTerminal.App.Avalonia | product | Y | A selectable application theme and its compiled Avalonia palette resource. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Theming/ThemeStudioView.axaml.cs` | 19 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Theming/ThemeStudioView.axaml` | 224 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Theming/ThemeStudioViewModel.cs` | 228 | linux | TradingTerminal.App.Avalonia | product | Y | A named, collapsible group of token editors. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Theming/ThemeToken.cs` | 35 | linux | TradingTerminal.App.Avalonia | product | Y | Whether a palette token is a flat colour or a multi-stop gradient. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Theming/ThemeTokenViewModel.cs` | 237 | linux | TradingTerminal.App.Avalonia | product | Y | One editable colour stop inside a gradient token. |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Tools/CorrelationHeatmapControl.cs` | 110 | linux | TradingTerminal.App.Avalonia | product | Y | Custom-drawn correlation-matrix heatmap for Avalonia — an N×N grid coloured by Pearson |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Tools/CorrelationViewModel.cs` | 56 | linux | TradingTerminal.App.Avalonia | product | Y | Avalonia Correlation-matrix window VM. Computes a Pearson correlation matrix via the broker-neutral |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Tools/CorrelationWindow.axaml.cs` | 8 | linux | TradingTerminal.App.Avalonia | product | Y |  |
| `src/linux/Shell/TradingTerminal.App.Avalonia/Tools/CorrelationWindow.axaml` | 30 | linux | TradingTerminal.App.Avalonia | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/AiKeyStore.cs` | 105 | linux | TradingTerminal.Login | product | Y | Provider ids that currently have a stored key. |
| `src/linux/Shell/TradingTerminal.Login/BrokerLoginFormBase.cs` | 291 | linux | TradingTerminal.Login | product | Y | Two/three-letter square-badge text (e.g. "BN", "IB"). |
| `src/linux/Shell/TradingTerminal.Login/BrokerLoginFormFactory.cs` | 54 | linux | TradingTerminal.Login | product | Y | Lazily resolves login forms only for broker clients registered on this platform. |
| `src/linux/Shell/TradingTerminal.Login/CredentialStore.cs` | 64 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/CredentialStoreAiKeyResolver.cs` | 23 | linux | TradingTerminal.Login | product | Y | Resolves AI-provider keys for the codegen factory from the Keychain-backed , falling |
| `src/linux/Shell/TradingTerminal.Login/Forms/AlpacaLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/AlpacaLoginForm.axaml` | 24 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/AlpacaLoginFormViewModel.cs` | 101 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/BinanceLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/BinanceLoginForm.axaml` | 23 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/BinanceLoginFormViewModel.cs` | 44 | linux | TradingTerminal.Login | product | Y | Login form for Binance public market data. There are no credentials to |
| `src/linux/Shell/TradingTerminal.Login/Forms/BybitLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/BybitLoginForm.axaml` | 23 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/BybitLoginFormViewModel.cs` | 24 | linux | TradingTerminal.Login | product | Y | Login form for Bybit public market data — no credentials (keyless, like |
| `src/linux/Shell/TradingTerminal.Login/Forms/CTraderLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/CTraderLoginForm.axaml` | 47 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/CTraderLoginFormViewModel.cs` | 246 | linux | TradingTerminal.Login | product | Y | True while |
| `src/linux/Shell/TradingTerminal.Login/Forms/CoinbaseLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/CoinbaseLoginForm.axaml` | 23 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/CoinbaseLoginFormViewModel.cs` | 24 | linux | TradingTerminal.Login | product | Y | Login form for Coinbase public market data — no credentials (keyless, like |
| `src/linux/Shell/TradingTerminal.Login/Forms/IbLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/IbLoginForm.axaml` | 45 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/IbLoginFormViewModel.cs` | 145 | linux | TradingTerminal.Login | product | Y | TWS / IB Gateway must be up — surfaced inside this row |
| `src/linux/Shell/TradingTerminal.Login/Forms/IronBeamLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/IronBeamLoginForm.axaml` | 15 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/IronBeamLoginFormViewModel.cs` | 93 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/KrakenLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/KrakenLoginForm.axaml` | 23 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/KrakenLoginFormViewModel.cs` | 24 | linux | TradingTerminal.Login | product | Y | Login form for Kraken public market data — no credentials (keyless, like |
| `src/linux/Shell/TradingTerminal.Login/Forms/LondonStrategicEdgeLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/LondonStrategicEdgeLoginForm.axaml` | 12 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/LondonStrategicEdgeLoginFormViewModel.cs` | 74 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/NinjaLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/NinjaLoginForm.axaml` | 22 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/NinjaLoginFormViewModel.cs` | 97 | linux | TradingTerminal.Login | product | Y | NinjaTrader 8 must be up — surfaced inside this row (see the |
| `src/linux/Shell/TradingTerminal.Login/Forms/OkxLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/OkxLoginForm.axaml` | 23 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/OkxLoginFormViewModel.cs` | 24 | linux | TradingTerminal.Login | product | Y | Login form for OKX public market data — no credentials (keyless, like |
| `src/linux/Shell/TradingTerminal.Login/Forms/UpstoxLoginForm.axaml.cs` | 8 | linux | TradingTerminal.Login | product | Y |  |
| `src/linux/Shell/TradingTerminal.Login/Forms/UpstoxLoginForm.axaml` | 29 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/Forms/UpstoxLoginFormViewModel.cs` | 200 | linux | TradingTerminal.Login | product | Y | Status text shown beneath the auth buttons (success or a user-facing error). |
| `src/linux/Shell/TradingTerminal.Login/LoginClipboard.cs` | 24 | linux | TradingTerminal.Login | product | Y | Small clipboard seam so the login view-model stays independent of Avalonia. |
| `src/linux/Shell/TradingTerminal.Login/LoginServiceCollectionExtensions.cs` | 78 | linux | TradingTerminal.Login | product | Y | The login window/flow plus the KEYLESS broker forms (public crypto feeds — |
| `src/linux/Shell/TradingTerminal.Login/LoginViewModel.cs` | 502 | linux | TradingTerminal.Login | product | Y | The forms as their concrete base type, pre-sorted Keyless → Credentialed → |
| `src/linux/Shell/TradingTerminal.Login/LoginWindow.axaml.cs` | 111 | linux | TradingTerminal.Login | product | Y | Avalonia host for the current two-pane, multi-broker login workspace. |
| `src/linux/Shell/TradingTerminal.Login/LoginWindow.axaml` | 394 | linux | TradingTerminal.Login | product | N | UI |
| `src/linux/Shell/TradingTerminal.Login/PlatformSecretStore.cs` | 346 | linux | TradingTerminal.Login | product | Y | Stores secrets in the current macOS user's login Keychain. The serialized value |
| `src/linux/Shell/TradingTerminal.Login/ServiceDependencyViewModel.cs` | 306 | linux | TradingTerminal.Login | product | Y | The live state of an external dependency the terminal talks to but |
| `src/linux/Shell/TradingTerminal.Login/StoredCredentials.cs` | 150 | linux | TradingTerminal.Login | product | Y | Which broker the user last signed in with. Drives the form shown |
