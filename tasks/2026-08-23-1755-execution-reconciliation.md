# Execution reconciliation

## Goal

Implement the Windows Paper-OMS broker-versus-ledger reconciliation behavior inside the existing
Mac Core and Infrastructure projects. This slice compares immutable local projections with one
point-in-time adapter snapshot, persists discrepancies and resolutions, and blocks new exposure
while material truth differences remain unresolved. It introduces no live order route.

## Component behavior and substeps

### Snapshot contracts

- [x] RECON-01.1 Define one execution-resource-bound snapshot envelope.
- [x] RECON-01.2 Capture order identity, canonical terms, lifecycle state, and cumulative fill.
- [x] RECON-02.1 Capture individual fills with exact quantity, price, fee, and time.
- [x] RECON-03.1 Capture exact instrument positions.
- [x] RECON-04.1 Capture exact total and available cash by currency.
- [x] RECON-04.2 Reject invalid, stale, duplicate, or internally inconsistent snapshots.

### Identity matching and comparison

- [x] RECON-05.1 Match orders by client order ID first.
- [x] RECON-05.2 Cross-check broker and exchange IDs and reject ambiguous reuse.
- [x] RECON-05.3 Match fills by immutable trade ID and parent order identity.
- [x] RECON-06 Detect broker-only orders/fills as locally missing.
- [x] RECON-07 Detect ledger-only dispatched orders/fills as broker missing.
- [x] RECON-08.1 Detect exact order quantity, fill quantity, price, and lifecycle mismatches.
- [x] RECON-08.2 Do not classify pre-dispatch Draft/Validated/Prepared/Armed orders as broker missing.
- [x] RECON-09 Detect exact position mismatch by instrument.
- [x] RECON-10 Detect exact cash mismatch by currency.

### Durable cases and admission

- [x] RECON-11.1 Define immutable case facts and an append-only case-store seam.
- [x] RECON-11.2 Implement deterministic in-memory case storage.
- [x] RECON-11.3 Add append-only SQLite reconciliation-case storage.
- [x] RECON-11.4 Verify case sequences and evidence during SQLite startup integrity checks.
- [x] RECON-12.1 Block new exposure when a material case is open.
- [x] RECON-12.2 Fail closed when snapshot validation or case persistence fails.
- [x] RECON-12.3 Keep cancellation outside the exposure-admission gate.
- [x] RECON-13.1 Append operator resolution identity, time, and evidence without rewriting the opening fact.
- [x] RECON-13.2 Automatically resolve a prior discrepancy only when a later valid snapshot clears it.
- [x] RECON-14.1 Support explicit Startup and Reconnect trigger values.
- [ ] RECON-14.2 Compose automatic startup/reconnect invocation in the future execution host.

## Acceptance tests

- Clean snapshot leaves admission open.
- Broker-only and ledger-only orders and fills open typed cases.
- Changed native IDs, order terms, fill economics, position, cash, or lifecycle open typed cases.
- Pre-dispatch local orders do not create false broker-missing cases.
- Invalid/stale/duplicate snapshots fail closed.
- Material cases survive SQLite reopen and continue blocking admission.
- Operator resolution appends a new fact and preserves the original observation.
- A later valid matching snapshot appends resolution and reopens admission.

## Blast radius

- `src/linux/Core/TradingTerminal.Core/Execution/`
- `src/linux/Pipeline/TradingTerminal.Infrastructure/Execution/`
- `tests/linux/TradingTerminal.Tests.Headless/Execution/`
- This task record

No shell, broker SDK, credential, project topology, or live-execution path is changed.

## Build filter

- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`

## Verification

- `ReconciliationEngineTests`: 10/10 pass.
- The tests cover clean parity, typed order/fill/position/cash discrepancies, automatic and
  operator resolution, stale/duplicate snapshot rejection, OMS exposure gating with cancellation
  still available, SQLite restart/tamper behavior, real Paper venue-versus-ledger comparison, and
  snapshot-acquisition failure.
- The reconciliation coordinator has explicit Startup and Reconnect entry points, but it is not yet
  invoked by an application execution host because that host does not exist on Mac.

## Deferred

- Real IB/cTrader/Alpaca snapshot acquisition remains part of their future paper/demo adapters.
- Automatic startup/reconnect invocation remains dependent on the Paper execution host composition.
- UI case inspection and resolution remain part of the Execution Console slice.
