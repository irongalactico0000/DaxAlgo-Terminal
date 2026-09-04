# Pending Paper command recovery

## Goal

Make a committed Paper cancel or replace survive a process crash without guessing, duplicating an
external action, or permanently blocking the account. Record whether the command crossed the local
venue boundary, replay an unrecorded committed intent only against a newly constructed deterministic
Paper venue, finish the callback through the normal OMS event path, and keep genuinely unknown
outcomes fail-closed.

## Plan

1. Add hash-bound cancel/replace dispatch-receipt facts without invalidating existing v1 event hashes.
2. Record the receipt after the Paper dispatcher returns and before its callback queue is consumed.
3. Restore PendingCancel and PendingReplace by applying the committed intent to the pristine local
   Paper venue, then queue the ordinary CancelConfirmed or ReplaceConfirmed callback.
4. Drain recovered callbacks before startup reconciliation and new-order admission.
5. Prove crashes before receipt persistence, after receipt persistence, and genuinely unknown
   dispatch outcomes independently.

## Blast radius

- Existing execution events, lifecycle, projection, OMS, deterministic Paper venue, and Paper runtime.
- Focused headless execution/restart tests.
- Generated public context after the public event surface changes.

No broker SDK, live execution route, credential flow, new project, repository remote, or unrelated
user work is changed.

## Build filter

- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`
- `src/linux/Shell/TradingTerminal.App.Avalonia/TradingTerminal.App.Avalonia.csproj`

## Tests

- `SqliteOrderEventStoreTests`: 19 passed, 0 failed, 0 skipped.
- Complete `TradingTerminal.Tests.Headless.Execution` namespace: 96 passed, 0 failed, 0 skipped.
- Complete headless project with a physical `/private/tmp` root: 963 passed, 0 failed, 6 skipped.
- `TradingTerminal.App.Avalonia.Tests`: 124 passed, 0 failed, 0 skipped.
- Named `TradingTerminal.App.Avalonia.csproj` build: 0 errors; one existing `NU1900` warning because
  the restricted environment cannot query NuGet vulnerability metadata.

## Findings

- `CancelRequested` and `ReplaceRequested` are already durable before dispatch, but the successful
  cancel/replace dispatch receipt is not represented in the event chain.
- The deterministic Paper venue is process-local and reconstructed only from the ledger. Replaying a
  committed intent on a pristine venue is therefore safe; applying that rule to a real broker would
  be unsafe without a broker snapshot.
- `OutcomeUnknown` already records genuinely uncertain dispatches and must remain admission-blocking.

## Diff summary

- Added `CancelDispatchRecorded` and `ReplaceDispatchRecorded` as state-preserving PendingCancel /
  PendingReplace facts authorized only from the OMS command boundary.
- Added the exact `ExecutionDispatchReceipt` to immutable order events and their hash payload. Events
  without a receipt retain the v1 hash payload; new receipt facts use the v2 payload.
- `OrderManagementService` now commits the dispatch receipt before draining cancel/replace callbacks.
- `DeterministicPaperVenue.RestoreFromLedger()` now reconstructs committed pending commands:
  - a request without a receipt is applied once to the new pristine local venue;
  - a request with a receipt reconstructs the already-crossed venue action;
  - both queue the ordinary cancel/replace callback rather than mutating the ledger directly;
  - `OutcomeUnknown` and other unsafe lifecycle states still reject recovery.
- `PaperExecutionServiceRuntime` drains recovered callbacks through the OMS before startup
  reconciliation and admission.
- Added separate cancel and replace crash tests for pre-receipt, post-receipt, and explicit-unknown
  outcomes.

## Verification

- Pre-receipt crash injection proves the ledger ends at PendingCancel/PendingReplace with no receipt,
  then restart replays the committed intent, appends its normal confirmation, reconciles, and opens
  admission.
- Post-receipt tests prove the receipt survives SQLite restart, remains hash-bound, and the missing
  callback completes before admission. Mutating only the stored dispatch-attempt identity produces
  `EventHashMismatch`.
- Explicit unknown tests prove restart remains `UnsafeLifecycleState` and admission stays closed.
- Existing stop activation, partial-fill, lifecycle, service, IPC, desktop, and application tests
  remain green.
- `git diff --check` passes for tracked files in this slice; direct trailing-whitespace inspection
  passes for the untracked execution source set.
- Regenerated `.claude/context/linux`: 66 projects, 1,238 files, 220,720 LOC, fingerprint
  `bdc5faf3fa7e`.

## Risks/deferred

- Real-broker pending-command recovery remains adapter-specific and must query broker truth.
- An independently supervised execution process remains a separate desktop deployment gap.
