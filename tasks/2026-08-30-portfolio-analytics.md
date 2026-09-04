# Paper portfolio analytics parity

## Goal

Implement the Windows Execution Console's missing account/performance behavior in the existing
macOS Paper execution path: persisted opening capital, fill-derived realized P&L, marked equity and
exposure, time-range metrics, and a truthful native Console surface.

## Plan

1. Persist and validate an opening balance on every Paper execution book.
2. Derive FIFO closed-trade P&L and open-lot cost from immutable ledger fills.
3. Value current positions from the active Paper mark, falling back explicitly to the latest fill.
4. Calculate 7D/30D/90D/YTD return, drawdown, Sharpe, win rate, and trade counts.
5. Surface the results in the existing Avalonia Paper Console and book manager.
6. Verify pure accounting, restart persistence, view-model projection, and rendered AXAML contracts.

## Blast radius

- Existing Paper execution book configuration and desktop composition.
- Existing UI Core Paper client/read model and Console view model.
- Existing Avalonia Paper book/Console views.
- Focused UI Core, App Avalonia, and headless execution tests.

No new project is created. No broker adapter or live-order route is introduced.

## Evidence boundary

- Fills are durable immutable ledger facts.
- Opening balance is explicit operator configuration persisted in `books.json`.
- Current Paper marks are used when present; the latest durable fill is the declared fallback.
- Metrics are denominated in Paper `SIM` units. No FX conversion or broker account statement is
  claimed.

## Verification

Pending.

## Deferred

- Broker-native account balances and broker reconciliation.
- Cross-currency conversion and derivative contract multipliers.
- User-configurable risk-limit policy and independent execution-host process.
