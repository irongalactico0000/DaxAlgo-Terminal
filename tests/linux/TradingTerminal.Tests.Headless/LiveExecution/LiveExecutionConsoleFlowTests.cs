using FluentAssertions;
using TradingTerminal.Core.Time;
using TradingTerminal.Execution;
using TradingTerminal.Execution.Alpaca;
using TradingTerminal.Execution.Oms;
using TradingTerminal.ExecutionUi;
using Xunit;

namespace TradingTerminal.Tests.Headless.LiveExecution;

/// <summary>
/// The macOS live-parity path with the network replaced: an explicitly registered broker adapter
/// connects, a Real book binds to it through <c>LiveBrokerBookRuntime</c>, and a limit ticket reaches
/// the OMS. Everything below the transport is the shipped code, so a regression in the gates — mode,
/// certification, lease, reconciliation, or risk — fails this test rather than an operator's account.
/// </summary>
public sealed class LiveExecutionConsoleFlowTests
{
    private const int InstrumentId = 7101;
    private const string Symbol = "AAPL";
    private static readonly DateTime NowUtc = new(2026, 3, 5, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Connect_then_real_book_then_limit_ticket_reaches_the_oms()
    {
        await using var harness = PaperHarness();

        var connected = await harness.Client.ConnectAdapterAsync(
            new ExecutionAdapterConnectRequest(
                AlpacaExecutionAdapter.StableAdapterId,
                KeyId: "test-key",
                SecretKey: "test-secret"));
        connected.IsSuccess.Should().BeTrue(connected.Message);

        var adapter = harness.Client.GetSnapshot().Adapters
            .Single(item => item.Id == AlpacaExecutionAdapter.StableAdapterId);
        adapter.IsConnected.Should().BeTrue();
        adapter.CanCreateBook.Should().BeTrue();
        adapter.Mode.Should().Be(ExecutionMode.Paper);

        var created = await harness.Client.CreateBookAsync(new ExecutionBookCreateRequest(
            "Alpaca parity",
            AlpacaExecutionAdapter.StableAdapterId,
            Array.Empty<string>()));
        created.IsSuccess.Should().BeTrue(created.Message);

        var book = harness.Client.GetSnapshot().Books.Single();
        book.AdapterId.Should().Be(AlpacaExecutionAdapter.StableAdapterId);
        book.TradableInstruments.Should().ContainSingle()
            .Which.Instrument.Value.Should().Be(InstrumentId);
        book.CanSubmitManualOrder.Should().BeTrue();

        var submitted = await harness.Client.SubmitManualOrderAsync(new ExecutionManualOrderRequest(
            book.Id,
            book.TradableInstruments[0].Instrument,
            Symbol,
            ExecutionManualOrderSide.Buy,
            ScaledQuantity.FromWhole(1),
            ExecutionManualOrderType.Limit,
            new ScaledPrice(19_000, 2)));

        submitted.IsSuccess.Should().BeTrue(submitted.Message);
        harness.Transport.SubmittedOrders.Should().ContainSingle();
        harness.Transport.SubmittedOrders[0].OrderType.Should().Be("limit");
        harness.Client.GetSnapshot().Books.Single().Orders.Should().ContainSingle()
            .Which.OrderType.Should().Be("Limit");
    }

    [Fact]
    public async Task Real_book_is_refused_while_the_adapter_is_not_execution_authenticated()
    {
        await using var harness = PaperHarness();

        var created = await harness.Client.CreateBookAsync(new ExecutionBookCreateRequest(
            "Alpaca parity",
            AlpacaExecutionAdapter.StableAdapterId,
            Array.Empty<string>()));

        created.IsSuccess.Should().BeFalse();
        harness.Client.GetSnapshot().Books.Should().BeEmpty();
    }

    private static Harness PaperHarness()
    {
        var accountId = $"PA-FLOW-{Guid.NewGuid():N}";
        var options = new AlpacaExecutionOptions
        {
            Enabled = true,
            Mode = ExecutionMode.Paper,
            Symbol = Symbol,
            CanonicalInstrumentId = InstrumentId,
            KeyId = "test-key",
            SecretKey = "test-secret",
            ExpectedAccountId = accountId,
        };
        var clock = new FixedClock(NowUtc);
        var transport = new FakeAlpacaTransport(AlpacaExecutionEndpointGate.Resolve(options), accountId);
        var scheduler = new AlpacaSerializedEventScheduler();
        var adapter = new AlpacaExecutionAdapter(
            options,
            transport,
            new FakeTradeUpdateSource(),
            clock,
            scheduler);
        var client = new InProcessExecutionClient(
            registeredAdapters: [adapter],
            liveConfirmationStore: new InMemoryLiveExecutionConfirmationStore(),
            alpacaOptions: options,
            executionClock: clock,
            executionLeaseStore: new InMemoryExecutionLeaseStore());
        return new Harness(client, adapter, scheduler, transport);
    }

    private sealed record Harness(
        InProcessExecutionClient Client,
        AlpacaExecutionAdapter Adapter,
        AlpacaSerializedEventScheduler Scheduler,
        FakeAlpacaTransport Transport) : IAsyncDisposable
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

    /// <summary>
    /// One healthy, unencumbered Alpaca paper account: no working orders, no positions, and exactly
    /// one cash snapshot, which is what the live book runtime requires to open.
    /// </summary>
    private sealed class FakeAlpacaTransport(AlpacaExecutionEndpoint endpoint, string accountId) : IAlpacaExecutionTransport
    {
        private readonly List<AlpacaOrderSnapshot> _orders = [];

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
                accountId,
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
            var accepted = new AlpacaOrderSnapshot(
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
                NowUtc);
            _orders.Add(accepted);
            return Task.FromResult(accepted);
        }

        public Task CancelOrderAsync(string orderId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<AlpacaOrderSnapshot> ReplaceOrderAsync(
            string orderId,
            AlpacaReplaceRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_orders.Single(order => order.OrderId == orderId));

        public Task<AlpacaOrderSnapshot?> GetOrderByIdAsync(string orderId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_orders.FirstOrDefault(order => order.OrderId == orderId));

        public Task<AlpacaOrderSnapshot?> GetOrderByClientIdAsync(
            string clientOrderId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(_orders.FirstOrDefault(order => order.ClientOrderId == clientOrderId));

        // Reconciliation reads the account before the book opens; reporting the just-submitted order
        // here would race the OMS projection, so this stays the empty startup view.
        public Task<IReadOnlyList<AlpacaOrderSnapshot>> GetOrdersAsync(
            AlpacaOrderStatusFilter status,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlpacaOrderSnapshot>>(Array.Empty<AlpacaOrderSnapshot>());

        public Task<IReadOnlyList<AlpacaPositionSnapshot>> GetPositionsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AlpacaPositionSnapshot>>(Array.Empty<AlpacaPositionSnapshot>());

        public ValueTask DisposeAsync()
        {
            IsConnected = false;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class FakeTradeUpdateSource : IAlpacaTradeUpdateSource
    {
        public bool IsRunning { get; private set; }

        public event Action<AlpacaOrderSnapshot>? OrderUpdated { add { } remove { } }

        public event Action<Exception>? Faulted { add { } remove { } }

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
