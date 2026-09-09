using Avalonia.Controls;
using TradingTerminal.App.Avalonia.Execution;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Time;
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
    /// Opens the strategy harness: Paper Strategy Runner bound to the frozen kernel registration.
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
            Title = $"Harness · {registration.DisplayName}",
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
}
