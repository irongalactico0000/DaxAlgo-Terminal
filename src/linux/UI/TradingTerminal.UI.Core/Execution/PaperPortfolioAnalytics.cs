using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Trading;

namespace TradingTerminal.UI.Execution;

public enum PaperExecutionTimeRange : byte
{
    SevenDays = 1,
    ThirtyDays = 2,
    NinetyDays = 3,
    YearToDate = 4,
}

public enum PaperMarkBasis : byte
{
    CurrentPaperMark = 1,
    LatestLedgerFill = 2,
}

public sealed record PaperClosedTradeSnapshot(
    InstrumentId InstrumentId,
    DateTimeOffset ClosedAtUtc,
    decimal Quantity,
    decimal RealizedProfitAndLoss);

public sealed record PaperInstrumentExposureSnapshot(
    InstrumentId InstrumentId,
    decimal SignedQuantity,
    decimal AverageEntryPrice,
    decimal MarkPrice,
    decimal MarketValue,
    decimal UnrealizedProfitAndLoss,
    PaperMarkBasis MarkBasis);

public sealed record PaperDailyProfitAndLossSnapshot(DateTime DateUtc, decimal RealizedProfitAndLoss);

public sealed record PaperEquityPointSnapshot(DateTime DateUtc, decimal Equity);

public sealed record PaperPerformancePeriodSnapshot(
    PaperExecutionTimeRange Range,
    string Label,
    decimal EquityAtStart,
    decimal RealizedEquity,
    decimal RealizedProfitAndLoss,
    decimal ReturnPercent,
    double AnnualizedSharpe,
    decimal MaximumDrawdownPercent,
    decimal WinRatePercent,
    int TradeCount,
    int WinningTrades,
    IReadOnlyList<PaperEquityPointSnapshot> EquitySeries,
    IReadOnlyList<PaperDailyProfitAndLossSnapshot> DailyProfitAndLossSeries);

/// <summary>
/// Account analytics derived from an explicit opening balance plus immutable Paper fills. Current
/// marks are used when available; latest fill is the deterministic fallback. Values are Paper SIM
/// units and do not claim broker cash, FX conversion, or external account reconciliation.
/// </summary>
public sealed record PaperPortfolioAnalyticsSnapshot(
    decimal OpeningBalance,
    decimal LedgerCashFlow,
    decimal CurrentCash,
    decimal RealizedProfitAndLoss,
    decimal UnrealizedProfitAndLoss,
    decimal MarkedEquity,
    decimal GrossExposure,
    decimal NetExposure,
    int OpenPositionCount,
    IReadOnlyList<PaperClosedTradeSnapshot> ClosedTrades,
    IReadOnlyList<PaperInstrumentExposureSnapshot> Exposures,
    IReadOnlyList<PaperPerformancePeriodSnapshot> Periods,
    string Currency,
    string ValuationBasis)
{
    public PaperPerformancePeriodSnapshot Period(PaperExecutionTimeRange range) =>
        Periods.First(item => item.Range == range);
}

public static class PaperPortfolioAnalyticsCalculator
{
    private const int MaximumChartPoints = 370;
    private static readonly PaperExecutionTimeRange[] Ranges =
    [
        PaperExecutionTimeRange.SevenDays,
        PaperExecutionTimeRange.ThirtyDays,
        PaperExecutionTimeRange.NinetyDays,
        PaperExecutionTimeRange.YearToDate,
    ];

    public static PaperPortfolioAnalyticsSnapshot Calculate(
        decimal openingBalance,
        ExecutionReconciliationSnapshot economics,
        DateTimeOffset asOfUtc,
        Func<InstrumentId, ScaledPrice?>? currentMark = null,
        string currency = "SIM")
    {
        if (openingBalance <= 0m)
            throw new ArgumentOutOfRangeException(nameof(openingBalance), "Opening balance must be positive.");
        ArgumentNullException.ThrowIfNull(economics);
        if (asOfUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("The analytics observation must be UTC.", nameof(asOfUtc));

        var lots = new Dictionary<InstrumentId, List<OpenLot>>();
        var latestFillMarks = new Dictionary<InstrumentId, decimal>();
        var closedTrades = new List<PaperClosedTradeSnapshot>();
        foreach (var fill in economics.Fills
                     .OrderBy(item => item.OccurredAtUtc)
                     .ThenBy(item => item.TradeId.Value, StringComparer.Ordinal))
        {
            var quantity = ExecutionNumericBoundary.ToDecimal(fill.Quantity);
            var price = ExecutionNumericBoundary.ToDecimal(fill.Price);
            var fee = ExecutionNumericBoundary.ToDecimal(fill.Fee);
            var direction = fill.Side == OrderSide.Buy ? 1m : -1m;
            latestFillMarks[fill.InstrumentId] = price;
            if (!lots.TryGetValue(fill.InstrumentId, out var instrumentLots))
            {
                instrumentLots = [];
                lots.Add(fill.InstrumentId, instrumentLots);
            }

            var remaining = quantity;
            var feePerUnit = fee / quantity;
            while (remaining > 0m && instrumentLots.Count > 0 && Math.Sign(instrumentLots[0].SignedQuantity) != Math.Sign(direction))
            {
                var lot = instrumentLots[0];
                var closeQuantity = Math.Min(remaining, Math.Abs(lot.SignedQuantity));
                var raw = lot.SignedQuantity > 0m
                    ? (price - lot.EntryPrice) * closeQuantity
                    : (lot.EntryPrice - price) * closeQuantity;
                var realized = raw - ((lot.EntryFeePerUnit + feePerUnit) * closeQuantity);
                closedTrades.Add(new PaperClosedTradeSnapshot(
                    fill.InstrumentId,
                    fill.OccurredAtUtc,
                    closeQuantity,
                    realized));

                var residual = Math.Abs(lot.SignedQuantity) - closeQuantity;
                if (residual == 0m)
                    instrumentLots.RemoveAt(0);
                else
                    instrumentLots[0] = lot with { SignedQuantity = Math.Sign(lot.SignedQuantity) * residual };
                remaining -= closeQuantity;
            }

            if (remaining > 0m)
                instrumentLots.Add(new OpenLot(direction * remaining, price, feePerUnit));
        }

        var exposures = new List<PaperInstrumentExposureSnapshot>();
        foreach (var (instrumentId, instrumentLots) in lots.OrderBy(item => item.Key.Value))
        {
            var signedQuantity = instrumentLots.Sum(item => item.SignedQuantity);
            if (signedQuantity == 0m) continue;
            var absoluteQuantity = instrumentLots.Sum(item => Math.Abs(item.SignedQuantity));
            var averageEntry = instrumentLots.Sum(item => Math.Abs(item.SignedQuantity) * item.EntryPrice) / absoluteQuantity;
            var supplied = currentMark?.Invoke(instrumentId);
            var mark = supplied is { IsValid: true, Coefficient: > 0 }
                ? ExecutionNumericBoundary.ToDecimal(supplied.Value)
                : latestFillMarks[instrumentId];
            var markBasis = supplied is { IsValid: true, Coefficient: > 0 }
                ? PaperMarkBasis.CurrentPaperMark
                : PaperMarkBasis.LatestLedgerFill;
            var marketValue = signedQuantity * mark;
            var unrealized = instrumentLots.Sum(lot =>
                lot.SignedQuantity > 0m
                    ? (mark - lot.EntryPrice) * lot.SignedQuantity - lot.EntryFeePerUnit * lot.SignedQuantity
                    : (lot.EntryPrice - mark) * Math.Abs(lot.SignedQuantity) - lot.EntryFeePerUnit * Math.Abs(lot.SignedQuantity));
            exposures.Add(new PaperInstrumentExposureSnapshot(
                instrumentId,
                signedQuantity,
                averageEntry,
                mark,
                marketValue,
                unrealized,
                markBasis));
        }

        var cashFlow = economics.Cash.Count == 0
            ? 0m
            : ExecutionNumericBoundary.ToDecimal(economics.Cash[0].Total);
        var currentCash = openingBalance + cashFlow;
        var markedEquity = currentCash + exposures.Sum(item => item.MarketValue);
        var realizedPnl = closedTrades.Sum(item => item.RealizedProfitAndLoss);
        var periods = Ranges.Select(range => CalculatePeriod(
            openingBalance,
            closedTrades,
            range,
            asOfUtc.UtcDateTime)).ToArray();
        return new PaperPortfolioAnalyticsSnapshot(
            openingBalance,
            cashFlow,
            currentCash,
            realizedPnl,
            markedEquity - openingBalance - realizedPnl,
            markedEquity,
            exposures.Sum(item => Math.Abs(item.MarketValue)),
            exposures.Sum(item => item.MarketValue),
            exposures.Count,
            Array.AsReadOnly(closedTrades.ToArray()),
            Array.AsReadOnly(exposures.ToArray()),
            Array.AsReadOnly(periods),
            currency,
            exposures.Any(item => item.MarkBasis == PaperMarkBasis.LatestLedgerFill)
                ? "Current Paper marks where observed; latest immutable fill fallback"
                : "Current Paper marks");
    }

    public static double AnnualizedSharpe(IReadOnlyList<double> periodicReturns)
    {
        ArgumentNullException.ThrowIfNull(periodicReturns);
        if (periodicReturns.Count < 2) return 0d;
        var mean = periodicReturns.Average();
        var sumSquaredDeviation = periodicReturns.Sum(value =>
        {
            var delta = value - mean;
            return delta * delta;
        });
        var sampleDeviation = Math.Sqrt(sumSquaredDeviation / (periodicReturns.Count - 1));
        return sampleDeviation <= 1e-12 ? 0d : mean / sampleDeviation * Math.Sqrt(252d);
    }

    private static PaperPerformancePeriodSnapshot CalculatePeriod(
        decimal openingBalance,
        IReadOnlyList<PaperClosedTradeSnapshot> history,
        PaperExecutionTimeRange range,
        DateTime asOfUtc)
    {
        var start = range switch
        {
            PaperExecutionTimeRange.SevenDays => asOfUtc.Date.AddDays(-6),
            PaperExecutionTimeRange.ThirtyDays => asOfUtc.Date.AddDays(-29),
            PaperExecutionTimeRange.NinetyDays => asOfUtc.Date.AddDays(-89),
            PaperExecutionTimeRange.YearToDate => new DateTime(asOfUtc.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            _ => throw new ArgumentOutOfRangeException(nameof(range)),
        };
        var eligible = history.Where(item => item.ClosedAtUtc.UtcDateTime <= asOfUtc).ToArray();
        var equityAtStart = openingBalance + eligible
            .Where(item => item.ClosedAtUtc.UtcDateTime.Date < start)
            .Sum(item => item.RealizedProfitAndLoss);
        var periodTrades = eligible.Where(item => item.ClosedAtUtc.UtcDateTime.Date >= start).ToArray();
        var dailyLookup = periodTrades
            .GroupBy(item => item.ClosedAtUtc.UtcDateTime.Date)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.RealizedProfitAndLoss));
        var daily = new List<PaperDailyProfitAndLossSnapshot>();
        for (var date = start; date <= asOfUtc.Date; date = date.AddDays(1))
        {
            dailyLookup.TryGetValue(date, out var dailyPnl);
            daily.Add(new PaperDailyProfitAndLossSnapshot(date, dailyPnl));
        }
        if (daily.Count > MaximumChartPoints) daily = daily[^MaximumChartPoints..];

        var rolling = equityAtStart;
        var peak = equityAtStart;
        var maximumDrawdown = 0m;
        var returns = new List<double>(daily.Count);
        var equity = new List<PaperEquityPointSnapshot>(daily.Count);
        foreach (var point in daily)
        {
            var prior = rolling;
            rolling += point.RealizedProfitAndLoss;
            if (prior != 0m) returns.Add((double)(point.RealizedProfitAndLoss / prior));
            if (rolling > peak) peak = rolling;
            if (peak > 0m)
                maximumDrawdown = Math.Min(maximumDrawdown, (rolling - peak) / peak * 100m);
            equity.Add(new PaperEquityPointSnapshot(point.DateUtc, rolling));
        }
        var periodPnl = daily.Sum(item => item.RealizedProfitAndLoss);
        var winning = periodTrades.Count(item => item.RealizedProfitAndLoss > 0m);
        return new PaperPerformancePeriodSnapshot(
            range,
            range switch
            {
                PaperExecutionTimeRange.SevenDays => "7D",
                PaperExecutionTimeRange.ThirtyDays => "30D",
                PaperExecutionTimeRange.NinetyDays => "90D",
                PaperExecutionTimeRange.YearToDate => "YTD",
                _ => throw new ArgumentOutOfRangeException(nameof(range)),
            },
            equityAtStart,
            rolling,
            periodPnl,
            equityAtStart == 0m ? 0m : periodPnl / equityAtStart * 100m,
            AnnualizedSharpe(returns),
            maximumDrawdown,
            periodTrades.Length == 0 ? 0m : winning * 100m / periodTrades.Length,
            periodTrades.Length,
            winning,
            Array.AsReadOnly(equity.ToArray()),
            Array.AsReadOnly(daily.ToArray()));
    }

    private sealed record OpenLot(decimal SignedQuantity, decimal EntryPrice, decimal EntryFeePerUnit);
}
