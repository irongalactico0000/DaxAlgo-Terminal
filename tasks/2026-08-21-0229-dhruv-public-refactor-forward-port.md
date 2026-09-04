# Dhruv public refactor forward-port

## Goal

Produce a standalone macOS/Avalonia DaxAlgo repository that retains the latest portable Mac
strategy-research work while adopting the product and runtime contracts from Dhruv Sharma's public
Windows `main` at `6d69136`.

## Plan

1. Preserve all original repositories, worktrees, and ZIP archives as read-only donors.
2. Inventory the public changes after `9ca25bc` and classify them as portable core/runtime,
   Windows UI requiring Avalonia adaptation, archival/removal, or not applicable on macOS.
3. Port the smallest coherent dependency slices, updating the Mac solution and generated context.
4. Reconcile the two uncommitted Mac strategy-research workstreams only after the public runtime
   contract is stable.
5. Run focused tests, the headless suite, the named Mac solution build, and context checks.

## Blast radius

High. Expected areas are Core, Infrastructure, MarketData, execution composition, authoring,
packaging, the Avalonia shell, solution routing, and headless tests. Windows WPF source is a contract
reference and must not be copied as Mac UI implementation.

## Build filter

Start with changed project builds and focused tests. Final targets:

- `dotnet build TradingTerminal.Mac.slnx`
- `dotnet test tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj`
- `powershell -File .claude/context/manage-context.ps1 deep-check`

## Tests

- `dotnet test tests/linux/DaxAlgo.Package.Tests/DaxAlgo.Package.Tests.csproj --nologo`
  — package + StrategyBuilder/Extensions/Marketplace bridge tests.
- Repeated with `--no-restore` after resolving the only XML-doc warning — package suite green and no
  compiler warning.
- `dotnet test tests/linux/DaxAlgo.Sdk.Drawing.Tests/DaxAlgo.Sdk.Drawing.Tests.csproj --no-restore --nologo`
  — 42 passed, 0 failed, 0 skipped.
- `dotnet test tests/linux/TradingTerminal.App.Avalonia.Tests/TradingTerminal.App.Avalonia.Tests.csproj --no-build --no-restore --nologo -m:1`
  — 109 passed, 0 failed, 0 skipped, including 4 native render-adapter tests.

## Findings

- Canonical Mac baseline: `6cbd184` (`agent/quant-research-lifecycle-v1`).
- Public contract authority: `6d69136` (`public-upstream/main`).
- `DaxAlgo-Terminal-Mac.zip` has no source file absent from the live Mac repository.
- The newer Pro ZIP adds no source beyond the recovered Pro repository.
- Public ZIPs are older combined Windows/Linux snapshots already superseded by Git history.
- `05e604d` is excluded from the baseline because it adds machine-local submodule URLs only.
- Public Windows was fast-forwarded to `6d69136`; the first functional parity slice is the portable
  open-package boundary used by Extensions and Marketplace.

## Diff summary

- Added `DaxAlgo.Package`, its adversarial/round-trip tests, and Mac solution routing.
- Added StrategyBuilder provenance bridge, Extensions verify gate, and Marketplace handoff
  projection tests; wired Avalonia Strategy Manager to inspect open packages first.
- Ported `TradingTerminal.Sandbox` (visualizer runtime + contexts), Core parameter Instrument/
  GetText/GetEnum APIs, `AuthoredUnitHost`/`Presenter`, Avalonia `AuthoredUnitView`,
  `AuthoredVisualizerSession`, and `IVisualizerRegistry` DI registration.
- Corrected the copied project description to name the actual public extensions.
- Added the portable SDK strategy/visualizer lifecycle, sandbox capability contexts, render-surface
  primitives, drawing routines, 42 headless tests, and Mac solution routing.
- Added a native Avalonia render adapter and headless Skia tests, plus the four-lane platform
  translation protocol in `docs/windows-to-macos-translation-guide.md`.
- Added kind-aware strategy/visualizer catalog cards and routed visualizer cards through the
  Avalonia authored-visualizer session.
- Added `docs/windows-macos-functional-inventory.md` with a broker-by-broker and subsystem-by-subsystem
  Windows-to-Mac status map, evidence boundaries, and dependency-ordered next work.
- Expanded that inventory into broker capability, individual SDK/sandbox/execution contract,
  UI-action, persistence/recovery, security, update/release, and test-boundary matrices. It now
  explicitly separates data connection from order execution and records the Mac header's `LIVE`
  connection-count wording as a safety-relevant UI gap.
- Ported the Windows `TradingTerminal.Sandbox.Portfolio` and `TradingTerminal.Sandbox.Runtime` projects
  to plain `net9.0`, preserving deterministic accounting, faults, pending entries, the bounded
  serialized market-data pump, lifecycle, and virtual-book reconciliation.
- Ported the upstream Paper-only portfolio/runtime tests. The two Windows runtime tests coupled to
  `TradingTerminal.ExecutionUi` remain deferred with the execution slice rather than receiving a fake
  Mac substitute.
- Added `docs/windows-mac-parity.md` as the ordered behavior-level integration map.
- Regenerated `.claude/context/linux` after the SDK slice for 59 projects / 1133 files /
  186835 LOC.

## Verification

- Package suite: PASS (34/34) — open-package adversarial tests plus StrategyBuilder →
  Extensions → Marketplace bridge tests.
- SDK drawing/contract suite: PASS (42/42).
- Avalonia app suite: PASS (109/109), including render adapter parity (4/4).
- AuthoredUnit/catalog suite: PASS (16/16 in UI.Core.Tests).
- Sandbox visualizer suite: PASS (27/27).
- Sandbox portfolio suite: PASS (146/146).
- Sandbox strategy runtime suite: PASS (30/30); execution-replication cases intentionally deferred.
- Avalonia UI + App builds: PASS after AuthoredUnitView / Sandbox wiring.
- Full `TradingTerminal.Mac.slnx` build: PASS (66 projects, 0 errors, 9 warnings). The warnings are
  three NuGet vulnerability-feed availability warnings, two existing DAXQ nullable warnings, and four
  existing simulated-broker test methods declared `async` without `await`.
- Headless regression suite: 828 passed, 6 skipped, 2 failed. Both failures are pre-existing macOS
  path-policy incompatibilities: the tests create artifacts below `/var`, while the security checks
  reject `/var` because macOS exposes it through a reparse/symlink path. The new package, SDK,
  portfolio, runtime, UI Core, sandbox, and Avalonia focused suites remain green.
- Generated context: PASS after the runtime and inventory work (66 projects / 1181 files / 200060 LOC).
- PowerShell `deep-check`: not run because neither `pwsh` nor `powershell` is installed on this Mac
  shell.

## Risks/deferred

- The public refactor archives the backtest engine while the Mac strategy-builder line currently
  depends on it for TradeIR validation; this requires an explicit replacement seam, not deletion.
- Two Mac donor worktrees contain large overlapping uncommitted changes and must be reconciled by
  behavior and tests rather than wholesale copying.
- GitHub push requires a confirmed destination and valid credentials after local verification.
- SDK version is still the Mac baseline `0.2.0-alpha`; portable 0.3 contracts are present, but the
  version bump and analyzer adoption remain deferred until consumers and the Avalonia adapter align.
