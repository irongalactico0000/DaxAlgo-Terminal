using FluentAssertions;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Time;
using TradingTerminal.Execution;
using TradingTerminal.Execution.Alpaca;
using TradingTerminal.Execution.Oms;
using TradingTerminal.ExecutionUi;
using TradingTerminal.Sandbox.Runtime;
using Xunit;
using ExecutionReplicator = TradingTerminal.Execution.SandboxExecutionReplicator;
using ExecutionReplicationOptions = TradingTerminal.Execution.SandboxExecutionReplicationOptions;

namespace TradingTerminal.Tests.Headless.LiveExecution;

/// <summary>
/// Strategy path into a Real book: <see cref="ExecutionReplicator"/> →
/// <see cref="IExecutionBookTargetIntake"/> on <see cref="InProcessExecutionClient"/> → OMS → venue.
/// Strategies never call a broker PlaceOrder API.
/// </summary>
public sealed class LiveExecutionReplicatorToRealBookTests
{
    private const int InstrumentId = 7101;
    private const string Symbol = "AAPL";
    private const string StrategyId = "smoke-strategy";
    private static readonly DateTime NowUtc = new(2026, 3, 5, 15, 0, 0, DateTimeKind.Utc);
    private static readonly InstrumentId Instrument = new(InstrumentId);

    [Fact]
    public async Task Replicator_committed_target_reaches_real_book_oms_transport()
    {
        await using var harness = await ConnectedRealBookAsync();

        var source = new ManualPortfolioSource();
        await using var replicator = new ExecutionReplicator(
            source,
            harness.Client,
            new ExecutionReplicationOptions(harness.BookId, StrategyId));
        var completion = new TaskCompletionSource<TradingTerminal.Execution.SandboxExecutionReplicationOutcome>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        replicator.SubmissionCompleted += outcome => completion.TrySetResult(outcome);

        source.Publish(new SandboxPortfolioSnapshot(
            Instrument,
            PositionUnits: 2d,
            PositionQuantity: 2d,
            AverageEntryPrice: 190d,
            BarsHeld: 1,
            Equity: 100_000d,
            RealizedGrossProfitLoss: 0d,
            CommissionTotal: 0d,
            SlippageTotal: 0d,
            EquityPeak: 100_000d,
            MaximumDrawdown: 0d,
            LifetimeClosedTripCount: 0,
            LifetimeWinningTripCount: 0,
            LifetimeLosingTripCount: 0,
            RetainedTradeCount: 0,
            Streak: 0,
            IsComplete: false));

        var outcome = await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        outcome.Result.IsSuccess.Should().BeTrue(outcome.Result.Message);
        harness.Transport.SubmittedOrders.Should().NotBeEmpty();
        harness.Transport.SubmittedOrders[^1].Quantity.Should().Be(ScaledQuantity.FromWhole(2));
    }

    private static async Task<Harness> ConnectedRealBookAsync()
    {
        var accountId = $"PA-REP-{Guid.NewGuid():N}";
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

        var connected = await client.ConnectAdapterAsync(
            new ExecutionAdapterConnectRequest(
                AlpacaExecutionAdapter.StableAdapterId,
                KeyId: "test-key",
                SecretKey: "test-secret"));
        connected.IsSuccess.Should().BeTrue(connected.Message);

        var created = await client.CreateBookAsync(new ExecutionBookCreateRequest(
            "Replicator Real",
            AlpacaExecutionAdapter.StableAdapterId,
            [StrategyId]));
        created.IsSuccess.Should().BeTrue(created.Message);
        var bookId = client.GetSnapshot().Books.Single().Id;
        return new Harness(client, adapter, scheduler, transport, bookId);
    }

    private sealed record Harness(
        InProcessExecutionClient Client,
        AlpacaExecutionAdapter Adapter,
        AlpacaSerializedEventScheduler Scheduler,
        FakeAlpacaTransport Transport,
        string BookId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await Adapter.DisposeAsync();
            await Scheduler.DisposeAsync();
        }
    }

    private sealed class ManualPortfolioSource : IModelPortfolioSource
    {
        public IModelPortfolio? CurrentSnapshot { get; private set; }

        public event Action<IModelPortfolio>? SnapshotChanged;

        public void Publish(IModelPortfolio snapshot)
        {
            CurrentSnapshot = snapshot;
            SnapshotChanged?.Invoke(snapshot);
        }
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FakeAlpacaTransport(AlpacaExecutionEndpoint endpoint, string accountId) : IAlpacaExecutionTransport
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
