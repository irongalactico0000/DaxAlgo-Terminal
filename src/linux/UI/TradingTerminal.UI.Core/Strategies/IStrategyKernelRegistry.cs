using DaxAlgo.Sdk;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;

namespace TradingTerminal.UI.Strategies;

/// <summary>One runnable canonical SDK strategy and its immutable authored specification.</summary>
public sealed record StrategyKernelRegistration(
    string Id,
    string DisplayName,
    string Description,
    Func<IStrategyKernel> Create,
    AuthoredUnitSpecificationV1 AuthoredSpecification,
    StrategyParameterSchema Schema)
{
    public StrategyDataRequirement DataRequirement => AuthoredSpecification.DataRequirement;
}

/// <summary>
/// App-lifetime registry for canonical <see cref="IStrategyKernel"/> artifacts. The legacy
/// backtest-strategy registry remains an adapter source; new AI-authored strategies enter here.
/// </summary>
public interface IStrategyKernelRegistry
{
    IReadOnlyList<StrategyKernelRegistration> All { get; }
    StrategyKernelRegistration? Find(string id);
    void Register(StrategyKernelRegistration registration);
    bool Remove(string id);
    event EventHandler? Changed;
}

public sealed class StrategyKernelRegistry : IStrategyKernelRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, StrategyKernelRegistration> _byId = new(StringComparer.Ordinal);

    public StrategyKernelRegistry(IEnumerable<AuthoredStrategyKernelPluginRegistration>? authoredPlugins = null)
    {
        foreach (var plugin in authoredPlugins ?? [])
        {
            try
            {
                if (plugin.VerificationContractVersion != AuthoredPluginBootstrap.CurrentVerificationContractVersion)
                    continue;
                if (string.IsNullOrWhiteSpace(plugin.SpecificationJson))
                    continue;
                var specification = AuthoredUnitSpecificationCanonicalJsonV1.Deserialize(plugin.SpecificationJson);
                if (specification.Kind != AuthoredUnitKindV1.Strategy ||
                    AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification).Count != 0 ||
                    !CanHost(plugin.KernelType))
                    continue;

                var hashProperty = plugin.KernelType.GetProperty(
                    "SpecificationHashSha256",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
                var instance = (IStrategyKernel)Activator.CreateInstance(plugin.KernelType)!;
                if (hashProperty?.PropertyType != typeof(string) ||
                    !string.Equals(hashProperty.GetValue(null) as string, hash, StringComparison.Ordinal) ||
                    instance is not IAuthoredDrawingManifest manifest ||
                    !specification.Drawing.Layers.Select(static layer => layer.TypeId)
                        .SequenceEqual(manifest.DrawingLayerTypeIds, StringComparer.Ordinal) ||
                    instance.DataRequirement != specification.DataRequirement)
                    continue;

                _byId[plugin.Id] = new StrategyKernelRegistration(
                    plugin.Id,
                    plugin.DisplayName,
                    plugin.Description,
                    () => (IStrategyKernel)Activator.CreateInstance(plugin.KernelType)!,
                    specification,
                    instance.Schema);
            }
            catch
            {
                // Invalid or stale persisted plugins are not executable catalog entries.
            }
        }
    }

    public IReadOnlyList<StrategyKernelRegistration> All
    {
        get { lock (_gate) return _byId.Values.ToArray(); }
    }

    public StrategyKernelRegistration? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_gate) return _byId.GetValueOrDefault(id);
    }

    public void Register(StrategyKernelRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (_gate) _byId[registration.Id] = registration;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Remove(string id)
    {
        bool removed;
        lock (_gate) removed = _byId.Remove(id);
        if (removed) Changed?.Invoke(this, EventArgs.Empty);
        return removed;
    }

    public event EventHandler? Changed;

    private static bool CanHost(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsPublic: true } &&
        typeof(IStrategyKernel).IsAssignableFrom(type) &&
        type.GetConstructor(Type.EmptyTypes) is not null;
}
