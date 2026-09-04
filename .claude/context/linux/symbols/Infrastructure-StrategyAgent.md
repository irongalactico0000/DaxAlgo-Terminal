# TradingTerminal.Infrastructure / StrategyAgent — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Pipeline/TradingTerminal.Infrastructure/StrategyAgent/IStrategyAgentClient.cs
```cs
    6: public interface IStrategyAgentClient
    8:     Task<StrategyAgentSessionStatus> CreateSessionAsync(
    9:     JsonElement frozenContext,
   10:     CancellationToken cancellationToken = default);
   12:     Task<StrategyAgentSessionStatus> GetSessionAsync(
   13:     string sessionId,
   14:     CancellationToken cancellationToken = default);
   16:     Task<StrategyAgentSessionStatus> SubmitMessageAsync(
   17:     string sessionId,
   18:     string message,
   19:     CancellationToken cancellationToken = default);
   21:     Task<StrategyAgentRunStatus> ConfirmAsync(
   22:     string sessionId,
   23:     StrategyAgentRunManifest manifest,
   24:     string inputWorkspace,
   25:     JsonElement confirmedIntent,
   26:     CancellationToken cancellationToken = default);
   28:     Task<StrategyAgentRunStatus> StartAsync(
   29:     string runId,
   30:     CancellationToken cancellationToken = default);
   32:     Task<StrategyAgentRunStatus> CancelAsync(
   33:     string runId,
   34:     CancellationToken cancellationToken = default);
   36:     Task<StrategyAgentRunStatus> GetRunAsync(
   37:     string runId,
   38:     CancellationToken cancellationToken = default);
   40:     Task<StrategyAgentEventPage> GetSessionEventsAsync(
   41:     string sessionId,
   42:     long afterSequence = 0,
   43:     int limit = 200,
   44:     CancellationToken cancellationToken = default);
   46:     Task<StrategyAgentEventPage> GetRunEventsAsync(
   47:     string runId,
   48:     long afterSequence = 0,
   49:     int limit = 200,
   50:     CancellationToken cancellationToken = default);
   52:     Task<StrategyAgentArtifact> GetArtifactAsync(
   53:     string runId,
   54:     string relativePath,
   55:     CancellationToken cancellationToken = default);
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/StrategyAgent/IStrategyAgentHost.cs
```cs
    4: public interface IStrategyAgentHost
    6:     bool IsRunning { get; }
   10:     Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default);
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/StrategyAgent/StrategyAgentContracts.cs
```cs
    6: public sealed record StrategyAgentComponentPin(
   11: public sealed record StrategyAgentDataFile(
   20: public sealed record StrategyAgentRunManifest(
   32: public sealed record StrategyAgentSessionStatus(
   41: public sealed record StrategyAgentLaneResult(
   56: public sealed record StrategyAgentComparison(
   61: public sealed record StrategyAgentRunStatus(
   74: public sealed record StrategyAgentEvent(
   85: public sealed record StrategyAgentEventPage(
   91: public sealed record StrategyAgentArtifact(
  100: public sealed class StrategyAgentApiException : Exception
  102: public StrategyAgentApiException(
  113: public string Code { get; }
  114: public HttpStatusCode? StatusCode { get; }
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/StrategyAgent/StrategyAgentHostService.cs
```cs
   25: public StrategyAgentHostService(
   33: public bool IsRunning { get; private set; }
   39: public Task StartAsync(CancellationToken cancellationToken)
   52: public Task StopAsync(CancellationToken cancellationToken)
   58: public async Task<bool> EnsureRunningAsync(CancellationToken cancellationToken = default)
  509: public void Dispose()
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/StrategyAgent/StrategyAgentHttpClient.cs
```cs
   19: public StrategyAgentHttpClient(HttpClient http, IStrategyAgentHost host)
   25: public Task<StrategyAgentSessionStatus> CreateSessionAsync(
   34: public Task<StrategyAgentSessionStatus> GetSessionAsync(
   43: public Task<StrategyAgentSessionStatus> SubmitMessageAsync(
   56: public Task<StrategyAgentRunStatus> ConfirmAsync(
   72: public Task<StrategyAgentRunStatus> StartAsync(
   81: public Task<StrategyAgentRunStatus> CancelAsync(
   90: public Task<StrategyAgentRunStatus> GetRunAsync(
   99: public Task<StrategyAgentEventPage> GetSessionEventsAsync(
  114: public Task<StrategyAgentEventPage> GetRunEventsAsync(
  129: public Task<StrategyAgentArtifact> GetArtifactAsync(
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/StrategyAgent/StrategyAgentOptions.cs
```cs
    7: public sealed class StrategyAgentOptions
    9: public const string SectionName = "StrategyAgent";
   10: public const int DefaultPort = 8766;
   14: public bool Enabled { get; set; }
   17: public bool AutoStart { get; set; } = true;
   20: public int Port { get; set; } = DefaultPort;
   23: public string ExecutablePath { get; set; } = "";
   26: public string PackagePath { get; set; } = "";
   29: public string PythonPath { get; set; } = "";
   32: public int StartupTimeoutSeconds { get; set; } = 60;
   36: public int RequestTimeoutSeconds { get; set; } = 300;
   39: public string StoreRoot { get; set; } = "";
   42: public string QueryEngineRoot { get; set; } = "";
   45: public string QueryEnginePython { get; set; } = "";
   48: public string QueryEngineEnvironmentFile { get; set; } = "";
   51: public string VibeQuantRoot { get; set; } = "";
   54: public string VibeQuantPython { get; set; } = "";
   57: public string CspPython { get; set; } = "";
   60: public string UpstreamLockPath { get; set; } = "";
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/StrategyAgent/StrategyAgentServiceCollectionExtensions.cs
```cs
    8: public static class StrategyAgentServiceCollectionExtensions
   14: public static IServiceCollection AddStrategyAgent(
```
