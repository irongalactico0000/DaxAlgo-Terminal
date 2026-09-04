# TradingTerminal.Infrastructure / Notifications — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Pipeline/TradingTerminal.Infrastructure/Notifications/AiAnalystCredentialProtection.cs
```cs
   33: public static string? Encrypt(string? plaintext)
   76: public static string? Decrypt(string? cipherBase64)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Notifications/AiAnalystOptions.cs
```cs
    9: public sealed class AiAnalystOptions
   12: public bool Enabled { get; set; }
   15: public string Endpoint { get; set; } = "http://127.0.0.1:8765";
   18: public string Provider { get; set; } = "openai";
   21: public string Model { get; set; } = "gpt-4o";
   24: public string VisionModel { get; set; } = "gpt-4o";
   27: public int BarCount { get; set; } = 50;
   30: public int TimeoutSeconds { get; set; } = 60;
   33: public bool IncludeInEnricher { get; set; }
   38: public string? ApiKeyEncryptedBase64 { get; set; }
   42: public string? ApiKey
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Notifications/AllowAllSignalGate.cs
```cs
   12: public bool ShouldSuppress(StrategyNotification notification, out string? reason)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Notifications/Discord/DiscordTransport.cs
```cs
   16: public const string HttpClientName = "discord";
   22: public DiscordTransport(
   32: public string Name => "discord";
   34: public bool IsEnabled
   45: public async Task SendAsync(StrategyNotification notification, CancellationToken ct)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Notifications/NotificationDispatcher.cs
```cs
   27: public NotificationDispatcher(
   47: public ValueTask PublishAsync(StrategyNotification notification, CancellationToken ct = default)
   55: public Task StartAsync(CancellationToken cancellationToken)
   62: public async Task StopAsync(CancellationToken cancellationToken)
  122: public void Dispose()
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Notifications/NotificationFormatter.cs
```cs
   11: public static string ToTelegramHtml(StrategyNotification n)
   36: public static string ToDiscordMarkdown(StrategyNotification n)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Notifications/NotificationsOptions.cs
```cs
    5: public sealed class NotificationsOptions
    7: public const string SectionName = "Notifications";
    9: public TelegramOptions Telegram { get; set; } = new();
   11: public DiscordOptions Discord { get; set; } = new();
   13: public OllamaOptions Ollama { get; set; } = new();
   15: public AiAnalystOptions AiAnalyst { get; set; } = new();
   21: public int QueueCapacity { get; set; } = 256;
   24: public sealed class TelegramOptions
   26: public bool Enabled { get; set; }
   29: public string BotToken { get; set; } = "";
   32: public string ChatId { get; set; } = "";
   35: public bool IncludeIdleSignals { get; set; }
   38: public sealed class OllamaOptions
   40: public bool Enabled { get; set; }
   43: public string Endpoint { get; set; } = "http://localhost:11434";
   46: public string Model { get; set; } = "llama3.2";
   53: public int TimeoutSeconds { get; set; } = 4;
   56: public string SystemPrompt { get; set; } = "";
   59: public sealed class DiscordOptions
   61: public bool Enabled { get; set; }
   64: public string WebhookUrl { get; set; } = "";
   67: public string Username { get; set; } = "DaxAlgo Terminal";
   70: public bool IncludeIdleSignals { get; set; }
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Notifications/NotificationsServiceCollectionExtensions.cs
```cs
   12: public static class NotificationsServiceCollectionExtensions
   20: public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Notifications/Ollama/OllamaCommentaryEnricher.cs
```cs
   22: public const string HttpClientName = "ollama";
   31: public OllamaCommentaryEnricher(
   41: public bool ShouldRun(StrategyNotification notification)
   51: public async Task<StrategyNotification> EnrichAsync(StrategyNotification notification, CancellationToken ct)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Notifications/Telegram/TelegramTransport.cs
```cs
   15: public const string HttpClientName = "telegram";
   21: public TelegramTransport(
   31: public string Name => "telegram";
   33: public bool IsEnabled
   44: public async Task SendAsync(StrategyNotification notification, CancellationToken ct)
```
