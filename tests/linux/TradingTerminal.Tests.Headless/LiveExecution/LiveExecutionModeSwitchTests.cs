using FluentAssertions;
using TradingTerminal.Core.Time;
using TradingTerminal.Execution;
using TradingTerminal.Execution.Alpaca;
using TradingTerminal.Execution.Oms;
using TradingTerminal.ExecutionUi;
using Xunit;

namespace TradingTerminal.Tests.Headless.LiveExecution;

/// <summary>
/// Operator Switch-to-LIVE must fail closed without typed LIVE + AllowLiveExecution, and only then
/// reconstruct the adapter on the live endpoint with a persisted confirmation.
/// </summary>
public sealed class LiveExecutionModeSwitchTests
{
    private const int InstrumentId = 7101;
    private const string Symbol = "AAPL";
    private const string AccountId = "PA-LIVE-ACCOUNT";
    private static readonly DateTime NowUtc = new(2026, 3, 5, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Switch_to_LIVE_is_refused_without_typed_LIVE_acknowledgement()
    {
        await using var harness = await PaperHarnessAsync(allowLiveExecution: true);

        var result = await harness.Client.SetExecutionModeAsync(new ExecutionModeChangeRequest(
            AlpacaExecutionAdapter.StableAdapterId,
            AccountId,
            ExecutionMode.Live,
            typedConfirmation: "live",
            keyId: "live-key",
            secretKey: "live-secret"));

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("LIVE");
        harness.Client.GetSnapshot().Adapters
            .Single(item => item.Id == AlpacaExecutionAdapter.StableAdapterId)
            .Mode.Should().Be(ExecutionMode.Paper);
        harness.Store.Read(AlpacaExecutionOptions.BrokerId, AccountId).Should().BeNull();
    }

    [Fact]
    public async Task Switch_to_LIVE_is_refused_when_AllowLiveExecution_is_false()
    {
        await using var harness = await PaperHarnessAsync(allowLiveExecution: false);

        var result = await harness.Client.SetExecutionModeAsync(new ExecutionModeChangeRequest(
            AlpacaExecutionAdapter.StableAdapterId,
            AccountId,
            ExecutionMode.Live,
            typedConfirmation: LiveExecutionConfirmation.RequiredAcknowledgement,
            keyId: "live-key",
            secretKey: "live-secret"));

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Match(message =>
            message.Contains("AllowLiveExecution", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("PAPER", StringComparison.Ordinal));
        harness.Client.GetSnapshot().Adapters
            .Single(item => item.Id.Contains("alpaca", StringComparison.OrdinalIgnoreCase))
            .Mode.Should().Be(ExecutionMode.Paper);
    }

    [Fact]
    public async Task Switch_to_LIVE_persists_confirmation_and_rebuilds_on_live_endpoint()
    {
        await using var harness = await PaperHarnessAsync(allowLiveExecution: true);

        var result = await harness.Client.SetExecutionModeAsync(new ExecutionModeChangeRequest(
            AlpacaExecutionAdapter.StableAdapterId,
            AccountId,
            ExecutionMode.Live,
            typedConfirmation: LiveExecutionConfirmation.RequiredAcknowledgement,
            keyId: "live-key",
            secretKey: "live-secret"));

        result.IsSuccess.Should().BeTrue(result.Message);
        var adapter = harness.Client.GetSnapshot().Adapters
            .Single(item => item.Id.Contains("alpaca", StringComparison.OrdinalIgnoreCase));
        adapter.Mode.Should().Be(ExecutionMode.Live);
        adapter.IsLive.Should().BeTrue();
        harness.Store.Read(AlpacaExecutionOptions.BrokerId, AccountId).Should().NotBeNull();
        harness.Store.Read(AlpacaExecutionOptions.BrokerId, AccountId)!.Acknowledgement
            .Should().Be(LiveExecutionConfirmation.RequiredAcknowledgement);

        harness.Store.Remove(AlpacaExecutionOptions.BrokerId, AccountId).Should().BeTrue();
        harness.Store.Read(AlpacaExecutionOptions.BrokerId, AccountId).Should().BeNull();
    }

    private static async Task<Harness> PaperHarnessAsync(bool allowLiveExecution)
    {
        var options = new AlpacaExecutionOptions
        {
            Enabled = true,
            Mode = ExecutionMode.Paper,
            AllowLiveExecution = allowLiveExecution,
            Symbol = Symbol,
            CanonicalInstrumentId = InstrumentId,
            KeyId = "paper-key",
            SecretKey = "paper-secret",
        };
        var clock = new FixedClock(NowUtc);
        var store = new InMemoryLiveExecutionConfirmationStore();
        var transport = new FakeAlpacaTransport(AlpacaExecutionEndpointGate.Resolve(options));
        var scheduler = new AlpacaSerializedEventScheduler();
        var adapter = new AlpacaExecutionAdapter(
            options,
            transport,
            new FakeTradeUpdateSource(),
            clock,
            scheduler);
        var client = new InProcessExecutionClient(
            registeredAdapters: [adapter],
            liveConfirmationStore: store,
            alpacaOptions: options,
            executionClock: clock,
            executionLeaseStore: new InMemoryExecutionLeaseStore());
        await Task.CompletedTask;
        return new Harness(client, adapter, scheduler, transport, store);
    }

    private sealed record Harness(
        InProcessExecutionClient Client,
        AlpacaExecutionAdapter Adapter,
        AlpacaSerializedEventScheduler Scheduler,
        FakeAlpacaTransport Transport,
        InMemoryLiveExecutionConfirmationStore Store) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Adapter.DisposeAsync();
            await Scheduler.DisposeAsync();
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeAlpacaTransport(AlpacaExecutionEndpoint endpoint) : IAlpacaExecutionTransport
    {
        public List<AlpacaSubmitRequest> SubmittedOrders { get; } = [];

        public AlpacaExecutionEndpoint Endpoint { get; } = endpoint;

        public bool IsConnected { get; private set; }

        public Task ConnectAsync(string keyId, string secretKey, CancellationToken cancellationToken = default)
        {
            IsConnected = true;
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken cancellationToken = default)
        {
            IsConnected = false;
            return Task.CompletedTask;
        }

        public Task<AlpacaAccountSnapshot> GetAccountAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AlpacaAccountSnapshot(
                "PA-TEST",
                "ACTIVE",
                "USD",
                new ScaledMoney(100_000_00, 2),
                new ScaledMoney(200_000_00, 2),
                TradingBlocked: false,
                AccountBlocked: false));

        public Task<AlpacaAssetSnapshot> GetAssetAsync(string symbol, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AlpacaAssetSnapshot(
                symbol,
                "us_equity",
                Tradable: true,
                Fractionable: false,
                MinimumOrderSize: null,
                MinimumTradeIncrement: null,
                PriceIncrement: new ScaledPrice(1, 2)));

        public Task<AlpacaLatestTrade?> GetLatestTradeAsync(string symbol, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlpacaLatestTrade?>(new AlpacaLatestTrade(new ScaledPrice(19_000, 2), NowUtc));

        public Task<AlpacaOrderSnapshot> SubmitOrderAsync(
            AlpacaSubmitRequest request,
            CancellationToken cancellationToken = default)
        {
            SubmittedOrders.Add(request);
            return Task.FromResult(new AlpacaOrderSnapshot(
                $"broker-{SubmittedOrders.Count}",
                request.ClientOrderId,
                request.Symbol,
                "us_equity",
                request.Side,
                request.OrderType,
                request.TimeInForce,
                "new",
                request.Quantity,
                ScaledQuantity.Zero,
                FilledAveragePrice: null,
                request.LimitPrice,
                request.StopPrice,
                NowUtc));
        }

        public Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AlpacaOrderSnapshot> ReplaceOrderAsync(
            string orderId,
            AlpacaReplaceRequest request,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("No replace.");

        public Task<AlpacaOrderSnapshot?> GetOrderByIdAsync(string orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AlpacaOrderSnapshot?>(null);

        public Task<AlpacaOrderSnapshot?> GetOrderByClientIdAsync(
            string clientOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<AlpacaOrderSnapshot?>(null);

        public Task<IReadOnlyList<AlpacaOrderSnapshot>> GetOrdersAsync(
            AlpacaOrderStatusFilter status,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlpacaOrderSnapshot>>([]);

        public Task<IReadOnlyList<AlpacaPositionSnapshot>> GetPositionsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlpacaPositionSnapshot>>([]);

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTradeUpdateSource : IAlpacaTradeUpdateSource
    {
        public bool IsRunning { get; private set; }

        public event Action<AlpacaOrderSnapshot>? OrderUpdated
        {
            add { }
            remove { }
        }

        public event Action<Exception>? Faulted
        {
            add { }
            remove { }
        }

        public Task StartAsync(IAlpacaExecutionTransport transport, CancellationToken cancellationToken = default)
        {
            IsRunning = true;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            IsRunning = false;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            IsRunning = false;
            return ValueTask.CompletedTask;
        }
    }
}
