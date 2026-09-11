using TradingTerminal.UI.Strategies;

namespace TradingTerminal.App.Avalonia.Execution;

/// <summary>
/// In-process Prepare→book: ensure a Paper book exists for an admitted unit (no second Native app).
/// </summary>
public static class ValidatedPaperBookHandoff
{
    /// <summary>
    /// Selects an existing book for the stable admit account, or creates one and selects it.
    /// </summary>
    public static PaperExecutionBookResult EnsureAdmittedBook(
        PaperExecutionBookManager manager,
        StrategyKernelRegistration registration,
        string primarySymbol)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentException.ThrowIfNullOrWhiteSpace(primarySymbol);

        var accountId = $"admit-{registration.Id}";
        if (accountId.Length > 64)
            accountId = accountId[..64];

        var existing = manager.Books.FirstOrDefault(book =>
            string.Equals(book.AccountId, accountId, StringComparison.Ordinal));
        if (existing is not null)
            return manager.SelectBook(existing.Id);

        var name = $"Admit · {registration.DisplayName}";
        if (name.Length > 80)
            name = name[..80];

        return manager.CreateBook(
            name,
            accountId,
            primarySymbol.Trim(),
            strategies: [registration.Id]);
    }
}
