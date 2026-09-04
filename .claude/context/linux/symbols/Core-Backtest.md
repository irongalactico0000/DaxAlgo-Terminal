# TradingTerminal.Core / Backtest — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Core/TradingTerminal.Core/Backtest/BacktestConfig.cs
```cs
    9: public enum BacktestDataSource
   44: public sealed record BacktestConfig(
   67: public sealed record BacktestBarSeries(
```

## src/linux/Core/TradingTerminal.Core/Backtest/BacktestInstrumentEvent.cs
```cs
   10: public sealed record BacktestInstrumentEvent(
   25: public interface IInstrumentAwareBacktestStrategy
   27:     Task OnMarketEventBatchAsync(
   28:     IReadOnlyList<BacktestInstrumentEvent> events,
   29:     Time.IClock clock,
   30:     Trading.IOrderRouter router,
   31:     CancellationToken ct);
```

## src/linux/Core/TradingTerminal.Core/Backtest/BacktestResult.cs
```cs
   10: public sealed record BacktestResult(
```

## src/linux/Core/TradingTerminal.Core/Backtest/BacktestStatistics.cs
```cs
   19: public sealed record BacktestStatistics(
```

## src/linux/Core/TradingTerminal.Core/Backtest/BacktestStrategyOption.cs
```cs
   14: public sealed record BacktestStrategyOption(
   21: public StrategyParameterSchema Schema { get; init; } = StrategyParameterSchema.Empty;
   24: public Func<Contract, StrategyParameters, IBacktestStrategy>? ParameterizedBuild { get; init; }
   32: public Func<Contract, IBacktestStrategy>? BacktestBuild { get; init; }
   36: public IBacktestStrategy CreateForBacktest(Contract contract) =>
   47: public Func<WalkForwardAxes, IReadOnlyList<WalkForwardCandidate>>? WalkForwardGrid { get; init; }
   50: public bool HasParameters => !Schema.IsEmpty;
   58: public StrategyDataRequirement DataRequirement { get; init; } =
   67: public string? ResearchPaperUrl { get; init; }
   74: public IBacktestStrategy Create(Contract contract, StrategyParameters? parameters = null) =>
```

## src/linux/Core/TradingTerminal.Core/Backtest/EquityPoint.cs
```cs
    4: public sealed record EquityPoint(DateTime TimestampUtc, double Equity);
```

## src/linux/Core/TradingTerminal.Core/Backtest/Fast/FastBacktestRequest.cs
```cs
   13: public sealed record FastBacktestRequest(
```

## src/linux/Core/TradingTerminal.Core/Backtest/Fast/FastBacktestResult.cs
```cs
   11: public sealed record FastBacktestResult(
```

## src/linux/Core/TradingTerminal.Core/Backtest/Fast/IFastBacktestRunner.cs
```cs
   14: public interface IFastBacktestRunner
   16:     bool IsAvailable { get; }
   18:     Task<FastBacktestResult> RunAsync(FastBacktestRequest request, CancellationToken ct = default);
   21: public sealed class FastBacktestUnavailableException : Exception
   23: public FastBacktestUnavailableException(string message) : base(message) { }
   24: public FastBacktestUnavailableException(string message, Exception inner) : base(message, inner) { }
```

## src/linux/Core/TradingTerminal.Core/Backtest/FillRecord.cs
```cs
   10: public sealed record FillRecord(
```

## src/linux/Core/TradingTerminal.Core/Backtest/IBacktestSession.cs
```cs
   10: public interface IBacktestSession
   16:     Task<BacktestResult> RunAsync(
   17:     BacktestConfig config,
   18:     IBacktestStrategy strategy,
   19:     IRiskManager? risk = null,
   20:     CancellationToken ct = default);
```

## src/linux/Core/TradingTerminal.Core/Backtest/IBacktestStrategy.cs
```cs
   16: public interface IBacktestStrategy
   19:     Task OnStartAsync(IClock clock, IOrderRouter router, CancellationToken ct);
   22:     Task OnTickAsync(Tick tick, IClock clock, IOrderRouter router, CancellationToken ct);
   28:     Task OnBarAsync(Bar bar, IClock clock, IOrderRouter router, CancellationToken ct)
   29:     => Task.CompletedTask;
   38:     Task OnDepthAsync(DepthSnapshot depth, IClock clock, IOrderRouter router, CancellationToken ct)
   39:     => Task.CompletedTask;
   49:     Task OnTradeAsync(TradePrint trade, IClock clock, IOrderRouter router, CancellationToken ct)
   50:     => Task.CompletedTask;
   53:     Task OnOrderEventAsync(OrderEvent evt, CancellationToken ct);
   56:     Task OnEndAsync(IClock clock, IOrderRouter router, CancellationToken ct);
```

## src/linux/Core/TradingTerminal.Core/Backtest/IParquetQueryService.cs
```cs
   16: public interface IParquetQueryService
   23:     IAsyncEnumerable<Tick> ReadTicksAsync(
   24:     string parquetGlob,
   25:     DateTime? fromUtc = null,
   26:     DateTime? toUtc = null,
   27:     CancellationToken ct = default);
   36:     Task<IReadOnlyList<OhlcvAggregate>> AggregateBarsAsync(
   37:     string parquetGlob,
   38:     TimeSpan interval,
   39:     DateTime? fromUtc = null,
   40:     DateTime? toUtc = null,
   41:     CancellationToken ct = default);
   50:     Task<ParquetQueryResult> QueryAsync(string sql, CancellationToken ct = default);
   55: public sealed record OhlcvAggregate(
   66: public sealed record ParquetQueryResult(
```

## src/linux/Core/TradingTerminal.Core/Backtest/MonteCarlo.cs
```cs
   17: public static class MonteCarlo
   19: public sealed record Result(
   37: public static Result Run(
```

## src/linux/Core/TradingTerminal.Core/Backtest/Trade.cs
```cs
    9: public sealed record Trade(
```

## src/linux/Core/TradingTerminal.Core/Backtest/TransactionCostAnalysis.cs
```cs
   15: public static class TransactionCostAnalysis
   17: public sealed record Report(
   33: public sealed record HourBucket(int Hour, int Fills, double MeanSlippage, double MakerFraction);
   35: public static Report Compute(IReadOnlyList<FillRecord> fills)
```

## src/linux/Core/TradingTerminal.Core/Backtest/WalkForward.cs
```cs
   10: public sealed record WalkForwardCandidate(string Label, Func<Contract, IBacktestStrategy> Build);
   17: public sealed record WalkForwardAxes(
   28: public static WalkForwardAxes Defaults { get; } = new([], [], [], [], [], [], [], 1);
   33: public static T[] Or<T>(T[] axis, params T[] fallback) => axis is { Length: > 0 } ? axis : fallback;
```
