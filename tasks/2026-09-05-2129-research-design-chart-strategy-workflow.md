# Research, design, chart, and strategy workflow

## Goal

Define the concrete native macOS Strategy Builder workflow that turns a user brief or chart reference
into research evidence, an optional visual design, a runnable visualizer or strategy, a backtest, and
Paper execution. The chart must be an interactive projection of the same typed strategy definition,
not an unrelated preview.

## Plan

1. Interpret the supplied Dolpago article as a research workflow rather than copying its claims as a
   trading strategy.
2. Map the proposed workflow onto current authored-unit, research-case, chart-rendering, backtest, and
   Paper execution seams.
3. Specify screen layout, controls, state transitions, invalidation rules, data contracts, and
   acceptance criteria.
4. Preserve the existing integrated project topology and prohibit real-money routing.

## Blast radius

Documentation only:

- `.omx/plans/daxalgo-research-design-chart-strategy-workflow.md`
- this task record

No source, project, test, broker, credential, release, or execution configuration is changed.

## Build filter

Not applicable to this documentation-only planning pass.

## Tests

No product tests are run. The plan defines the focused contract, view-model, rendering, integration,
and end-to-end tests required by implementation.

## Findings

- Current intake already supports text, chart references, image inspection, and historical pattern
  selection.
- Current strategy confirmation already carries objective, hypothesis, evidence, falsifiers,
  unresolved decisions, and seven semantic stages.
- The current authored rendering surface supports drawing and hover/pressed cursor state, but not
  host-owned selection, brushing, annotations, hit-testing, or edit events.
- Current navigation has only `Design` and `Build`; retained research runs explicitly are not created
  from the chart or authoring chat.
- Therefore the central missing capability is a shared, versioned workspace state plus host-owned
  chart interactions that update research/design/strategy drafts and invalidate downstream proof.

## Diff summary

- Added a difference-first product and engineering plan for optional Research, optional Design,
  interactive chart editing, compilation, backtest, and Paper handoff.
- Added a control inventory and a Dolpago-style event-study example.

## Verification

- Referenced source paths and line-level contracts were inspected on branch
  `integration/dhruv-public-f7e8431` at `f9c6bcf`.
- Confirmed the only pre-existing untracked path is `.omc/`; it was not touched.

## Risks/deferred

- Research results must remain hypotheses until historical and out-of-sample gates pass.
- Full-depth/trade research depends on broker capability and retained event-level data; bars are not
  a substitute.
- Interactive authoring must be implemented in host UI contracts, not by exposing Avalonia controls
  to untrusted authored code.
- Real-money order routing remains out of scope and prohibited by repository policy.
