# DaxAlgo Terminal macOS

Private macOS edition of DaxAlgo Terminal, using Avalonia on .NET 9.

This is an independent terminal source tree: it compiles locally and does not reference either
Windows checkout. The current non-strategy shell, views, tools, charts, brokers, backtest, AI,
persistence, and strategy-management contracts are present. Concrete strategy implementations are
distributed separately as plugins and are not shipped in this repository.

## Build and test

```bash
dotnet build TradingTerminal.Mac.slnx
dotnet test tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj
dotnet run --project src/linux/Shell/TradingTerminal.App.Avalonia
```

## Package on macOS

Run `bash tools/macos/package.sh osx-arm64` or `bash tools/macos/package.sh osx-x64` on a Mac. The
script publishes a self-contained `.app`, creates the `.icns`, signs the complete bundle, and creates
the distributable zip. Set `CODESIGN_IDENTITY` to a Developer ID Application identity and
`NOTARY_KEYCHAIN_PROFILE` to an `xcrun notarytool` profile to notarize and staple it. Without a
Developer ID identity, the result is an ad-hoc-signed local build only.

Interactive Brokers packaging uses the official TWS C# API. `IB_API_MODE` defaults to `required`, so
packaging fails if that API is unavailable. If it is not resolved automatically, set
`TWS_API_CLIENT_DLL` to the official `CSharpAPI.dll`. Use `IB_API_MODE=auto` only to allow a package
without IB support after a warning, or `IB_API_MODE=off` to compile IB support out explicitly.

Private DAXQ releases must set `DAXQ_VM_MODE=required` and provide:

- `DAXQ_VM_LICENSE_KEY_SHA256_HEX`
- `DAXQ_VM_LICENSE_ISSUER`
- `DAXQ_VM_LICENSE_AUDIENCE`
- a non-ad-hoc `CODESIGN_IDENTITY`

The protected runtime is then built, pinned, and signed as part of the bundle. Production DAXQ
packages require a real Mac and a Developer ID Application certificate. The default
`DAXQ_VM_MODE=auto` warns and omits that runtime when the private inputs or signing identity are not
available; `off` disables it explicitly.

## Strategy smoke

Run the development build with
`dotnet run --project src/linux/Shell/TradingTerminal.App.Avalonia -- --smoke-strategies`, or pass
`--smoke-strategies` to the packaged app executable. The smoke discovers installed strategy plugins,
exercises the strategy factory, writes `DaxAlgoTerminal/diagnostics/smoke-strategies.txt` beneath the
platform local application-data directory, and exits with the smoke result. Run it with the real
release plugins; an unextended app is expected to contain no strategy implementations.

## Vibe Quant strategy builder

Vibe Quant generates four independently reviewable strategy representations, can synthesize them
into a hash-bound canonical TradeIR artifact, and can run a package-valid Typed Graph through the
bounded in-process synthetic QuoteL1 smoke path. The smoke path is explicitly non-historical and
non-worker-isolated. See the [four-lane workflow](docs/vibe-quant-four-lane-workflow.md) and
[normative lane contracts](docs/vibe-quant-lane-contracts.md).

Four-lane follow-ups retain the original strategy brief instead of replacing it. Backtest-navigation
messages open the separate test guidance without starting four new model calls or changing candidate
hashes. In Candidate, every lane becomes inspectable as soon as its own request finishes; these live
source/JSON previews remain read-only until the complete ordered four-lane batch validates.
Each turn starts four concurrent initial requests. An invalid lane may make one bounded,
validation-aware repair request; restored results are explicitly labeled and never masquerade as a
new run.

For the shortest supported trial, open **New strategy**, search for `smoke`, choose
**QuoteL1 EMA crossover · smoke compatible**, generate the four lanes, then choose
**Graph · Typed → Use selected in editor → Run exact-hash synthetic smoke**. This exercises deterministic
synthetic QuoteL1 events for the exact selected hash; it is not a historical Backtest Studio run.

## Charts indicators

Built-in overlays match common quant platforms (SMA/EMA, RSI, MACD, Bollinger, Stochastic, ATR,
VWAP, ADX). Chat can pick them from the host catalog; they render on the native Charts window.

Users add custom SMA/EMA/RSI/ATR periods in
`%LocalAppData%/DaxAlgo Terminal/user-indicators.json` (seeded on first Charts open).
See [chart indicators](docs/chart-indicators.md).

## Remaining release boundary

Windows-hosted builds and tests verify source and packaging invariants, not macOS delivery. Before
release, validate each supported RID on real Mac hardware: Developer ID signing, notarization and
Gatekeeper launch; Keychain-backed secrets; official IB API loading and connectivity; protected DAXQ
build, load, and execution; strategy smoke with release plugins; and final visual/runtime parity.

## Layout

- `src/linux/` — independent cross-platform and macOS product source; the inherited directory name
  does not imply a dependency on the former Linux repository.
- `tests/linux/` — headless, macOS, and packaging-invariant tests.
- `tools/macos/` — application packaging, signing, and release helpers.
- `tasks/` — implementation findings, checks, and parity evidence.
- `.claude/context/linux/` — generated routing data for the independent product tree.

Local secrets belong only in ignored `appsettings.local.json` or environment variables.
