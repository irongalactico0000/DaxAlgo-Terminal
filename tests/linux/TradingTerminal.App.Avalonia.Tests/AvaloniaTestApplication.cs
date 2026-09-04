using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

[assembly: AvaloniaTestApplication(typeof(TradingTerminal.App.Avalonia.Tests.TestAppBuilder))]

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class TestApplication : Application
{
    public override void Initialize()
    {
        RequestedThemeVariant = ThemeVariant.Dark;
        Resources.MergedDictionaries.Add(new ResourceInclude(
            new Uri("avares://TradingTerminal.App.Avalonia/"))
        {
            Source = new Uri("avares://TradingTerminal.App.Avalonia/Themes/Palette.axaml"),
        });
        Styles.Add(new FluentTheme());
        Styles.Add(new StyleInclude(new Uri("avares://TradingTerminal.App.Avalonia/"))
        {
            Source = new Uri("avares://TradingTerminal.App.Avalonia/Themes/Controls.axaml"),
        });
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<TestApplication>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions
        {
            UseHeadlessDrawing = false,
        });
}
