using Avalonia.Controls;
using TradingTerminal.App.Avalonia.Execution;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Time;
using TradingTerminal.ExecutionUi;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.UI.Avalonia.Controls.Render;
using TradingTerminal.UI.Logging;
using TradingTerminal.UI.Strategies;

namespace TradingTerminal.App.Avalonia.Harness;

/// <summary>
/// One product shell around a frozen authored unit: visualizers use
/// <see cref="AuthoredVisualizerSession"/>; strategies open Paper Strategy Runner
/// (OMS book qty) through this harness entry instead of ad-hoc shell branches.
/// </summary>
public static class AuthoredUnitHarnessSession
{
    public static Task OpenVisualizerAsync(
        string title,
        Func<DaxAlgo.Sdk.IVisualizer> visualizerFactory,
        IMarketDataHub hub,
        IClock clock,
        InMemoryLogSink log,
        AuthoredUnitSpecificationV1? specification = null,
        IMarketDataIngest? ingest = null,
        IInstrumentRegistry? instrumentRegistry = null,
        IBrokerSelector? brokerSelector = null,
        Window? owner = null)
    {
        return AuthoredVisualizerSession.OpenAsync(
            title,
            visualizerFactory,
            hub,
            clock,
            log,
            specification: specification,
            ingest: ingest,
            instrumentRegistry: instrumentRegistry,
            brokerSelector: brokerSelector,
            owner: owner);
    }

    /// <summary>
    /// Opens the strategy harness on a leased Paper (or broker-Paper) book session.
    /// Caller owns lease disposal when the window closes.
    /// </summary>
    public static PaperStrategyRunnerWindow OpenStrategy(
        StrategyKernelRegistration? initialStrategy,
        IBacktestStrategyRegistry strategyRegistry,
        IMarketDataHub hub,
        IClock clock,
        InMemoryLogSink log,
        PaperExecutionBookSessionLease bookLease,
        IInstrumentRegistry instruments,
        IStrategyKernelRegistry? strategyKernelRegistry = null,
        IMarketDataIngest? marketDataIngest = null,
        IBrokerSelector? brokerSelector = null,
        IReadOnlyDictionary<string, object?>? testedParameters = null,
        IExecutionClient? executionClient = null,
        bool autoStart = false,
        Window? owner = null,
        PaperStrategyRunnerWindow? window = null)
    {
        ArgumentNullException.ThrowIfNull(bookLease);

        var viewModel = new PaperStrategyRunnerViewModel(
            strategyRegistry,
            hub,
            clock,
            log,
            bookLease.Session,
            instruments,
            bookLease.Book,
            strategyKernelRegistry,
            marketDataIngest,
            brokerSelector,
            initialStrategy,
            testedParameters,
            executionClient);

        window ??= new PaperStrategyRunnerWindow();
        window.Title = FormatPaperTitle(bookLease.Book.Name, initialStrategy?.DisplayName);
        window.DataContext = viewModel;
        if (owner is not null)
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        return window;
    }

    /// <summary>
    /// Opens the strategy harness: Paper Strategy Runner bound to the frozen kernel registration
    /// on a desktop Paper session (no book lease).
    /// </summary>
    public static PaperStrategyRunnerWindow OpenStrategy(
        StrategyKernelRegistration registration,
        IBacktestStrategyRegistry strategyRegistry,
        IMarketDataHub hub,
        IClock clock,
        InMemoryLogSink log,
        PaperExecutionDesktopSession paper,
        IInstrumentRegistry instruments,
        IStrategyKernelRegistry? strategyKernelRegistry = null,
        IMarketDataIngest? marketDataIngest = null,
        IBrokerSelector? brokerSelector = null,
        IReadOnlyDictionary<string, object?>? testedParameters = null,
        bool autoStart = false,
        Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var viewModel = new PaperStrategyRunnerViewModel(
            strategyRegistry,
            hub,
            clock,
            log,
            paper,
            instruments,
            strategyKernelRegistry: strategyKernelRegistry,
            marketDataIngest: marketDataIngest,
            brokerSelector: brokerSelector,
            initialStrategy: registration);

        if (testedParameters is not null)
            viewModel.TryPrepareTestedStrategy(registration, testedParameters, out _);

        var window = new PaperStrategyRunnerWindow
        {
            Title = FormatPaperTitle(bookName: null, registration.DisplayName),
            DataContext = viewModel,
        };
        if (owner is not null)
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        window.Closed += (_, _) => viewModel.Dispose();
        if (owner is null) window.Show();
        else window.Show(owner);

        if (autoStart)
            _ = viewModel.StartCommand.ExecuteAsync(null);

        return window;
    }

    /// <summary>One titled continuous session: Gate/Validate → Paper Runner.</summary>
    public static string FormatPaperTitle(string? bookName, string? strategyDisplayName)
    {
        var strategy = string.IsNullOrWhiteSpace(strategyDisplayName) ? "Strategy" : strategyDisplayName.Trim();
        return string.IsNullOrWhiteSpace(bookName)
            ? $"Harness · Paper · {strategy}"
            : $"Harness · Paper · {bookName.Trim()} · {strategy}";
    }
}
