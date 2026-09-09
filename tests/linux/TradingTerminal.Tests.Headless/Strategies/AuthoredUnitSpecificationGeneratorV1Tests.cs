using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class AuthoredUnitSpecificationGeneratorV1Tests
{
    [Fact]
    public async Task Host_selected_overlays_overwrite_model_drawing_with_catalog_layers()
    {
        var aapl = Candidate(7, "AAPL", AssetClass.Equity, BrokerKind.InteractiveBrokers);
        var modelDrawing = Visualizer(
            "aapl-rsi",
            "Show AAPL with RSI.",
            aapl,
            [CandleLayer()]);
        var provider = new FixedProvider(modelDrawing);

        var result = await new AuthoredUnitSpecificationGeneratorV1().GenerateAsync(
            provider,
            new AuthoredUnitSpecificationGenerationRequestV1(
                modelDrawing.UnitId,
                modelDrawing.RawRequest,
                [aapl],
                SelectedChartOverlayIds: ["rsi-14"]));

        Assert.True(result.Success);
        Assert.Contains(
            result.Specification!.Drawing.Layers,
            static layer => layer.TypeId == "indicator.rsi@1" && layer.PaneId == "rsi");
        Assert.Contains(
            result.Specification.Drawing.Panes,
            static pane => pane.PaneId == "rsi" && pane.Role == AuthoredChartPaneRoleV1.Indicator);
    }

    [Fact]
    public async Task Plain_candle_request_becomes_a_launchable_visualizer()
    {
        var btc = Candidate(42, "BTC-USD", AssetClass.Crypto, BrokerKind.Coinbase);
        var specification = Visualizer(
            "btc-candles",
            "Show BTC one-minute candles.",
            btc,
            [CandleLayer()]);
        var provider = new FixedProvider(specification);

        var result = await new AuthoredUnitSpecificationGeneratorV1().GenerateAsync(
            provider,
            new AuthoredUnitSpecificationGenerationRequestV1(
                specification.UnitId,
                specification.RawRequest,
                [btc]));

        Assert.True(result.Success);
        Assert.Equal(AuthoredUnitKindV1.Visualizer, result.Specification!.Kind);
        Assert.Equal(StrategyDataRequirement.Bars, result.Specification.DataRequirement);
        Assert.Contains("kind \"visualizer\"", provider.LastRequest!.SystemContext, StringComparison.Ordinal);
    }

    [Fact]
    public async Task String_null_intent_is_treated_as_json_null_for_a_visualizer()
    {
        var spx = Candidate(77, "SPX", AssetClass.Index, BrokerKind.InteractiveBrokers);
        var specification = Visualizer(
            "spx-event-chart",
            "Show historical S&P 500 charts that rose at least 5% the next day.",
            spx,
            [CandleLayer()]);
        var json = AuthoredUnitSpecificationCanonicalJsonV1.Serialize(specification)
            .Replace("\"confirmedStrategyIntent\":null", "\"confirmedStrategyIntent\":\"null\"", StringComparison.Ordinal);

        var result = await new AuthoredUnitSpecificationGeneratorV1().GenerateAsync(
            new RawProvider(json),
            new AuthoredUnitSpecificationGenerationRequestV1(
                specification.UnitId,
                specification.RawRequest,
                [spx]));

        Assert.True(result.Success);
        Assert.Null(result.Specification!.ConfirmedStrategyIntent);
    }

    [Fact]
    public async Task Reference_appearance_and_indicators_are_bound_to_exact_hash_and_layers()
    {
        var btc = Candidate(42, "BTC-USD", AssetClass.Crypto, BrokerKind.Coinbase);
        var reference = new AuthoredChartReferenceV1(
            "chart-1",
            new string('a', 64),
            "image/png",
            [ChartReferenceSimilarityV1.VisualStyle, ChartReferenceSimilarityV1.IndicatorComposition]);
        var layers = new[]
        {
            CandleLayer(reference.ReferenceId),
            new AuthoredChartLayerV1(
                "ema-9", "price", AuthoredChartLayerKindV1.IndicatorLine, "indicator.ema@1",
                new Dictionary<string, string> { ["period"] = "9" }, reference.ReferenceId),
        };
        var specification = Visualizer(
            "btc-like-reference",
            "Show BTC candles with indicators and styling like this chart.",
            btc,
            layers) with
        {
            SourceKind = AuthoredUnitSourceKindV1.TextAndReferenceChart,
            References = [reference],
            ReferenceResolutions =
            [
                new AuthoredChartReferenceResolutionV1(
                    reference.ReferenceId,
                    ["candles", "ema-9"],
                    [],
                    "Reproduces the inspected candle styling and EMA overlay."),
            ],
        };

        var result = await new AuthoredUnitSpecificationGeneratorV1().GenerateAsync(
            new FixedProvider(specification),
            new AuthoredUnitSpecificationGenerationRequestV1(
                specification.UnitId,
                specification.RawRequest,
                [btc],
                ChartReferences: [reference],
                ChartReferenceInspections:
                [
                    new AuthoredChartReferenceInspectionV1(
                        reference.ReferenceId,
                        reference.ContentHashSha256,
                        "Candles with an EMA overlay.",
                        "candlestick",
                        "BTC-USD",
                        "1m",
                        ["EMA 9"],
                        "Dark background" ,
                        null),
                ]));

        Assert.True(result.Success);
        Assert.All(result.Specification!.Drawing.Layers, layer =>
            Assert.Equal(reference.ReferenceId, layer.SourceReferenceId));
    }

    [Fact]
    public async Task Similar_index_chart_uses_only_the_explicit_real_history_selection()
    {
        var spx = Candidate(77, "SPX", AssetClass.Index, BrokerKind.InteractiveBrokers);
        var reference = new AuthoredChartReferenceV1(
            "pattern-1",
            new string('b', 64),
            "image/jpeg",
            [ChartReferenceSimilarityV1.MarketPattern, ChartReferenceSimilarityV1.RelatedInstruments]);
        var match = new ChartPatternMatchV1(
            spx.InstrumentId,
            spx.CanonicalSymbol,
            spx.AssetClass,
            spx.Exchange,
            BarSize.OneHour,
            BrokerKind.InteractiveBrokers,
            120,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc),
            91.2,
            0.91,
            0.84,
            0.95,
            0.90);
        var selection = new ChartPatternSelectionV1(
            reference.ReferenceId,
            reference.ContentHashSha256,
            match);
        var specification = Visualizer(
            "similar-index",
            "Show an index whose historical chart is similar to this one.",
            spx,
            [CandleLayer(reference.ReferenceId)]) with
        {
            SourceKind = AuthoredUnitSourceKindV1.TextAndReferenceChart,
            Instruments =
            [
                new AuthoredInstrumentRequestV1(
                    "selected-index",
                    "explicit selected historical match",
                    spx.InstrumentId,
                    AssetClass.Index,
                    BrokerKind.InteractiveBrokers),
            ],
            Timeframe = new AuthoredUnitTimeframeV1("1 hour", TimeSpan.FromHours(1)),
            References = [reference],
            ReferenceResolutions =
            [
                new AuthoredChartReferenceResolutionV1(
                    reference.ReferenceId,
                    ["candles"],
                    ["selected-index"],
                    "Uses the user's explicit SPX match selected from stored one-hour history."),
            ],
        };

        var result = await new AuthoredUnitSpecificationGeneratorV1().GenerateAsync(
            new FixedProvider(specification),
            new AuthoredUnitSpecificationGenerationRequestV1(
                specification.UnitId,
                specification.RawRequest,
                [spx],
                ChartReferences: [reference],
                ChartReferenceInspections:
                [
                    new AuthoredChartReferenceInspectionV1(
                        reference.ReferenceId,
                        reference.ContentHashSha256,
                        "A rising index-like price path.",
                        "line",
                        null,
                        "1h",
                        [],
                        null,
                        "Find similar historical paths.",
                        new ChartPatternFingerprintV1([0, 0.2, 0.1, 0.4, 0.5, 0.45, 0.8, 1.0])),
                ],
                ChartPatternSelections: [selection]));

        Assert.True(result.Success);
        var instrument = Assert.Single(result.Specification!.Instruments);
        Assert.Equal(spx.InstrumentId, instrument.InstrumentId);
        Assert.Equal(BrokerKind.InteractiveBrokers, instrument.PreferredBroker);
    }

    [Fact]
    public async Task Provider_cannot_invent_an_instrument_or_detach_a_reference_layer()
    {
        var btc = Candidate(42, "BTC-USD", AssetClass.Crypto, BrokerKind.Coinbase);
        var reference = new AuthoredChartReferenceV1(
            "chart-1", new string('c', 64), "image/png", [ChartReferenceSimilarityV1.VisualStyle]);
        var invalid = Visualizer(
            "invalid",
            "Show this chart.",
            btc,
            [CandleLayer()]) with
        {
            SourceKind = AuthoredUnitSourceKindV1.TextAndReferenceChart,
            Instruments = [new AuthoredInstrumentRequestV1("invented", "invented", new InstrumentId(999))],
            References = [reference],
            ReferenceResolutions =
            [
                new AuthoredChartReferenceResolutionV1("chart-1", ["candles"], [], "Copied style."),
            ],
        };

        var result = await new AuthoredUnitSpecificationGeneratorV1().GenerateAsync(
            new FixedProvider(invalid),
            new AuthoredUnitSpecificationGenerationRequestV1(
                invalid.UnitId,
                invalid.RawRequest,
                [btc],
                ChartReferences: [reference],
                ChartReferenceInspections:
                [
                    new AuthoredChartReferenceInspectionV1(
                        reference.ReferenceId,
                        reference.ContentHashSha256,
                        "Dark candle chart.",
                        "candlestick",
                        "BTC-USD",
                        "1m",
                        [],
                        "Dark background",
                        null),
                ]));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "UNIT_INSTRUMENT_NOT_AUTHORIZED");
        Assert.Contains(result.Issues, issue => issue.Code == "unit.reference.resolution.layer_detached");
    }

    [Fact]
    public async Task Provider_cannot_bind_an_order_book_to_a_broker_without_live_depth()
    {
        var stock = Candidate(91, "SPY", AssetClass.Equity, BrokerKind.Alpaca);
        var invalid = Visualizer(
            "alpaca-book",
            "Show the live SPY order book.",
            stock,
            [new AuthoredChartLayerV1(
                "book", "price", AuthoredChartLayerKindV1.OrderBook, "market.depth_ladder@1",
                new Dictionary<string, string>())]) with
        {
            Timeframe = new AuthoredUnitTimeframeV1("live", null),
            DataRequirement = StrategyDataRequirement.Depth,
        };

        var result = await new AuthoredUnitSpecificationGeneratorV1().GenerateAsync(
            new FixedProvider(invalid),
            new AuthoredUnitSpecificationGenerationRequestV1(
                invalid.UnitId,
                invalid.RawRequest,
                [stock]));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "UNIT_BROKER_DATA_UNSUPPORTED");
    }

    [Fact]
    public async Task Runnable_strategy_preserves_the_complete_confirmed_intent()
    {
        var btc = Candidate(42, "BTC-USD", AssetClass.Crypto, BrokerKind.Coinbase);
        var classification = new StrategyClassificationBindingV1("ema-cross", new string('d', 64));
        var intent = ConfirmedIntent(classification);
        var specification = Strategy("btc-ema-paper", "Trade the reviewed BTC EMA crossover.", btc, intent);
        var provider = new FixedProvider(specification);

        var result = await new AuthoredUnitSpecificationGeneratorV1().GenerateAsync(
            provider,
            new AuthoredUnitSpecificationGenerationRequestV1(
                specification.UnitId,
                specification.RawRequest,
                [btc],
                intent));

        Assert.True(result.Success);
        Assert.Equal(AuthoredUnitKindV1.Strategy, result.Specification!.Kind);
        Assert.Equal(
            StrategyIntentCanonicalJsonV1.Serialize(intent),
            StrategyIntentCanonicalJsonV1.Serialize(result.Specification.ConfirmedStrategyIntent!));
        Assert.Contains("confirmedStrategyIntent", provider.LastRequest!.Messages[0].Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_cannot_change_a_reviewed_strategy_requirement()
    {
        var btc = Candidate(42, "BTC-USD", AssetClass.Crypto, BrokerKind.Coinbase);
        var classification = new StrategyClassificationBindingV1("ema-cross", new string('d', 64));
        var intent = ConfirmedIntent(classification);
        var changedIntent = intent with
        {
            Requirements =
            [
                intent.Requirements[0] with
                {
                    Description = "Use a different entry rule that the user did not review.",
                },
            ],
        };
        var changedSpecification = Strategy(
            "btc-ema-paper",
            "Trade the reviewed BTC EMA crossover.",
            btc,
            changedIntent);

        var result = await new AuthoredUnitSpecificationGeneratorV1().GenerateAsync(
            new FixedProvider(changedSpecification),
            new AuthoredUnitSpecificationGenerationRequestV1(
                changedSpecification.UnitId,
                changedSpecification.RawRequest,
                [btc],
                intent));

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.Code == "UNIT_CONFIRMED_STRATEGY_INTENT_CHANGED");
    }

    private static AuthoredUnitInstrumentCandidateV1 Candidate(
        int id,
        string symbol,
        AssetClass assetClass,
        BrokerKind broker) =>
        new(new InstrumentId(id), symbol, assetClass, "TEST", "USD", [broker]);

    private static AuthoredUnitSpecificationV1 Visualizer(
        string id,
        string request,
        AuthoredUnitInstrumentCandidateV1 instrument,
        IReadOnlyList<AuthoredChartLayerV1> layers) =>
        StrategyInteractionBindingsFactoryV1.UpgradeTrustedSpecification(new(
            AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            id,
            id,
            request,
            AuthoredUnitSourceKindV1.Text,
            AuthoredUnitKindV1.Visualizer,
            [new AuthoredInstrumentRequestV1("primary", instrument.CanonicalSymbol, instrument.InstrumentId,
                instrument.AssetClass, instrument.AvailableBrokers[0])],
            new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
            StrategyDataRequirement.Bars,
            [],
            new AuthoredChartCompositionV1(
                [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0, "Price")],
                layers),
            [],
            [],
            AuthoredUnitExecutionIntentV1.None));

    private static AuthoredUnitSpecificationV1 Strategy(
        string id,
        string request,
        AuthoredUnitInstrumentCandidateV1 instrument,
        ConfirmedStrategyIntentV1 intent) =>
        StrategyInteractionBindingsFactoryV1.UpgradeTrustedSpecification(new(
            AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            id,
            id,
            request,
            AuthoredUnitSourceKindV1.Text,
            AuthoredUnitKindV1.Strategy,
            [new AuthoredInstrumentRequestV1(
                "primary",
                instrument.CanonicalSymbol,
                instrument.InstrumentId,
                instrument.AssetClass,
                instrument.AvailableBrokers[0])],
            new AuthoredUnitTimeframeV1("1 minute", TimeSpan.FromMinutes(1)),
            StrategyDataRequirement.Bars,
            [],
            new AuthoredChartCompositionV1(
                [new AuthoredChartPaneV1("price", AuthoredChartPaneRoleV1.Price, 0, "Price")],
                [CandleLayer()]),
            [],
            [],
            AuthoredUnitExecutionIntentV1.PaperTargets,
            intent.Classification,
            intent));

    private static ConfirmedStrategyIntentV1 ConfirmedIntent(
        StrategyClassificationBindingV1 classification) => new(
        ConfirmedStrategyIntentV1.CurrentSchemaVersion,
        "intent-1",
        "candidate-1",
        1,
        new string('1', 64),
        new string('2', 64),
        classification,
        new StrategyIntentModelV1(StrategyIntentKindV1.PositionTarget),
        "strategy-requirements/v1",
        [
            new StrategySemanticRequirementV1(
                "ema-cross-target",
                StrategySemanticStageV1.DecideIntent,
                StrategySemanticDispositionV1.Applicable,
                "Set the BTC position target when the reviewed EMA crossover occurs.",
                true,
                new StrategyRequirementProvenanceV1(
                    ["candidate-statement-1"],
                    ["research-evidence-1"],
                    "The user reviewed this target rule.")),
        ],
        new string('3', 64));

    private static AuthoredChartLayerV1 CandleLayer(string? referenceId = null) =>
        new("candles", "price", AuthoredChartLayerKindV1.Candles, "price.candles@1",
            new Dictionary<string, string>(), referenceId);

    private sealed class FixedProvider(AuthoredUnitSpecificationV1 specification) : IStrategyCodegenClient
    {
        public string ProviderId => "fixed-authored-unit";
        public string DisplayName => "Fixed authored unit";
        public bool IsAvailable => true;
        public StrategyCodegenRequest? LastRequest { get; private set; }

        public Task<StrategyCodegenResponse> GenerateAsync(
            StrategyCodegenRequest request,
            CancellationToken ct = default)
        {
            LastRequest = request;
            var json = AuthoredUnitSpecificationCanonicalJsonV1.Serialize(specification);
            return Task.FromResult(new StrategyCodegenResponse(
                true,
                null,
                json,
                null,
                [],
                new CodegenUsage(10, 20)));
        }
    }

    private sealed class RawProvider(string json) : IStrategyCodegenClient
    {
        public string ProviderId => "raw-authored-unit";
        public string DisplayName => "Raw authored unit";
        public bool IsAvailable => true;

        public Task<StrategyCodegenResponse> GenerateAsync(
            StrategyCodegenRequest request,
            CancellationToken ct = default) =>
            Task.FromResult(StrategyCodegenResponse.Reply(json));
    }
}
