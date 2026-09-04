# Paper market-data bridge

## Goal

Complete VENUE-21 by routing canonical L1 quotes into the existing deterministic Paper venue and
immediately committing any resulting acknowledgement/fill/cancel callbacks through the canonical
OMS event ledger.

## Plan

1. Preserve bid-side and ask-side quote liquidity independently in `PaperMarketSnapshot`.
2. Add a disposable `TradingTerminal.Sandbox` bridge from `IMarketDataHub.Quotes()` to
   `DeterministicPaperVenue.OnMarket()` and `OrderManagementService.ProcessVenueEvents()`.
3. Reject malformed/non-representable quotes at the explicit legacy-double boundary.
4. Prove fills, side-specific liquidity, invalid-input handling, and disposal with focused tests.

## Blast radius

- Existing Core Paper snapshot/venue source.
- Existing `TradingTerminal.Sandbox` project.
- Existing `TradingTerminal.Sandbox.Tests` project.
- This task record.

No app composition, broker SDK, service, IPC, project topology, credential, or live-order route is
introduced.

## Build filter

- `tests/linux/TradingTerminal.Sandbox.Tests/TradingTerminal.Sandbox.Tests.csproj`
- Existing Paper OMS focused tests in `TradingTerminal.Tests.Headless`.

## Tests

- [x] A waiting limit order moves Working → PartiallyFilled → Filled from two canonical quotes.
- [x] Bid and ask sizes are independent liquidity pools.
- [x] Invalid and duplicate quotes cannot mutate Paper state.
- [x] Disposing the bridge prevents subsequent quotes from reaching the venue.
- [x] Existing Paper OMS lifecycle tests remain green.

## Findings

- VENUE-14–20 are already implemented and tested in the current worktree.
- `PaperMarketSnapshot` currently carries one shared liquidity value although canonical L1 quotes
  carry separate bid and ask sizes. A bridge must not let a buy consume sell-side liquidity or vice
  versa.
- No existing component subscribes the Paper venue to `IMarketDataHub`.

## Diff summary

- Extended `PaperMarketSnapshot` with independent bid/ask available quantities while preserving the
  existing single-quantity constructor for current callers.
- Updated deterministic quote evaluation so buy orders consume ask liquidity and sell orders consume
  bid liquidity.
- Added `PaperMarketDataExecutionBridge`, which owns canonical quote subscriptions, enforces
  per-instrument sequence/time monotonicity, converts prices at an explicit configured scale, feeds
  the Paper venue, and commits queued callbacks through the OMS.
- Added typed diagnostics for malformed, stale, non-representable, source-failed, and OMS-rejected
  quote paths.

## Verification

- Focused bridge tests: **3/3 passed**.
- Complete `TradingTerminal.Sandbox.Tests`: **30/30 passed**.
- Existing `PaperOmsLifecycleTests`: **10/10 passed**.
- Focused build completed with zero warnings/errors. The headless project retains four pre-existing
  `CS1998` warnings in `SimulatedBrokerClientTests`; this slice did not touch those methods.
- Regenerated `.claude/context/linux` successfully: 66 projects, 1,207 files, 210,598 LOC.
- The prescribed PowerShell structural check could not run because neither `pwsh` nor `powershell`
  is installed in this environment; context generation itself completed successfully.

## Risks/deferred

- VENUE-21 is complete at the reusable composition boundary; app/service ownership remains deferred
  until the Paper execution host is composed.
- VENUE-22 durable working-order restoration remains separate.
- Application composition remains Paper-only and deferred until the execution host boundary exists.
- Live order execution remains prohibited by `AGENTS.md`.
