# Execution parity E00–E02

## Goal

Lock the current Windows/Mac comparison baseline, reproduce the Mac build and focused test evidence,
and correct the status bar so market-data connectivity is not described as live order execution.

## Plan

1. Record repository SHAs, branches, toolchain, platform, and dirty-worktree fingerprint.
2. Run the named Mac solution build and focused tests before changing product code.
3. Preserve the user's existing changes in `MainWindow.axaml` and its test project.
4. Change only the broker-count status wording and add a focused markup contract test.
5. Run the narrow test project, named solution build, and context structural check.

## Baseline evidence

- Mac repository: `6cbd18466fb56dff4f729a6bac0b2e1835a7428f`
- Mac branch: `integration/dhruv-public-f7e8431`
- Windows authority: `6d691367774961635337e257de0a75919f948a25`
- Windows branch: `main`
- macOS: `26.2` build `25C56`, `arm64`
- .NET SDK: `9.0.316`
- Dirty-worktree porcelain fingerprint before this slice:
  `c533c07a07885f397f33f71092e00984e2e6efdaee54685a485f8bdbd099e78e`
- Dirty entries before this slice: 126 modified, 35 untracked.
- Existing overlap in `MainWindow.axaml`: 23 added and 4 removed lines, all in the strategy catalog
  region; the status-bar `LIVE` text was unchanged by that diff.
- Existing overlap in the Avalonia test project: one added package reference for
  `Avalonia.Headless.XUnit`; it must be preserved.

## Blast radius

- `src/linux/Shell/TradingTerminal.App.Avalonia/Shell/MainWindow.axaml`
- One focused test file under `tests/linux/TradingTerminal.App.Avalonia.Tests/`
- This task record

No broker connection, execution, credential, persistence, project topology, or repository remote is
changed in this slice.

## Build filter

- `TradingTerminal.Mac.slnx`
- `tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj`

## Tests

- Pre-change named solution build:
  `dotnet build TradingTerminal.Mac.slnx --no-restore` — PASS, 0 errors. The only reported
  advisories were the known NuGet vulnerability-feed availability warnings.
- Pre-change Avalonia application suite — PASS, 109/109 before adding the E02 assertion.
- Post-change focused broker-status assertion — PASS, 1/1.
- Post-change Avalonia application suite:
  - First run: 109/110; the unrelated
    `CandidateRestoreRecoveryTests.Stop_rejects_late_replacement_and_preserves_last_bound_batch_across_restart`
    concurrency test observed one lane in `WaitingForModel` after `Stop`.
  - Isolated rerun of that test: PASS, 1/1.
  - Immediate complete-project rerun: PASS, 110/110.
- Sandboxed VSTest cannot bind its local loopback control socket on this host. Test evidence above
  came from the approved unrestricted VSTest runs with build-server reuse disabled.

## Findings

- Before E02, the bottom status bar bound to `ConnectedBrokerCount` but prefixed it with the literal
  `LIVE`; the current text now explicitly describes market-data connectivity.
- That count describes connected market-data brokers, not order-execution sessions.
- The existing dirty changes in the same AXAML file are spatially separate and can be preserved.

## Diff summary

- Removed the `LIVE` literal from the broker-count status bar.
- Changed the suffix to `market-data brokers connected` while preserving the existing
  `ConnectedBrokerCount` binding.
- Added one structural AXAML regression test proving the status cannot claim live execution.
- Preserved the user's separate strategy-card edits in the same AXAML file and the existing
  `Avalonia.Headless.XUnit` test-project change.

## Verification

- Exact status-bar diff reviewed independently from the pre-existing strategy-card diff.
- Focused assertion passes 1/1.
- Complete Avalonia test project passes 110/110 on immediate rerun.
- No broker connection, execution, credential, persistence, project topology, or repository remote
  changed in E00–E02.

## Risks/deferred

- The worktree contains substantial user-owned changes; this slice will not reformat or regenerate
  unrelated files.
- Separate data/execution capability contracts are E03 and remain deferred until E02 is green.
- Live order execution remains prohibited by `AGENTS.md`.
- The candidate-recovery suite showed one non-reproducible timing failure before passing both in
  isolation and as a complete project. It is recorded as a pre-existing stability risk rather than
  attributed to the static status-text change.
