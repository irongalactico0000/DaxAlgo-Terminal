using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using DaxAlgo.Sdk;
using TradingTerminal.UI.Controls.Render;

namespace TradingTerminal.UI.Avalonia.Controls.Render;

/// <summary>
/// Avalonia host chrome for an authored strategy or visualizer: parameters, picture, optional book,
/// and activity log. Frame pacing stays in <see cref="AuthoredUnitHost"/>.
/// </summary>
public partial class AuthoredUnitView : UserControl, IDisposable
{
    private AuthoredUnitPresenter? _presenter;
    private int _disposed;

    public AuthoredUnitView()
    {
        InitializeComponent();
        Surface.ThemeResolver = ResolveTheme;
        DataContextChanged += OnDataContextChanged;
        DetachedFromVisualTree += (_, _) => Detach();
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        Detach();
        if (Volatile.Read(ref _disposed) != 0 || DataContext is not AuthoredUnitPresenter presenter)
            return;

        _presenter = presenter;
        _presenter.PropertyChanged += OnPresenterChanged;
        _presenter.Log.CollectionChanged += OnLogChanged;
        _presenter.FrameRequested += OnFrameRequested;
        Surface.Draw = presenter.Draw;
    }

    private void Detach()
    {
        if (_presenter is null)
            return;

        _presenter.PropertyChanged -= OnPresenterChanged;
        _presenter.Log.CollectionChanged -= OnLogChanged;
        _presenter.FrameRequested -= OnFrameRequested;
        _presenter = null;
        Surface.Draw = null;
    }

    private void OnPresenterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_presenter is null)
            return;

        if (e.PropertyName == nameof(AuthoredUnitPresenter.Draw))
            Surface.Draw = _presenter.Draw;
    }

    private void OnFrameRequested(object? sender, EventArgs e) => Invalidate();

    private void OnLogChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add)
            return;

        Dispatcher.UIThread.Post(() => LogScroller.ScrollToEnd(), DispatcherPriority.Background);
    }

    private Color ResolveTheme(RenderThemeColor token) => token switch
    {
        RenderThemeColor.Text => Color.FromRgb(0xE6, 0xED, 0xF3),
        RenderThemeColor.TextSecondary => Color.FromRgb(0x8B, 0x94, 0xA0),
        RenderThemeColor.Background => Color.FromRgb(0x0D, 0x11, 0x17),
        RenderThemeColor.Surface => Color.FromRgb(0x16, 0x1B, 0x22),
        RenderThemeColor.Grid => Color.FromRgb(0x30, 0x36, 0x3D),
        RenderThemeColor.Border => Color.FromRgb(0x48, 0x4F, 0x58),
        RenderThemeColor.Accent => Color.FromRgb(0x2F, 0x81, 0xF7),
        RenderThemeColor.Bullish => Color.FromRgb(0x3F, 0xB9, 0x50),
        RenderThemeColor.Bearish => Color.FromRgb(0xF8, 0x51, 0x49),
        RenderThemeColor.Warning => Color.FromRgb(0xD2, 0x99, 0x22),
        _ => Color.FromRgb(0x8B, 0x94, 0xA0),
    };

    public void Invalidate()
    {
        if (Volatile.Read(ref _disposed) == 0)
            Surface.InvalidateVisual();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        Detach();
        DataContextChanged -= OnDataContextChanged;
    }
}
