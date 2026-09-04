using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class RoslynAuthoredUnitCompilerV1Tests
{
    [Fact]
    public void Compiles_exact_specification_bound_visualizer()
    {
        var specification = Visualizer();
        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, Source(specification)));

        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.NotNull(result.Unit);
        Assert.Equal(typeof(DaxAlgo.Sdk.IVisualizer), result.Unit!.RuntimeType.GetInterfaces().Single(
            type => type == typeof(DaxAlgo.Sdk.IVisualizer)));
        Assert.Equal(StrategyDataRequirement.Bars, result.Unit.DataRequirement);
        Assert.Equal(AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification),
            result.Unit.SpecificationHashSha256);
    }

    [Fact]
    public void Rejects_runtime_bound_to_different_specification_hash()
    {
        var specification = Visualizer();
        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, Source(specification, new string('0', 64))));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DAXU202");
    }

    [Fact]
    public void Rejects_runtime_data_requirement_that_differs_from_specification()
    {
        var specification = Visualizer();
        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, Source(specification, requirement: "StrategyDataRequirement.L1")));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DAXU204");
    }

    [Fact]
    public void Rejects_visualizer_source_that_implements_strategy_kernel()
    {
        var specification = Visualizer();
        var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        var source = $$"""
            public sealed class WrongKind : IStrategyKernel
            {
                public static string SpecificationHashSha256 => "{{hash}}";
                public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
                public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
                public Task OnStartAsync(IStrategyRuntimeContext context, CancellationToken ct) => Task.CompletedTask;
            }
            """;

        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, source));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DAXU200");
    }

    [Fact]
    public void Rejects_runtime_that_does_not_bind_reviewed_drawing_layers()
    {
        var specification = Visualizer();
        var source = Source(specification).Replace(
            "new[] { \"price.candles@1\" }",
            "new[] { \"indicator.ema@1\" }",
            StringComparison.Ordinal);

        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, source));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DAXU208");
    }

    [Fact]
    public void Rejects_runtime_that_opens_with_a_blank_first_frame()
    {
        var specification = Visualizer();
        var source = Source(specification).Replace(
            "surface.Text(8, 18, \"Waiting for bars…\");",
            "_ = surface.Viewport;",
            StringComparison.Ordinal);

        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, source));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DAXU210");
    }

    [Fact]
    public void Rejects_declared_bar_data_when_runtime_uses_the_default_no_op_callback()
    {
        var specification = Visualizer();
        var source = Source(specification).Replace(
            "public Task OnBarAsync(OhlcvBar bar, IVisualizerContext context, CancellationToken ct)\n    {\n        _lastClose = bar.Close;\n        _bars.Add(bar);\n        if (_bars.Count > 256) _bars.RemoveAt(0);\n        return Task.CompletedTask;\n    }\n    ",
            string.Empty,
            StringComparison.Ordinal);

        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, source));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DAXU211");
    }

    [Fact]
    public void Rejects_runtime_that_receives_bars_but_keeps_rendering_the_waiting_frame()
    {
        var specification = Visualizer();
        var source = Source(specification).Replace(
            "if (_bars.Count == 0)",
            "if (true)",
            StringComparison.Ordinal);

        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, source));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DAXU213");
    }

    [Fact]
    public void Rejects_manifest_only_candle_claim_without_emitted_layer_scope()
    {
        var specification = Visualizer();
        var source = Source(specification).Replace(
            "using (surface.Layer(\"candles\", \"price.candles@1\"))",
            "using (surface.Panel(\"price\", RenderPanelKind.Chart))",
            StringComparison.Ordinal);

        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, source));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DAXU214");
    }

    [Fact]
    public void Rejects_candle_layer_that_emits_only_text_instead_of_candle_primitives()
    {
        var specification = Visualizer();
        var source = Source(specification).Replace(
            "Candles.Draw(surface, _bars);",
            "surface.Text(8, 18, $\"Close {_lastClose!.Value:R}\");",
            StringComparison.Ordinal);

        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, source));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "DAXU215");
    }

    [Fact]
    public void Two_ema_layers_with_one_type_id_must_emit_both_reviewed_instances()
    {
        var specification = Visualizer() with
        {
            Drawing = new AuthoredChartCompositionV1(
                [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0)],
                [
                    new AuthoredChartLayerV1(
                        "ema-fast", "price", AuthoredChartLayerKindV1.IndicatorLine,
                        "indicator.ema@1", new Dictionary<string, string> { ["period"] = "9" }),
                    new AuthoredChartLayerV1(
                        "ema-slow", "price", AuthoredChartLayerKindV1.IndicatorLine,
                        "indicator.ema@1", new Dictionary<string, string> { ["period"] = "21" }),
                ]),
        };
        var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        var source = $$"""
            public sealed class IncompleteEmaChart : IVisualizer, IAuthoredDrawingManifest
            {
                private double? _close;
                public static string SpecificationHashSha256 => "{{hash}}";
                public IReadOnlyList<string> DrawingLayerTypeIds =>
                    new[] { "indicator.ema@1", "indicator.ema@1" };
                public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
                public StrategyDataRequirement DataRequirement => StrategyDataRequirement.Bars;
                public Task OnStartAsync(IVisualizerContext context, CancellationToken ct) => Task.CompletedTask;
                public Task OnBarAsync(OhlcvBar bar, IVisualizerContext context, CancellationToken ct)
                {
                    _close = bar.Close;
                    return Task.CompletedTask;
                }
                public void Draw(IRenderSurface surface)
                {
                    if (_close is null)
                    {
                        surface.Text(8, 18, "Waiting for bars");
                        return;
                    }
                    using (surface.Layer("ema-fast", "indicator.ema@1"))
                        surface.Line(0, _close.Value, 1, _close.Value);
                }
            }
            """;

        var result = new RoslynAuthoredUnitCompilerV1().Compile(
            specification,
            Script(specification, source));

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "DAXU214" && diagnostic.Message.Contains("ema-slow", StringComparison.Ordinal));
    }

    private static AuthoredUnitSpecificationV1 Visualizer() => new(
        AuthoredUnitSpecificationV1.CurrentSchemaVersion,
        "btc-candles",
        "BTC Candles",
        "Show BTC one-minute candles.",
        AuthoredUnitSourceKindV1.Text,
        AuthoredUnitKindV1.Visualizer,
        [new AuthoredInstrumentRequestV1(
            "primary", "BTC", new InstrumentId(42), AssetClass.Crypto, BrokerKind.Coinbase)],
        new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
        StrategyDataRequirement.Bars,
        [],
        new AuthoredChartCompositionV1(
            [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0)],
            [new AuthoredChartLayerV1(
                "candles", "price", AuthoredChartLayerKindV1.Candles, "price.candles@1",
                new Dictionary<string, string>())]),
        [],
        [],
        AuthoredUnitExecutionIntentV1.None);

    private static StrategyScript Script(AuthoredUnitSpecificationV1 specification, string source) =>
        new(specification.UnitId, specification.Name, [new StrategyFile("BitcoinCandles.cs", source)]);

    private static string Source(
        AuthoredUnitSpecificationV1 specification,
        string? hash = null,
        string requirement = "StrategyDataRequirement.Bars") => $$"""
        public sealed class BitcoinCandles : IVisualizer, IAuthoredDrawingManifest
        {
            private double? _lastClose;
            private readonly List<OhlcvBar> _bars = new();
            public static string SpecificationHashSha256 => "{{hash ?? AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification)}}";
            public IReadOnlyList<string> DrawingLayerTypeIds => new[] { "price.candles@1" };
            public StrategyParameterSchema Schema => StrategyParameterSchema.Empty;
            public StrategyDataRequirement DataRequirement => {{requirement}};
            public Task OnStartAsync(IVisualizerContext context, CancellationToken ct) => Task.CompletedTask;
            public Task OnBarAsync(OhlcvBar bar, IVisualizerContext context, CancellationToken ct)
            {
                _lastClose = bar.Close;
                _bars.Add(bar);
                if (_bars.Count > 256) _bars.RemoveAt(0);
                return Task.CompletedTask;
            }
            public void Draw(IRenderSurface surface)
            {
                if (_bars.Count == 0)
                {
                    surface.Text(8, 18, "Waiting for bars…");
                    return;
                }

                using (surface.Layer("candles", "price.candles@1"))
                    Candles.Draw(surface, _bars);
            }
        }
        """;
}
