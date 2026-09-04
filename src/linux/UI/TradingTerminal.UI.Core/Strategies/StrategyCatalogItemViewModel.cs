using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TradingTerminal.Core.Strategies;

namespace TradingTerminal.UI.Strategies;

/// <summary>
/// One catalog row backed by either a compiled strategy or a runnable visualizer descriptor, plus
/// the user's presentation overrides.
/// </summary>
public sealed partial class StrategyCatalogItemViewModel : ViewModelBase
{
    public StrategyCatalogItemViewModel(ITradingStrategy strategy)
        : this(strategy, StrategyPresentationStore.Get(strategy.Id)) { }

    public StrategyCatalogItemViewModel(ITradingStrategy strategy, StrategyPresentation presentation)
    {
        Strategy = strategy;
        _name = strategy.DisplayName;
        _description = strategy.Description;
        Apply(presentation);
    }

    public StrategyCatalogItemViewModel(VisualizerDescriptor visualizer)
        : this(visualizer, StrategyPresentationStore.Get(visualizer.Id)) { }

    public StrategyCatalogItemViewModel(VisualizerDescriptor visualizer, StrategyPresentation presentation)
    {
        Visualizer = visualizer;
        Kind = CatalogItemKind.Visualizer;
        _name = visualizer.DisplayName;
        _description = visualizer.Description;
        Apply(presentation);
    }

    public StrategyCatalogItemViewModel(StrategyKernelRegistration strategyKernel)
        : this(strategyKernel, StrategyPresentationStore.Get(strategyKernel.Id)) { }

    public StrategyCatalogItemViewModel(
        StrategyKernelRegistration strategyKernel,
        StrategyPresentation presentation)
    {
        StrategyKernel = strategyKernel ?? throw new ArgumentNullException(nameof(strategyKernel));
        _name = strategyKernel.DisplayName;
        _description = strategyKernel.Description;
        Apply(presentation);
    }

    /// <summary>The underlying strategy — the catalog's pill converters, Open and Quick-backtest all key
    /// off this, so it stays exposed even as the display fields are overridden.</summary>
    public CatalogItemKind Kind { get; } = CatalogItemKind.Strategy;
    public ITradingStrategy? Strategy { get; }
    public VisualizerDescriptor? Visualizer { get; }
    public StrategyKernelRegistration? StrategyKernel { get; }
    public bool HasLegacyStrategy => Strategy is not null;

    public string Id => Strategy?.Id ?? Visualizer?.Id ?? StrategyKernel!.Id;
    public string KindLabel => Kind == CatalogItemKind.Strategy ? "STRATEGY" : "VISUALIZER";
    public string PrimaryActionLabel => StrategyKernel is not null
        ? "Run strategy in Paper"
        : Kind == CatalogItemKind.Strategy ? "Open strategy" : "Open visualizer";
    public bool HasQuickBacktest => Strategy is not null ||
        StrategyKernel is { AuthoredSpecification.Instruments.Count: > 0 } authored &&
        !authored.DataRequirement.HasFlag(StrategyDataRequirement.Depth) &&
        authored.AuthoredSpecification.Timeframe.BarSize is { } interval &&
        Enum.GetValues<TradingTerminal.Core.Domain.BarSize>()
            .Any(size => TradingTerminal.Core.Domain.BarSizeExtensions.ToTimeSpan(size) == interval);
    public IReadOnlyList<string> DataRequirementTags => Visualizer?.DataRequirementTags
        ?? (StrategyKernel is null ? [] : BuildAuthoredStrategyTags(StrategyKernel));
    public bool HasDataRequirementTags => DataRequirementTags.Count != 0;

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _description;
    [ObservableProperty] private string? _linkUrl;
    [ObservableProperty] private string? _formula;
    [ObservableProperty] private string? _imagePath;

    /// <summary>Extra free-text tags the user added — rendered alongside the auto data/asset pills.</summary>
    public ObservableCollection<string> CustomTags { get; } = [];

    public bool HasFormula => !string.IsNullOrWhiteSpace(Formula);
    public bool HasCustomTags => CustomTags.Count > 0;
    public Uri? LinkUri => Uri.TryCreate(LinkUrl, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
            ? uri
            : null;
    public bool HasLink => LinkUri is not null;

    partial void OnFormulaChanged(string? value) => OnPropertyChanged(nameof(HasFormula));
    partial void OnLinkUrlChanged(string? value)
    {
        OnPropertyChanged(nameof(LinkUri));
        OnPropertyChanged(nameof(HasLink));
    }

    /// <summary>Overlay the strategy's compiled metadata with a set of overrides (blank ⇒ fall back).</summary>
    public void Apply(StrategyPresentation presentation)
    {
        var defaultName = Strategy?.DisplayName ?? Visualizer?.DisplayName ?? StrategyKernel!.DisplayName;
        var defaultDescription = Strategy?.Description ?? Visualizer?.Description ?? StrategyKernel!.Description;
        var defaultImagePath = Visualizer?.ImagePath;

        Name = string.IsNullOrWhiteSpace(presentation.Name) ? defaultName : presentation.Name!;
        Description = string.IsNullOrWhiteSpace(presentation.Description) ? defaultDescription : presentation.Description!;
        LinkUrl = string.IsNullOrWhiteSpace(presentation.LinkUrl) ? Strategy?.LinkUrl : presentation.LinkUrl.Trim();
        Formula = string.IsNullOrWhiteSpace(presentation.Formula) ? null : presentation.Formula;
        ImagePath = string.IsNullOrWhiteSpace(presentation.ImagePath) ? defaultImagePath : presentation.ImagePath;

        CustomTags.Clear();
        foreach (var tag in presentation.Tags ?? new List<string>())
            if (!string.IsNullOrWhiteSpace(tag)) CustomTags.Add(tag.Trim());
        OnPropertyChanged(nameof(HasCustomTags));
    }

    private static IReadOnlyList<string> BuildAuthoredStrategyTags(StrategyKernelRegistration registration)
    {
        var specification = registration.AuthoredSpecification;
        var tags = new List<string>();
        foreach (var instrument in specification.Instruments)
            tags.Add(instrument.UserText);
        if (specification.Timeframe.BarSize is { } barSize)
            tags.Add(barSize.ToString());
        foreach (var flag in new[]
                 {
                     StrategyDataRequirement.Bars,
                     StrategyDataRequirement.L1,
                     StrategyDataRequirement.Depth,
                     StrategyDataRequirement.TradeTape,
                 })
        {
            if (specification.DataRequirement.HasFlag(flag)) tags.Add(flag.ToString());
        }
        tags.Add("Paper only");
        return tags.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
