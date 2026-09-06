# TradingTerminal.Infrastructure / NinjaTrader — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Pipeline/TradingTerminal.Infrastructure/NinjaTrader/RealNinjaClient.cs
```cs
   30: public sealed class RealNinjaClient : IBrokerClient
   40: public RealNinjaClient(ILogger<RealNinjaClient> logger, IOptions<NinjaTraderOptions> options)
   46: public BrokerKind Kind => BrokerKind.NinjaTrader;
   48: public IObservable<ConnectionState> ConnectionState => _state.AsObservable();
   50: public Task ConnectAsync(CancellationToken ct = default)
   84: public Task<IReadOnlyList<TradableInstrument>> ListInstrumentsAsync(CancellationToken ct = default) =>
   87: public Task DisconnectAsync(CancellationToken ct = default)
  103: public Task<IReadOnlyList<Bar>> RequestHistoricalBarsAsync(
  136: public async IAsyncEnumerable<Bar> SubscribeBarsAsync(
  196: public async IAsyncEnumerable<Tick> SubscribeTicksAsync(
  243: public IAsyncEnumerable<DepthSnapshot> SubscribeDepthAsync(
  252: public IAsyncEnumerable<TradeTick> SubscribeTradesAsync(
  305: public ValueTask DisposeAsync()
  325: public static extern int Connected(int showMessage);
  328: public static extern int SubscribeMarketData([MarshalAs(UnmanagedType.LPStr)] string instrument);
  331: public static extern int UnsubscribeMarketData([MarshalAs(UnmanagedType.LPStr)] string instrument);
  334: public static extern double Bid([MarshalAs(UnmanagedType.LPStr)] string instrument);
  337: public static extern double Ask([MarshalAs(UnmanagedType.LPStr)] string instrument);
  340: public static extern double LastPrice([MarshalAs(UnmanagedType.LPStr)] string instrument);
  343: public static extern long Volume([MarshalAs(UnmanagedType.LPStr)] string instrument);
  346: public static extern int Command(
```
