using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Trading;
using TradingTerminal.UI.Execution;
using Xunit;

namespace TradingTerminal.UI.Core.Tests;

public sealed class PaperPortfolioAnalyticsTests
{
    private static readonly InstrumentId Instrument = new(7);
    private static readonly ExecutionResource Resource = new(
        new VenueId("paper-simulator"),
        new TradingAccountId("analytics-paper"),
        ExecutionEnvironment.SimulatedPaper);
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Calculate_uses_fifo_fees_cash_flow_and_current_mark_without_inventing_broker_cash()
    {
        var fills = new[]
        {
            Fill("buy-2", OrderSide.Buy, 2m, 100m, 2m, Now.AddDays(-2)),
            Fill("sell-1", OrderSide.Sell, 1m, 110m, 1m, Now.AddDays(-1)),
        };
        var economics = Snapshot(fills, cashFlow: -93m, position: 1m);

        var result = PaperPortfolioAnalyticsCalculator.Calculate(
            1_000m,
            economics,
            Now,
            id => id == Instrument ? ExecutionNumericBoundary.PriceFromDecimal(110m) : null);

        Assert.Equal(1_000m, result.OpeningBalance);
        Assert.Equal(-93m, result.LedgerCashFlow);
        Assert.Equal(907m, result.CurrentCash);
        Assert.Equal(8m, result.RealizedProfitAndLoss);
        Assert.Equal(9m, result.UnrealizedProfitAndLoss);
        Assert.Equal(1_017m, result.MarkedEquity);
        Assert.Equal(110m, result.GrossExposure);
        Assert.Equal(110m, result.NetExposure);
        Assert.Equal(PaperMarkBasis.CurrentPaperMark, Assert.Single(result.Exposures).MarkBasis);
        Assert.Equal(8m, Assert.Single(result.ClosedTrades).RealizedProfitAndLoss);
    }

    [Fact]
    public void Calculate_handles_short_close_and_reversal_as_distinct_fifo_lots()
    {
        var fills = new[]
        {
            Fill("sell-2", OrderSide.Sell, 2m, 100m, 0m, Now.AddDays(-2)),
            Fill("buy-3", OrderSide.Buy, 3m, 90m, 0m, Now.AddDays(-1)),
        };
        var economics = Snapshot(fills, cashFlow: -70m, position: 1m);

        var result = PaperPortfolioAnalyticsCalculator.Calculate(1_000m, economics, Now);

        Assert.Equal(20m, result.RealizedProfitAndLoss);
        Assert.Equal(1_020m, result.MarkedEquity);
        var exposure = Assert.Single(result.Exposures);
        Assert.Equal(1m, exposure.SignedQuantity);
        Assert.Equal(90m, exposure.AverageEntryPrice);
        Assert.Equal(PaperMarkBasis.LatestLedgerFill, exposure.MarkBasis);
        Assert.Contains("latest immutable fill", result.ValuationBasis, StringComparison.Ordinal);
    }

    [Fact]
    public void Calculate_builds_all_windows_equivalent_ranges_and_carries_prior_realized_equity()
    {
        var fills = new[]
        {
            Fill("buy-old", OrderSide.Buy, 1m, 100m, 0m, Now.AddDays(-41)),
            Fill("sell-old", OrderSide.Sell, 1m, 120m, 0m, Now.AddDays(-40)),
            Fill("buy-new", OrderSide.Buy, 1m, 100m, 0m, Now.AddDays(-6)),
            Fill("sell-new", OrderSide.Sell, 1m, 90m, 0m, Now.AddDays(-5)),
        };
        var economics = Snapshot(fills, cashFlow: 10m, position: 0m);

        var result = PaperPortfolioAnalyticsCalculator.Calculate(1_000m, economics, Now);
        var thirtyDays = result.Period(PaperExecutionTimeRange.ThirtyDays);

        Assert.Equal(4, result.Periods.Count);
        Assert.Equal(1_020m, thirtyDays.EquityAtStart);
        Assert.Equal(-10m, thirtyDays.RealizedProfitAndLoss);
        Assert.Equal(1_010m, thirtyDays.RealizedEquity);
        Assert.Equal(-10m / 1_020m * 100m, thirtyDays.ReturnPercent);
        Assert.Equal(30, thirtyDays.DailyProfitAndLossSeries.Count);
        Assert.Equal(1, thirtyDays.TradeCount);
        Assert.Equal(0, thirtyDays.WinningTrades);
    }

    [Fact]
    public void Calculate_rejects_nonpositive_opening_balance()
    {
        var economics = Snapshot([], cashFlow: 0m, position: 0m);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            PaperPortfolioAnalyticsCalculator.Calculate(0m, economics, Now));
    }

    private static ReconciliationFillSnapshot Fill(
        string id,
        OrderSide side,
        decimal quantity,
        decimal price,
        decimal fee,
        DateTimeOffset occurredAtUtc) =>
        new(
            new TradeId(id),
            new ClientOrderId("client-" + id),
            null,
            null,
            Instrument,
            side,
            ExecutionNumericBoundary.QuantityFromDecimal(quantity),
            ExecutionNumericBoundary.PriceFromDecimal(price),
            ExecutionNumericBoundary.MoneyFromDecimal(fee),
            occurredAtUtc);

    private static ExecutionReconciliationSnapshot Snapshot(
        IReadOnlyList<ReconciliationFillSnapshot> fills,
        decimal cashFlow,
        decimal position) =>
        new(
            Resource,
            Now,
            [],
            fills,
            position == 0m
                ? []
                : [new ReconciliationPositionSnapshot(
                    Instrument,
                    ExecutionNumericBoundary.QuantityFromDecimal(position),
                    Now)],
            [new ReconciliationCashSnapshot(
                "SIM",
                ExecutionNumericBoundary.MoneyFromDecimal(cashFlow),
                ExecutionNumericBoundary.MoneyFromDecimal(cashFlow),
                Now)]);
}
