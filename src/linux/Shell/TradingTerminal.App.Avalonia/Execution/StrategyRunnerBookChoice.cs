using TradingTerminal.Execution.Oms;
using TradingTerminal.ExecutionUi;

namespace TradingTerminal.App.Avalonia.Execution;

/// <summary>One book the Strategy Runner may bind: local Paper OMS or a console Real book.</summary>
public enum StrategyRunnerBookKind : byte
{
    /// <summary>Authenticated IPC into the local SimulatedPaper book stack.</summary>
    LocalPaper = 0,

    /// <summary>
    /// Console Real book on a broker Paper endpoint (Alpaca Paper / cTrader Demo / IB Paper).
    /// LIVE Real books are listed but not startable from the Runner.
    /// </summary>
    ConsoleReal = 1,
}

/// <summary>Selectable Runner binding target with fail-closed LIVE eligibility.</summary>
public sealed record StrategyRunnerBookChoice(
    StrategyRunnerBookKind Kind,
    string BookId,
    string DisplayName,
    string ModeLabel,
    bool IsLive,
    bool CanStart,
    string BlockReason,
    IReadOnlyList<string> BoundStrategies)
{
    public string BadgeText => Kind switch
    {
        StrategyRunnerBookKind.LocalPaper => "PAPER OMS · AUTHENTICATED IPC",
        StrategyRunnerBookKind.ConsoleReal when IsLive => "LIVE BLOCKED · CONSOLE ONLY",
        StrategyRunnerBookKind.ConsoleReal => $"{ModeLabel} REAL BOOK · OMS",
        _ => ModeLabel,
    };

    public static StrategyRunnerBookChoice FromLocalPaper(PaperExecutionBookDefinition book) =>
        new(
            StrategyRunnerBookKind.LocalPaper,
            book.Id,
            $"Paper · {book.Name}",
            "PAPER",
            IsLive: false,
            CanStart: true,
            BlockReason: string.Empty,
            BoundStrategies: book.Strategies);

    public static StrategyRunnerBookChoice FromConsoleBook(ExecutionBookReadModel book)
    {
        ArgumentNullException.ThrowIfNull(book);
        var isInProcessPaper = string.Equals(book.AdapterId, "paper", StringComparison.Ordinal);
        if (isInProcessPaper)
        {
            return new StrategyRunnerBookChoice(
                StrategyRunnerBookKind.ConsoleReal,
                book.Id,
                $"{book.Name} · in-process Paper",
                book.ModeLabel,
                IsLive: false,
                CanStart: false,
                BlockReason: "Use the local Paper OMS book for SimulatedPaper, not the console Paper adapter.",
                BoundStrategies: book.Strategies);
        }

        if (book.IsLive || book.Mode == ExecutionMode.Live)
        {
            return new StrategyRunnerBookChoice(
                StrategyRunnerBookKind.ConsoleReal,
                book.Id,
                $"{book.Name} · {book.AdapterName}",
                book.ModeLabel,
                IsLive: true,
                CanStart: false,
                BlockReason: "LIVE Real books cannot be started from the Strategy Runner. Use the Execution Console after typed LIVE confirmation.",
                BoundStrategies: book.Strategies);
        }

        if (!book.AdmissionOpen)
        {
            return new StrategyRunnerBookChoice(
                StrategyRunnerBookKind.ConsoleReal,
                book.Id,
                $"{book.Name} · {book.AdapterName}",
                book.ModeLabel,
                IsLive: false,
                CanStart: false,
                BlockReason: "Real book admission is closed (connect the adapter and open the gate in the Execution Console).",
                BoundStrategies: book.Strategies);
        }

        return new StrategyRunnerBookChoice(
            StrategyRunnerBookKind.ConsoleReal,
            book.Id,
            $"{book.Name} · {book.AdapterName} · {book.ModeLabel}",
            book.ModeLabel,
            IsLive: false,
            CanStart: true,
            BlockReason: string.Empty,
            BoundStrategies: book.Strategies);
    }
}
