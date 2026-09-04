# TradingTerminal.Infrastructure / Alpaca — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Pipeline/TradingTerminal.Infrastructure/Alpaca/RealAlpacaClient.cs
```cs
   34: public sealed class RealAlpacaClient : IBrokerClient
   46: public RealAlpacaClient(ILogger<RealAlpacaClient> logger, IOptions<AlpacaOptions> options)
   52: public BrokerKind Kind => BrokerKind.Alpaca;
   54: public IObservable<ConnectionState> ConnectionState => _state.AsObservable();
   56: public async Task ConnectAsync(CancellationToken ct = default)
  139: public async Task<IReadOnlyList<TradableInstrument>> ListInstrumentsAsync(CancellationToken ct = default)
  217: public async Task DisconnectAsync(CancellationToken ct = default)
  235: public async Task<IReadOnlyList<Bar>> RequestHistoricalBarsAsync(
  268: public async IAsyncEnumerable<Bar> SubscribeBarsAsync(
  319: public async IAsyncEnumerable<Tick> SubscribeTicksAsync(
  405: public IAsyncEnumerable<DepthSnapshot> SubscribeDepthAsync(
  410: public IAsyncEnumerable<TradeTick> SubscribeTradesAsync(
  475: public async ValueTask DisposeAsync()
```
