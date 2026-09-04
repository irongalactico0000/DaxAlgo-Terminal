using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class MacPackagingConfigurationTests
{
    [Fact]
    public void App_project_targets_both_mac_architectures_and_stages_safe_configuration()
    {
        var project = XDocument.Load(Fixture("TradingTerminal.App.Avalonia.csproj"));

        project.Descendants("RuntimeIdentifiers").Single().Value.Split(';')
            .Should().Equal("osx-arm64", "osx-x64");

        var shipped = FindNone(project, "appsettings.json");
        shipped.Element("CopyToOutputDirectory")!.Value.Should().Be("PreserveNewest");
        shipped.Element("CopyToPublishDirectory")!.Value.Should().Be("PreserveNewest");

        var local = FindNone(project, "appsettings.local.json");
        local.Attribute("Condition")!.Value.Should().Contain("Exists('appsettings.local.json')");
        local.Element("CopyToPublishDirectory")!.Value.Should().Be("Never");

        foreach (var reference in project.Descendants("ProjectReference"))
        {
            var include = reference.Attribute("Include")!.Value;
            Path.IsPathRooted(include).Should().BeFalse($"{include} must remain destination-relative");
            include.Should().NotContain("DaxAlgo-Terminal-Pro");
            include.Replace('\\', '/').Should().NotContain("/src/windows/");
            include.Should().NotContain("TradingTerminal.Strategies.");
        }
    }

    [Fact]
    public void Launch_profiles_cover_professional_and_data_modes_without_platform_specific_leftovers()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Fixture("launchSettings.json")));
        var profiles = document.RootElement.GetProperty("profiles");
        var expected = new Dictionary<string, (string? Environment, string? Arguments)>
        {
            ["Avalonia Shell (Desktop)"] = (null, null),
            ["Dev — Login (Google → Broker → Terminal)"] = ("DevLogin", null),
            ["Dev — Simulated (Google → Broker → Terminal)"] = ("DevSimLogin", null),
            ["Dev — New User (signup/login test)"] = ("DevNewUser", null),
            ["Dev — No Strategies"] = ("DevNoStrategies", null),
            ["Dev — Bypass login (broker only)"] = ("DevLive", "--bypass-account-login"),
            ["Dev — Live crypto + installed plugins"] = ("DevLiveAll", "--bypass-login"),
            ["Dev — Simulated (offline)"] = ("DevSim", null),
            ["Dev — Replay (local DB)"] = ("DevReplay", null),
        };

        profiles.EnumerateObject().Select(profile => profile.Name)
            .Should().BeEquivalentTo(expected.Keys);

        foreach (var (name, selection) in expected)
        {
            var profile = profiles.GetProperty(name);
            profile.GetProperty("commandName").GetString().Should().Be("Project");

            if (selection.Environment is null)
            {
                profile.TryGetProperty("environmentVariables", out _).Should().BeFalse();
            }
            else
            {
                profile.GetProperty("environmentVariables")
                    .GetProperty("DOTNET_ENVIRONMENT").GetString()
                    .Should().Be(selection.Environment);
            }

            var arguments = profile.TryGetProperty("commandLineArgs", out var configuredArguments)
                ? configuredArguments.GetString()
                : null;
            arguments.Should().Be(selection.Arguments);
        }
    }

    [Fact]
    public void Startup_wiring_honors_environment_plugin_and_broker_profile_controls()
    {
        var composition = File.ReadAllText(Fixture("ServiceConfiguration.cs.txt"));
        composition.Should().Contain("Environment.GetEnvironmentVariable(\"ASPNETCORE_ENVIRONMENT\")");
        composition.Should().Contain("nameof(DevOptions.DisableStrategyPlugins)");
        composition.Should().Contain("if (disableStrategyPlugins)");
        composition.Should().Contain("new PluginHostContext(pluginsRoot, pluginPolicy, [])");

        var app = File.ReadAllText(Fixture("App.axaml.cs.txt"));
        app.Should().Contain("startupDevOptions.BypassLogin || bypassLoginRequested");
        app.Should().Contain("startupDevOptions.AutoConnectBrokers");
        app.Should().Contain("BrokerKind.Simulated");
        app.Should().Contain("selector.ConnectAsync(kind)");
        app.Should().Contain("!bypassAccountLoginRequested");
    }

    [Fact]
    public void Shell_visual_contract_matches_the_professional_windows_hierarchy()
    {
        var shellText = File.ReadAllText(Fixture("MainWindow.axaml"));
        var shell = XDocument.Parse(shellText);
        XNamespace av = "https://github.com/avaloniaui";
        var root = shell.Root!;

        root.Attribute("Title")!.Value.Should().Be("DaxAlgo Terminal · Professional");

        var menu = root.Descendants(av + "Menu").Single();
        menu.Elements(av + "MenuItem")
            .Select(item => item.Attribute("Header")?.Value.Replace("_", string.Empty)
                ?? item.Element(av + "MenuItem.Header")?
                    .Element(av + "TextBlock")?.Attribute("Text")?.Value)
            .Should().Equal(
                "File", "View", "Tools", "Strategy Studio", "Charts",
                "Research", "Data", "Execution Engine", "Settings", "Help");

        shellText.Should().Contain("SIMULATED DATA — not a live feed");
        shellText.Should().Contain("StringFormat='{}{0} STRATEGY ISSUE(S)'");
        shellText.Should().Contain("IsVisible=\"{Binding HasFeedDrops}\"");

        var themeFacingStaticReferences = Regex.Matches(
            shellText,
            @"\{StaticResource\s+(?:Accent|Ai|Background|Border|Bullish|Danger|Gradient|Highlight|Text|Warning)\.");
        themeFacingStaticReferences.Should().BeEmpty();

        File.ReadAllText(Fixture("Controls.axaml"))
            .Should().NotContain("{StaticResource ");

        var support = File.ReadAllText(Fixture("SupportWindow.axaml"));
        support.Should().Contain("Write to the developer");
        support.Should().Contain("{Binding DonateMessage}");
        support.Should().Contain("Content=\"Send to developer\"");
    }

    [Fact]
    public void Broker_status_describes_market_data_connectivity_not_live_execution()
    {
        XNamespace av = "https://github.com/avaloniaui";
        var shell = XDocument.Load(Fixture("MainWindow.axaml"));

        var connectedBrokerCount = shell.Descendants(av + "TextBlock").Single(element =>
            (string?)element.Attribute("Text") == "{Binding ConnectedBrokerCount}");
        var statusParts = connectedBrokerCount.Parent!
            .Elements(av + "TextBlock")
            .Select(element => (string?)element.Attribute("Text"))
            .ToArray();

        statusParts.Should().Equal(
            "{Binding ConnectedBrokerCount}",
            " market-data brokers connected");
        shell.Descendants(av + "TextBlock").Should().NotContain(element =>
            (string?)element.Attribute("Text") == "LIVE ");
    }

    [Fact]
    public void Login_surfaces_keep_windows_geometry_and_state_visuals()
    {
        XNamespace av = "https://github.com/avaloniaui";

        var accountText = File.ReadAllText(Fixture("AccountGateWindow.axaml"));
        var account = XDocument.Parse(accountText).Root!;
        account.Attribute("Width")!.Value.Should().Be("760");
        account.Attribute("Height")!.Value.Should().Be("540");
        accountText.Should().Contain("RowDefinitions=\"Auto,Auto\"");
        accountText.Should().Contain("TextWrapping=\"Wrap\"");

        var loginText = File.ReadAllText(Fixture("LoginWindow.axaml"));
        var login = XDocument.Parse(loginText).Root!;
        login.Attribute("Width")!.Value.Should().Be("960");
        login.Attribute("Height")!.Value.Should().Be("700");
        login.Attribute("Icon")!.Value.Should().EndWith("/Icon/DaxAlgoLogo.png");
        login.Descendants(av + "Grid").First().Attribute("ColumnDefinitions")!.Value.Should().Be("280,*");
        login.Descendants(av + "TextBlock").Should().Contain(element =>
            (string?)element.Attribute("Text") == "Sign in");
        loginText.Should().Contain("Classes.connected=\"{Binding IsConnected}\"");
        loginText.Should().Contain("Classes.busy=\"{Binding IsConnecting}\"");
        loginText.Should().Contain("Converter={StaticResource BrokerLogo}");
        loginText.Should().Contain("x:Name=\"servicesToggle\"");
        loginText.Should().Contain("IsDefault=\"True\"");
        loginText.Should().Contain("IsCancel=\"True\"");
        loginText.Should().NotContain("Connect market data");
        loginText.Should().NotContain("Apple Silicon + Intel");
    }

    [Fact]
    public void Vibe_quant_matches_the_three_region_windows_contract()
    {
        XNamespace av = "https://github.com/avaloniaui";
        var text = File.ReadAllText(Fixture("StrategyAuthoringWindow.axaml"));
        var root = XDocument.Parse(text).Root!;

        root.Attribute("Width")!.Value.Should().Be("1100");
        root.Attribute("Height")!.Value.Should().Be("760");
        root.Descendants(av + "Grid").Should().Contain(element =>
            (string?)element.Attribute("ColumnDefinitions") == "Auto,*,4,390");
        text.Should().Contain("SIMULATED DATA — not a live feed");
        text.Should().Contain("IsVisible=\"{Binding HasConversation}\"");
        text.Should().Contain("No AI provider is set up yet.");
        text.Should().Contain("<Button.Flyout>");
        root.Descendants(av + "TabItem").Select(item => item.Attribute("Header")?.Value)
            .Should().Equal("Code", "Parameters", "Activity", "{Binding CandidateTabHeader}");
        text.Should().Contain("Content=\"{Binding GenerationModeActionText}\"");
        text.Should().Contain("Command=\"{Binding ToggleGenerationModeCommand}\"");
        text.Should().Contain("ItemsSource=\"{Binding CandidateGroups}\"");
        text.Should().Contain("ItemsSource=\"{Binding CandidateBuildSupport}\"");
        text.Should().Contain("Command=\"{Binding ConfirmCandidateCommand}\"");
        text.Should().Contain("CANDIDATE OUTCOME");
        text.Should().Contain("ItemsSource=\"{Binding GeneratedCandidateOptions}\"");
        text.Should().Contain("SelectedItem=\"{Binding SelectedGeneratedCandidateOption, Mode=TwoWay}\"");
        text.Should().Contain("Command=\"{Binding ChooseGeneratedCandidateCommand}\"");
        text.Should().Contain("Command=\"{Binding RevalidateGeneratedCandidateCommand}\"");
        text.Should().Contain("Text=\"ARTIFACT PREVIEW\"");
        text.Should().Contain("SelectedGeneratedCandidateOption.ArtifactPreview");
        text.Should().Contain("Four AI generation agents:");
        text.Should().Contain("Every structurally valid artifact can be chosen;");
        text.Should().Contain("package validation is reported separately");
        text.Should().Contain("choosing never tests or runs it");
        text.Should().NotContain("Only artifacts that pass a registered package validator can be chosen.");
        text.Should().NotContain("Unsupported lanes explain the missing contract");
        text.Should().Contain("AuthoringBoundaryText");
        text.Should().Contain("IsEnabled=\"{Binding !IsGenerating}\"");
        text.Should().Contain("ItemsSource=\"{Binding Diagnostics}\"");
        text.Should().NotContain("Header=\"Diagnostics\"");
    }

    [Fact]
    public void Shipped_appsettings_contains_public_identity_but_no_credentials()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Fixture("appsettings.json")));
        var root = document.RootElement;

        var google = root.GetProperty("GoogleAuth");
        google.GetProperty("ClientId").GetString().Should().NotBeNullOrWhiteSpace();
        google.TryGetProperty("ClientSecret", out _).Should().BeFalse();

        ReadString(root, "Alpaca", "ApiKey").Should().BeEmpty();
        ReadString(root, "Alpaca", "ApiSecret").Should().BeEmpty();
        ReadString(root, "IronBeam", "ApiKey").Should().BeEmpty();
        ReadString(root, "LondonStrategicEdge", "ApiKey").Should().BeEmpty();
        ReadString(root, "Upstox", "ApiKey").Should().BeEmpty();
        ReadString(root, "Upstox", "ApiSecret").Should().BeEmpty();
        ReadString(root, "Upstox", "AccessToken").Should().BeEmpty();
        ReadString(root, "TelegramArchive", "ApiHash").Should().BeEmpty();
        root.GetProperty("Notifications").GetProperty("Telegram")
            .GetProperty("BotToken").GetString().Should().BeEmpty();

        var plugins = root.GetProperty("Plugins");
        plugins.GetProperty("TrustPolicy").GetString().Should().Be("Curated");
        plugins.GetProperty("FeedPublicKey").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Bundle_metadata_and_script_preserve_signing_and_layout_invariants()
    {
        var info = ReadPlist(Fixture("Info.plist"));
        info["CFBundleExecutable"].Value.Should().Be("DaxAlgoTerminal");
        info["CFBundleIdentifier"].Value.Should().Be("com.daxalgo.terminal");
        info["CFBundlePackageType"].Value.Should().Be("APPL");
        info["LSMinimumSystemVersion"].Value.Should().Be("12.0");

        var entitlements = ReadPlist(Fixture("DaxAlgoTerminal.entitlements"));
        entitlements["com.apple.security.cs.allow-jit"].Name.LocalName.Should().Be("true");
        entitlements["com.apple.security.cs.disable-library-validation"].Name.LocalName.Should().Be("true");

        var script = File.ReadAllText(Fixture("package.sh"));
        script.Should().Contain("set -euo pipefail");
        script.Should().Contain("osx-arm64|osx-x64");
        script.Should().Contain("--runtime \"$RID\"");
        script.Should().Contain("--self-contained true");
        script.Should().Contain("cp -R \"$PUBLISH_DIR\"/. \"$APP_MACOS\"/");
        script.Should().Contain("find \"$APP_MACOS\" -type f");
        script.Should().Contain("--entitlements \"$MACOS_DIR/DaxAlgoTerminal.entitlements\"");
        script.Should().Contain("codesign --verify --deep --strict");
        script.Should().Contain("notarytool submit");
        script.Should().NotContain("DaxAlgo-Terminal-Pro");
    }

    private static XElement FindNone(XDocument project, string update) =>
        project.Descendants("None").Single(element =>
            string.Equals(element.Attribute("Update")?.Value, update, StringComparison.Ordinal));

    private static string ReadString(JsonElement root, string section, string property) =>
        root.GetProperty(section).GetProperty(property).GetString() ?? string.Empty;

    private static Dictionary<string, XElement> ReadPlist(string path)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore };
        using var reader = XmlReader.Create(path, settings);
        var document = XDocument.Load(reader);
        var elements = document.Root!.Element("dict")!.Elements().ToList();
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);

        for (var index = 0; index < elements.Count - 1; index++)
        {
            if (elements[index].Name.LocalName != "key") continue;
            result[elements[index].Value] = elements[++index];
        }

        return result;
    }

    private static string Fixture(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
