using TradingTerminal.Core.Execution;
using TradingTerminal.ExecutionUi;
using SandboxIntake = TradingTerminal.Sandbox.Runtime.IExecutionBookTargetIntake;
using SandboxResult = TradingTerminal.Sandbox.Runtime.ExecutionTargetSubmissionResult;
using ExecutionIntake = TradingTerminal.Execution.IExecutionBookTargetIntake;
using ExecutionTradeIntent = TradingTerminal.Execution.TradeIntent;
using ExecutionQuantityMode = TradingTerminal.Execution.TradeIntentQuantityMode;
using CoreTradeIntent = TradingTerminal.Core.Execution.TradeIntent;

namespace TradingTerminal.App.Avalonia.Execution;

/// <summary>
/// Bridges the Runner's <see cref="TradingTerminal.Sandbox.Runtime.SandboxExecutionReplicator"/>
/// (Sandbox.Runtime intake) onto the live-capable console client
/// (<see cref="TradingTerminal.Execution.IExecutionBookTargetIntake"/> / <see cref="InProcessExecutionClient"/>).
/// Fail-closed for LIVE books: strategy targets never arm a live endpoint from the Runner.
/// </summary>
public sealed class ExecutionClientTargetIntakeAdapter : SandboxIntake, IDisposable
{
    private readonly IExecutionClient _client;
    private readonly ExecutionIntake _intake;
    private readonly string _bookId;
    private int _disposed;

    public ExecutionClientTargetIntakeAdapter(IExecutionClient client, string bookId)
    {
        ArgumentNullException.ThrowIfNull(client);
        if (string.IsNullOrWhiteSpace(bookId))
            throw new ArgumentException("A bound execution book id is required.", nameof(bookId));
        if (client is not ExecutionIntake intake)
        {
            throw new ArgumentException(
                "The execution client must implement TradingTerminal.Execution.IExecutionBookTargetIntake.",
                nameof(client));
        }

        _client = client;
        _intake = intake;
        _bookId = bookId.Trim();
    }

    public async ValueTask<SandboxResult> SubmitTargetAsync(
        string bookId,
        CoreTradeIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return SandboxResult.Failure("Strategy target refused because the Real-book intake is disposed.");
        if (!string.Equals(bookId?.Trim(), _bookId, StringComparison.Ordinal))
            return SandboxResult.Failure("Strategy target refused because the execution-book binding does not match.");

        var book = _client.GetSnapshot().Books
            .FirstOrDefault(item => string.Equals(item.Id, _bookId, StringComparison.Ordinal));
        if (book is null)
            return SandboxResult.Failure("Strategy target refused because the Real book is no longer in the console.");
        if (book.IsLive || book.Mode == TradingTerminal.Execution.Oms.ExecutionMode.Live)
        {
            return SandboxResult.Failure(
                "Strategy target refused because LIVE Real books cannot receive Runner targets. " +
                "Arm LIVE only from the Execution Console after typed confirmation.");
        }
        if (string.Equals(book.AdapterId, "paper", StringComparison.Ordinal))
        {
            return SandboxResult.Failure(
                "Strategy target refused because the in-process Paper adapter is not a Real book route.");
        }
        if (!book.AdmissionOpen || book.IsIntakePaused)
            return SandboxResult.Failure("Strategy target refused because the Real book intake gate is closed.");

        var result = await _intake
            .SubmitTargetAsync(_bookId, ToExecutionIntent(intent), cancellationToken)
            .ConfigureAwait(false);
        return result.IsSuccess
            ? SandboxResult.Success(result.Message)
            : SandboxResult.Failure(result.Message);
    }

    internal static ExecutionTradeIntent ToExecutionIntent(CoreTradeIntent intent) =>
        new(
            intent.Instrument,
            (ExecutionQuantityMode)(byte)intent.QuantityMode,
            MapQuantity(intent.SignedUnits),
            MapPrice(intent.ProtectiveStopPrice),
            MapPrice(intent.ProfitTargetPrice),
            MapMoney(intent.EstimatedRoundTripCostPerUnit),
            intent.StrategyId,
            intent.StrategyNoteId,
            intent.PolicyVersion,
            MapPrice(intent.EntryLimitPrice),
            MapPrice(intent.EntryStopPrice));

    private static TradingTerminal.Execution.ScaledQuantity MapQuantity(
        TradingTerminal.Core.Execution.ScaledQuantity value) =>
        new(value.Coefficient, value.Scale);

    private static TradingTerminal.Execution.ScaledMoney MapMoney(
        TradingTerminal.Core.Execution.ScaledMoney value) =>
        new(value.Coefficient, value.Scale);

    private static TradingTerminal.Execution.ScaledPrice? MapPrice(
        TradingTerminal.Core.Execution.ScaledPrice? value) =>
        value is { } price ? new TradingTerminal.Execution.ScaledPrice(price.Coefficient, price.Scale) : null;

    public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
}
