using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Session;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Infrastructure.Plugins;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.Infrastructure.Threading;
using TradingTerminal.Recording;
using TradingTerminal.App.Avalonia.Execution;
using TradingTerminal.UI;
using TradingTerminal.UI.Logging;
using TradingTerminal.UI.Strategies;

namespace TradingTerminal.App.Avalonia.Shell;

/// <summary>
/// Root VM for the cross-platform shell — the Avalonia mirror of the WPF
/// <c>TradingTerminal.App.MainWindowViewModel</c>. Same surface (strategy catalog over
/// <see cref="IStrategyFactory"/>, aggregate connection state, UTC + local clocks, market-session
/// badges, the universal Activity Log with a live filter, and the header API meter) so the shell
/// reads identically to the WPF terminal. Strategy windows are opened through the factory by the
/// view's code-behind; the shell never names a concrete strategy.
/// </summary>
public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IBrokerSelector _brokerSelector;
    private readonly IStrategyFactory? _strategyFactory;
    private readonly IVisualizerRegistry? _visualizerRegistry;
    private readonly IStrategyKernelRegistry? _strategyKernelRegistry;
    private readonly SessionContext _session;
    private readonly ICliWorkspaceLauncher? _cliLauncher;
    private readonly DispatcherTimer _clockTimer;
    private bool _simulatedWarningLogged;

    public MainWindowViewModel(
        IStrategyFactory factory,
        IBrokerSelector brokerSelector,
        BrokerApiMeterViewModel apiMeter,
        InMemoryLogSink activityLog,
        PluginHostContext pluginHost,
        SessionContext session,
        IVisualizerRegistry visualizerRegistry,
        IStrategyKernelRegistry strategyKernelRegistry,
        ICliWorkspaceLauncher? cliLauncher = null,
        TickRecordingService? recorder = null,
        PaperExecutionBooksViewModel? executionBooks = null)
    {
        _brokerSelector = brokerSelector;
        _strategyFactory = factory;
        _visualizerRegistry = visualizerRegistry;
        _strategyKernelRegistry = strategyKernelRegistry;
        _session = session;
        _cliLauncher = cliLauncher;
        ApiMeter = apiMeter;
        Recorder = recorder;
        ExecutionBooks = executionBooks;
        ActivityLog = activityLog;
        PluginProblemCount = pluginHost.Report?.AttentionCount ?? 0;
        var installedClis = cliLauncher?.AvailableClis() ?? [];
        CliLaunchChoices = AgentCliAdapter.All
            .Select(adapter => new CliLaunchChoice(adapter, installedClis.Contains(adapter)))
            .ToList();

        Strategies = new ObservableCollection<ITradingStrategy>(factory.All);
        CatalogItems = new ObservableCollection<StrategyCatalogItemViewModel>(
            factory.All.Select(strategy => new StrategyCatalogItemViewModel(strategy))
                .Concat(visualizerRegistry.All.Select(visualizer =>
                    new StrategyCatalogItemViewModel(visualizer.Descriptor)))
                .Concat(strategyKernelRegistry.All.Select(strategy =>
                    new StrategyCatalogItemViewModel(strategy))));
        factory.Changed += OnStrategyCatalogChanged;
        visualizerRegistry.Changed += OnVisualizerCatalogChanged;
        strategyKernelRegistry.Changed += OnStrategyKernelCatalogChanged;
        VisibleLog = new ObservableCollection<LogEntry>(activityLog.Entries);
        activityLog.Entries.CollectionChanged += OnLogEntriesChanged;

        // 1 Hz clock + market-session flags (approximate; no DST/holiday calendar — same as WPF).
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clockTimer.Tick += (_, _) => UpdateClocks();
        _clockTimer.Start();
        UpdateClocks();

        RefreshAggregateState();
        _brokerSelector.StateChanged += OnBrokerStateChanged;
        _session.Changed += OnSessionChanged;

        activityLog.Append("Avalonia", "INFO", "Cross-platform shell started on the portable core (DI).");
        activityLog.Append("Avalonia", "INFO", $"{Strategies.Count} strategies loaded from the plug-in factory.");
    }

    /// <summary>Design-time ctor — empty graph so the previewer has something to render.</summary>
    public MainWindowViewModel()
    {
        _brokerSelector = null!;
        _session = null!;
        _strategyFactory = null;
        _clockTimer = null!;
        ActivityLog = new InMemoryLogSink();
        Strategies = new ObservableCollection<ITradingStrategy>();
        CatalogItems = new ObservableCollection<StrategyCatalogItemViewModel>();
        VisibleLog = new ObservableCollection<LogEntry>();
        CliLaunchChoices = Array.Empty<CliLaunchChoice>();
    }

    // ── Strategy catalog ────────────────────────────────────────────────────────────────────────

    /// <summary>The live strategy catalog — <see cref="IStrategyFactory.All"/> (ITradingStrategy),
    /// the same source the WPF shell binds to. Cards render the data-requirement + classification
    /// pills via the ported converters.</summary>
    public ObservableCollection<ITradingStrategy> Strategies { get; }

    public ObservableCollection<StrategyCatalogItemViewModel> CatalogItems { get; }

    [ObservableProperty] private StrategyCatalogItemViewModel? _selectedCatalogItem;

    public ITradingStrategy? SelectedStrategy => SelectedCatalogItem?.Strategy;
    public bool HasNoStrategies => CatalogItems.Count == 0;
    public bool HasStrategies => CatalogItems.Count > 0;

    public IReadOnlyList<CliLaunchChoice> CliLaunchChoices { get; }

    /// <summary>App-lifetime Paper books chip; null only in the design-time constructor.</summary>
    public PaperExecutionBooksViewModel? ExecutionBooks { get; }

    public void LaunchCli(CliLaunchChoice? choice)
    {
        if (choice is null) return;
        if (_cliLauncher is null || !choice.IsAvailable)
        {
            ActivityLog.Append("Vibe Quant", "WARN",
                $"{choice.DisplayName} is not available. Install it and ensure it is on PATH.");
            return;
        }

        var result = _cliLauncher.Launch(
            choice.Adapter,
            "vibe-scratch",
            "Vibe scratch strategy",
            StrategyBuildEffort.Standard);
        ActivityLog.Append("Vibe Quant", result.Success ? "INFO" : "WARN", result.Message);
    }

    /// <summary>Description shown in the details panel for the selected strategy.</summary>
    public string SelectedDetails => SelectedCatalogItem?.Description ?? "Select a strategy or visualizer to see its description.";

    partial void OnSelectedCatalogItemChanged(StrategyCatalogItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedStrategy));
        OnPropertyChanged(nameof(SelectedDetails));
    }

    private void OnStrategyCatalogChanged(object? sender, StrategyCatalogChange change) =>
        Dispatcher.UIThread.Post(() =>
        {
            var existingStrategy = Strategies.FirstOrDefault(strategy => strategy.Id == change.Strategy.Id);
            if (existingStrategy is null) Strategies.Add(change.Strategy);
            else Strategies[Strategies.IndexOf(existingStrategy)] = change.Strategy;

            var replacement = new StrategyCatalogItemViewModel(change.Strategy);
            var existingItem = CatalogItems.FirstOrDefault(item => item.Id == change.Strategy.Id);
            if (existingItem is null)
            {
                CatalogItems.Add(replacement);
            }
            else
            {
                var wasSelected = ReferenceEquals(SelectedCatalogItem, existingItem);
                CatalogItems[CatalogItems.IndexOf(existingItem)] = replacement;
                if (wasSelected) SelectedCatalogItem = replacement;
            }

            OnPropertyChanged(nameof(HasNoStrategies));
            OnPropertyChanged(nameof(HasStrategies));
        });

    private void OnVisualizerCatalogChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            for (var index = CatalogItems.Count - 1; index >= 0; index--)
            {
                if (CatalogItems[index].Kind == CatalogItemKind.Visualizer)
                    CatalogItems.RemoveAt(index);
            }

            foreach (var registration in _visualizerRegistry?.All ?? [])
                CatalogItems.Add(new StrategyCatalogItemViewModel(registration.Descriptor));

            OnPropertyChanged(nameof(HasNoStrategies));
            OnPropertyChanged(nameof(HasStrategies));
        });

    private void OnStrategyKernelCatalogChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            var selectedId = SelectedCatalogItem?.StrategyKernel?.Id;
            for (var index = CatalogItems.Count - 1; index >= 0; index--)
            {
                if (CatalogItems[index].StrategyKernel is not null)
                    CatalogItems.RemoveAt(index);
            }

            foreach (var registration in _strategyKernelRegistry?.All ?? [])
            {
                var item = new StrategyCatalogItemViewModel(registration);
                CatalogItems.Add(item);
                if (string.Equals(item.Id, selectedId, StringComparison.Ordinal))
                    SelectedCatalogItem = item;
            }

            OnPropertyChanged(nameof(HasNoStrategies));
            OnPropertyChanged(nameof(HasStrategies));
        });

    // ── Header API meter ────────────────────────────────────────────────────────────────────────

    public BrokerApiMeterViewModel? ApiMeter { get; }
    public TickRecordingService? Recorder { get; }

    public int PluginProblemCount { get; }
    public bool HasPluginProblems => PluginProblemCount > 0;

    // ── Universal Activity Log (filtered) ─────────────────────────────────────────────────────────

    public InMemoryLogSink ActivityLog { get; }

    /// <summary>Filtered view of the activity log shown in the bottom drawer.</summary>
    public ObservableCollection<LogEntry> VisibleLog { get; }

    [ObservableProperty] private string _logFilter = string.Empty;

    [ObservableProperty] private bool _isLogVisible;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyTitle = "Loading...";
    [ObservableProperty] private string _busyMessage = string.Empty;

    public void BeginBusy(string title, string message)
    {
        BusyTitle = title;
        BusyMessage = message;
        IsBusy = true;
    }

    public void EndBusy() => IsBusy = false;

    partial void OnLogFilterChanged(string value) => RebuildVisibleLog();

    private void OnLogEntriesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Append new entries that match the filter; full rebuild on resets/removes (capacity trim).
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems is not null)
        {
            foreach (LogEntry entry in e.NewItems)
                if (Matches(entry)) VisibleLog.Add(entry);
        }
        else
        {
            RebuildVisibleLog();
        }
    }

    private void RebuildVisibleLog()
    {
        VisibleLog.Clear();
        foreach (var entry in ActivityLog.Entries)
            if (Matches(entry)) VisibleLog.Add(entry);
    }

    private bool Matches(LogEntry e)
    {
        var f = LogFilter?.Trim();
        if (string.IsNullOrEmpty(f)) return true;
        return e.Source.Contains(f, StringComparison.OrdinalIgnoreCase)
            || e.Level.Contains(f, StringComparison.OrdinalIgnoreCase)
            || e.Message.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    // ── Aggregate connection state + market sessions ──────────────────────────────────────────────

    [ObservableProperty] private ConnectionState _connectionState = ConnectionState.Disconnected;
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("HH:mm:ss");
    [ObservableProperty] private string _currentTimeUtc = DateTime.UtcNow.ToString("HH:mm:ss");
    [ObservableProperty] private bool _nyseOpen;
    [ObservableProperty] private bool _lseOpen;
    [ObservableProperty] private bool _isSimulatedActive;
    [ObservableProperty] private long _feedDropCount;

    public bool IsDisconnected => ConnectionState is not ConnectionState.Connected;
    public bool HasFeedDrops => FeedDropCount > 0;
    public string DisconnectBannerText => "Disconnected — connect a broker to resume";
    public int ConnectedBrokerCount => _brokerSelector?.Connected.Count ?? 0;
    public bool IsAuthenticated => _session?.IsAuthenticated == true;
    public string SessionUserDisplay => !IsAuthenticated
        ? "Not signed in"
        : $"{(string.IsNullOrWhiteSpace(_session.Username) ? "Anonymous" : _session.Username)} | {_session.AccountType}";

    partial void OnConnectionStateChanged(ConnectionState value) => OnPropertyChanged(nameof(IsDisconnected));
    partial void OnFeedDropCountChanged(long value) => OnPropertyChanged(nameof(HasFeedDrops));

    public string RuntimeInfo =>
        $"{RuntimeInformation.OSDescription}  ·  {RuntimeInformation.FrameworkDescription}  ·  {RuntimeInformation.ProcessArchitecture}";

    private void OnBrokerStateChanged(object? sender, BrokerStateChangedEventArgs e) =>
        Dispatcher.UIThread.Post(RefreshAggregateState);

    private void RefreshAggregateState()
    {
        var states = _brokerSelector.AvailableKinds.Select(k => _brokerSelector.CurrentStateOf(k)).ToList();
        ConnectionState =
            states.Any(s => s == ConnectionState.Connected) ? ConnectionState.Connected
            : states.Any(s => s == ConnectionState.Reconnecting) ? ConnectionState.Reconnecting
            : states.Any(s => s == ConnectionState.Connecting) ? ConnectionState.Connecting
            : states.Any(s => s == ConnectionState.Failed) ? ConnectionState.Failed
            : ConnectionState.Disconnected;
        OnPropertyChanged(nameof(ConnectedBrokerCount));
        RefreshSimulatedState();
    }

    private void RefreshSimulatedState()
    {
        var active = _brokerSelector.Connected.Contains(BrokerKind.Simulated);
        IsSimulatedActive = active;
        SimulatedDataState.Set(active);
        if (active && !_simulatedWarningLogged)
        {
            _simulatedWarningLogged = true;
            ActivityLog.Append("System", "WARN",
                "SIMULATED DATA - the Simulated broker is connected. This is not a live market feed.");
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        OnPropertyChanged(nameof(IsAuthenticated));
        OnPropertyChanged(nameof(SessionUserDisplay));
    });

    /// <summary>File → Reconnect: re-arm every available broker's connect loop (mirrors the WPF
    /// ReconnectAsync). Failures are swallowed per broker so one bad broker can't abort the rest.</summary>
    public async Task ReconnectAllAsync()
    {
        if (_brokerSelector is null) return;
        foreach (var kind in _brokerSelector.AvailableKinds)
        {
            try { await _brokerSelector.ConnectAsync(kind); }
            catch { /* observed via StateChanged; don't abort the loop */ }
        }
    }

    private void UpdateClocks()
    {
        CurrentTime = DateTime.Now.ToString("HH:mm:ss");
        FeedDropCount = FeedDropMeter.GlobalDropped;
        var utc = DateTime.UtcNow;
        CurrentTimeUtc = utc.ToString("HH:mm:ss");
        NyseOpen = IsSessionOpen(utc, 14, 30, 21, 0);  // ~09:30–16:00 ET
        LseOpen = IsSessionOpen(utc, 8, 0, 16, 30);     // ~08:00–16:30 London
    }

    private static bool IsSessionOpen(DateTime utc, int startH, int startM, int endH, int endM)
    {
        if (utc.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        var minutes = utc.Hour * 60 + utc.Minute;
        return minutes >= startH * 60 + startM && minutes < endH * 60 + endM;
    }

    public void Dispose()
    {
        _clockTimer?.Stop();
        if (_brokerSelector is not null) _brokerSelector.StateChanged -= OnBrokerStateChanged;
        if (_session is not null) _session.Changed -= OnSessionChanged;
        if (_strategyFactory is not null) _strategyFactory.Changed -= OnStrategyCatalogChanged;
        if (_visualizerRegistry is not null) _visualizerRegistry.Changed -= OnVisualizerCatalogChanged;
        if (_strategyKernelRegistry is not null) _strategyKernelRegistry.Changed -= OnStrategyKernelCatalogChanged;
        ActivityLog.Entries.CollectionChanged -= OnLogEntriesChanged;
        ApiMeter?.Dispose();
    }
}

/// <summary>One discoverable entry in the Vibe Code CLI launcher.</summary>
public sealed class CliLaunchChoice(AgentCliAdapter adapter, bool isAvailable)
{
    public AgentCliAdapter Adapter { get; } = adapter;
    public bool IsAvailable { get; } = isAvailable;
    public string DisplayName => Adapter.DisplayName;
    public string MenuHeader => IsAvailable ? Adapter.DisplayName : $"{Adapter.DisplayName} - not installed";
}
