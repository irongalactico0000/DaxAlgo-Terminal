using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Definition;
using TradingTerminal.Core.Strategies.Parameters;

namespace TradingTerminal.Core.Strategies.Generation;

/// <summary>The runnable product artifact Hyperion is being asked to author.</summary>
public enum AuthoredUnitKindV1
{
    Visualizer,
    Strategy,
}

/// <summary>How the user communicated the desired unit.</summary>
public enum AuthoredUnitSourceKindV1
{
    Text,
    ReferenceChart,
    TextAndReferenceChart,
}

/// <summary>
/// What "like this chart" means. Visual reproduction and historical market similarity are separate
/// jobs and must never be inferred from one another.
/// </summary>
public enum ChartReferenceSimilarityV1
{
    VisualStyle,
    Layout,
    ChartType,
    IndicatorComposition,
    Annotations,
    MarketPattern,
    RelatedInstruments,
}

public enum AuthoredChartPaneRoleV1
{
    Price,
    Volume,
    Indicator,
    OrderBook,
    TradeTape,
    Custom,
}

public enum AuthoredChartLayerKindV1
{
    Candles,
    PriceLine,
    Volume,
    IndicatorLine,
    Histogram,
    Scatter,
    OrderBook,
    Footprint,
    TradeTape,
    Annotation,
    Custom,
}

/// <summary>Only Paper target production is authorized by this repository.</summary>
public enum AuthoredUnitExecutionIntentV1
{
    None,
    PaperTargets,
}

/// <summary>
/// One immutable reference supplied by the user. The artifact itself remains host-owned; Core keeps
/// only its stable id, media type, and content hash so a later build cannot silently analyze a
/// different image.
/// </summary>
public sealed record AuthoredChartReferenceV1(
    string ReferenceId,
    string ContentHashSha256,
    string MediaType,
    IReadOnlyList<ChartReferenceSimilarityV1> Similarity,
    string? UserInstruction = null);

/// <summary>
/// The deterministic result of interpreting a reference chart. A style/layout reference resolves to
/// chart layers; a market-pattern or related-instrument reference also resolves to instrument
/// requests produced by the historical search stage.
/// </summary>
public sealed record AuthoredChartReferenceResolutionV1(
    string ReferenceId,
    IReadOnlyList<string> LayerIds,
    IReadOnlyList<string> InstrumentRequestIds,
    string Explanation);

/// <summary>
/// Vision-stage observations about one exact reference. This is evidence for specification
/// generation, not permission to launch: historical similarity still requires real search results,
/// and all inferred layers remain subject to authored-unit validation.
/// </summary>
public sealed record AuthoredChartReferenceInspectionV1(
    string ReferenceId,
    string ContentHashSha256,
    string Summary,
    string ChartType,
    string? VisibleInstrument,
    string? VisibleTimeframe,
    IReadOnlyList<string> Indicators,
    string? StyleDescription,
    string? MarketPatternQuery,
    ChartPatternFingerprintV1? PatternFingerprint = null);

/// <summary>
/// An instrument requested in natural language. <see cref="InstrumentId.None"/> is permitted while
/// interviewing but rejected by launch validation.
/// </summary>
public sealed record AuthoredInstrumentRequestV1(
    string RequestId,
    string UserText,
    InstrumentId InstrumentId,
    AssetClass? ExpectedAssetClass = null,
    BrokerKind? PreferredBroker = null);

/// <summary>Separates the user's timeframe wording from the resolved bar duration.</summary>
public sealed record AuthoredUnitTimeframeV1(string UserText, TimeSpan? BarSize);

/// <summary>
/// Portable parameter declaration. Values remain canonical strings here; the compiler lowers them
/// into the SDK's typed <see cref="StrategyParameterSchema"/> only after validation.
/// </summary>
public sealed record AuthoredUnitParameterV1(
    string Key,
    string DisplayName,
    ParameterKind Kind,
    string CanonicalDefault,
    string? CanonicalMinimum = null,
    string? CanonicalMaximum = null,
    IReadOnlyList<string>? Choices = null,
    string? Unit = null,
    string? Description = null);

public sealed record AuthoredChartPaneV1(
    string PaneId,
    AuthoredChartPaneRoleV1 Role,
    int Order,
    string? Title = null);

/// <summary>
/// One requested drawing layer. <paramref name="TypeId"/> is a stable implementation-neutral id,
/// for example <c>price.candles@1</c>, <c>indicator.ema@1</c>, or <c>market.depth_ladder@1</c>.
/// </summary>
public sealed record AuthoredChartLayerV1(
    string LayerId,
    string PaneId,
    AuthoredChartLayerKindV1 Kind,
    string TypeId,
    IReadOnlyDictionary<string, string> Parameters,
    string? SourceReferenceId = null);

public sealed record AuthoredChartCompositionV1(
    IReadOnlyList<AuthoredChartPaneV1> Panes,
    IReadOnlyList<AuthoredChartLayerV1> Layers);

/// <summary>
/// The versioned handoff from Hyperion intake to code generation, verification, installation, and
/// launch. It describes what must run; it contains no generated C# and grants no live-order authority.
/// </summary>
public sealed record AuthoredUnitSpecificationV1(
    string SchemaVersion,
    string UnitId,
    string Name,
    string RawRequest,
    AuthoredUnitSourceKindV1 SourceKind,
    AuthoredUnitKindV1 Kind,
    IReadOnlyList<AuthoredInstrumentRequestV1> Instruments,
    AuthoredUnitTimeframeV1 Timeframe,
    StrategyDataRequirement DataRequirement,
    IReadOnlyList<AuthoredUnitParameterV1> Parameters,
    AuthoredChartCompositionV1 Drawing,
    IReadOnlyList<AuthoredChartReferenceV1> References,
    IReadOnlyList<AuthoredChartReferenceResolutionV1> ReferenceResolutions,
    AuthoredUnitExecutionIntentV1 ExecutionIntent,
    StrategyClassificationBindingV1? StrategyClassification = null,
    ConfirmedStrategyIntentV1? ConfirmedStrategyIntent = null)
{
    public const string CurrentSchemaVersion = "authored-unit-spec/v1";
}

public sealed record AuthoredUnitSpecificationIssueV1(string Code, string Path, string Message);

/// <summary>
/// One host-authorized instrument the specification model may select. The model receives canonical
/// ids instead of inventing broker symbols; launch still validates the chosen broker capability.
/// </summary>
public sealed record AuthoredUnitInstrumentCandidateV1(
    InstrumentId InstrumentId,
    string CanonicalSymbol,
    AssetClass AssetClass,
    string Exchange,
    string Currency,
    IReadOnlyList<BrokerKind> AvailableBrokers);

/// <summary>
/// Exact evidence supplied to authored-unit specification generation. For a visualizer the confirmed
/// strategy intent is null. For a strategy it is mandatory and the resulting classification binding
/// must be byte-for-byte identical to the confirmed intent's binding.
/// When <paramref name="SelectedChartOverlayIds"/> is set, those ids must come from
/// <see cref="AuthoredChartChoiceCatalogV1"/> and the host freezes the matching drawing layers after
/// model output so chat-selected famous indicators cannot drift into unsupported type ids.
/// </summary>
public sealed record AuthoredUnitSpecificationGenerationRequestV1(
    string UnitId,
    string RawRequest,
    IReadOnlyList<AuthoredUnitInstrumentCandidateV1> AvailableInstruments,
    ConfirmedStrategyIntentV1? ConfirmedStrategyIntent = null,
    IReadOnlyList<AuthoredChartReferenceV1>? ChartReferences = null,
    IReadOnlyList<AuthoredChartReferenceInspectionV1>? ChartReferenceInspections = null,
    IReadOnlyList<ChartPatternSelectionV1>? ChartPatternSelections = null,
    ResearchExperimentEvidenceV1? ResearchExperiment = null,
    IReadOnlyList<string>? SelectedChartOverlayIds = null);

public sealed record AuthoredUnitSpecificationGenerationIssueV1(
    string Code,
    string Path,
    string Message);

public sealed record AuthoredUnitSpecificationGenerationResultV1(
    AuthoredUnitSpecificationV1? Specification,
    IReadOnlyList<AuthoredUnitSpecificationGenerationIssueV1> Issues,
    CodegenUsage Usage,
    string? RawResponse = null)
{
    public bool Success => Specification is not null && Issues.Count == 0;
}

/// <summary>
/// Turns reviewed user/reference evidence into the one versioned contract later source, install, and
/// launch receipts must bind. Implementations may use AI, but host-owned identities remain immutable.
/// </summary>
public interface IAuthoredUnitSpecificationGeneratorV1
{
    Task<AuthoredUnitSpecificationGenerationResultV1> GenerateAsync(
        IStrategyCodegenClient provider,
        AuthoredUnitSpecificationGenerationRequestV1 request,
        CancellationToken cancellationToken = default);
}

/// <summary>The one bounded decision made before choosing the visualizer or strategy workflow.</summary>
public sealed record AuthoredUnitIntentClassificationV1(
    AuthoredUnitKindV1 Kind,
    string Confidence,
    string Rationale,
    string? ClarificationQuestion = null);

public sealed record AuthoredUnitIntentClassificationResultV1(
    AuthoredUnitIntentClassificationV1? Classification,
    IReadOnlyList<AuthoredUnitSpecificationGenerationIssueV1> Issues,
    CodegenUsage Usage)
{
    public bool Success => Classification is not null &&
        string.IsNullOrWhiteSpace(Classification.ClarificationQuestion) &&
        Issues.Count == 0;
}

public interface IAuthoredUnitIntentClassifierV1
{
    Task<AuthoredUnitIntentClassificationResultV1> ClassifyAsync(
        IStrategyCodegenClient provider,
        string rawRequest,
        IReadOnlyList<AuthoredChartReferenceV1>? chartReferences = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The immutable input to source generation. The generator recomputes the canonical specification
/// hash; callers cannot substitute a different hash or source after review.
/// </summary>
public sealed record AuthoredUnitSourceGenerationRequestV1(
    AuthoredUnitSpecificationV1 Specification);

public sealed record AuthoredUnitSourceGenerationIssueV1(
    string Code,
    string Path,
    string Message);

/// <summary>
/// Generated source plus the exact specification identity it implements. This is not yet runnable:
/// the authored-unit compiler must independently verify the hash, interface, schema, data needs, and
/// policy before the assembly may be registered.
/// </summary>
public sealed record AuthoredUnitSourceGenerationResultV1(
    StrategyScript? Script,
    string? SpecificationHashSha256,
    IReadOnlyList<AuthoredUnitSourceGenerationIssueV1> Issues,
    CodegenUsage Usage,
    string? RawResponse = null)
{
    public bool Success => Script is not null &&
        !string.IsNullOrWhiteSpace(SpecificationHashSha256) &&
        Issues.Count == 0;
}

/// <summary>
/// Lowers one launch-valid authored-unit specification into source. It does not install or launch the
/// result and therefore grants neither market-data ownership nor execution authority.
/// </summary>
public interface IAuthoredUnitSourceGeneratorV1
{
    Task<AuthoredUnitSourceGenerationResultV1> GenerateAsync(
        IStrategyCodegenClient provider,
        AuthoredUnitSourceGenerationRequestV1 request,
        CancellationToken cancellationToken = default);
}

/// <summary>Pure structural and launch gates shared by AI intake, compiler admission, and the UI.</summary>
public static class AuthoredUnitSpecificationValidatorV1
{
    private const StrategyDataRequirement KnownDataRequirements =
        StrategyDataRequirement.L1 |
        StrategyDataRequirement.Bars |
        StrategyDataRequirement.Depth |
        StrategyDataRequirement.TradeTape;

    public static IReadOnlyList<AuthoredUnitSpecificationIssueV1> Validate(
        AuthoredUnitSpecificationV1? specification) =>
        ValidateCore(specification, forLaunch: false);

    public static IReadOnlyList<AuthoredUnitSpecificationIssueV1> ValidateForLaunch(
        AuthoredUnitSpecificationV1? specification) =>
        ValidateCore(specification, forLaunch: true);

    private static IReadOnlyList<AuthoredUnitSpecificationIssueV1> ValidateCore(
        AuthoredUnitSpecificationV1? specification,
        bool forLaunch)
    {
        var issues = new List<AuthoredUnitSpecificationIssueV1>();
        if (specification is null)
        {
            issues.Add(Issue("unit.required", "$", "An authored-unit specification is required."));
            return issues;
        }

        RequiredExact(specification.SchemaVersion, AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            "schemaVersion", "unit.schema.unsupported", issues);
        Required(specification.UnitId, "unitId", "unit.id.required", issues);
        Required(specification.Name, "name", "unit.name.required", issues);
        Required(specification.RawRequest, "rawRequest", "unit.request.required", issues);
        ValidEnum(specification.SourceKind, "sourceKind", issues);
        ValidEnum(specification.Kind, "kind", issues);
        ValidEnum(specification.ExecutionIntent, "executionIntent", issues);

        if (specification.Kind == AuthoredUnitKindV1.Visualizer)
        {
            if (specification.ExecutionIntent != AuthoredUnitExecutionIntentV1.None)
                issues.Add(Issue("unit.visualizer.execution_forbidden", "executionIntent",
                    "A visualizer cannot produce position or order targets."));
            if (specification.StrategyClassification is not null)
                issues.Add(Issue("unit.visualizer.classification_forbidden", "strategyClassification",
                    "A visualizer must not carry a strategy classification."));
            if (specification.ConfirmedStrategyIntent is not null)
                issues.Add(Issue("unit.visualizer.confirmed_intent_forbidden", "confirmedStrategyIntent",
                    "A visualizer must not carry position-changing strategy intent."));
        }
        else if (specification.Kind == AuthoredUnitKindV1.Strategy)
        {
            if (specification.ExecutionIntent != AuthoredUnitExecutionIntentV1.PaperTargets)
                issues.Add(Issue("unit.strategy.paper_required", "executionIntent",
                    "A runnable strategy must declare Paper target execution."));
            if (specification.StrategyClassification is null)
                issues.Add(Issue("unit.strategy.classification_required", "strategyClassification",
                    "A strategy must bind its confirmed StrategySpec classification."));
            if (specification.ConfirmedStrategyIntent is null)
            {
                issues.Add(Issue("unit.strategy.confirmed_intent_required", "confirmedStrategyIntent",
                    "A runnable strategy must carry the exact confirmed strategy intent it implements."));
            }
            else
            {
                ValidateConfirmedStrategyIntent(specification, issues);
            }
        }

        if (specification.DataRequirement == StrategyDataRequirement.None ||
            (specification.DataRequirement & ~KnownDataRequirements) != 0)
        {
            issues.Add(Issue("unit.data.invalid", "dataRequirement",
                "At least one known market-data requirement must be declared."));
        }

        ValidateInstruments(specification.Instruments, forLaunch, issues);
        ValidateTimeframe(specification, issues);
        ValidateParameters(specification.Parameters, issues);
        ValidateDrawing(specification, issues);
        ValidateReferences(specification, forLaunch, issues);

        return issues;
    }

    private static void ValidateConfirmedStrategyIntent(
        AuthoredUnitSpecificationV1 specification,
        ICollection<AuthoredUnitSpecificationIssueV1> issues)
    {
        var intent = specification.ConfirmedStrategyIntent!;
        if (!string.Equals(
                intent.SchemaVersion,
                ConfirmedStrategyIntentV1.CurrentSchemaVersion,
                StringComparison.Ordinal))
        {
            issues.Add(Issue("unit.strategy.confirmed_intent_schema_invalid", "confirmedStrategyIntent.schemaVersion",
                $"Confirmed strategy intent must use '{ConfirmedStrategyIntentV1.CurrentSchemaVersion}'."));
        }

        if (!Equals(specification.StrategyClassification, intent.Classification))
            issues.Add(Issue("unit.strategy.confirmed_intent_classification_mismatch", "confirmedStrategyIntent.classification",
                "The authored strategy classification must equal the confirmed intent classification."));

        if (intent.Requirements is null || intent.Requirements.Count == 0)
            issues.Add(Issue("unit.strategy.confirmed_intent_requirements_required", "confirmedStrategyIntent.requirements",
                "A runnable strategy needs at least one reviewed semantic requirement."));
        else if (intent.Requirements.Any(static requirement =>
                     requirement is null || requirement.Disposition is
                         StrategySemanticDispositionV1.Unresolved or StrategySemanticDispositionV1.Unsupported))
            issues.Add(Issue("unit.strategy.confirmed_intent_unresolved", "confirmedStrategyIntent.requirements",
                "Unresolved or unsupported strategy requirements cannot enter runnable source generation."));
    }

    private static void ValidateInstruments(
        IReadOnlyList<AuthoredInstrumentRequestV1>? instruments,
        bool forLaunch,
        ICollection<AuthoredUnitSpecificationIssueV1> issues)
    {
        if (instruments is null || instruments.Count == 0)
        {
            issues.Add(Issue("unit.instruments.required", "instruments",
                "At least one instrument request is required."));
            return;
        }

        Unique(instruments.Select(static item => item?.RequestId), "instruments", "instrument request", issues);
        for (var index = 0; index < instruments.Count; index++)
        {
            var item = instruments[index];
            var path = $"instruments[{index}]";
            if (item is null)
            {
                issues.Add(Issue("unit.instrument.required", path, "Instrument requests cannot be null."));
                continue;
            }
            Required(item.RequestId, $"{path}.requestId", "unit.instrument.id.required", issues);
            Required(item.UserText, $"{path}.userText", "unit.instrument.text.required", issues);
            if (forLaunch && item.InstrumentId.IsNone)
                issues.Add(Issue("unit.instrument.unresolved", $"{path}.instrumentId",
                    $"Instrument request '{item.RequestId}' must resolve before launch."));
            if (forLaunch && item.PreferredBroker is null)
                issues.Add(Issue("unit.instrument.broker_unresolved", $"{path}.preferredBroker",
                    $"Instrument request '{item.RequestId}' must select one broker feed before launch."));
            if (item.ExpectedAssetClass is { } assetClass && !Enum.IsDefined(assetClass))
                issues.Add(Issue("unit.enum.invalid", $"{path}.expectedAssetClass",
                    $"Undefined {nameof(AssetClass)} value '{assetClass}'."));
            if (item.PreferredBroker is { } broker && !Enum.IsDefined(broker))
                issues.Add(Issue("unit.enum.invalid", $"{path}.preferredBroker",
                    $"Undefined {nameof(BrokerKind)} value '{broker}'."));
        }
    }

    private static void ValidateTimeframe(
        AuthoredUnitSpecificationV1 specification,
        ICollection<AuthoredUnitSpecificationIssueV1> issues)
    {
        if (specification.Timeframe is null)
        {
            issues.Add(Issue("unit.timeframe.required", "timeframe", "A timeframe is required."));
            return;
        }
        Required(specification.Timeframe.UserText, "timeframe.userText", "unit.timeframe.text.required", issues);
        if (specification.Timeframe.BarSize is { } barSize && barSize <= TimeSpan.Zero)
            issues.Add(Issue("unit.timeframe.invalid", "timeframe.barSize", "Bar size must be positive."));
        if (specification.DataRequirement.HasFlag(StrategyDataRequirement.Bars) &&
            specification.Timeframe.BarSize is null)
        {
            issues.Add(Issue("unit.timeframe.bar_size_required", "timeframe.barSize",
                "A bar-consuming unit must resolve its bar size."));
        }
    }

    private static void ValidateParameters(
        IReadOnlyList<AuthoredUnitParameterV1>? parameters,
        ICollection<AuthoredUnitSpecificationIssueV1> issues)
    {
        if (parameters is null)
        {
            issues.Add(Issue("unit.parameters.required", "parameters",
                "Parameters must be present, even when empty."));
            return;
        }
        Unique(parameters.Select(static item => item?.Key), "parameters", "parameter", issues);
        for (var index = 0; index < parameters.Count; index++)
        {
            var item = parameters[index];
            var path = $"parameters[{index}]";
            if (item is null)
            {
                issues.Add(Issue("unit.parameter.required", path, "Parameters cannot be null."));
                continue;
            }
            Required(item.Key, $"{path}.key", "unit.parameter.key.required", issues);
            Required(item.DisplayName, $"{path}.displayName", "unit.parameter.name.required", issues);
            Required(item.CanonicalDefault, $"{path}.canonicalDefault", "unit.parameter.default.required", issues);
            ValidEnum(item.Kind, $"{path}.kind", issues);
            if (item.Kind == ParameterKind.Choice && (item.Choices is null || item.Choices.Count == 0))
                issues.Add(Issue("unit.parameter.choices.required", $"{path}.choices",
                    "Choice parameters require at least one allowed value."));
        }
    }

    private static void ValidateDrawing(
        AuthoredUnitSpecificationV1 specification,
        ICollection<AuthoredUnitSpecificationIssueV1> issues)
    {
        if (specification.Drawing is null)
        {
            issues.Add(Issue("unit.drawing.required", "drawing", "A drawing composition is required."));
            return;
        }
        var panes = specification.Drawing.Panes;
        var layers = specification.Drawing.Layers;
        if (panes is null || panes.Count == 0)
            issues.Add(Issue("unit.drawing.panes.required", "drawing.panes", "At least one chart pane is required."));
        if (layers is null || layers.Count == 0)
            issues.Add(Issue("unit.drawing.layers.required", "drawing.layers", "At least one chart layer is required."));
        if (panes is null || layers is null) return;

        Unique(panes.Select(static pane => pane?.PaneId), "drawing.panes", "pane", issues);
        Unique(layers.Select(static layer => layer?.LayerId), "drawing.layers", "layer", issues);
        var paneIds = panes.Where(static pane => pane is not null)
            .Select(static pane => pane.PaneId).ToHashSet(StringComparer.Ordinal);
        for (var index = 0; index < panes.Count; index++)
        {
            var pane = panes[index];
            var path = $"drawing.panes[{index}]";
            if (pane is null)
            {
                issues.Add(Issue("unit.drawing.pane.required", path, "Chart panes cannot be null."));
                continue;
            }
            Required(pane.PaneId, $"{path}.paneId", "unit.drawing.pane.id.required", issues);
            ValidEnum(pane.Role, $"{path}.role", issues);
            if (pane.Order < 0)
                issues.Add(Issue("unit.drawing.pane.order.invalid", $"{path}.order",
                    "Pane order cannot be negative."));
        }

        for (var index = 0; index < layers.Count; index++)
        {
            var layer = layers[index];
            var path = $"drawing.layers[{index}]";
            if (layer is null)
            {
                issues.Add(Issue("unit.drawing.layer.required", path, "Chart layers cannot be null."));
                continue;
            }
            Required(layer.LayerId, $"{path}.layerId", "unit.drawing.layer.id.required", issues);
            Required(layer.PaneId, $"{path}.paneId", "unit.drawing.layer.pane.required", issues);
            Required(layer.TypeId, $"{path}.typeId", "unit.drawing.layer.type.required", issues);
            ValidEnum(layer.Kind, $"{path}.kind", issues);
            if (!paneIds.Contains(layer.PaneId))
                issues.Add(Issue("unit.drawing.layer.pane_unknown", $"{path}.paneId",
                    $"Layer '{layer.LayerId}' references unknown pane '{layer.PaneId}'."));
            if (layer.Parameters is null)
                issues.Add(Issue("unit.drawing.layer.parameters.required", $"{path}.parameters",
                    "Layer parameters must be present, even when empty."));

            var required = RequiredData(layer.Kind);
            if (required != StrategyDataRequirement.None && !specification.DataRequirement.HasFlag(required))
                issues.Add(Issue("unit.drawing.data_missing", path,
                    $"Layer '{layer.LayerId}' requires market data '{required}'."));
        }
    }

    private static void ValidateReferences(
        AuthoredUnitSpecificationV1 specification,
        bool forLaunch,
        ICollection<AuthoredUnitSpecificationIssueV1> issues)
    {
        var references = specification.References;
        var resolutions = specification.ReferenceResolutions;
        if (references is null || resolutions is null)
        {
            issues.Add(Issue("unit.references.required", "references",
                "References and resolutions must be present, even when empty."));
            return;
        }

        var mustHaveReference = specification.SourceKind is
            AuthoredUnitSourceKindV1.ReferenceChart or AuthoredUnitSourceKindV1.TextAndReferenceChart;
        if (mustHaveReference && references.Count == 0)
            issues.Add(Issue("unit.reference.required", "references",
                "The selected source kind requires at least one reference chart."));
        if (specification.SourceKind == AuthoredUnitSourceKindV1.Text && references.Count > 0)
            issues.Add(Issue("unit.reference.source_mismatch", "references",
                "A text-only request cannot carry reference charts."));

        Unique(references.Select(static reference => reference?.ReferenceId), "references", "reference", issues);
        Unique(resolutions.Select(static resolution => resolution?.ReferenceId), "referenceResolutions", "resolution", issues);
        var layers = specification.Drawing?.Layers?.Where(static layer => layer is not null)
            .Select(static layer => layer.LayerId).ToHashSet(StringComparer.Ordinal) ?? [];
        var layerById = specification.Drawing?.Layers?.Where(static layer => layer is not null)
            .GroupBy(static layer => layer.LayerId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal)
            ?? new Dictionary<string, AuthoredChartLayerV1>(StringComparer.Ordinal);
        var instruments = specification.Instruments?.Where(static instrument => instrument is not null)
            .Select(static instrument => instrument.RequestId).ToHashSet(StringComparer.Ordinal) ?? [];
        var resolutionById = resolutions.Where(static resolution => resolution is not null)
            .GroupBy(static resolution => resolution.ReferenceId, StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.First(), StringComparer.Ordinal);

        for (var index = 0; index < references.Count; index++)
        {
            var reference = references[index];
            var path = $"references[{index}]";
            if (reference is null)
            {
                issues.Add(Issue("unit.reference.null", path, "References cannot be null."));
                continue;
            }
            Required(reference.ReferenceId, $"{path}.referenceId", "unit.reference.id.required", issues);
            if (!IsLowerSha256(reference.ContentHashSha256))
                issues.Add(Issue("unit.reference.hash.invalid", $"{path}.contentHashSha256",
                    "Reference content hash must be a lowercase SHA-256 value."));
            if (!IsSupportedChartMediaType(reference.MediaType))
                issues.Add(Issue("unit.reference.media_type.invalid", $"{path}.mediaType",
                    "Reference charts must be PNG, JPEG, WebP, PDF, or DaxAlgo chart JSON."));
            if (reference.Similarity is null || reference.Similarity.Count == 0)
                issues.Add(Issue("unit.reference.similarity.required", $"{path}.similarity",
                    "Specify whether similarity means style, layout, indicators, or market behavior."));
            else
            {
                foreach (var similarity in reference.Similarity)
                    ValidEnum(similarity, $"{path}.similarity", issues);
                if (reference.Similarity.Distinct().Count() != reference.Similarity.Count)
                    issues.Add(Issue("unit.reference.similarity.duplicate", $"{path}.similarity",
                        "Reference similarity aspects cannot be duplicated."));
            }

            if (!resolutionById.TryGetValue(reference.ReferenceId, out var resolution))
            {
                if (forLaunch)
                    issues.Add(Issue("unit.reference.unresolved", $"referenceResolutions[{reference.ReferenceId}]",
                        $"Reference '{reference.ReferenceId}' must be interpreted before launch."));
                continue;
            }

            Required(resolution.Explanation, $"referenceResolutions[{reference.ReferenceId}].explanation",
                "unit.reference.resolution.explanation.required", issues);
            foreach (var layerId in resolution.LayerIds ?? [])
            {
                if (!layers.Contains(layerId))
                    issues.Add(Issue("unit.reference.resolution.layer_unknown",
                        $"referenceResolutions[{reference.ReferenceId}].layerIds",
                        $"Reference resolution names unknown layer '{layerId}'."));
                else if (layerById.TryGetValue(layerId, out var layer) &&
                         !string.Equals(layer.SourceReferenceId, reference.ReferenceId, StringComparison.Ordinal))
                    issues.Add(Issue("unit.reference.resolution.layer_detached",
                        $"referenceResolutions[{reference.ReferenceId}].layerIds",
                        $"Layer '{layerId}' is not bound to reference '{reference.ReferenceId}'."));
            }
            foreach (var instrumentId in resolution.InstrumentRequestIds ?? [])
                if (!instruments.Contains(instrumentId))
                    issues.Add(Issue("unit.reference.resolution.instrument_unknown",
                        $"referenceResolutions[{reference.ReferenceId}].instrumentRequestIds",
                        $"Reference resolution names unknown instrument request '{instrumentId}'."));

            var searchesMarket = reference.Similarity?.Any(static aspect => aspect is
                ChartReferenceSimilarityV1.MarketPattern or ChartReferenceSimilarityV1.RelatedInstruments) == true;
            if (forLaunch && searchesMarket && (resolution.InstrumentRequestIds?.Count ?? 0) == 0)
                issues.Add(Issue("unit.reference.search_result.required",
                    $"referenceResolutions[{reference.ReferenceId}].instrumentRequestIds",
                    "Market-pattern and related-instrument requests require resolved search results before launch."));
        }

        foreach (var resolution in resolutions.Where(static resolution => resolution is not null))
            if (!references.Any(reference => reference is not null &&
                    string.Equals(reference.ReferenceId, resolution.ReferenceId, StringComparison.Ordinal)))
                issues.Add(Issue("unit.reference.resolution.orphan", "referenceResolutions",
                    $"Resolution '{resolution.ReferenceId}' has no matching reference."));

        foreach (var layer in layerById.Values.Where(static layer => layer.SourceReferenceId is not null))
            if (!references.Any(reference => reference is not null &&
                    string.Equals(reference.ReferenceId, layer.SourceReferenceId, StringComparison.Ordinal)))
                issues.Add(Issue("unit.drawing.layer.reference_unknown",
                    $"drawing.layers[{layer.LayerId}].sourceReferenceId",
                    $"Layer '{layer.LayerId}' references unknown chart evidence '{layer.SourceReferenceId}'."));
    }

    private static StrategyDataRequirement RequiredData(AuthoredChartLayerKindV1 kind) => kind switch
    {
        AuthoredChartLayerKindV1.Candles or
        AuthoredChartLayerKindV1.PriceLine or
        AuthoredChartLayerKindV1.Volume or
        AuthoredChartLayerKindV1.IndicatorLine or
        AuthoredChartLayerKindV1.Histogram or
        AuthoredChartLayerKindV1.Scatter or
        AuthoredChartLayerKindV1.Annotation => StrategyDataRequirement.Bars,
        AuthoredChartLayerKindV1.OrderBook => StrategyDataRequirement.Depth,
        AuthoredChartLayerKindV1.Footprint or
        AuthoredChartLayerKindV1.TradeTape => StrategyDataRequirement.TradeTape,
        _ => StrategyDataRequirement.None,
    };

    private static bool IsLowerSha256(string? value) =>
        value is { Length: 64 } && value.All(static character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsSupportedChartMediaType(string? value) => value is
        "image/png" or "image/jpeg" or "image/webp" or "application/pdf" or
        "application/vnd.daxalgo.chart+json";

    private static void Unique(
        IEnumerable<string?> values,
        string path,
        string label,
        ICollection<AuthoredUnitSpecificationIssueV1> issues)
    {
        var duplicate = values.Where(static value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(static value => value!, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicate is not null)
            issues.Add(Issue($"unit.{label.Replace(' ', '_')}.duplicate", path,
                $"The {label} id '{duplicate.Key}' is duplicated."));
    }

    private static void Required(
        string? value,
        string path,
        string code,
        ICollection<AuthoredUnitSpecificationIssueV1> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
            issues.Add(Issue(code, path, $"{path} is required."));
    }

    private static void RequiredExact(
        string? value,
        string expected,
        string path,
        string code,
        ICollection<AuthoredUnitSpecificationIssueV1> issues)
    {
        if (!string.Equals(value, expected, StringComparison.Ordinal))
            issues.Add(Issue(code, path, $"{path} must be '{expected}'."));
    }

    private static void ValidEnum<T>(
        T value,
        string path,
        ICollection<AuthoredUnitSpecificationIssueV1> issues)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(value))
            issues.Add(Issue("unit.enum.invalid", path,
                $"Undefined {typeof(T).Name} value '{value}'."));
    }

    private static AuthoredUnitSpecificationIssueV1 Issue(string code, string path, string message) =>
        new(code, path, message);
}

public static class AuthoredUnitSpecificationCanonicalJsonV1
{
    public static string Serialize(AuthoredUnitSpecificationV1 specification) =>
        ExecutableStrategyDefinitionCanonicalJson.Serialize(specification);

    public static AuthoredUnitSpecificationV1 Deserialize(string json) =>
        ExecutableStrategyDefinitionCanonicalJson.Deserialize<AuthoredUnitSpecificationV1>(json);

    public static string Hash(AuthoredUnitSpecificationV1 specification) =>
        ExecutableStrategyDefinitionCanonicalJson.Hash(specification);

    public static string Canonicalize(string json) =>
        ExecutableStrategyDefinitionCanonicalJson.Canonicalize(json);
}
