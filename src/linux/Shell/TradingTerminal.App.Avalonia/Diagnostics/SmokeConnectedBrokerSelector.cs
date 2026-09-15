using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;

namespace TradingTerminal.App.Avalonia.Diagnostics;

/// <summary>
/// Headless smoke helper: reports <paramref name="kind"/> as already connected so Harness Start
/// does not require a real login under <c>--bypass-login</c>.
/// </summary>
internal sealed class SmokeConnectedBrokerSelector(BrokerKind kind) : IBrokerSelector
{
    private readonly IBrokerClient _client = new SmokeConnectedBrokerClient(kind);

    public IReadOnlyList<BrokerKind> AvailableKinds => [kind];
    public bool IsAvailable(BrokerKind candidate) => candidate == kind;
    public IReadOnlyList<BrokerKind> Connected => [kind];
    public bool IsConnected(BrokerKind candidate) => candidate == kind;
    public IBrokerClient Get(BrokerKind candidate) =>
        candidate == kind ? _client : throw new KeyNotFoundException();
    public BrokerConnectionMode ModeOf(BrokerKind candidate) => new(candidate, false, "Smoke", "Smoke");
    public IObservable<ConnectionState> StateOf(BrokerKind candidate) =>
        new SmokeImmediateObservable<ConnectionState>(CurrentStateOf(candidate));
    public ConnectionState CurrentStateOf(BrokerKind candidate) =>
        candidate == kind ? ConnectionState.Connected : ConnectionState.Disconnected;
    public event EventHandler<BrokerStateChangedEventArgs>? StateChanged;
    public Task ConnectAsync(BrokerKind candidate, CancellationToken ct = default) => Task.CompletedTask;
    public Task DisconnectAsync(BrokerKind candidate, CancellationToken ct = default) => Task.CompletedTask;
}

file sealed class SmokeConnectedBrokerClient(BrokerKind kind) : IBrokerClient
{
    public BrokerKind Kind => kind;
    public IObservable<ConnectionState> ConnectionState { get; } =
        new SmokeImmediateObservable<ConnectionState>(
            TradingTerminal.Core.Domain.ConnectionState.Connected);
    public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<TradableInstrument>> ListInstrumentsAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<TradableInstrument>>([]);
    public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<Bar>> RequestHistoricalBarsAsync(
        Contract contract, BarSize barSize, TimeSpan duration, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<Bar>>([]);
    public IAsyncEnumerable<Bar> SubscribeBarsAsync(
        Contract contract, BarSize barSize, CancellationToken ct = default) => EmptyAsync<Bar>();
    public IAsyncEnumerable<Tick> SubscribeTicksAsync(
        Contract contract, CancellationToken ct = default) => EmptyAsync<Tick>();
    public IAsyncEnumerable<DepthSnapshot> SubscribeDepthAsync(
        Contract contract, int levels = 10, CancellationToken ct = default) => EmptyAsync<DepthSnapshot>();
    public IAsyncEnumerable<TradeTick> SubscribeTradesAsync(
        Contract contract, CancellationToken ct = default) => EmptyAsync<TradeTick>();
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async IAsyncEnumerable<T> EmptyAsync<T>()
    {
        await Task.CompletedTask;
        yield break;
    }
}

file sealed class SmokeImmediateObservable<T>(T value) : IObservable<T>
{
    public IDisposable Subscribe(IObserver<T> observer)
    {
        observer.OnNext(value);
        observer.OnCompleted();
        return SmokeEmptyDisposable.Instance;
    }
}

file sealed class SmokeEmptyDisposable : IDisposable
{
    public static readonly SmokeEmptyDisposable Instance = new();
    public void Dispose() { }
}
