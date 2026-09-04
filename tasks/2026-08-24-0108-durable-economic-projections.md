# Durable Paper position and cash projections

## Goal

Complete VENUE-23 / SQL-08.2 by materializing exact Paper position and cash projections in SQLite
inside the same transaction as each accepted fill, while retaining immutable fills as replay truth.

## Plan

1. Add version-2 position and cash projection tables and a v1→v2 migration.
2. Backfill v1 ledgers from immutable fill events during the migration transaction.
3. Update both projections atomically when a fill event is appended.
4. Expose typed position/cash reads for future service and UI composition.
5. Verify materialized rows against independent ledger replay during startup integrity checks.
6. Add exact buy/sell/fractional, restart, migration, tamper, and rollback tests.

## Blast radius

- Existing execution schema/store under `TradingTerminal.Infrastructure/Execution`.
- Existing headless SQLite execution tests.
- This task record.

No project topology, shell, broker SDK, credential, IPC, or live-order route changes.

## Build filter

- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`

## Tests

- Exact fractional buy/sell netting for position and cash.
- Close/reopen durability and continued post-restart fills.
- Transactional v1→v2 migration/backfill from immutable fill events.
- Position-row tamper detection against independent replay.
- Injected failure after fill/position/cash writes but before commit.

## Findings

- Fill facts are durable and positions/cash can be replayed, but no dedicated materialized economic
  rows currently exist.
- Schema version 1 therefore needs an explicit transactional migration rather than silently changing
  its declared layout.

## Diff summary

- Added schema version 2 with resource-scoped position and cash projection tables.
- Added transactional v1→v2 migration and immutable-fill backfill.
- Added atomic fill→position/cash projection updates.
- Added typed `ReadPositionProjections` and `ReadCashProjections` APIs.
- Added startup verification that rejects materialized economic rows differing from fill replay.

## Verification

- Named headless project build succeeded with 0 errors; four pre-existing CS1998 warnings remain in
  `Infrastructure/SimulatedBrokerClientTests.cs`.
- `SqliteOrderEventStoreTests`: 12/12 passed.
- Combined SQLite, reconciliation, Paper OMS lifecycle, and lease regression filter: 35/35 passed.

## Risks/deferred

- Application Support ledger path isolation (SQL-16) remains separate.
- Backup artifact creation before future destructive migrations remains separate; the v1→v2 migration
  itself is transactional and replay-backed.
- Execution Console consumption remains a later UI slice.
