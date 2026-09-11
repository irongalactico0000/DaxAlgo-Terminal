# Open package sample — buy-once

Lane **3 · Strategy as software** reference layout for Marketplace install.

## Required content (fail-closed without these)

| Path inside package content | Role |
|-----------------------------|------|
| `authored/unit.specification.v1.json` | Exact authored unit contract |
| `GeneratedPaperStrategy.cs` (or any `.cs`) | Compiles against that specification |

Host registrar: `OpenPackageHostRegistrar.SpecificationRelativePath`.

## Build a `.daxalgostrategy` in tests / tooling

```csharp
OpenPackageBuyOnceSample.WritePackage("/tmp/buy-once.daxalgostrategy", new InstrumentId(42));
```

Install:

```bash
dotnet run --project src/linux/Shell/TradingTerminal.App.Avalonia -- \
  --install-open-package=/tmp/buy-once.daxalgostrategy --bypass-login
```

Or: Strategy Manager → Install from URL / local file.

After install, the unit appears in `IStrategyKernelRegistry` and can run on **your** Paper book (not pooled/ETF).

## Operator path

1. Browse Marketplace site (software catalog) — does not execute orders.
2. Install package into Terminal.
3. Open via catalog / Harness → Paper with **your** keys/books.
