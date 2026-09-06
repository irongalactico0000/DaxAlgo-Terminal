# DaxAlgo.Sdk — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Sdk/DaxAlgo.Sdk/AuthoredPlugin.cs
```cs
   14: public sealed record AuthoredVisualizerPluginRegistration(
   26: public sealed record AuthoredStrategyKernelPluginRegistration(
   43: public sealed record AuthoredStrategyTypes(
   50: public bool HasLiveWindow => Descriptor is not null && ViewModel is not null && View is not null;
   55: public bool CanComposeLiveWindow => Descriptor is not null && ViewModel is not null;
   62: public static AuthoredStrategyTypes DiscoverIn(Assembly assembly)
  109: public static class AuthoredPluginBootstrap
  116: public const int CurrentVerificationContractVersion = 2;
  118: public static void Register(
  132: public static void Register(
```

## src/linux/Sdk/DaxAlgo.Sdk/Drawing/Candles.cs
```cs
   10: public readonly record struct CandleOptions(
   24: public static CandleOptions Default { get; } = new(BodyFraction: 0.7d);
   35: public static class Candles
   38: public static PlotRange Draw(
```

## src/linux/Sdk/DaxAlgo.Sdk/Drawing/Footprint.cs
```cs
   15: public readonly record struct FootprintOptions(
   33: public static FootprintOptions Default { get; } = new(ColumnWidth: 74d);
   47: public static class Footprint
   50: public static void Draw(
  186: public static (double Low, double High) ValueArea(FootprintBar bar, double share = 0.7d)
```

## src/linux/Sdk/DaxAlgo.Sdk/Drawing/Ladder.cs
```cs
   12: public readonly record struct LadderOptions(
   27: public static LadderOptions Default { get; } = new(Levels: 10);
   41: public static class Ladder
   44: public static void Draw(IRenderSurface surface, DepthSnapshot? depth, LadderOptions options = default)
```

## src/linux/Sdk/DaxAlgo.Sdk/Drawing/Plot.cs
```cs
    8: public readonly record struct PlotRange(double Minimum, double Maximum)
   11: public static PlotRange Empty { get; } = new(double.PositiveInfinity, double.NegativeInfinity);
   14: public bool IsValid => double.IsFinite(Minimum) && double.IsFinite(Maximum) && Maximum > Minimum;
   16: public double Span => Maximum - Minimum;
   19: public PlotRange Include(double value) => double.IsFinite(value)
   28: public PlotRange Padded(double fraction = 0.05d)
   52: public static class Plot
   55: public static PlotRange RangeOf<T>(IReadOnlyList<T> items, Func<T, double> select)
   72: public static void HorizontalGrid(
  110: public static void Crosshair(
  139: public static double ToY(double value, PlotRange range, double height) =>
  145: public static double FromY(double y, PlotRange range, double height) =>
  154: public static double NiceStep(double rawStep)
```

## src/linux/Sdk/DaxAlgo.Sdk/IAuthoredDrawingManifest.cs
```cs
    8: public interface IAuthoredDrawingManifest
   11:     IReadOnlyList<string> DrawingLayerTypeIds { get; }
```

## src/linux/Sdk/DaxAlgo.Sdk/IPluginRegistrar.cs
```cs
   19: public interface IPluginRegistrar
   23:     IServiceCollection Services { get; }
   26:     PluginContext Context { get; }
   33: public sealed record PluginContext(string Name, string AssemblyPath, string TargetSdkVersion);
```

## src/linux/Sdk/DaxAlgo.Sdk/IRenderSurface.cs
```cs
    4: public enum RenderPanelKind
   20: public enum RenderSeriesKind
   30: public enum RenderMarkerShape
   45: public enum RenderThemeColor
   64: public readonly record struct RenderColor(byte R, byte G, byte B);
   75: public readonly record struct RenderStyle(
   86: public readonly record struct RenderViewport(double Width, double Height, double Scale);
   97: public readonly record struct RenderCursor(double X, double Y, bool IsInside, bool IsPressed);
  115: public interface IRenderSurface
  118:     RenderViewport Viewport { get; }
  121:     RenderCursor Cursor { get; }
  124:     RenderColor Theme(RenderThemeColor token);
  133:     IDisposable Layer(string layerId, string typeId)
  135:     ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
  136:     ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
  137:     return EmptyRenderScope.Instance;
  141:     void SetStyle(RenderStyle style);
  148:     IDisposable Panel(string title, RenderPanelKind kind);
  151:     void AxisX(double minimum, double maximum, string? format = null);
  154:     void AxisY(double minimum, double maximum, string? format = null);
  160:     IDisposable Series(string name, RenderSeriesKind kind);
  163:     void Push(double x, double y);
  166:     void Line(double x1, double y1, double x2, double y2);
  169:     void Rect(double x, double y, double width, double height, bool filled = true);
  172:     void Text(double x, double y, string text);
  175:     void Marker(double x, double y, RenderMarkerShape shape);
  181: public void Dispose() { }
```

## src/linux/Sdk/DaxAlgo.Sdk/IStrategyEngineFactory.cs
```cs
   13: public interface IStrategyEngineFactory
   16:     StrategyParameterSchema Schema { get; }
   19:     StrategyDataRequirement DataRequirement { get; }
   22:     IBacktestStrategy Create(Contract contract, StrategyParameters parameters);
   25:     IBacktestStrategy Create(Contract contract) => Create(contract, Schema.CreateDefaults());
```

## src/linux/Sdk/DaxAlgo.Sdk/IStrategyKernel.cs
```cs
   11: public interface IStrategyKernel
   14:     StrategyParameterSchema Schema { get; }
   17:     StrategyDataRequirement DataRequirement { get; }
   20:     Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct);
   23:     Task OnQuoteAsync(Quote quote, IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;
   26:     Task OnTradeAsync(TradePrint trade, IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;
   29:     Task OnDepthAsync(
   30:     InstrumentId instrument,
   31:     DepthSnapshot depth,
   32:     IStrategyRuntimeContext context,
   33:     CancellationToken ct) => Task.CompletedTask;
   36:     Task OnBarAsync(OhlcvBar bar, IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;
   50:     void Draw(IRenderSurface surface)
   55:     Task OnStopAsync(IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;
```

## src/linux/Sdk/DaxAlgo.Sdk/IStrategyLifecycle.cs
```cs
    4: public interface IStrategyLifecycle
    7:     bool IsRunning { get; }
   10:     bool IsPaused { get; }
   13:     Task RunAsync(CancellationToken ct = default);
   16:     Task PauseAsync(CancellationToken ct = default);
   22:     Task ResumeAsync(CancellationToken ct = default);
   25:     Task StopAsync(CancellationToken ct = default);
   32: public interface IVisualizerLifecycle
   35:     bool IsRunning { get; }
   38:     bool IsPaused { get; }
   41:     Task PauseAsync(CancellationToken ct = default);
   47:     Task ResumeAsync(CancellationToken ct = default);
   50:     Task StopAsync(CancellationToken ct = default);
```

## src/linux/Sdk/DaxAlgo.Sdk/IStrategyPlugin.cs
```cs
   16: public interface IStrategyPlugin
   19:     string Name { get; }
   24:     string TargetSdkVersion { get; }
   27:     void Register(IPluginRegistrar registrar);
```

## src/linux/Sdk/DaxAlgo.Sdk/IVisualizer.cs
```cs
   11: public interface IVisualizer
   14:     StrategyParameterSchema Schema { get; }
   17:     StrategyDataRequirement DataRequirement { get; }
   20:     Task OnStartAsync(IVisualizerContext context, CancellationToken ct);
   23:     Task OnQuoteAsync(Quote quote, IVisualizerContext context, CancellationToken ct) => Task.CompletedTask;
   26:     Task OnTradeAsync(TradePrint trade, IVisualizerContext context, CancellationToken ct) => Task.CompletedTask;
   29:     Task OnDepthAsync(
   30:     InstrumentId instrument,
   31:     DepthSnapshot depth,
   32:     IVisualizerContext context,
   33:     CancellationToken ct) => Task.CompletedTask;
   36:     Task OnBarAsync(OhlcvBar bar, IVisualizerContext context, CancellationToken ct) => Task.CompletedTask;
   50:     void Draw(IRenderSurface surface)
   55:     Task OnStopAsync(IVisualizerContext context, CancellationToken ct) => Task.CompletedTask;
```

## src/linux/Sdk/DaxAlgo.Sdk/NullRenderSurface.cs
```cs
   14: public sealed class NullRenderSurface : IRenderSurface
   20: public void Dispose()
   26: public static NullRenderSurface Instance { get; } = new();
   28: public RenderViewport Viewport => new(0d, 0d, 1d);
   30: public RenderCursor Cursor => new(0d, 0d, IsInside: false, IsPressed: false);
   32: public RenderColor Theme(RenderThemeColor token) => new(0, 0, 0);
   34: public void SetStyle(RenderStyle style)
   38: public IDisposable Panel(string title, RenderPanelKind kind) => NoScope.Instance;
   40: public void AxisX(double minimum, double maximum, string? format = null)
   44: public void AxisY(double minimum, double maximum, string? format = null)
   48: public IDisposable Series(string name, RenderSeriesKind kind) => NoScope.Instance;
   50: public void Push(double x, double y)
   54: public void Line(double x1, double y1, double x2, double y2)
   58: public void Rect(double x, double y, double width, double height, bool filled = true)
   62: public void Text(double x, double y, string text)
   66: public void Marker(double x, double y, RenderMarkerShape shape)
```

## src/linux/Sdk/DaxAlgo.Sdk/SandboxContexts.cs
```cs
   14: public interface IMarketDataView
   17:     IReadOnlySet<InstrumentId> Instruments { get; }
   20:     StrategyDataRequirement DataRequirement { get; }
   23:     IReadOnlyList<OhlcvBar> RecentBars(InstrumentId instrument, BarSize size, int maxCount);
   26:     IReadOnlyList<Quote> RecentQuotes(InstrumentId instrument, int maxCount);
   29:     DepthSnapshot? LatestDepth(InstrumentId instrument);
   32:     IReadOnlyList<TradePrint> RecentTrades(InstrumentId instrument, int maxCount);
   36: public interface IParameters
   39:     StrategyParameterSchema Schema { get; }
   42:     int GetInt(string name);
   45:     long GetLong(string name);
   48:     double GetDouble(string name);
   51:     bool GetBool(string name);
   54:     string GetString(string name);
   57:     string GetText(string name);
   60:     TEnum GetEnum<TEnum>(string name) where TEnum : struct, System.Enum;
   63:     InstrumentId GetInstrument(string name);
   67: public enum VirtualEntryKind
   96: public sealed record VirtualTargetIntent(
  105: public bool IsPendingEntry =>
  113: public interface IVirtualBook
  116:     void SubmitTarget(VirtualTargetIntent intent);
  119:     void SetTargetPosition(
  120:     InstrumentId instrument,
  121:     double targetUnits,
  122:     double? protectiveStopPrice = null,
  123:     double? profitTargetPrice = null) =>
  124:     SubmitTarget(new VirtualTargetIntent(
  125:     instrument,
  126:     targetUnits,
  127:     protectiveStopPrice,
  128:     profitTargetPrice));
  139:     void SetPendingEntry(
  140:     InstrumentId instrument,
  141:     double targetUnits,
  142:     VirtualEntryKind kind,
  143:     double triggerPrice,
  144:     double? protectiveStopPrice = null,
  145:     double? profitTargetPrice = null) =>
  146:     SubmitTarget(new VirtualTargetIntent(
  147:     instrument,
  148:     targetUnits,
  149:     protectiveStopPrice,
  150:     profitTargetPrice,
  151:     kind,
  152:     triggerPrice));
  156: public enum AlertLevel
  172: public static class AlertLimits
  175: public const int MaxMessageLength = 512;
  178: public const int MaxDedupeKeyLength = 128;
  186: public interface IAlertSink
  193:     void Alert(string message, AlertLevel level, string? dedupeKey = null);
  196:     void AlertIf(bool condition, string message, AlertLevel level, string? dedupeKey = null)
  198:     if (condition)
  199:     Alert(message, level, dedupeKey);
  204: public interface IStrategyRuntimeContext
  207:     IMarketDataView Data { get; }
  210:     IClock Clock { get; }
  213:     IParameters Parameters { get; }
  216:     IVirtualBook Book { get; }
  219:     IAlertSink Alerts { get; }
  226: public interface IVisualizerContext
  229:     IMarketDataView Data { get; }
  232:     IClock Clock { get; }
  235:     IParameters Parameters { get; }
  238:     IAlertSink Alerts { get; }
```

## src/linux/Sdk/DaxAlgo.Sdk/SdkInfo.cs
```cs
   14: public static class SdkInfo
   17: public const string Version = "0.2.0-alpha";
```
