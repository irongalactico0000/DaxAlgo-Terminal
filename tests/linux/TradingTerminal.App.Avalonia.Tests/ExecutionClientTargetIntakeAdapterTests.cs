using FluentAssertions;
using TradingTerminal.App.Avalonia.Execution;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Execution;
using TradingTerminal.Execution.Oms;
using TradingTerminal.ExecutionUi;
using Xunit;
using CoreTradeIntent = TradingTerminal.Core.Execution.TradeIntent;
using ExecutionTradeIntent = TradingTerminal.Execution.TradeIntent;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class ExecutionClientTargetIntakeAdapterTests
{
    [Fact]
    public async Task LIVE_real_book_is_refused_before_oms_intake()
    {
        var client = new FakeExecutionClient(
            bookId: "real-1",
            mode: ExecutionMode.Live,
            adapterId: "alpaca-live",
            admissionOpen: true);
        using var adapter = new ExecutionClientTargetIntakeAdapter(client, "real-1");

        var result = await adapter.SubmitTargetAsync("real-1", SampleIntent());

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("LIVE");
        client.SubmitCalls.Should().Be(0);
    }

    [Fact]
    public async Task Broker_paper_real_book_forwards_to_execution_intake()
    {
        var client = new FakeExecutionClient(
            bookId: "real-1",
            mode: ExecutionMode.Paper,
            adapterId: "alpaca-paper",
            admissionOpen: true);
        using var adapter = new ExecutionClientTargetIntakeAdapter(client, "real-1");

        var result = await adapter.SubmitTargetAsync("real-1", SampleIntent());

        result.IsSuccess.Should().BeTrue(result.Message);
        client.SubmitCalls.Should().Be(1);
    }

    [Fact]
    public void Console_live_book_choice_cannot_start()
    {
        var book = MinimalBook("live-1", "alpaca-live", ExecutionMode.Live, admissionOpen: true);
        var choice = StrategyRunnerBookChoice.FromConsoleBook(book);
        choice.CanStart.Should().BeFalse();
        choice.IsLive.Should().BeTrue();
        choice.BlockReason.Should().Contain("LIVE");
    }

    [Fact]
    public void Console_broker_paper_book_can_start_when_gate_open()
    {
        var book = MinimalBook("real-1", "alpaca-paper", ExecutionMode.Paper, admissionOpen: true);
        var choice = StrategyRunnerBookChoice.FromConsoleBook(book);
        choice.CanStart.Should().BeTrue();
        choice.IsLive.Should().BeFalse();
    }

    private static CoreTradeIntent SampleIntent() =>
        new(
            new InstrumentId(7101),
            TradingTerminal.Core.Execution.TradeIntentQuantityMode.TargetPosition,
            TradingTerminal.Core.Execution.ScaledQuantity.FromWhole(2),
            ProtectiveStopPrice: null,
            ProfitTargetPrice: null,
            TradingTerminal.Core.Execution.ScaledMoney.Zero,
            "smoke",
            StrategyNoteId: 0,
            "sandbox-model-portfolio-v1");

    private static ExecutionBookReadModel MinimalBook(
        string id,
        string adapterId,
        ExecutionMode mode,
        bool admissionOpen) =>
        new(
            id,
            "Test",
            adapterId,
            adapterId,
            ["smoke"],
            "$0",
            ExecutionTone.Neutral,
            new ExecutionLeaseReadModel(ExecutionLeaseStatus.Held, 1, "ok"),
            IsIntakePaused: false,
            AdmissionOpen: admissionOpen,
            OpenRealPositionCount: 0,
            Positions: [],
            Orders: [],
            History: [],
            ReconciliationCases: [],
            Risk: new ExecutionRiskReadModel([], "ok"),
            LedgerEvents: [],
            Analytics: new ExecutionPortfolioAnalyticsReadModel(
                [],
                [],
                new ExecutionQualityReadModel(0, 0, 0, 0, 0, 0, 0, 0d, 0, 0d)),
            Mode: mode);

    private sealed class FakeExecutionClient : IExecutionClient, IExecutionBookTargetIntake
    {
        private readonly ExecutionBookReadModel _book;

        public FakeExecutionClient(string bookId, ExecutionMode mode, string adapterId, bool admissionOpen)
        {
            _book = MinimalBook(bookId, adapterId, mode, admissionOpen);
        }

        public int SubmitCalls { get; private set; }

        public event EventHandler? SnapshotInvalidated
        {
            add { }
            remove { }
        }

        public ExecutionConsoleSnapshot GetSnapshot() =>
            new(
                [],
                [_book],
                new ExecutionPortfolioAnalyticsReadModel(
                    [],
                    [],
                    new ExecutionQualityReadModel(0, 0, 0, 0, 0, 0, 0, 0d, 0, 0d)),
                DateTime.UtcNow,
                null);

        public ValueTask<ExecutionCommandResult> SetIntakePausedAsync(string bookId, bool paused, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ExecutionCommandResult.Success("ok"));

        public ValueTask<ExecutionCommandResult> ReconcileAsync(string bookId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ExecutionCommandResult.Success("ok"));

        public ValueTask<ExecutionCommandResult> KillAsync(string bookId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ExecutionCommandResult.Success("ok"));

        public ValueTask<ExecutionCommandResult> SetExecutionModeAsync(ExecutionModeChangeRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ExecutionCommandResult.Success("ok"));

        public ValueTask<ExecutionCommandResult> ConnectAdapterAsync(string adapterId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ExecutionCommandResult.Success("ok"));

        public ValueTask<ExecutionCommandResult> ConnectAdapterAsync(ExecutionAdapterConnectRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ExecutionCommandResult.Success("ok"));

        public ValueTask<ExecutionCommandResult> DisconnectAdapterAsync(string adapterId, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ExecutionCommandResult.Success("ok"));

        public ValueTask<ExecutionCommandResult> CreateBookAsync(ExecutionBookCreateRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ExecutionCommandResult.Success("ok"));

        public ValueTask<ExecutionCommandResult> SubmitManualOrderAsync(ExecutionManualOrderRequest request, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ExecutionCommandResult.Success("ok"));

        public ValueTask<ExecutionTargetSubmissionResult> SubmitTargetAsync(
            string bookId,
            ExecutionTradeIntent intent,
            CancellationToken cancellationToken = default)
        {
            SubmitCalls++;
            return ValueTask.FromResult(ExecutionTargetSubmissionResult.Success("forwarded"));
        }

        public void Dispose()
        {
        }
    }
}
