using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaxAlgo.Sdk;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Core.Time;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.Sandbox;
using TradingTerminal.Sandbox.Runtime;
using TradingTerminal.UI.Avalonia.Controls.Render;
using TradingTerminal.UI.Execution;
using TradingTerminal.UI.Logging;
using TradingTerminal.UI.Strategies;

namespace TradingTerminal.App.Avalonia.Execution;

public sealed record PaperStrategyChoice(
    string Id,
    string Name,
    string Description,
    StrategyDataRequirement DataRequirement,
    StrategyParameterSchema RuntimeSchema,
    BacktestStrategyOption? LegacyOption = null,
    StrategyKernelRegistration? CanonicalRegistration = null)
{
    public string DisplayName => $"{Name} · {Id}";
}

public sealed partial class PaperStrategyParameterRow : ObservableObject
{
    public PaperStrategyParameterRow(StrategyParameter parameter)
    {
        Parameter = parameter ?? throw new ArgumentNullException(nameof(parameter));
        Value = Convert.ToString(parameter.Default, CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public StrategyParameter Parameter { get; }
    public string Key => Parameter.Key;
    public string DisplayName => Parameter.DisplayName;
    public string KindText => Parameter.Kind.ToString();
    public string Hint => Parameter.Description ??
        (Parameter.Min.HasValue || Parameter.Max.HasValue
            ? $"Range {Parameter.Min?.ToString(CultureInfo.InvariantCulture) ?? "−∞"} to {Parameter.Max?.ToString(CultureInfo.InvariantCulture) ?? "+∞"}"
            : "Launch-time value");

    [ObservableProperty]
    private string _value;
}

public sealed partial class PaperStrategyLegRow : ObservableObject
{
    public PaperStrategyLegRow(InstrumentId instrument, string symbol)
    {
        Instrument = instrument;
        Symbol = symbol;
    }

    public InstrumentId Instrument { get; }
    public string Symbol { get; }

    [ObservableProperty]
    private string _target = "0";

    [ObservableProperty]
    private string _position = "0";

    [ObservableProperty]
    private string _averageEntry = "—";
}

/// <summary>
/// Native desktop owner for one canonical SDK kernel, its bounded per-instrument model portfolio, and the
/// authenticated strategy-target route into the local Paper OMS.
/// </summary>
public sealed partial class PaperStrategyRunnerViewModel : ObservableObject, IDisposable
{
    private static readonly StrategyVersion RunnerStrategyVersion = new("1.0.0");

    private readonly IMarketDataHub _hub;
    private readonly IClock _clock;
    private readonly InMemoryLogSink _activityLog;
    private readonly PaperExecutionDesktopSession _paper;
    private readonly string _bookId;
    private readonly string _bookName;
    private readonly IInstrumentRegistry _instrumentRegistry;
    private readonly IMarketDataIngest? _marketDataIngest;
    private readonly IBrokerSelector? _brokerSelector;
    private SandboxStrategyRuntime? _runtime;
    private SandboxExecutionReplicator? _replicator;
    private AuthenticatedPaperExecutionBookTargetIntake? _intake;
    private PaperMarketDataExecutionBridge? _marketBridge;
    private AuthoredUnitFeedLease? _feedLease;
    private int _disposed;

    public PaperStrategyRunnerViewModel(
        IBacktestStrategyRegistry strategyRegistry,
        IMarketDataHub hub,
        IClock clock,
        InMemoryLogSink activityLog,
        PaperExecutionDesktopSession paper,
        IInstrumentRegistry instrumentRegistry,
        PaperExecutionBookDefinition? bookDefinition = null,
        IStrategyKernelRegistry? strategyKernelRegistry = null,
        IMarketDataIngest? marketDataIngest = null,
        IBrokerSelector? brokerSelector = null,
        StrategyKernelRegistration? initialStrategy = null)
    {
        ArgumentNullException.ThrowIfNull(strategyRegistry);
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _activityLog = activityLog ?? throw new ArgumentNullException(nameof(activityLog));
        _paper = paper ?? throw new ArgumentNullException(nameof(paper));
        _instrumentRegistry = instrumentRegistry ?? throw new ArgumentNullException(nameof(instrumentRegistry));
        _marketDataIngest = marketDataIngest;
        _brokerSelector = brokerSelector;
        _bookId = bookDefinition?.Id ?? paper.BookId;
        _bookName = bookDefinition?.Name ?? paper.BookName;

        var boundStrategies = bookDefinition?.Strategies ?? [];
        var registeredLegacyStrategies = strategyRegistry.All
            .Where(option => boundStrategies.Count == 0 || boundStrategies.Contains(option.Id, StringComparer.Ordinal))
            .ToArray();
        var registeredCanonicalStrategies = (strategyKernelRegistry?.All ?? [])
            .Where(strategy => boundStrategies.Count == 0 || boundStrategies.Contains(strategy.Id, StringComparer.Ordinal))
            .ToArray();
        var canonicalIds = registeredCanonicalStrategies.Select(static strategy => strategy.Id)
            .ToHashSet(StringComparer.Ordinal);

        UnsupportedMultiAssetStrategyCount = registeredLegacyStrategies.Count(option =>
            option.Schema.Parameters.Count(parameter => parameter.Kind == ParameterKind.Instrument) > 1);

        Strategies = registeredCanonicalStrategies
            .Where(strategy => strategy.AuthoredSpecification.Instruments.Count > 0)
            .Select(strategy => new PaperStrategyChoice(
                strategy.Id,
                strategy.DisplayName,
                $"AI-authored Paper strategy. Data: {strategy.DataRequirement}. {strategy.Description}",
                strategy.DataRequirement,
                strategy.Schema,
                CanonicalRegistration: strategy))
            .Concat(registeredLegacyStrategies
                .Where(option => !canonicalIds.Contains(option.Id))
            .Where(option =>
                option.Schema.Parameters.Count(parameter => parameter.Kind == ParameterKind.Instrument) <= 1)
                .Select(option => new PaperStrategyChoice(
                    option.Id,
                    option.DisplayName,
                    $"Data: {option.DataRequirement}. " +
                    (option.ResearchPaperUrl is null ? "Catalog strategy." : "Research-derived strategy."),
                    option.DataRequirement,
                    RuntimeSchema(option.Schema),
                    LegacyOption: option)))
            .OrderBy(static choice => choice.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Instruments = paper.Instruments;
        SelectedStrategy = initialStrategy is null
            ? Strategies.FirstOrDefault()
            : Strategies.FirstOrDefault(choice => string.Equals(choice.Id, initialStrategy.Id, StringComparison.Ordinal))
                ?? Strategies.FirstOrDefault();
        SelectedInstrument = bookDefinition is { PrimarySymbol.Length: > 0 }
            ? Instruments.FirstOrDefault(instrument => string.Equals(
                instrument.Symbol, bookDefinition.PrimarySymbol, StringComparison.OrdinalIgnoreCase))
                ?? Instruments.FirstOrDefault()
            : Instruments.FirstOrDefault();
        SelectCanonicalInstrument();
        StatusText = Strategies.Count == 0
            ? UnsupportedMultiAssetStrategyCount == 0
                ? "No eligible strategy kernel is registered."
                : $"No eligible strategy is available; {UnsupportedMultiAssetStrategyCount} legacy multi-asset definition(s) were blocked."
            : $"Book {_bookName} selected. Review the strategy's canonical asset set, then start the Paper-only runtime.";
        LastMessage = "No strategy order has been sent.";
        UpdateAssetSummary();
        RebuildStrategyLegs();
        RebuildParameters();
        RefreshCommandState();
    }

    public IReadOnlyList<PaperStrategyChoice> Strategies { get; }
    public IReadOnlyList<PaperExecutionInstrumentChoice> Instruments { get; }
    public int UnsupportedMultiAssetStrategyCount { get; }
    public string StrategyEligibilitySummary => UnsupportedMultiAssetStrategyCount == 0
        ? "Canonical single-asset and pair/basket strategies are eligible when every reviewed feed is available."
        : $"{UnsupportedMultiAssetStrategyCount} legacy multi-asset definition(s) remain hidden because the old factory accepts only one Contract.";
    public ObservableCollection<PaperStrategyParameterRow> Parameters { get; } = [];
    public ObservableCollection<PaperStrategyLegRow> StrategyLegs { get; } = [];
    public Action<IRenderSurface> Draw => DrawFrame;

    public bool HasEligibleStrategies => Strategies.Count != 0;
    public bool SelectionLocked => _runtime is not null;
    public bool IsRunning => _runtime?.State == SandboxStrategyRuntimeState.Running;
    public bool IsPaused => _runtime?.State == SandboxStrategyRuntimeState.Paused;
    public bool IsStopped => _runtime is null;
    public bool CanSelectInstrument => IsStopped && SelectedStrategy?.CanonicalRegistration is null;
    public bool IsMultiAssetStrategy => StrategyLegs.Count > 1;

    public event EventHandler? FrameRequested;

    [ObservableProperty]
    private PaperStrategyChoice? _selectedStrategy;

    [ObservableProperty]
    private PaperExecutionInstrumentChoice? _selectedInstrument;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _lastMessage = string.Empty;

    [ObservableProperty]
    private string _assetSummary = "No canonical asset selected";

    [ObservableProperty]
    private string _runtimeState = "Stopped";

    [ObservableProperty]
    private string _modelTarget = "0";

    [ObservableProperty]
    private string _modelPosition = "0";

    [ObservableProperty]
    private string _averageEntry = "—";

    [ObservableProperty]
    private string _modelEquity = "—";

    [ObservableProperty]
    private string _droppedEvents = "0";

    [ObservableProperty]
    private string _lastAlert = "No strategy alerts";

    partial void OnSelectedStrategyChanged(PaperStrategyChoice? value)
    {
        if (!SelectionLocked)
        {
            SelectCanonicalInstrument();
            RebuildParameters();
            RebuildStrategyLegs();
            UpdateAssetSummary();
        }
        OnPropertyChanged(nameof(CanSelectInstrument));
        RefreshCommandState();
    }

    partial void OnSelectedInstrumentChanged(PaperExecutionInstrumentChoice? value)
    {
        UpdateAssetSummary();
        RebuildStrategyLegs();
        RefreshCommandState();
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (SelectedStrategy is null ||
            (SelectedStrategy.CanonicalRegistration is null && SelectedInstrument is null)) return;
        IsBusy = true;
        RefreshCommandState();
        try
        {
            var choice = SelectedStrategy;
            var strategyId = new StrategyId(choice.Id);
            var values = Parameters.ToDictionary(
                parameter => parameter.Key,
                parameter => (object?)parameter.Value,
                StringComparer.Ordinal);
            Func<IStrategyKernel> kernelFactory;
            IReadOnlySet<InstrumentId>? hostAuthorizedInstruments = null;
            IReadOnlySet<InstrumentId> executionInstruments;

            if (choice.CanonicalRegistration is { } canonical)
            {
                var requested = canonical.AuthoredSpecification.Instruments;
                if (requested.Count == 0 || requested.Any(instrument =>
                        !Instruments.Any(choice => choice.InstrumentId == instrument.InstrumentId)))
                {
                    throw new InvalidOperationException(
                        "Every reviewed authored-strategy asset must exist in the selected Paper book.");
                }
                if (_marketDataIngest is null || _brokerSelector is null)
                    throw new InvalidOperationException(
                        "The authored-strategy market-feed owner is unavailable in this host.");

                _feedLease = AuthoredUnitFeedLease.Acquire(
                    canonical.AuthoredSpecification,
                    _marketDataIngest,
                    _instrumentRegistry,
                    _brokerSelector,
                    AuthoredUnitKindV1.Strategy);
                hostAuthorizedInstruments = _feedLease.AuthorizedInstruments;
                executionInstruments = hostAuthorizedInstruments;
                kernelFactory = canonical.Create;
            }
            else if (choice.LegacyOption is { } option)
            {
                if (SelectedInstrument is null)
                    throw new InvalidOperationException("A canonical Paper asset must be selected.");
                var instrumentParameter = choice.RuntimeSchema.Parameters.Single(parameter =>
                    parameter.Kind == ParameterKind.Instrument);
                values[instrumentParameter.Key] = SelectedInstrument.InstrumentId;
                var contract = ContractFor(SelectedInstrument.InstrumentId);
                var legacyParameters = new StrategyParameters(option.Schema, values);
                kernelFactory = () => new LegacyStrategyKernelAdapter(
                    option.Create(contract, legacyParameters),
                    SelectedInstrument.InstrumentId,
                    choice.RuntimeSchema);
                executionInstruments = new HashSet<InstrumentId> { SelectedInstrument.InstrumentId };
            }
            else
            {
                throw new InvalidOperationException("The selected strategy has no runnable kernel factory.");
            }

            _marketBridge = new PaperMarketDataExecutionBridge(
                _hub,
                executionInstruments,
                _paper.Runtime.Venue,
                _paper.Runtime.Oms,
                resultObserver: _ => QueueClientRefresh(),
                faultObserver: fault => Post(() =>
                {
                    LastMessage = $"Paper market bridge failed closed: {fault.Reason}";
                    StatusText = "Strategy execution paused by a Paper market-data fault.";
                }));
            _intake = new AuthenticatedPaperExecutionBookTargetIntake(
                _bookId,
                strategyId,
                RunnerStrategyVersion,
                _paper.Client,
                _paper,
                _clock,
                _marketBridge.TryGetLatestReferencePrice);
            _runtime = new SandboxStrategyRuntime(
                kernelFactory,
                choice.RuntimeSchema,
                values,
                _hub,
                _clock,
                instruments => instruments.Count == 1
                    ? new ModelPortfolioAccount(instruments, new ModelPortfolioAccountConfig())
                    : new MultiInstrumentModelPortfolioAccount(instruments, new ModelPortfolioAccountConfig()),
                _activityLog.Append,
                alert => Post(() =>
                {
                    LastAlert = $"{alert.Level}: {alert.Message}";
                    LastMessage = alert.Message;
                }),
                hostAuthorizedInstruments: hostAuthorizedInstruments);
            _runtime.SnapshotChanged += OnSnapshotChanged;
            _replicator = new SandboxExecutionReplicator(
                _runtime,
                _intake,
                new SandboxExecutionReplicationOptions(_bookId, choice.Id));
            _replicator.SubmissionCompleted += OnSubmissionCompleted;

            await _runtime.RunAsync().ConfigureAwait(false);
            Post(() =>
            {
                foreach (var snapshot in _runtime.CurrentSnapshots)
                    ApplySnapshot(snapshot);
                StatusText = "Running · committed model targets replicate through authenticated IPC to the Paper OMS.";
                LastMessage = "Strategy started. Waiting for authorized market data and a committed target.";
                RuntimeState = _runtime.State.ToString();
                NotifyRuntimeState();
            });
            _activityLog.Append("Paper Strategy", "INFO",
                $"Started {choice.Name} on {string.Join(", ", StrategyLegs.Select(static leg => leg.Symbol))} in Paper-only mode.");
        }
        catch (Exception exception)
        {
            await CleanupAsync(stopRuntime: true).ConfigureAwait(false);
            Post(() =>
            {
                StatusText = "Stopped · strategy startup failed closed.";
                LastMessage = $"No order was sent: {exception.Message}";
                RuntimeState = "Stopped";
                NotifyRuntimeState();
            });
        }
        finally
        {
            Post(() =>
            {
                IsBusy = false;
                RefreshCommandState();
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanPause))]
    private async Task PauseAsync()
    {
        if (_runtime is null) return;
        IsBusy = true;
        RefreshCommandState();
        try
        {
            await _runtime.PauseAsync().ConfigureAwait(false);
            Post(() =>
            {
                RuntimeState = "Paused";
                StatusText = "Paused · no new market events reach the kernel; existing Paper orders remain visible in the console.";
                LastMessage = "Strategy runtime paused. Paper orders were not silently cancelled.";
                NotifyRuntimeState();
            });
        }
        catch (Exception exception)
        {
            Post(() => LastMessage = $"Pause failed closed: {exception.Message}");
        }
        finally
        {
            Post(() => { IsBusy = false; RefreshCommandState(); });
        }
    }

    [RelayCommand(CanExecute = nameof(CanResume))]
    private async Task ResumeAsync()
    {
        if (_runtime is null) return;
        IsBusy = true;
        RefreshCommandState();
        try
        {
            await _runtime.ResumeAsync().ConfigureAwait(false);
            Post(() =>
            {
                RuntimeState = "Running";
                StatusText = "Running · model targets replicate through authenticated IPC to the Paper OMS.";
                LastMessage = "Strategy runtime resumed with the same locked strategy and asset binding.";
                NotifyRuntimeState();
            });
        }
        catch (Exception exception)
        {
            Post(() => LastMessage = $"Resume failed closed: {exception.Message}");
        }
        finally
        {
            Post(() => { IsBusy = false; RefreshCommandState(); });
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private async Task StopAsync()
    {
        IsBusy = true;
        RefreshCommandState();
        try
        {
            await CleanupAsync(stopRuntime: true).ConfigureAwait(false);
            Post(() =>
            {
                RuntimeState = "Stopped";
                StatusText = "Stopped · strategy market subscriptions and target replication are disposed.";
                LastMessage = "Strategy stopped. Existing Paper orders remain under explicit console control.";
                NotifyRuntimeState();
            });
        }
        finally
        {
            Post(() => { IsBusy = false; RefreshCommandState(); });
        }
    }

    [RelayCommand(CanExecute = nameof(CanRetryTarget))]
    private void RetryTarget()
    {
        if (_replicator?.ReplicateCurrent() == true)
            LastMessage = "Retry queued for the latest committed model target.";
    }

    private bool CanStart() =>
        !IsBusy &&
        _runtime is null &&
        SelectedStrategy is not null &&
        (SelectedStrategy.CanonicalRegistration is not null
            ? StrategyLegs.Count > 0
            : SelectedInstrument is not null);
    private bool CanPause() => !IsBusy && IsRunning;
    private bool CanResume() => !IsBusy && IsPaused;
    private bool CanStop() => !IsBusy && _runtime is not null;
    private bool CanRetryTarget() => !IsBusy && _replicator is not null;

    private void OnSnapshotChanged(IModelPortfolio snapshot) => Post(() => ApplySnapshot(snapshot));

    private void OnSubmissionCompleted(SandboxExecutionReplicationOutcome outcome) => Post(() =>
    {
        ModelTarget = outcome.Intent is { } intent
            ? $"{SymbolFor(intent.Instrument)} {ExecutionNumericBoundary.ToDecimal(intent.SignedUnits).ToString(CultureInfo.InvariantCulture)}"
            : "rejected before mapping";
        if (outcome.Intent is { } targetIntent &&
            StrategyLegs.FirstOrDefault(leg => leg.Instrument == targetIntent.Instrument) is { } leg)
        {
            leg.Target = ExecutionNumericBoundary.ToDecimal(targetIntent.SignedUnits)
                .ToString(CultureInfo.InvariantCulture);
        }
        LastMessage = outcome.Result.Message;
        FrameRequested?.Invoke(this, EventArgs.Empty);
        RetryTargetCommand.NotifyCanExecuteChanged();
    });

    private void ApplySnapshot(IModelPortfolio? snapshot)
    {
        if (snapshot is null) return;
        if (StrategyLegs.FirstOrDefault(leg => leg.Instrument == snapshot.Instrument) is { } leg)
        {
            leg.Position = snapshot.PositionUnits.ToString("0.########", CultureInfo.InvariantCulture);
            leg.AverageEntry = snapshot.PositionUnits == 0d
                ? "—"
                : snapshot.AverageEntryPrice.ToString("0.########", CultureInfo.InvariantCulture);
        }
        if (StrategyLegs.Count <= 1)
        {
            ModelPosition = snapshot.PositionUnits.ToString("0.########", CultureInfo.InvariantCulture);
            AverageEntry = snapshot.PositionUnits == 0d
                ? "—"
                : snapshot.AverageEntryPrice.ToString("0.########", CultureInfo.InvariantCulture);
            ModelEquity = snapshot.Equity.ToString("0.00", CultureInfo.InvariantCulture);
        }
        else
        {
            ModelPosition = $"{StrategyLegs.Count} legs";
            AverageEntry = "See legs";
            ModelEquity = "Paper account";
        }
        DroppedEvents = (_runtime?.DroppedEventCount ?? 0).ToString(CultureInfo.InvariantCulture);
        FrameRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RebuildParameters()
    {
        Parameters.Clear();
        if (SelectedStrategy is null) return;
        foreach (var parameter in SelectedStrategy.RuntimeSchema.Parameters
                     .Where(parameter => parameter.Kind != ParameterKind.Instrument))
        {
            Parameters.Add(new PaperStrategyParameterRow(parameter));
        }
    }

    private void SelectCanonicalInstrument()
    {
        if (SelectedStrategy?.CanonicalRegistration?.AuthoredSpecification.Instruments is not { Count: > 0 } requests)
            return;
        var instrumentId = requests[0].InstrumentId;
        SelectedInstrument = Instruments.FirstOrDefault(instrument => instrument.InstrumentId == instrumentId);
    }

    private void UpdateAssetSummary()
    {
        if (SelectedStrategy?.CanonicalRegistration?.AuthoredSpecification.Instruments is { Count: > 0 } requests)
        {
            AssetSummary = string.Join(" · ", requests.Select(request =>
                $"{SymbolFor(request.InstrumentId)} [{request.RequestId}]"));
            return;
        }
        AssetSummary = SelectedInstrument is null
            ? "No canonical asset selected"
            : $"{SelectedInstrument.Symbol} · {SelectedInstrument.AssetClass} · {SelectedInstrument.Exchange} · {SelectedInstrument.Currency} · ID {SelectedInstrument.InstrumentId.Value}";
    }

    private void RebuildStrategyLegs()
    {
        StrategyLegs.Clear();
        var instrumentIds = SelectedStrategy?.CanonicalRegistration?.AuthoredSpecification.Instruments
            .Select(static request => request.InstrumentId)
            .Distinct()
            .ToArray() ?? (SelectedInstrument is null ? [] : [SelectedInstrument.InstrumentId]);
        foreach (var instrument in instrumentIds)
            StrategyLegs.Add(new PaperStrategyLegRow(instrument, SymbolFor(instrument)));
        OnPropertyChanged(nameof(IsMultiAssetStrategy));
    }

    private string SymbolFor(InstrumentId instrument) =>
        Instruments.FirstOrDefault(choice => choice.InstrumentId == instrument)?.Symbol ??
        _instrumentRegistry.Get(instrument)?.CanonicalSymbol ??
        $"#{instrument.Value}";

    private void DrawFrame(IRenderSurface surface) => _runtime?.TryDraw(surface);

    private Contract ContractFor(InstrumentId instrumentId)
    {
        var instrument = _instrumentRegistry.Get(instrumentId)
            ?? throw new InvalidOperationException("The selected canonical instrument is no longer registered.");
        return new Contract(
            instrument.CanonicalSymbol,
            SecTypeFor(instrument.AssetClass),
            instrument.Exchange,
            instrument.Currency,
            instrument.Exchange);
    }

    private static StrategyParameterSchema RuntimeSchema(StrategyParameterSchema optionSchema)
    {
        var parameters = optionSchema.Parameters.ToList();
        var instrumentCount = parameters.Count(parameter => parameter.Kind == ParameterKind.Instrument);
        if (instrumentCount > 1)
        {
            throw new NotSupportedException(
                "The legacy strategy factory accepts exactly one Contract; use a canonical SDK strategy for pair/basket execution.");
        }
        if (instrumentCount == 0)
        {
            parameters.Insert(0, StrategyParameter.Instrument(
                "Instrument",
                "Canonical asset",
                InstrumentId.None,
                group: "Universe",
                description: "Bound explicitly by the Paper Strategy Runner before launch."));
        }
        return new StrategyParameterSchema(parameters);
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

    private void QueueClientRefresh() => _ = Task.Run(async () =>
    {
        try { await _paper.Client.RefreshAsync().ConfigureAwait(false); }
        catch { }
    });

    private async Task CleanupAsync(bool stopRuntime)
    {
        var runtime = Interlocked.Exchange(ref _runtime, null);
        var replicator = Interlocked.Exchange(ref _replicator, null);
        var intake = Interlocked.Exchange(ref _intake, null);
        var bridge = Interlocked.Exchange(ref _marketBridge, null);
        var feedLease = Interlocked.Exchange(ref _feedLease, null);
        if (runtime is not null) runtime.SnapshotChanged -= OnSnapshotChanged;
        if (replicator is not null) replicator.SubmissionCompleted -= OnSubmissionCompleted;

        if (stopRuntime && runtime is not null)
        {
            try { await runtime.StopAsync().ConfigureAwait(false); }
            catch { }
        }
        if (replicator is not null) await replicator.DisposeAsync().ConfigureAwait(false);
        intake?.Dispose();
        bridge?.Dispose();
        feedLease?.Dispose();
        if (runtime is not null) await runtime.DisposeAsync().ConfigureAwait(false);
    }

    private void NotifyRuntimeState()
    {
        OnPropertyChanged(nameof(SelectionLocked));
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsPaused));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(CanSelectInstrument));
        RefreshCommandState();
    }

    private void RefreshCommandState()
    {
        StartCommand.NotifyCanExecuteChanged();
        PauseCommand.NotifyCanExecuteChanged();
        ResumeCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        RetryTargetCommand.NotifyCanExecuteChanged();
    }

    private static void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        CleanupAsync(stopRuntime: true).GetAwaiter().GetResult();
        FrameRequested = null;
        GC.SuppressFinalize(this);
    }
}
