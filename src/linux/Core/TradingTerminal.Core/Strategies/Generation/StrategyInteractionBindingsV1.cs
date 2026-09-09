using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Definition;

namespace TradingTerminal.Core.Strategies.Generation;

/// <summary>Maps one stable parameter identity to the runtime/schema key that carries its value.</summary>
public sealed record StrategyParameterBindingV1(
    string ParameterId,
    string ParameterKey);

/// <summary>
/// Identifies one deterministic computation independently from any layer that happens to display it.
/// </summary>
public sealed record StrategyFeatureBindingV1(
    string FeatureId,
    string TypeId,
    StrategyDataRequirement RequiredData,
    IReadOnlyList<string> ParameterIds,
    string DisplayName);

/// <summary>
/// Projects computed features onto a drawing layer. Layer visibility is deliberately absent: hiding
/// a layer is appearance state and cannot remove a feature or disable a strategy rule.
/// </summary>
public sealed record StrategyLayerBindingV1(
    string LayerId,
    IReadOnlyList<string> FeatureIds,
    IReadOnlyDictionary<string, string> ParameterBindings);

/// <summary>Connects one reviewed decision rule to its exact features, parameters, instruments, and intent evidence.</summary>
public sealed record StrategyRuleBindingV1(
    string RuleId,
    IReadOnlyList<string> FeatureIds,
    IReadOnlyList<string> ParameterIds,
    IReadOnlyList<string> InstrumentRequestIds,
    IReadOnlyList<string> RequirementIds,
    string Description);

/// <summary>
/// The semantic interaction graph shared by Design, generated source, historical validation, and
/// Paper. It prevents a chart indicator and a trading rule from silently using different values.
/// </summary>
public sealed record StrategyInteractionBindingsV1(
    string SchemaVersion,
    IReadOnlyList<StrategyParameterBindingV1> Parameters,
    IReadOnlyList<StrategyFeatureBindingV1> Features,
    IReadOnlyList<StrategyLayerBindingV1> Layers,
    IReadOnlyList<StrategyRuleBindingV1> Rules)
{
    public const string CurrentSchemaVersion = "strategy-interaction-bindings/v1";
}

public sealed record StrategyInteractionBindingIssueV1(string Code, string Path, string Message);

public static class StrategyInteractionBindingsCanonicalJsonV1
{
    public static string Serialize(StrategyInteractionBindingsV1 value) =>
        ExecutableStrategyDefinitionCanonicalJson.Serialize(value);

    public static string Hash(StrategyInteractionBindingsV1 value) =>
        ExecutableStrategyDefinitionCanonicalJson.Hash(value);
}

public static class StrategyInteractionBindingsValidatorV1
{
    public static IReadOnlyList<StrategyInteractionBindingIssueV1> Validate(
        AuthoredUnitSpecificationV1 specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        var graph = specification.InteractionBindings;
        var issues = new List<StrategyInteractionBindingIssueV1>();
        if (graph is null)
        {
            issues.Add(Issue("unit.interaction.required", "interactionBindings",
                "A v2 authored unit requires explicit parameter, feature, layer, and rule bindings."));
            return issues;
        }

        if (!string.Equals(graph.SchemaVersion, StrategyInteractionBindingsV1.CurrentSchemaVersion, StringComparison.Ordinal))
            issues.Add(Issue("unit.interaction.schema.unsupported", "interactionBindings.schemaVersion",
                $"Interaction bindings must use '{StrategyInteractionBindingsV1.CurrentSchemaVersion}'."));

        var parameterIds = UniqueIds(graph.Parameters, static item => item?.ParameterId,
            "interactionBindings.parameters", "parameter", issues);
        var featureIds = UniqueIds(graph.Features, static item => item?.FeatureId,
            "interactionBindings.features", "feature", issues);
        var layerIds = UniqueIds(graph.Layers, static item => item?.LayerId,
            "interactionBindings.layers", "layer binding", issues);
        _ = UniqueIds(graph.Rules, static item => item?.RuleId,
            "interactionBindings.rules", "rule", issues);

        var authoredParameters = (specification.Parameters ?? [])
            .Where(static item => item is not null)
            .Select(static item => item.Key)
            .ToHashSet(StringComparer.Ordinal);
        var authoredLayers = (specification.Drawing?.Layers ?? [])
            .Where(static item => item is not null)
            .Select(static item => item.LayerId)
            .ToHashSet(StringComparer.Ordinal);
        var authoredInstruments = (specification.Instruments ?? [])
            .Where(static item => item is not null)
            .Select(static item => item.RequestId)
            .ToHashSet(StringComparer.Ordinal);
        var intentRequirements = (specification.ConfirmedStrategyIntent?.Requirements ?? [])
            .Where(static item => item is not null)
            .Select(static item => item.RequirementId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var parameter in graph.Parameters ?? [])
        {
            if (parameter is null) continue;
            RequiredId(parameter.ParameterId, "interactionBindings.parameters.parameterId", issues);
            if (string.IsNullOrWhiteSpace(parameter.ParameterKey) || !authoredParameters.Contains(parameter.ParameterKey))
                issues.Add(Issue("unit.interaction.parameter.key_unknown", "interactionBindings.parameters.parameterKey",
                    $"Parameter '{parameter.ParameterId}' references unknown authored parameter key '{parameter.ParameterKey}'."));
        }

        foreach (var feature in graph.Features ?? [])
        {
            if (feature is null) continue;
            RequiredId(feature.FeatureId, "interactionBindings.features.featureId", issues);
            if (string.IsNullOrWhiteSpace(feature.TypeId))
                issues.Add(Issue("unit.interaction.feature.type_required", "interactionBindings.features.typeId",
                    $"Feature '{feature.FeatureId}' requires a stable type id."));
            if (feature.RequiredData == StrategyDataRequirement.None ||
                (feature.RequiredData & specification.DataRequirement) != feature.RequiredData)
                issues.Add(Issue("unit.interaction.feature.data_unavailable", "interactionBindings.features.requiredData",
                    $"Feature '{feature.FeatureId}' requires data not declared by the authored unit."));
            ValidateReferences(feature.ParameterIds, parameterIds, "feature parameter", feature.FeatureId, issues);
        }

        foreach (var layer in graph.Layers ?? [])
        {
            if (layer is null) continue;
            RequiredId(layer.LayerId, "interactionBindings.layers.layerId", issues);
            if (!authoredLayers.Contains(layer.LayerId))
                issues.Add(Issue("unit.interaction.layer.unknown", "interactionBindings.layers.layerId",
                    $"Layer binding '{layer.LayerId}' does not identify a drawing layer."));
            ValidateReferences(layer.FeatureIds, featureIds, "layer feature", layer.LayerId, issues);
            foreach (var binding in layer.ParameterBindings ?? new Dictionary<string, string>())
            {
                if (string.IsNullOrWhiteSpace(binding.Key))
                    issues.Add(Issue("unit.interaction.layer.parameter_name_required", "interactionBindings.layers.parameterBindings",
                        $"Layer '{layer.LayerId}' has an empty drawing-parameter name."));
                if (!parameterIds.Contains(binding.Value))
                    issues.Add(Issue("unit.interaction.layer.parameter_unknown", "interactionBindings.layers.parameterBindings",
                        $"Layer '{layer.LayerId}' references unknown parameter id '{binding.Value}'."));
            }
        }

        foreach (var missingLayer in authoredLayers.Except(layerIds, StringComparer.Ordinal))
            issues.Add(Issue("unit.interaction.layer.binding_missing", "interactionBindings.layers",
                $"Drawing layer '{missingLayer}' has no interaction binding."));

        foreach (var rule in graph.Rules ?? [])
        {
            if (rule is null) continue;
            RequiredId(rule.RuleId, "interactionBindings.rules.ruleId", issues);
            ValidateReferences(rule.FeatureIds, featureIds, "rule feature", rule.RuleId, issues);
            ValidateReferences(rule.ParameterIds, parameterIds, "rule parameter", rule.RuleId, issues);
            ValidateReferences(rule.InstrumentRequestIds, authoredInstruments, "rule instrument", rule.RuleId, issues);
            ValidateReferences(rule.RequirementIds, intentRequirements, "rule requirement", rule.RuleId, issues);
            if (string.IsNullOrWhiteSpace(rule.Description))
                issues.Add(Issue("unit.interaction.rule.description_required", "interactionBindings.rules.description",
                    $"Rule '{rule.RuleId}' requires a reviewed description."));
        }

        if (specification.Kind == AuthoredUnitKindV1.Visualizer && (graph.Rules?.Count ?? 0) != 0)
            issues.Add(Issue("unit.interaction.visualizer.rule_forbidden", "interactionBindings.rules",
                "A display-only visualizer cannot contain a position-changing rule."));
        if (specification.Kind == AuthoredUnitKindV1.Strategy && (graph.Rules?.Count ?? 0) == 0)
            issues.Add(Issue("unit.interaction.strategy.rule_required", "interactionBindings.rules",
                "A strategy requires at least one explicit rule binding."));

        return issues;
    }

    public static void RequireValid(AuthoredUnitSpecificationV1 specification)
    {
        var issues = Validate(specification);
        if (issues.Count != 0)
            throw new ArgumentException(string.Join(" ", issues.Select(static issue => $"{issue.Code}: {issue.Message}")), nameof(specification));
    }

    private static HashSet<string> UniqueIds<T>(
        IReadOnlyList<T>? items,
        Func<T, string?> selector,
        string path,
        string label,
        ICollection<StrategyInteractionBindingIssueV1> issues)
    {
        if (items is null)
        {
            issues.Add(Issue($"unit.interaction.{label.Replace(' ', '_')}.list_required", path,
                $"The {label} list must be present, even when empty."));
            return [];
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            if (item is null)
            {
                issues.Add(Issue($"unit.interaction.{label.Replace(' ', '_')}.null", path,
                    $"The {label} list cannot contain null values."));
                continue;
            }
            var id = selector(item);
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!ids.Add(id))
                issues.Add(Issue($"unit.interaction.{label.Replace(' ', '_')}.duplicate", path,
                    $"The {label} id '{id}' is duplicated."));
        }
        return ids;
    }

    private static void RequiredId(
        string? id,
        string path,
        ICollection<StrategyInteractionBindingIssueV1> issues)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(static character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
            issues.Add(Issue("unit.interaction.id.invalid", path,
                "Stable interaction ids may contain only ASCII letters, digits, '.', '_' and '-'."));
    }

    private static void ValidateReferences(
        IReadOnlyCollection<string>? values,
        IReadOnlySet<string> known,
        string label,
        string owner,
        ICollection<StrategyInteractionBindingIssueV1> issues)
    {
        if (values is null)
        {
            issues.Add(Issue("unit.interaction.references.required", "interactionBindings",
                $"{label} references for '{owner}' must be present, even when empty."));
            return;
        }
        if (values.Count != values.Distinct(StringComparer.Ordinal).Count())
            issues.Add(Issue("unit.interaction.references.duplicate", "interactionBindings",
                $"{label} references for '{owner}' contain duplicates."));
        foreach (var value in values.Where(value => !known.Contains(value)))
            issues.Add(Issue("unit.interaction.reference.unknown", "interactionBindings",
                $"{label} reference '{value}' on '{owner}' is unknown."));
    }

    private static StrategyInteractionBindingIssueV1 Issue(string code, string path, string message) =>
        new(code, path, message);
}

public static class StrategyInteractionBindingEditorV1
{
    /// <summary>
    /// Applies one canonical parameter edit to its single authored parameter. Drawing and rule
    /// bindings remain stable and resolve the new value through the shared parameter id.
    /// </summary>
    public static AuthoredUnitSpecificationV1 ApplyCanonicalParameter(
        AuthoredUnitSpecificationV1 specification,
        string parameterId,
        string canonicalValue)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentException.ThrowIfNullOrWhiteSpace(parameterId);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalValue);
        StrategyInteractionBindingsValidatorV1.RequireValid(specification);

        var graph = specification.InteractionBindings!;
        var binding = graph.Parameters.SingleOrDefault(item =>
            string.Equals(item.ParameterId, parameterId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown parameter id '{parameterId}'.", nameof(parameterId));
        var parameters = specification.Parameters
            .Select(item => string.Equals(item.Key, binding.ParameterKey, StringComparison.Ordinal)
                ? item with { CanonicalDefault = canonicalValue }
                : item)
            .ToArray();
        var updated = specification with
        {
            Parameters = parameters,
        };
        StrategyInteractionBindingsValidatorV1.RequireValid(updated);
        return updated;
    }

    /// <summary>
    /// Resolves the effective drawing parameters for one layer from the same canonical parameter
    /// values consumed by strategy rules. The stored layer template remains unchanged.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ResolveLayerParameters(
        AuthoredUnitSpecificationV1 specification,
        string layerId)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
        StrategyInteractionBindingsValidatorV1.RequireValid(specification);

        var layer = specification.Drawing.Layers.SingleOrDefault(item =>
            string.Equals(item.LayerId, layerId, StringComparison.Ordinal))
            ?? throw new ArgumentException($"Unknown layer id '{layerId}'.", nameof(layerId));
        var layerBinding = specification.InteractionBindings!.Layers.Single(item =>
            string.Equals(item.LayerId, layerId, StringComparison.Ordinal));
        var parameterKeyById = specification.InteractionBindings.Parameters
            .ToDictionary(static item => item.ParameterId, static item => item.ParameterKey, StringComparer.Ordinal);
        var canonicalByKey = specification.Parameters
            .ToDictionary(static item => item.Key, static item => item.CanonicalDefault, StringComparer.Ordinal);
        var resolved = layer.Parameters
            .ToDictionary(static item => item.Key, static item => item.Value, StringComparer.Ordinal);
        foreach (var binding in layerBinding.ParameterBindings)
        {
            var key = parameterKeyById[binding.Value];
            resolved[binding.Key] = canonicalByKey[key];
        }
        return resolved;
    }
}

public static class StrategyInteractionBindingsFactoryV1
{
    /// <summary>
    /// Deterministically upgrades a host-created legacy specification. AI-produced v2 output must
    /// still declare its own exact graph; this factory exists for trusted catalog/test construction
    /// and legacy migration, never for guessing provider intent.
    /// </summary>
    public static AuthoredUnitSpecificationV1 UpgradeTrustedSpecification(AuthoredUnitSpecificationV1 specification)
    {
        ArgumentNullException.ThrowIfNull(specification);
        if (specification.InteractionBindings is not null)
            return specification with { SchemaVersion = AuthoredUnitSpecificationV1.CurrentSchemaVersion };

        var parameters = specification.Parameters
            .Select(item => new StrategyParameterBindingV1($"parameter.{item.Key}", item.Key))
            .ToArray();
        var parameterByKey = parameters.ToDictionary(static item => item.ParameterKey, StringComparer.Ordinal);
        var features = specification.Drawing.Layers.Select(layer =>
        {
            var linked = layer.Parameters
                .Select(static item => TryReadParameterKey(item.Value) ?? item.Key)
                .Where(parameterByKey.ContainsKey)
                .Select(key => parameterByKey[key].ParameterId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return new StrategyFeatureBindingV1(
                $"feature.{layer.LayerId}",
                layer.TypeId,
                RequiredData(layer.Kind),
                linked,
                layer.LayerId);
        }).ToArray();
        var layers = specification.Drawing.Layers.Select(layer =>
        {
            var parameterBindings = layer.Parameters
                .Select(item => (DrawingParameter: item.Key, ParameterKey: TryReadParameterKey(item.Value) ?? item.Key))
                .Where(item => parameterByKey.ContainsKey(item.ParameterKey))
                .ToDictionary(
                    static item => item.DrawingParameter,
                    item => parameterByKey[item.ParameterKey].ParameterId,
                    StringComparer.Ordinal);
            return new StrategyLayerBindingV1(
                layer.LayerId,
                [$"feature.{layer.LayerId}"],
                parameterBindings);
        }).ToArray();
        var featureIds = features.Select(static item => item.FeatureId).ToArray();
        var parameterIds = parameters.Select(static item => item.ParameterId).ToArray();
        var instrumentIds = specification.Instruments.Select(static item => item.RequestId).ToArray();
        var requirements = specification.ConfirmedStrategyIntent?.Requirements
            .Where(static item => item.Stage == StrategySemanticStageV1.DecideIntent &&
                item.Disposition == StrategySemanticDispositionV1.Applicable)
            .ToArray() ?? [];
        StrategyRuleBindingV1[] rules = specification.Kind == AuthoredUnitKindV1.Strategy
            ? (requirements.Length == 0
                ? [new StrategyRuleBindingV1(
                    "rule.primary",
                    featureIds,
                    parameterIds,
                    instrumentIds,
                    [],
                    "Primary reviewed strategy decision rule.")]
                : requirements.Select(requirement => new StrategyRuleBindingV1(
                    $"rule.{requirement.RequirementId}",
                    featureIds,
                    parameterIds,
                    instrumentIds,
                    [requirement.RequirementId],
                    requirement.Description)).ToArray())
            : [];

        var upgraded = specification with
        {
            SchemaVersion = AuthoredUnitSpecificationV1.CurrentSchemaVersion,
            InteractionBindings = new StrategyInteractionBindingsV1(
                StrategyInteractionBindingsV1.CurrentSchemaVersion,
                parameters,
                features,
                layers,
                rules),
        };
        StrategyInteractionBindingsValidatorV1.RequireValid(upgraded);
        return upgraded;
    }

    private static StrategyDataRequirement RequiredData(AuthoredChartLayerKindV1 kind) => kind switch
    {
        AuthoredChartLayerKindV1.OrderBook => StrategyDataRequirement.Depth,
        AuthoredChartLayerKindV1.Footprint or AuthoredChartLayerKindV1.TradeTape => StrategyDataRequirement.TradeTape,
        _ => StrategyDataRequirement.Bars,
    };

    private static string? TryReadParameterKey(string value)
    {
        if (value.Length < 4 || value[0] != '$' || value[1] != '{' || value[^1] != '}')
            return null;
        var key = value[2..^1];
        return string.IsNullOrWhiteSpace(key) ? null : key;
    }
}
