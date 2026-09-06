# TradingTerminal.Infrastructure / Strategies — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Pipeline/TradingTerminal.Infrastructure/Strategies/Authoring/AuthoredStrategyInstaller.cs
```cs
   22: public sealed record AuthoredStrategyInstall(
   28: public sealed record AuthoredUnitPersistence(
   51: public sealed class AuthoredStrategyInstaller(
   63: public AuthoredUnitPersistence PersistAuthoredUnit(
   81: public AuthoredStrategyInstall Install(StrategyScript script, StrategyCompileResult compiled)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Strategies/Authoring/RoslynAuthoredUnitCompilerV1.cs
```cs
   25: public sealed class RoslynAuthoredUnitCompilerV1 : IAuthoredUnitCompilerV1
   43: public AuthoredUnitCompilationResultV1 Compile(
  569: public sealed class DaxAlgoAuthoredUnitPlugin : DaxAlgo.Sdk.IStrategyPlugin
  571: public string Name => {{Literal(script.DisplayName)}};
  572: public string TargetSdkVersion => {{Literal(SdkInfo.Version)}};
  574: public void Register(DaxAlgo.Sdk.IPluginRegistrar registrar) =>
  636: public ProbeRuntimeContext(ProbeMarketDataView data, IParameters parameters)
  642: public ProbeMarketDataView Data { get; }
  645: public ProbeClock Clock { get; } = new();
  648: public IParameters Parameters { get; }
  649: public IVirtualBook Book { get; } = new ProbeBook();
  650: public IAlertSink Alerts { get; } = new ProbeAlertSink();
  655: public DateTime UtcNow { get; private set; } =
  658: public void Set(DateTime value) => UtcNow = value;
  665: public ProbeParameters(StrategyParameterSchema schema) => _values = new StrategyParameters(schema);
  666: public StrategyParameterSchema Schema => _values.Schema;
  667: public int GetInt(string name) => _values.GetInt(name);
  668: public long GetLong(string name) => _values.GetLong(name);
  669: public double GetDouble(string name) => _values.GetDouble(name);
  670: public bool GetBool(string name) => _values.GetBool(name);
  671: public string GetString(string name) => _values.GetString(name);
  672: public string GetText(string name) => _values.GetText(name);
  673: public TEnum GetEnum<TEnum>(string name) where TEnum : struct, Enum => _values.GetEnum<TEnum>(name);
  674: public InstrumentId GetInstrument(string name) => _values.GetInstrument(name);
  679: public List<VirtualTargetIntent> Targets { get; } = [];
  680: public void SubmitTarget(VirtualTargetIntent intent) => Targets.Add(intent);
  685: public void Alert(string message, AlertLevel level, string? dedupeKey = null)
  697: public ProbeMarketDataView(
  705: public IReadOnlySet<InstrumentId> Instruments { get; }
  706: public StrategyDataRequirement DataRequirement { get; }
  708: public void Add(
  721: public IReadOnlyList<OhlcvBar> RecentBars(InstrumentId instrument, BarSize size, int maxCount) =>
  724: public IReadOnlyList<Quote> RecentQuotes(InstrumentId instrument, int maxCount) =>
  727: public DepthSnapshot? LatestDepth(InstrumentId instrument) =>
  730: public IReadOnlyList<TradePrint> RecentTrades(InstrumentId instrument, int maxCount) =>
  757: public int OperationCount { get; private set; }
  758: public IReadOnlyDictionary<string, ProbeLayerEmission> EmittedLayers => _layerEmissions;
  759: public string Fingerprint => Convert.ToHexString(
  761: public RenderViewport Viewport => new(960, 640, 1);
  762: public RenderCursor Cursor => new(0, 0, false, false);
  763: public RenderColor Theme(RenderThemeColor token) => new(128, 128, 128);
  764: public IDisposable Layer(string layerId, string typeId)
  783: public void SetStyle(RenderStyle style) => Record(ProbeDrawingPrimitive.Style, $"style:{style}");
  784: public IDisposable Panel(string title, RenderPanelKind kind)
  789: public void AxisX(double minimum, double maximum, string? format = null) =>
  791: public void AxisY(double minimum, double maximum, string? format = null) =>
  793: public IDisposable Series(string name, RenderSeriesKind kind)
  799: public void Push(double x, double y) =>
  801: public void Line(double x1, double y1, double x2, double y2) =>
  803: public void Rect(double x, double y, double width, double height, bool filled = true) =>
  805: public void Text(double x, double y, string text) =>
  807: public void Marker(double x, double y, RenderMarkerShape shape) =>
  810: public bool HasRequiredPrimitives(string layerId, AuthoredChartLayerKindV1 kind)
  854: public sealed record ProbeLayerEmission(
  858: public enum ProbeDrawingPrimitive
  875: public void Dispose()
  883: public static NoopDisposable Instance { get; } = new();
  884: public void Dispose() { }
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Strategies/Authoring/RoslynStrategyCompiler.cs
```cs
   27: public sealed class RoslynStrategyCompiler : IStrategyCompiler
   59: public StrategyCompileResult Compile(StrategyScript script)
  156: public sealed class DaxAlgoAuthoredPlugin : DaxAlgo.Sdk.IStrategyPlugin
  158: public string Name => {{Literal(script.DisplayName)}};
  159: public string TargetSdkVersion => {{Literal(SdkInfo.Version)}};
  161: public void Register(DaxAlgo.Sdk.IPluginRegistrar registrar) =>
```
