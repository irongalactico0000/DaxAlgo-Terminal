using Avalonia.Controls;

namespace TradingTerminal.App.Avalonia.Execution;

/// <summary>
/// Thin Avalonia view over the shared <c>ExecutionConsoleViewModel</c>: Brokers, New book (Paper or
/// Real), and the manual ticket. Every gate lives in the view-model and the OMS behind it.
/// </summary>
public partial class ExecutionConsoleWindow : Window
{
    public ExecutionConsoleWindow() => InitializeComponent();
}
