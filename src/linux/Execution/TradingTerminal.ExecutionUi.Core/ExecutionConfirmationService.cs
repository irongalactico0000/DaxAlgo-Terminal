namespace TradingTerminal.ExecutionUi;

public readonly record struct ExecutionTypedConfirmationResult(
    bool IsConfirmed,
    string EnteredText)
{
    public static ExecutionTypedConfirmationResult Cancelled => new(false, string.Empty);
}

/// <summary>
/// The console's prompt seam. Deliberately UI-framework-free so the portable view-model layer never
/// takes a dependency on a window toolkit; each UI head supplies its own dialog implementation.
/// </summary>
public interface IExecutionConfirmationService
{
    ValueTask<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default);

    ValueTask<ExecutionTypedConfirmationResult> ConfirmTypedAsync(
        string title,
        string message,
        string requiredText,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default seam for headless hosts and for any UI head that has not yet registered a dialog. Every
/// prompt is refused, so an unattended process can never answer a live-execution confirmation on a
/// user's behalf.
/// </summary>
public sealed class DeniedExecutionConfirmationService : IExecutionConfirmationService
{
    public ValueTask<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(false);
    }

    public ValueTask<ExecutionTypedConfirmationResult> ConfirmTypedAsync(
        string title,
        string message,
        string requiredText,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredText);
        return ValueTask.FromResult(ExecutionTypedConfirmationResult.Cancelled);
    }
}
