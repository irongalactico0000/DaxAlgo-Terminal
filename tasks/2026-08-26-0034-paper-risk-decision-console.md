# Paper risk-decision Console

## Goal

Expose every durable initial and replacement risk decision already recorded by the Paper OMS as an
auditable native Console workflow. A user must be able to see allow/deny, reason code, policy and
limits hashes, exact projected economics, evaluation inputs, and the command payload hash without
reading raw SQLite or generic event names.

## Plan

1. Project immutable risk observations from the verified client event stream.
2. Add portable risk-decision rows and selection state to the existing Console view model.
3. Add a dedicated native Risk Decisions tab with decision and evidence details.
4. Prove accepted, rejected, and replacement decisions plus rendered surface behavior.
5. Run focused and broad regressions, then regenerate context.

## Blast radius

- Existing `TradingTerminal.UI.Core/Execution` snapshot and Console projection.
- Existing Avalonia Paper Execution Console AXAML.
- Existing UI Core and Avalonia app tests.
- Generated macOS context and this task record.

No risk policy mutation, broker SDK, live execution, project topology, credential, repository
remote, or unrelated integrated work is changed.

## Build filter

- `tests/linux/TradingTerminal.UI.Core.Tests/TradingTerminal.UI.Core.Tests.csproj`
- `tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj`
- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`

## Tests

- `PaperExecutionClientTests.Resync_rebuilds_exact_orders_fills_positions_cash_and_event_history`
  proves an accepted submit decision reaches the desktop snapshot with its client order ID, event
  kind, allow code, and exact command payload hash.
- `PaperExecutionClientTests.Client_replace_and_cancel_use_the_latest_verified_order_sequence`
  proves one order exposes both its initial `RiskAccepted` and later `ReplaceRiskAccepted` facts in
  aggregate order.
- `PaperExecutionClientTests.Rejected_submit_immediately_exposes_the_exact_committed_risk_decision`
  proves a denied command returns failure while immediately resynchronizing the durable
  `RiskRejected` event and its applied maximum-order limit.
- `PaperExecutionConsoleViewModelTests.Committed_risk_decision_exposes_policy_hashes_projected_economics_and_inputs`
  proves the portable Console row exposes the policy version, limits hash, command hash, projected
  economics, account state, rate evidence, and applied limits.
- `PaperExecutionConsoleSurfaceTests.Console_declares_every_operational_surface_without_a_live_selector`
  now requires the Risk Decisions tab, both hashes, and immutable-history warning.
- The existing full Console headless render test proves the expanded window loads, measures, and
  renders.

## Findings

- `OrderRiskObservation` already binds a command payload hash, typed `RiskDecision`, policy version,
  limits hash, and exact `RiskEvaluationContext` into immutable RiskAccepted/RiskRejected and
  replacement-risk events.
- SQLite persists that observation atomically with its order event, and authenticated resync already
  carries the full immutable event to the desktop client.
- The current Console shows only the event kind, state, source, and time, so an operator cannot see
  which risk gate admitted or rejected the order or inspect the evidence used.
- The desktop order factory currently constructs a permissive fixed policy. Editing and persisting
  account-specific limits is therefore a separate missing functionality, not something this
  read-only evidence slice should pretend to solve.

## Diff summary

- Added `PaperRiskDecisionSnapshot` to the existing execution-client read model. It projects only
  committed `OrderRiskObservation` values from the already verified outbox; it does not duplicate
  or recalculate risk logic.
- Changed submit and replace client mutations to resynchronize after both success and rejection.
  A durable denial is therefore visible immediately while the original typed denial still returns
  to the caller. A failed resync remains fail-closed and supersedes the mutation result.
- Added `PaperRiskDecisionRow`, selected-decision state, count, and exact evidence formatting to the
  existing portable Console view model.
- Added a native Risk Decisions summary and tab. It distinguishes submit versus replace and shows
  allow/deny, reason code, projected net/gross, policy version, limits hash, command hash, control
  state, position/reservation/account/market/rate inputs, and all limits applied.
- Historical evidence is explicitly read-only and is never recalculated against current settings.

## Verification

- Focused durable client path: 7 passed, 0 failed, 0 skipped.
- Complete UI Core suite: 21 passed, 0 failed, 0 skipped.
- Focused Console surface/render suite: 9 passed, 0 failed, 0 skipped.
- Complete Avalonia app suite: 131 passed, 0 failed, 0 skipped.
- Complete headless suite with canonical `TMPDIR=/private/tmp/daxalgo-risk-console-regression`:
  968 passed, 0 failed, 6 intentional process-worker skips, 974 total.
- The first broad run with the default macOS temporary path reproduced two pre-existing `/var`
  reparse-path failures outside execution. The canonical `/private/tmp` rerun passed completely.
- No live adapter, credential, repository remote, or project topology was added.
- Context regeneration was attempted as required, but this restored shell has neither `powershell`
  nor `pwsh`; the committed generated context therefore remains at fingerprint `be468d77e084` until
  the repository's PowerShell prerequisite is available again.

## Risks/deferred

- Risk-policy editing, durable account-specific configuration, and authorization remain separate.
- This surface reports the decision the OMS actually committed; it does not recalculate history
  using current settings.
- Desktop policy creation remains fixed and permissive. The next risk-functionality slice must add
  durable per-book/account policy configuration and a real kill/control-mode input before this can
  claim Windows operational risk-console parity.
