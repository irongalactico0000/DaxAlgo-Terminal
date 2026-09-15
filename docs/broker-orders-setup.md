# Broker orders setup (product guide)

Plain-language setup for **placing orders** from the Mac Execution Console.
Market-data login (Charts) uses the same IB / cTrader forms where noted.

## Venue matrix

| Venue | Orders in product? | What you provide |
|-------|--------------------|------------------|
| Alpaca | Yes | Paper API key + secret |
| Interactive Brokers | Yes | IB Gateway (or TWS) running locally + Paper account id (`DU…`) |
| cTrader | Yes | Spotware OAuth client + access token + `ctidTraderAccountId` |
| Binance | **No** | Charts/data only — no order adapter (by design) |

## Interactive Brokers — what “Gateway” means

**IB Gateway** (or **TWS**) is Interactive Brokers’ own desktop app on *your* Mac.
DaxAlgo does **not** replace it. The terminal opens a local socket to that app
(`127.0.0.1` + a port). You log into IBKR inside Gateway/TWS; DaxAlgo then sends orders through it.

### First-time Paper path

1. Install **IB Gateway** (lighter) or **TWS** from IBKR.
2. Open it → sign in with your **IBKR Paper** login (not Live).
3. Enable **API → Settings → Enable ActiveX and Socket Clients** and allow **127.0.0.1**.
4. In DaxAlgo → Execution Console → Interactive Brokers:
   - Tap a **port preset** (`TWS Paper · 7497` or `Gateway Paper · 4002`).
   - Optionally **Open IB Gateway download** / **Re-check Gateway**.
5. Enter your Paper **account id** (`DU…`) in the console field.
6. **Connect** → create a Real book → place a Limit (still Paper until LIVE is armed).

| App you opened | Paper port | Live port |
|----------------|------------|-----------|
| TWS | **7497** | 7496 |
| IB Gateway | **4002** | 4001 |

## cTrader

1. Open [Spotware apps](https://connect.spotware.com/apps) (button on the cTrader form).
2. Create an app → copy **Client ID** and **Client Secret**.
3. Authorize → copy **access token**.
4. Paste into the form → **Discover** to fill `ctidTraderAccountId`.
5. Leave **Use live endpoint** unchecked for demo/Paper → **Connect**.

Tokens expire; re-authorize and paste a new token when Connect starts failing.

## Alpaca

Paste Paper API key id + secret on the Alpaca row → Connect.
LIVE needs the exact LIVE account id field + Keychain confirmation.

## Binance

Use Binance for **charts / market data** only.
There is **no** Binance order path in this Mac product. Do not expect Execution Console Connect for Binance.

## Related

- Operator checklist: [`ib-paper-proof-checklist.md`](ib-paper-proof-checklist.md)
- LIVE fences: [`live-execution-macos.md`](live-execution-macos.md)
- Product lanes: [`product-lanes-and-regulated-fences.md`](product-lanes-and-regulated-fences.md)
