using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading.Channels;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Notifications;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.UI.Presets;

namespace TradingTerminal.UI;

/// <summary>
/// Base view-model for "live signal mode" hosts. Each per-strategy project subclasses
/// this and overrides <see cref="BuildStrategy"/> to instantiate its underlying
/// <see cref="IBacktestStrategy"/> with strategy-specific parameters. The base owns the
/// quote + depth subscriptions, the synthetic order router, the price-bar aggregation used for
/// chart visualisation, and notification publishing.
///
/// Streaming routes through the canonical market-data pipeline: a single
/// <see cref="IMarketDataIngest.Subscribe"/> handle starts (or joins) the ref-counted broker
/// L1 pump for the selected instrument, and this VM observes <see cref="IMarketDataHub.Quotes"/>
/// / <see cref="IMarketDataHub.Depth"/> keyed by canonical <see cref="InstrumentId"/>. Quote
/// records are projected to the legacy <see cref="Tick"/> shape at the boundary so the
/// engine-side <see cref="IBacktestStrategy"/> contract stays unchanged. On start, the chart is
/// warmed from the local <see cref="IMarketDataStore"/> so users see context immediately even
/// before the first live tick lands.
///
/// The host flow mirrors RSI / Cumulative Delta: user fills the setup form → presses
/// Continue (<see cref="IsConfigured"/> flips) → tick stream starts and price bars render
/// → optional Run/Stop arms the algo (<see cref="IsAlgoRunning"/>); notifications are
/// suppressed when not armed.
/// </summary>
public abstract partial class LiveSignalStrategyViewModelBase : ViewModelBase, IDisposable
{
    public const int MaxSignalsRetained = 200;
    public const int MaxBarsRetained = 300;

    /// <summary>Cap on how many instruments the picker shows at once. The broker universe can be
    /// ~11k symbols (Alpaca); the search box narrows it, so we never bind the whole list.</summary>
    public const int MaxInstrumentsDisplayed = 500;

    /// <summary>Bar size pulled from the store to pre-populate the chart at start. Mismatches the
    /// 15-second live aggregation by design — the store doesn't carry sub-minute bars, and a
    /// minute of recent context is more useful than no context at all.</summary>
    private const BarSize WarmupBarSize = BarSize.OneMinute;

    /// <summary>How many warm-up bars to pull from the store on Start. Subclasses override to
    /// scale the warm-up to their analysis window (e.g. the APEX scalper pulls
    /// <c>max(WindowSize, MaxChartCandles)</c> so the per-indicator charts have immediate context).</summary>
    protected virtual int WarmupBarCount => 120;

    private readonly LiveStrategyHostServices _services;
    private readonly INotificationPublisher _notifications;
    private readonly ILogger _logger;
    private readonly IClock _clock;
    private readonly ISignalGeneratorRouterFactory _routerFactory;

    private CancellationTokenSource? _streamCts;
    private SignalGeneratorRouter? _router;
    private IBacktestStrategy? _strategy;
    private IDisposable? _eventSubscription;
    private IDisposable? _ingestHandle;
    private IDisposable? _tradeIngestHandle;
    private int _disposeState;
    private DateTime _currentBarStart = DateTime.MinValue;
    private double _barOpen, _barHigh, _barLow, _barClose;
    private long _barVolume;
    private static readonly TimeSpan BarInterval = TimeSpan.FromSeconds(15);

    // Hub pumps use bounded, DropOldest channels (not unbounded) so a fast feed the UI can't drain
    // is capped in memory — the newest event always wins — instead of piling the backlog into GBs
    // over a long session. Each pump batch-drains: one UI-thread marshal per drained batch rather
    // than one per event, so the consumer keeps pace and the channel rarely has to shed.
    private const int QuoteChannelCapacity = 16_384;
    private const int DepthChannelCapacity = 2_048;
    private const int TradeChannelCapacity = 65_536;
    private const int MaxStreamDrainBatch = 4_096;
    private const StrategyDataRequirement KnownDataRequirements =
        StrategyDataRequirement.L1 |
        StrategyDataRequirement.Bars |
        StrategyDataRequirement.Depth |
        StrategyDataRequirement.TradeTape;

    protected LiveSignalStrategyViewModelBase(
        string strategyId,
        string strategyDisplayName,
        LiveStrategyHostServices services,
        INotificationPublisher notifications,
        IClock clock,
        ISignalGeneratorRouterFactory routerFactory,
        ILogger logger)
    {
        StrategyId = strategyId;
        StrategyDisplayName = strategyDisplayName;
        _services = services;
        _notifications = notifications;
        _clock = clock;
        _routerFactory = routerFactory;
        _logger = logger;

        _presetStore = new ToolPresetStore<StrategyViewPreset>($"strategy-{strategyId}");
        PresetNames = new ObservableCollection<string>(_presetStore.Names);

        // Seed from the canonical instrument registry (no hardcoded catalog). Instrument-discovery
        // fills the registry the moment a broker connects, so if any broker is already up this is the
        // real, discovered universe; if none is up yet it's empty and the picker fills on connect.
        AllInstruments = RegistryRows();
        // Hide-until-search: start with an empty visible list (ApplyInstrumentFilter collapses it to
        // just the selection) and restore the instrument last used in this strategy window, falling
        // back to the old SPY default only on first-ever use. See InstrumentPickerFilter.
        Instruments = new ObservableCollection<SignalInstrument>();
        SelectedInstrument = InstrumentPickerFilter.InitialSelection(StrategyId, AllInstruments,
            () => AllInstruments.FirstOrDefault(i => i.Contract.Symbol == "SPY") ?? AllInstruments.FirstOrDefault());
        ApplyInstrumentFilter();
        Signals = new ObservableCollection<SignalEntry>();
        Bars = new ObservableCollection<Bar>();

        // Refine with the connected brokers' own (broker-tagged) lists where available.
        // Fire-and-forget: the continuation resumes on the UI context (VM is built there).
        _ = LoadInstrumentsAsync();

        // Reload the picker whenever a broker reaches Connected. The window often opens *before*
        // a broker has finished connecting (e.g. IB + 2FA, or a broker connected from the shell
        // after entry), and the static-catalog fallback would otherwise be pinned for the VM's
        // life — the user would never see their broker's instruments without reopening. This
        // refreshes the list the moment a broker comes up. Unsubscribed in Dispose.
        _services.Selector.StateChanged += OnBrokerStateChanged;
    }

    /// <summary>Refreshes the instrument picker when any broker reaches
    /// <see cref="ConnectionState.Connected"/>, so a broker that connects after the window opened
    /// populates the dropdown without a reopen. No-op once the user has configured the strategy
    /// (the picker is locked past that point).</summary>
    private void OnBrokerStateChanged(object? sender, BrokerStateChangedEventArgs e)
    {
        if (e.State != ConnectionState.Connected || IsConfigured) return;
        _ = UiThread.RunAsync(() => LoadInstrumentsAsync());
    }

    public string StrategyId { get; }
    public string StrategyDisplayName { get; }

    /// <summary>Full tradable universe from the connected broker (or the static fallback);
    /// <see cref="InstrumentSearchText"/> filters this into <see cref="Instruments"/>.</summary>
    public IReadOnlyList<SignalInstrument> AllInstruments { get; private set; }

    public ObservableCollection<SignalEntry> Signals { get; }

    /// <summary>15-second price bars derived from the live tick stream. Used for chart drawing.</summary>
    public ObservableCollection<Bar> Bars { get; }

    public event EventHandler? BarsChanged;

    /// <summary>Fires after every live quote tick has been pushed through the strategy. Charts
    /// that need per-tick refresh (rather than per-bar) subscribe here and coalesce their
    /// redraws — see the Apex window's DispatcherTimer-throttled redraw loop. Fired on the UI
    /// thread.</summary>
    public event EventHandler? TickProcessed;

    /// <summary>Most recent depth-of-market snapshot for the active instrument, or null when
    /// the broker doesn't expose L2 (NinjaTrader, Alpaca, IB-not-yet-wired). Set from the
    /// depth pump on the UI thread; raised via <see cref="ObservablePropertyAttribute"/> so the
    /// order-book pane re-renders automatically when it changes.</summary>
    [ObservableProperty] private DepthSnapshot? _latestDepth;

    /// <summary>
    /// True while a strategy that requires trade tape has an active tape pump backed by a
    /// broker client that declares <see cref="MarketDataCapabilities.SupportsLiveTrades"/>.
    /// This is source capability plus local pump health, not proof of account entitlement or
    /// symbol permission. Derived windows bind this to a feed-quality badge. Set on the UI
    /// thread; false before start, after stop, and when the pump fails.
    /// </summary>
    [ObservableProperty] private bool _tradeTapeAvailable;

    /// <summary>
    /// The market data feeds this hosted strategy needs. The base default is
    /// <see cref="StrategyDataRequirement.L1"/> | <see cref="StrategyDataRequirement.Bars"/>;
    /// subclasses that consume depth or trade tape override this to include
    /// <see cref="StrategyDataRequirement.Depth"/> and/or
    /// <see cref="StrategyDataRequirement.TradeTape"/> respectively. The base
    /// <see cref="StartAsync"/> reads this once at the moment Continue is pressed
    /// to decide which pumps to start — changing it after streaming begins has no effect.
    /// </summary>
    protected virtual StrategyDataRequirement DataRequirement =>
        StrategyDataRequirement.L1 | StrategyDataRequirement.Bars;

    /// <summary>Instruments shown in the picker — a capped, search-filtered view of
    /// <see cref="AllInstruments"/>.</summary>
    [ObservableProperty] private ObservableCollection<SignalInstrument> _instruments = new();

    /// <summary>Free-text filter applied over <see cref="AllInstruments"/> (see the picker's search box).</summary>
    [ObservableProperty] private string _instrumentSearchText = string.Empty;

    [ObservableProperty] private SignalInstrument? _selectedInstrument;
    [ObservableProperty] private string _status = "Configure the strategy to begin.";
    [ObservableProperty] private double? _lastBid;
    [ObservableProperty] private double? _lastAsk;
    [ObservableProperty] private double? _lastMid;
    [ObservableProperty] private long _ticksSeen;
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private string? _validationError;

    /// <summary>True once the user clicks Continue on the setup form.</summary>
    [ObservableProperty] private bool _isConfigured;

    /// <summary>True while Continue/Start is building the strategy and warming up history — drives the
    /// loading overlay so the user sees that data is being fetched rather than a frozen/blank chart.</summary>
    [ObservableProperty] private bool _isStarting;

    /// <summary>Headline shown on the loading overlay while <see cref="IsStarting"/> is true.</summary>
    [ObservableProperty] private string _loadingTitle = "Loading…";

    /// <summary>True only while the user has armed the algo via Run. Display-only otherwise.</summary>
    [ObservableProperty] private bool _isAlgoRunning;

    // ---------- Chart axis controls (live, render-only) ----------
    // Shared by every base-backed strategy's price chart. These only affect how the chart is
    // drawn — never strategy state — so they're live-editable while streaming. Windows read them
    // in their redraw; a change re-raises BarsChanged so the chart refreshes immediately.

    /// <summary>How many trailing bars the price chart draws (X-axis zoom / window).</summary>
    [ObservableProperty] private int _chartBarsShown = 150;

    /// <summary>When true the price chart Y range auto-fits; otherwise it's pinned to
    /// [<see cref="YAxisMin"/>, <see cref="YAxisMax"/>].</summary>
    [ObservableProperty] private bool _yAutoScale = true;

    [ObservableProperty] private double _yAxisMin;
    [ObservableProperty] private double _yAxisMax;

    partial void OnChartBarsShownChanged(int value) => RaiseBarsChanged();
    partial void OnYAutoScaleChanged(bool value) => RaiseBarsChanged();
    partial void OnYAxisMinChanged(double value) { if (!YAutoScale) RaiseBarsChanged(); }
    partial void OnYAxisMaxChanged(double value) { if (!YAutoScale) RaiseBarsChanged(); }

    // ---------- Display pause (render-only; the strategy keeps running) ----------

    /// <summary>Display pause: the chart-refresh events (<see cref="BarsChanged"/> /
    /// <see cref="TickProcessed"/>) stop firing while the pumps, bar aggregation, strategy and —
    /// if armed — signal generation all keep running underneath. Resume replays one catch-up
    /// refresh, so it is instant and exact.</summary>
    [ObservableProperty] private bool _isPaused;

    /// <summary>Set when a refresh was suppressed while paused, so resume can replay it.</summary>
    private bool _redrawPending;

    partial void OnIsPausedChanged(bool value)
    {
        if (value)
        {
            Status = $"⏸ Display paused — {StrategyDisplayName} keeps running underneath.";
            return;
        }
        Status = IsStreaming
            ? $"Streaming {SelectedInstrument?.DisplayName} — {StrategyDisplayName}"
            : "Resumed.";
        if (_redrawPending)
        {
            _redrawPending = false;
            BarsChanged?.Invoke(this, EventArgs.Empty);
            TickProcessed?.Invoke(this, EventArgs.Empty);
        }
        OnPauseReleased();
    }

    /// <summary>Called on resume (after the base catch-up refresh) so subclasses with their own
    /// render events — e.g. a 3D surface's SurfaceChanged — can replay a suppressed redraw.
    /// Subclasses gate their raise sites on <see cref="IsPaused"/> themselves.</summary>
    protected virtual void OnPauseReleased() { }

    /// <summary>The one choke point every chart-refresh signal goes through — pausing here pauses
    /// every window's render path (bar redraws and per-tick coalesced loops alike) without any
    /// per-window code.</summary>
    private void RaiseBarsChanged()
    {
        if (IsPaused) { _redrawPending = true; return; }
        BarsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RaiseTickProcessed()
    {
        if (IsPaused) { _redrawPending = true; return; }
        TickProcessed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Populates the picker from real instruments only — no hardcoded catalog. Prefers each connected
    /// broker's own (broker-tagged) list where available; otherwise falls back to the canonical
    /// instrument registry, which instrument-discovery fills on connect and is broker-agnostic — so a
    /// broker that returns a cold/curated list (LSE, IronBeam) or a momentary state mismatch can't
    /// blank the picker. Re-runs when a broker connects (see <see cref="OnBrokerStateChanged"/>).
    /// </summary>
    private async Task LoadInstrumentsAsync()
    {
        try
        {
            var connected = _services.Selector.Connected;
            var list = await _services.Repository.ListInstrumentsAsync();

            var brokerRows = (list ?? Array.Empty<TradableInstrument>())
                .Select(i => new SignalInstrument(
                    $"{i.DisplayName}  ·  {BrokerLabel(i.Broker)}",
                    i.Category,
                    i.Contract,
                    i.Broker))
                .ToList();

            // Broker-tagged rows are richer (pills show the source broker); fall back to the registry
            // when a broker contributed nothing so the picker reflects the discovered universe.
            IReadOnlyList<SignalInstrument> rows = brokerRows.Count > 0 ? brokerRows : RegistryRows();

            if (rows.Count == 0)
            {
                var brokers = connected.Count == 0 ? "none connected" : string.Join(", ", connected);
                Status = $"No instruments available yet ({brokers}). Connect a broker to load its universe.";
                _services.ActivityLog.Append(StrategyDisplayName, "INFO",
                    $"No instruments available ({brokers}); registry empty.");
                return;
            }

            AllInstruments = rows;
            var source = brokerRows.Count > 0 ? string.Join(", ", connected) : "instrument registry";
            _services.ActivityLog.Append(StrategyDisplayName, "INFO",
                $"Loaded {AllInstruments.Count} instruments from {source}.");

            // Preserve the user's current pick across a reload (this method also runs when a broker
            // connects after the window opened — see OnBrokerStateChanged). Match by symbol since the
            // broker-tagged DisplayName differs from any registry row the user may have selected.
            var prevSymbol = SelectedInstrument?.Contract.Symbol;
            SelectedInstrument =
                (prevSymbol is not null ? AllInstruments.FirstOrDefault(i => i.Contract.Symbol == prevSymbol) : null)
                ?? InstrumentPickerFilter.Remembered(StrategyId, AllInstruments)
                ?? AllInstruments.FirstOrDefault(i => i.Contract.Symbol == "SPY")
                ?? AllInstruments.FirstOrDefault(i => i.Contract.Symbol == "AAPL")
                ?? AllInstruments.FirstOrDefault();
            ApplyInstrumentFilter();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Strategy} instrument list load failed", StrategyId);
            _services.ActivityLog.Append(StrategyDisplayName, "WARN",
                $"Instrument list load failed ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    /// <summary>Picker rows from the canonical instrument registry (broker-agnostic; the host resolves
    /// each row's broker at Start). Reuses the shared converter so every picker builds rows identically.</summary>
    private IReadOnlyList<SignalInstrument> RegistryRows() =>
        SignalInstrumentCatalog.FromRegistry(_services.Registry);

    /// <summary>Short broker label appended to instrument rows so users can disambiguate the
    /// same ticker exposed by multiple connected brokers (e.g. "ES — IB" vs "ES — cTrader").</summary>
    private static string BrokerLabel(Core.Brokers.BrokerKind broker) => broker switch
    {
        Core.Brokers.BrokerKind.InteractiveBrokers => "IB",
        Core.Brokers.BrokerKind.NinjaTrader => "NinjaTrader",
        Core.Brokers.BrokerKind.CTrader => "cTrader",
        Core.Brokers.BrokerKind.Alpaca => "Alpaca",
        _ => broker.ToString(),
    };

    /// <summary>Resolves the broker to talk to for the selected instrument. Live picker rows carry
    /// their source broker; static-catalog rows (no broker connected yet) fall back to whichever
    /// broker is currently connected first. Throws if no broker is connected — the caller surfaces
    /// the message in <see cref="ValidationError"/> so the user can go connect one.</summary>
    private Core.Brokers.BrokerKind ResolveBroker(SignalInstrument instrument)
    {
        if (instrument.Broker is { } explicitBroker && _services.Selector.IsConnected(explicitBroker))
            return explicitBroker;
        var connected = _services.Selector.Connected;
        if (connected.Count == 0)
            throw new InvalidOperationException("No broker is connected. Connect at least one broker in the login screen.");
        return connected[0];
    }

    partial void OnInstrumentSearchTextChanged(string value) => ApplyInstrumentFilter();

    /// <summary>Rebuilds the visible <see cref="Instruments"/> for the picker. With no search text it
    /// collapses to just the current selection — the predefined universe stays hidden until the user
    /// searches; typing filters <see cref="AllInstruments"/>. Rebuilt in place so the selection never
    /// flickers out. See <see cref="InstrumentPickerFilter"/>.</summary>
    private void ApplyInstrumentFilter() => InstrumentPickerFilter.Apply(
        Instruments,
        InstrumentPickerFilter.Visible(AllInstruments, InstrumentSearchText, SelectedInstrument, MaxInstrumentsDisplayed));

    /// <summary>Subclasses build a fresh <see cref="IBacktestStrategy"/> here using their
    /// current parameter property values.</summary>
    protected abstract IBacktestStrategy BuildStrategy(Contract contract);

    /// <summary>Override to validate strategy-specific parameters before
    /// <see cref="IsConfigured"/> flips. Return an error message or null.</summary>
    protected virtual string? ValidateSetup() => null;

    /// <summary>Override to refresh indicator series after each new bar.
    /// Default does nothing.</summary>
    protected virtual void OnBarsUpdated() { }

    /// <summary>Called after <see cref="WarmUpBarsAsync"/> seeds <see cref="Bars"/> from the
    /// local store and before the live tick stream starts. Subclasses can pre-populate engine
    /// state from the same bars — e.g. the APEX scalper seeds its snapshot history so the
    /// price chart isn't blank until the first live candle rolls. Default does nothing.</summary>
    protected virtual Task OnWarmupBarsLoadedAsync(IReadOnlyList<Bar> bars) => Task.CompletedTask;

    /// <summary>Push one row onto the universal activity log, tagged with this strategy's display
    /// name as the source. Self-bounds and marshals to the UI thread inside the shared sink.</summary>
    protected void Log(string category, string message) =>
        _services.ActivityLog.Append(StrategyDisplayName, category, message);

    [RelayCommand]
    private async Task ContinueAsync()
    {
        ValidationError = null;
        if (SelectedInstrument is null) { ValidationError = "Pick an instrument before continuing."; return; }
        var setupError = ValidateSetup();
        if (setupError is not null) { ValidationError = setupError; return; }

        IsConfigured = true;
        await StartAsync();
    }

    [RelayCommand]
    private void ToggleAlgo()
    {
        IsAlgoRunning = !IsAlgoRunning;
        var label = SelectedInstrument?.DisplayName ?? "(none)";
        _logger.LogInformation("{Strategy} algo {State} for {Symbol}",
            StrategyDisplayName, IsAlgoRunning ? "ARMED" : "STOPPED", label);
        Status = IsAlgoRunning
            ? $"Algo armed on {label} — {StrategyDisplayName}"
            : $"Streaming {label} — algo idle";
        Log("ALGO", IsAlgoRunning ? $"ARMED on {label}" : $"DISARMED on {label}");
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (Volatile.Read(ref _disposeState) != 0) return;

        ValidationError = null;
        if (SelectedInstrument is null) { ValidationError = "Pick an instrument before starting."; return; }
        if (IsStreaming) return;

        Core.Brokers.BrokerKind broker;
        try { broker = ResolveBroker(SelectedInstrument); }
        catch (InvalidOperationException ex) { ValidationError = ex.Message; return; }

        // Snapshot the selected client's immutable source capabilities once. Every decision for
        // this run is made from the same descriptor, so a strategy cannot be constructed against
        // one capability view and have its pumps opened against another.
        var capabilities = _services.Selector.Get(broker).MarketDataCapabilities;
        var requirement = DataRequirement;
        if (DescribeCapabilityMismatch(broker, requirement, capabilities) is { } capabilityError)
        {
            IsConfigured = false;
            LatestDepth = null;
            TradeTapeAvailable = false;
            ValidationError = capabilityError;
            Status = capabilityError;
            Log("CAPABILITY", capabilityError);
            return;
        }

        var contract = SelectedInstrument.Contract;
        var instrumentId = _services.Ingest.Resolve(contract, broker);

        // Curtain up: building the strategy + warming history is the slow part the user was waiting on
        // with no feedback. Always taken down in the finally, on every exit path.
        IsStarting = true;
        LoadingTitle = $"Loading {SelectedInstrument.DisplayName}";
        Status = $"Loading market data for {SelectedInstrument.DisplayName} — {StrategyDisplayName}…";
        try
        {
            var runCts = new CancellationTokenSource();
            var runToken = runCts.Token;
            _streamCts = runCts;
            var strategy = BuildStrategy(contract);
            var router = _routerFactory.Create();
            _strategy = strategy;
            _router = router;
            router.SignalEmitted += OnSignalEmitted;
            _eventSubscription = router.OrderEvents.Subscribe(async evt =>
            {
                try { await strategy.OnOrderEventAsync(evt, runToken); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{Strategy} OnOrderEventAsync threw", StrategyId);
                    PluginFaultEvents.Report(ex);
                }
            });

            Signals.Clear();
            Bars.Clear();
            _currentBarStart = DateTime.MinValue;
            TicksSeen = 0;
            IsStreaming = true;
            Log("STREAM", $"Started {SelectedInstrument.DisplayName} ({contract.SecType} {contract.Exchange})");

            try
            {
                await strategy.OnStartAsync(_clock, router, runToken);
            }
            catch (OperationCanceledException) when (runToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                if (!IsCurrentRun(runCts) || runToken.IsCancellationRequested) return;
                _logger.LogError(ex, "{Strategy} OnStartAsync threw", StrategyId);
                PluginFaultEvents.Report(ex);
                Status = $"Failed to start: {ex.Message}";
                await StopAsync();
                return;
            }

            // Closing/stopping can complete while a plug-in ignores cancellation in OnStartAsync.
            // Never continue that stale run against the source StopAsync has already disposed.
            if (!IsCurrentRun(runCts) || runToken.IsCancellationRequested) return;

            await WarmUpBarsAsync(instrumentId, broker, runToken);
            if (!IsCurrentRun(runCts) || runToken.IsCancellationRequested) return;
            Status = $"Streaming {SelectedInstrument.DisplayName} — {StrategyDisplayName}";

            // Start (or join) the ref-counted L1 broker pump for this instrument. Quotes and depth
            // share the same handle on the ingest side, so a single Subscribe powers both observables.
            _ingestHandle = _services.Ingest.Subscribe(contract, broker);

            _ = RunQuoteStreamAsync(instrumentId, runToken);

            // Depth remains a useful optional display feed for baseline strategies, but opening a
            // hub reader for a broker that cannot publish it creates a permanently empty stream.
            // Required depth was already fail-closed above; optional depth is started only when
            // this exact selected client declares it.
            LatestDepth = null;
            if (capabilities.SupportsLevel2Depth)
                _ = RunDepthStreamAsync(
                    instrumentId,
                    requirement.HasFlag(StrategyDataRequirement.Depth),
                    runCts);

            // Tape is strategy-required rather than display-optional. Capability was validated
            // before BuildStrategy, so no exception-probe or speculative broker subscription is
            // needed here.
            TradeTapeAvailable = false;
            if (requirement.HasFlag(StrategyDataRequirement.TradeTape))
            {
                _ = RunTradeStreamAsync(broker, contract, instrumentId, runCts);
            }
        }
        finally
        {
            IsStarting = false;
        }
    }

    [RelayCommand]
    private async Task StopAsync()
    {
        var streamCts = Interlocked.Exchange(ref _streamCts, null);
        var strategy = Interlocked.Exchange(ref _strategy, null);
        var router = Interlocked.Exchange(ref _router, null);
        var ingestHandle = Interlocked.Exchange(ref _ingestHandle, null);
        var tradeIngestHandle = Interlocked.Exchange(ref _tradeIngestHandle, null);
        var eventSubscription = Interlocked.Exchange(ref _eventSubscription, null);
        if (!IsStreaming && streamCts is null && strategy is null && router is null &&
            ingestHandle is null && tradeIngestHandle is null && eventSubscription is null)
            return;

        try
        {
            try { streamCts?.Cancel(); }
            catch (Exception ex) { _logger.LogDebug(ex, "{Strategy} stream cancellation threw", StrategyId); }

            if (strategy is not null && router is not null)
                await strategy.OnEndAsync(_clock, router, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Strategy} OnEndAsync threw", StrategyId);
            PluginFaultEvents.Report(ex);
        }
        finally
        {
            ingestHandle?.Dispose();
            tradeIngestHandle?.Dispose();
            eventSubscription?.Dispose();
            if (router is not null) router.SignalEmitted -= OnSignalEmitted;
            streamCts?.Dispose();
            await DisposeStrategyAsync(strategy);
        }

        IsStreaming = false;
        IsAlgoRunning = false;
        LatestDepth = null;
        TradeTapeAvailable = false;
        Status = "Stopped";
        Log("STREAM", "Stopped");
    }

    private bool IsCurrentRun(CancellationTokenSource runCts) =>
        Volatile.Read(ref _disposeState) == 0 && ReferenceEquals(Volatile.Read(ref _streamCts), runCts);

    private static string? DescribeCapabilityMismatch(
        BrokerKind broker,
        StrategyDataRequirement requirement,
        MarketDataCapabilities capabilities)
    {
        var unknown = requirement & ~KnownDataRequirements;
        if (unknown != StrategyDataRequirement.None)
            return $"Cannot start strategy: unknown market-data requirement flags 0x{(int)unknown:X}.";

        var missing = new List<string>(3);
        if ((requirement.HasFlag(StrategyDataRequirement.L1) ||
             requirement.HasFlag(StrategyDataRequirement.Bars)) &&
            !capabilities.SupportsLevel1Quotes)
        {
            missing.Add(requirement.HasFlag(StrategyDataRequirement.Bars)
                ? "L1 quotes (required for live bars)"
                : "L1 quotes");
        }
        if (requirement.HasFlag(StrategyDataRequirement.Depth) && !capabilities.SupportsLevel2Depth)
            missing.Add("L2 depth");
        if (requirement.HasFlag(StrategyDataRequirement.TradeTape) && !capabilities.SupportsLiveTrades)
            missing.Add("trade tape");

        return missing.Count == 0
            ? null
            : $"Cannot start strategy: {broker} does not provide {string.Join(" and ", missing)} in this build.";
    }

    private async Task FailRequiredFeedAsync(
        CancellationTokenSource runCts,
        string feed,
        Exception? error = null)
    {
        if (!IsCurrentRun(runCts)) return;

        var detail = error is null ? "stream ended" : error.Message;
        var message = $"Required {feed} feed failed: {detail}";
        await UiThread.RunAsync(async () =>
        {
            if (!IsCurrentRun(runCts)) return;
            await StopAsync();
            IsConfigured = false;
            ValidationError = message;
            Status = message;
            Log("CAPABILITY", message);
        });
    }

    [RelayCommand]
    private void ClearSignals() => Signals.Clear();

    /// <summary>Seeds <see cref="Bars"/> with the most recent 1-minute bars from the local store
    /// so the chart isn't empty when the user clicks Start. Granularity intentionally differs from
    /// the 15-second live aggregation that follows — the store has no sub-minute bars and recent
    /// 1-minute context is more useful than no context at all. Silent on failure.</summary>
    private async Task WarmUpBarsAsync(InstrumentId instrumentId, BrokerKind broker, CancellationToken ct)
    {
        try
        {
            var recent = await _services.Store.GetRecentBarsAsync(instrumentId, WarmupBarSize, WarmupBarCount, broker, ct);
            if (recent.Count == 0) return;
            foreach (var b in recent) Bars.Add(b.ToBar());
            while (Bars.Count > MaxBarsRetained) Bars.RemoveAt(0);
            await OnWarmupBarsLoadedAsync(Bars.ToList());
            OnBarsUpdated();
            RaiseBarsChanged();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Strategy} warm-up read from store failed", StrategyId);
        }
    }

    /// <summary>Pumps canonical quotes off the hub, projects them to the legacy <see cref="Tick"/>
    /// the strategy interface still speaks, and feeds each one to the strategy on the UI thread.
    /// A bounded channel decouples the hub's publish thread from the consumer so a slow strategy
    /// can't block ingest.</summary>
    private async Task RunQuoteStreamAsync(InstrumentId instrumentId, CancellationToken ct)
    {
        var channel = Channel.CreateBounded<Tick>(new BoundedChannelOptions(QuoteChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        using var subscription = _services.Hub.Quotes(instrumentId).Subscribe(q =>
            channel.Writer.TryWrite(new Tick(q.EventTimeUtc, q.Bid, q.Ask, q.BidSize, q.AskSize)));

        var batch = new List<Tick>(256);
        try
        {
            while (await channel.Reader.WaitToReadAsync(ct))
            {
                batch.Clear();
                while (batch.Count < MaxStreamDrainBatch && channel.Reader.TryRead(out var t)) batch.Add(t);
                if (batch.Count == 0) continue;
                if (_strategy is null || _router is null) break;
                await UiThread.RunAsync(async () =>
                {
                    foreach (var tick in batch)
                    {
                        if (_strategy is null || _router is null) break;
                        LastBid = tick.Bid;
                        LastAsk = tick.Ask;
                        LastMid = (tick.Bid + tick.Ask) * 0.5;
                        TicksSeen++;
                        var completedBar = AggregateBar(tick);
                        _router.UpdateMarketContext(tick);
                        if (completedBar is not null)
                        {
                            try { await _strategy.OnBarAsync(completedBar, _clock, _router, ct); }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "{Strategy} OnBarAsync threw", StrategyId);
                                PluginFaultEvents.Report(ex);
                            }
                        }
                        try { await _strategy.OnTickAsync(tick, _clock, _router, ct); }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "{Strategy} OnTickAsync threw", StrategyId);
                            PluginFaultEvents.Report(ex);
                        }
                    }
                    // Per-batch redraw trigger — windows coalesce their own redraws off this anyway.
                    RaiseTickProcessed();
                });
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Strategy} tick stream ended", StrategyId);
            await UiThread.RunAsync(() => Status = $"Stream ended: {ex.Message}");
        }
        finally
        {
            channel.Writer.TryComplete();
            await UiThread.RunAsync(() => IsStreaming = false);
        }
    }

    /// <summary>
    /// Capability-gated L2 depth pump running alongside the quote stream. Forwards each book
    /// snapshot to the strategy's <see cref="IBacktestStrategy.OnDepthAsync"/> so book-aware
    /// signals (OBI) can use real depth. <see cref="StartAsync"/> never calls this method for a
    /// selected client whose capability snapshot declares depth unsupported.
    /// </summary>
    private async Task RunDepthStreamAsync(
        InstrumentId instrumentId,
        bool required,
        CancellationTokenSource runCts)
    {
        var ct = runCts.Token;
        var channel = Channel.CreateBounded<DepthSnapshot>(new BoundedChannelOptions(DepthChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        var batch = new List<DepthSnapshot>(64);
        try
        {
            using var subscription = _services.Hub.Depth(instrumentId).Subscribe(s =>
                channel.Writer.TryWrite(s));

            while (await channel.Reader.WaitToReadAsync(ct))
            {
                batch.Clear();
                while (batch.Count < MaxStreamDrainBatch && channel.Reader.TryRead(out var s)) batch.Add(s);
                if (batch.Count == 0) continue;
                if (_strategy is null || _router is null) break;
                await UiThread.RunAsync(async () =>
                {
                    foreach (var snapshot in batch)
                    {
                        if (_strategy is null || _router is null) break;
                        try { await _strategy.OnDepthAsync(snapshot, _clock, _router, ct); }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "{Strategy} OnDepthAsync threw", StrategyId);
                            PluginFaultEvents.Report(ex);
                        }
                    }
                    // The order-book pane only needs the freshest book; intermediates are stale.
                    LatestDepth = batch[^1];
                });
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Strategy} depth stream ended", StrategyId);
            if (required)
                await FailRequiredFeedAsync(runCts, "L2 depth", ex);
            else if (IsCurrentRun(runCts))
                await UiThread.RunAsync(() => LatestDepth = null);
        }
        finally
        {
            channel.Writer.TryComplete();
            if (required && IsCurrentRun(runCts) && !ct.IsCancellationRequested)
                await FailRequiredFeedAsync(runCts, "L2 depth");
        }
    }

    /// <summary>
    /// Opt-in trade-tape pump running alongside the quote and depth pumps, started only when
    /// <see cref="DataRequirement"/> includes <see cref="StrategyDataRequirement.TradeTape"/>.
    ///
    /// <para><see cref="StartAsync"/> validates the selected client's immutable
    /// <see cref="MarketDataCapabilities"/> snapshot before constructing the strategy. This pump
    /// therefore opens only the canonical ingest/hub route; it never calls the broker's
    /// <c>SubscribeTradesAsync</c> as an exception-based capability probe. Runtime pump failures
    /// clear <see cref="TradeTapeAvailable"/> without affecting quote/depth pumps.</para>
    ///
    /// <para>The channel/marshalling/cancellation pattern is identical to
    /// <see cref="RunDepthStreamAsync"/>. A bounded <see cref="Channel{T}"/> decouples the
    /// hub publish thread from the consumer so a slow strategy cannot block ingest.</para>
    /// </summary>
    private async Task RunTradeStreamAsync(
        BrokerKind broker, Contract contract, InstrumentId instrumentId, CancellationTokenSource runCts)
    {
        var ct = runCts.Token;
        var channel = Channel.CreateBounded<TradePrint>(new BoundedChannelOptions(TradeChannelCapacity)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        var batch = new List<TradePrint>(256);
        try
        {
            _tradeIngestHandle = _services.Ingest.SubscribeTrades(contract, broker);
            using var subscription = _services.Hub.Trades(instrumentId).Subscribe(t =>
                channel.Writer.TryWrite(t));
            if (IsCurrentRun(runCts))
                await UiThread.RunAsync(() => TradeTapeAvailable = true);

            while (await channel.Reader.WaitToReadAsync(ct))
            {
                batch.Clear();
                while (batch.Count < MaxStreamDrainBatch && channel.Reader.TryRead(out var t)) batch.Add(t);
                if (batch.Count == 0) continue;
                if (_strategy is null || _router is null) break;
                await UiThread.RunAsync(async () =>
                {
                    foreach (var trade in batch)
                    {
                        if (_strategy is null || _router is null) break;
                        try { await _strategy.OnTradeAsync(trade, _clock, _router, ct); }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "{Strategy} OnTradeAsync threw", StrategyId);
                            PluginFaultEvents.Report(ex);
                        }
                    }
                });
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Strategy} trade stream ended", StrategyId);
            if (IsCurrentRun(runCts))
                await UiThread.RunAsync(() => TradeTapeAvailable = false);
            await FailRequiredFeedAsync(runCts, $"trade tape ({broker})", ex);
        }
        finally
        {
            channel.Writer.TryComplete();
            if (IsCurrentRun(runCts) && !ct.IsCancellationRequested)
            {
                await UiThread.RunAsync(() => TradeTapeAvailable = false);
                await FailRequiredFeedAsync(runCts, $"trade tape ({broker})");
            }
        }
    }

    private Bar? AggregateBar(Tick tick)
    {
        var mid = (tick.Bid + tick.Ask) * 0.5;
        var ts = tick.TimestampUtc;
        var bucket = new DateTime(ts.Ticks - (ts.Ticks % BarInterval.Ticks), DateTimeKind.Utc);

        if (_currentBarStart == DateTime.MinValue)
        {
            _currentBarStart = bucket;
            _barOpen = _barHigh = _barLow = _barClose = mid;
            _barVolume = 1;
            return null;
        }

        if (bucket != _currentBarStart)
        {
            var bar = new Bar(_currentBarStart, _barOpen, _barHigh, _barLow, _barClose, _barVolume);
            Bars.Add(bar);
            while (Bars.Count > MaxBarsRetained) Bars.RemoveAt(0);
            Log("BAR", $"{bar.TimestampUtc:HH:mm:ss} O={bar.Open:F4} H={bar.High:F4} L={bar.Low:F4} C={bar.Close:F4} V={bar.Volume}");
            OnBarsUpdated();
            RaiseBarsChanged();

            _currentBarStart = bucket;
            _barOpen = _barHigh = _barLow = _barClose = mid;
            _barVolume = 1;
            return bar;
        }

        if (mid > _barHigh) _barHigh = mid;
        if (mid < _barLow) _barLow = mid;
        _barClose = mid;
        _barVolume++;
        return null;
    }

    private void OnSignalEmitted(SignalEntry entry)
    {
        Signals.Insert(0, entry);
        while (Signals.Count > MaxSignalsRetained) Signals.RemoveAt(Signals.Count - 1);

        var activity = entry.DirectSignal is { } emitted
            ? $"{entry.SideText} strength {emitted.Strength:F3} @ {entry.Price:F4} (mid {entry.Mid:F4})"
            : $"{entry.SideText} {entry.Quantity} {entry.OrderType} @ {entry.Price:F4} (mid {entry.Mid:F4})";
        Log("SIGNAL", activity + (IsAlgoRunning ? "" : " [idle]"));

        if (!IsAlgoRunning) return;

        var symbol = SelectedInstrument?.DisplayName ?? "(none)";
        var direction = entry.DirectSignal?.Kind switch
        {
            StrategySignalKind.Long => "LONG",
            StrategySignalKind.Short => "SHORT",
            StrategySignalKind.Flat => "FLAT",
            _ => entry.Side == OrderSide.Buy ? "LONG" : "SHORT",
        };
        var msg = entry.DirectSignal is { } direct
            ? $"{entry.SideText} strength {direct.Strength:F3} @ {entry.Price:F4} (mid {entry.Mid:F4})"
            : $"{entry.SideText} {entry.Quantity} {entry.OrderType} @ {entry.Price:F4} (mid {entry.Mid:F4})";

        _notifications.PublishAsync(new StrategyNotification(
            Kind: NotificationKind.Signal,
            StrategyId: StrategyId,
            StrategyName: StrategyDisplayName,
            Symbol: symbol,
            Direction: direction,
            Message: msg,
            TimestampUtc: entry.TimestampUtc))
            .FireAndForgetSafe(_logger, $"signal publish {StrategyId}");
    }

    // ---------- Named view presets (chart-axis controls + window-specific extras) ----------

    private readonly ToolPresetStore<StrategyViewPreset> _presetStore;

    /// <summary>Preset names for the picker; per strategy (tool-presets\strategy-{id}.json).</summary>
    public ObservableCollection<string> PresetNames { get; }

    /// <summary>Editable preset-picker text: type a name and Save, or pick an existing preset to apply.</summary>
    [ObservableProperty] private string _presetName = string.Empty;
    [ObservableProperty] private string? _selectedPreset;

    partial void OnSelectedPresetChanged(string? value)
    {
        if (value is null) return;
        PresetName = value;
        if (_presetStore.Get(value) is { } preset) ApplyPreset(preset);
    }

    /// <summary>Window-specific display toggles to persist inside a preset — override and return
    /// a string bag (each window owns its keys). Base returns null (no extras).</summary>
    protected virtual Dictionary<string, string>? CaptureExtraPreset() => null;

    /// <summary>Counterpart of <see cref="CaptureExtraPreset"/> — apply the window-specific bag.
    /// Called before the catch-up redraw; missing keys should keep current values.</summary>
    protected virtual void ApplyExtraPreset(IReadOnlyDictionary<string, string> extras) { }

    [RelayCommand]
    private void SavePreset()
    {
        var name = PresetName.Trim();
        if (name.Length == 0) return;
        _presetStore.Save(name, new StrategyViewPreset(
            ChartBarsShown, YAutoScale, YAxisMin, YAxisMax, CaptureExtraPreset()));
        RefreshPresetNames(selected: name);
        Log("PRESET", $"Preset '{name}' saved");
    }

    [RelayCommand]
    private void DeletePreset()
    {
        var name = SelectedPreset ?? PresetName.Trim();
        if (string.IsNullOrEmpty(name) || !_presetStore.Delete(name)) return;
        RefreshPresetNames(selected: null);
        Log("PRESET", $"Preset '{name}' deleted");
    }

    private void ApplyPreset(StrategyViewPreset preset)
    {
        if (preset.ChartBarsShown > 0) ChartBarsShown = preset.ChartBarsShown;
        YAutoScale = preset.YAutoScale;
        YAxisMin = preset.YAxisMin;
        YAxisMax = preset.YAxisMax;
        if (preset.Extras is not null) ApplyExtraPreset(preset.Extras);
        RaiseBarsChanged();
    }

    private void RefreshPresetNames(string? selected)
    {
        PresetNames.Clear();
        foreach (var n in _presetStore.Names) PresetNames.Add(n);
        SelectedPreset = selected;
    }

    // ---------- CSV export (portable UiFile seam; PNG snapshots stay view-side) ----------

    /// <summary>Exports the live 15-second chart bars. Volume is the tick count per bar (the
    /// live aggregation counts quote updates, not traded size) — the header says so.</summary>
    [RelayCommand]
    private async Task ExportBarsCsvAsync()
    {
        if (Bars.Count == 0) return;
        var sb = new StringBuilder();
        sb.AppendLine("time_utc,open,high,low,close,ticks");
        foreach (var b in Bars)
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{b.TimestampUtc:O},{b.Open},{b.High},{b.Low},{b.Close},{b.Volume}"));
        await SaveCsvAsync($"{StrategyId}-bars-{SymbolToken()}", sb.ToString());
    }

    /// <summary>Exports the signal tape, oldest first.</summary>
    [RelayCommand]
    private async Task ExportSignalsCsvAsync()
    {
        if (Signals.Count == 0) return;
        var sb = new StringBuilder();
        sb.AppendLine("time_utc,side,quantity_or_strength,order_or_signal_type,price,mid,note");
        foreach (var s in Signals.Reverse())   // stored newest-first
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{s.TimestampUtc:O},{s.SideText},{s.QuantityText},{s.TypeText},{s.Price},{s.Mid},{CsvQuote(s.Note)}"));
        await SaveCsvAsync($"{StrategyId}-signals-{SymbolToken()}", sb.ToString());
    }

    private static string CsvQuote(string? value) =>
        string.IsNullOrEmpty(value) ? "" : "\"" + value.Replace("\"", "\"\"") + "\"";

    private string SymbolToken() =>
        (SelectedInstrument?.Contract.Symbol ?? "live").Replace('/', '-').Replace(':', '-');

    private async Task SaveCsvAsync(string baseName, string content)
    {
        try
        {
            var path = await UiFile.SaveAsync("CSV", new[] { "csv" },
                $"{baseName}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv");
            if (path is null) return;
            await File.WriteAllTextAsync(path, content);
            Log("EXPORT", $"Exported → {path}");
            Status = $"Exported → {path}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Strategy} CSV export failed", StrategyId);
            Status = $"Export failed: {ex.Message}";
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0) return;

        // Remember the instrument the user was last on so this strategy window reopens on it.
        LastInstrumentStore.Save(StrategyId, SelectedInstrument?.Contract.Symbol);
        _services.Selector.StateChanged -= OnBrokerStateChanged;
        var streamCts = Interlocked.Exchange(ref _streamCts, null);
        var router = Interlocked.Exchange(ref _router, null);
        var ingestHandle = Interlocked.Exchange(ref _ingestHandle, null);
        var tradeIngestHandle = Interlocked.Exchange(ref _tradeIngestHandle, null);
        var eventSubscription = Interlocked.Exchange(ref _eventSubscription, null);
        var strategy = Interlocked.Exchange(ref _strategy, null);
        try { streamCts?.Cancel(); }
        catch (Exception ex) { _logger.LogDebug(ex, "{Strategy} stream cancellation threw during disposal", StrategyId); }
        streamCts?.Dispose();
        ingestHandle?.Dispose();
        tradeIngestHandle?.Dispose();
        eventSubscription?.Dispose();
        if (router is not null) router.SignalEmitted -= OnSignalEmitted;
        DisposeStrategy(strategy);
    }

    private async ValueTask DisposeStrategyAsync(IBacktestStrategy? strategy)
    {
        if (strategy is null) return;
        try
        {
            if (strategy is IAsyncDisposable asyncDisposable)
                await asyncDisposable.DisposeAsync();
            else if (strategy is IDisposable disposable)
                disposable.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Strategy} runtime disposal threw", StrategyId);
        }
    }

    private void DisposeStrategy(IBacktestStrategy? strategy)
    {
        if (strategy is null) return;
        if (strategy is IDisposable disposable)
        {
            try { disposable.Dispose(); }
            catch (Exception ex) { _logger.LogDebug(ex, "{Strategy} runtime disposal threw", StrategyId); }
            return;
        }

        if (strategy is IAsyncDisposable asyncDisposable)
            _ = DisposeStrategyAsyncCore(asyncDisposable);
    }

    private async Task DisposeStrategyAsyncCore(IAsyncDisposable strategy)
    {
        try { await strategy.DisposeAsync(); }
        catch (Exception ex) { _logger.LogDebug(ex, "{Strategy} runtime disposal threw", StrategyId); }
    }
}
