# Paper execution service boundary

## Goal

Implement the missing Windows execution-service behavior inside the existing Mac Core project: a
versioned, lease-fenced, request-correlated API over the durable Paper OMS. The service must expose
status, submit, cancel, replace, query, reconciliation, and outbox resync without introducing IPC,
broker SDKs, or live order execution.

## Plan

1. Add the versioned request/response/event contract and exact kind-specific payload rules.
2. Add a service engine that validates protocol, account, lease/fencing, and payload before OMS use.
3. Add request-id replay/conflict handling and a bounded outbox event batch.
4. Prove submit/cancel/replace/query/reconcile/resync behavior and all fail-closed boundaries.

## Blast radius

- `src/linux/Core/TradingTerminal.Core/Execution/`
- `tests/linux/TradingTerminal.Tests.Headless/Execution/`
- Generated macOS context after public API changes.

No Avalonia composition, local socket, Keychain secret, real broker adapter, credential, project
topology, or live execution route is changed.

## Build filter

- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`

## Tests

- Focused `ExecutionServiceEngineTests`: 7/7 passed.
- Focused service engine plus durable runtime tests: 9/9 passed.
- Complete `TradingTerminal.Tests.Headless.Execution` namespace: 76/76 passed.

## Findings

- Windows service protocol version 1 exposes Status, Submit, Cancel, Replace, Reconcile, and Resync.
- Windows Query and Kill are execution-client behaviors, not distinct service request kinds: order
  state is read from resynced projections, while Kill enumerates cancellable orders and sends normal
  Cancel requests. Mac will preserve that separation instead of inventing an incompatible wire kind.
- Mac already has the required durable outbox, canonical commands, OMS, lease validator, and
  reconciliation engine. The missing component is the bounded, versioned service boundary.

## Diff summary

- Added protocol version 1 request/response/event contracts for Status, Submit, Cancel, Replace,
  Reconcile, and Resync.
- Added an exact payload-shape validator, resource validator, lease/fencing validator, typed fault
  mapping, and a bounded 256-event outbox exchange.
- Added serialized request handling with a bounded 4,096-request replay cache: exact replay returns
  the original exchange and conflicting reuse fails without mutation.
- Added `PaperExecutionServiceReconciliationRunner`, which captures local immutable-ledger truth and
  deterministic Paper venue truth before invoking the existing reconciliation engine.
- Added `PaperExecutionServiceRuntime` in the existing Infrastructure project. It opens SQLite,
  verifies integrity, acquires one durable lease generation, restores Paper venue state, completes
  startup reconciliation, exposes the service, renews ownership, and releases it on disposal.
- Added nine focused behavior tests, including durable reopen of filled order, exact position/cash,
  outbox resync, lease renewal, and successor fencing generation.

## Verification

- A service submit follows the existing OMS and returns DraftCreated through FillReceived facts.
- Working orders replace and cancel through the same OMS and Paper callback path.
- Protocol mismatch, wrong resource, malformed payload, stale fence, missing reconciliation, and
  conflicting request replay are separately classified and create no order mutation.
- Status reports lease loss while Resync remains read-only and available.
- SQLite reopen returns the same filled order, exact +2 position, exact -200 SIM cash, verified
  ledger integrity, and durable event history.
- Lease renewal retains its fencing token; clean release lets a successor acquire a strictly newer
  token.
- Public context was regenerated: 66 projects, 1,217 files, 214,538 LOC.

## Risks/deferred

- Authenticated Unix-domain-socket transport and Keychain secret storage remain a later IPC slice.
- The Avalonia execution client/console and its client-side Kill orchestration remain later slices.
- Live broker execution remains prohibited by `AGENTS.md`.
