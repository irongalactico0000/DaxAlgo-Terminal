using System.Globalization;
using Avalonia;
using Avalonia.Media;
using DaxAlgo.Sdk;
using AvRect = Avalonia.Rect;

namespace TradingTerminal.UI.Avalonia.Controls.Render;

/// <summary>
/// Immediate-mode Avalonia implementation of <see cref="IRenderSurface"/> with data-to-pixel
/// transforms, panel clipping, non-finite input rejection, and a bounded operation budget.
/// </summary>
internal sealed class DrawingContextSurface : IRenderSurface
{
    private static readonly Typeface Typeface = new("SF Mono, Menlo, monospace");

    private readonly DrawingContext _context;
    private readonly Size _size;
    private readonly double _scale;
    private readonly RenderCursor _rawCursor;
    private readonly Func<RenderThemeColor, Color> _theme;
    private readonly int _expectedPanels;
    private readonly bool _discovering;
    private readonly List<PanelSlot> _panels = [];
    private readonly Stack<IDisposable> _clips = new();

    private PanelSlot? _panel;
    private RenderStyle _style = new(new RenderColor(0xE6, 0xED, 0xF3));
    private IBrush? _brush;
    private IPen? _pen;
    private SeriesScope? _series;

    internal DrawingContextSurface(
        DrawingContext context,
        Size size,
        double scale,
        RenderCursor cursor,
        Func<RenderThemeColor, Color> theme,
        int expectedPanels = 0,
        bool discovering = false)
    {
        _context = context;
        _size = size;
        _scale = scale <= 0d ? 1d : scale;
        _rawCursor = cursor;
        _theme = theme;
        _expectedPanels = expectedPanels;
        _discovering = discovering;
    }

    internal int PanelCount => _panels.Count;

    internal int OperationCount { get; private set; }

    internal bool WasTruncated { get; private set; }

    public RenderViewport Viewport => _panel is { } panel
        ? new RenderViewport(panel.Bounds.Width, panel.Bounds.Height, _scale)
        : new RenderViewport(_size.Width, _size.Height, _scale);

    public RenderCursor Cursor
    {
        get
        {
            if (!_rawCursor.IsInside || _panel is not { } panel)
                return _rawCursor;
            if (!panel.Bounds.Contains(new Point(_rawCursor.X, _rawCursor.Y)))
                return new RenderCursor(0d, 0d, IsInside: false, IsPressed: false);

            return new RenderCursor(
                _rawCursor.X - panel.Bounds.X,
                _rawCursor.Y - panel.Bounds.Y,
                IsInside: true,
                _rawCursor.IsPressed);
        }
    }

    public RenderColor Theme(RenderThemeColor token)
    {
        var color = _theme(token);
        return new RenderColor(color.R, color.G, color.B);
    }

    public void SetStyle(RenderStyle style)
    {
        _style = style;
        _brush = null;
        _pen = null;
    }

    public IDisposable Panel(string title, RenderPanelKind kind)
    {
        var index = _panels.Count;
        _panels.Add(new PanelSlot(index, title, kind, default));
        LayoutPanels();
        _panel = _panels[index];

        if (!_discovering)
        {
            _clips.Push(_context.PushClip(_panel.Bounds));
            Count();
        }

        return new PanelScope(this, index);
    }

    public void AxisX(double minimum, double maximum, string? format = null)
    {
        if (_panel is { } panel && maximum > minimum)
            Replace(panel with { XMinimum = minimum, XMaximum = maximum, XFormat = format });
    }

    public void AxisY(double minimum, double maximum, string? format = null)
    {
        if (_panel is { } panel && maximum > minimum)
            Replace(panel with { YMinimum = minimum, YMaximum = maximum, YFormat = format });
    }

    public IDisposable Series(string name, RenderSeriesKind kind)
    {
        CloseSeries();
        _series = new SeriesScope(kind);
        return new SeriesCloser(this);
    }

    public void Push(double x, double y)
    {
        if (_series is null || !Finite(x, y) || !Count())
            return;

        _series.Points.Add(ToPixels(x, y));
    }

    public void Line(double x1, double y1, double x2, double y2)
    {
        if (!Finite(x1, y1, x2, y2) || !Count())
            return;

        var start = ToPixels(x1, y1);
        var end = ToPixels(x2, y2);
        if (Finite(start.X, start.Y, end.X, end.Y))
            _context.DrawLine(CurrentPen(), start, end);
    }

    public void Rect(double x, double y, double width, double height, bool filled = true)
    {
        if (!Finite(x, y, width, height) || !Count())
            return;

        var a = ToPixels(x, y);
        var b = ToPixels(x + width, y + height);
        var rect = new AvRect(
            Math.Min(a.X, b.X),
            Math.Min(a.Y, b.Y),
            Math.Abs(b.X - a.X),
            Math.Abs(b.Y - a.Y));
        _context.DrawRectangle(filled ? CurrentBrush() : null, filled ? null : CurrentPen(), rect);
    }

    public void Text(double x, double y, string text)
    {
        if (string.IsNullOrEmpty(text) || !Finite(x, y) || !Count())
            return;

        var fontSize = double.IsFinite(_style.FontSize) && _style.FontSize > 0d
            ? _style.FontSize
            : 11d;
        var formatted = new FormattedText(
            text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            Typeface,
            fontSize,
            CurrentBrush());
        var origin = ToPixels(x, y);
        _context.DrawText(formatted, new Point(origin.X, origin.Y - formatted.Height));
    }

    public void Marker(double x, double y, RenderMarkerShape shape)
    {
        if (!Finite(x, y) || !Count())
            return;

        var point = ToPixels(x, y);
        const double radius = 3.5d;
        switch (shape)
        {
            case RenderMarkerShape.Square:
                _context.DrawRectangle(
                    CurrentBrush(), null,
                    new AvRect(point.X - radius, point.Y - radius, radius * 2d, radius * 2d));
                break;
            case RenderMarkerShape.Cross:
                _context.DrawLine(
                    CurrentPen(),
                    new Point(point.X - radius, point.Y - radius),
                    new Point(point.X + radius, point.Y + radius));
                _context.DrawLine(
                    CurrentPen(),
                    new Point(point.X - radius, point.Y + radius),
                    new Point(point.X + radius, point.Y - radius));
                break;
            case RenderMarkerShape.Triangle:
            case RenderMarkerShape.Diamond:
                _context.DrawGeometry(CurrentBrush(), null, Polygon(point, radius, shape));
                break;
            default:
                _context.DrawEllipse(CurrentBrush(), null, point, radius, radius);
                break;
        }
    }

    internal void Close()
    {
        CloseSeries();
        while (_clips.Count > 0)
            _clips.Pop().Dispose();
    }

    private void LayoutPanels()
    {
        if (_panels.Count == 0)
            return;

        var total = Math.Max(_expectedPanels, _panels.Count);
        var height = _size.Height / total;
        for (var index = 0; index < _panels.Count; index++)
        {
            _panels[index] = _panels[index] with
            {
                Bounds = new AvRect(0d, index * height, _size.Width, height),
            };
        }
    }

    private void ClosePanel(int index)
    {
        if (!_discovering && _clips.Count > 0)
            _clips.Pop().Dispose();

        _panel = null;
        _ = index;
    }

    private void Replace(PanelSlot updated)
    {
        _panels[updated.Index] = updated;
        _panel = updated;
    }

    private void CloseSeries()
    {
        if (_series is not { } series)
            return;

        _series = null;
        if (_discovering || series.Points.Count == 0)
            return;

        switch (series.Kind)
        {
            case RenderSeriesKind.Scatter:
                foreach (var point in series.Points)
                    _context.DrawEllipse(CurrentBrush(), null, point, 1.5d, 1.5d);
                break;
            case RenderSeriesKind.Bars:
                foreach (var point in series.Points)
                    _context.DrawLine(CurrentPen(), new Point(point.X, BaselineY()), point);
                break;
            default:
                DrawJoined(series);
                break;
        }
    }

    private void DrawJoined(SeriesScope series)
    {
        var figure = new PathFigure
        {
            StartPoint = series.Points[0],
            IsClosed = series.Kind == RenderSeriesKind.Area,
        };
        for (var index = 1; index < series.Points.Count; index++)
        {
            var point = series.Points[index];
            if (series.Kind == RenderSeriesKind.Steps)
            {
                figure.Segments!.Add(new LineSegment
                {
                    Point = new Point(point.X, series.Points[index - 1].Y),
                });
            }

            figure.Segments!.Add(new LineSegment { Point = point });
        }

        if (series.Kind == RenderSeriesKind.Area)
        {
            var baseline = BaselineY();
            figure.Segments!.Add(new LineSegment
            {
                Point = new Point(series.Points[^1].X, baseline),
            });
            figure.Segments!.Add(new LineSegment
            {
                Point = new Point(series.Points[0].X, baseline),
            });
        }

        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        _context.DrawGeometry(
            series.Kind == RenderSeriesKind.Area ? CurrentBrush() : null,
            CurrentPen(),
            geometry);
    }

    private double BaselineY() => _panel is { } panel ? panel.Bounds.Bottom : _size.Height;

    private Point ToPixels(double x, double y)
    {
        if (_panel is not { } panel)
            return new Point(x, y);

        var bounds = panel.Bounds;
        var px = panel.HasXAxis
            ? bounds.X + ((x - panel.XMinimum) / (panel.XMaximum - panel.XMinimum) * bounds.Width)
            : bounds.X + x;
        var py = panel.HasYAxis
            ? bounds.Bottom - ((y - panel.YMinimum) / (panel.YMaximum - panel.YMinimum) * bounds.Height)
            : bounds.Y + y;
        return new Point(px, py);
    }

    private static bool Finite(params double[] values)
    {
        foreach (var value in values)
        {
            if (!double.IsFinite(value))
                return false;
        }

        return true;
    }

    private bool Count()
    {
        if (_discovering)
            return false;
        if (OperationCount >= RenderSurfaceView.MaximumOperationsPerFrame)
        {
            WasTruncated = true;
            return false;
        }

        OperationCount++;
        return true;
    }

    private IBrush CurrentBrush()
    {
        if (_brush is not null)
            return _brush;

        var alpha = double.IsFinite(_style.Alpha) ? Math.Clamp(_style.Alpha, 0d, 1d) : 1d;
        _brush = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(alpha * 255d),
            _style.Color.R,
            _style.Color.G,
            _style.Color.B));
        return _brush;
    }

    private IPen CurrentPen()
    {
        if (_pen is not null)
            return _pen;

        var thickness = double.IsFinite(_style.Thickness) && _style.Thickness > 0d
            ? _style.Thickness
            : 1d;
        _pen = new Pen(
            CurrentBrush(),
            thickness,
            _style.Dashed ? DashStyle.Dash : null);
        return _pen;
    }

    private static PathGeometry Polygon(Point centre, double radius, RenderMarkerShape shape)
    {
        var points = shape == RenderMarkerShape.Triangle
            ? new[]
            {
                new Point(centre.X, centre.Y - radius),
                new Point(centre.X + radius, centre.Y + radius),
                new Point(centre.X - radius, centre.Y + radius),
            }
            : new[]
            {
                new Point(centre.X, centre.Y - radius),
                new Point(centre.X + radius, centre.Y),
                new Point(centre.X, centre.Y + radius),
                new Point(centre.X - radius, centre.Y),
            };
        var figure = new PathFigure { StartPoint = points[0], IsClosed = true };
        foreach (var point in points.Skip(1))
            figure.Segments!.Add(new LineSegment { Point = point });
        var geometry = new PathGeometry();
        geometry.Figures!.Add(figure);
        return geometry;
    }

    private sealed record PanelSlot(
        int Index,
        string Title,
        RenderPanelKind Kind,
        AvRect Bounds,
        double XMinimum = 0d,
        double XMaximum = 0d,
        string? XFormat = null,
        double YMinimum = 0d,
        double YMaximum = 0d,
        string? YFormat = null)
    {
        internal bool HasXAxis => XMaximum > XMinimum;

        internal bool HasYAxis => YMaximum > YMinimum;
    }

    private sealed class SeriesScope(RenderSeriesKind kind)
    {
        internal RenderSeriesKind Kind { get; } = kind;

        internal List<Point> Points { get; } = [];
    }

    private sealed class PanelScope(DrawingContextSurface surface, int index) : IDisposable
    {
        private bool _closed;

        public void Dispose()
        {
            if (_closed)
                return;

            _closed = true;
            surface.ClosePanel(index);
        }
    }

    private sealed class SeriesCloser(DrawingContextSurface surface) : IDisposable
    {
        private bool _closed;

        public void Dispose()
        {
            if (_closed)
                return;

            _closed = true;
            surface.CloseSeries();
        }
    }
}
