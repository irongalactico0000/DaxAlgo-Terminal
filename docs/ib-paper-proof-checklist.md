# IB Paper proof — your steps (Mac)

DaxAlgo side is ready (`CSharpAPI.dll` present, `InteractiveBrokersExecution` Enabled Paper).
**Google login is only for DaxAlgo.** IB needs **IBKR username/password** in Gateway/TWS.

## You do (cannot be automated)

1. **Download & install** IB Gateway (or TWS) from the page that just opened  
   https://www.interactivebrokers.com/en/trading/ibgateway-latest.php?p=stable
2. **Log in with IBKR** → choose **Paper Trading** (not Live).
3. In Gateway/TWS: enable **API** / socket clients; allow **127.0.0.1**.
4. Note your Paper account id (`DU…`) from the IB account window.
5. Tell the assistant that id (or put it in `appsettings.json` → `InteractiveBrokersExecution:AccountId`).

## Port must match the app

| App | Paper port | Our default `appsettings` |
|-----|------------|---------------------------|
| TWS Paper | **7497** | Already set (`Port: 7497`) |
| IB Gateway Paper | **4002** | Change `Port` to `4002` if you use Gateway |

Keep `AllowLiveExecution: false` and `Mode: "Paper"`.

## Then in DaxAlgo

Execution Engine → **Execution Console (live-capable)** → Brokers → Interactive Brokers → **Connect** → New Real book → Limit.

After step 5, ask the assistant: “AccountId is DU…” — config + Connect check can continue from there.
