# Avalonia Paper Execution Console

## Goal

Expose the already-implemented durable Paper OMS through the existing macOS/Avalonia application with a truthful, operational console. The console must use the same service/client path as strategy Paper execution and must not introduce a live-order route.

## Windows behavior being ported

- Paper/Real mode is unmistakable before any order control.
- The console shows writer/admission health, open orders, fills, positions, cash, and immutable history.
- Submit, cancel, replace, reconcile, intake pause/resume, and kill/flatten are commands against the execution client rather than local UI mutations.
- Kill pauses intake, reconciles, cancels working orders, flattens positions, and verifies flatness.
- Losing the writer lease leaves inspection available but blocks state-changing commands.

## Existing Mac foundation reused

- `PaperExecutionServiceRuntime` owns SQLite recovery, lease/fence, OMS, venue, and reconciliation.
- `PaperExecutionClient` owns verified resync and submit/cancel/replace/reconcile/kill orchestration.
- `IInstrumentRegistry` remains the canonical asset source.
- The existing `TradingTerminal.App.Avalonia` project remains the application/composition destination.

## Implementation packets

1. Add a portable console view model and row projections in `TradingTerminal.UI.Core`.
2. Add an app-owned Paper session that selects an account-isolated Application Support ledger and constructs exact canonical instructions/risk evidence.
3. Add a full Avalonia console window with ticket, safety controls, KPIs, orders, positions, fills, cash, and event history.
4. Add one shell menu entry and preserve all existing shell edits.
5. Add focused view-model, composition, AXAML, and execution regression tests.
6. Render/inspect the actual window surface without weakening the account gate.

## Blast radius

- Existing `TradingTerminal.UI.Core` execution folder.
- Existing `TradingTerminal.App.Avalonia` composition, shell menu, and a new execution window folder.
- Existing UI/Core and Avalonia test projects.
- This task record and generated public context after public API changes.

## Safety boundaries

- Environment is fixed to `SimulatedPaper`.
- Venue/account are fixed to local Paper resources.
- No broker execution adapter is referenced.
- A missing/stale lease fails closed.
- Kill requires an explicit second confirmation action.
- A ledger integrity/recovery failure is surfaced and does not create a replacement trading session silently.

## Verification

### Implemented component behavior

| Component | Concrete Mac behavior now | Remaining Windows behavior |
|---|---|---|
| Desktop Paper composition | Lazily opens one `SimulatedPaper` resource (`paper-simulator` / `local-paper`), acquires a durable SQLite writer lease, restores venue economics, reconciles, and exposes only the versioned execution client | Multiple execution books/accounts and selectable adapters |
| Lease lifetime | Uses a unique lease ID per process generation, a durable monotonic fence, and renews the five-minute grant every minute | Out-of-process owner supervision is not present |
| Restart identity | Recovers the next `paper-client-N` sequence from the ledger and gives every client generation a unique service request/deduplication namespace | Cross-process IPC request identity is not present |
| Manual ticket | Selects a canonical registry instrument and builds exact quantity/mark/limit/stop values, Market/Limit/Stop/StopLimit, Day/GTC/IOC/FOK, reduce-only, canonical instruction, current position, lease/fence, and risk evidence | Broker/account-specific capability filtering is not applicable until adapters exist |
| Submit | UI → `IPaperExecutionClient` → versioned service → lease validation → risk → append-only ledger → deterministic Paper venue → venue callbacks → projection/outbox | No live broker submission |
| Cancel | Requires a selected cancellable order; persists pending cancel and consumes the venue cancellation callback rather than changing the row locally | No live broker cancellation |
| Replace | Rebuilds exact terms, runs fresh risk using existing fill/reservation evidence, and crosses the same OMS/service boundary | No broker-specific native/cancel-replace selection |
| Reconcile | Runs ledger-versus-Paper-venue order/fill/position/cash comparison and blocks on unresolved startup divergence | Console does not yet expose a full reconciliation-case resolution workflow |
| Kill/flatten | Two explicit clicks; pauses intake, reconciles, cancels working orders, constructs reduce-only market flatten orders, submits them through OMS, and verifies flatness | No account-wide live-broker kill route |
| Read models | Shows orders, open count, fills, non-flat positions, cash, exact event cursor, writer/fence state, intake state, and immutable history; registry symbols such as `SPY` are shown instead of numeric IDs | Windows analytics/equity charts and execution-book management remain absent |
| Shell | Adds top-level `Execution Engine` in the same Windows menu position, with a truthful `Paper Execution Console` child and `PAPER EXECUTION ONLY` status chip | No Real mode selector or live readiness indicator |
| Visual surface | Native 1440×900 Avalonia window with full ticket, safety controls, KPIs, and Orders/Positions/Fills/Cash/Immutable-history tabs | No chart panel and no reconciliation-case detail editor |

### Defects found and closed during end-to-end verification

1. Terminal filled orders were not restoring Paper venue positions/cash because recovery was conditioned only on nonterminal startup orders. Runtime recovery now restores whenever durable execution history exists.
2. The desktop used one reusable lease identity. Every process generation now uses a unique lease ID while SQLite advances the fence.
3. Manual `paper-client-N` order IDs restarted at one. The next sequence is now recovered from the ledger.
4. Service request/deduplication IDs restarted with each client and conflicted with durable event keys. Every client generation now has a unique request namespace.
5. Console rows rendered instrument number `1` instead of `SPY`. Rows now resolve canonical IDs through the ticket instrument catalog.
6. The Mac shell hierarchy contract was stale. The menu now matches current Windows placement: `Data → Execution Engine → Settings`.

### Named checks

- `TradingTerminal.Tests.Headless`, execution namespace: **81/81 passed**.
- `TradingTerminal.UI.Core.Tests`: **19/19 passed**. One pre-existing static-timer test failed once in the broad run, passed alone, then the complete suite passed on rerun.
- `TradingTerminal.App.Avalonia.Tests`: **118/118 passed**.
- Focused real desktop composition: submit → fill → dispose → reopen → recovered economics → new resting order → cancel → reduce-only flatten → flat verification: **passed**.
- Focused console/shell/render tests: **4/4 passed**.
- Named App project build: **0 warnings, 0 errors**.
- Actual themed headless render inspected at `/private/tmp/daxalgo-paper-execution-console.png`; it showed three orders, two fills, zero open orders, zero non-flat positions, fence 2, paused intake, and 25 immutable events.
- Regenerated `.claude/context/linux`: **66 projects / 1226 files / 217107 LOC**.

## Diff summary

- Added the portable execution-console view model and exact UI row projections in the existing UI Core project.
- Added the app-lifetime Paper composition, full AXAML console, shell entry, and one-window lifecycle in the existing Avalonia app project.
- Corrected durable venue recovery, restart-safe order/request identity, continuous lease renewal, and canonical-symbol presentation.
- Added unit, shell-contract, headless-render, and real SQLite restart/kill acceptance coverage.

## Deferred boundaries

- Authenticated Unix-domain-socket/Keychain IPC remains the next execution-layer gap.
- Windows execution books, analytics charts, and reconciliation-case resolution UI are not implemented yet.
- IB, cTrader, and Alpaca Mac order adapters remain absent.
- Real-money execution remains prohibited by `AGENTS.md`; no live route, credential path, or authorization bypass was added.
