# Persistent Paper execution books

## Goal

Replace the Mac desktop's hard-coded `local-paper` account and strategy-book constants with
persistent, explicitly selected Paper execution books. The manual Execution Console and Paper
Strategy Runner must bind to the same selected book, account-isolated ledger, writer lease, and
intake state while preserving the existing authenticated IPC and Paper-only safety boundary.

## Plan

1. Port the Windows execution-book configuration behavior into the existing Mac UI/application
   projects without adding project topology.
2. Add versioned, atomic JSON persistence with a migration-compatible default local Paper book.
3. Add app-lifetime book/session management and book-scoped resources, ledgers, and runtime leases.
4. Add native Avalonia book management and shell status surfaces.
5. Bind both operational execution windows to an explicit selected book and prove isolation,
   restart restoration, pause state, and active-session safety.

## Blast radius

- Existing `TradingTerminal.UI.Core/Execution` portable read models where appropriate.
- Existing `TradingTerminal.App.Avalonia/Execution`, composition, and shell surfaces.
- Existing UI Core and Avalonia application tests.
- Generated macOS context and this task record.

No live broker adapter, broker credential, real account, Windows source, project topology, or
repository remote is changed.

## Build filter

- `tests/linux/TradingTerminal.UI.Core.Tests/TradingTerminal.UI.Core.Tests.csproj`
- `tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj`
- `src/linux/Shell/TradingTerminal.App.Avalonia/TradingTerminal.App.Avalonia.csproj`

## Tests

- `PaperExecutionBooksTests`: 5/5 passed. Covers atomic JSON persistence, fail-closed invalid
  configuration, selected-account restart, paused-intake restart, independent ledgers/resources,
  independent orders/positions, active-window deletion refusal, exposure deletion refusal, and
  ledger retention.
- Full `TradingTerminal.App.Avalonia.Tests`: 131/131 passed.

## Findings

- The desktop composition is currently one singleton `PaperExecutionDesktopSession` fixed to
  `paper-simulator/local-paper` and one ledger path.
- The strategy runner separately hard-codes `desktop-paper-strategy-book`, so the book identity is
  not currently shared with the manual console.
- Windows persists execution-book intent separately from its immutable order ledger and restores
  paused intake on restart. That separation fits the existing Mac topology.

## Diff summary

- Added versioned `PaperExecutionBookDefinition`/catalog persistence under Application Support.
- Added an app-lifetime manager that creates one isolated `ExecutionResource`, SQLite ledger,
  authenticated local IPC session, and writer lease per lazily opened Paper book.
- Added create/select/rename/run-stop/remove behavior with unique account IDs and fail-closed
  deletion while a book is in use or has working orders/non-flat exposure.
- Bound newly opened manual Consoles and Strategy Runners to the explicitly selected book instead
  of the former hard-coded `local-paper`/`desktop-paper-strategy-book` constants.
- Added a native Avalonia Paper Execution Books window and shell status/entry point.
- Preserved deleted-book ledgers instead of deleting execution evidence.

## Verification

- Named Avalonia application build passed with zero errors. The only warning was NU1900 because the
  restricted environment could not query NuGet vulnerability metadata.
- Focused book tests passed 5/5.
- Full Avalonia application regression suite passed 131/131.
- `git diff --check` passed for this slice's tracked files.

## Risks/deferred

- Execution remains local deterministic Paper only.
- Real broker account discovery and order adapters remain separate milestones.
- Existing windows remain bound to the book they opened; selecting another book affects newly
  opened windows. This prevents a running order surface from silently changing accounts.
