using Avalonia.Controls;
using DaxAlgo.Sdk;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Time;
using TradingTerminal.Sandbox;
using TradingTerminal.UI.Avalonia.Controls.Render;
using TradingTerminal.UI.Controls.Render;
using TradingTerminal.UI.Logging;
using TradingTerminal.UI.Strategies;

namespace TradingTerminal.App.Avalonia.Harness;

/// <summary>
/// One observe-only shell around a frozen authored unit:
/// instrument (required) + interactive graph + UI parameter chrome + live/broker feed + bar data.
/// Does not place Paper or live orders — visualizer path only.
/// </summary>
public static class UnitHarnessShell
{
    public static async Task<(Window Window, AuthoredUnitHost Host, SandboxVisualizerRuntime Runtime)> OpenVisualizerAsync(
        VisualizerRegistration registration,
        IMarketDataHub hub,
        IClock clock,
        InMemoryLogSink log,
        IMarketDataIngest ingest,
        IInstrumentRegistry instruments,
        IBrokerSelector brokers,
        Window? owner = null)
    {
        ArgumentNullException.ThrowIfNull(registration);
        var specification = registration.AuthoredSpecification
            ?? throw new InvalidOperationException(
                "Harness open/run requires a frozen AuthoredUnitSpecification (instrument + timeframe + drawing).");

        if (specification.Instruments.Count == 0 ||
            specification.Instruments.Any(static item => item.InstrumentId.IsNone))
        {
            throw new InvalidOperationException(
                "Harness cannot launch without at least one resolved instrument.");
        }

        var issues = AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification);
        if (issues.Count > 0)
            throw new InvalidOperationException($"Harness launch blocked: {issues[0].Message}");

        return await AuthoredVisualizerSession.OpenAsync(
            title: registration.Descriptor.DisplayName,
            visualizerFactory: registration.Create,
            hub: hub,
            clock: clock,
            log: log,
            specification: specification,
            ingest: ingest,
            instrumentRegistry: instruments,
            brokerSelector: brokers,
            owner: owner).ConfigureAwait(true);
    }
}
