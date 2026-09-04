# Windows-exact scaled execution values

## Goal

Replace decimal/double execution economics with the Windows coefficient-and-scale model before the
SQLite ledger schema is frozen. This task tracks NUM-01 through NUM-10 independently so type
presence is never reported as full numeric migration.

## Substeps

- [x] NUM-01 Port `ScaledQuantity`.
- [x] NUM-02 Port `ScaledPrice`.
- [x] NUM-03 Port `ScaledMoney`.
- [x] NUM-04 Port `ScaledRatio`.
- [x] NUM-05 Port checked alignment, multiplication, addition, narrowing, comparison, and
  midpoint-to-even rounding arithmetic.
- [x] NUM-06 Replace decimal quantity/limit/stop fields in the Mac OMS `OrderTerms` wire model.
- [x] NUM-07 Replace fill and projection quantity, price, average price, and fees with scaled values.
- [x] NUM-08 Convert OMS risk calculations and evidence to scaled types.
- [x] NUM-09 Restrict explicit decimal/double conversion to named legacy/broker boundaries.
- [ ] NUM-10 Match Windows canonical serialization and golden hashes.

### NUM-08 risk migration checklist

- [x] NUM-08.1 Change `RiskDecision` projected quantity/notional to scaled values.
- [x] NUM-08.2 Change every quantity/money limit in `RiskLimits` to scaled values.
- [x] NUM-08.3 Change positions, reservations, buying power, PnL, equity, market price, and contract
  multiplier in `RiskEvaluationContext` to the appropriate scaled types.
- [x] NUM-08.4 Replace decimal reservation addition/subtraction with aligned checked arithmetic.
- [x] NUM-08.5 Replace order-notional multiplication with `Int128` coefficient/scale arithmetic.
- [x] NUM-08.6 Replace loss/drawdown/buying-power comparisons with exact scale-aware comparisons.
- [x] NUM-08.7 Preserve reduce-only and worst-case long/short rules without decimal conversion.
- [x] NUM-08.8 Record scaled evidence and add denial-boundary/overflow tests.

### NUM-09 boundary checklist

- [x] NUM-09.1 Add the explicit `ExecutionNumericBoundary` adapter.
- [x] NUM-09.2 Convert whole scaled quantity to the legacy backtest book only at
  `TradeIrRiskGatewayV1.ToOrderRequest()`.
- [x] NUM-09.3 Convert scaled prices to legacy book doubles only in the same gateway.
- [x] NUM-09.4 Convert `PaperMarketSnapshot` bid/ask/liquidity to scaled values.
- [x] NUM-09.5 Remove temporary scaled-to-decimal conversion from `RiskPolicy`.
- [x] NUM-09.6 Remove temporary decimal working quantity/price from `DeterministicPaperVenue`.
- [x] NUM-09.7 Inventory future UI and broker adapters and require conversion at their outer edge.

Named conversion boundaries after NUM-09:

- `TradeIrRiskGatewayV1.CreateRiskContext()` converts legacy portfolio/book decimal and double
  values into scaled OMS evidence; unrepresentable values carry an explicit non-admissible flag.
- `TradeIrRiskGatewayV1.ToOrderRequest()` converts scaled OMS terms back to the legacy backtest
  book's long/double request only after whole-unit and range validation.
- Future broker adapters must convert SDK decimal/double values at their inbound callback and
  outbound request methods; no broker numeric type may enter Core execution records.
- Future Execution Console formatting is display-only; commands sent back from UI must parse into
  scaled values before constructing Core execution types.
- Future strategy replication must convert target-book values before constructing a canonical
  instruction; it cannot pass portfolio doubles through the OMS.

### NUM-10 wire/hash checklist

- [x] NUM-10.1 Match the exact Windows serialized field set for all four scaled values.
- [x] NUM-10.2 Resolve whether computed `IsValid` is wire data or ignored derived state.
- [x] NUM-10.3 Add Windows-produced golden JSON for quantity, price, money, and ratio.
- [ ] NUM-10.4 Add a Windows-produced canonical command fixture once INSTR-01–09 exists.
- [ ] NUM-10.5 Add a Windows-produced immutable event/hash-chain fixture.
- [x] NUM-10.6 Reject scalar wire-shape changes through frozen golden-byte tests. Command/event
  schema version enforcement remains coupled to NUM-10.4/10.5.

Windows has two scalar JSON profiles: SQLite uses PascalCase in declaration order and IPC uses
camelCase in declaration order. Both serialize computed `IsValid`. Mac canonical hashing sorts the
camelCase keys. All three byte strings are frozen separately; they must never be conflated into one
format. A Mac canonical instruction/submit golden hash is also frozen, but NUM-10.4 remains open
until the same fixture is emitted by the Windows authority, and NUM-10.5 remains open because the
Mac risk-event payload is not yet the Windows `RiskDecisionRecord` shape.

## Windows behavior being preserved

`0.001` is represented as coefficient `1`, scale `3`. Alignment normalizes trailing zeros before
checked multiplication by powers of ten. Intermediate arithmetic uses `Int128`; overflow, scales
above 18, non-integral whole-unit conversion, and narrowing outside `long` fail as values rather
than wrapping. Ratio rounding is midpoint-to-even.

## Blast radius

- `src/linux/Core/TradingTerminal.Core/Execution/ScaledValues.cs`
- Existing Core execution terms, events, projection, risk, and Paper venue as NUM-06–09 proceed
- Focused tests in the existing headless test project

No new project, package, UI, broker, credential, service, or live-order route.

## Verification

- Core build passed with zero warnings and zero errors.
- Focused scaled-value, exact-risk, TradeIR risk/integration, and Paper lifecycle suite passed:
  61/61.
- Tests cover all four value types, maximum scale, exact whole-unit conversion, normalization,
  alignment, checked overflow, checked multiplication, addition/narrowing, positive comparison,
  midpoint-to-even integer ratio rounding, and double quantization rejection/rounding.

## Current exact boundary

NUM-01–09 are functional through command → risk → event → projection → deterministic Paper venue.
Tests prove `0.001` quantity and `100.125` fill price retain coefficient/scale values, successive
fractional fills accumulate instead of replacing prior totals, exact weighted price is reconstructed,
scale-equivalent limits compare equal, and notional overflow denies rather than wraps or dispatches.
Canonical serialization and Windows golden hashes remain NUM-10, so numeric parity is **9/10
numeric substeps**, not complete numeric parity.

## Deferred stop gate

SQLite execution schema work must not begin until NUM-06–10 are complete, because changing numeric
wire representation after durable event storage would require a migration and would invalidate
golden event hashes.
