using Avalonia.Controls;
using Avalonia.Interactivity;
using TradingTerminal.UI.Execution;

namespace TradingTerminal.App.Avalonia.Execution;

public partial class PaperExecutionConsoleWindow : Window
{
    public PaperExecutionConsoleWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        if (DataContext is PaperExecutionConsoleViewModel viewModel &&
            viewModel.RefreshCommand.CanExecute(null))
            viewModel.RefreshCommand.Execute(null);
    }
}
