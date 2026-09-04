# macOS index / Sandbox

Generated from source fingerprint `e91d50e75733`. macOS/Avalonia source only.

| File | LOC | Tree | Project | Role | Public surface | Purpose |
|---|---:|---|---|---|---|---|
| `src/linux/Sandbox/TradingTerminal.Sandbox.Portfolio/ClosedTrip.cs` | 11 | linux | TradingTerminal.Sandbox.Portfolio | product | N | One bounded closed-trip record. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Portfolio/ModelPortfolioFault.cs` | 82 | linux | TradingTerminal.Sandbox.Portfolio | product | Y | No fault occurred. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Portfolio/ModelPortfolioSimulator.cs` | 1498 | linux | TradingTerminal.Sandbox.Portfolio | product | Y | Upper bound on the retained closed-trip ring length. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Portfolio/ModelPortfolioSnapshot.cs` | 48 | linux | TradingTerminal.Sandbox.Portfolio | product | Y | An immutable committed-state diagnostic snapshot of the model portfolio. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Portfolio/PortfolioState.cs` | 54 | linux | TradingTerminal.Sandbox.Portfolio | product | Y | The complete staged or committed core state. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/ModelPortfolioAccount.cs` | 255 | linux | TradingTerminal.Sandbox.Runtime | product | Y | Creates an account for one canonical instrument. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/ModelPortfolioContracts.cs` | 116 | linux | TradingTerminal.Sandbox.Runtime | product | Y | Read-only committed model-portfolio state exposed to host consumers. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/MultiInstrumentModelPortfolioAccount.cs` | 209 | linux | TradingTerminal.Sandbox.Runtime | product | Y | Routes one canonical strategy callback across bounded per-instrument model accounts. This is |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/PaperExecutionBookTargetIntake.cs` | 477 | linux | TradingTerminal.Sandbox.Runtime | product | Y | Resolves the latest exact Paper reference price and its exchange observation time. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/RecordingVirtualBook.cs` | 43 | linux | TradingTerminal.Sandbox.Runtime | product | Y | Creates a bounded recorder for the complete declared instrument set. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/SandboxExecutionReplicator.cs` | 404 | linux | TradingTerminal.Sandbox.Runtime | product | Y | Result returned by the guarded execution-book target intake. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/SandboxStrategyContext.cs` | 47 | linux | TradingTerminal.Sandbox.Runtime | product | Y | The capability-only context supplied to one Pro sandbox kernel instance. |
| `src/linux/Sandbox/TradingTerminal.Sandbox.Runtime/SandboxStrategyRuntime.cs` | 1187 | linux | TradingTerminal.Sandbox.Runtime | product | Y | Lifecycle state for one headless sandbox kernel host. |
