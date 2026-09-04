using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using TradingTerminal.App.Avalonia.Execution;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.Infrastructure.Execution;
using TradingTerminal.UI.Execution;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class PaperExecutionDesktopSessionTests
{
    [AvaloniaFact]
    public async Task Authenticated_strategy_retarget_cancels_working_order_before_submitting_new_delta()
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-desktop-retarget-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var clock = new MutableClock(new DateTime(2026, 8, 25, 1, 45, 0, DateTimeKind.Utc));
        var registry = new MemoryRegistry();
        try
        {
            using var session = PaperExecutionDesktopSession.CreateForLedger(
                Path.Combine(directory, "orders.db"),
                clock,
                registry,
                new FixedSecretStore());
            var instrument = registry.Resolve(BrokerKind.Simulated, "SPY")!.Value;
            var strategyId = new StrategyId("retarget-strategy");
            using var intake = new AuthenticatedPaperExecutionBookTargetIntake(
                "retarget-book",
                strategyId,
                new StrategyVersion("1.0.0"),
                session.Client,
                session,
                clock,
                ResolvePrice);

            var resting = await intake.SubmitTargetAsync(
                "retarget-book",
                Target(2, entryLimit: new ScaledPrice(90, 0)));
            var retargeted = await intake.SubmitTargetAsync("retarget-book", Target(3));

            Assert.True(resting.IsSuccess, resting.Message);
            Assert.True(retargeted.IsSuccess, retargeted.Message);
            Assert.Contains("cancelled the conflicting order", retargeted.Message, StringComparison.Ordinal);
            var snapshot = session.Client.GetSnapshot();
            Assert.Equal(2, snapshot.Orders.Count);
            Assert.Equal(
                OrderLifecycleState.Cancelled,
                Assert.Single(snapshot.Orders, order => order.Terms.Type == OrderType.Limit).State);
            Assert.Equal(
                OrderLifecycleState.Filled,
                Assert.Single(snapshot.Orders, order => order.Terms.Type == OrderType.Market).State);
            Assert.Equal(
                ExecutionNumericBoundary.QuantityFromDecimal(3m),
                Assert.Single(snapshot.Economics.Positions).Quantity);

            TradeIntent Target(long units, ScaledPrice? entryLimit = null) =>
                new(
                    instrument,
                    TradeIntentQuantityMode.TargetPosition,
                    ScaledQuantity.FromWhole(units),
                    ProtectiveStopPrice: null,
                    ProfitTargetPrice: null,
                    ScaledMoney.Zero,
                    strategyId.Value,
                    StrategyNoteId: 0,
                    PolicyVersion: "desktop-retarget-v1",
                    EntryLimitPrice: entryLimit,
                    EntryStopPrice: null);

            bool ResolvePrice(
                InstrumentId requested,
                out ScaledPrice price,
                out DateTimeOffset observedAtUtc)
            {
                price = new ScaledPrice(100, 0);
                observedAtUtc = new DateTimeOffset(clock.UtcNow);
                return requested == instrument;
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [AvaloniaFact]
    public async Task Actual_desktop_composition_submits_persists_recovers_and_kill_flattens()
    {
        var directory = Path.Combine(Path.GetTempPath(), "daxalgo-desktop-paper-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "orders.db");
        var clock = new MutableClock(new DateTime(2026, 8, 24, 9, 0, 0, DateTimeKind.Utc));
        var registry = new MemoryRegistry();
        try
        {
            var secretStore = new FixedSecretStore();
            using (var session = PaperExecutionDesktopSession.CreateForLedger(path, clock, registry, secretStore))
            using (var viewModel = new PaperExecutionConsoleViewModel(session.Client, session))
            {
                viewModel.SelectedInstrument = viewModel.Instruments.Single(item => item.Symbol == "SPY");
                viewModel.Quantity = "2";
                viewModel.MarkPrice = "100.25";

                await viewModel.RefreshCommand.ExecuteAsync(null);
                Assert.True(viewModel.CanIssueCommands, viewModel.LastMessage);
                await viewModel.SubmitCommand.ExecuteAsync(null);

                var snapshot = session.Client.GetSnapshot();
                Assert.Single(snapshot.Orders);
                Assert.Equal(OrderLifecycleState.Filled, snapshot.Orders[0].State);
                Assert.Equal(ExecutionNumericBoundary.QuantityFromDecimal(2m), snapshot.Economics.Positions.Single().Quantity);
                Assert.Equal(ExecutionNumericBoundary.MoneyFromDecimal(-200.50m), snapshot.Economics.Cash.Single().Total);
            }

            clock.UtcNow = clock.UtcNow.AddSeconds(1);
            using (var recovered = PaperExecutionDesktopSession.CreateForLedger(path, clock, registry, secretStore))
            using (var viewModel = new PaperExecutionConsoleViewModel(recovered.Client, recovered))
            {
                await viewModel.RefreshCommand.ExecuteAsync(null);
                Assert.Equal("1", viewModel.OrderCount);
                Assert.Equal("1", viewModel.PositionCount);

                // A fresh ticket after restart must not reuse paper-client-1. Keep it resting so
                // Kill also proves the normal OMS cancel path before flattening the recovered fill.
                viewModel.SelectedInstrument = viewModel.Instruments.Single(item => item.Symbol == "SPY");
                viewModel.SelectedOrderType = OrderType.Limit;
                viewModel.Quantity = "1";
                viewModel.MarkPrice = "100.25";
                viewModel.LimitPrice = "90";
                await viewModel.SubmitCommand.ExecuteAsync(null);
                Assert.True(viewModel.LastOperationSucceeded, viewModel.LastMessage);

                var beforeKill = recovered.Client.GetSnapshot();
                Assert.Equal(2, beforeKill.Orders.Count);
                Assert.Contains(beforeKill.Orders, item =>
                    item.ClientOrderId.Value == "paper-client-2" &&
                    item.State == OrderLifecycleState.Working);

                await viewModel.KillCommand.ExecuteAsync(null);
                Assert.Equal(0, recovered.Client.GetSnapshot().Orders.Count(item => item.SubmitCommand.Terms.ReduceOnly));
                await viewModel.KillCommand.ExecuteAsync(null);

                var killed = recovered.Client.GetSnapshot();
                Assert.True(killed.IntakePaused);
                var cases = recovered.Runtime.Ledger.Read(killed.Resource);
                Assert.True(
                    killed.Economics.Positions.Single().Quantity == ScaledQuantity.Zero,
                    viewModel.LastMessage + " Cases: " + string.Join("; ", cases.Select(item =>
                        $"{item.SubjectKind}/{item.Kind}/{item.Status}: {item.LocalEvidence} <> {item.BrokerEvidence}")));
                Assert.Contains(killed.Orders, item => item.SubmitCommand.Terms.ReduceOnly && item.State == OrderLifecycleState.Filled);
                Assert.All(viewModel.Orders, item => Assert.Equal("SPY", item.Instrument));
                Assert.All(viewModel.Fills, item => Assert.Equal("SPY", item.Instrument));

                var capturePath = Environment.GetEnvironmentVariable("DAXALGO_PAPER_CONSOLE_CAPTURE");
                if (!string.IsNullOrWhiteSpace(capturePath))
                {
                    var window = new PaperExecutionConsoleWindow { DataContext = viewModel };
                    try
                    {
                        window.Show();
                        Dispatcher.UIThread.RunJobs();
                        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                        var frame = window.CaptureRenderedFrame();
                        Assert.NotNull(frame);
                        frame.Save(capturePath);
                    }
                    finally
                    {
                        window.Close();
                    }
                }
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class FixedSecretStore : IExecutionServiceSecretStore
    {
        private readonly byte[] _secret = Enumerable.Range(0, ExecutionIpcProtocol.SecretSize)
            .Select(value => checked((byte)value))
            .ToArray();

        public byte[] LoadOrCreate() => (byte[])_secret.Clone();
    }

    private sealed class MemoryRegistry : IInstrumentRegistry
    {
        private readonly Dictionary<int, Instrument> _instruments = [];
        private readonly Dictionary<(BrokerKind Broker, string Symbol), InstrumentId> _aliases = [];
        private int _next;

        public Instrument? Get(InstrumentId id) => _instruments.GetValueOrDefault(id.Value);
        public InstrumentId? Resolve(BrokerKind broker, string brokerSymbol) =>
            _aliases.TryGetValue((broker, brokerSymbol), out var value) ? value : null;

        public InstrumentId ResolveOrCreate(Contract contract, BrokerKind broker)
        {
            if (_aliases.TryGetValue((broker, contract.Symbol), out var existing)) return existing;
            var id = new InstrumentId(++_next);
            _instruments.Add(id.Value, new Instrument(
                id,
                contract.Symbol,
                AssetClass.Equity,
                contract.PrimaryExchange,
                contract.Currency,
                0.01,
                1));
            _aliases.Add((broker, contract.Symbol), id);
            return id;
        }

        public string? ToBrokerSymbol(InstrumentId id, BrokerKind broker) =>
            _aliases.FirstOrDefault(item => item.Key.Broker == broker && item.Value == id).Key.Symbol;
        public void RegisterAlias(InstrumentAlias alias) => _aliases[(alias.Broker, alias.BrokerSymbol)] = alias.InstrumentId;
        public IReadOnlyList<Instrument> All() => _instruments.Values.OrderBy(item => item.Id.Value).ToArray();
    }
}
