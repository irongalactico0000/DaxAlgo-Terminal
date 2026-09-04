# Paper execution desktop client

## Goal

Implement the missing Windows desktop execution-client behavior in the existing Mac UI Core: resync
the durable service outbox into exact order/economic read state, expose submit/cancel/replace and
reconcile commands, pause intake, and implement Kill as reconcile + cancel outstanding orders +
flatten non-zero Paper positions.

## Plan

1. Add a UI-facing Paper execution client and exact snapshot contract in `TradingTerminal.UI.Core`.
2. Rebuild projections only from verified service events and derive positions/cash from the same
   immutable facts.
3. Generate cancel/replace metadata from the current projection and current lease generation.
4. Implement Kill with a host-supplied exact flatten-order factory and verify the final flat state.

## Blast radius

- `src/linux/UI/TradingTerminal.UI.Core/Execution/`
- Focused headless tests using the existing durable Paper runtime.

No Avalonia view, app composition, IPC, broker SDK, credential, project topology, or live-order path
is changed in this slice.

## Build filter

- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`

## Tests

- Focused `PaperExecutionClientTests`: 5/5 passed.
- Complete `TradingTerminal.Tests.Headless.Execution` namespace: 81/81 passed.

## Findings

- Windows `IExecutionClient.KillAsync` first pauses intake, reconciles, cancels outstanding orders,
  waits for those cancellations, then submits exposure-reducing market orders and verifies flatness.
- Windows order Query is not a standalone service mutation; the client reconstructs its read model
  from durable Resync events.
- Mac can preserve this separation using its new versioned service and existing exact reconciliation
  snapshot builder; no separate UI project is required.

## Diff summary

- Added `IPaperExecutionClient`, exact client result/snapshot records, and
  `IPaperExecutionFlattenOrderFactory` in the existing UI Core project.
- Added bounded service Status/Resync paging and strict outbox-sequence validation.
- Rebuilds each order through `OmsOrderProjection.Rebuild`; malformed event chains fail before
  changing the client view.
- Reuses `ExecutionReconciliationSnapshotBuilder` through a read-only client ledger view, so fills,
  positions, and cash come from the same exact immutable evidence as the durable runtime.
- Added submit, cancel, replace, reconcile, intake pause/resume, and Windows-style Kill behavior.
- Kill pauses intake, reconciles, cancels cancellable orders, creates exact reducing flatten orders
  through a host factory, then resyncs and verifies no non-zero position remains.

## Verification

- One filled service order appears as a verified Filled projection, exact fill quantity, exact +2
  position, exact -200 cash, and complete ledger event history.
- Replace uses the current replayed sequence and completes through ReplaceConfirmed.
- Cancel uses the current replayed sequence and completes through CancelConfirmed.
- Kill cancels one working order, flattens a +2 position with an exact reduce-only sell 2 order,
  verifies zero, and leaves intake paused.
- Without a flatten factory, Kill cancels what it can but explicitly fails while the book remains
  non-flat; it never reports a false safe state.
- Lease loss keeps verified orders/economics readable while `LeaseHeld` and admission become false.
- Public context was regenerated: 66 projects, 1,219 files, 215,471 LOC.

## Risks/deferred

- Avalonia formatting, commands, and full-page composition remain the next slice.
- Authenticated macOS IPC remains later; this client initially uses the in-process service engine.
- Live broker execution remains prohibited by `AGENTS.md`.
