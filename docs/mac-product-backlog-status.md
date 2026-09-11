# Backlog status — Mac product gaps

Updated: 2026-09-11

| Item | Status |
|------|--------|
| Real Mac UI smoke Validate → Paper → visible book qty | **Done** — `--smoke-paper-handoff` PASS (`BOOK POSITION` qty=2); shutdown dispose hardened |
| Marketplace → registry open/run | **Wired + sample** — `OpenPackageBuyOnceSample` packs `unit.specification` + `.cs`; install→register tests green. Site browse ≠ Terminal install |
| Harness as one shell | **Partial→stronger** — titles unified `Harness · Paper · …`; Validate→Paper still opens Runner via harness entry |
| Capture → indicator overlays | **Done** — after B/C/N label, additive `ema-20` / `rsi-14` / `atr-14` (no period retune) |
| Live adapters / LIVE gate | **Done** — Mac live OMS full parity: ported `TradingTerminal.Execution` + Keychain LIVE + Avalonia Execution Console (Alpaca / IB / cTrader). Binance data-only. See `docs/live-execution-macos.md`. |
| Strategy Runner → Real books | **Done** — broker-Paper Real books; LIVE blocked in Runner |
| Chart → strategy draft (interactive) | **Done (R1.3 E2E)** — Place STOP/TARGET → fields + lines → Send → Builder `CHART STRATEGY DRAFT`; evidence `tmp/draft-audit/10–12` (`--preview-draft-e2e`). Shell polish / lock / Bindings still open. See `docs/chart-strategy-execution-requirements.md` |
| Broker From/To history API | **Done** — `IBrokerClient` / `IMarketDataRepository` from–to overload; Charts Load History uses it; Alpaca/cTrader/Upstox/IB/Binance/Simulated override |
| Avg / PnL Execution Console parity | **Done** — POSITIONS: AVG / LAST / UNREAL. / REAL. / TARGET / DRIFT + daily realized P&L list |
| Interaction bindings (W4) inspector | **Done (UI)** — Builder **Bindings** tab lists parameters/features/layers/rules; Apply uses `ApplyCanonicalParameter` (invalidates register). Core vs optional params still Gap |
| Core vs optional parameters | **Modeled** — `AuthoredUnitParameterPresenceV1` Required/Defaultable; Confirm fails on unset Required; Bindings shows Presence |
| Similar-history / A→B | **Deferred** |
| Product lanes / regulated fences | **Done (docs + menu cues)** — `docs/product-lanes-and-regulated-fences.md`; follow/pool/ETF parked |
| Alpaca real-vendor Paper proof | **Blocked** — CLI profile keys 401; refresh Paper keys then Connect→book→Limit |

## Product lanes (do not blur)

1. **Research** — Charts → Builder → Historical BT → Paper (hash-bound).
2. **Operator execution** — API key → Execution Console → your book (Paper default; LIVE gated).
3. **Strategy as software** — Marketplace install → your registry → your keys/books.

**Parked:** follow / pool / ETF-share (counsel before any ship). Matrix: [`docs/product-lanes-and-regulated-fences.md`](product-lanes-and-regulated-fences.md).

## Gaps vs Hyperion and LuxAlgo

**Hyperion** = DaxAlgo’s own Strategy Builder / AI authoring intake (classify → generate → compile → Paper).  
**LuxAlgo** = external chart→AI→backtest benchmark (TradingView-class Quant UX + rich indicator kits).

Word overview (includes this section): [`docs/mac-backend-frontend-status.docx`](mac-backend-frontend-status.docx).

### Vs Hyperion (finish the chart → Builder bridge)

| Issue | Impact on Hyperion |
|-------|--------------------|
| Chart draft → Builder screenshot-proven | **Closed for P0** — `tmp/draft-audit/10–12` + `RESULT.txt` PASS (`--preview-draft-e2e`). OS synthetic clicks still flaky; in-app place path is authoritative |
| Draft → TradeIR lock still chart-secondary | Confirm/generate still owns lock; chart is not primary “freeze this idea” |
| Bindings inspector | **Closed for P1.2–P1.3 lite** — Builder Bindings tab + canonical Apply; hide-layer ≠ disable-rule documented in UI |
| Core vs optional params not modeled | **Closed for model** — `Presence` Required/Defaultable + Confirm gate; generators still mostly Defaultable |
| Explicit broker From–To history | **Done** — Charts uses range API; primary venues override |
| Marketplace needs full package | **Sample path Done** — `OpenPackageBuyOnceSample` + fixture README; Builder export of packages still optional |

**Hyperion strength:** typed contracts, leakage-safe research samples, hash-bound Paper / LIVE separation.

### Vs LuxAlgo (chart-first fluidity)

| LuxAlgo-like expectation | DaxAlgo issue today |
|--------------------------|---------------------|
| Draw levels/structure on chart → becomes strategy | Stop/target place + Send draft works (P0); richer structure/library still Lux-gap |
| Same window: idea → historical backtest | Multi-room (Charts → Builder → Validate/Studio), not one continuous shell |
| Large ready indicator / structure library | Small host catalog (EMA/RSI/MACD…); no Lux-scale PAC toolkit |
| Plain language → live on *this* chart fast | Works via Hyperion with more gates (intent, compile, register) |
| Alerts / webhooks / TV replicate | Different product: DaxAlgo targets Paper/OMS/LIVE Console |

**DaxAlgo strength vs Lux:** real execution path (Paper books, Runner, LIVE Console + Keychain). Lux is stronger on chart/AI/backtest/alert surface, not full Mac OMS.

### One-line synthesis

- **Vs Hyperion:** chart draft intake proven (P0); finish TradeIR lock UX + Bindings inspector next.  
- **Vs LuxAlgo:** catch up on interaction density and one-window research→backtest; stay ahead on broker Paper/LIVE architecture.

## Backend vs frontend (unfinished work)

Word copy: [`docs/mac-backend-frontend-status.docx`](mac-backend-frontend-status.docx).

| Area | Backend | Frontend | Status |
|------|---------|----------|--------|
| Live OMS on Mac | Ported `TradingTerminal.Execution` + Alpaca/IB/cTrader + Keychain LIVE; Binance data-only | Avalonia Execution Console + LIVE confirm + smoke | Done (tree still dirty/uncommitted) |
| Strategy Runner → Real books | Intake accepts broker-Paper Real books; refuses LIVE | Runner book picker; LIVE blocked with Console message | Done |
| Chart → StrategyDraft | `StrategyDraftV1` + validator + gesture applier | Place STOP/TARGET + lines + Send; session persist; `--preview-draft-e2e` PNG pack | **Done (R1.3)** |
| Interaction bindings (W4) | Parameter↔feature↔layer↔rule graph | Builder **Bindings** tab + `ApplyCanonicalParameter` | **Done (inspector)** |
| Core vs optional params | `AuthoredUnitParameterPresenceV1` + Confirm gate | Bindings shows Presence | **Done (model)** — chat ask-flow still lite |

**Shipped contrast:** chat/research → authored unit → Validate → Paper (workspace hashes, B/C/N dataset, compile/install, Paper intake + smoke).

## How to run Paper UI smoke

```bash
dotnet run --project src/linux/Shell/TradingTerminal.App.Avalonia -- --smoke-paper-handoff --bypass-login
```

Report: `~/Library/Application Support/DaxAlgoTerminal/diagnostics/smoke-paper-handoff.txt`

## How to run live OMS smoke (mock Alpaca, no network)

```bash
dotnet run --project src/linux/Shell/TradingTerminal.App.Avalonia -- --smoke-live-oms --bypass-login
```

Aliases: `--smoke-live-paper-alpaca`. Report: `~/Library/Application Support/DaxAlgoTerminal/diagnostics/smoke-live-oms.txt`

## Open-package registration contract

Installable packages must include:

- `authored/unit.specification.v1.json` under package content (or `payload/authored/…` after `DaxPackage.Extract`)
- one or more `.cs` sources that compile against that exact specification

Without the specification payload, install stays durable-only and open/run stays unregistered (explicit message).

**Sample (Lane 3):** `OpenPackageBuyOnceSample.WritePackage(...)` → `--install-open-package=` / Strategy Manager. Fixture notes: `tests/linux/Fixtures/OpenPackages/buy-once/README.md`.
