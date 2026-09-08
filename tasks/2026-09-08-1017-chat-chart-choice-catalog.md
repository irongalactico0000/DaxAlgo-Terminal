# Goal

Let chat choose famous chart overlays and research scans, render on live chart,
and support research capture (observation→outcome → B/C/N). Skip strategy/backtest
for this slice — capture only.

# Verification (2026-09-08)

- Overlay render: `tasks/evidence/2026-09-08-host-overlays-ema-rsi-bb.png`
- Research capture armed: `…-host-research-capture.png`
- Capture windows seeded (Send to Builder enabled):
  `tasks/evidence/2026-09-08-host-research-capture-seeded.png`
  log: `research capture seeded (observation+outcome visible)`

# Connected vs missing

```
Chat catalog overlays     ✅
Live chart overlays       ✅ screenshot
Research scan → chart     ✅ opens + seeds capture windows
S&P outcome gallery       ✅ code wired (Sp100 daily scan + Research UI list)
Gallery hits in UI        ⚠️ needs Sp100 daily history in store; else empty
                          → auto-opens top hit when found (leaves AAPL)
Capture → Builder label   ✅ Send to Builder enabled after seed
Strategy / backtest       ⏸ deferred (user: capture only)
Paper path                ✅ gates exist; not exercised this slice
```
