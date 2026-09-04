# TradingTerminal.Sandbox.Portfolio — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Sandbox/TradingTerminal.Sandbox.Portfolio/ModelPortfolioFault.cs
```cs
    7: public enum ModelPortfolioFault : byte
```

## src/linux/Sandbox/TradingTerminal.Sandbox.Portfolio/ModelPortfolioSimulator.cs
```cs
    9: public sealed class ModelPortfolioSimulator
   49: public const int MaximumAbsoluteUnits = 100;
   53: public const int MaximumRetainedClosedTrips = 256;
   64: public static ModelPortfolioFault TryCreate(
   83: public ModelPortfolioSnapshot CommittedSnapshot => new(
  109: public ModelPortfolioFault BeginOnBar(double close)
  124: public ModelPortfolioFault BeginOnTick(double bid, double ask, double last)
  160: public ModelPortfolioFault CommitCallback()
  190: public void RollbackCallback()
  200: public ModelPortfolioFault CompleteRun()
  234: public ModelPortfolioFault MpMarket(double signedUnits, out double fillPrice)
  257: public ModelPortfolioFault MpClose(double fraction, out double fillPrice)
  297: public ModelPortfolioFault MpStop(long mode, double value)
  335: public ModelPortfolioFault MpTarget(long mode, double value)
  367: public ModelPortfolioFault MpTrail(long mode, double value, double activationR)
  430: public ModelPortfolioFault MpPendingEntry(double price, double signedTargetUnits, bool isStop)
  456: public ModelPortfolioFault MpCancelPendingEntry()
  469: public ModelPortfolioFault MpPendingEntryState(
  531: public ModelPortfolioFault MpCancelExits()
  546: public ModelPortfolioFault MpPosition(out double position)
  557: public ModelPortfolioFault MpEntry(out double entryPrice)
  568: public ModelPortfolioFault MpBarsHeld(out long barsHeld)
  579: public ModelPortfolioFault MpOpenR(out double riskMultiple)
  605: public ModelPortfolioFault MpEquity(out double equity)
  629: public ModelPortfolioFault MpTradeCount(out long tradeCount)
  640: public ModelPortfolioFault MpTradeR(long n, out double riskMultiple)
  662: public ModelPortfolioFault MpTradeHasR(long n, out long hasR)
  673: public ModelPortfolioFault MpTradeUnits(long n, out double peakAbsoluteUnits)
  684: public ModelPortfolioFault MpTradeBars(long n, out long barsHeld)
  695: public ModelPortfolioFault MpStreak(out long streak)
```

## src/linux/Sandbox/TradingTerminal.Sandbox.Portfolio/ModelPortfolioSnapshot.cs
```cs
   27: public readonly record struct ModelPortfolioSnapshot(
```

## src/linux/Sandbox/TradingTerminal.Sandbox.Portfolio/PortfolioState.cs
```cs
    6: public double PositionUnits;
    7: public double PositionQuantity;
    8: public double AverageEntryPrice;
    9: public long BarsHeld;
   11: public double RealizedGrossProfitLoss;
   12: public double CommissionTotal;
   13: public double SlippageTotal;
   14: public double LastSampledEquity;
   15: public double EquityPeak;
   16: public double MaximumDrawdown;
   18: public long LifetimeClosedTripCount;
   19: public long LifetimeWinningTripCount;
   20: public long LifetimeLosingTripCount;
   22: public double TripGrossProfitLoss;
   23: public double TripEntryCommission;
   24: public double TripEntrySlippage;
   25: public double TripExitCommission;
   26: public double TripExitSlippage;
   27: public double TripPeakAbsoluteUnits;
   28: public double TripPeakAbsoluteQuantity;
   30: public bool HasCapturedR;
   31: public double CapturedR;
   33: public bool HasStop;
   34: public double StopPrice;
   35: public bool HasTarget;
   36: public double TargetPrice;
   37: public bool HasTrail;
   38: public double TrailDistance;
   39: public double TrailActivationR;
   40: public double TrailHighWaterMark;
   41: public bool TrailArmed;
   45: public bool HasPendingEntry;
   46: public double PendingEntryPrice;
   47: public double PendingEntryUnits;
   48: public bool PendingEntryIsStop;
   50: public int TripRingNextIndex;
   51: public int TripRingCount;
   52: public long Streak;
```
