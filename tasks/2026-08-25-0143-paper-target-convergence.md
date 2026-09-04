# Paper strategy target convergence

## Goal

Make an existing nonterminal Paper order converge to a newer single-instrument strategy target
without stacking exposure or requiring the user to cancel it manually.

## Current behavior

- Repeating the same target is coalesced correctly.
- A different target is refused while any order for the instrument remains nonterminal.
- The refusal occurs in both the reusable headless intake and the authenticated desktop intake.

## Required behavior

1. Refresh/reconstruct the authoritative Paper position and active order.
2. Keep a working order when its remaining reservation already reaches the target.
3. Fail closed when multiple active orders or an ambiguous non-cancellable state require reconciliation.
4. Cancel exactly one Working or PartiallyFilled conflicting order through the normal OMS/client route.
5. Re-read position after cancellation so fills received before confirmation are included.
6. Submit only the remaining target delta through the canonical instruction, risk, lease, ledger and Paper venue path.
7. Never create a second order until cancellation is durably confirmed terminal.

## Blast radius

- `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/PaperExecutionBookTargetIntake.cs`
- `src/linux/Shell/TradingTerminal.App.Avalonia/Execution/AuthenticatedPaperExecutionBookTargetIntake.cs`
- Focused Sandbox Runtime and Avalonia application tests.

No live adapter, credential flow, project topology, or real-money route is introduced.

## Verification

- Focused headless target-intake tests: **8/8 passed**.
- Complete Sandbox Runtime suite: **49/49 passed**.
- Focused authenticated desktop-session tests: **2/2 passed**.
- Complete Avalonia application suite: **124/124 passed**.
- Named `TradingTerminal.App.Avalonia.csproj` build: succeeded with **0 errors**. The only warning
  was the existing offline NuGet vulnerability-feed warning (`NU1900`).
- Repository context regenerated successfully: **66 projects, 1,238 files, 220,107 LOC**.
- `git diff --check`: passed.

## Diff summary

- The reusable headless intake now coalesces an already-correct reservation, cancels one conflicting
  Working/PartiallyFilled order through `OrderManagementService.Cancel`, requires its projection to
  become terminal, replays fills/position, and submits only the remaining target delta.
- The authenticated desktop intake performs the same sequence through
  `IPaperExecutionClient.CancelAsync` and a fresh verified IPC snapshot before it constructs another
  canonical request.
- A flat retarget cancels a resting order without creating a replacement order.
- Multiple active orders and ambiguous lifecycle states remain explicit reconciliation failures.
- Added acceptance coverage for both the in-memory/headless route and the real SQLite + authenticated
  Unix-socket desktop route.

## Risks/deferred

- Ambiguous PendingCancel/PendingReplace/Releasing/Unknown/Reconciling states remain reconciliation
  boundaries; this change must not guess their external outcome.
- Multi-asset atomic target convergence remains outside the single-instrument runtime contract.
