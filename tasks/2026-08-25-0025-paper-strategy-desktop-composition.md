# Paper strategy desktop composition

## Goal

Compose the already implemented canonical sandbox runtime and target replicator into the existing
macOS/Avalonia application. A user must explicitly choose one strategy and one canonical asset,
start/pause/resume/stop the runtime, and see committed model targets reach the same authenticated
Paper OMS used by the manual Execution Console.

## Plan

1. Preserve the completed `IStrategyKernel -> IVirtualBook -> SandboxExecutionReplicator` boundary.
2. Add an IPC-backed execution-book intake which reads verified client snapshots and submits only
   through `IPaperExecutionClient`.
3. Add one native Avalonia runner surface with explicit strategy, asset, parameter, lifecycle,
   model-portfolio, and replication state.
4. Compose the existing Sandbox Runtime project into the app; create no new project.
5. Prove the route end-to-end and verify the UI and fail-closed states.

## Blast radius

- `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/`
- `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/`
- Existing app project reference, DI, and shell menu/action only
- Matching Sandbox Runtime and Avalonia app tests

No live broker adapter, real account, credential flow, new project, repository remote, or Windows
source is changed.

## Build filter

- `tests/linux/TradingTerminal.Sandbox.Runtime.Tests/TradingTerminal.Sandbox.Runtime.Tests.csproj`
- `tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj`
- `src/linux/Shell/TradingTerminal.App.Avalonia/TradingTerminal.App.Avalonia.csproj`

## Tests

- `TradingTerminal.App.Avalonia.Tests`: 123 passed, 0 failed, 0 skipped.
- `TradingTerminal.Sandbox.Runtime.Tests`: 48 passed, 0 failed, 0 skipped.
- Focused desktop route/surface set: 8 passed.
- Focused runtime drawing set: 2 passed.
- Both affected test projects build successfully. The only build warning is `NU1900` because the
  restricted environment cannot query NuGet vulnerability metadata.
- The named `TradingTerminal.App.Avalonia.csproj` build succeeds with the same single `NU1900`
  environmental warning and no compile errors.

## Findings

- `SandboxStrategyRuntime`, `SandboxExecutionReplicator`, and a direct-OMS headless intake already
  exist and have focused tests.
- The app does not reference `TradingTerminal.Sandbox.Runtime`, expose a strategy Paper runner, or
  compose any replicator.
- The existing direct-OMS intake is unsuitable for desktop composition because it would bypass the
  authenticated Unix-socket execution-service boundary completed in the preceding milestone.
- The runtime supports exactly one resolved `Instrument` parameter. The desktop must therefore make
  asset selection explicit before Run and must not infer a pair/basket from free-form text.

## Diff summary

### Product route now implemented

1. `PaperStrategyRunnerViewModel.StartAsync()` takes one real `BacktestStrategyOption` from the
   application's dynamic strategy registry and one explicit canonical instrument from the existing
   Paper catalog.
2. It creates the strategy through `BacktestStrategyOption.Create()` and bridges the legacy
   `IBacktestStrategy` into the canonical SDK contract with `LegacyStrategyKernelAdapter`.
3. `SandboxStrategyRuntime.RunAsync()` resolves and locks the instrument/launch parameters, creates
   a private `ModelPortfolioAccount`, starts the kernel, subscribes only the declared market-data
   requirements, and serializes callbacks through its bounded pump.
4. A strategy order becomes a declarative `IVirtualBook` target. The runtime reconciles and commits
   it before `SandboxExecutionReplicator` observes the snapshot.
5. `SandboxExecutionReplicator` maps the committed position/protection state into one exact
   `TradeIntent` with `TargetPosition` quantity semantics.
6. `AuthenticatedPaperExecutionBookTargetIntake.SubmitTargetAsync()` verifies the book and strategy
   binding, requires a fresh exact reference price, refreshes the authenticated service snapshot,
   and refuses submission when the Paper admission/lease gate is closed.
7. `PaperExecutionDesktopSession.TryCreateTargetSubmit()` compares broker-independent Paper
   position plus non-terminal reservations with the target. It no-ops an already converging target,
   blocks a conflicting order, or creates the exact delta and canonical instruction.
8. `IPaperExecutionClient.SubmitAsync()` sends the request through authenticated owner-only Mac IPC.
   The execution service then runs the same risk, event ledger, OMS, deterministic venue, fill,
   projection, and SQLite durability path as the manual Execution Console.

### Native desktop surface now implemented

- Existing shell menu: `Execution Engine -> Paper Strategy Runner...`.
- One owned runner window; duplicate clicks focus the same operational surface.
- Dynamic installed/in-session authored strategy choices, not a hard-coded demo strategy list.
- Explicit canonical asset selector; the asset is converted to a canonical `Contract` and locked
  with the strategy/parameters while active.
- Run, Pause, Resume, Stop, and explicit Retry Target commands.
- Runtime state, committed model target/position, average entry, model equity, dropped-event count,
  latest alert, and latest authenticated route result.
- Active SDK `IRenderSurface` visualization on the right. `SandboxStrategyRuntime.TryDraw()` permits
  drawing only while running/paused and isolates authored drawing exceptions from the event pump.
- Before Run, the visualization panel is no longer an unexplained blank. It shows the selected
  canonical asset, states that the authored frame begins after the binding is locked, and explains
  why an explicit multi-instrument schema is required for a pair/basket.
- Paper-only and authenticated-IPC wording; no live adapter can be selected or inferred.

### Exact Windows comparison for the visualization/asset behavior

- Windows `AuthoredUnitView` always allocates the `RenderSurfaceView`; the shell opens the window and
  then starts `SandboxVisualizerRuntime`. Before startup or when `Draw()` emits nothing, Windows
  deliberately shows an empty surface (`AuthoredUnitPresenter.Draw` documents that behavior).
- Windows instrument identity is supplied through declared `StrategyParameter` values and the
  scoped runtime context. Windows does not parse strategy names or natural-language text into an
  asset or pair.
- Windows `ModelPortfolioAccount` is also single-instrument. Its generic authored-unit chrome can
  display a picture, but that does not mean Windows provides shared-equity pair execution.
- Mac now preserves those boundaries while making the pre-start state more explicit than Windows:
  selected asset identity and pair/basket rejection are visible inside the visualization panel.

### Asset-general and multi-asset behavior

- No ticker is hard-coded in the production route. The end-to-end test selects a catalog entry
  without naming its symbol and verifies that exact `InstrumentId` reaches the persisted order.
- Strategies declaring zero instrument parameters receive one explicit runner-bound instrument.
- Strategies declaring one instrument parameter use that explicit binding.
- Strategies declaring two or more instrument parameters are excluded and counted with a visible
  reason. One pair/basket definition cannot crash or hide all otherwise eligible strategies.
- Pair/basket execution is not inferred from names or typed prose. It remains blocked because the
  existing `ModelPortfolioAccount` is deliberately single-instrument and cannot provide honest
  shared-equity basket accounting.

### Defects found and corrected during functional proof

- The initial runner requested a 100,000-unit model bound even though the existing simulator's
  validated maximum is 100. Startup correctly failed closed. The runner now uses the simulator's
  existing bounded default instead of bypassing or weakening validation.
- A test initially named `SPY` as deterministic input. That literal was removed; the proof now uses
  an arbitrary catalog asset and asserts identity propagation, demonstrating the route is generic.
- Headless success-state assertions now drain the Avalonia dispatcher before reading asynchronously
  posted UI messages; this changes no product behavior.

## Verification

- End-to-end proof asserts: catalog strategy starts; one quote drives a +2 model target; the target
  crosses authenticated IPC; the durable Paper order fills; strategy provenance, client-order ID,
  target-position semantics, canonical instrument identity, and exact final position are preserved.
- Surface proof asserts shell/menu wiring, Paper-only language, explicit single-asset warning, and
  successful headless rendering of the runner window.
- Runtime proof asserts drawing is unavailable while idle/stopped, available while running/paused,
  and that an injected drawing exception is logged while subsequent market events continue.
- Regenerated `.claude/context/linux` after the public runtime API and app dependency changes:
  66 projects, 1,238 files, 219,816 LOC.
- The generated dependency graph confirms `TradingTerminal.App.Avalonia ->
  TradingTerminal.Sandbox.Runtime`. The optional PowerShell `deep-check` wrapper could not run
  because neither `pwsh` nor `powershell` is installed; the repository-required generator itself
  completed successfully.

## Risks/deferred

- Multi-asset/pairs kernels remain unsupported by the current single-instrument model portfolio;
  the UI must state this instead of pretending pair execution exists.
- Historical warm-up remains deferred by `SandboxStrategyRuntime`.
- Native multi-instrument pair/basket portfolio accounting and one atomic multi-leg OMS intent remain
  deferred; hiding an unsupported schema is truthful capability handling, not pair-trading parity.
- The dynamic strategy registry is snapshotted when the runner window is constructed; registry
  changes require reopening the runner to refresh its choices.
- Real broker execution remains prohibited by repository policy.
