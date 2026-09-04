# Windows-to-macOS functional inventory

## Scope and audit legend

Windows authority: `dhruuvsharma/DaxAlgo-Terminal` `main` at `6d69136`.
Mac integration baseline: `6cbd184` plus the uncommitted forward-port recorded in
`tasks/2026-08-21-0229-dhruv-public-refactor-forward-port.md`.

This inventory compares behavior, not filenames. The detailed rows use these verdicts:

| Verdict | Meaning |
|---|---|
| **Tested** | Ported behavior is composed or directly exercised by automated tests in this checkout. |
| **Present** | The code and composition exist, but this audit did not authenticate a real vendor/account. |
| **Partial** | Useful implementation exists, but the user cannot complete the Windows workflow end to end. |
| **Missing** | No equivalent composed Mac product path exists. |
| **Mac extra** | Useful functionality exists on Mac but is outside the latest public Windows product. |
| **Platform N/A** | The Windows feature depends on a Windows-only product/vendor boundary. |
| **Unverified** | A vendor claim exists in code, but no real-vendor acceptance evidence was found. |

The granular matrices are authoritative. Percentages are retained only as a secondary roll-up; they
are engineering estimates, not coverage measurements, and do not claim that real broker credentials
or live venues were tested.

## Executive result

The Mac product can select and connect market-data brokers, persist/replay/archive data, run the new
headless strategy sandbox against a deterministic virtual portfolio, and host native visualizers. It
cannot yet send a canonical strategy order to even a simulated OMS, recover an order ledger, reconcile
an account, arm Real mode, or use the Windows update-notification path. Those missing execution and
release boundaries—not charts or broker-login forms—define the remaining critical path.

## The important architecture distinction

```text
broker login + connect/disconnect
    -> market-data session (quotes, trades, depth, bars)
    -> IStrategyKernel receives scoped data
    -> sandbox runtime applies targets to a virtual model portfolio
    -> execution replicator submits admitted changes
    -> risk + OMS + ledger + reconciliation
    -> broker execution adapter
    -> real account
```

The Mac implementation now reaches the first four lines through a Paper-only headless runtime: it has
the portable `IStrategyKernel`, a serialized sandbox host, and a deterministic virtual model
portfolio. It does **not** yet have the downstream execution replication/OMS stack. A green broker
connection therefore proves a market-data session; it does not prove that strategy orders can safely
reach a broker.

## Granular implementation audit

### A. Broker-by-broker capability matrix

`Connect` below means market-data connectivity unless the order columns explicitly say otherwise.
`Bars` distinguishes a direct vendor bar stream from bars aggregated locally from quotes/trades.

| Broker/source | Login/auth UI on Mac | Select/connect/disconnect/reconnect | Instruments | History | Live bars | L1 quotes | L2 depth | Trade tape | Windows order route | Mac order route | Exact verdict / caveat |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Interactive Brokers | Host/port/client/account inputs present | Present through shared `IBrokerSelector` | Present | Present | Present | Present | **Unsupported by client** | Present | IB adapter + TWS transport | **Missing** | Data path present; Mac package requires the official TWS C# assembly. Data login is not execution authorization. |
| NinjaTrader | Deliberately hidden on macOS | **Platform N/A** | Windows only | Synthetic Windows history | Locally aggregated on Windows | Windows polling | Unsupported | Unsupported | None | N/A | `NTDirect.dll` is Windows-only; do not attempt a source-level Mac port. |
| cTrader | Token/account form present | Present | Present | Present | Aggregated from spot events | Present | Reconstructed depth | Unsupported | cTrader adapter | **Missing** | Data path present; reconstructed depth is not a native exchange order book. |
| Alpaca | Key/secret and environment form present | Present | Present | Stocks + crypto | Aggregated from ticks | Stocks + crypto | Unsupported | Not wired | Alpaca adapter | **Missing** | The data client owning trading-SDK objects does not make it the canonical OMS route. |
| Binance | Keyless public-feed form/path | Present | Present | Present | Present | Present | Present | Present | None in latest Windows | N/A | Market-data parity only; product does not promise Binance order execution. |
| Coinbase | Keyless public-feed form/path | Present | Present | Present | Present | Present | Present | Present | None | N/A | Market-data parity only. |
| Bybit | Keyless public-feed form/path | Present | Present | Present | Present | Present | Present | Present | None | N/A | Market-data parity only. |
| Kraken | Keyless public-feed form/path | Present | Present | Present | Present | Present | Present | Present | None | N/A | Market-data parity only. |
| OKX | Keyless public-feed form/path | Present | Present | Present | Present | Present | Present | Present | None | N/A | Market-data parity only. |
| Ironbeam | Credential form present | Present | **Missing/cold catalog** | **Missing** | No direct bar subscription | Present | Present | Present | None | N/A | Charts can aggregate bars downstream, but discovery/history parity is incomplete. |
| London Strategic Edge | API-key form present | Present | Unverified | Present | No direct bar subscription | Present | Unsupported | No verified tape | None | N/A | Data-only source; live bars must be aggregated downstream. |
| Upstox | OAuth form present | Present | Unverified | Present | Aggregated | Present | Five-level depth | No true tape | None | N/A | V3 schema field mapping is explicitly not live-verified in source. |
| Simulated/replay | Local, no vendor credential | Present on Mac | Synthetic/replayed | Replayed | Synthetic/replayed | Synthetic/replayed | Synthetic where supplied | Synthetic where supplied | Provenance-only in latest Windows; Paper venue is in OMS | Older Mac feed only | **Mac extra**, useful for research; not canonical Paper execution parity. |

Broker lifecycle contract, independently for every available broker:

| Contract/action | Windows | Mac | Verdict |
|---|---|---|---|
| Enumerate `AvailableKinds` | Yes | Yes | Present |
| Resolve client with `Get(kind)` | Yes | Yes | Present |
| Read `StateOf(kind)` / current aggregate state | Yes | Yes | Present |
| Observe `StateChanged` | Yes | Yes | Present |
| `ConnectAsync(kind, credentials)` | Yes | Yes | Present; vendor authentication not exercised in this audit |
| `DisconnectAsync(kind)` | Yes | Yes | Present |
| Reconnect selected/previous brokers | Yes | Yes | Present |
| Maintain multiple simultaneous broker sessions | Yes | Yes | Present |
| Distinguish connected-data state from live-order state | Yes through Execution mode UI | No visible Mac execution state | **Partial/UI bug risk** |
| Header status wording | Connection state | Mac says `LIVE {count} brokers` for connected data brokers | **Incorrect semantic label**; must say connected, not LIVE |

### B. Contract-by-contract strategy and visualizer matrix

| Contract/member | What it guarantees | Mac state | Validation / remaining gap |
|---|---|---|---|
| `IPluginRegistrar.RegisterStrategy` | Native registration without reflection-first discovery | Ported | Tested through UI Core catalog tests |
| `IPluginRegistrar.RegisterVisualizer` | Visualizer registration under the same authored-plugin boundary | Ported | Tested through kind-aware catalog tests |
| `AuthoredPlugin` metadata | Stable id/version/name/description/publisher/entry type | Ported | Package/catalog projection present |
| `IStrategyKernel.Schema` | Typed strategy parameter description | Ported | Runtime consumes the kernel contract |
| `IStrategyKernel.DataRequirement` | Declares authorized instruments/data channels | Ported | Runtime rejects callbacks outside declared bounds |
| `IStrategyKernel.OnStart` | One deterministic start boundary | Ported | Covered by sandbox runtime tests |
| `IStrategyKernel.OnQuote` | Scoped L1 callback | Ported | Serialized callback pump tested |
| `IStrategyKernel.OnTrade` | Scoped trade callback | Ported | Serialized callback pump tested |
| `IStrategyKernel.OnDepth` | Scoped depth callback | Ported | Serialized callback pump tested |
| `IStrategyKernel.OnBar` | Scoped bar callback | Ported | Serialized callback pump tested |
| `IStrategyKernel.Draw` | Strategy-owned drawing without WPF types | Ported | Avalonia render adapter exists |
| `IStrategyKernel.OnStop` | Deterministic stop boundary | Ported | Stop/drain behavior tested |
| `IStrategyLifecycle.Run/Pause/Resume/Stop` | Host-controlled lifecycle state machine | Contract ported | Headless runtime implements lifecycle; shell/catalog launch is not composed yet |
| `IVisualizer` schema/data/draw lifecycle | Native visualizer plug shape | Ported | Visualizer runtime + Avalonia hosted session tested |
| `IMarketDataView` | Read-only strategy-scoped market view | Ported | Sandbox context supplies bounded data |
| `IClock` | Host time rather than arbitrary wall-clock access | Ported | Sandbox context supplies clock |
| Parameter view | Host-supplied typed parameter values | Ported | Context and schema present |
| Alert sink | Host-owned user notification boundary | Ported | Context present; full app alert UX needs catalog strategy composition |
| Virtual book `SetTarget` | Declare desired position, not place a broker order | Ported | `RecordingVirtualBook` and portfolio update tested |
| Virtual book pending target/order | Declare bounded pending intent | Ported | Model-portfolio behavior tested; no OMS replication yet |
| `IRenderSurface` candles | Portable candle drawing | Ported | SDK + Avalonia tests pass |
| `IRenderSurface` plots | Portable line/series drawing | Ported | SDK + Avalonia tests pass |
| `IRenderSurface` ladder | Portable price-ladder drawing | Ported | SDK + Avalonia tests pass |
| `IRenderSurface` footprint | Portable footprint drawing | Ported | SDK + Avalonia tests pass |
| `SandboxStrategyRuntime` | Serialized start/data/stop host | Ported headlessly | 30/30 focused tests pass; not launched from catalog yet |
| `ModelPortfolioSimulator` | Deterministic virtual fills/equity/faults/trips | Ported headlessly | 146/146 focused tests pass |
| `.daxalgostrategy` package | Bounded, hashed open strategy archive | Ported | 34/34 package tests pass |
| `.daxalgovisualizer` package | Bounded, hashed open visualizer archive | Ported | Same package verifier |
| `DAX3001` analyzer | Compile-time ban on filesystem/network/process/P/Invoke/host API | **Missing** | Must port before accepting third-party authored code |
| SDK package version | Consumer ABI/version identity | **Partial** | Windows is `0.3.0`; Mac still advertises `0.2.0-alpha` |
| `dotnet new` strategy template | Reproducible author starter project | **Missing** | Port and test Windows template |
| `dotnet new` visualizer template | Reproducible visualizer starter project | **Missing** | Port and test Windows template |
| Public SDK samples | Executable reference behavior | **Missing latest set** | Port after version/analyzer convergence |
| Durable package installer | Verified archive -> durable catalog registration | **Missing** | Current catalog open actions do not install packages |
| Runtime process isolation | Prevent untrusted code from escaping the host | **Missing/weak upstream** | Analyzer/package checks are not a security sandbox |

### C. Execution contract-by-contract matrix

These rows are intentionally separate from backtesting. A backtest fill or virtual-book target is not
an OMS order.

| Windows execution unit | User-visible/safety purpose | Mac state | Next translation |
|---|---|---|---|
| `OrderIdentifiers` | Stable internal/client/broker identities and idempotency | Missing | Port unchanged where portable |
| `OrderDomain` / `OrderLifecycle` | Valid order types, sides, quantities, prices and state transitions | Missing | Port as plain `net9.0` domain |
| `OrderEvents` | Append-only lifecycle facts | Missing | Port before any adapter |
| `OrderManagementService` | Single admitted submit/cancel/replace state machine | Missing | Paper-only composition first |
| `OrderEventStore` | Durable event contract | Missing | Port interface and contract tests |
| `SqliteOrderEventStore` | Durable ledger, schema, projections and recovery | Missing | Use Mac-safe SQLite path/permissions |
| `ExecutionLease` + fencing | Prevent two executors owning the same account | Missing | Port and preserve monotonic fencing semantics |
| `ReconciliationEngine` | Compare broker truth with ledger after disconnect/restart | Missing | Port after simulated adapter |
| `RiskPolicy` / `RiskEngine` | Admit/reject scaled order intent before OMS submission | Missing | Do not substitute legacy backtest `RiskManager` |
| `ScaledValues` / `UnitDefinition` | Avoid floating-point ambiguity in money/quantity | Missing | Port as wire/domain contract |
| `SignalExecutionPolicy` / `TradeIntent` | Convert admitted signals into bounded intent | Missing | Port after domain/risk |
| `SandboxExecutionReplicator` | Only bridge from virtual book into OMS | Missing | First bridge stays Paper-only |
| `SimulatedVenue` | Deterministic venue behavior | Missing | Port before any real adapter |
| `SimulatedExecutionAdapter` | Paper broker behind the same OMS seam | Missing | Required first execution adapter |
| `ExecutionCoordinator` | Own mode/account/adapter coordination | Missing | Compose only after ledger/risk/reconciliation |
| Session Paper/Real selection | App starts Paper; Real requires exact `LIVE`; not persisted | Missing | Add headless authorization contract before UI |
| Per-account live authorization | Second confirmation binds broker + account | Missing | Keychain-backed Mac implementation |
| Wrong-side stop/limit validation | Reject orders that would immediately/incorrectly trigger | Missing | Port upstream tests with order domain |
| Resting market/limit/stop/stop-limit behavior | Preserve pending lifecycle through fills/cancels | Missing | Prove in simulated venue tests |
| IB execution adapter | Real IB submit/cancel/status/reconciliation | Missing | Last phase after Paper acceptance |
| cTrader execution adapter | Real cTrader submit/cancel/status/reconciliation | Missing | Port independently after Paper |
| Alpaca execution adapter | Real Alpaca submit/cancel/status/reconciliation | Missing | Port independently after Paper |
| Execution service contract | Keeps privileged execution behind a narrow API | Missing | Portable request/response contract can port |
| Authenticated local transport | Prevent local process impersonation | Named pipe + DPAPI on Windows only | Replace with owner-only Unix-domain socket/XPC design + Keychain secret |
| Execution Console | Orders, positions, mode, faults, reconciliation UI | Missing | Avalonia UI only after headless safety stack passes |

### D. UI action-by-action matrix

| Surface/action | Windows latest | Mac action today | Verdict / required work |
|---|---|---|---|
| Header broker/API flyout | Present | Shows per-broker chips/state | Present |
| Header `REC` | Opens recorder | Opens recorder | Present |
| Header extensions/package control | Present | Opens plugin/extensions manager | Present but install/catalog durability incomplete |
| Header help | Help affordance | `F1 HELP` appears display-only | Partial; wire or remove misleading affordance |
| Disconnect banner | Reconnect action | Reconnect action | Present |
| Connected broker count | Connected state | Labeled `LIVE` | **UI wording defect**; connected data is not live execution |
| File > Reconnect | Reconnect brokers | `OnReconnect` | Present |
| File > Start QuestDB | Starts/configures QuestDB | `OnStartQuestDb` | Present |
| File > Exit | Exit app | Exit | Present |
| View > Activity log | Opens activity view | Opens filter/copy/clear drawer | Present/richer |
| View > Theme selection | Theme support | Dynamic theme menu | Mac extra/richer |
| View > Theme Studio | Opens theme editor | Opens Theme Studio | Present |
| Vibe Code > Hyperion | Opens Hyperion | Mac exposes Vibe Quant under Strategy Studio | Different workflow; verify final naming/route |
| Vibe Code > CLI | Launches CLI | Launches CLI | Present |
| Vibe Code > Extensions | Opens extensions | Header/plugin manager route | Present through different placement |
| Data > Archive settings | Opens settings | Opens archive settings | Present |
| Data > Archive history | Opens archive activity | Opens archive history | Present |
| Data > Instant offload | Starts immediate archival | Starts instant offload | Present |
| Execution Engine > Console | Opens Execution Console | No menu/action | **Missing** |
| Session Paper/Real control | Visible mode selection + exact `LIVE` arm | No action | **Missing** |
| Per-account live confirmation | Required before adapter can trade | No action | **Missing** |
| Settings > Notifications | Opens notification settings | Opens notification settings | Present |
| Settings > Research | Not in current public shell | Opens research settings | Mac extra |
| Settings > AI providers | Not in current public shell | Opens provider settings | Mac extra |
| Help > Support | Opens support | Opens support | Present |
| Help > About | Opens About | Routes to same support handler | Partial; About needs a distinct dialog |
| Strategy catalog > Open strategy | Opens native authored strategy | Resolves `IStrategyFactory`; warns if no Avalonia view | Partial; does not yet start `SandboxStrategyRuntime` |
| Strategy catalog > Open visualizer | Opens native visualizer | Opens hosted authored visualizer session | Tested/present |
| Strategy catalog > Quick backtest | Not current Windows shell | Opens quick backtest | Mac extra |
| Strategy catalog > Edit card/presentation | Not core Windows path | Edits local presentation | Mac extra |
| Strategy catalog > Research paper | Opens external research URL | Opens URL | Present if URL supplied |
| Strategy catalog > Catalog link | Opens external catalog URL | Opens URL | Present |
| Strategy catalog > Marketplace | Opens Marketplace concept | Opens `https://daxalgo.com/marketplace` | Handoff present; installation return path missing |
| Charts > Chart | Not current public shell | Opens chart window | Mac extra |
| Charts > Order book | Not current public shell | Opens order-book window | Mac extra |
| Charts > Volume footprint | Not current public shell | Opens footprint window | Mac extra |
| Charts > Bookmap/VolBook | Not current public shell | Opens heatmap/book view | Mac extra |
| Charts > Surface lab | Not current public shell | Opens surface lab | Mac extra |
| Charts > Bubble line | Not current public shell | Opens bubble chart | Mac extra |
| Tools > Backtest Studio | Removed from latest Windows product | Opens Backtest Studio | Mac extra; not OMS parity |
| Tools > Market regime | Not current public shell | Opens regime tool | Mac extra |
| Tools > Correlation/live correlation | Not current public shell | Opens correlation tools | Mac extra |
| Tools > Recorder | Header route in Windows | Opens recorder | Present |
| Tools > LSE backtester | Not current public shell | Opens LSE backtester | Mac extra |
| Tools > Stationarity/ARIMA/GARCH/Kalman | Not current public shell | Opens research tools | Mac extra |
| Tools > QuantConnect tabs | Not current public shell | Opens backtest/projects/data/settings | Mac extra |
| Research > Paper Lab | Not current public shell | Opens Paper Lab | Mac extra; separate from canonical OMS Paper mode |
| Research > Factor/ML/backtest analysis/market analyst | Not current public shell | Opens research tools | Mac extra |
| Activity drawer > filter/copy/clear | Simpler activity view | All actions wired | Present/richer |

### E. Persistence and recovery matrix

| Persisted state/data | Mac implementation | Runtime/user control | Gap or risk |
|---|---|---|---|
| Normalized market data | `SqliteMarketDataStore` | Config-driven | Present |
| Broker-separated market data | `PerBrokerSqliteMarketDataStore` | Config-driven | Present |
| Instrument registry | SQLite and Npgsql persistence | Automatic | Present |
| PostgreSQL/TimescaleDB | `NpgsqlMarketDataStore` + Timescale schema | Config-driven | Present; deployment not exercised here |
| QuestDB | Store + Docker bootstrap/service | File menu start action | Present; depends on local Docker/runtime |
| Composite/fallback store | `CompositeMarketDataStore` | Composition-driven | Present |
| Query/replay | Repository + replay/simulated consumers | Tool-specific | Present |
| Recorder captures | Recorder/capture services | `REC` UI | Present |
| Archive manifest | `ArchiveManifestStore` | Archive settings/history | Present |
| Scheduled archive | `ArchiveScheduleService` | Archive settings | Present |
| Immediate archive/offload | `MarketDataArchiver` | Data > Instant offload | Present |
| Local Parquet lake | `LocalParquetLakeExporter` | Archive configuration | Present |
| Telegram archive transport | Transport + auth prompt + protected credential layer | Archive settings/login | Present; external login not tested |
| Backtest Parquet/CSV | Readers/writers + DuckDB query seam | Backtest tools | Mac extra/present |
| Authored sessions | `AuthoringSessionStore` in local app data | Automatic | Present |
| Strategy presentation/cards | `StrategyPresentationStore` | Catalog edit action | Present |
| Tool presets/last instrument | Dedicated local stores | Automatic | Present |
| Plugin state | `PluginStateStore` | Extensions UI | Legacy path remains beside new package model |
| User-owned live-data persistence toggle | Startup `PersistLiveData` only | No equivalent runtime toggle | **Partial**; port `ILocalMarketDataPersistence.SetLocalPersistence` |
| Virtual model portfolio | In memory | Runtime lifetime | Correct for current headless sandbox, but not durable OMS state |
| Order event ledger/projections | None | None | **Missing** |
| Open-order recovery after restart | None | None | **Missing** |
| Broker reconciliation after reconnect | None | None | **Missing** |
| Execution lease/fencing | None | None | **Missing** |
| Package installation/catalog registration | None durable for open packages | None | **Missing** |

### F. Security boundary matrix

| Security control | Windows latest | Mac state | Verdict / required Mac rule |
|---|---|---|---|
| Broker/AI credential storage | Windows protected storage/credential abstractions | `PlatformSecretStore` uses macOS Keychain; other protected stores exist | Present; consolidate duplicated secret paths over time |
| DAXQ protected material | DPAPI/Keychain abstraction | Keychain-backed platform protection and native signature checks | Mac extra/present |
| Open-package path traversal limits | Bounded canonical archive reader | Ported | Tested |
| Package undeclared/duplicate payload rejection | Enforced | Ported | Tested |
| Package payload length/hash binding | Enforced | Ported | Tested |
| Loose plugin DLL rejection | Required product rule | New open-package boundary rejects loose DLLs, but legacy plugin UI/path remains | Partial; remove ambiguous legacy install affordance |
| Authored-code forbidden API analyzer | `DAX3001` | Missing | Must ship before third-party compile/install |
| Runtime isolation | In-process; analyzer is a guard, not a sandbox | In-process | Known limitation on both; do not market as hostile-code isolation |
| Default execution mode | Paper | No canonical execution mode | Missing |
| Session live arm | Exact `LIVE`, non-persisted | Missing | Must remain non-persisted and fail closed |
| Per-account live arm | Separate broker/account confirmation | Missing | Bind confirmation to exact adapter + account in Keychain-backed state |
| Pre-submit risk gate | Canonical scaled risk engine | Only older backtest risk components | Missing canonical gate |
| Durable audit trail | SQLite order ledger/projections | Missing | Required before Real mode |
| Recovery/reconciliation | OMS/broker comparison | Missing | Required before Real mode |
| Local execution IPC authentication | HMAC-framed named pipe + DPAPI service secret | Missing | Use owner-protected Unix socket/XPC plus Keychain secret and replay protection |
| Live adapters enabled by default | Guarded by two live gates | No adapters | Keep disabled until adapter-specific acceptance is green |
| Vendor portability | Ninja gated by Windows dependency | Ninja removed from Mac forms | Correct platform gate |
| App code signing | Windows release artifact path | `codesign` nested Mach-O then app bundle | Present in local packaging script |
| Hardened runtime/timestamp | Windows-specific signing model | Enabled when Developer ID identity is supplied | Present conditionally |
| Notarization/stapling/Gatekeeper assessment | N/A | Supported when notary profile is supplied | Present conditionally; not verified in this audit |

### G. Update, build, packaging, and release matrix

| Release function | Windows latest | Mac state | Exact next work |
|---|---|---|---|
| Update contract | `IUpdateChecker` / `IUpdateNotifier` / manifest | Missing | Port portable Core contracts |
| Remote update check | `HttpUpdateChecker` | Missing | Port with pinned HTTPS endpoint, size/time bounds and cancellation |
| Update background service | `UpdateCheckService` | Missing | Compose without blocking startup |
| User update notice | `UpdateNoticeViewModel` + dismissed-version store | Missing | Build Avalonia notice/action |
| Update installation | Detection/link only | Missing | Keep detection/link only initially; do not self-replace app bundle |
| Update tests | 32 declared Windows cases | None | Port contracts/checker/view-model tests |
| Windows CI release trigger | Tag `v*` or manual dispatch | No Mac workflow found | Add separate macOS runner workflow |
| Windows artifact | Self-contained `win-x64` zip | N/A | Reference behavior only |
| Mac architectures | N/A | Packaging supports `osx-arm64` and `osx-x64` | Present |
| Mac app bundle | N/A | Builds `DaxAlgo Terminal.app` with plist/icon | Present in `tools/macos/package.sh` |
| Mac dependency staging | QuestDB staged in Windows CI | IB SDK required/auto/off; DAXQ native optional/required/off | Present but release credentials/dependencies must be supplied |
| Nested native signing | N/A | Signs Mach-O files before app seal | Present |
| Developer ID signing | N/A | Supported via `CODESIGN_IDENTITY` | Conditional/unverified |
| Apple notarization | N/A | `notarytool`, stapling and `spctl` supported | Conditional/unverified |
| Mac release upload | GitHub Release in Windows workflow | No workflow | **Missing**; add checksums and both RID artifacts |
| SDK NuGet publication | SDK 0.3 source/templates represented | Mac version mismatch; no integrated publish evidence | **Missing convergence/release step** |
| Template package publication | Windows templates exist | Missing | Port, test, then publish with SDK version |
| New sandbox projects in app bundle | Windows baseline includes them through graph | Added to Mac solution | Verify publish dependency graph and both RIDs before release |

### H. Test evidence by boundary

| Boundary | Current Mac evidence | What it proves | What it does not prove |
|---|---:|---|---|
| Open package reader/writer | 34/34 | Bounded archive and verification behavior | Runtime installation or hostile-code isolation |
| Portable SDK/drawing | 42/42 | Contract/drawing behavior | A third-party strategy can be safely compiled |
| Avalonia application suite | 109/109 | Shell/UI composition under tests | Real vendor login or pixel-perfect manual UI |
| Visualizer runtime | 27/27 | Hosted visualizer lifecycle | Marketplace install round trip |
| UI Core catalog/host | 16/16 | Kind-aware catalog routing | Strategy runtime launch is composed |
| Model portfolio | 146/146 | Deterministic virtual portfolio/fault behavior | OMS durability or live fills |
| Sandbox strategy runtime | 30/30 | Serialized callbacks/lifecycle/virtual-book recording | Execution replication or broker orders |
| Windows execution reference | 207 declared cases in public checkout | Depth of upstream execution contract | No Mac execution code has passed them yet |
| Windows execution UI reference | 53 declared cases | Depth of upstream console/mode behavior | No Avalonia console exists yet |
| Windows updates reference | 32 declared cases | Update detection/dismissal contract exists | No Mac update implementation exists |

## Subsystem roll-up (secondary)

### 1. Startup, login, and shell

| Function | Windows latest | Integrated Mac | Status / evidence |
|---|---|---|---|
| Application shell | One .NET 9 WPF `TradingTerminal.App.Basic` shell | .NET 9 Avalonia macOS shell | Equivalent framework role; native UI adaptation is required |
| Product-account gate | No DaxAlgo subscription/account gate; opens broker selection | Mac retains an account/session layer in addition to broker login | Mac extra/different product policy |
| Broker selection | Multi-broker selection with independent sessions | Multi-broker form factory and selector | Equivalent contract |
| Connection status | Aggregate state, per-broker state, reconnect | Aggregate status, connected count, disconnect banner, reconnect-all | Equivalent user-visible behavior |
| Header/API meter | API state and activity visibility | Per-broker API tracker, feed-drop indicator, clocks and sessions | Mac equal or richer |
| Recorder chip | `REC` chip in header | `REC` chip and recorder window | Equivalent |
| Activity log | Universal activity log | Filter/copy/clear drawer | Equivalent or richer |
| Execution menu | Execution Console entry | No Execution Engine menu | Missing |
| Paper/Real mode | Session-only Paper/Real arm; exact `LIVE` text required | No composed execution-mode selection | Missing |

Mac evidence: `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/MainWindow.axaml`,
`MainWindowViewModel.cs`, and `src/linux/Shell/TradingTerminal.Login/BrokerLoginFormFactory.cs`.
Windows evidence: `README.md` sections “Order execution”, “Brokers”, and “The current shell”, plus
`src/windows/Core/TradingTerminal.Core/Execution/ExecutionModeSelection.cs`.

### 2. Brokers: data connection versus order execution

| Broker/source | Windows market data | Mac market data/login | Windows order execution | Mac order execution |
|---|---|---|---|---|
| Interactive Brokers | Optional vendor SDK, credentials, connect/reconnect | Client and login form present; vendor/runtime availability still applies | Present | Missing |
| NinjaTrader | Optional `NTDirect.dll` desktop integration | Explicitly unavailable on macOS | None | Not applicable |
| cTrader | Credentials/token/account selection | Client and login form present | Present | Missing |
| Alpaca | Key/secret, paper/live data endpoint | Client and login form present | Present | Missing |
| Binance | Keyless public crypto feed | Present | None | Not applicable |
| Coinbase | Keyless public crypto feed | Present | None | Not applicable |
| Bybit | Keyless public crypto feed | Present | None | Not applicable |
| Kraken | Keyless public crypto feed | Present | None | Not applicable |
| OKX | Keyless public crypto feed | Present | None | Not applicable |
| Ironbeam | Credentialed REST/WebSocket data | Client and login form present | None | Not applicable |
| London Strategic Edge | API-key data source | Client and login form present | None | Not applicable |
| Upstox | OAuth market-data source | Client and login form present | None | Not applicable |
| Simulated | Provenance enum only; no longer connectable | Still connectable for offline synthetic/replay work | Paper adapter exists in execution engine | Older Mac simulation only; not canonical OMS |

Both repositories expose the same independent `IBrokerSelector` lifecycle: available kinds,
connected kinds, per-broker state, `ConnectAsync`, and `DisconnectAsync`. Mac deliberately filters
NinjaTrader from `BrokerLoginFormFactory` on macOS. No live network/account verification is recorded in
this integration task, so “present” means code and composition are present, not that every vendor was
authenticated during this run.

### 3. Market data, storage, replay, recorder, and archives

| Function | Integrated Mac status | Detail |
|---|---|---|
| Canonical `InstrumentId` and provenance | Equivalent | Broker SDK types stay behind Infrastructure; normalized records cross the boundary |
| Quote/trade/bar/depth ingest | Equivalent | Bounded, tick-primary pipeline and hub are present |
| Per-broker connection routing | Equivalent | Selector and discovery enumerate available sessions |
| SQLite/per-broker SQLite | Present | Mac retains both embedded store paths |
| PostgreSQL/TimescaleDB | Present | Configurable store and fallback logic exist |
| QuestDB | Present | Launcher/bootstrap, store, and UI start action exist |
| Query/replay | Present | Store/repository and simulated/replay consumers exist |
| Background recording | Present | Recorder UI and capture services exist |
| Parquet/archive/offload | Present | Archive history, scheduling, transport, and lake exporter exist |
| User-owned “do not persist live data” runtime switch | Partial | Startup `PersistLiveData` option exists; latest Windows `ILocalMarketDataPersistence.SetLocalPersistence` UI seam is absent |

### 4. SDK, authoring, sandbox, and rendering

| Function | Windows latest | Integrated Mac | Status |
|---|---|---|---|
| `IStrategyKernel` | SDK 0.3 contract | Portable contract ported | Equivalent contract |
| `IVisualizer` | SDK 0.3 contract | Portable contract ported | Equivalent contract |
| Scoped data/clock/parameters/alerts/book | SDK contexts | Portable context contracts ported | Equivalent contracts |
| Drawing primitives | Candles, plot, ladder, footprint | Ported and tested | Equivalent portable behavior |
| Native rendering | WPF `RenderSurfaceView` | Avalonia `RenderSurfaceView` and `DrawingContextSurface` | Equivalent native adaptation |
| Visualizer runtime | In-memory sandbox runtime | Ported with hosted Avalonia view/session | Mostly equivalent |
| Strategy catalog kind | Strategy and visualizer cards | Kind-aware cards and visualizer open action ported | Equivalent presentation contract |
| SDK analyzer | `DAX3001` blocks filesystem, network, process, P/Invoke and host APIs | Analyzer project/package missing | Missing safety/compiler gate |
| SDK version | `0.3.0` | Still reports `0.2.0-alpha` | Missing convergence step |
| Templates and samples | Two `dotnet new` templates plus tested samples | Latest templates/samples not present | Missing developer workflow |
| Strategy runtime | `SandboxStrategyRuntime` with serialized pump and lifecycle | Ported as plain `net9.0`; 30/30 focused tests pass | Equivalent headless host |
| Model portfolio | Bounded simulator, snapshots, faults, closed trips | Ported as plain `net9.0`; 146/146 focused tests pass | Equivalent deterministic model |
| Virtual book bridge | `RecordingVirtualBook` captures declared targets | Ported with declared-instrument bounds | Equivalent headless bridge |

The SDK contract alone does not run a strategy. `IStrategyKernel` is the plug shape; the sandbox
runtime is the engine that starts it, serializes authorized data callbacks, applies its virtual-book
targets, maintains model-portfolio state, and stops it deterministically.

### 5. Packages, Extensions, and Marketplace

| Function | Integrated Mac status | Detail |
|---|---|---|
| Read/write `.daxalgostrategy` | Ported | Portable bounded package contract and adversarial tests pass |
| Read/write `.daxalgovisualizer` | Ported | Same open-package model |
| Manifest/hash verification | Ported | Extensions inspection uses the shared verifier |
| StrategyBuilder provenance | Mac-forward-port extension | Confirmed-run/artifact digests can be bound into package provenance |
| Marketplace projection | Bridge present | Produces a verified handoff projection; the website remains a separate workstream |
| Install package into durable runtime catalog | Missing | Also unfinished in latest public Windows; current Mac catalog still contains legacy `.daxplugin` paths |
| Loose DLL loading | Must remain rejected | Latest public boundary does not accept loose plugin DLLs |
| `.daxstrategy` bundle | Present, Mac richer | Signed/inspectable storage format; not the open Marketplace package |
| `.daxq` | Mac extra | Protected alpha/signal runtime; not an order-execution replacement |

### 6. Execution and risk

| Function | Windows latest | Integrated Mac | Status |
|---|---|---|---|
| Order domain and identifiers | Explicit IDs, terms, lifecycle and events | Some older Core execution DTOs exist | Partial/incompatible until mapped |
| OMS | Order management service with state transitions | No equivalent project | Missing |
| Durable order ledger | SQLite event store and projections | No equivalent | Missing |
| Lease/fencing | Execution lease ownership and fencing | No equivalent | Missing |
| Reconciliation | Broker/ledger recovery and reconciliation | No equivalent | Missing |
| Risk engine | Scaled values, policies, admission decisions | Older `RiskManager` used primarily in backtests | Not equivalent |
| Virtual-book replication | Only route from strategy toward OMS | No composed equivalent | Missing |
| Paper venue | Simulated execution adapter/venue through the same OMS | Mac backtest simulation is a separate path | Missing canonical Paper route |
| Real adapters | IB, cTrader, Alpaca | No execution adapter projects | Missing |
| Order types | Market, limit, stop, stop-limit and resting entries | Backtest order types do not establish live parity | Missing product path |
| App-wide live arm | Non-persisted, exact `LIVE`, defaults Paper | Missing | Missing |
| Per-account live acknowledgement | Separate exact broker/account confirmation | Missing | Missing |
| Execution IPC/service | Authenticated named pipe + Windows service | No Mac equivalent | Requires native substitution, not source copy |
| Execution Console | WPF books/orders/positions/mode UI | No Avalonia equivalent | Missing |

Windows has 207 declared execution test cases and 53 execution-UI cases in the latest checkout. The
Mac tree has no `src/linux/Execution` directory and no equivalent execution test projects. Existing
Mac `BacktestOrderRouter`, `SimulatedOrderBook`, TradeIR risk gateway, and `RiskManager` are valuable
test/research components, but they do not provide OMS persistence, reconciliation, broker adapters, or
the two live-money gates.

### 7. Charts, tools, research, and backtesting

The Mac application is broader than the latest public Windows shell in this area. It retains chart,
order-book, footprint, heatmap/bookmap, 3D surface, bubble-chart, correlation, market-regime,
stationarity, ARIMA/GARCH, Kalman, QuantConnect, Backtest Studio, Paper Lab, AI research, TradeIR,
DAXQ, and strategy-research surfaces. Latest public Windows intentionally removed its legacy backtest
engine and exposes none of those Mac research additions as a replacement.

These are `Mac extra`, not evidence that the canonical SDK strategy-to-execution lifecycle is complete.
They should remain available as validation laboratories while the shared runtime boundary is converged.

### 8. Updates, platform security, packaging, and release

| Function | Integrated Mac status | Required translation |
|---|---|---|
| Signed update-manifest detection | Missing | Port portable manifest/signature/check logic; open a release page rather than self-updating |
| Credential protection | Present | Mac Keychain replaces Windows credential/DPAPI storage for broker and AI keys |
| Live-confirmation secret storage | Missing with execution | Keychain-backed, fail-closed store is required before real mode |
| Execution IPC | Missing | Owner-protected Unix-domain/local IPC or app-hosted service with peer authentication |
| App signing/notarization | Existing Mac pipeline | Revalidate after every new executable/project is added |
| NinjaTrader | Explicitly gated | No safe macOS translation exists |

## Evidence versus inference

### Direct evidence

- Windows `README.md` states the real-order capability, two independent live gates, virtual-book-only
  route, supported order types, broker matrix, package limitations, and current shell surface.
- Windows `src/windows/Execution/` contains the OMS, event store, leases, reconciliation, risk engine,
  simulated venue, three real adapters, secured service transport, and Execution Console.
- Mac `src/linux/Core/TradingTerminal.Core/Brokers/IBrokerSelector.cs` and
  `src/linux/Pipeline/TradingTerminal.Infrastructure/Brokers/BrokerSelector.cs` contain the same
  independent per-broker connection lifecycle.
- Mac `src/linux/Shell/TradingTerminal.Login/BrokerLoginFormFactory.cs` exposes all supported forms and
  explicitly removes NinjaTrader on macOS.
- Mac `src/linux/Sdk/DaxAlgo.Sdk/IStrategyKernel.cs` contains the current portable kernel contract;
  `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/` and
  `src/linux/Sandbox/TradingTerminal.Sandbox.Portfolio/` now contain the ported headless runtime,
  virtual-book bridge, and model-portfolio implementation.
- Mac has no `src/linux/Execution` or Core Updates directory; Windows has dedicated projects and tests
  for both.

### Engineering inference

- Broker market-data parity is high because contracts, clients, login forms, composition, reconnect,
  status aggregation, and UI are present. Confidence is **high for code presence**, but only **medium
  for real-world operability** because this integration did not authenticate every vendor.
- Overall Windows-product parity is about 60-70%. Confidence is **medium** because subsystem weighting
  is judgmental even though the missing/present boundaries are directly observable.
- The completed model-portfolio/sandbox runtime was the correct first execution-adjacent port because
  it is portable, exercises `IStrategyKernel`, remains Paper-only, and is an upstream dependency of
  execution replication. Confidence is **high**.

## Dependency-correct next work

1. Compose the ported `SandboxStrategyRuntime` behind catalog strategy launch while preserving its
   Paper-only virtual boundary.
2. Port the SDK analyzer, templates, samples, and converge SDK/versioned consumers to 0.3.
3. Port the portable execution domain, event ledger, simulated venue, risk, leases, and reconciliation
   under Paper-only tests.
4. Design the Keychain-backed live confirmation and owner-protected Mac execution service boundary.
5. Add the Avalonia Execution Console and session-only Paper/Real control.
6. Add IB/cTrader/Alpaca execution adapters one at a time, with Paper first and explicit live-account
   acceptance tests before enabling Real.
7. Add runtime persistence control, update detection, and package installation/catalog durability.
