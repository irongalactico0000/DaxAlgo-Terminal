using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DaxAlgo.Sdk;
using TradingTerminal.App.Avalonia.Harness;
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
using TradingTerminal.ExecutionUi;
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
    private string _bookPosition = "0";

    [ObservableProperty]
    private string _averageEntry = "—";
}

/// <summary>
/// Native desktop owner for one canonical SDK kernel, its bounded per-instrument model portfolio, and the
/// authenticated strategy-target route into the local Paper OMS or a console Real book (broker Paper only).
/// </summary>
public sealed partial class PaperStrategyRunnerViewModel : ObservableObject, IDisposable
{
    private static readonly StrategyVersion RunnerStrategyVersion = new("1.0.0");

    private readonly IMarketDataHub _hub;
    private readonly IClock _clock;
    private readonly InMemoryLogSink _activityLog;
    private readonly PaperExecutionDesktopSession _paper;
    private readonly IExecutionClient? _executionClient;
    private readonly PaperExecutionBookDefinition? _localPaperBook;
    private readonly string _defaultBookName;
    private readonly IInstrumentRegistry _instrumentRegistry;
    private readonly IMarketDataIngest? _marketDataIngest;
    private readonly IBrokerSelector? _brokerSelector;
    private SandboxStrategyRuntime? _runtime;
    private SandboxExecutionReplicator? _replicator;
    private IExecutionBookTargetIntake? _intake;
    private PaperMarketDataExecutionBridge? _marketBridge;
    private AuthoredUnitFeedLease? _feedLease;
    private int _disposed;
    private int _bookRefreshGeneration;

    public string BookId => SelectedBook?.BookId ?? _localPaperBook?.Id ?? _paper.BookId;

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
        StrategyKernelRegistration? initialStrategy = null,
        IReadOnlyDictionary<string, object?>? initialParameters = null,
        IExecutionClient? executionClient = null)
    {
        ArgumentNullException.ThrowIfNull(strategyRegistry);
        _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _activityLog = activityLog ?? throw new ArgumentNullException(nameof(activityLog));
        _paper = paper ?? throw new ArgumentNullException(nameof(paper));
        _instrumentRegistry = instrumentRegistry ?? throw new ArgumentNullException(nameof(instrumentRegistry));
        _marketDataIngest = marketDataIngest;
        _brokerSelector = brokerSelector;
        _executionClient = executionClient;
        _localPaperBook = bookDefinition;
        _defaultBookName = bookDefinition?.Name ?? paper.BookName;

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
        RebuildAvailableBooks();
        SelectedBook = AvailableBooks.FirstOrDefault(static book => book.CanStart)
            ?? AvailableBooks.FirstOrDefault();
        StatusText = Strategies.Count == 0
            ? UnsupportedMultiAssetStrategyCount == 0
                ? "No eligible strategy kernel is registered."
                : $"No eligible strategy is available; {UnsupportedMultiAssetStrategyCount} legacy multi-asset definition(s) were blocked."
            : $"Book {_defaultBookName} selected. Choose a Paper OMS or broker-Paper Real book, then start.";
        LastMessage = "No strategy order has been sent.";
        UpdateAssetSummary();
        RebuildStrategyLegs();
        RebuildParameters();
        ApplyParameterValues(initialParameters);
        if (initialParameters is not null)
        {
            StatusText = "Backtest-tested parameters loaded. Account risk will be evaluated again before every order.";
            LastMessage = "Review the selected book and press Start when ready.";
        }
        RefreshCommandState();
    }

    public IReadOnlyList<PaperStrategyChoice> Strategies { get; }
    public IReadOnlyList<PaperExecutionInstrumentChoice> Instruments { get; }
    public ObservableCollection<StrategyRunnerBookChoice> AvailableBooks { get; } = [];
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
    public string ModeBadgeText => SelectedBook?.BadgeText ?? "PAPER OMS · AUTHENTICATED IPC";
    public string BookBlockReason => SelectedBook is { CanStart: false, BlockReason: { Length: > 0 } reason }
        ? reason
        : string.Empty;
    public bool HasBookBlockReason => BookBlockReason.Length > 0;

    /// <summary>True when opened via Validate → Paper admit path.</summary>
    public bool OpenedFromValidatePaper { get; private set; }

    public string HarnessContextStrip =>
        AuthoredUnitHarnessSession.FormatContextStrip(
            SelectedStrategy?.Name,
            SelectedStrategy?.Id,
            SelectedBook?.DisplayName,
            ModeBadgeText,
            OpenedFromValidatePaper);

    public void MarkOpenedFromValidatePaper()
    {
        OpenedFromValidatePaper = true;
        OnPropertyChanged(nameof(OpenedFromValidatePaper));
        OnPropertyChanged(nameof(HarnessContextStrip));
    }

    public event EventHandler? FrameRequested;

    /// <summary>
    /// Selects an installed canonical strategy and copies the exact normalized parameter values from a
    /// completed Quick Backtest. A running strategy is never replaced underneath its live feed/OMS route.
    /// </summary>
    public bool TryPrepareTestedStrategy(
        StrategyKernelRegistration registration,
        IReadOnlyDictionary<string, object?> testedParameters,
        out string reason)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(testedParameters);
        if (_runtime is not null)
        {
            reason = "Stop the current Paper strategy before loading another tested strategy.";
            return false;
        }

        var choice = Strategies.FirstOrDefault(item =>
            string.Equals(item.Id, registration.Id, StringComparison.Ordinal));
        if (choice is null || choice.CanonicalRegistration is null ||
            !ReferenceEquals(choice.CanonicalRegistration, registration))
        {
            reason = "The exact tested strategy artifact is no longer registered or eligible for the selected Paper book. Run Quick Backtest again.";
            return false;
        }

        SelectedStrategy = choice;
        ApplyParameterValues(testedParameters);
        StatusText = "Backtest-tested parameters loaded. Paper account risk will be evaluated again before every order.";
        LastMessage = "Review the selected Paper book and press Start when ready.";
        reason = string.Empty;
        return true;
    }

    [ObservableProperty]
    private PaperStrategyChoice? _selectedStrategy;

    [ObservableProperty]
    private PaperExecutionInstrumentChoice? _selectedInstrument;

    [ObservableProperty]
    private StrategyRunnerBookChoice? _selectedBook;

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
    private string _bookPosition = "0";

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
        OnPropertyChanged(nameof(HarnessContextStrip));
        RefreshCommandState();
    }

    partial void OnSelectedInstrumentChanged(PaperExecutionInstrumentChoice? value)
    {
        UpdateAssetSummary();
        RebuildStrategyLegs();
        RefreshCommandState();
    }

    partial void OnSelectedBookChanged(StrategyRunnerBookChoice? value)
    {
        OnPropertyChanged(nameof(ModeBadgeText));
        OnPropertyChanged(nameof(BookBlockReason));
        OnPropertyChanged(nameof(HasBookBlockReason));
        OnPropertyChanged(nameof(BookId));
        OnPropertyChanged(nameof(HarnessContextStrip));
        if (IsStopped)
        {
            StatusText = value is null
                ? "Select a Paper OMS or broker-Paper Real book."
                : value.CanStart
                    ? $"Book {value.DisplayName} selected. Review assets, then Start."
                    : $"Book {value.DisplayName} is blocked: {value.BlockReason}";
        }
        RefreshCommandState();
    }

    [RelayCommand(CanExecute = nameof(CanRefreshBooks))]
    private void RefreshBooks()
    {
        var previousId = SelectedBook?.BookId;
        RebuildAvailableBooks();
        SelectedBook = AvailableBooks.FirstOrDefault(book =>
                           string.Equals(book.BookId, previousId, StringComparison.Ordinal))
                       ?? AvailableBooks.FirstOrDefault(static book => book.CanStart)
                       ?? AvailableBooks.FirstOrDefault();
        RefreshCommandState();
    }

    private bool CanRefreshBooks() => IsStopped && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        if (SelectedStrategy is null ||
            SelectedBook is null ||
            (SelectedStrategy.CanonicalRegistration is null && SelectedInstrument is null)) return;
        if (!SelectedBook.CanStart)
        {
            LastMessage = SelectedBook.BlockReason;
            return;
        }

        IsBusy = true;
        RefreshCommandState();
        try
        {
            var choice = SelectedStrategy;
            var boundBook = SelectedBook;
            var strategyId = new StrategyId(choice.Id);
            EnsureStrategyBoundToBook(boundBook, choice.Id);
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
                        "Every reviewed authored-strategy asset must exist in the host instrument registry.");
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
                    throw new InvalidOperationException("A canonical asset must be selected.");
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

            if (boundBook.Kind == StrategyRunnerBookKind.LocalPaper)
            {
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
                    boundBook.BookId,
                    strategyId,
                    RunnerStrategyVersion,
                    _paper.Client,
                    _paper,
                    _clock,
                    _marketBridge.TryGetLatestReferencePrice);
            }
            else
            {
                if (_executionClient is null)
                    throw new InvalidOperationException("The live-capable execution client is unavailable.");
                _intake = new ExecutionClientTargetIntakeAdapter(_executionClient, boundBook.BookId);
            }

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
                new SandboxExecutionReplicationOptions(boundBook.BookId, choice.Id));
            _replicator.SubmissionCompleted += OnSubmissionCompleted;

            await _runtime.RunAsync().ConfigureAwait(false);
            var routeLabel = boundBook.Kind == StrategyRunnerBookKind.LocalPaper
                ? "authenticated IPC to the Paper OMS"
                : $"OMS Real book '{boundBook.DisplayName}' (broker Paper only)";
            Post(() =>
            {
                foreach (var snapshot in _runtime.CurrentSnapshots)
                    ApplySnapshot(snapshot);
                StatusText = $"Running · committed model targets replicate through {routeLabel}.";
                LastMessage = "Strategy started. Waiting for authorized market data and a committed target.";
                RuntimeState = _runtime.State.ToString();
                NotifyRuntimeState();
            });
            _activityLog.Append("Paper Strategy", "INFO",
                $"Started {choice.Name} on {string.Join(", ", StrategyLegs.Select(static leg => leg.Symbol))} → {boundBook.DisplayName}.");
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
        SelectedBook is { CanStart: true } &&
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
        QueueClientRefresh();
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

    private void ApplyParameterValues(IReadOnlyDictionary<string, object?>? values)
    {
        if (values is null || SelectedStrategy is null) return;
        var normalized = new StrategyParameters(SelectedStrategy.RuntimeSchema, values);
        foreach (var row in Parameters)
        {
            var value = normalized.GetRaw(row.Key);
            row.Value = value switch
            {
                double number => number.ToString("R", CultureInfo.InvariantCulture),
                float number => number.ToString("R", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => value?.ToString() ?? string.Empty,
            };
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
        var generation = Interlocked.Increment(ref _bookRefreshGeneration);
        try
        {
            if (SelectedBook?.Kind == StrategyRunnerBookKind.ConsoleReal && _executionClient is not null)
            {
                if (generation != Volatile.Read(ref _bookRefreshGeneration))
                    return;
                var book = _executionClient.GetSnapshot().Books
                    .FirstOrDefault(item => string.Equals(item.Id, SelectedBook.BookId, StringComparison.Ordinal));
                if (book is null)
                    return;
                Post(() =>
                {
                    if (generation != Volatile.Read(ref _bookRefreshGeneration))
                        return;
                    ApplyConsoleBookSnapshot(book);
                });
                return;
            }

            await _paper.Client.RefreshAsync().ConfigureAwait(false);
            if (generation != Volatile.Read(ref _bookRefreshGeneration))
                return;
            var snapshot = _paper.Client.GetSnapshot();
            Post(() =>
            {
                if (generation != Volatile.Read(ref _bookRefreshGeneration))
                    return;
                ApplyBookSnapshot(snapshot);
            });
        }
        catch { }
    });

    /// <summary>
    /// Surfaces durable Paper OMS / adapter qty beside the model portfolio so Validate → Paper
    /// handoff can prove fills without opening the Execution Console.
    /// </summary>
    private void ApplyBookSnapshot(PaperExecutionClientSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        foreach (var leg in StrategyLegs)
        {
            var quantity = snapshot.Economics.Positions
                .FirstOrDefault(position => position.InstrumentId == leg.Instrument)?.Quantity
                ?? default;
            leg.BookPosition = FormatBookQuantity(quantity);
        }

        ApplyLegBookPositionSummary();
    }

    private void ApplyConsoleBookSnapshot(ExecutionBookReadModel book)
    {
        ArgumentNullException.ThrowIfNull(book);
        foreach (var leg in StrategyLegs)
        {
            var symbol = leg.Symbol;
            var position = book.Positions.FirstOrDefault(item =>
                string.Equals(item.Instrument, symbol, StringComparison.OrdinalIgnoreCase) ||
                item.Instrument.Contains(symbol, StringComparison.OrdinalIgnoreCase));
            leg.BookPosition = position is null
                ? "0"
                : string.IsNullOrWhiteSpace(position.RealQuantity) ? "0" : position.RealQuantity.Trim();
        }

        ApplyLegBookPositionSummary();
    }

    private void ApplyLegBookPositionSummary()
    {
        if (StrategyLegs.Count == 0)
        {
            BookPosition = "0";
            return;
        }

        if (StrategyLegs.Count == 1)
        {
            BookPosition = StrategyLegs[0].BookPosition;
            return;
        }

        var nonFlat = StrategyLegs.Count(static leg =>
            !string.Equals(leg.BookPosition, "0", StringComparison.Ordinal));
        BookPosition = nonFlat == 0 ? "0" : $"{nonFlat}/{StrategyLegs.Count} legs";
    }

    private static string FormatBookQuantity(ScaledQuantity quantity) =>
        ExecutionNumericBoundary.ToDecimal(quantity)
            .ToString("0.########", CultureInfo.InvariantCulture);

    private void RebuildAvailableBooks()
    {
        AvailableBooks.Clear();
        if (_localPaperBook is not null)
            AvailableBooks.Add(StrategyRunnerBookChoice.FromLocalPaper(_localPaperBook));
        else
        {
            AvailableBooks.Add(new StrategyRunnerBookChoice(
                StrategyRunnerBookKind.LocalPaper,
                _paper.BookId,
                $"Paper · {_defaultBookName}",
                "PAPER",
                IsLive: false,
                CanStart: true,
                BlockReason: string.Empty,
                BoundStrategies: []));
        }

        if (_executionClient is null)
            return;

        foreach (var book in _executionClient.GetSnapshot().Books
                     .OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            AvailableBooks.Add(StrategyRunnerBookChoice.FromConsoleBook(book));
        }
    }

    private static void EnsureStrategyBoundToBook(StrategyRunnerBookChoice book, string strategyId)
    {
        if (book.Kind != StrategyRunnerBookKind.ConsoleReal)
            return;
        if (book.BoundStrategies.Count == 0)
        {
            throw new InvalidOperationException(
                $"Real book '{book.DisplayName}' has no bound strategies. Create/bind the strategy id in the Execution Console before Start.");
        }
        if (!book.BoundStrategies.Contains(strategyId, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Strategy '{strategyId}' is not bound to Real book '{book.DisplayName}'. Bind it in the Execution Console, then refresh books.");
        }
    }

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
        if (intake is IDisposable disposableIntake)
            disposableIntake.Dispose();
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
        RefreshBooksCommand.NotifyCanExecuteChanged();
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
