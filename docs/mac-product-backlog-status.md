# Backlog status — Mac product gaps

Updated: 2026-09-11

| Item | Status |
|------|--------|
| Real Mac UI smoke Validate → Paper → visible book qty | **Done** — `--smoke-paper-handoff` PASS (`BOOK POSITION` qty=2); shutdown dispose hardened |
| Marketplace → registry open/run | **Wired** — durable install + `OpenPackageHostRegistrar` (needs `authored/unit.specification.v1.json` + `.cs`); startup + Plugin Manager install register into `IStrategyKernelRegistry` |
| Harness as one shell | **Partial** — `AuthoredUnitHarnessSession` + catalog/Paper opens titled `Harness · Paper · …` |
| Capture → indicator overlays | **Done** — after B/C/N label, additive `ema-20` / `rsi-14` / `atr-14` (no period retune) |
| Live adapters / LIVE gate | **Done** — Mac live OMS full parity: ported `TradingTerminal.Execution` + Keychain LIVE + Avalonia Execution Console (Alpaca / IB / cTrader). Binance data-only. See `docs/live-execution-macos.md`. |
| Strategy Runner → Real books | **Done** — broker-Paper Real books; LIVE blocked in Runner |
| Chart → strategy draft (interactive) | **Partial** — shell + click-to-place + From/To true range; Historical BT auto-Compile→review (Register still manual). See `docs/chart-strategy-execution-requirements.md` |
| Broker From/To history API | **Done** — `IBrokerClient` / `IMarketDataRepository` from–to overload; Charts Load History uses it; Alpaca/cTrader/Upstox/IB/Binance/Simulated override |
| Avg / PnL Execution Console parity | **Done** — POSITIONS: AVG / LAST / UNREAL. / REAL. / TARGET / DRIFT + daily realized P&L list |
| Interaction bindings (W4) inspector | **Partial** — Core graph + validator exist; Builder Bindings UI still Gap |
| Core vs optional parameters | **Not modeled** — flat defaults + chat catalog presets only |
| Similar-history / A→B | **Deferred** |

## Gaps vs Hyperion and LuxAlgo

**Hyperion** = DaxAlgo’s own Strategy Builder / AI authoring intake (classify → generate → compile → Paper).  
**LuxAlgo** = external chart→AI→backtest benchmark (TradingView-class Quant UX + rich indicator kits).

Word overview (includes this section): [`docs/mac-backend-frontend-status.docx`](mac-backend-frontend-status.docx).

### Vs Hyperion (finish the chart → Builder bridge)

| Issue | Impact on Hyperion |
|-------|--------------------|
| Click→draft→Builder not screenshot-proven E2E | Place STOP/TARGET visible; traders cannot yet trust “draw on chart → Hyperion receives draft” |
| Draft → TradeIR lock still chart-secondary | Confirm/generate still owns lock; chart is not primary “freeze this idea” |
| Bindings inspector missing | Graph exists in Core; user cannot see/edit parameter↔feature↔layer↔rule in Builder |
| Core vs optional params not modeled | Chat/Hyperion cannot ask “must set” vs “default OK” |
| Explicit broker From–To history | **Done** — Charts uses range API; primary venues override |
| Marketplace needs full package | Open/run fails closed without `unit.specification` + `.cs` |

**Hyperion strength:** typed contracts, leakage-safe research samples, hash-bound Paper / LIVE separation.

### Vs LuxAlgo (chart-first fluidity)

| LuxAlgo-like expectation | DaxAlgo issue today |
|--------------------------|---------------------|
| Draw levels/structure on chart → becomes strategy | Stop/target UI started; not a finished chart-native authoring loop |
| Same window: idea → historical backtest | Multi-room (Charts → Builder → Validate/Studio), not one continuous shell |
| Large ready indicator / structure library | Small host catalog (EMA/RSI/MACD…); no Lux-scale PAC toolkit |
| Plain language → live on *this* chart fast | Works via Hyperion with more gates (intent, compile, register) |
| Alerts / webhooks / TV replicate | Different product: DaxAlgo targets Paper/OMS/LIVE Console |

**DaxAlgo strength vs Lux:** real execution path (Paper books, Runner, LIVE Console + Keychain). Lux is stronger on chart/AI/backtest/alert surface, not full Mac OMS.

### One-line synthesis

- **Vs Hyperion:** finish chart bridge + inspectors so Hyperion is not fed only by chat/text.  
- **Vs LuxAlgo:** catch up on interaction density and one-window research→backtest; stay ahead on broker Paper/LIVE architecture.

## Backend vs frontend (unfinished work)

Word copy: [`docs/mac-backend-frontend-status.docx`](mac-backend-frontend-status.docx).

| Area | Backend | Frontend | Status |
|------|---------|----------|--------|
| Live OMS on Mac | Ported `TradingTerminal.Execution` + Alpaca/IB/cTrader + Keychain LIVE; Binance data-only | Avalonia Execution Console + LIVE confirm + smoke | Done (tree still dirty/uncommitted) |
| Strategy Runner → Real books | Intake accepts broker-Paper Real books; refuses LIVE | Runner book picker; LIVE blocked with Console message | Done |
| Chart → StrategyDraft | `StrategyDraftV1` + validator + gesture applier | Place STOP/TARGET chrome + Send draft; session persist | Partial (E2E click path not screenshot-proven) |
| Interaction bindings (W4) | Parameter↔feature↔layer↔rule graph | No Bindings inspector / semantic param edit yet | Backend ahead of UI |
| Core vs optional params | No `IsRequired` / `IsOptional` / Core flag | Chat does not ask required vs optional | Not a feature yet |

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

- `authored/unit.specification.v1.json` under package content
- one or more `.cs` sources that compile against that exact specification

Without the specification payload, install stays durable-only and open/run stays unregistered (explicit message).
