# DaxAlgo Mac research, design, chart, and strategy workflow

## Outcome

The Strategy Builder becomes one continuous, evidence-preserving workflow:

```text
brief / chart image / selected chart range
  -> classify the requested product
  -> optional Research
  -> optional visual Design
  -> confirm one typed strategy meaning
  -> generate and verify runnable code
  -> interactive historical preview
  -> backtest
  -> explicit Paper-book handoff
  -> live chart + Paper orders + positions + P&L
```

Research and Design are optional stages, not separate disconnected applications. The chart remains
visible throughout and projects the same versioned workspace definition used by generation,
backtesting, and Paper execution.

This plan does not enable real-money broker orders.

## Why the supplied article changes the product requirement

The article does not describe a conventional indicator strategy. It describes a human-guided event
study:

- collect live order-book and trade-print data;
- rank the daily universe by volume and operate on roughly 100–200 active instruments;
- manually identify pre-shooting and pre-crash examples;
- capture engineered values at a point or over a preceding window;
- normalize values so one method can be compared across instruments;
- select useful features and search candidate conditions through repeated simulation;
- vary order behavior according to market conditions.

DaxAlgo must not turn those statements directly into a profitable-strategy claim. It should let the
user express, inspect, label, test, falsify, and eventually implement that research idea. The product
therefore needs both a normal indicator path and an event-discovery path.

## Existing evidence and exact gap

| Capability | Current Mac evidence | Exact product gap |
|---|---|---|
| Visualizer vs strategy classification | `AuthoredUnitKindV1` distinguishes `Visualizer` and `Strategy` in `src/linux/Core/TradingTerminal.Core/Strategies/Generation/AuthoredUnitSpecificationV1.cs:9` | The UI does not expose the classification as the first user-reviewable decision. |
| Text or chart intake | Source kinds include text, reference chart, or both at `AuthoredUnitSpecificationV1.cs:16`; current composer and attachment controls are in `StrategyAuthoringWindow.axaml:453` | Intake is chat-centric and not yet one durable workspace with chart selection and research state. |
| Reference meaning | Visual style, layout, chart type, indicators, annotations, market pattern, and related instruments are distinct at `AuthoredUnitSpecificationV1.cs:24` | These meanings are selected at attachment time but are not editable later in a dedicated reference inspector. |
| Similar-chart search | Current UI can search instruments or indexes at `StrategyAuthoringWindow.axaml:511`; real stored/connected-broker history is used by `ChartPatternSearchV1.cs:36` | Results cannot be opened side-by-side, brushed, labeled, or promoted into research examples. |
| Research semantics | `ResearchCaseV1` has objective, hypothesis, evidence, falsifiers, and unresolved/resolved items at `ConfirmedStrategyIntentV1.cs:125`; current form is at `StrategyAuthoringWindow.axaml:2041` | Research is a set of text boxes. There is no dataset definition, event labels, capture window, feature set, normalization, split, run, or result comparison. |
| Strategy semantics | Seven stages—observe, qualify, decide, size, execute, manage, finish—exist at `ConfirmedStrategyIntentV1.cs:37` | Rules are not linked to concrete chart layers, markers, parameters, or research evidence in the UI. |
| Typed chart design | Panes and layers are first-class records at `AuthoredUnitSpecificationV1.cs:140` | There is no visual pane/layer editor or safe pre-compilation preview. |
| Runtime drawing | Both `IVisualizer.Draw` and `IStrategyKernel.Draw` render through `IRenderSurface`; the surface supports semantic layers at `IRenderSurface.cs:126` | The contract is output-oriented. Cursor hover/pressed state exists, but no selection, brushing, click action, annotation mutation, or hit-test identity exists. |
| Host chart input | `RenderSurfaceView` tracks pointer movement and pressed state at `RenderSurfaceView.cs:44` | Host UI does not translate gestures into typed workspace edits. Authored code must not receive Avalonia controls. |
| Research orchestration | `ResearchCoordinator` runs Planner → Evidence Analyst → Critic → Synthesizer → Risk Judge at `ResearchCoordinator.cs:16` | Current native run UI explicitly says chart selection and research chat are not connected at `StrategyAuthoringWindow.axaml:993`. |
| Build/install/live/Paper | The repository already has authored-unit compile/install/catalog/feed and Quick-Backtest-to-Paper paths | The upstream research/design/chart state does not yet drive those existing paths as one immutable version chain. |

## Product model: one workspace, six stages

Do not add mutually exclusive global “Research mode” and “Design mode” toggles. Use a stage rail with
optional stages:

1. **Brief** — always present.
2. **Research** — `Skipped`, `Suggested`, `Required`, `Running`, `Needs review`, or `Approved`.
3. **Design** — `Automatic`, `Customized`, or `Approved`.
4. **Build** — generate, verify, compile, install.
5. **Validate** — historical preview and backtest.
6. **Paper** — select book and run the tested strategy.

The user may revisit an earlier stage. Any material edit creates a new workspace revision and marks
dependent evidence stale instead of silently reusing it.

### When Research is optional or required

| User request | Research stage | Reason |
|---|---|---|
| “Show BTC candles with EMA 9/21” | Skip by default | Pure visualization; no trading claim. |
| “Trade EMA 9/21 crossover” | Suggested but optional | The rule is already explicit; backtest can falsify it. |
| “Find what happens before breakouts and build a strategy” | Required | The rule is unknown and must be discovered from labeled evidence. |
| “Build something like the attached profitable chart” | Required | Appearance is observable, profitability and causality are not. |
| “Show instruments historically similar to this chart” | Research-light | Requires real-history search, but no trading rule. |
| “Use order-book/trade behavior before shooting events” | Required | Needs event labels, tick/depth data, normalization, leakage controls, and out-of-sample validation. |

### When Design is optional

- **Automatic**: the system produces candles plus only those feature/rule layers needed to explain the
  strategy.
- **Customized**: the user changes panes, layers, colors, annotations, or reference-chart matching.
- **Skipped visually**: a headless signal strategy may draw nothing, but the Validate stage still uses
  a host-owned diagnostic chart for inputs, signals, targets, and fills.

## Recommended screen layout

```text
┌─────────────────────────────────────────────────────────────────────────────┐
│ Brief  Research?  Design?  Build  Validate  Paper     revision 12 · saved │
├───────────────┬───────────────────────────────────────┬─────────────────────┤
│ AI / brief    │ INTERACTIVE CHART                     │ CONTEXT INSPECTOR   │
│               │ candles, depth, tape, features,      │ Data                │
│ prompt        │ labels, rules, signals, fills        │ Indicators/features │
│ questions     │                                       │ Rules               │
│ evidence      │ brush/select/click/compare            │ Research            │
│ suggestions   │                                       │ Appearance          │
├───────────────┴───────────────────────────────────────┴─────────────────────┤
│ Data coverage | Event samples | Experiments | Backtests | Logs             │
└─────────────────────────────────────────────────────────────────────────────┘
```

The chart is center stage. Chat explains and proposes; the typed inspector shows what will actually
be saved and run.

## Stage 1 — Brief

### User controls

- `TextBox`: “Describe a chart, research question, or strategy.”
- `Attach chart` button with independently editable intents:
  - copy appearance/layout;
  - identify visible indicators;
  - find similar price histories;
  - find related instruments/indexes;
  - use the image as research context only.
- `Product` segmented control: `Auto`, `Visualizer`, `Strategy`.
- `Instrument scope` selector:
  - one instrument;
  - explicit basket;
  - dynamic universe;
  - pair/multi-leg.
- `Timeframe` control with `tick`, seconds, minutes, hours, daily, and multi-timeframe.
- `Required data` chips: `Bars`, `L1`, `Trades`, `L2`; inferred by default, user-reviewable.
- `Research` selector: `Auto`, `Skip`, `Use research`.
- `Design` selector: `Automatic`, `Customize`.
- primary action: `Interpret request`.

### Result shown to the user

- product: Visualizer or Strategy;
- instruments and broker candidates;
- timeframe and lookback;
- data required and current availability;
- proposed research/design stages;
- material questions that must be answered;
- a plain-language statement of what the system will and will not do.

### Gate

The user confirms the interpreted brief, not generated code. No research or implementation starts if
the instrument is unresolved or the required data cannot be supplied.

## Stage 2 — Research (optional)

Research has two paths.

### A. Evidence review

Use this for papers, text, links, or claims. Controls:

- objective and falsifiable hypothesis;
- evidence cards with source, claim, confidence, and point-in-time availability;
- assumptions list;
- falsifiers/rejection conditions;
- unresolved questions;
- `Approve research case` button bound to an exact content hash.

This reuses the existing `ResearchCaseV1` and coordinator roles.

### B. Market-pattern discovery

Use this for Dolpago-like requests. Controls:

#### Universe panel

- market/exchange/asset filters;
- liquidity ranking basis: same-day notional volume, trade count, spread, or explicit custom rule;
- rank range, for example top 100–200;
- cohort normalization: none, market-cap bucket, price bucket, volatility bucket, or custom;
- session window and timezone;
- survivorship policy and delisted-symbol inclusion;
- data-coverage counter before Run.

#### Outcome label panel

- labels: `Pre-breakout`, `Pre-crash`, `Neutral`, plus user-defined labels;
- outcome horizon, such as next 10 seconds or next 20 trades;
- outcome threshold, such as +1.5% or −1.5%;
- label source: manual, rule-suggested then human-reviewed, or imported;
- class counts and imbalance warning.

#### Chart gestures

- click a timestamp: capture a point-in-time example;
- drag horizontally: define a preceding observation window;
- drag outcome handle: define the future window used only for labeling;
- `B` shortcut: mark pre-breakout;
- `C` shortcut: mark pre-crash;
- `N` shortcut: mark neutral;
- click an event marker: open its raw order-book/trade evidence;
- multi-select samples: compare feature distributions.

The future outcome window must be visually shaded and technically excluded from feature computation.

#### Feature panel

Feature groups must state their raw data dependency:

- Trades: buy/sell imbalance, signed volume, trade intensity, acceleration, inter-trade time,
  aggressor streak, large-trade share.
- L1: spread, spread change, microprice, mid-price velocity.
- L2: depth imbalance by level, slope, concentration, replenishment, cancellation pressure, queue
  depletion.
- Cross-sectional/session: volume rank, volatility rank, market-cap cohort, time since open.
- Conventional indicators remain available but are not automatically inserted when the user asks for
  order-flow-only research.

Each row contains:

- enable checkbox;
- dependency badge (`Trades`, `L1`, `L2`, `Bars`);
- point/window selector;
- aggregation window;
- normalization selector;
- missing-data policy;
- `Show on chart` toggle;
- distribution and leakage warning.

#### Normalization panel

- price: raw, basis points from mid, percent return, tick distance;
- quantity: raw, percentage of visible depth, percentile within instrument/session, cohort z-score;
- rate: per second, per trade, or event-time bucket;
- cross-instrument: per-instrument, cohort, or universe rank;
- winsorization/clipping bounds;
- train-fitted transforms only, applied unchanged to validation/test.

#### Experiment panel

- split: chronological train/validation/test;
- walk-forward controls;
- embargo/purge interval around overlapping events;
- objective metric and constraints;
- search budget and reproducible seed;
- minimum events per class;
- transaction cost/slippage assumptions;
- `Run discovery` and `Stop`;
- progress, elapsed time, event count, and data exclusions.

#### Results panel

Every candidate shows:

- exact feature/rule definition;
- train, validation, and test results separately;
- event counts and coverage;
- stability across instruments, days, and regimes;
- sensitivity to thresholds and costs;
- leakage and multiple-testing checks;
- falsifiers triggered;
- `Overlay candidate`, `Compare`, `Reject`, and `Promote to strategy draft`.

Promotion copies exact evidence IDs, transform IDs, feature IDs, parameter IDs, and candidate hash into
the strategy draft. It never copies a textual summary alone.

## Stage 3 — Design (optional)

Design means “how this typed information is displayed,” not “invent the trading logic.”

### Chart controls

- pane list with add/remove/reorder and height ratio;
- layer list with visibility, lock, reorder, duplicate, and remove;
- layer types: candles, price, volume, indicator line, histogram, scatter, annotation, depth ladder,
  footprint, and tape;
- reference-image opacity and side-by-side comparison;
- semantic theme token, line thickness, marker type, axis, scale, and legend label;
- `Reset to automatic explanation view`;
- `Approve design`.

### Indicator/feature inspector

Selecting an EMA line, custom feature, signal marker, target, order, or fill opens one inspector:

- stable feature/layer ID;
- display name and explanation;
- source data and freshness;
- parameters and allowed range;
- dependent rules;
- dependent research results;
- `Visible` control;
- `Used by strategy` read-only status;
- `Link display parameter to strategy parameter` explicit switch;
- `Edit strategy parameter` action.

### Critical separation

- Hiding a layer changes appearance only.
- Deleting a layer asks whether to remove only the drawing or also the dependent feature/rule.
- Changing a display-only style never invalidates a backtest.
- Changing a linked computation parameter creates a new strategy revision and invalidates compile,
  backtest, and Paper readiness.
- A strategy rule may use a feature that is hidden; the inspector must say so.

## Two-way chart/strategy interaction contract

The present `IRenderSurface` remains a safe output-only SDK. Do not add Avalonia controls or mutable
strategy APIs to authored code. Add host-owned interaction alongside it.

### New conceptual contracts

```text
StrategyWorkspaceRevision
  briefHash
  researchCaseHash?
  datasetDefinitionHash?
  featureDefinitions[]
  parameterDefinitions[]
  semanticRules[]
  chartComposition
  buildArtifactHash?
  backtestEvidenceHash?

ChartSelection
  instrumentId
  timeframe
  start/end event time
  selected layer/feature/rule/event IDs

ChartEditCommand
  AddResearchLabel
  ResizeObservationWindow
  ChangeParameter
  ToggleLayerVisibility
  ReorderLayer
  BindLayerToFeature
  BindRuleToFeature
```

### Stable identity rules

- `FeatureId` identifies the computation.
- `ParameterId` identifies one typed value used by computation or rule.
- `LayerId` identifies a visual projection.
- `RuleId` identifies a strategy decision.
- `EvidenceId` identifies the research support.
- `EventSampleId` identifies a labeled historical event.

One EMA computation can feed one rule and one line layer. The period is one `ParameterId`, not two
independent numbers. Conversely, line color and visibility belong only to the layer.

### Required synchronization examples

1. User changes EMA period from 9 to 12 through the chart inspector.
   - update the shared parameter;
   - recompute the preview;
   - update the rule explanation;
   - mark generated source, backtest, and Paper handoff stale;
   - preserve the previous revision and evidence.
2. User hides the EMA line.
   - chart redraws;
   - rule and backtest remain valid;
   - inspector says “hidden but used by EntryRule-1.”
3. User drags a breakout threshold.
   - host emits a typed `ChangeParameter` command;
   - preview recomputes from historical data;
   - no Paper order is sent;
   - a fresh backtest is required before Paper.
4. User clicks a buy marker.
   - select the exact rule evaluation;
   - show all input values as of that timestamp;
   - show target, risk decision, simulated order, and fill when available;
   - never use later data in the explanation.
5. User brushes a pre-breakout range.
   - create an event-sample draft;
   - show observation and future-label windows separately;
   - require label confirmation before adding it to a research dataset.

## Stage 4 — Build

### Controls

- immutable summary of confirmed brief, research case, design, data, and strategy semantics;
- generated file list and exact hashes;
- capability report;
- verification checklist;
- diagnostics grouped by brief, research, rule, drawing, compile, and policy;
- `Generate`, `Regenerate from revision`, `Compile`, `Review`, and `Install` actions.

### Gate

The generated unit must bind the exact workspace revision. Any material upstream edit detaches the
source and disables installation until regenerated. Reuse the current compile/register/install path.

## Stage 5 — Validate

### Controls

- dataset, instruments, timeframe, date range, and provenance;
- parameter set and revision hash;
- risk assumptions;
- commission/slippage model;
- `Preview historical`, `Run backtest`, `Compare revisions`, and `Open result on chart`;
- metrics plus event/rule-level drilldown;
- train/validation/test or walk-forward separation for discovered strategies;
- baseline comparison: buy-and-hold, no-signal, and simpler-rule baseline where applicable.

### Chart behavior

The center chart shows feature values, entry/exit markers, targets, risk rejections, orders, partial
fills, fills, stops, and P&L. Clicking any marker opens its causation chain. Parameter edits create a
new revision and clear the previous “tested” status.

### Gate

Only an exact installed strategy revision with a successful canonical backtest can expose `Run tested
strategy in Paper`.

## Stage 6 — Paper

### Controls

- Paper book selector and opening/current equity;
- exact tested strategy revision and parameter set;
- instrument/feed/capability status;
- Start, Pause, Resume, Stop;
- target, pending entry, order, fill, position, cash, equity, and P&L;
- risk decisions and reconciliation issues;
- `Open Execution Console`.

Changing a strategy computation parameter stops the direct tested handoff until a new backtest passes.
Display-only changes remain allowed while running. No control exposes real-money routing.

## Complete Dolpago-style example

User says:

> Research intraday shooting and crash setups using trade prints and the order book. Focus on the top
> 150 symbols by same-day volume, normalize across instruments, show me the useful custom indicators,
> then build and Paper-test the best robust rule.

The product should do this:

1. Classify as `Strategy + required Research + automatic Design`.
2. Resolve market, session, universe, outcome horizon, thresholds, and required `Trades + L2`.
3. Check that the selected connected broker and recorder can supply and retain both streams.
4. Open the chart and let the user label shooting, crash, and neutral examples.
5. Let the user choose point or preceding-window capture per feature.
6. Build normalized order-flow features without using future outcome data.
7. Run chronological/walk-forward discovery with transaction costs.
8. Show candidates overlaid on real historical events, including failures.
9. Let the user promote one exact candidate into the seven-stage strategy semantics.
10. Automatically design an explanation view: price, trade intensity, depth imbalance, signal markers,
    targets, and fills.
11. Confirm the exact research case and strategy meaning.
12. Generate and install one SDK `IStrategyKernel` whose `Draw()` displays the same feature/rule IDs it
    uses for targets.
13. Run historical backtest and inspect signals from the chart.
14. Transfer the exact tested revision and parameters to a selected Paper book.
15. Start live market data; calculate the same features; submit virtual targets; pass Paper risk; emit
    deterministic acknowledgements/fills; update positions, cash, equity, and P&L; persist and recover.

## Implementation packets in the existing Mac architecture

### W1 — Workspace state and invalidation

- Add a versioned workspace aggregate under Core strategy-generation contracts.
- Include hashes for brief, research, dataset/features, strategy semantics, drawing, build, and test.
- Persist it through the existing authoring-session repository.
- Acceptance: each material mutation creates a revision; appearance-only changes do not invalidate a
  semantic backtest; computation/rule/data changes do.

### W2 — Six-stage navigation in the existing authoring window

- Replace the two-value `StrategyAuthoringScreen` with stage state while retaining the same window and
  view-model partial architecture.
- Add optional/skipped status and truthful gating.
- Acceptance: no blank workbench, no direct jump past unresolved material decisions, restart restores
  the exact stage and revision.

### W3 — Host-owned interactive chart editor

- Keep SDK `IRenderSurface` output-only.
- Add a trusted Avalonia overlay/control that owns selection, brushing, handles, annotations, and typed
  edit commands.
- Acceptance: untrusted authored code never receives a Control; gestures are bounded, deterministic,
  keyboard accessible, and serializable.

### W4 — Feature/layer/rule identity and inspector

- Introduce stable feature and rule IDs plus explicit bindings to layer and parameter IDs.
- Add Data, Feature, Rule, Research, and Appearance inspector tabs.
- Acceptance: hiding a layer does not alter a rule; changing one linked period changes both the
  computed feature and rule input; stale downstream evidence is visible.

### W5 — Research dataset and event labels

- Define universe, event sample, observation window, future label window, raw data requirements, and
  provenance.
- Add chart labeling and coverage UI.
- Acceptance: future data cannot enter features; edits and exclusions are auditable; raw capability
  shortfalls block the run rather than substituting bars.

### W6 — Normalization and feature computation

- Add versioned, deterministic transformations fitted on train data only.
- Add order-flow feature registry with explicit L1/L2/trade dependencies.
- Acceptance: fixtures reproduce exact feature values; transform leakage and cross-provenance mixing
  are rejected.

### W7 — Experiment runner and results

- Add chronological splits, walk-forward, purge/embargo, costs, baselines, deterministic seed, and
  bounded search.
- Connect retained research orchestration to the chart-selected workspace revision.
- Acceptance: restart resumes a retained run; results bind exact dataset/feature/revision hashes; no
  candidate is labeled proven or profitable.

### W8 — Safe typed design preview

- Render known typed layers from historical data before generated code is trusted.
- Add pane/layer editing and reference comparison.
- Acceptance: preview matches declared layer identities and required data; unsupported layers fail
  visibly.

### W9 — Generate/build integration

- Feed the confirmed workspace revision to the existing authored-unit specification/source compiler.
- Preserve existing reference, capability, source-policy, draw-probe, installation, and catalog gates.
- Acceptance: generated source cannot change confirmed rule, research, instrument, or layer bindings.

### W10 — Validate/Paper integration

- Reuse the existing Quick Backtest, exact-parameter handoff, Paper runner, OMS, and console.
- Add causation drilldown from chart marker to target/risk/order/fill.
- Acceptance: only the exact tested revision runs; restart restores strategy/Paper state; real routing
  remains absent.

## Verification matrix

| Layer | Required proof |
|---|---|
| Core contracts | canonical serialization, hashing, revision, binding, and invalidation table tests |
| Research | leakage, split, transform, provenance, capability, event-label, and reproducibility tests |
| Chart interaction | pointer/keyboard selection, brushing, handle edits, undo/redo, hit identity, and accessibility tests |
| Design | layer reorder/visibility/style versus semantic-change invalidation tests |
| Generation | exact workspace-to-spec-to-source binding and hostile provider mutation tests |
| Runtime | same feature/parameter/rule identity in historical preview and live callbacks |
| Backtest | exact revision and parameter handoff, stale-result rejection, event-marker causation |
| Paper | target → risk → OMS → venue → fill → ledger → account → UI, plus restart recovery |
| Safety | unsupported L2/trades fail closed; no authored-code UI access; no real order route |

## Definition of complete

The workflow is complete only when a user can:

1. describe either a normal indicator strategy or an open-ended market-pattern research idea;
2. see and correct the inferred instrument, data, timeframe, product type, and optional stages;
3. label or select chart evidence and obtain reproducible feature calculations;
4. see every research feature and strategy rule on the same chart when desired;
5. edit a chart-linked parameter once and see every dependent layer/rule update truthfully;
6. distinguish appearance changes from semantic changes and understand stale evidence;
7. confirm a hashed research case, strategy meaning, and design;
8. generate, compile, install, and reopen the artifact from the Catalog;
9. inspect historical signals and pass a backtest for the exact revision;
10. run that exact revision through existing local Paper execution and recover its account state;
11. do all of the above without granting authored code UI authority or enabling real-money orders.
