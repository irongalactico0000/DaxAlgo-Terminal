# TradingTerminal.Core / Events — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Core/TradingTerminal.Core/Events/EventBus.cs
```cs
    5: public sealed class EventBus : IEventBus
   10: public IDisposable Subscribe<T>(Action<T> handler)
   21: public void Publish<T>(T evt)
   39: public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
   43: public sealed record StrategyOpenedEvent(string StrategyId, string DisplayName);
   44: public sealed record ConnectionStateChangedEvent(Domain.ConnectionState State, string? Reason = null);
```

## src/linux/Core/TradingTerminal.Core/Events/IEventBus.cs
```cs
    7: public interface IEventBus
    9:     IDisposable Subscribe<T>(Action<T> handler);
   10:     void Publish<T>(T evt);
```
