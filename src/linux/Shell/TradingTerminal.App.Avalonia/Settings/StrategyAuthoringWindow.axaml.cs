using System.Collections.Specialized;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DaxAlgo.Package;
using TradingTerminal.App.Authoring;
using TradingTerminal.App.Plugins;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.UI;

namespace TradingTerminal.App.Avalonia.Settings;

public partial class StrategyAuthoringWindow : Window
{
    private INotifyCollectionChanged? _messages;

    public event EventHandler? ResearchChartRequested;
    public event EventHandler? HistoricalValidationRequested;
    public event EventHandler? PaperHandoffRequested;

    public StrategyAuthoringWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Closed += OnClosed;
    }

    public bool ShowSimulatedDataBanner
    {
        get => SimulatedDataBanner.IsVisible;
        set => SimulatedDataBanner.IsVisible = value;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachMessages();

        if (DataContext is StrategyAuthoringViewModel viewModel)
        {
            _messages = viewModel.Messages;
            _messages.CollectionChanged += OnMessagesChanged;
            ScrollTranscriptToEnd();
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        DetachMessages();
        DataContextChanged -= OnDataContextChanged;
        Closed -= OnClosed;
    }

    private void DetachMessages()
    {
        if (_messages is not null)
            _messages.CollectionChanged -= OnMessagesChanged;
        _messages = null;
    }

    private void OnMessagesChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ScrollTranscriptToEnd();

    private void ScrollTranscriptToEnd() =>
        Dispatcher.UIThread.Post(ChatScroll.ScrollToEnd, DispatcherPriority.Background);

    private void OnComposerKeyDown(object? sender, KeyEventArgs e)
    {
        var sendModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                           e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (e.Key != Key.Enter || !sendModifier || DataContext is not StrategyAuthoringViewModel viewModel)
            return;

        if (viewModel.SendCommand.CanExecute(null))
            viewModel.SendCommand.Execute(null);
        e.Handled = true;
    }

    private void OnUseStarter(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: StrategyStarterBrief brief } && DataContext is StrategyAuthoringViewModel viewModel)
            viewModel.UseStarterPromptCommand.Execute(brief);
    }

    private void OnDeleteSession(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: { } session } && DataContext is StrategyAuthoringViewModel viewModel)
            viewModel.DeleteSavedSessionCommand.Execute(session);
    }

    private void OnLaunchCli(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: { } adapter } && DataContext is StrategyAuthoringViewModel viewModel)
            viewModel.LaunchCliCommand.Execute(adapter);
    }

    private void OnResearchChartRequested(object? sender, RoutedEventArgs e) =>
        ResearchChartRequested?.Invoke(this, EventArgs.Empty);

    private void OnHistoricalValidationRequested(object? sender, RoutedEventArgs e) =>
        HistoricalValidationRequested?.Invoke(this, EventArgs.Empty);

    private void OnPaperHandoffRequested(object? sender, RoutedEventArgs e) =>
        PaperHandoffRequested?.Invoke(this, EventArgs.Empty);

    private async void OnExportOpenPackage(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not StrategyAuthoringViewModel viewModel) return;
        if (!viewModel.TryGetOpenPackageExport(out var specification, out var sources, out var reason))
        {
            viewModel.Status = reason;
            return;
        }

        var suggested = $"{specification.UnitId}{DaxPackage.StrategyExtension}";
        var path = await UiFile.SaveAsync(
            "DaxAlgo strategy package",
            [DaxPackage.StrategyExtension.TrimStart('.')],
            suggested);
        if (path is null) return;

        _ = AuthoredUnitOpenPackageExporter.TryWrite(path, specification, sources, out var message);
        viewModel.Status = message;
    }

    private async void OnCopyChat(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not StrategyAuthoringViewModel viewModel) return;

        var transcript = string.Join(
            Environment.NewLine + Environment.NewLine,
            viewModel.Messages.Select(FormatChatEntry));
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (string.IsNullOrWhiteSpace(transcript) || clipboard is null) return;
        await clipboard.SetTextAsync(transcript);
    }

    private static string FormatChatEntry(AuthoringMessage message)
    {
        if (message.IsUser) return $"User: {message.Text}";
        if (message.IsAssistant) return $"Assistant: {message.Text}";

        var body = message.Kind == AuthoringMessage.KindPlan
            ? message.PlanSnapshotText()
            : string.Join(
                Environment.NewLine,
                new[] { message.ToolTitle, message.Text, message.ToolDetail }
                    .Where(static value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal));
        return $"System: {body}";
    }
}

/// <summary>Lets the Avalonia parameter workbench select the correct editor without UI-specific VM code.</summary>
public sealed class ParameterKindMatchConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ParameterKind kind || parameter is not string expected)
            return false;

        return expected.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(candidate => Enum.TryParse<ParameterKind>(candidate, out var parsed) && parsed == kind);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Converts collection counts to visibility without relying on implicit numeric coercion.</summary>
public sealed class PositiveCountConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int count && count > 0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
