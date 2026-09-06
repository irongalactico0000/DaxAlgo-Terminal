using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.Charts;

/// <summary>The two mutually exclusive left-drag behaviors supported by the native chart.</summary>
public enum ChartInteractionMode
{
    Pan,
    SelectResearchRange,
}

/// <summary>A half-open UTC range selected over completed chart bars.</summary>
public sealed record ChartTimeRange
{
    public ChartTimeRange(DateTimeOffset startUtc, DateTimeOffset endUtcExclusive)
    {
        StartUtc = startUtc.ToUniversalTime();
        EndUtcExclusive = endUtcExclusive.ToUniversalTime();
        if (EndUtcExclusive <= StartUtc)
            throw new ArgumentException("The chart-range end must be later than its start.", nameof(endUtcExclusive));
    }

    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset EndUtcExclusive { get; }
}

public sealed class ChartRangeSelectedEventArgs(ChartTimeRange range) : EventArgs
{
    public ChartTimeRange Range { get; } = range ?? throw new ArgumentNullException(nameof(range));
}

public sealed class ResearchChartSelectionRequestedEventArgs(ResearchChartSelectionV1 selection) : EventArgs
{
    public ResearchChartSelectionV1 Selection { get; } = selection ?? throw new ArgumentNullException(nameof(selection));
}

/// <summary>Maps a horizontal drag over the visible candle window to a half-open range.</summary>
public static class ChartRangeSelectionMapper
{
    public static ChartTimeRange Map(
        IReadOnlyList<ChartCandle> candles,
        int visibleStart,
        int visibleEndExclusive,
        double chartWidth,
        double anchorX,
        double currentX)
    {
        ArgumentNullException.ThrowIfNull(candles);
        if (candles.Count == 0)
            throw new ArgumentException("At least one candle is required.", nameof(candles));
        if (visibleStart < 0 || visibleEndExclusive > candles.Count || visibleEndExclusive <= visibleStart)
            throw new ArgumentOutOfRangeException(nameof(visibleStart), "The visible candle window is invalid.");
        if (!double.IsFinite(chartWidth) || chartWidth <= 0)
            throw new ArgumentOutOfRangeException(nameof(chartWidth), "Chart width must be positive and finite.");

        var count = visibleEndExclusive - visibleStart;
        var first = IndexAt(anchorX);
        var last = IndexAt(currentX);
        var startIndex = Math.Min(first, last);
        var endIndex = Math.Max(first, last);
        var start = DateTimeOffset.FromUnixTimeSeconds(candles[startIndex].Time);
        var end = DateTimeOffset.FromUnixTimeSeconds(candles[endIndex].Time + InferInterval(candles, endIndex));
        return new ChartTimeRange(start, end);

        int IndexAt(double x)
        {
            var clamped = Math.Clamp(double.IsFinite(x) ? x : 0, 0, chartWidth);
            var relative = Math.Min(count - 1, (int)Math.Floor(clamped / chartWidth * count));
            return visibleStart + relative;
        }
    }

    private static long InferInterval(IReadOnlyList<ChartCandle> candles, int index)
    {
        var before = index > 0 ? candles[index].Time - candles[index - 1].Time : long.MaxValue;
        var after = index + 1 < candles.Count ? candles[index + 1].Time - candles[index].Time : long.MaxValue;
        var interval = Math.Min(before > 0 ? before : long.MaxValue, after > 0 ? after : long.MaxValue);
        return interval == long.MaxValue ? 1 : interval;
    }
}
