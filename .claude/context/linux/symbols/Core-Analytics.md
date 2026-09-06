# TradingTerminal.Core / Analytics — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Core/TradingTerminal.Core/Analytics/CorrelationCalculator.cs
```cs
   14: public static class CorrelationCalculator
   19: public static IReadOnlyList<double> LogReturns(IReadOnlyList<double> closes)
   41: public static (IReadOnlyList<DateTime> Timestamps, double[][] AlignedCloses) AlignByTimestamp(
   78: public static double Pearson(IReadOnlyList<double> a, IReadOnlyList<double> b)
  108: public static double[,] PearsonMatrix(IReadOnlyList<IReadOnlyList<double>> returnSeries)
```

## src/linux/Core/TradingTerminal.Core/Analytics/CorrelationResult.cs
```cs
   10: public sealed record CorrelationMatrix(
   15: public int Size => Labels.Count;
   17: public double At(int row, int col) => Values[row, col];
```
