# Paper OMS event ledger

## Goal

Implement the Windows OMS lifecycle, immutable order-event chain, deterministic projection, and
in-memory transactional store inside the existing Mac Core execution boundary. This slice remains
Paper-only and does not add a broker, socket, service, UI, or live-order path.

## Plan

1. Extend the existing Mac execution identifiers with the ownership identities used by the OMS.
2. Add the 17-state lifecycle, 25 event kinds, legal edges, and event-source authorization.
3. Bind immutable event drafts and committed events to existing Mac execution commands and risk data.
4. Verify hash chains and rebuild current order state without ambient time or mutable state.
5. Add an atomic in-memory inbox/event/outbox store as the executable OMS foundation.
6. Only after the source slice is complete, build the named Core consumer and run focused tests.

## Blast radius

- `src/linux/Core/TradingTerminal.Core/Execution/ExecutionIdentifiers.cs`
- `src/linux/Core/TradingTerminal.Core/Execution/OrderLifecycle.cs`
- New execution event, projection, and in-memory store source in the same existing directory
- Focused tests added after implementation

No project, solution, package, broker, UI, credential, or release topology changes.

## Build filter

- `src/linux/Core/TradingTerminal.Core/TradingTerminal.Core.csproj`
- Focused execution tests in the existing headless test project

## Tests

- [x] Market submit persists dispatch receipt before acknowledgement and fill.
- [x] Partial fill remains valid before cancel confirmation.
- [x] Replace confirmation adopts new terms before the resulting fill.
- [x] Unknown venue callback fails closed.
- [x] Fill is accepted while cancel is pending.
- [x] Fill is accepted while replace is pending.
- [x] Two immediate submits cannot consume the same stored quote liquidity.
- [x] Stale lease/fencing claims cannot reach submit/cancel/replace dispatch.
- [x] A partial-fill stream and projection survive SQLite close/reopen exactly.

## Findings

- Mac already has validated submit/replace/cancel/query command DTOs, canonical JSON, payload hashing,
  and a pre-dispatch risk policy. The missing boundary begins after those contracts.
- The older six-state `Trading.OrderState` is a broker/UI/backtest projection and must not be expanded
  into the canonical OMS state machine.
- Windows aggregates the immutable OMS stream by client order identity; this implementation preserves
  that behavior while retaining Mac's separate internal `OrderId` on the submit command.

## Diff summary

- Added Windows-style identities, 17-state lifecycle/event gating, immutable hash-chained events,
  deterministic replay projection, atomic in-memory store, OMS submit/cancel/replace/query, Paper
  dispatch receipts, deterministic Paper venue, and queued callback ingestion.
- Completed VENUE-14–20: acknowledgements, fills, cancel/replace confirmations, rejection, and
  expiry callbacks now enter the same immutable OMS chain.
- Preserved the dispatch barrier: `SubmissionRecorded` commits before any queued callback is read.
- Stored Paper quote liquidity is decremented after immediate execution so it cannot be reused by a
  later order.

## Verification

- Core and the existing Infrastructure project build successfully.
- Combined Paper OMS + durable SQLite + lease/fencing slice: 18/18 passed.
- The broader numeric/instruction/risk/Paper/TradeIR suite passed 72/72 before adding the five
  SQLite and three lease cases; final combined closure is recorded in the current task handoff.

## Risks/deferred

- VENUE-21 market-data-to-Paper quote composition, VENUE-22 SQLite restoration, and VENUE-23
  positions/cash accounting remain open.
- SQLite durability, execution leases, reconciliation workflows, strategy replication, and the
  Execution Console are later source slices.
- Live broker execution remains prohibited by `AGENTS.md`.
