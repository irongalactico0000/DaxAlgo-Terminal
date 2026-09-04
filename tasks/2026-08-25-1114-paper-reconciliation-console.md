# Paper reconciliation control plane and Console

## Goal

Expose the already durable Paper reconciliation engine as an operational workflow. The authenticated
execution service, client snapshot, and native Avalonia Console must list current and historical
reconciliation facts, visibly block new exposure for unresolved material cases, and allow an operator
to record an explicit durable resolution with evidence.

## Plan

1. Extend the existing versioned execution-service contract with typed case-list and case-resolution
   requests and responses.
2. Validate resource, writer lease, fencing token, case identity, operator identity, and resolution
   evidence before writing a resolution fact.
3. Carry reconciliation facts and admission state through the existing authenticated Unix-domain
   socket into `PaperExecutionClientSnapshot`.
4. Add case rows, evidence detail, blocking status, and an explicit resolution action to the existing
   Paper Execution Console.
5. Prove persistence, restart, IPC, stale-fence rejection, and UI behavior with named focused and
   broad test targets.

## Blast radius

- Existing `TradingTerminal.Core/Execution` service and reconciliation contracts.
- Existing execution SQLite/service runtime and Unix-socket serialization only as required.
- Existing `TradingTerminal.UI.Core/Execution` client and Console projection.
- Existing Avalonia Paper Console AXAML.
- Existing headless and Avalonia execution tests.
- Generated macOS context and this task record.

No broker SDK, market-data client, live execution route, project topology, repository remote, or
unrelated Dhruv-owned source is changed.

## Build filter

- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`
- `tests/linux/TradingTerminal.UI.Core.Tests/TradingTerminal.UI.Core.Tests.csproj`
- `tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj`
- `src/linux/Shell/TradingTerminal.App.Avalonia/TradingTerminal.App.Avalonia.csproj`

## Tests

- Focused service/client/IPC reconciliation set: 23 passed, 0 failed, 0 skipped.
- Complete `TradingTerminal.Tests.Headless.Execution` namespace: 100 passed, 0 failed, 0 skipped.
- Complete `TradingTerminal.UI.Core.Tests`: 20 passed, 0 failed, 0 skipped.
- Focused Paper Console surface/render set: 9 passed, 0 failed, 0 skipped.
- Complete `TradingTerminal.App.Avalonia.Tests`: 131 passed, 0 failed, 0 skipped.
- Complete `TradingTerminal.Tests.Headless`: 967 passed, 0 failed, 6 intentionally skipped.

## Findings

- `ReconciliationEngine` and `SqliteOrderEventStore` already persist append-only open/resolved case
  facts and enforce monotonic case history.
- `ExecutionServiceRequestKind.Reconcile` runs a comparison but the current response exposes only a
  success/failure message; no case collection crosses the service boundary.
- `PaperExecutionClientSnapshot` therefore cannot project unresolved-case admission state, and the
  Console has no case/evidence/resolution surface.

## Diff summary

- Advanced the execution-service wire contract to protocol version 2 and added typed, bounded
  reconciliation-case list and resolution operations.
- Made every accepted service envelope carry a serializable lease identity and fencing token;
  direct in-process reads can no longer create a request that authenticated IPC would reject.
- Added deterministic case-id cursor paging with at most 128 facts per exchange. Case pages do not
  retransmit unrelated order-event outbox frames.
- Added lease/fence/resource/payload validation before resolution and retained exact request-id
  replay semantics so one resolution request appends at most one immutable fact.
- Extended the Paper reconciliation runner with explicit case-store ownership, latest-fact reads,
  admission state, and resource-bound resolution.
- Carried case frames through the authenticated Unix socket with count, ordering, cursor, resource,
  and validity checks on the client endpoint.
- Added client-side paged case synchronization, visible admission blocking, local submit refusal,
  and durable operator resolution followed by verified refresh.
- Added Console case rows, local-versus-venue evidence, resolution history, operator/evidence
  inputs, an explicit append action, and a visible reconciliation safety state.
- Added service paging/fencing/replay tests, SQLite close/reopen persistence proof, authenticated IPC
  evidence/resolution proof, portable view-model behavior, and rendered Avalonia surface coverage.

## Verification

- A 130-case service fixture returns 128 ordered facts plus a cursor, followed by the remaining two;
  both pages contain zero unrelated order-event frames.
- A stale fencing token cannot resolve a case. The current owner appends one resolution fact, and an
  exact request replay returns the original exchange without a third fact.
- The desktop client observes an open material case, closes `AdmissionOpen`, refuses submit, appends
  operator evidence, reopens admission, disposes, reopens the same SQLite ledger under a successor
  lease, and recovers the same resolved fact.
- The authenticated Unix socket transports both opening and resolution evidence and preserves the
  resource/cursor/count contract.
- The portable Console disables submit while blocked, exposes both evidence sides, invokes the
  resolution client with the selected case, and re-enables commands only after a resolved snapshot.
- The native AXAML includes the Reconciliation tab, local and venue evidence labels, immutable
  resolution warning, and append action; the complete window renders headlessly.
- Broad regression evidence: execution 100/100, UI Core 20/20, Avalonia app 131/131, and complete
  headless 967 passed with six pre-existing process-worker skips.
- Regenerated `.claude/context/linux`: 66 projects, 1,242 files, 222,703 LOC, fingerprint
  `be468d77e084`.

## Risks/deferred

- This slice resolves local deterministic Paper reconciliation cases only. Broker-native account
  snapshots remain unavailable until each Paper/demo execution adapter is implemented.
- Resolution records operator evidence; it must not delete or rewrite the original mismatch fact.
- A separately supervised execution host remains a deployment gap; the current desktop composition
  uses the authenticated socket boundary but owns its Paper host lifetime in the application.
