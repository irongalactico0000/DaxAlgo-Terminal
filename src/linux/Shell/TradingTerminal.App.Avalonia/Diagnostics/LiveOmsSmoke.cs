using TradingTerminal.Core.Time;
using TradingTerminal.Execution;
using TradingTerminal.Execution.Alpaca;
using TradingTerminal.Execution.Oms;
using TradingTerminal.ExecutionUi;

namespace TradingTerminal.App.Avalonia.Diagnostics;

/// <summary>
/// Headless live-OMS smoke (mocked Alpaca transport): Connect → Real book → Limit submit.
/// Invoke with <c>--smoke-live-oms</c> or <c>--smoke-live-paper-alpaca</c>
/// (optional <c>=/path/report.txt</c>). Does not hit the network; vendor Paper uses the Execution Console.
/// </summary>
internal static class LiveOmsSmoke
{
    private const int InstrumentId = 7101;
    private const string Symbol = "AAPL";
    private static readonly DateTime NowUtc = new(2026, 3, 5, 15, 0, 0, DateTimeKind.Utc);

    public static async Task<int> RunAsync(string reportPath)
    {
        var lines = new List<string>
        {
            $"Live OMS smoke — {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            "Flow: mock Alpaca Connect → Real CreateBook → Limit submit (no network)",
            string.Empty,
        };
        var exitCode = 1;
        try
        {
            var accountId = $"PA-SMOKE-{Guid.NewGuid():N}";
            var options = new AlpacaExecutionOptions
            {
                Enabled = true,
                Mode = ExecutionMode.Paper,
                Symbol = Symbol,
                CanonicalInstrumentId = InstrumentId,
                KeyId = "smoke-key",
                SecretKey = "smoke-secret",
                ExpectedAccountId = accountId,
            };
            var clock = new FixedClock(NowUtc);
            var endpoint = AlpacaExecutionEndpointGate.Resolve(options);
            var transport = new SmokeAlpacaTransport(endpoint, accountId);
            await using var scheduler = new AlpacaSerializedEventScheduler();
            await using var adapter = new AlpacaExecutionAdapter(
                options,
                transport,
                new SmokeTradeUpdateSource(),
                clock,
                scheduler);
            using var client = new InProcessExecutionClient(
                registeredAdapters: [adapter],
                liveConfirmationStore: new InMemoryLiveExecutionConfirmationStore(),
                alpacaOptions: options,
                executionClock: clock,
                executionLeaseStore: new InMemoryExecutionLeaseStore());

            var connected = await client.ConnectAdapterAsync(
                new ExecutionAdapterConnectRequest(
                    AlpacaExecutionAdapter.StableAdapterId,
                    KeyId: "smoke-key",
                    SecretKey: "smoke-secret"));
            lines.Add(connected.IsSuccess
                ? "PASS  Connect Alpaca Paper (mock)"
                : $"FAIL  Connect: {connected.Message}");
            if (!connected.IsSuccess)
                goto Done;

            var created = await client.CreateBookAsync(new ExecutionBookCreateRequest(
                "Smoke Real",
                AlpacaExecutionAdapter.StableAdapterId,
                Array.Empty<string>()));
            lines.Add(created.IsSuccess
                ? "PASS  CreateBook Real → LiveBrokerBookRuntime"
                : $"FAIL  CreateBook: {created.Message}");
            if (!created.IsSuccess)
                goto Done;

            var book = client.GetSnapshot().Books.Single();
            var submitted = await client.SubmitManualOrderAsync(new ExecutionManualOrderRequest(
                book.Id,
                book.TradableInstruments[0].Instrument,
                Symbol,
                ExecutionManualOrderSide.Buy,
                ScaledQuantity.FromWhole(1),
                ExecutionManualOrderType.Limit,
                new ScaledPrice(19_000, 2)));
            lines.Add(submitted.IsSuccess
                ? "PASS  Limit ticket reached OMS/transport"
                : $"FAIL  Submit: {submitted.Message}");
            exitCode = submitted.IsSuccess && transport.SubmittedOrders.Count == 1 ? 0 : 1;

            var binanceAdapters = typeof(AlpacaExecutionAdapter).Assembly.GetTypes()
                .Count(type => typeof(IBrokerExecutionAdapter).IsAssignableFrom(type) &&
                               type is { IsClass: true, IsAbstract: false } &&
                               type.Name.Contains("Binance", StringComparison.OrdinalIgnoreCase));
            lines.Add(binanceAdapters == 0
                ? "PASS  No Binance order adapter in live kernel"
                : "FAIL  Binance order adapter type present");
            if (binanceAdapters != 0)
                exitCode = 1;
        }
        catch (Exception exception)
        {
            lines.Add($"FAIL  {exception.GetType().Name}: {exception.Message}");
            exitCode = 1;
        }

    Done:
        lines.Add(string.Empty);
        lines.Add(exitCode == 0 ? "RESULT: PASS" : "RESULT: FAIL");
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllLinesAsync(reportPath, lines).ConfigureAwait(false);
        return exitCode;
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class SmokeAlpacaTransport(AlpacaExecutionEndpoint endpoint, string accountId) : IAlpacaExecutionTransport
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

    private sealed class SmokeTradeUpdateSource : IAlpacaTradeUpdateSource
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
