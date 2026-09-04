# Canonical order instruction parity

## Goal

Make every Mac submit command carry one immutable economic instruction so identity, provenance,
intent, native terms, risk admission, and dispatch cannot silently describe different trades.

## Component substeps and exact status

- [x] **INSTR-01 Trade intent.** Added target/delta mode, signed scaled units, instrument,
  protective stop, profit target, entry limit/stop, exact estimated cost, strategy id, strategy
  note id, and policy version.
- [x] **INSTR-02 Canonical terms.** Added separate canonical side/type/TIF/quantity/limit/stop
  representation and structural validation. Mac intentionally retains positive fractional scaled
  quantity support; the current Windows `CanonicalOrderTerms.Validate()` still calls
  `TryGetWholeUnits()`. This one rule remains a documented Windows-source divergence because the
  numeric parity requirement and existing crypto/Paper path require `0.001` to remain executable.
- [x] **INSTR-03 Canonical instruction.** Added the immutable identity + intent + terms aggregate.
- [x] **INSTR-04 Identity binding.** Submit instructions carry intent, bucket, leg, client order,
  correlation, causation, lease, and fencing identities. Broker/exchange ids remain absent until a
  venue assigns them.
- [x] **INSTR-05 Provenance binding.** Strategy/manual provenance comes from the validated command
  `StrategyId`; note and policy version are supplied by the host mapping context.
- [x] **INSTR-06 Economic assumptions.** Entry condition, protective stop, target, and estimated
  round-trip cost are immutable fields. TradeIR currently binds an explicit zero cost because its
  existing intent contract carries no cost assumption.
- [x] **INSTR-07 Validation.** Rejects invalid identity/classification/quantity/price shape,
  negative cost, incomplete provenance, and exact entry-price mismatch.
- [x] **INSTR-08 Existing command mapping.** TradeIR and deterministic Paper test callers now map
  their existing `ExecutionCommandMetadata` + `OrderTerms` into the canonical instruction before
  constructing `SubmitOrderCommand`.
- [x] **INSTR-09 Economic mismatch rejection.** Submit construction rejects client/routing/terms
  substitution. Risk re-derives target/delta from its current position and denies stale side or
  quantity with `CanonicalInstructionMismatch` before dispatch.

## Concrete Windows behavior now present on Mac

For target position `-3` with current position `+5`, the mapper accepts only a sell quantity of
`8`. A buy, sell `3`, or stale sell `8` evaluated after the position changes is denied. An intent
entry limit encoded as coefficient `100`, scale `1` does not silently match native terms encoded as
coefficient `10`, scale `0`, even though both display as `10`.

## Files

- `src/linux/Core/TradingTerminal.Core/Execution/CanonicalOrderInstruction.cs`
- `src/linux/Core/TradingTerminal.Core/Execution/ExecutionCommands.cs`
- `src/linux/Core/TradingTerminal.Core/Execution/ExecutionIdentifiers.cs`
- `src/linux/Core/TradingTerminal.Core/Execution/RiskPolicy.cs`
- `src/linux/Backtest/TradingTerminal.Backtest.Engine/TradeIr/TradeIrRiskGatewayV1.cs`
- `tests/linux/TradingTerminal.Tests.Headless/Execution/CanonicalOrderInstructionTests.cs`

## Verification

- Eight canonical-instruction tests pass.
- The rebuilt scaled/risk/OMS/Paper/TradeIR focused suite passes **72/72** after the identity and
  projection closure below.
- Frozen Mac canonical hashes exist for one complete instruction and submit command.

## Identity/event/projection closure

- [x] **ID-04 Submit carries the aggregate identity.** `SubmitOrderCommand` requires one validated
  `CanonicalOrderInstruction`; its `OrderIdentity.ClientOrderId` must equal the command client id.
- [x] **ID-05.1 Stream identity.** Like Windows, the durable aggregate key remains the strongly typed
  `ClientOrderId`; it is not replaced with a mutable compound key.
- [x] **ID-05.2 Initial event binding.** The event store rejects a first event unless its submit
  command and canonical instruction both identify the same aggregate.
- [x] **ID-05.3 Projection authority.** `OmsOrderProjection` now explicitly retains the immutable
  canonical instruction and canonical effective terms instead of exposing only the Mac submit DTO.
- [x] **ID-05.4 External identity adoption.** Broker and exchange ids may be assigned once by venue
  evidence; a later different value rejects the candidate projection before the append commits.
- [x] **ID-05.5 Hostile callback proof.** A Paper callback attempting to replace
  `CONTROLLED-1` with `ATTACKER-ORDER-ID` returns `ExternalIdentityChanged`; event sequence,
  broker identity, and filled quantity remain unchanged.
- [x] **PROJ-09 Canonical instruction projection.** Replay validates the instruction, validates its
  command mapping and aggregate identity, initializes external ids from it, and retains canonical
  terms across confirmed replacements.
- [ ] **ID-06 Ambiguous execution string removal.** No order-id parameter in the new Core OMS is a
  raw string. The remaining string provenance fields (`PolicyVersion`, explanatory text) are not
  order identities. A repository-wide consumer migration is still required before this item closes.
- [x] **ID-07 Lease/fence dispatch validation.** Submit, cancel, and replace now validate the
  instruction lease/fence against the current durable generation immediately before dispatch.
- [ ] **ID-08 SQLite/IPC survival.** SQLite survival is now implemented and tested across restart;
  IPC remains later, so the combined gate is still open.
- [ ] **ID-09 Broker boundary translation.** Paper assigns a typed `BrokerOrderId`; real broker
  adapters remain absent and therefore cannot yet satisfy this gate.

## Remaining compatibility gates

- Windows-produced command bytes must be compared against the Mac golden; Mac and Windows identity
  JSON representations currently differ, so this is not yet claimed byte-compatible.
- Windows event hash v2 binds `RiskDecisionRecord`; Mac currently binds its different
  `OrderRiskObservation`. Replacing the event hash before that semantic mismatch is resolved would
  create a falsely labeled Windows-compatible ledger.
- Replace commands still carry replacement terms rather than a replacement canonical instruction;
  the original submit instruction remains immutable, and Windows-style replacement-risk binding is
  a later OMS/RISK substep.
- Durable acquire/renew/validate/release/expiry takeover is now implemented in the existing SQLite
  ledger. Authenticated service-process ownership and UI status remain later IPC/UI gates.
