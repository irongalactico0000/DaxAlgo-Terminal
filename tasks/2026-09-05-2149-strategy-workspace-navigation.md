# Strategy Workspace, chart research, validation, and Paper handoff

## Goal

Implement a coherent first vertical slice of the chart-centered AI strategy workflow: one
versioned, hash-bound Strategy Workspace; truthful Brief → Research → Design → Build → Validate →
Paper navigation; host-owned event selection; leakage-safe feature experiments; persisted depth
replay; and an exact historical-validation receipt before a Paper-only handoff.

## Plan

1. Add platform-neutral workspace, dataset, experiment, and validation-evidence contracts with
   canonical hashes and downstream invalidation.
2. Persist/restore safe authoring artifacts while clearing executable validation authority that
   cannot be re-established after restart.
3. Add host-owned observation/outcome range selection and B/C/N labels without exposing Avalonia
   controls to authored code.
4. Extract bars, L1, trades, and depth from observation windows only; fit transforms on the
   chronological training partition; bind evidence into generation as a hypothesis.
5. Merge persisted depth into replay and bind successful historical backtests to the exact
   specification, build, feature set, parameters, window, and selected Paper book.
6. Run focused tests, the named Mac solution build, and generated-context checks.

## Blast radius

- `TradingTerminal.Core` strategy-generation and validation-evidence contracts.
- Market-data store replay, deterministic merge ordering, and the Infrastructure research runner.
- Charts host selection UI and the Settings authoring session/view-model.
- Code-generation request envelopes, Quick Backtest receipts, shell orchestration, and Paper runner
  book identity.
- Focused Core/headless and Avalonia authoring/backtest tests plus generated context metadata.

No broker transport, real-order route, execution ledger, credential, repository topology, or
unrelated user-owned file is changed.

## Build filter

- `tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`
- `tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj`
- `TradingTerminal.Mac.slnx`

## Tests

- PASS — focused headless workspace/dataset/research/store suite: 14/14.
- PASS — focused Avalonia Builder/session/generation/chart/backtest suite: 31/31.
- PASS — `dotnet build tests/linux/TradingTerminal.App.Avalonia.Tests/...`: 0 errors.
- PASS — `dotnet build TradingTerminal.Mac.slnx --no-restore --disable-build-servers -m:1`: 0 errors.
- PASS — generated macOS context regeneration and `gen-context-linux.sh --check`.
- PASS — `git diff --check`.

## Findings

- The source article describes human-labeled pre-event windows, trade/order-book feature capture,
  normalization, and repeated statistical simulation; it does not justify presenting hypotheses as
  proven machine-learning output.
- The current Mac builder persists candidate, research, intent, chart-reference, and authored-unit
  artifacts separately, but has no aggregate revision binding them together.
- The current window exposes only Design and Build navigation, so Research, Validate, and Paper
  readiness cannot be represented truthfully.
- The existing Quick Backtest could launch Paper but its receipt did not identify the originating
  Strategy Workspace, specification, compiled source, feature set, or tested parameter set.
- Persisted quotes/trades/bars already had replay paths; depth required inclusion in the store feed
  merge. Research now fails closed when a declared observation stream is absent instead of
  manufacturing zero-valued features.

## Diff summary

- Added versioned canonical workspace, research dataset, chronological experiment, and historical
  validation-evidence contracts.
- Added six-stage Builder navigation with safe session migration/invalidation.
- Added chart observation/outcome brushing, B/C/N labeling, and dataset authoring.
- Added deterministic observation-only feature extraction for bars/L1/trades/L2 with train-only
  normalization and explicitly non-promotional evidence.
- Added persisted depth replay and stable quote/trade/depth/bar merge ordering.
- Bound research evidence into candidate/specification generation as a hypothesis.
- Added an exact-revision Quick Backtest receipt and selected-book Paper-only handoff; stale results
  are discarded and synthetic TradeIR smoke cannot unlock Paper.

## Verification

- Focused headless command completed with 14 passed, 0 failed.
- Focused Avalonia command completed with 31 passed, 0 failed.
- Named solution build completed with 0 errors and four pre-existing warnings (two offline NuGet
  audit warnings and two nullable warnings in `DaxqIlLowerer`).
- Generated context reports 66 projects, 1,290 files, and 239,016 LOC and reproduces cleanly.
- No commit, push, external write, credential change, or real-money route was created.

## Risks/deferred

- Daily top-volume universe scheduling, broker-wide historical data acquisition, richer statistical
  selection, and the Windows reference's C++ optimizer remain later slices.
- Quick Backtest intentionally rejects strategies requiring L2 replay; the research store supports
  depth, but the historical execution engine does not yet simulate an L2 matching model.
- Research evidence remains exploratory and is never labeled as a historical backtest.
- Historical-validation authority and tested parameter objects are intentionally session-scoped;
  restart clears them until the exact compiled artifact is revalidated.
- Real-money execution remains prohibited; Paper is the only execution destination in scope.
