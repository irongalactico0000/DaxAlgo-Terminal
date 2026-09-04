# Paper venue restart recovery

## Goal

Implement VENUE-22: reconstruct deterministic Paper venue working orders, completed orders, fills,
positions, and cash from the verified OMS ledger after restart, then release new-order admission only
after startup reconciliation proves that venue and ledger truth match.

## Plan

1. Add a fail-closed, atomic `DeterministicPaperVenue.RestoreFromLedger` operation.
2. Restore only orders proven to have crossed the dispatch boundary.
3. Reject uncertain lifecycle states and unprovable stop activation without partially mutating venue state.
4. Rebuild exact fill, position, cash, order-ID, and trade-ID state.
5. Let a successful startup reconciliation explicitly release the SQLite recovery gate.
6. Add restart, continued-fill, duplicate-ID, and ambiguous-state tests.

## Blast radius

- `src/linux/Core/TradingTerminal.Core/Execution/DeterministicPaperVenue.cs`
- `src/linux/Core/TradingTerminal.Core/Execution/OrderEventStore.cs`
- `src/linux/Core/TradingTerminal.Core/Execution/Reconciliation.cs`
- `src/linux/Pipeline/TradingTerminal.Infrastructure/Execution/SqliteOrderEventStore.cs`
- Focused execution tests under `tests/linux/TradingTerminal.Tests.Headless/Execution/`

No live broker adapter, account credential, app shell, project topology, or release artifact changes.

## Build filter

- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`

## Tests

- Reopen a partially filled SQLite order, restore the Paper venue, reconcile, finish the order, and
  submit another order without reusing broker/trade identities.
- Reject zero-fill Stop recovery because activation cannot be proven.
- Reject PendingCancel recovery because dispatch outcome is uncertain.
- Preserve existing SQLite, reconciliation, and Paper OMS behavior.

## Findings

- SQLite already reopens and verifies immutable event streams, but permanently blocks admission when a
  non-terminal order exists because startup recovery cannot be completed explicitly.
- The Paper venue is process-local; after restart it reports no orders/fills/positions/cash, so startup
  reconciliation necessarily fails until venue state is rebuilt.
- `PendingCancel`, `PendingReplace`, `Releasing`, `Unknown`, and `Reconciling` cannot be reconstructed
  safely from ledger state alone because the external outcome may be missing.
- A zero-fill Stop or StopLimit order may have triggered before the crash without that activation being
  represented in the current 25-event contract. Recovery must fail closed rather than reset it silently.

## Diff summary

- Added atomic, resource-bound `DeterministicPaperVenue.RestoreFromLedger` with typed faults.
- Restored working/completed orders, fills, positions, cash, broker sequence, and trade sequence.
- Added a ledger-derived event/dispatch identity epoch for post-restart dedupe safety.
- Added `IExecutionStartupRecoveryGate`; successful startup reconciliation now releases SQLite
  admission only for the matching venue/account/environment.
- Added three focused restart/fail-closed tests.

## Verification

- Named headless test project build: succeeded; 0 errors. Four pre-existing CS1998 warnings remain in
  `Infrastructure/SimulatedBrokerClientTests.cs`.
- Focused `SqliteOrderEventStoreTests`, `ReconciliationEngineTests`, and `PaperOmsLifecycleTests`:
  28/28 passed.

## Risks/deferred

- Durable stop-activation evidence and deterministic local Paper pending cancel/replace recovery were
  completed in the 2026-08-25 recovery slices.
- Real broker recovery remains outside this Paper-only repository invariant.
