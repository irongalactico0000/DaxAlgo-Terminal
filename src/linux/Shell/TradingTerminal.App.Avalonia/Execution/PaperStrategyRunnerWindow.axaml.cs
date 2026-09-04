using Avalonia.Controls;
using Avalonia.Threading;

namespace TradingTerminal.App.Avalonia.Execution;

public partial class PaperStrategyRunnerWindow : Window
{
    private readonly DispatcherTimer _frameTimer = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(100),
    };
    private PaperStrategyRunnerViewModel? _viewModel;

    public PaperStrategyRunnerWindow()
    {
        InitializeComponent();
        _frameTimer.Tick += OnFrameTick;
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.FrameRequested -= OnFrameRequested;
        _viewModel = DataContext as PaperStrategyRunnerViewModel;
        if (_viewModel is not null)
            _viewModel.FrameRequested += OnFrameRequested;
    }

    private void OnOpened(object? sender, EventArgs e) => _frameTimer.Start();

    private void OnFrameTick(object? sender, EventArgs e) => Surface.InvalidateVisual();

    private void OnFrameRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(Surface.InvalidateVisual, DispatcherPriority.Background);

    private void OnClosed(object? sender, EventArgs e)
    {
        _frameTimer.Stop();
        _frameTimer.Tick -= OnFrameTick;
        if (_viewModel is not null)
            _viewModel.FrameRequested -= OnFrameRequested;
        _viewModel = null;
        DataContextChanged -= OnDataContextChanged;
        Opened -= OnOpened;
        Closed -= OnClosed;
    }
}
