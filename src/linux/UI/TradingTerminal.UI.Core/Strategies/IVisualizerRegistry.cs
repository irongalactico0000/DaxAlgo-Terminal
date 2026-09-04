using DaxAlgo.Sdk;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.UI.Strategies;

/// <summary>
/// One registered visualizer: what the catalog card says about it, and how to build one.
///
/// <para>The factory is what makes this more than a descriptor. A <see cref="VisualizerDescriptor"/>
/// alone can only be displayed — which is exactly why the catalog's "Add to chart" button did nothing
/// for so long. Registration pairs the card with something runnable.</para>
/// </summary>
/// <param name="Descriptor">The catalog card's metadata.</param>
/// <param name="Create">Builds a fresh instance. Called once per opened window, never shared.</param>
public sealed record VisualizerRegistration(
    VisualizerDescriptor Descriptor,
    Func<IVisualizer> Create,
    AuthoredUnitSpecificationV1? AuthoredSpecification = null)
{
    public string Id => Descriptor.Id;
}

/// <summary>
/// The runtime source of available visualizers — the counterpart to
/// <c>IBacktestStrategyRegistry</c>, and deliberately the same shape, because a user installing a
/// visualizer pack and a user installing a strategy pack should not meet two different mechanisms.
///
/// <para><see cref="Changed"/> is what lets a visualizer authored in Hyperion appear in the catalog
/// without a restart.</para>
/// </summary>
public interface IVisualizerRegistry
{
    IReadOnlyList<VisualizerRegistration> All { get; }

    /// <summary>Looks one up by id, or null when nothing is registered under it.</summary>
    VisualizerRegistration? Find(string id);

    /// <summary>Adds one, replacing any existing entry with the same id. Raises <see cref="Changed"/>.</summary>
    void Register(VisualizerRegistration registration);

    /// <summary>Removes one by id. Returns true if it was there. Raises <see cref="Changed"/>.</summary>
    bool Remove(string id);

    /// <summary>Fires when the set changes — a runtime author, install, or removal.</summary>
    event EventHandler? Changed;
}

/// <inheritdoc />
public sealed class VisualizerRegistry : IVisualizerRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<string, VisualizerRegistration> _byId = new(StringComparer.Ordinal);

    public VisualizerRegistry(
        IEnumerable<VisualizerRegistration>? registrations = null,
        IEnumerable<AuthoredVisualizerPluginRegistration>? authoredPlugins = null)
    {
        foreach (var registration in registrations ?? [])
            _byId[registration.Id] = registration;

        foreach (var plugin in authoredPlugins ?? [])
        {
            if (plugin.VerificationContractVersion != AuthoredPluginBootstrap.CurrentVerificationContractVersion)
                continue;

            AuthoredUnitSpecificationV1? specification = null;
            if (!string.IsNullOrWhiteSpace(plugin.SpecificationJson))
            {
                try
                {
                    var parsed = AuthoredUnitSpecificationCanonicalJsonV1.Deserialize(plugin.SpecificationJson);
                    var hash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(parsed);
                    var property = plugin.VisualizerType.GetProperty(
                        "SpecificationHashSha256",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(parsed).Count == 0 &&
                        parsed.Kind == AuthoredUnitKindV1.Visualizer &&
                        property?.PropertyType == typeof(string) &&
                        string.Equals(property.GetValue(null) as string, hash, StringComparison.Ordinal) &&
                        Activator.CreateInstance(plugin.VisualizerType) is IAuthoredDrawingManifest manifest &&
                        parsed.Drawing.Layers.Select(static layer => layer.TypeId)
                            .SequenceEqual(manifest.DrawingLayerTypeIds, StringComparer.Ordinal))
                    {
                        specification = parsed;
                    }
                }
                catch
                {
                    // A persisted unit without a valid specification binding is not launchable.
                }
            }

            if (specification is null)
                continue;

            var discovered = VisualizerDescriptors.FromType(plugin.VisualizerType, plugin.Id);
            _byId[plugin.Id] = discovered with
            {
                Descriptor = discovered.Descriptor with
                {
                    DisplayName = plugin.DisplayName,
                    Description = plugin.Description,
                },
                AuthoredSpecification = specification,
            };
        }
    }

    public IReadOnlyList<VisualizerRegistration> All
    {
        get { lock (_gate) return _byId.Values.ToArray(); }
    }

    public VisualizerRegistration? Find(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        lock (_gate) return _byId.TryGetValue(id, out var registration) ? registration : null;
    }

    public void Register(VisualizerRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        lock (_gate) _byId[registration.Id] = registration;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        bool removed;
        lock (_gate) removed = _byId.Remove(id);
        if (removed)
            Changed?.Invoke(this, EventArgs.Empty);
        return removed;
    }

    public event EventHandler? Changed;
}
