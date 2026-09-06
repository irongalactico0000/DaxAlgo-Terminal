# TradingTerminal.Core / Hosting — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Core/TradingTerminal.Core/Hosting/ISidecarController.cs
```cs
    9: public interface ISidecarController
   12:     bool IsRunning { get; }
   16:     Task<bool> EnsureRunningAsync(CancellationToken ct = default);
```

## src/linux/Core/TradingTerminal.Core/Hosting/NullSidecarController.cs
```cs
    9: public sealed class NullSidecarController : ISidecarController
   11: public bool IsRunning => false;
   14: public Task<bool> EnsureRunningAsync(CancellationToken ct = default) => Task.FromResult(false);
```
