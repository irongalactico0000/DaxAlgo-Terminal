# DaxAlgo.Coordinator.Cli — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Tools/DaxAlgo.Coordinator.Cli/CliApplication.cs
```cs
   12: public sealed class CliApplication(TextWriter output, TextWriter error)
   22: public async Task<int> RunAsync(IReadOnlyList<string> args, CancellationToken cancellationToken)
```

## src/linux/Tools/DaxAlgo.Coordinator.Cli/CliArguments.cs
```cs
   13: public IReadOnlyList<string> Positionals { get; }
   15: public static CliArguments Parse(IReadOnlyList<string> args)
   42: public bool Has(string name) => _options.ContainsKey(name);
   44: public string? Optional(string name) =>
   47: public string Required(string name) =>
   52: public IReadOnlyList<string> All(string name) =>
```

## src/linux/Tools/DaxAlgo.Coordinator.Cli/CoordinatorCliConfig.cs
```cs
    6: public sealed record CoordinatorCliConfig
    8: public const string CurrentSchemaVersion = "vibe-quant-client/v1";
   10: public string SchemaVersion { get; init; } = CurrentSchemaVersion;
   12: public string ServerBaseUrl { get; init; } = "http://127.0.0.1:5080";
   14: public CoordinatorClientAuthenticationConfig Authentication { get; init; } = new();
   17: public sealed record CoordinatorClientAuthenticationConfig
   19: public string Mode { get; init; } = "development";
   21: public string? AccessTokenEnvironmentVariable { get; init; }
   23: public string? DevelopmentSubject { get; init; } = "local-vibe-quant-operator";
   25: public string? DevelopmentEmail { get; init; } = "local-vibe-quant-operator@development.invalid";
   28: public static class CoordinatorCliConfigLoader
   30: public const int MaxConfigBytes = 1_000_000;
   32: public static async Task<CoordinatorCliConfig> LoadAsync(
   58: public static void Validate(CoordinatorCliConfig config)
```

## src/linux/Tools/DaxAlgo.Coordinator.Cli/CoordinatorRuntime.cs
```cs
   15: public CoordinatorCliConfig Config { get; }
   17: public HttpClient HttpClient { get; }
   19: public IVibeQuantApiClient Client { get; }
   21: public static async Task<CoordinatorRuntime> CreateAsync(
   63: public void Dispose() => HttpClient.Dispose();
```

## src/linux/Tools/DaxAlgo.Coordinator.Cli/Program.cs
```cs
    5: public static async Task<int> Main(string[] args)
```
