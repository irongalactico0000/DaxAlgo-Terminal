# Product lanes vs regulated surfaces

**Not legal advice.** Product fences for DaxAlgo Terminal (macOS) while securities counsel
defines what may ship as tool vs adviser vs fund. Last updated: 2026-09-11.

## Three lanes (ship these; keep them separate in UX)

| Lane | Operator journey | Who holds keys / books | Orders how |
|------|------------------|------------------------|------------|
| **1 · Research** | Charts → Builder → Historical BT → Approve → Prepare → Paper | Local Paper book / hash-bound admit | `SandboxExecutionReplicator` → Paper OMS only |
| **2 · Operator execution** | Paste API key → Execution Console → Connect → Real book → Limit/manual | **Their** broker keys; venue starts Paper; LIVE = Keychain + flags | Console / replicator → OMS; **no** Binance orders |
| **3 · Strategy as software** | Marketplace / Plugin Manager install → registry → *their* Paper/keys | Installer gets package (`unit.specification` + `.cs`); run is still lane 1→2 on **their** machine | Same as 1–2 after register; site browse ≠ Terminal install |

**Invariant:** Strategy code never calls broker `PlaceOrder`. Only replicator → OMS.

## Product vs regulated matrix (UI)

| Topic | UI **may** say | UI **must never** imply |
|-------|----------------|-------------------------|
| Research / Historical BT | Exact-hash replay; smoke ≠ performance proof | Guaranteed returns; “proven alpha”; investable track record |
| Paper | Local / broker-Paper simulation; learning & qualification | Real-money results; “risk-free profit” |
| LIVE Console | Operator-armed; Keychain typed confirm; per-venue | One-click live; auto-arm from research; silent LIVE |
| API keys | You paste keys; session memory for Paper form; LIVE confirm in Keychain | We custody your funds; we trade for you |
| Marketplace package | Install software unit into **your** registry | Subscribe to a pool; buy a share of a book; we execute for a crowd |
| Harness / Runner | Observe or run **your** admitted unit on **your** book | Managed account; discretionary allocation across users |
| Copy / follow fills | — (not shipped) | “Follow this trader”; mirror someone else’s OMS book |
| ETF-like / pooled | — (not shipped) | ETF; fund share; NAV; pro-rata book ownership; collective vehicle |

## Parked (counsel before any ship) — “follow / pool / ETF share”

| Idea | Demand | Why parked |
|------|--------|------------|
| Others follow a publisher’s fills | High | Often advice or managed trading |
| Pooled capital / one book → many investors | Very high | Fund / broker-dealer / adviser territory in many jurisdictions |
| Marketing MFT/LFT as investable product | High | Performance-claim / prospectus-class risk |

**Engineering rule until counsel clears an entity model:** no menu, no Marketplace badge, no Runner mode named Follow / Pool / ETF / Copy-trade / Shared book PnL.

Closest **safe** distribute story today: lane 3 only — user installs package, brings **own** keys, runs **own** book (self-custody software tool framing; marketing still needs counsel).

## Where this shows up in the app

| Surface | Lane cue |
|---------|----------|
| Charts research shell | Research only; does not place broker orders |
| Vibe Quant / Builder | Research → qualify; Register consent before historical BT |
| Execution Engine → live-capable Console | Lane 2; Paper default; LIVE per venue |
| Paper Strategy Runner | Paper or broker-Paper Real books; LIVE blocked |
| Browse Marketplace | Opens site; install via Strategy Manager / `--install-open-package` — software, not pooled execution |

## Todo mapping

| Todo | Role |
|------|------|
| Alpaca Paper proof (1b) | Prove lane 2 with real vendor keys |
| Harness one shell | Lane 1 continuity (Validate → Paper) |
| Marketplace `unit.specification` + `.cs` | Lane 3 content |
| This doc + UX tips | Fences |
| Follow / pool / ETF | Explicitly **out of scope** until counsel |

See also: `docs/execution-oms-split.md`, `docs/live-execution-macos.md`, `docs/chart-strategy-execution-requirements.md`, `docs/mac-product-backlog-status.md`.
