# TradingTerminal.TradeIr.Runtime — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Backtest/TradingTerminal.TradeIr.Runtime/TradeIrEvaluatorV1.cs
```cs
    8: public sealed class TradeIrEvaluatorV1
   23: public TradeIrEvaluatorV1(CompiledTradeIrPlanV1 plan)
   40: public TradeIrOrderIntentV1? EvaluateQuote(TradeIrQuoteFrameV1 frame)
  142: public void ApplyOrderFeedback(TradeIrOrderFeedbackV1 feedback)
  197: public TradeIrOrderIntentV1? End(TradeIrPortfolioFrameV1 portfolio)
  453: public bool IsReady;
  454: public double Number;
  455: public bool Boolean;
  456: public long Target;
  457: public bool Exit;
  459: public static RuntimeSlotValue FromNumber(double value) => new() { IsReady = true, Number = value };
  460: public static RuntimeSlotValue FromBoolean(bool value) => new() { IsReady = true, Boolean = value };
  461: public static RuntimeSlotValue FromTarget(long value) => new() { IsReady = true, Target = value };
  462: public static RuntimeSlotValue FromExit(bool value) => new() { IsReady = true, Exit = value };
  463: public static RuntimeSlotValue IntentReady => new() { IsReady = true };
  471: public EmaState(int period)
  477: public int Period { get; }
  478: public double Value { get; private set; }
  479: public bool IsReady => _sampleCount >= Period;
  481: public void Push(double value)
  500: public bool IsReady => _readyInputObservations > 1;
  502: public bool Evaluate(double price, long currentPosition, double fraction)
```

## src/linux/Backtest/TradingTerminal.TradeIr.Runtime/TradeIrRuntimeContractsV1.cs
```cs
    3: public enum TradeIrOrderSideV1
    9: public enum TradeIrTimeInForceV1
   16: public enum TradeIrOrderKindV1
   21: public enum TradeIrOrderFeedbackStatusV1
   36: public static class TradeIrRuntimeLimitsV1
   38: public const int MaximumInstructionCount = 4_096;
   39: public const long MaximumAbsolutePositionQuantity = long.MaxValue / 2;
   41: public static bool IsSupportedPositionQuantity(long value) =>
   49: public abstract class TradeIrInstructionV1
   58: public int Slot { get; }
   59: public string NodeId { get; }
   62: public sealed class QuoteMidInstructionV1 : TradeIrInstructionV1
   64: public QuoteMidInstructionV1(int slot, string nodeId, string requirementId)
   68: public string RequirementId { get; }
   71: public sealed class EmaInstructionV1 : TradeIrInstructionV1
   73: public EmaInstructionV1(int slot, string nodeId, int valueSlot, int period)
   82: public int ValueSlot { get; }
   83: public int Period { get; }
   86: public sealed class GreaterThanInstructionV1 : TradeIrInstructionV1
   88: public GreaterThanInstructionV1(int slot, string nodeId, int leftSlot, int rightSlot)
   97: public int LeftSlot { get; }
   98: public int RightSlot { get; }
  101: public sealed class FixedQuantityInstructionV1 : TradeIrInstructionV1
  103: public FixedQuantityInstructionV1(
  121: public int DecisionSlot { get; }
  122: public long WhenFalse { get; }
  123: public long WhenTrue { get; }
  126: public sealed class TrailingFractionInstructionV1 : TradeIrInstructionV1
  128: public TrailingFractionInstructionV1(
  145: public int PriceSlot { get; }
  146: public int TargetSlot { get; }
  147: public double Fraction { get; }
  150: public sealed class MarketIntentInstructionV1 : TradeIrInstructionV1
  152: public MarketIntentInstructionV1(
  168: public int TargetSlot { get; }
  169: public int? ExitSlot { get; }
  170: public TradeIrTimeInForceV1 TimeInForce { get; }
  177: public sealed record CompiledTradeIrPlanV1
  204: public string DefinitionSha256 { get; }
  205: public string AdmissionManifestSha256 { get; }
  206: public string RuntimeSemanticsVersion { get; }
  207: public string InstrumentKey { get; }
  208: public IReadOnlyList<TradeIrInstructionV1> Instructions { get; }
  209: public string OrderIntentOutputId { get; }
  210: public string OrderIntentNodeId { get; }
  211: public bool FlattenOnEnd { get; }
  214: public sealed record TradeIrQuoteFrameV1
  216: public TradeIrQuoteFrameV1(
  242: public string InstrumentKey { get; }
  243: public string AdmissionManifestSha256 { get; }
  244: public long EventSequence { get; }
  245: public long EventTimeUnixMicroseconds { get; }
  246: public double Bid { get; }
  247: public double Ask { get; }
  248: public long CurrentPositionQuantity { get; }
  251: public sealed record TradeIrPortfolioFrameV1
  253: public TradeIrPortfolioFrameV1(
  269: public string InstrumentKey { get; }
  270: public long EventSequence { get; }
  271: public long EventTimeUnixMicroseconds { get; }
  272: public long CurrentPositionQuantity { get; }
  275: public sealed record TradeIrOrderFeedbackV1
  277: public TradeIrOrderFeedbackV1(
  290: public long IntentSequence { get; }
  291: public TradeIrOrderFeedbackStatusV1 Status { get; }
  292: public long CumulativeFilledQuantity { get; }
  299: public sealed record TradeIrOrderIntentV1
  341: public string DefinitionSha256 { get; }
  342: public string AdmissionManifestSha256 { get; }
  343: public long IntentSequence { get; }
  344: public long SourceEventSequence { get; }
  345: public string OutputId { get; }
  346: public string NodeId { get; }
  347: public string InstrumentKey { get; }
  348: public TradeIrOrderKindV1 Kind => TradeIrOrderKindV1.Market;
  349: public TradeIrOrderSideV1 Side { get; }
  350: public long Quantity { get; }
  351: public long TargetQuantity { get; }
  352: public TradeIrTimeInForceV1 TimeInForce { get; }
  353: public bool ReduceOnly { get; }
  354: public long EventTimeUnixMicroseconds { get; }
  359: public static string RequireText(string value, string parameterName)
  369: public static string RequireSha256(string value, string parameterName)
```

## src/linux/Backtest/TradingTerminal.TradeIr.Runtime/TradeIrRuntimeSemanticsV1.cs
```cs
    8: public static class TradeIrRuntimeSemanticsV1
   10: public const string Version = "daxalgo.tradeir.runtime/v1";
   12: public const string QuoteMidContract =
   15: public const string EmaContract =
   18: public const string GreaterThanContract =
   21: public const string FixedQuantityContract =
   24: public const string TrailingFractionContract =
   27: public const string MarketIntentContract =
```
