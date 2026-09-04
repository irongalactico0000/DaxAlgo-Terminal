# Paper execution-quality Console

## Goal

Port the Windows Execution Console's execution-quality component into the existing Mac Paper
Console using only verified immutable ledger facts: order count, filled count, rejects, cancels,
reconciliation cases, unknown outcomes, fill/reject rates, and acknowledgement latency.

## Windows authority

- `src/windows/Execution/TradingTerminal.ExecutionUi/ExecutionReadModels.cs` defines
  `ExecutionQualityReadModel` and its fill-rate, reject-rate, slippage, and acknowledgement values.
- `InProcessExecutionClient.BuildExecutionQuality()` derives counts from projections and immutable
  events and deliberately reports slippage as unavailable when no arrival-price benchmark exists.
- `ExecutionConsoleView.xaml` renders fill rate, average slippage, rejects, average acknowledgement,
  orders, cancels, reconciliation cases, and unknown outcomes.

## Mac gap before this slice

The Paper Console exposes orders, fills, positions, cash, risk decisions, reconciliation, and raw
events, but it does not summarize execution quality. Users must manually count ledger facts and
cannot see acknowledgement latency.

## Plan

1. Derive one immutable quality snapshot from verified projections, events, and cases.
2. Add a portable quality row/surface to the existing Console view model.
3. Render the Windows-equivalent quality fields without inventing slippage.
4. Prove filled/rejected/cancelled/unknown and acknowledgement calculations.
5. Run focused and broad regressions and update context evidence.

## Blast radius

- Existing Paper execution client snapshot.
- Existing portable Paper Console view model.
- Existing native Avalonia Paper Console.
- Focused client/UI/surface tests and generated context.

No execution mutation, broker SDK, credential, live route, project topology, repository remote, or
unrelated integrated work is changed.

## Tests

- The existing accepted-submit client test now proves one order, one fill, 100% fill rate, one
  acknowledgement observation, zero rejects, and no invented slippage.
- The existing replace/cancel client test now proves two orders, one filled order, and one durable
  cancel request.
- The rejected-submit client test now proves one order, one rejection, and a 100% reject rate.
- `PaperExecutionConsoleViewModelTests.Execution_quality_exposes_verified_rates_counts_latency_and_truthful_slippage`
  proves all formatted rates/counts, average acknowledgement, and `n/a` slippage behavior.
- The Console surface contract requires the quality tab and eight Windows-equivalent labels.
- The existing full Console headless render test covers the expanded native surface.

## Diff summary

- Added `PaperExecutionQualitySnapshot` to the existing client read model with Windows-equivalent
  fill/reject/average fields.
- Derived order/fill/reject/unknown counts from verified projections, cancels from immutable
  `CancelRequested` facts, reconciliation count from synchronized case facts, and acknowledgement
  latency from ordered `SubmissionRecorded` → `VenueAcknowledged` event timestamps.
- Added a portable `PaperExecutionQualityRow` and a native Execution Quality tab showing fill rate,
  average slippage, reject rate, average acknowledgement, orders, cancels, reconciliation cases,
  unknown outcomes, filled/rejected counts, and provenance.
- Kept slippage at `n/a` because the ledger does not record an arrival-price benchmark; fill prices
  are not misrepresented as slippage.

## Verification

- Focused client path: 7 passed, 0 failed, 0 skipped.
- Complete UI Core suite: 22 passed, 0 failed, 0 skipped.
- Focused Console surface/render suite: 9 passed, 0 failed, 0 skipped.
- Complete Avalonia app suite: 131 passed, 0 failed, 0 skipped.
- Complete headless suite with canonical `/private/tmp`: 968 passed, 0 failed, 6 intentional
  process-worker skips, 974 total.
- `git diff --check` and a direct trailing-whitespace scan passed for this slice.
- Context regeneration remains unavailable in this restored shell because neither `powershell` nor
  `pwsh` is installed; generated context remains at fingerprint `be468d77e084`.

## Risks/deferred

- Slippage remains `n/a` until an immutable arrival/reference-price benchmark is recorded.
- Portfolio equity/P&L/Sharpe/drawdown analytics require an explicit opening-equity and realized-P&L
  accounting contract; this slice will not fabricate them from Paper cash flow.
