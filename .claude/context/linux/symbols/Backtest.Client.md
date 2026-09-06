# TradingTerminal.Backtest.Client — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Backtest/TradingTerminal.Backtest.Client/AbandonedWorkerStagingCleaner.cs
```cs
   13: public static int Cleanup(string jobRoot, TimeSpan minimumAge, DateTime utcNow)
```

## src/linux/Backtest/TradingTerminal.Backtest.Client/BacktestJobClient.cs
```cs
   22: public sealed class BacktestJobClient : IBacktestJobClient, IDisposable
   30: public BacktestJobClient(
   37: public BacktestJobClient(
   45: public async Task<BacktestJobOutcome> RunAsync(
  486: public void Dispose()
 1301: public Process Process { get; } = process;
 1303: public bool TryAssign() => guard.TryAssign(Process);
 1305: public void Dispose()
```

## src/linux/Backtest/TradingTerminal.Backtest.Client/BacktestWorkerExecutableResolver.cs
```cs
   15: public static bool TryResolve(
```

## src/linux/Backtest/TradingTerminal.Backtest.Client/BacktestWorkerOptions.cs
```cs
    5: public sealed class BacktestWorkerOptions
   11: public string? WorkerExecutablePath { get; set; }
   14: public List<string> WorkerArguments { get; } = [];
   17: public string? JobRootDirectory { get; set; }
   20: public string? StrategyBundleStoreRoot { get; set; }
   26: public StrategyBundleInstallPolicy? StrategyBundlePolicy { get; set; }
   29: public TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(10);
   31: public int ProgressBufferCapacity { get; set; } = 32;
   32: public int MaxProgressLineCharacters { get; set; } = 16 * 1024;
   33: public int MaxCapturedStandardErrorCharacters { get; set; } = 64 * 1024;
   36: public TimeSpan AbandonedStagingAge { get; set; } = TimeSpan.FromDays(2);
```

## src/linux/Backtest/TradingTerminal.Backtest.Client/BacktestWorkerServiceCollectionExtensions.cs
```cs
    5: public static class BacktestWorkerServiceCollectionExtensions
    7: public static IServiceCollection AddBacktestWorker(
```

## src/linux/Backtest/TradingTerminal.Backtest.Client/IBacktestJobClient.cs
```cs
    6: public interface IBacktestJobClient
    8:     Task<BacktestJobOutcome> RunAsync(
    9:     BacktestJobRequest request,
   10:     IProgress<BacktestJobProgress>? progress = null,
   11:     CancellationToken ct = default);
```
