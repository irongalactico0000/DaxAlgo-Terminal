using Avalonia.Controls;
using DaxAlgo.Sdk;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Core.Time;
using TradingTerminal.Sandbox;
using TradingTerminal.UI.Controls.Render;
using TradingTerminal.UI.Logging;

namespace TradingTerminal.UI.Avalonia.Controls.Render;

/// <summary>
/// Opens one sandboxed visualizer in Avalonia chrome the same way Windows
/// <c>AddVisualizerToChart</c> does: <see cref="SandboxVisualizerRuntime.TryDraw"/> →
/// <see cref="AuthoredUnitHost"/> → <see cref="AuthoredUnitView"/>.
/// </summary>
public static class AuthoredVisualizerSession
{
    public static async Task<(Window Window, AuthoredUnitHost Host, SandboxVisualizerRuntime Runtime)> OpenAsync(
        string title,
        Func<IVisualizer> visualizerFactory,
        IMarketDataHub hub,
        IClock clock,
        InMemoryLogSink log,
        Action<AlertRecord>? showBanner = null,
        IReadOnlyDictionary<string, object?>? currentValues = null,
        AuthoredUnitSpecificationV1? specification = null,
        IMarketDataIngest? ingest = null,
        IInstrumentRegistry? instrumentRegistry = null,
        IBrokerSelector? brokerSelector = null,
        Window? owner = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentNullException.ThrowIfNull(visualizerFactory);
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(log);

        AuthoredUnitFeedLease? feedLease = null;
        IReadOnlySet<InstrumentId>? authorizedInstruments = null;
        if (specification is not null)
        {
            var issues = AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification);
            if (issues.Count > 0)
                throw new InvalidOperationException($"The authored visualizer specification is not launch-valid: {issues[0].Message}");
            if (specification.Kind != AuthoredUnitKindV1.Visualizer)
                throw new InvalidOperationException("A strategy specification cannot be opened as a visualizer.");
            ArgumentNullException.ThrowIfNull(ingest);
            ArgumentNullException.ThrowIfNull(instrumentRegistry);
            ArgumentNullException.ThrowIfNull(brokerSelector);

            feedLease = AuthoredUnitFeedLease.Acquire(
                specification,
                ingest,
                instrumentRegistry,
                brokerSelector,
                AuthoredUnitKindV1.Visualizer);
            authorizedInstruments = feedLease.AuthorizedInstruments;
        }

        StrategyParameterSchema? schema = null;
        try
        {
            schema = visualizerFactory().Schema;
        }
        catch
        {
            // Schema is optional chrome; a construct failure is reported when StartAsync runs.
        }

        var runtime = new SandboxVisualizerRuntime(
            visualizerFactory,
            currentValues,
            hub,
            clock,
            log.Append,
            showBanner ?? (alert => log.Append(alert.Source, alert.Level.ToString(), alert.Message)),
            hostAuthorizedInstruments: authorizedInstruments);

        var host = new AuthoredUnitHost(title, runtime.TryDraw, schema, currentValues, log);
        var view = new AuthoredUnitView { DataContext = host.Presenter };
        var window = new Window
        {
            Title = title,
            Width = 960,
            Height = 640,
            Content = view,
        };
        if (owner is not null)
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;

        window.Closed += async (_, _) =>
        {
            host.Dispose();
            view.Dispose();
            await runtime.DisposeAsync();
            feedLease?.Dispose();
        };

        if (owner is null)
            window.Show();
        else
            window.Show(owner);

        try
        {
            await runtime.StartAsync();
        }
        catch
        {
            host.Freeze();
            feedLease?.Dispose();
            throw;
        }

        return (window, host, runtime);
    }

}

/// <summary>
/// Owns the broker-ingest references for one authored Visualizer or Strategy runtime. Acquisition is all-or-none:
/// an unavailable broker, disconnected session, unsupported channel, stale canonical instrument, or
/// missing broker alias prevents the runtime from starting and releases any earlier subscriptions.
/// </summary>
public sealed class AuthoredUnitFeedLease : IDisposable
{
    private readonly List<IDisposable> _handles;
    private int _disposed;

    private AuthoredUnitFeedLease(
        IReadOnlySet<InstrumentId> authorizedInstruments,
        List<IDisposable> handles)
    {
        AuthorizedInstruments = authorizedInstruments;
        _handles = handles;
    }

    /// <summary>The exact canonical instruments the runtime may observe for this window.</summary>
    public IReadOnlySet<InstrumentId> AuthorizedInstruments { get; }

    public static AuthoredUnitFeedLease Acquire(
        AuthoredUnitSpecificationV1 specification,
        IMarketDataIngest ingest,
        IInstrumentRegistry registry,
        IBrokerSelector selector,
        AuthoredUnitKindV1 requiredKind)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(ingest);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(selector);

        var issues = AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification);
        if (issues.Count > 0)
            throw new InvalidOperationException(
                $"The authored visualizer specification is not launch-valid: {issues[0].Message}");
        if (specification.Kind != requiredKind)
            throw new InvalidOperationException(
                $"A {specification.Kind} specification cannot acquire a {requiredKind} runtime feed.");

        var handles = new List<IDisposable>();
        var authorizedInstruments = specification.Instruments
            .Select(static instrument => instrument.InstrumentId)
            .ToHashSet();
        // A Paper target needs a fresh exact bid/ask reference for risk and simulated execution even
        // when the authored kernel itself is Bars-only. This host-owned L1 stream is not added to the
        // kernel's ScopedMarketDataView, so acquisition does not broaden authored-code authority.
        var feedRequirement = specification.DataRequirement;
        if (requiredKind == AuthoredUnitKindV1.Strategy &&
            specification.ExecutionIntent == AuthoredUnitExecutionIntentV1.PaperTargets)
        {
            feedRequirement |= StrategyDataRequirement.L1;
        }
        var barSize = specification.DataRequirement.HasFlag(StrategyDataRequirement.Bars)
            ? ResolveBarSize(specification.Timeframe.BarSize!.Value)
            : (BarSize?)null;

        try
        {
            foreach (var requested in specification.Instruments)
            {
                var broker = requested.PreferredBroker ?? throw new InvalidOperationException(
                    $"Instrument '{requested.UserText}' has no selected broker feed.");
                if (!selector.IsAvailable(broker))
                    throw new InvalidOperationException($"{broker} is not available in this build.");
                if (!selector.IsConnected(broker))
                    throw new InvalidOperationException($"Connect {broker} before starting this {requiredKind.ToString().ToLowerInvariant()}.");

                var client = selector.Get(broker);
                var capabilities = client.MarketDataCapabilities;
                RequireCapabilities(feedRequirement, capabilities, broker);

                var instrument = registry.Get(requested.InstrumentId) ?? throw new InvalidOperationException(
                    $"Canonical instrument {requested.InstrumentId.Value} is no longer registered.");
                var brokerSymbol = registry.ToBrokerSymbol(requested.InstrumentId, broker) ?? throw new InvalidOperationException(
                    $"{instrument.CanonicalSymbol} has no registered {broker} subscription symbol.");
                var contract = new Contract(
                    brokerSymbol,
                    SecTypeFor(instrument.AssetClass),
                    instrument.Exchange,
                    instrument.Currency,
                    instrument.Exchange);

                if (feedRequirement.HasFlag(StrategyDataRequirement.L1) ||
                    feedRequirement.HasFlag(StrategyDataRequirement.Depth))
                    handles.Add(ingest.Subscribe(contract, broker));
                if (barSize is { } size)
                    handles.Add(ingest.SubscribeBars(contract, broker, size));
                if (specification.DataRequirement.HasFlag(StrategyDataRequirement.TradeTape))
                    handles.Add(ingest.SubscribeTrades(contract, broker));
            }

            return new AuthoredUnitFeedLease(authorizedInstruments, handles);
        }
        catch
        {
            DisposeFeeds(handles);
            throw;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;
        DisposeFeeds(_handles);
    }

    private static void RequireCapabilities(
        StrategyDataRequirement requirement,
        MarketDataCapabilities capabilities,
        BrokerKind broker)
    {
        if (requirement.HasFlag(StrategyDataRequirement.L1) && !capabilities.SupportsLevel1Quotes)
            throw new NotSupportedException($"{broker} does not provide the requested L1 quote stream.");
        if (requirement.HasFlag(StrategyDataRequirement.Bars) && !capabilities.SupportsLiveBars)
            throw new NotSupportedException($"{broker} does not provide the requested live-bar stream.");
        if (requirement.HasFlag(StrategyDataRequirement.Depth) && !capabilities.SupportsLevel2Depth)
            throw new NotSupportedException($"{broker} does not provide the requested L2 order book.");
        if (requirement.HasFlag(StrategyDataRequirement.TradeTape) && !capabilities.SupportsLiveTrades)
            throw new NotSupportedException($"{broker} does not provide the requested live trade tape.");
    }

    private static BarSize ResolveBarSize(TimeSpan interval)
    {
        foreach (var candidate in Enum.GetValues<BarSize>())
            if (candidate.ToTimeSpan() == interval)
                return candidate;
        throw new NotSupportedException($"Live authored units do not support bar interval '{interval}'.");
    }

    private static string SecTypeFor(AssetClass assetClass) => assetClass switch
    {
        AssetClass.Future => "FUT",
        AssetClass.Forex => "CASH",
        AssetClass.Crypto => "CRYPTO",
        AssetClass.Option => "OPT",
        AssetClass.Index => "IND",
        _ => "STK",
    };

    private static void DisposeFeeds(ICollection<IDisposable> handles)
    {
        foreach (var handle in handles.Reverse())
        {
            try { handle.Dispose(); }
            catch { }
        }
        handles.Clear();
    }
}
