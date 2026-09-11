using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.App.Authoring;

/// <summary>
/// W4 Bindings inspector: surfaces StrategyInteractionBindingsV1 (parameters / features / layers / rules)
/// and applies canonical parameter edits without inventing a second value path.
/// </summary>
public sealed partial class StrategyAuthoringViewModel
{
    public ObservableCollection<InteractionBindingParameterRow> InteractionBindingParameters { get; } = [];
    public ObservableCollection<InteractionBindingFeatureRow> InteractionBindingFeatures { get; } = [];
    public ObservableCollection<InteractionBindingLayerRow> InteractionBindingLayers { get; } = [];
    public ObservableCollection<InteractionBindingRuleRow> InteractionBindingRules { get; } = [];

    public bool HasInteractionBindings =>
        AuthoredUnitSpecification?.InteractionBindings is not null;

    public string InteractionBindingsSummaryText
    {
        get
        {
            if (AuthoredUnitSpecification?.InteractionBindings is not { } graph)
                return "No interaction bindings on the current authored unit. Compile/generate a v2 unit first.";

            return $"{graph.Parameters.Count} parameters · {graph.Features.Count} features · " +
                   $"{graph.Layers.Count} layers · {graph.Rules.Count} rules. " +
                   "Edit a parameter once; features and layers resolve the same id. Hiding a layer is not modeled here (appearance ≠ disable rule).";
        }
    }

    private void RefreshInteractionBindingsInspector()
    {
        InteractionBindingParameters.Clear();
        InteractionBindingFeatures.Clear();
        InteractionBindingLayers.Clear();
        InteractionBindingRules.Clear();

        OnPropertyChanged(nameof(HasInteractionBindings));
        OnPropertyChanged(nameof(InteractionBindingsSummaryText));
        ApplyInteractionParameterCommand.NotifyCanExecuteChanged();

        if (AuthoredUnitSpecification?.InteractionBindings is not { } graph)
            return;

        var canonicalByKey = AuthoredUnitSpecification.Parameters
            .ToDictionary(static item => item.Key, static item => item.CanonicalDefault, StringComparer.Ordinal);

        foreach (var parameter in graph.Parameters.OrderBy(static item => item.ParameterId, StringComparer.Ordinal))
        {
            canonicalByKey.TryGetValue(parameter.ParameterKey, out var canonical);
            var presence = AuthoredUnitSpecification.Parameters
                .FirstOrDefault(item => string.Equals(item.Key, parameter.ParameterKey, StringComparison.Ordinal))
                ?.Presence
                ?? AuthoredUnitParameterPresenceV1.Defaultable;
            InteractionBindingParameters.Add(new InteractionBindingParameterRow(
                parameter.ParameterId,
                parameter.ParameterKey,
                canonical ?? string.Empty,
                presence.ToString()));
        }

        foreach (var feature in graph.Features.OrderBy(static item => item.FeatureId, StringComparer.Ordinal))
        {
            InteractionBindingFeatures.Add(new InteractionBindingFeatureRow(
                feature.FeatureId,
                feature.TypeId,
                feature.DisplayName,
                string.Join(", ", feature.ParameterIds),
                feature.RequiredData.ToString()));
        }

        foreach (var layer in graph.Layers.OrderBy(static item => item.LayerId, StringComparer.Ordinal))
        {
            string resolved;
            try
            {
                resolved = string.Join(
                    ", ",
                    StrategyInteractionBindingEditorV1.ResolveLayerParameters(AuthoredUnitSpecification, layer.LayerId)
                        .OrderBy(static item => item.Key, StringComparer.Ordinal)
                        .Select(static item => $"{item.Key}={item.Value}"));
            }
            catch (Exception exception)
            {
                resolved = $"unresolved ({exception.Message})";
            }

            InteractionBindingLayers.Add(new InteractionBindingLayerRow(
                layer.LayerId,
                string.Join(", ", layer.FeatureIds),
                string.Join(", ", layer.ParameterBindings.OrderBy(static item => item.Key, StringComparer.Ordinal)
                    .Select(static item => $"{item.Key}→{item.Value}")),
                resolved));
        }

        foreach (var rule in graph.Rules.OrderBy(static item => item.RuleId, StringComparer.Ordinal))
        {
            InteractionBindingRules.Add(new InteractionBindingRuleRow(
                rule.RuleId,
                rule.Description,
                string.Join(", ", rule.FeatureIds),
                string.Join(", ", rule.ParameterIds),
                string.Join(", ", rule.InstrumentRequestIds)));
        }
    }

    private bool CanApplyInteractionParameter(InteractionBindingParameterRow? row) =>
        row is not null &&
        AuthoredUnitSpecification?.InteractionBindings is not null &&
        !string.IsNullOrWhiteSpace(row.EditValue) &&
        !IsGenerating;

    [RelayCommand(CanExecute = nameof(CanApplyInteractionParameter))]
    private void ApplyInteractionParameter(InteractionBindingParameterRow? row)
    {
        if (row is null || AuthoredUnitSpecification is null)
            return;

        var value = row.EditValue.Trim();
        if (string.IsNullOrWhiteSpace(value))
            return;

        try
        {
            var updated = StrategyInteractionBindingEditorV1.ApplyCanonicalParameter(
                AuthoredUnitSpecification,
                row.ParameterId,
                value);

            InvalidateDerivedArtifactState(markUnregistered: true);
            AuthoredUnitSpecification = updated;
            RefreshInteractionBindingsInspector();
            Status =
                $"Canonical parameter {row.ParameterId} → {value}. " +
                "Feature/layer resolve share this id. Recompile and register before historical BT / Paper.";
            AiStatus = Status;
            Save();
        }
        catch (Exception exception)
        {
            Status = $"Bindings edit rejected: {exception.Message}";
            AiStatus = Status;
        }
    }
}

public sealed partial class InteractionBindingParameterRow : ObservableObject
{
    public InteractionBindingParameterRow(string parameterId, string parameterKey, string canonicalValue, string presence)
    {
        ParameterId = parameterId;
        ParameterKey = parameterKey;
        CanonicalValue = canonicalValue;
        Presence = presence;
        EditValue = canonicalValue;
    }

    public string ParameterId { get; }
    public string ParameterKey { get; }
    public string CanonicalValue { get; }
    public string Presence { get; }
    [ObservableProperty] private string _editValue;
}

public sealed record InteractionBindingFeatureRow(
    string FeatureId,
    string TypeId,
    string DisplayName,
    string ParameterIds,
    string RequiredData);

public sealed record InteractionBindingLayerRow(
    string LayerId,
    string FeatureIds,
    string ParameterBindings,
    string ResolvedParameters);

public sealed record InteractionBindingRuleRow(
    string RuleId,
    string Description,
    string FeatureIds,
    string ParameterIds,
    string InstrumentRequestIds);
