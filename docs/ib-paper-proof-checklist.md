# IB Paper proof — your steps (Mac)

DaxAlgo side is ready (`CSharpAPI.dll` present, `InteractiveBrokersExecution` Enabled Paper).
**Google login is only for DaxAlgo.** IB needs **IBKR username/password** in Gateway/TWS.

In-product help: Execution Console → Interactive Brokers row shows **What is IB Gateway?**,
port presets, and **Open IB Gateway download**. Full guide: [`broker-orders-setup.md`](broker-orders-setup.md).

## You do (cannot be automated)

1. **Download & install** IB Gateway (or TWS) — use the in-app button, or  
   https://www.interactivebrokers.com/en/trading/ibgateway-latest.php?p=stable
2. **Log in with IBKR** → choose **Paper Trading** (not Live).
3. In Gateway/TWS: enable **API** / socket clients; allow **127.0.0.1**.
4. Note your Paper account id (`DU…`) from the IB account window.
5. In Execution Console: tap **Gateway Paper · 4002** (or **TWS Paper · 7497**), paste `DU…`, **Connect**.

## Port must match the app

| App | Paper port | Our default `appsettings` |
|-----|------------|---------------------------|
| TWS Paper | **7497** | Already set (`Port: 7497`) |
| IB Gateway Paper | **4002** | Use the **Gateway Paper** preset in the IB form |

Keep `AllowLiveExecution: false` and `Mode: "Paper"`.

## Then in DaxAlgo

Execution Engine → **Execution Console (live-capable)** → Brokers → Interactive Brokers → **Connect** → New Real book → Limit.
