using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FluentAssertions;
using TradingTerminal.App.Avalonia.Execution;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class PaperExecutionConsoleSurfaceTests
{
    [Fact]
    public void Shell_exposes_a_truthful_Paper_only_execution_entry()
    {
        XNamespace av = "https://github.com/avaloniaui";
        var shell = XDocument.Load(Fixture("MainWindow.axaml"));

        shell.Descendants(av + "MenuItem").Should().Contain(element =>
            (string?)element.Attribute("Header") == "_Paper Execution Console…" &&
            (string?)element.Attribute("Click") == "OnExecutionConsole");
        shell.Descendants(av + "TextBlock").Should().Contain(element =>
            (string?)element.Attribute("Text") == "PAPER EXECUTION ONLY");
        shell.Descendants(av + "MenuItem").Should().Contain(element =>
            (string?)element.Attribute("Header") == "Paper Execution _Books…" &&
            (string?)element.Attribute("Click") == "OnExecutionBooks");
    }

    [Fact]
    public void Book_manager_surface_exposes_persistence_selection_and_safety_actions()
    {
        var text = File.ReadAllText(Fixture("PaperExecutionBooksWindow.axaml"));
        foreach (var required in new[]
                 {
                     "PAPER EXECUTION BOOKS", "CREATE PAPER BOOK", "Simulated account ID",
                     "Opening capital (SIM)",
                     "Select for new windows", "Run / stop selected", "Remove definition",
                     "Each book owns one simulated account", "never deletes its ledger",
                     "No live broker order adapter is loaded"
                 })
            text.Should().Contain(required);
    }

    [Fact]
    public void Console_declares_every_operational_surface_without_a_live_selector()
    {
        var text = File.ReadAllText(Fixture("PaperExecutionConsoleWindow.axaml"));

        foreach (var required in new[]
                 {
                     "Submit to Paper OMS", "Replace selected", "Cancel selected", "Reconcile",
                     "KillActionText", "Orders", "Positions", "Fills", "Cash", "Immutable history",
                     "ModeText", "Reconciliation", "LOCAL LEDGER", "VENUE SNAPSHOT",
                     "Append durable resolution", "never deletes or rewrites the original mismatch",
                     "Risk decisions", "COMMAND PAYLOAD HASH", "LIMITS HASH",
                     "Historical evidence is immutable and is not recalculated using current settings",
                     "Execution quality", "FILL RATE", "AVG SLIPPAGE", "REJECT RATE", "AVG ACK",
                     "Slippage is reported as n/a", "Fill prices are never relabelled as slippage evidence",
                     "Portfolio analytics", "OPENING CAPITAL", "MARKED EQUITY", "REALIZED P&amp;L",
                     "PERFORMANCE BY RANGE", "CLOSED FIFO LOTS", "Latest ledger fill",
                     "period equity series is realized-only"
                 })
            text.Should().Contain(required);
        text.Should().NotContain("LiveExecution");
        text.Should().NotContain("IBrokerExecutionAdapter");
    }

    [Fact]
    public void Shell_and_runner_expose_explicit_multi_asset_authenticated_Paper_behavior()
    {
        XNamespace av = "https://github.com/avaloniaui";
        var shell = XDocument.Load(Fixture("MainWindow.axaml"));
        shell.Descendants(av + "MenuItem").Should().Contain(element =>
            (string?)element.Attribute("Header") == "Paper _Strategy Runner…" &&
            (string?)element.Attribute("Click") == "OnPaperStrategyRunner");

        var text = File.ReadAllText(Fixture("PaperStrategyRunnerWindow.axaml"));
        foreach (var required in new[]
                 {
                     "PAPER STRATEGY RUNNER", "PAPER ONLY · AUTHENTICATED IPC",
                     "CANONICAL ASSET SET", "every reviewed leg and feed",
                     "not represented as an atomic exchange order", "STRATEGY LEGS · MODEL TARGETS",
                     "STRATEGY-ASSET VISUALIZATION", "ASSET BINDING READY",
                     "never inferred from a strategy name or typed prose",
                     "Retry target", "No live broker order adapter is loaded"
                 })
            text.Should().Contain(required);
        text.Should().NotContain("AllowLiveExecution");
        text.Should().NotContain("IBrokerExecutionAdapter");
    }

    [Fact]
    public void Unavailable_surface_says_no_order_was_sent_and_remains_fail_closed()
    {
        var text = File.ReadAllText(Fixture("PaperExecutionUnavailableWindow.axaml"));

        text.Should().Contain("PAPER EXECUTION UNAVAILABLE");
        text.Should().Contain("No order was sent");
        text.Should().Contain("fail-closed");
        text.Should().Contain("Market-data connectivity does not bypass this gate");
    }

    [AvaloniaFact]
    public void Full_console_window_loads_measures_and_renders_headlessly()
    {
        var window = new PaperExecutionConsoleWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var frame = window.CaptureRenderedFrame();

            frame.Should().NotBeNull();
            window.Bounds.Width.Should().BeGreaterThan(1100);
            window.Bounds.Height.Should().BeGreaterThan(650);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Unavailable_window_loads_and_renders_the_exact_failure_reason()
    {
        var window = new PaperExecutionUnavailableWindow("Keychain access denied by the test.");
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            window.CaptureRenderedFrame().Should().NotBeNull();
            window.FindControl<TextBlock>("ReasonText")!.Text.Should().Be("Keychain access denied by the test.");
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paper_strategy_runner_loads_measures_and_renders_headlessly()
    {
        var window = new PaperStrategyRunnerWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            window.CaptureRenderedFrame().Should().NotBeNull();
            window.Bounds.Width.Should().BeGreaterThan(1000);
            window.Bounds.Height.Should().BeGreaterThan(650);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Paper_execution_books_window_loads_measures_and_renders_headlessly()
    {
        var window = new PaperExecutionBooksWindow();
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();

            window.CaptureRenderedFrame().Should().NotBeNull();
            window.Bounds.Width.Should().BeGreaterThan(900);
            window.Bounds.Height.Should().BeGreaterThan(580);
        }
        finally
        {
            window.Close();
        }
    }

    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
}
