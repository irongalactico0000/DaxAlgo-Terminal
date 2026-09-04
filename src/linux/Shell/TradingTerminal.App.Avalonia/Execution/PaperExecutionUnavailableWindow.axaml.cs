using Avalonia.Controls;
using Avalonia.Interactivity;

namespace TradingTerminal.App.Avalonia.Execution;

public partial class PaperExecutionUnavailableWindow : Window
{
    public PaperExecutionUnavailableWindow()
        : this("The local Paper execution service could not be initialized.")
    {
    }

    public PaperExecutionUnavailableWindow(string reason)
    {
        InitializeComponent();
        ReasonText.Text = string.IsNullOrWhiteSpace(reason)
            ? "The local Paper execution service could not be initialized."
            : reason;
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
