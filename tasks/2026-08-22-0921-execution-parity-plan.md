# Windows-to-macOS execution parity plan

## Goal

Produce a source-grounded, dependency-ordered implementation plan for bringing the latest Windows
execution behavior to the native macOS/Avalonia repository without implementing or enabling live
execution in this planning pass.

## Plan

- Audit the Windows execution project boundaries and Mac composition boundary.
- Separate portable domain/engine work from Avalonia and macOS-native substitutions.
- Define atomic work packets with prerequisites, acceptance evidence, and stop gates.
- Preserve Paper-first behavior and flag the current repository prohibition on live execution.

## Blast radius

Documentation only: `.omx/plans/daxalgo-windows-to-macos-execution-parity.md` and this task record.
No product source, project, test, repository remote, or release artifact is changed.

## Build filter

Not applicable to a documentation-only planning pass.

## Tests

No product tests run in this pass. Existing evidence and source contracts are cited in the plan;
implementation verification commands are defined per proposed layer.

## Findings

- Windows execution mixes portable OMS behavior with Windows-only target/framework/security code.
- Mac has the headless strategy runtime but does not compose it in the application.
- Mac broker connections are market-data connections and must not be treated as execution readiness.
- Current repository instructions explicitly prohibit introducing a live order-execution path.

## Diff summary

- Added a granular execution-parity implementation plan with milestones E00–E115.
- Added explicit Paper, IPC, broker-paper, live-policy, CI, signing, and release stop conditions.

## Verification

- Confirmed the plan references current Windows execution sources and current Mac composition files.
- Confirmed no product source files were edited by this planning pass.

## Risks/deferred

- Existing user work remains dirty and must be preserved during implementation.
- Real vendor acceptance requires broker paper/demo credentials and external systems.
- Live execution is deferred until the repository owner deliberately changes the current policy.
