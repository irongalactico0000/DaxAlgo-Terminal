using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Threading;
using TradingTerminal.ExecutionUi;

namespace TradingTerminal.App.Avalonia.Execution;

/// <summary>
/// Avalonia head for the execution console's confirmation seam. Arming LIVE requires the operator to
/// type the exact word back; anything else — including a closed shell with no window to parent the
/// dialog to — is refused, so an unattended process can never answer on the operator's behalf.
/// </summary>
public sealed class AvaloniaExecutionConfirmationService : IExecutionConfirmationService
{
    public async ValueTask<bool> ConfirmAsync(
        string title,
        string message,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        cancellationToken.ThrowIfCancellationRequested();
        var result = await PromptAsync(title, message, requiredText: null, cancellationToken)
            .ConfigureAwait(true);
        return result.IsConfirmed;
    }

    public async ValueTask<ExecutionTypedConfirmationResult> ConfirmTypedAsync(
        string title,
        string message,
        string requiredText,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredText);
        cancellationToken.ThrowIfCancellationRequested();
        return await PromptAsync(title, message, requiredText, cancellationToken).ConfigureAwait(true);
    }

    private static Task<ExecutionTypedConfirmationResult> PromptAsync(
        string title,
        string message,
        string? requiredText,
        CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
            return ShowAsync(title, message, requiredText, cancellationToken);

        var completion = new TaskCompletionSource<ExecutionTypedConfirmationResult>();
        Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                completion.SetResult(
                    await ShowAsync(title, message, requiredText, cancellationToken).ConfigureAwait(true));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        });
        return completion.Task;
    }

    private static async Task<ExecutionTypedConfirmationResult> ShowAsync(
        string title,
        string message,
        string? requiredText,
        CancellationToken cancellationToken)
    {
        if (Owner() is not { } owner)
            return ExecutionTypedConfirmationResult.Cancelled;

        var entry = new TextBox
        {
            Watermark = requiredText,
            IsVisible = requiredText is not null,
        };
        var confirm = new Button
        {
            Content = requiredText is null ? "Confirm" : $"Type {requiredText} to continue",
            IsEnabled = requiredText is null,
        };
        var cancel = new Button { Content = "Cancel", IsDefault = true };
        if (requiredText is not null)
        {
            entry.TextChanged += (_, _) =>
                confirm.IsEnabled = string.Equals(entry.Text, requiredText, StringComparison.Ordinal);
        }

        var dialog = new Window
        {
            Title = title,
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 12,
                Children =
                {
                    new TextBlock { Text = title, FontWeight = global::Avalonia.Media.FontWeight.Bold },
                    new TextBlock { Text = message, TextWrapping = global::Avalonia.Media.TextWrapping.Wrap },
                    entry,
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { cancel, confirm },
                    },
                },
            },
        };

        var outcome = ExecutionTypedConfirmationResult.Cancelled;
        confirm.Click += (_, _) =>
        {
            outcome = new ExecutionTypedConfirmationResult(true, entry.Text ?? string.Empty);
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();
        using var closeOnCancellation = cancellationToken.Register(() =>
            Dispatcher.UIThread.Post(dialog.Close));

        await dialog.ShowDialog(owner).ConfigureAwait(true);
        return outcome;
    }

    private static Window? Owner() =>
        Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.FirstOrDefault(window => window.IsActive) ?? desktop.MainWindow
            : null;
}
