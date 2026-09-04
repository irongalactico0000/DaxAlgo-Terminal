using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradingTerminal.UI;

namespace TradingTerminal.UI.Strategies;

/// <summary>
/// Edits a strategy card's presentation overrides. The reusable Windows behavior is retained; only
/// the file-picker seam is adapted to the platform-neutral <see cref="UiFile"/> service.
/// </summary>
public sealed partial class StrategyPresentationEditorViewModel : ViewModelBase
{
    private readonly StrategyCatalogItemViewModel _item;

    public StrategyPresentationEditorViewModel(StrategyCatalogItemViewModel item)
    {
        _item = item;
        DefaultName = item.Strategy?.DisplayName ?? item.Visualizer!.DisplayName;
        DefaultDescription = item.Strategy?.Description ?? item.Visualizer!.Description;
        DefaultLinkUrl = item.Strategy?.LinkUrl ?? string.Empty;

        _name = item.Name;
        _description = item.Description;
        _tagsText = string.Join(", ", item.CustomTags);
        _linkUrl = item.LinkUrl ?? string.Empty;
        _formula = item.Formula ?? string.Empty;
        _imagePath = item.ImagePath;
    }

    public string StrategyId => _item.Id;
    public string DefaultName { get; }
    public string DefaultDescription { get; }
    public string DefaultLinkUrl { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _tagsText;
    [ObservableProperty] private string _description;
    [ObservableProperty] private string _linkUrl;
    [ObservableProperty] private string _formula;
    [ObservableProperty] private string? _imagePath;

    public bool HasImage => !string.IsNullOrWhiteSpace(ImagePath);
    partial void OnImagePathChanged(string? value) => OnPropertyChanged(nameof(HasImage));

    [RelayCommand]
    private async Task BrowseImage()
    {
        var path = await UiFile.OpenAsync(
            "Strategy UI images",
            new[] { "png", "jpg", "jpeg", "bmp", "gif" });
        if (!string.IsNullOrWhiteSpace(path)) ImagePath = path;
    }

    [RelayCommand]
    private void ClearImage() => ImagePath = null;

    [RelayCommand]
    private void ResetToDefault()
    {
        Name = DefaultName;
        Description = DefaultDescription;
        TagsText = string.Empty;
        LinkUrl = DefaultLinkUrl;
        Formula = string.Empty;
        ImagePath = null;
    }

    public StrategyPresentation Build()
    {
        var tags = TagsText.Split(
            new[] { ',', ';' },
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new StrategyPresentation(
            Name: string.IsNullOrWhiteSpace(Name) || string.Equals(Name.Trim(), DefaultName) ? null : Name.Trim(),
            Description: string.IsNullOrWhiteSpace(Description) || string.Equals(Description.Trim(), DefaultDescription)
                ? null
                : Description.Trim(),
            Tags: tags.Length == 0 ? null : tags,
            LinkUrl: string.IsNullOrWhiteSpace(LinkUrl) ||
                     string.Equals(LinkUrl.Trim(), DefaultLinkUrl, StringComparison.Ordinal)
                ? null
                : LinkUrl.Trim(),
            Formula: string.IsNullOrWhiteSpace(Formula) ? null : Formula.Trim(),
            ImagePath: string.IsNullOrWhiteSpace(ImagePath) ? null : ImagePath);
    }
}
