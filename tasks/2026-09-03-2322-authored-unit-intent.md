# Authored unit intent

## Goal

Add the canonical, Core-owned request contract that carries a natural-language or reference-chart
request into the existing visualizer/strategy authoring pipeline. The contract must distinguish
visual similarity from indicator similarity and historical market-pattern search so Hyperion cannot
silently guess what "make a chart like this" means.

## Plan

1. Define a versioned authored-unit specification for Visualizer and Strategy artifacts.
2. Represent instrument selection, timeframe, data requirements, parameters, drawing panes/layers,
   reference-chart provenance, and Paper-only execution intent.
3. Add deterministic structural and launch-readiness validation.
4. Add canonical JSON/hash support and focused contract tests.
5. Import/hash/persist chart references in the existing Hyperion authoring session.
6. Carry reference provenance into semantic generation and block implementation until analyzed.
7. Inspect exact image bytes with a vision-capable provider.
8. Convert an image pattern into a bounded normalized path, compare it with real stored OHLCV
   history, and require an explicit instrument/index selection.
9. Generate, compile, install, register, and launch the reviewed Visualizer or Strategy.
10. Give the launched unit ownership of its reviewed broker feed.
11. Route canonical Strategy targets through backtest and durable Paper execution without
    rewriting the generated strategy against a second interface.

## Blast radius

- `TradingTerminal.Core` strategy-authoring contracts.
- Existing Codegen semantic-request envelope.
- Existing Settings authoring session and Avalonia Hyperion window.
- Focused headless and Avalonia authoring tests.
- Existing market-data store/registry composition for read-only pattern search.
- No broker execution, project-topology, or live-order behavior changes in this slice.

## Build filter

- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`
- `tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj`

## Tests

- Authored-unit filtered contract/generation/source/compiler/persistence suite: 35/35 passed.
- UI Core catalog/registry suite: 31/31 passed.
- SDK completed-bar backtest adapter: 3/3 passed.
- Catalog SDK strategy → Quick Backtest composition: 1/1 passed.
- Full Avalonia test project: 153/153 passed.
- Focused Quick Backtest/Paper Runner/Execution surface handoff suite: 18/18 passed.
- Full Sandbox Runtime test project: 53/53 passed.
- Full headless test project with `TMPDIR=/private/tmp`: 1016 passed, 6 explicitly skipped, 0 failed.
- Focused multi-instrument model/replication/runtime tests: 4/4 passed.
- Focused canonical pair-strategy desktop route: 1/1 passed.
- Named Backtest project build: succeeded with 0 warnings and 0 errors.
- Named Avalonia app build: succeeded with 0 errors. NU1900 only reports unavailable online NuGet
  vulnerability metadata.
- Earlier reference artifact/UI, multimodal inspection, and store-backed chart-pattern tests remain
  covered by the full project runs.

## Findings

- The existing `StrategySpec` classifies strategies but intentionally does not encode executable
  instrument selection, parameters, drawing, or reference-chart provenance.
- `IVisualizer` and the SDK `IStrategyKernel` already provide the two runtime shapes.
- Chart capture is explicitly reported as not wired in the native authoring screen.
- The current codegen provider message is text-only. Reference metadata can safely reach semantic
  review, but pixel analysis needs a separate vision-capable adapter before implementation may run.
- Historical chart similarity is not visual styling: it must query time-series history and return
  candidate instruments before launch.

## Diff summary

- Added the canonical Visualizer/Strategy authored-unit request, chart composition, reference
  provenance/resolution, and launch validation contract.
- Added a 25 MB bounded host-owned reference artifact store with SHA-256 identity checking.
- Added Hyperion controls for four explicit meanings: appearance/layout, indicators, historical
  price pattern, and related instruments/indexes.
- Persisted chart references and the authored-unit specification with saved sessions; missing or
  modified artifacts detach on restore.
- Added reference metadata to the semantic AI envelope and prohibited false claims of pixel or
  historical analysis.
- Disabled four-lane implementation generation while any reference lacks a verified resolution.
- Extended the common codegen message with optional hash-bound image inputs. OpenAI-compatible and
  Anthropic API adapters emit native multimodal content blocks; CLI adapters reject image requests
  instead of silently discarding them.
- Added `ChartReferenceInspectorV1` and registered it in the existing codegen composition. It checks
  the artifact hash, sends the exact PNG/JPEG/WebP bytes, accepts only typed observations, and cannot
  claim to have searched market history.
- Added the existing Hyperion-window `Analyze chart` action and persisted inspection evidence with
  the session. Semantic review now receives both the reference provenance and its inspection.
- Added a pure deterministic pattern scorer over normalized close paths and actual OHLCV bars. It
  exposes shape, return-correlation, volatility, and drawdown components rather than an opaque AI
  ranking.
- Added the store-backed search over canonical instruments with separate all-instrument and
  index-only scopes, one-source provenance, coverage counts, and bounded ranked results.
- Added Hyperion timeframe/search/result/selection controls. A selected match is hash-bound to its
  reference, saved with the authoring session, and passed into semantic generation.
- The UI explicitly states that historical similarity does not predict direction or return.
- Added verified source generation and Roslyn compilation for both `IVisualizer` and canonical SDK
  `IStrategyKernel` artifacts. Persisted plugin registrations restore runnable Catalog entries.
- Added authored Visualizer feed ownership: its reviewed instrument, timeframe, and declared data
  channels resolve through broker capabilities and are disposed with the window.
- Added canonical Strategy Catalog launch into the existing Paper runner. Strategy output remains a
  virtual target and reaches the risk-gated, authenticated, durable Paper OMS rather than a direct
  broker order path.
- Added completed-bar replay to the existing backtest engine. Broker OHLCV now reaches
  `IStrategyKernel.OnBarAsync` unchanged; deterministic L1 observations are derived separately only
  for the existing simulated fill model.
- Added `SdkStrategyBacktestAdapter`: canonical quote/bar/trade callbacks, bounded replay data,
  SDK parameters, virtual-target convergence, order-event position tracking, and routing through
  the existing `IOrderRouter`.
- Added canonical Strategy Quick Backtest from the Catalog. The reviewed single instrument and
  timeframe are locked, broker aliases resolve through `IInstrumentRegistry`, unsupported depth is
  rejected, and broker historical-bar/tape capability is checked before API use.
- Added schema-driven authored parameter controls to the native Quick Backtest window. Canonical
  strategies now stop for parameter/risk review instead of auto-running defaults; values are
  coerced by `StrategyParameters`, locked during a run, and supplied to the SDK runtime context.
- Replaced Quick Backtest's `risk: null` with a visible, non-null `RiskManager` for both legacy and
  canonical paths. Maximum position and maximum daily loss are configurable in the Mac window.
- Completed-bar history now fails closed on non-UTC, duplicate/out-of-order, non-finite/non-positive,
  inconsistent OHLC, or negative-volume input instead of silently sorting or repairing causality.
- Added a bounded multi-instrument model account for canonical pair/basket strategies. Each leg keeps
  independent model state; a callback containing a target whose leg has no valid reference price is
  rolled back before any model leg commits.
- Changed runtime snapshots and replication queues from one global slot to bounded per-instrument
  collections, so coalescing cannot overwrite one pair leg with another.
- Removed the canonical single-instrument gate from the live Paper runner. It now validates and owns
  every reviewed feed, bridges every leg into the Paper venue, submits every committed target through
  risk/authenticated IPC/OMS, and displays per-leg target, model position, and average entry.
- Preserved one financial authority: pair-leg simulator equity is not summed. Shared cash, positions,
  equity, P&amp;L, fills, and reconciliation remain projections of the durable Paper OMS ledger.
- Added a full two-leg desktop test proving four owned subscriptions, two target replications, two
  filled OMS orders, two durable positions, and deterministic disposal of all four subscriptions.

## Verification

- `dotnet build tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj --no-restore --disable-build-servers -m:1 -v:minimal`
  succeeded with 0 errors.
- `dotnet build src/linux/Tools/DaxAlgo.Codegen/DaxAlgo.Codegen.csproj --no-restore --disable-build-servers -m:1 -v:minimal`
  succeeded with 0 errors.
- `dotnet build src/linux/UI/TradingTerminal.Settings/TradingTerminal.Settings.csproj --no-restore --disable-build-servers -m:1 -v:minimal`
  succeeded with 0 warnings and 0 errors.
- `dotnet test ... --filter FullyQualifiedName~AuthoredUnitSpecificationV1Tests` passed 8/8.
- `dotnet test ... --filter FullyQualifiedName~AuthoringChartReferenceTests` passed 5/5.
- `dotnet test ... --filter FullyQualifiedName~ChartReferenceInspectorV1Tests` passed 3/3.
- `dotnet test ... --filter FullyQualifiedName~ChartPatternSearchV1Tests` passed 2/2.
- `dotnet test ... --filter FullyQualifiedName~SdkStrategyBacktestAdapterTests` passed 3/3.
- `dotnet test ... --filter FullyQualifiedName~CanonicalQuickBacktestTests` passed 1/1.
- `dotnet test tests/linux/TradingTerminal.UI.Core.Tests/TradingTerminal.UI.Core.Tests.csproj ...`
  passed 30/30.
- Full `TradingTerminal.App.Avalonia.Tests` passed 144/144.
- Full `TradingTerminal.Sandbox.Runtime.Tests` passed 53/53.
- The focused canonical pair desktop test passed 1/1: two assets, four feed leases, two Paper fills,
  and two durable position snapshots.
- Named Backtest and Avalonia app builds passed; one NU1900 warning came from unavailable online
  vulnerability metadata.
- `xmllint --noout StrategyAuthoringWindow.axaml` passed after adding the similarity controls.
- `git diff --check` passed for the affected Core, MarketData, Codegen, Settings, and AXAML paths.
- `gen-context-linux.sh` completed after supplying the explicit local .NET and `rg` paths: 66
  projects, 1269 files, 232002 LOC.

## Risks/deferred

- PNG/JPEG/WebP inspection, latest-window stored-history similarity, and bounded connected-broker
  history hydration are implemented. PDF/chart-JSON parsing and historical sliding-window
  analog/forward-return research remain separate follow-up work.
- A similarity result is descriptive. Turning “similar to this chart” into “likely to go up” requires
  an explicitly causal research/backtest request and must never be inferred by this search.
- `.claude/context/manage-context.ps1 deep-check` could not run because neither `pwsh` nor
  `powershell` is installed in this environment.
- Canonical live Paper and Quick Backtest now support reviewed pair/basket instruments and preserve
  every target leg. Quick Backtest requires exact shared UTC boundaries and fails closed instead of
  forward-filling sparse legs. Level-2 replay, combined bars plus genuine historical tape, futures
  multiplier/currency valuation, and persisted named parameter presets remain explicit follow-up work.
- The separate full Backtest and LSE-specific tools still have legacy `risk: null` call sites. They do
  not power the Catalog canonical flow completed here, but should be consolidated before claiming a
  product-wide backtest risk guarantee.
- The disconnected `PythonStrategyKernel` remains outside this completed native C# path.
- Real broker order/position/cash adapters remain outside this goal and prohibited by repository
  policy; connected Alpaca/IB/cTrader clients are market-data sessions only.
- Real-money execution remains prohibited.

## User-visible workflow status

| User action | Current native Mac behavior | Remaining boundary |
|---|---|---|
| “Show BTC 1m candles with EMA 9/21” | Typed Visualizer → generated/verified/compiled/persisted → Catalog → owned broker feed → live authored window | Broker must support the reviewed channels and symbol |
| “Make a chart visually like this image” | Exact image is hash-bound, inspected, and converted into explicit panes/layers/indicators before generation | PDF/chart JSON are not yet image inputs |
| “Find an index with a similar historical shape” | Stored OHLCV candidates are scored and ranked; missing local histories are hydrated from a compatible connected broker within a bounded request budget; user must explicitly select the match | Descriptive similarity only; no prediction claim |
| “Trade SPY EMA crossover and show it” | Typed canonical Strategy → compile/register → Quick Backtest with exact completed bars and risk → Paper runner with live feed → virtual target → durable Paper OMS | Quick Backtest currently one instrument; live Paper uses broker capabilities |
| “Trade a SPY/QQQ pair and show normalized lines/spread” | Typed canonical Strategy locks both reviewed instruments → synchronized multi-series Quick Backtest → owns both live feeds → kernel can draw both → both targets survive coalescing → each leg passes risk/authenticated IPC/Paper OMS → durable positions appear in the Console | Legs route separately, not atomically; sparse historical boundaries fail closed |
| Restart after Paper fills | Existing durable Paper ledger restores orders, fills, positions, cash, equity, P&amp;L, risk, and reconciliation UI | Real broker account recovery is not implemented |

## Quick Backtest to Paper handoff closure — 2026-09-05

The canonical backtest and Paper runtime previously existed as two independent Catalog actions. A
successful Quick Backtest ended at its result window; opening the Paper runner required navigating
back to the Catalog and re-entering parameters. That allowed the Paper run to differ silently from
the parameter set the user had just tested.

The native Mac workflow now keeps the exact successful run boundary:

```text
canonical strategy + reviewed instruments + tested parameters
  -> successful risk-gated Quick Backtest
  -> Run tested strategy in Paper
  -> same StrategyKernelRegistration
  -> normalized tested parameters loaded into Paper Strategy Runner
  -> explicit user Start
  -> Paper book risk evaluated again
  -> authenticated Paper OMS
```

The handoff is available only for a successful canonical SDK backtest. Starting another backtest
invalidates the prior handoff, failed/cancelled runs do not expose it, legacy strategies do not claim
canonical handoff, and an already-running Paper strategy cannot be replaced underneath its feed and
OMS route. The receipt is a desktop transition aid, not authorization: Paper account risk remains the
execution authority and is evaluated again before each order.

## Why the completed-bar gap existed

The strategy calculation was never the missing algorithm. `IStrategyKernel.OnBarAsync` could already
calculate an EMA and call `IVirtualBook.SetTargetPosition`. The old Quick Backtest path accepted broker
bars, projected each OHLC bar into four synthetic legacy ticks, and invoked only
`IBacktestStrategy.OnTickAsync`; it discarded the fact that a completed broker bar had occurred. A
canonical bar-only kernel therefore had no callback, no runtime data view, and no route from its
virtual target to the backtest order router. The implementation now preserves the bar event and adds
the adapter/risk/routing seam. This is exactly within the durable workflow objective; the objective
was not wrong, its front-to-middle composition was incomplete.

## Why a bar-only strategy could calculate a target but still fail end to end

`OnBarAsync` and `SetTargetPosition` answer only two questions: “did the strategy receive this bar?”
and “what position does it want?” A runnable product must still answer who owns the subscription,
which canonical instrument the target belongs to, whether every pair leg has a current price, how
multiple snapshots survive coalescing, where risk is checked, who sends the command, how fills alter
the shared Paper account, and what is restored after restart. The earlier single-asset host answered
those questions for one instrument only. It therefore could execute an EMA target but rejected a
valid pair specification before launch or could have overwritten one leg in its single pending slot.

The live Paper path now closes that host gap:

```text
reviewed pair specification
  -> feed lease for every instrument
  -> serialized multi-instrument SDK runtime
  -> per-leg model target and committed snapshot
  -> per-instrument replication queue (no overwritten leg)
  -> risk + authenticated IPC + durable Paper OMS for each leg
  -> two venue acknowledgements/fills
  -> shared ledger positions/cash/equity/P&L
```

This does not claim atomic pair execution. If leg A fills and leg B is later rejected, the durable
ledger truthfully exposes that leg risk for strategy/risk/reconciliation handling.

## Canonical multi-instrument Quick Backtest closure — 2026-09-04

The Catalog Quick Backtest path no longer rejects every canonical strategy with more than one
reviewed instrument. The implementation closes the engine-level gap rather than merely removing the
UI guard:

- `BacktestBarSeries` carries canonical instrument, broker contract, bar size, history, tick size,
  and contract multiplier for every leg.
- `BacktestTickSource` validates each series and performs a streaming deterministic N-way merge by
  UTC timestamp, payload kind, canonical instrument, and contract symbol. It does not build a second
  five-times-larger event collection.
- `BacktestSession` groups equal timestamps and gives an instrument-aware strategy one batch. All
  completed bars at that exact boundary can be preloaded together, while no later timestamp is
  visible.
- `SimulatedOrderBook.OnTick(contract, tick)` evaluates only working orders for that contract. This
  prevents a SPY order from filling against a QQQ/BTC/other-leg price.
- `SdkStrategyBacktestAdapter` now owns per-instrument replay history, last references, desired
  targets, working orders, and filled positions. It drains every target emitted in one callback
  instead of retaining one last basket leg.
- `TradeLedger` now has shared cash and independent FIFO lots/marks/multipliers per contract. Results
  expose fill/trade symbols and ending positions.
- `RiskManager` can apply the correct multiplier per replay symbol instead of borrowing one global
  multiplier for every leg.
- Quick Backtest fetches every reviewed instrument from the selected broker, shows the entire asset
  set, reports common/union UTC coverage, and stops before execution if histories are not perfectly
  aligned. It neither forward-fills a missing bar nor evaluates a stale pair leg by default.
- The Catalog now offers Quick Backtest for supported canonical pair/basket strategies, and the
  result table identifies which symbol produced each closed trade.

Focused evidence:

- `SdkStrategyBacktestAdapterTests`: 5/5 passed, including same-boundary visibility, two-leg target
  routing, contract-scoped fills, independent ending positions, and sparse-series no-forward-fill.
- `CanonicalQuickBacktestTests`: 3/3 passed, including two broker history requests, one multi-series
  config, visible coverage, and fail-closed missing-boundary behavior.
- Full Avalonia test project: 146/146 passed.
- Full headless project: 1004 passed, 6 skipped, 2 pre-existing `/var` reparse-path fixture failures;
  no Backtest, SDK, Risk, or routing test failed.

The resulting canonical pair path is now:

```text
reviewed SPY + QQQ specification
  -> Catalog Quick Backtest
  -> fetch both completed-bar histories
  -> verify identical UTC boundary coverage
  -> streaming timestamp merge
  -> preload both bars at one boundary
  -> kernel calculates both targets
  -> per-instrument target reconciliation
  -> shared risk gate
  -> contract-scoped simulated fills
  -> shared cash + independent lots/positions/equity
  -> symbol-attributed result rows
```

This remains a backtest, not durable Paper execution. After review, the same canonical kernel's live
path continues through the separately implemented feed leases, runtime, authenticated Paper OMS,
SQLite ledger, and Execution Console.

## Canonical runnable-strategy authoring closure — 2026-09-04

The Strategy Builder previously had two different meanings of “implementation.” A confirmed
strategy could enter the four research lanes (Python, declarative, TradeIR, and CSP), while the
canonical authored-unit source path was reachable only for Visualizers. Consequently, a reviewed
strategy could be semantically confirmed without producing a runnable SDK `IStrategyKernel`.

The native Mac path now closes that specific product gap:

```text
confirmed strategy intent
  -> Build runnable Paper strategy
  -> exact intent-bound AuthoredUnitSpecificationV1
  -> generated IStrategyKernel source
  -> policy + Roslyn + runtime/data/draw verification
  -> code-review consent
  -> canonical strategy registry
  -> Catalog / Quick Backtest / Paper Strategy Runner
```

Concrete changes:

- `AuthoredUnitSpecificationV1` carries the complete `ConfirmedStrategyIntentV1`, not merely the
  shorter classification label. Visualizers must not carry it; runnable strategies must.
- The specification generator rejects a missing, invented, reclassified, or otherwise changed
  confirmed intent. The source generator names that exact intent as executable semantic authority.
- The native Build screen now separates the primary runnable action from “Explore 4 implementation
  formats,” which remains non-runnable research.
- Position, multi-leg, and portfolio targets can enter the canonical Paper strategy path. Signal-
  only output remains display/alert behavior; quote-set, execution-schedule, and extension shapes
  remain blocked until the SDK has a truthful output contract for them.
- The compiler rejects a unit that declares Bars/L1/TradeTape/Depth but inherits the interface's
  no-op callback instead of implementing the corresponding callback (`DAXU211`).
- The compiler runs a bounded start → deterministic declared-data callbacks → draw → stop probe.
  It rejects a blank initial frame, blank post-data frame, or a frame whose value-level fingerprint
  never changes after data (`DAXU210`, `DAXU212`, `DAXU213`).
- The source-generation prompt now explicitly requires each declared callback, bounded state, and
  a data-responsive rendered frame.
- The authored-unit compile failure message now says “authored unit,” rather than incorrectly
  describing a failed Strategy as a Visualizer.

What the generic compiler probe deliberately does not claim: arbitrary synthetic data cannot prove
that every legitimate strategy rule must produce a target. Some reviewed rules require a particular
crossing, spread, session, or regime. Exact decision semantics therefore still need typed,
intent-bound executable scenarios and/or the reviewed Quick Backtest evidence; the compiler proves
runtime wiring and data responsiveness, not profitability or universal signal activation.

New executable evidence:

- Authored-unit filtered specification/generation/source/compiler/persistence tests: 35/35 passed.
- Canonical authoring workflow tests: 2/2 passed. The second test performs confirmed intent → source
  generation → real Roslyn compile/runtime probe → review confirmation → registry → instantiate the
  registered kernel → completed SPY five-minute bar → virtual target `+10`.
- Full Avalonia test project: 151/151 passed.
- UI Core test project: 31/31 passed.

## Live Visualizer and chart-search closure — 2026-09-05

The Visualizer path is now proven past compilation and across an application restart:

```text
typed BTC one-minute candle specification
  -> generated IVisualizer source
  -> runtime/data/drawing semantic verification
  -> persisted ordinary plugin
  -> simulated application restart and plugin reload
  -> restored Visualizer Catalog registration
  -> exact Simulated BTC one-minute feed lease
  -> broker-completed bar
  -> visible candle frame update
  -> window close disposes the feed and runtime
```

The compiler no longer accepts a class merely because its manifest claims a candle or EMA layer.
Generated code must open the exact reviewed `LayerId`/`TypeId` scope and emit primitives appropriate
for that layer kind. Missing layers, wrong type IDs, text-only candles, and unreviewed emitted layers
fail compilation with typed diagnostics. Layer IDs distinguish two instances such as EMA 9 and EMA
21 even when both share the same line renderer type.

Persisted authored registrations now carry verification-contract version 2. Older generated
artifacts remain on disk but are withheld from runnable Visualizer/Strategy registries until they are
regenerated, preventing a restart from bypassing the stronger compiler proof.

Historical-pattern search now fills missing local coverage from an already connected broker only
when that broker reports historical bars and has a registered symbol alias. Hydration is sequential,
bounded (40 candidates by default, configurable up to 500), cancellation-aware, and reports attempt,
success, failure, and limit counts. Results remain descriptive similarities, never predictions.

Closure evidence:

- Authored-unit filtered headless suite: 35/35 passed.
- Chart-pattern search suite: 4/4 passed, including empty-cache broker hydration and request-budget
  enforcement.
- UI Core registries: 31/31 passed, including rejection of stale verifier contracts.
- Full Avalonia suite: 151/151 passed, including persisted plugin reload into a real authored window,
  live bar rendering, and feed/runtime disposal.
- Full headless suite: 1016 passed, 6 explicitly skipped, 0 failed when run from `/private/tmp`.
  The default macOS `/var/folders` alias intentionally trips two reparse-point security guards;
  using its canonical path proves those are environment-path fixtures rather than product failures.
