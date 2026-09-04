# Strategy target to Paper OMS replication

## Goal

Implement the missing Windows strategy-replication behavior inside the existing Mac sandbox and
execution projects: coalesce committed model-portfolio targets, preserve exact target economics,
validate the bound book/strategy/resource, calculate the remaining OMS delta, and submit through the
same guarded Paper OMS used by manual orders.

## Plan

1. Port the bounded committed-snapshot replicator into `TradingTerminal.Sandbox.Runtime`.
2. Add the Paper execution-book intake that calculates target deltas from durable OMS state.
3. Bind only committed runtime snapshots; rolled-back callbacks must never submit.
4. Prove duplicate coalescing, pending-order blocking, reversal sizing, and end-to-end Paper fills.

## Blast radius

- `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/`
- `src/linux/Pipeline/TradingTerminal.Infrastructure/Execution/`
- Focused sandbox-runtime and headless execution tests.

No live broker adapter, credential, UI, application project topology, or release path is enabled.

## Build filter

- `tests/linux/TradingTerminal.Sandbox.Runtime.Tests/TradingTerminal.Sandbox.Runtime.Tests.csproj`
- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`

## Tests

- `SandboxExecutionReplicatorTests`: 8/8 passed.
- `PaperExecutionBookTargetIntakeTests`: 6/6 passed.
- `CommittedRuntimeSnapshotsAreReplicatedButStrategyNeverReceivesOms`: passed.
- `DurablePaperExecutionBookTargetIntakeTests`: passed.
- Complete `TradingTerminal.Sandbox.Runtime.Tests`: 46/46 passed.
- Focused `PaperMarketDataExecutionBridgeTests`: 4/4 passed.

## Findings

- Windows observes `IModelPortfolioSource.SnapshotChanged`, maps exact model state to a
  `TargetPosition` intent, coalesces duplicates through a one-slot channel, and submits through a
  book-bound intake rather than exposing the OMS to strategy code.
- Mac already exposes the same committed snapshot source from `SandboxStrategyRuntime`; therefore
  the replicator belongs in the existing runtime project and does not require a new project.
- Mac has a durable Paper OMS and exact position projection now, but no target-intake component
  currently calculates position delta or blocks a second order while the first remains working.

## Diff summary

- Added `SandboxExecutionReplicator`, a one-slot coalescing bridge from committed
  `IModelPortfolioSource` snapshots to exact `TargetPosition` intents.
- Added `PaperExecutionBookTargetIntake`, which validates book/strategy/resource ownership,
  reconstructs current position from immutable fills, calculates signed working reservations,
  coalesces an already-converging target, blocks conflicting nonterminal exposure, constructs a
  canonical target instruction, and submits through the existing lease/risk/OMS route.
- Added exact reversal sizing, non-crossing reduce-only classification, fresh-reference-price
  admission, command-rate evidence, and fail-closed identity/resource validation.
- Extended `PaperMarketDataExecutionBridge` to expose the latest accepted exact bid/ask midpoint
  and observation time to the target-intake risk boundary.
- Added a test-only Infrastructure reference to the existing sandbox-runtime test project so the
  strategy route is proven against the real SQLite execution ledger.

## Verification

- Exact +2 target becomes a filled Paper OMS projection.
- Exact +2 to -3 reversal submits sell 5 and converges to -3.
- +2 to flat is marked reduce-only and converges to zero.
- A working order that already projects to the target is coalesced without a second order.
- A conflicting target was originally refused without stacking exposure. Automatic single-order
  cancel-and-replan convergence was completed later in
  `tasks/2026-08-25-0143-paper-target-convergence.md`.
- Wrong book, wrong strategy, stale price, and risk-limit violations fail before Paper dispatch;
  the risk rejection remains ledgered.
- A real `SandboxStrategyRuntime` target is observed only after its account callback commits.
- SQLite reopen recovers exact strategy-created position and cash projections.

## Risks/deferred

- Application-shell and Execution Console composition remain separate steps after the headless
  route is proven.
- Ambiguous or multiple nonterminal orders still fail closed for reconciliation; one conflicting
  Working or PartiallyFilled Paper order now cancels durably before the new delta is submitted.
- Live broker execution remains prohibited by `AGENTS.md`.
