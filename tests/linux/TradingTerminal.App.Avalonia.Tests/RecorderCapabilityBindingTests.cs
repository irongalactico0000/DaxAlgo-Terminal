using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class RecorderCapabilityBindingTests
{
    [Theory]
    [InlineData("L1", "SupportsQuotes", "Classes.live")]
    [InlineData("BARS", "SupportsBars", "Classes.live")]
    [InlineData("L2", "SupportsDepth", "Classes.available")]
    [InlineData("TAPE", "SupportsTape", "Classes.available")]
    public void Stream_badges_bind_to_their_channel_capability(
        string label,
        string capability,
        string classAttribute)
    {
        XNamespace av = "https://github.com/avaloniaui";
        var view = XDocument.Load(Fixture("RecorderPanelView.axaml"));
        var labelElement = view.Descendants(av + "TextBlock").Single(element =>
            (string?)element.Attribute("Text") == label);
        var chip = labelElement.Parent!.Parent!;

        chip.Attribute(classAttribute)!.Value.Should().Be($"{{Binding {capability}}}");
        labelElement.Attribute(classAttribute)!.Value.Should().Be($"{{Binding {capability}}}");
        labelElement.Parent!.Elements(av + "TextBlock").Last()
            .Attribute("IsVisible")!.Value.Should().Be($"{{Binding {capability}}}");
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
