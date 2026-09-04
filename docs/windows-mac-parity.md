# Windows-to-Mac functional parity

Public authority: `dhruuvsharma/DaxAlgo-Terminal` `main` at `6d69136`.
Mac integration baseline: `6cbd184`.

This is a behavior map, not a file-copy checklist. Public Windows views are WPF; Mac views are
Avalonia. Portable contracts and view models may be shared or forward-ported, but visual surfaces and
OS integrations require native Mac implementations.

| Slice | Public Windows | Integrated Mac | Status / next boundary |
|---|---|---|---|
| Open artifact package | `.daxalgostrategy` and `.daxalgovisualizer` writer, reader, verifier, bounded extraction | Same portable contract, adversarial suite, StrategyBuilder provenance bridge, Extensions verify gate, Marketplace handoff projection | **Ported + connected: package + bridge tests pass** |
| SDK 0.3 strategy/visualizer contract | Capability-scoped `IStrategyKernel`, `IVisualizer`, render-surface primitives, analyzer | Portable strategy/visualizer lifecycle, sandbox contexts, render-surface contract, and candle/plot/ladder/footprint drawing routines | **Portable layer ported: 42/42 tests pass**; next: analyzer + SdkInfo 0.3 bump after catalog consumers |
| Extensions | Reads and verifies open packages; installation remains unfinished upstream | Avalonia Strategy Manager uses the shared Extensions verify gate for open packages; sealed `.daxq` remains Mac-only; install of open packages still gated | **Inspect path aligned**; catalog still installs legacy `.daxplugin` feed artifacts |
| Strategy authoring | Hyperion compile/review/register plus agent CLI | Richer Vibe Quant research, confirmed intent, native lanes, TradeIR smoke | Preserve Mac workflow; converge final artifact on SDK/package contract |
| Authored-unit host | `SandboxVisualizerRuntime.TryDraw` → `AuthoredUnitHost` → WPF `AuthoredUnitView` / `RenderSurfaceView` | Same runtime + host + Avalonia `AuthoredUnitView` / `RenderSurfaceView`; `AuthoredVisualizerSession` + `IVisualizerRegistry` in DI | **Hosted: Sandbox 27/27, UI.Core 16/16**; visualizer cards route through the authored session |
| Sandbox strategy runtime | `IStrategyKernel` serialized live-data host with bounded model portfolio and virtual book | Portable model portfolio and sandbox runtime ported as `net9.0` | **Headless host ported: Portfolio 146/146, Runtime 30/30**; catalog launch and execution replication remain separate |
| Execution | OMS, virtual books, persistent execution books, paper/real gates, IB/cTrader/Alpaca adapters | No composed execution product; TradeIR produces simulated intents only | Port domain/OMS headlessly before any Avalonia console or live adapter |
| Execution UI | Console, header books chip, book start/stop and persistence | No equivalent | Build Avalonia views only after headless execution tests pass |
| Login/trading mode | Blocking broker login with non-persisted Paper/Real switch and typed `LIVE` arm | Account/login flow without public execution-mode contract | Port mode-selection contract with Paper-only default before execution UI |
| Strategy/visualizer rendering | Shared render surface, authored-unit host, catalog kind switch | Avalonia `DrawingContextSurface` + `RenderSurfaceView` + `AuthoredUnitView`; host/runtime and kind-aware catalog ported | **Render + host + card action present**; package installation remains pending |
| Backtest | Legacy engine archived; no current replacement | Backtest engine, worker and TradeIR acceptance depend on it | Retain as a Mac validation lab; do not expose it as the canonical live runtime |
| Market-data persistence | User can disable local persistence; recorder remains explicit | Persistence/archive present but lacks the new global user-owned gate | Port the gate after execution foundations |
| Updates | Signed release feed, pinned ECDSA verification, dismissible UI notice | Missing | Portable low-risk slice after SDK convergence |
| Packaging/release | Windows build and SDK packages | `.app`, signing, notarization, DAXQ runtime packaging | Preserve Mac release pipeline; add new projects to signing/package checks |

## Integration rule

StrategyBuilder produces reviewed, hash-bound strategy intent and artifacts. The SDK/package layer is
the publication boundary: confirmed-run digests travel as provenance inside `.daxalgostrategy`,
Extensions verifies that package before any install, and Marketplace binds the resulting package /
manifest digests. The execution engine consumes admitted strategy targets through virtual books. UI
never bypasses package verification, capability checks, risk policy, or Paper/Real gates.

The required platform classifications and WPF-to-Avalonia/macOS substitutions are defined in
`docs/windows-to-macos-translation-guide.md`.

The evidence-backed subsystem and broker inventory is in
`docs/windows-macos-functional-inventory.md`.
