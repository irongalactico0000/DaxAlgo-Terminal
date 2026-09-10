# Chart → Strategy → Execution — engineering requirements

Product one-liner (research, no orders):

> Pick instrument → set range → draft strategy from interactive chart context → change rules/params/structure until satisfied → run historical same-kernel backtest on that locked TradeIR.

Buy path (separate):

> Smoke → Approve → Prepare → Strategy Runner and/or Execution Console ticket. LIVE only from Console.

Authority: Mac Avalonia (`DaxAlgo-Terminal-Mac-Integrated`). Related: `docs/execution-oms-split.md`, `docs/live-execution-macos.md`, `docs/mac-product-backlog-status.md`.

## How to use this doc

- One ID = one observable contract (input → output → fail-closed).
- PRs claim IDs in the description (`Implements R1.3`).
- Chart draft is UI state; **TradeIR content hash** is authority for smoke / Approve / Runner / historical backtest.
- Non-goals stay in **R0**.

Status: **Done** / **Partial** / **Gap** / **Deferred**.

---

## R0 — Invariants (must never regress)

| ID | Requirement | Status |
|----|-------------|--------|
| R0.1 | Chart / Builder / chat / backtest must not submit broker or Paper OMS orders. | **Done** (architecture) |
| R0.2 | Strategy runtime reaches OMS only via replicator / book intake (no kernel `PlaceOrder`). | **Done** |
| R0.3 | Smoke, Approve, Prepare, Runner, backtest bind the same TradeIR content hash. | **Done** |
| R0.4 | LIVE arming only from Execution Console + confirmation store; Runner refuses LIVE books. | **Done** |
| R0.5 | Binance = market data only. | **Done** |

---

## R1 — Charts (context + interactive draft)

| ID | Requirement | Status |
|----|-------------|--------|
| R1.1 | Essential: instrument + bar size before draft/send. | **Done** (Charts toolbar) |
| R1.2 | Essential (ideal): explicit history range (from–to), not only TF lookback. | **Done** — Charts Load History uses `GetHistoricalBarsAsync(from,to)`; Alpaca/cTrader/Upstox/IB/Binance/Simulated + metered wrapper implement range; other venues use cover+filter default |
| R1.3 | Authoring gestures produce structured **StrategyDraft** DTO. | **Partial** — Place STOP/TARGET click + fields → draft |
| R1.4 | Draft edits keep chart objects ↔ draft panel in sync. | **Partial** — stop/target lines on chart |
| R1.5 | Lock / Confirm materializes TradeIR; unlocked draft not runnable. | **Partial** — Lock binds draft to active TradeIR hash |
| R1.6 | Research samples are optional evidence; not a locked strategy. | **Done** |
| R6.1 | Instrument → range → draft → lock → historical BT in one shell. | **Partial** — opens BT when registered; else auto-Compile→review assist (Register consent remains) |

Contract note: `StrategyDraftV1` lives under Core `Strategies/Generation`. Capture/`ResearchChartSelectionV1` remains the **sample** path (R1.6), not the strategy draft path (R1.3).

---

## R2 — Refine

| ID | Requirement | Status |
|----|-------------|--------|
| R2.1 | Refine = mutate draft fields: rules, params, structure, instrument/TF scope. | **Partial** (chat + regenerate; not chart-object refine) |
| R2.2 | Chat refine applies only through the same draft / intent schema (no side-channel strategy). | **Partial** |
| R2.3 | After lock, refine either re-locks a new hash or stays dirty / not backtestable. | **Partial** |

---

## R3 — Same-kernel historical backtest

| ID | Requirement | Status |
|----|-------------|--------|
| R3.1 | Historical backtest runs only on locked TradeIR hash (same kernel as Paper admit). | **Partial** (Validate / Backtest Studio) |
| R3.2 | Builder “synthetic smoke” ≠ historical backtest; UI must not label smoke as performance proof. | **Done** (copy: exact-hash synthetic smoke; Studio separate) |
| R3.3 | Historical inputs: locked hash + instrument(s) + range + documented cost defaults. | **Partial** |
| R3.4 | Output: trades + metrics tied to that hash (reproducible). | **Partial** |

---

## R4 — Admit (gate to execution)

| ID | Requirement | Status |
|----|-------------|--------|
| R4.1 | Approve only after smoke pass on that hash. | **Done** |
| R4.2 | Prepare binds hash → book/session; Start refused on mismatch / closed gate. | **Done** |
| R4.3 | Paper Lab / Vibe candidates never skip R4. | **Done** |

---

## R5 — Order execution (“buy”)

| ID | Requirement | Status |
|----|-------------|--------|
| R5.1 | Strategy buy: Runner → local Paper or broker-Paper Real book → intake → OMS. | **Done** |
| R5.2 | Manual buy: Console Connect → CreateBook → manual ticket. | **Done** |
| R5.3 | Position truth = adapter/OMS qty; Avg/realized UI targets Windows console columns. | **Done** (AVG / LAST / UNREAL. / REAL. / TARGET / DRIFT + daily realized P&L list) |
| R5.4 | Default Paper; LIVE needs typed confirm. | **Done** |

---

## R6 — UX acceptance

| ID | User can… | Status |
|----|-----------|--------|
| R6.1 | Instrument → range → draft on chart → see rules update → lock → historical backtest without leaving research shell. | **Partial** — Charts shell strip + Historical BT opens Validate path; still needs compiled unit |
| R6.2 | After backtest, Approve → Prepare → Start Paper; see qty/fills without chart placing the order. | **Done** |
| R6.3 | Runner binds broker-Paper Real book; LIVE blocked in Runner. | **Done** |

---

## P0 vs later

| P0 (next engineering) | Later |
|----------------------|--------|
| `StrategyDraftV1` schema + validator + gesture→draft mutators (R1.3 foundation) | R1.2 explicit load range UI |
| Map one gesture (stop + target levels) → draft objects → visible draft summary | Full gesture vocabulary + chart↔panel sync (R1.4) |
| Keep R1.6 Capture path unchanged | Chart-primary lock → TradeIR (R1.5 complete) |
| R5.3 Avg / REAL columns on Avalonia Execution Console | R6.1 one-shell historical backtest |
| Do not weaken R0 | Harness shell / Marketplace spec packaging |

### Vertical slice (first interactive authoring)

1. User has instrument + TF on Charts.  
2. Places **stop** and **target** horizontal levels (or equivalent commands).  
3. Host writes `StrategyDraftV1` objects with roles `ProtectiveStop` / `ProfitTarget`.  
4. Builder shows those objects in a draft summary (read-only ok for P0).  
5. Existing Brief confirm / generate / smoke path still owns TradeIR lock (no orders from chart).

---

## Backend vs frontend (this workflow)

| Layer | Backend | Frontend |
|-------|---------|----------|
| R1 Chart draft | `StrategyDraftV1` schema, validator, gesture applier, canonical JSON/hash; session field `StrategyDraftJson` | Charts stop/target fields + Send draft; Builder `CHART STRATEGY DRAFT` summary; click-to-place lines still Gap |
| R1.6 Research | `ResearchChartSelectionV1` / dataset leakage gates | Brush + B/C/N + gallery (unchanged; not a locked strategy) |
| R4–R5 Admit / buy | TradeIR hash → Prepare → Runner/Console intake; LIVE Keychain gate | Runner book picker; Execution Console LIVE arming |
| W4 Bindings | `StrategyInteractionBindingsV1` fail-closed graph | Builder inspector not built yet |

See also: `docs/mac-backend-frontend-status.docx`, `docs/mac-product-backlog-status.md` (includes **Gaps vs Hyperion and LuxAlgo**).

## Related types (today)

| Type | Role |
|------|------|
| `ResearchChartSelectionV1` | Event sample brush (R1.6) |
| `ConfirmedStrategyIntentV1` | Reviewed brief before generate |
| `StrategyInteractionBindingsV1` | Spec graph: parameters ↔ features ↔ layers ↔ rules |
| `StrategyDraftV1` | Editable chart-authored draft before TradeIR lock |
