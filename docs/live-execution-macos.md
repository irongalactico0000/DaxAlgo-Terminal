# Live execution on macOS

The macOS terminal ships with the same order-management stack as Windows. Paper is the shipped
mode and the only mode a fresh install can reach; LIVE requires four independent opt-ins, and
missing any one of them leaves the venue in Paper or unregistered. This page is the operator
runbook. Start with Alpaca Paper — it is the only venue that needs nothing beyond an API key pair.

## What the app registers at startup

`Composition/ServiceConfiguration.cs` binds three configuration sections and registers one broker
adapter per enabled venue, plus the Keychain-backed confirmation store and the execution console:

| Section | Venue | Extra requirement |
| --- | --- | --- |
| `AlpacaExecution` | Alpaca | Key ID + secret |
| `CTraderExecution` | cTrader Open API | OAuth client/secret/token, cTID account, `SymbolId` |
| `InteractiveBrokersExecution` | TWS / IB Gateway | The TWS API (`CSharpAPI.dll`) and a running gateway |

The flags are independent per venue. Setting `Enabled: false` on one section removes only that
adapter; the others keep working. Interactive Brokers is additionally probed at startup — when the
TWS API assembly is absent the venue is skipped with a warning rather than failing the app, which is
the normal state on a Mac that has never installed the API.

## Alpaca Paper, end to end

1. Create a paper account at <https://alpaca.markets> and generate an API key pair for it.
2. Leave `AlpacaExecution` at the shipped defaults in `appsettings.json`: `Mode: "Paper"`,
   `AllowLiveExecution: false`, `BaseUrl: "https://paper-api.alpaca.markets"`.
3. Confirm `Symbol` and `CanonicalInstrumentId` describe the same instrument. The default pair is
   `AAPL` / `7101`. One adapter instance is certified for exactly one instrument, and a ticket for
   any other instrument is refused before it reaches the OMS.
4. Launch the app and open **Execution Engine → Execution Console (live-capable)**.
5. Expand **Brokers**, enter the key ID and secret in the Alpaca form, and press **Connect**. The
   row turns green and reports the authenticated Alpaca account ID.
6. Press **New book**, pick the Alpaca adapter, name the book, and press **Create**. This is a Real
   book: it holds a lease, reconciles against the broker on every operator action, and routes
   through the OMS to Alpaca.
7. In **Manual ticket**, choose side and quantity, leave the order type on **Limit**, enter a limit
   price, and press **Submit**. Market tickets are refused unless a reference trade newer than 15
   seconds is available, so Limit is the reliable choice outside regular hours.

Keys typed into the console live only in memory for that session. Nothing is written to
`appsettings.json`, and Paper credentials are never placed in the Keychain.

### Local developer bootstrap (optional)

When `AlpacaExecution:KeyId` / `SecretKey` are empty, the Mac shell seeds them from the Alpaca CLI
default profile at `~/.config/alpaca` (same source as `alpaca account get`). End users still use the
Brokers form; this is only for machines that already keep a CLI profile. Refresh an expired profile
with `alpaca profile login`. Brokers-form credentials still override at Connect.

## Arming LIVE

LIVE is deliberately harder than flipping `Mode`. Every one of these is required:

1. `Mode: "Live"` **and** `AllowLiveExecution: true` in the venue's section. Two separate flags so a
   config merge cannot arm real money on its own.
2. `BaseUrl` set to the venue's live endpoint — `https://api.alpaca.markets` for Alpaca. The gate
   rejects a live mode pointed at a paper URL and vice versa.
3. `ExpectedAccountId` set to the exact broker account ID. The adapter compares it against the
   authenticated account and refuses the connection on any mismatch, so a live key pair cannot be
   pointed at the wrong account.
4. A typed confirmation stored in the login Keychain. The console prompts for it when you press
   **Switch to LIVE** on a connected adapter: you type the required phrase verbatim, and it
   is written to the Keychain item
   `com.daxalgo.terminal.execution.live-confirmations` / `live-confirmations-v1`.

The store fails closed. If the Keychain is locked or access is denied, no confirmation can be read,
and every live gate refuses. The macOS login Keychain must be unlocked for the session.

Once a venue is armed, the shell status strip switches from `PAPER EXECUTION (DEFAULT)` to
`LIVE EXECUTION ARMED` and turns red. The banner is driven by the adapters actually registered, so
it reports LIVE even if the console window is closed.

### Revoking LIVE

Delete the Keychain item to revoke every stored confirmation at once:

```sh
security delete-generic-password \
  -s com.daxalgo.terminal.execution.live-confirmations \
  -a live-confirmations-v1
```

Restart the app afterwards. Setting `AllowLiveExecution: false` or `Mode: "Paper"` is equally
sufficient and does not require touching the Keychain.

## cTrader

cTrader Paper is the demo host `demo.ctraderapi.com:5035`; live is `live.ctraderapi.com:5035`. Beyond
the OAuth client ID, client secret, and access token, you must set `CtidTraderAccountId` to the
numeric cTID account and `SymbolId` to the venue's numeric symbol. The shipped `SymbolId: 1` is a
placeholder and will not match the pair you intend to trade — set it together with
`CanonicalInstrumentId` before connecting.

## Interactive Brokers

IB routes through a locally running TWS or IB Gateway, so there is no network credential in
configuration; `Host`, `Port`, and `ClientId` identify the local gateway socket. Paper listens on
TWS 7497 / Gateway 4002 and live on 7496 / 4001, and the port must agree with `Mode`. Enable
**API → Settings → Enable ActiveX and Socket Clients** in TWS and add `127.0.0.1` to the trusted IPs.
`AccountId` must be the exact IB account, for the same reason Alpaca requires `ExpectedAccountId`.

The venue only registers when the TWS API is present at build time. Install the API, place
`CSharpAPI.dll` where `TradingTerminal.Execution.csproj` resolves it, and rebuild; the project then
defines `HAS_IBAPI` and the real transport is compiled in. Without it the console starts normally
and logs that Interactive Brokers was skipped.

## Headless smoke (CI / no vendor keys)

```bash
dotnet run --project src/linux/Shell/TradingTerminal.App.Avalonia -- --smoke-live-oms --bypass-login
```

`--smoke-live-paper-alpaca` is an alias. The smoke uses an in-process mock Alpaca transport (Connect →
Real CreateBook → Limit). It never calls the network. Vendor Paper still uses the Execution Console
runbook above.

Report: `~/Library/Application Support/DaxAlgoTerminal/diagnostics/smoke-live-oms.txt`

## What still cannot happen

- There is no Binance order adapter. The Binance configuration is market data only.
- Strategies never reach a broker directly. A strategy influences a Real book only through
  `SandboxExecutionReplicator`, which submits target positions to the same gated OMS path.
- Paper books and Real books do not share a runtime. A Paper book is simulated end to end and
  cannot emit a broker order even when a live adapter is connected in the same session.
