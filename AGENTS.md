# DaxAlgo Terminal macOS — Codex guide

This private repository owns the macOS/Avalonia edition only. The Windows public core and
Professional overlay are separate repositories and outside the default blast radius.

## Session start

1. Read `.claude/context/linux/index.md`, `symbols.md`, and `deps.json`.
2. Read `.claude/context/PROTOCOL.md`.
3. Inspect `git status --short` and preserve unrelated work.
4. For material changes, create `tasks/YYYY-MM-DD-HHMM-slug.md` from `tasks/README.md`.

Navigate through the smallest generated index or symbol shard before opening source. Do not inspect,
mirror, or coordinate with a Windows repository unless the user explicitly places it in scope.

## Invariants

- Core has no Avalonia, broker SDK, storage implementation, or host dependency.
- MarketData depends on Core and stays below Infrastructure; broker SDK types stay in Infrastructure.
- `InstrumentId` is canonical and market-data provenance is preserved.
- Ingest is tick-primary and non-blocking; view models consume hub/ingest/store seams.
- MVVM remains strict; streaming UI is bounded and deterministically disposable.
- Sidecars bind to `127.0.0.1`. Live order execution is allowed only for **Alpaca / Interactive Brokers / cTrader** when composed with Keychain-backed `ILiveExecutionConfirmationStore`, per-broker `AllowLiveExecution`, and typed **LIVE** confirmation. **Binance stays market-data only** (no order adapter). Strategy → OMS only via `SandboxExecutionReplicator` (never kernel `PlaceOrder`).

## Verification

Always name the target:

```bash
dotnet build TradingTerminal.Mac.slnx
dotnet test tests/linux/TradingTerminal.Tests.Headless/TradingTerminal.Tests.Headless.csproj
```

Use `powershell -File .claude/context/manage-context.ps1 check` for structural context checks and
`deep-check` after changing projects, routed source sets, or context machinery.
