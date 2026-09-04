# Durable Paper stop-activation recovery

## Goal

Make zero-fill Stop and StopLimit orders restart-safe by recording whether Paper trigger monitoring
started and whether the stop actually activated. Preserve fail-closed behavior for legacy streams
that lack this evidence and for cancel/replace commands whose external outcome is uncertain.

## Plan

1. Add state-preserving stop-monitoring and stop-activation facts to the Paper callback and OMS event
   contracts.
2. Project those facts into an explicit activation state and include them in the immutable hash chain.
3. Emit monitoring evidence after submit/replace acknowledgement and activation evidence before any
   fill.
4. Restore inactive and activated zero-fill orders exactly; reject legacy ambiguous streams.
5. Add restart tests for inactive Stop, activated-zero-fill StopLimit, legacy ambiguity, and both
   PendingCancel/PendingReplace uncertainty.

## Blast radius

- `src/linux/Core/TradingTerminal.Core/Execution/OrderLifecycle.cs`
- `src/linux/Core/TradingTerminal.Core/Execution/OrderProjection.cs`
- `src/linux/Core/TradingTerminal.Core/Execution/PaperExecutionDispatch.cs`
- `src/linux/Core/TradingTerminal.Core/Execution/OrderManagementService.cs`
- `src/linux/Core/TradingTerminal.Core/Execution/DeterministicPaperVenue.cs`
- Focused headless execution tests.

No broker SDK, live execution route, credential flow, project topology, or unrelated user work is
changed.

## Build filter

- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`
- `src/linux/Pipeline/TradingTerminal.Infrastructure/TradingTerminal.Infrastructure.csproj`

## Tests

- `SqliteOrderEventStoreTests`: 15 passed in this slice before the subsequent pending-command tests.
- `PaperOmsLifecycleTests`: 11 passed.
- Complete execution namespace at this milestone: 92 passed.
- Complete headless project: 959 passed, 6 skipped, 0 failed with a physical `/private/tmp` root.
- `TradingTerminal.App.Avalonia.Tests`: 124 passed.
- Named application build: 0 errors; one environmental `NU1900` warning.

## Findings

- The Paper venue currently holds `StopActivated` only in memory.
- Recovery infers activation from a non-zero fill, which cannot represent an activated StopLimit that
  has not reached its limit price.
- Treating every zero-fill stop as inactive would silently mis-restore existing ledgers, so new
  monitoring evidence is required to distinguish new inactive orders from legacy ambiguity.
- Pending cancel and replace cannot be guessed from the OMS ledger alone after a crash; both remain
  explicit admission-blocking recovery outcomes.

## Diff summary

- Added durable `StopMonitoringStarted` and `StopActivated` Paper callback/event facts.
- Added a stop-activation projector which distinguishes NotApplicable, legacy Unknown, Monitoring,
  and Activated without changing the stored order-projection schema.
- The Paper venue records monitoring after submit/replace acknowledgement and activation before any
  fill, then restores that exact state from the immutable event chain.
- Inactive zero-fill stops, activated zero-fill stop-limits, and legacy ambiguous stops now have
  separate recovery behavior and tests.

## Verification

- An inactive restored Stop remains below its trigger and fills only after crossing it.
- An activated zero-fill StopLimit fills after restart without retriggering, even after price moves
  back below its original stop.
- A legacy zero-fill stop lacking monitoring evidence remains fail-closed.
- Pending cancel/replace uncertainty was explicitly tested here and was completed by the subsequent
  `2026-08-25-0836-pending-paper-command-recovery.md` slice.

## Risks/deferred

- Real-broker stop state and pending-command recovery require broker snapshot/reconciliation evidence
  and remain outside this Paper-only slice. Deterministic local Paper pending-command recovery is now
  implemented separately.
