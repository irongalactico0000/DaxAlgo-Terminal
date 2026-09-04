# TradingTerminal.Infrastructure / CTrader — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Pipeline/TradingTerminal.Infrastructure/CTrader/CTraderAccountDiscoveryService.cs
```cs
   16: public sealed class CTraderAccountDiscoveryService : ICTraderAccountDiscovery
   22: public CTraderAccountDiscoveryService(ILogger<CTraderAccountDiscoveryService> logger)
   27: public async Task<IReadOnlyList<CTraderDiscoveredAccount>> DiscoverAsync(
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/CTrader/RealCTraderClient.cs
```cs
   27: public sealed class RealCTraderClient : IBrokerClient
   46: public RealCTraderClient(ILogger<RealCTraderClient> logger, IOptions<CTraderOptions> options)
   52: public BrokerKind Kind => BrokerKind.CTrader;
   54: public IObservable<ConnectionState> ConnectionState => _state.AsObservable();
   56: public async Task ConnectAsync(CancellationToken ct = default)
  149: public Task DisconnectAsync(CancellationToken ct = default)
  163: public Task<IReadOnlyList<TradableInstrument>> ListInstrumentsAsync(CancellationToken ct = default)
  258: public async Task<IReadOnlyList<Bar>> RequestHistoricalBarsAsync(
  294: public async IAsyncEnumerable<Bar> SubscribeBarsAsync(
  345: public async IAsyncEnumerable<Tick> SubscribeTicksAsync(
  411: public async IAsyncEnumerable<DepthSnapshot> SubscribeDepthAsync(
  545: public IAsyncEnumerable<TradeTick> SubscribeTradesAsync(
  636: public ValueTask DisposeAsync()
  655: public MessageDispatcher(RealCTraderClient owner) => _owner = owner;
  657: public void OnNext(IMessage value)
  670: public void OnError(Exception error)
  676: public void OnCompleted()
```
