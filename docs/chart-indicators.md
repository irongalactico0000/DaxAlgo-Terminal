# Chart indicators

## Built-in (Charts window)

DaxAlgo Terminal Charts ships these host-owned overlays (aligned with common TradingView / thinkorswim / QuantConnect defaults):

| Id | Display | Pane |
|---|---|---|
| `sma-20` | SMA (20) | Price |
| `ema-20` / `ema-50` | EMA | Price |
| `bollinger-20` | Bollinger (20,2) | Price |
| `vwap` | VWAP (session) | Price |
| `rsi-14` | RSI (14) | Oscillator |
| `stochastic-14-3-3` | Stochastic (14·3) | Oscillator |
| `macd-12-26-9` | MACD (12·26·9) | Oscillator |
| `atr-14` | ATR (14) | Oscillator |
| `adx-14` | ADX (14) | Oscillator |

Chat (Vibe Quant) lists the same catalog when you ask for “famous indicators”. Picks toggle the live Charts window — they do **not** place Paper orders.

Under the composer, clickable chips include **Auto: +5% / crashes / breakouts from my Simulated data**. Those scan local history, open Charts on gallery hits, and auto-label up to four samples for the research experiment.

## User-defined indicators

Edit:

```text
%LocalAppData%/DaxAlgo Terminal/user-indicators.json
```

On first Charts open the file is seeded with sample SMA 200 / EMA 9 / RSI 7 entries. Schema:

```json
{
  "indicators": [
    {
      "id": "sma-200",
      "displayName": "SMA 200",
      "kind": "sma",
      "period": 200,
      "alias": "sma200"
    }
  ]
}
```

Supported `kind` values: `sma`, `ema`, `rsi`, `atr` (period 2–500). User rows appear under **USER INDICATORS** in Charts and are merged into the chat overlay catalog.
