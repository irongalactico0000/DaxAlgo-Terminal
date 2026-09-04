# Durable Paper execution ledger

## Goal

Make the existing Mac Paper OMS retain exactly the same accepted event stream and projection after
process restart. This slice is implemented in the existing `TradingTerminal.Infrastructure` project;
it adds no project, UI, broker, service, IPC, or live-order route.

## Windows behavior decomposed into executable substeps

- [x] **SQL-01.1 Dedicated database ownership.** Reject a SQLite file owned by another application
  or containing unrelated tables.
- [x] **SQL-01.2 Forward schema version.** Set and verify `application_id` and `user_version`.
- [x] **SQL-03.1 Event envelope.** Persist aggregate, sequence, lifecycle, source, dedupe, times,
  causation, previous hash, event hash, external ids, and canonical payload.
- [x] **SQL-03.2 Append-only enforcement.** SQLite triggers reject event update and delete.
- [x] **SQL-04 Projection.** Materialize the replayed current projection with last sequence/hash.
- [x] **SQL-05 Inbox.** Persist `(source, dedupe key)` so exact replay survives restart and a
  conflicting duplicate fails.
- [x] **SQL-06 Outbox.** Allocate a monotonic publication sequence in the same transaction.
- [x] **SQL-07 Risk evidence.** Persist every initial and replacement risk decision by order/event.
- [x] **SQL-08.1 Fill facts.** Persist exact fill quantity, price, and fee payloads.
- [x] **SQL-09 Lease/fencing facts.** Preserve every per-resource owner generation, renewal,
  expiry, and release; never overwrite or reuse an old fencing token.
- [x] **SQL-10 Reconciliation facts.** Persist append-only discrepancy observations and resolution
  facts, verify their sequence and immutable evidence at startup, and fail integrity on tampering.
- [x] **SQL-11 Connection durability.** Require foreign keys, 5-second busy timeout,
  `synchronous=FULL`, and WAL for file-backed ledgers.
- [x] **SQL-12 Atomic append.** Validate candidate replay, then commit inbox + event + projection +
  risk/fill + outbox together.
- [x] **SQL-13 Startup verification.** Run SQLite integrity and foreign-key checks, replay every event chain, compare
  the materialized projection, and verify inbox/outbox linkage.
- [x] **SQL-14 Fail closed.** Integrity failure prevents new-order admission and exposes a typed
  diagnostic; no broker dispatch is attempted.
- [ ] **SQL-15 Backup/migration.** Still required after the initial schema is proven.
- [ ] **SQL-16 Mac path isolation.** Still required for app composition; injected temporary paths
  are used in this slice.

## Acceptance scenarios

- Submit and partially fill, dispose the store, reopen it, and recover identical event count,
  sequence, hashes, external ids, exact fill economics, and projection.
- Replay the same dedupe key after restart and return the original event without a second outbox row.
- Reuse a dedupe key with different payload after restart and reject it.
- Mutate a persisted event payload/hash or materialized projection and fail startup integrity/admission.
- Inject a failure before commit and observe either the old complete state or the new complete state,
  never a partial inbox/event/projection/outbox set.

## Blast radius

- Core: only the minimum rehydration/friend boundary required by the durable implementation.
- Infrastructure: new execution SQLite schema/store under the existing project.
- Tests: focused durable-ledger tests in the existing headless test project.

## Verification

- Existing `TradingTerminal.Infrastructure` project builds with zero warnings/errors.
- Five durable-ledger behavior tests pass:
  1. partial fractional fill survives close/reopen with identical canonical event bytes and projection;
  2. exact inbox replay and conflicting duplicate behavior survive restart;
  3. injected pre-commit failure rolls back inbox/event/projection/outbox together;
  4. tampered materialized projection produces `ProjectionMismatch` and blocks admission;
  5. application id, schema version, WAL, and append-only trigger are enforced.
- Restart testing exposed and fixed a `RiskEvaluationContext` JSON-constructor mismatch that the
  former in-memory-only path could not reveal.

## Deferred

SQL-08.2 dedicated materialized position/cash projection tables remain open. Position and cash are
currently rebuilt exactly from the immutable fill ledger for reconciliation, but are not stored as
independent durable projections. SQL-15 backup/migration, SQL-16 production Application Support path selection,
service/IPC, and live broker execution remain separate gates.
