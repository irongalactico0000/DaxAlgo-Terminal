using System.Threading.Channels;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;

namespace TradingTerminal.Sandbox.Runtime;

/// <summary>Result returned by the guarded execution-book target intake.</summary>
public readonly record struct ExecutionTargetSubmissionResult(bool IsSuccess, string Message)
{
    public static ExecutionTargetSubmissionResult Success(string message) => new(true, message);

    public static ExecutionTargetSubmissionResult Failure(string message) => new(false, message);
}

/// <summary>
/// Narrow book-bound intake used by target sources. Implementations own reconciliation, risk,
/// lease/fencing, execution-mode, and OMS admission; a strategy never receives those capabilities.
/// </summary>
public interface IExecutionBookTargetIntake
{
    ValueTask<ExecutionTargetSubmissionResult> SubmitTargetAsync(
        string bookId,
        TradeIntent intent,
        CancellationToken cancellationToken = default);
}

/// <summary>Immutable configuration binding one sandbox strategy to one execution book.</summary>
public sealed record SandboxExecutionReplicationOptions(
    string BookId,
    string StrategyId,
    bool Enabled = true,
    string PolicyVersion = SandboxExecutionReplicator.DefaultPolicyVersion,
    ScaledMoney EstimatedRoundTripCostPerUnit = default);

/// <summary>One attempted replication and the guarded intake's result.</summary>
public readonly record struct SandboxExecutionReplicationOutcome(
    TradeIntent? Intent,
    ExecutionTargetSubmissionResult Result);

/// <summary>
/// Coalesces committed sandbox model-portfolio snapshots and maps each changed target one-for-one
/// to an exact <see cref="TradeIntentQuantityMode.TargetPosition"/> intent. This component knows no
/// broker or dispatcher; the bound intake owns the complete guarded Paper execution chain.
/// </summary>
public sealed class SandboxExecutionReplicator : IDisposable, IAsyncDisposable
{
    public const string DefaultPolicyVersion = "sandbox-model-portfolio-v1";

    private const byte PriceScale = 8;
    private readonly object _gate = new();
    private readonly object _queueGate = new();
    private readonly IModelPortfolioSource _source;
    private readonly IExecutionBookTargetIntake _intake;
    private readonly SandboxExecutionReplicationOptions _options;
    private readonly Channel<bool> _signals;
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _pump;
    private readonly Dictionary<InstrumentId, TradeIntent> _lastAcceptedIntents = [];
    private SandboxExecutionReplicationOutcome? _lastOutcome;
    private readonly Dictionary<InstrumentId, IModelPortfolio> _pendingSnapshots = [];
    private readonly HashSet<InstrumentId> _pendingForcedInstruments = [];
    private int _disposeStarted;

    public SandboxExecutionReplicator(
        IModelPortfolioSource source,
        IExecutionBookTargetIntake intake,
        SandboxExecutionReplicationOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(intake);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.BookId))
            throw new ArgumentException("A bound execution book id is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.StrategyId))
            throw new ArgumentException("Stable sandbox strategy provenance is required.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.PolicyVersion))
            throw new ArgumentException("A sandbox replication policy version is required.", nameof(options));
        if (!options.EstimatedRoundTripCostPerUnit.IsValid ||
            options.EstimatedRoundTripCostPerUnit.Coefficient < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Estimated round-trip cost must be an exact non-negative money value.");
        }

        _source = source;
        _intake = intake;
        _options = options with
        {
            BookId = options.BookId.Trim(),
            StrategyId = options.StrategyId.Trim(),
            PolicyVersion = options.PolicyVersion.Trim(),
        };
        _signals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropWrite,
            AllowSynchronousContinuations = false,
        });

        if (!_options.Enabled)
        {
            _pump = Task.CompletedTask;
            return;
        }

        _source.SnapshotChanged += OnSnapshotChanged;
        _pump = Task.Run(PumpAsync);
        QueueCurrent(force: false);
    }

    public bool IsEnabled => _options.Enabled;

    public SandboxExecutionReplicationOutcome? LastOutcome
    {
        get
        {
            lock (_gate)
                return _lastOutcome;
        }
    }

    public event Action<SandboxExecutionReplicationOutcome>? SubmissionCompleted;

    /// <summary>Retries the latest committed target after a closed intake gate is reopened.</summary>
    public bool ReplicateCurrent()
    {
        if (!_options.Enabled || Volatile.Read(ref _disposeStarted) != 0)
            return false;

        return QueueCurrent(force: true);
    }

    public void Dispose() => DisposeAsync().AsTask().GetAwaiter().GetResult();

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            return;

        if (_options.Enabled)
            _source.SnapshotChanged -= OnSnapshotChanged;
        _signals.Writer.TryComplete();
        _cancellation.Cancel();
        try
        {
            await _pump.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (_cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            _cancellation.Dispose();
        }
    }

    private void OnSnapshotChanged(IModelPortfolio snapshot)
    {
        if (Volatile.Read(ref _disposeStarted) == 0)
            Queue(snapshot, force: false);
    }

    private async Task PumpAsync()
    {
        await foreach (var _ in _signals.Reader.ReadAllAsync(_cancellation.Token).ConfigureAwait(false))
        {
            KeyValuePair<InstrumentId, IModelPortfolio>[] snapshots;
            HashSet<InstrumentId> forcedInstruments;
            lock (_queueGate)
            {
                snapshots = _pendingSnapshots
                    .OrderBy(static pair => pair.Key.Value)
                    .ToArray();
                forcedInstruments = new HashSet<InstrumentId>(_pendingForcedInstruments);
                _pendingSnapshots.Clear();
                _pendingForcedInstruments.Clear();
            }
            foreach (var pair in snapshots)
            {
                await ReplicateAsync(
                        pair.Value,
                        forcedInstruments.Contains(pair.Key),
                        _cancellation.Token)
                    .ConfigureAwait(false);
            }
        }
    }

    private async ValueTask ReplicateAsync(
        IModelPortfolio snapshot,
        bool force,
        CancellationToken cancellationToken)
    {
        if (!TryMap(snapshot, out var intent, out var failure))
        {
            Publish(new SandboxExecutionReplicationOutcome(
                null,
                ExecutionTargetSubmissionResult.Failure(failure)));
            return;
        }

        lock (_gate)
        {
            if (!force &&
                _lastAcceptedIntents.TryGetValue(intent.Instrument, out var accepted) &&
                accepted == intent)
            {
                return;
            }
        }

        ExecutionTargetSubmissionResult result;
        try
        {
            result = await _intake
                .SubmitTargetAsync(_options.BookId, intent, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            result = ExecutionTargetSubmissionResult.Failure(
                $"Sandbox target intake failed closed ({exception.GetType().Name}).");
        }

        lock (_gate)
        {
            if (result.IsSuccess)
                _lastAcceptedIntents[intent.Instrument] = intent;
        }
        Publish(new SandboxExecutionReplicationOutcome(intent, result));
    }

    private bool QueueCurrent(bool force)
    {
        var queued = false;
        foreach (var snapshot in _source.CurrentSnapshots)
            queued |= Queue(snapshot, force);
        return queued;
    }

    private bool Queue(IModelPortfolio snapshot, bool force)
    {
        lock (_queueGate)
        {
            if (Volatile.Read(ref _disposeStarted) != 0)
                return false;
            _pendingSnapshots[snapshot.Instrument] = snapshot;
            if (force)
                _pendingForcedInstruments.Add(snapshot.Instrument);
            _signals.Writer.TryWrite(true);
            return true;
        }
    }

    private bool TryMap(IModelPortfolio snapshot, out TradeIntent intent, out string failure)
    {
        intent = default;
        failure = string.Empty;
        if (snapshot.Instrument.IsNone)
        {
            failure = "Sandbox replication refused an unresolved instrument.";
            return false;
        }
        if (!TryWholeUnits(snapshot.PositionUnits, out var units))
        {
            failure = "Sandbox replication requires exact whole target units; no re-sizing or rounding is allowed.";
            return false;
        }
        if (!TryPrice(snapshot.ProtectiveStopPrice, out var stop))
        {
            failure = "Sandbox replication refused a non-exact protective-stop price.";
            return false;
        }
        if (!TryPrice(snapshot.ProfitTargetPrice, out var target))
        {
            failure = "Sandbox replication refused a non-exact profit-target price.";
            return false;
        }

        ScaledPrice? entryLimit = null;
        ScaledPrice? entryStop = null;
        if (snapshot.PendingEntry is { } pending)
        {
            if (!TryWholeUnits(pending.SignedTargetUnits, out units))
            {
                failure = "Sandbox replication requires exact whole pending-entry units.";
                return false;
            }
            if (!TryPrice(pending.TriggerPrice, out var trigger) || trigger is null)
            {
                failure = "Sandbox replication refused a non-exact pending-entry trigger price.";
                return false;
            }
            if (pending.IsStop)
                entryStop = trigger;
            else
                entryLimit = trigger;
        }

        intent = new TradeIntent(
            snapshot.Instrument,
            TradeIntentQuantityMode.TargetPosition,
            ScaledQuantity.FromWhole(units),
            stop,
            target,
            _options.EstimatedRoundTripCostPerUnit,
            _options.StrategyId,
            StrategyNoteId: 0,
            _options.PolicyVersion,
            entryLimit,
            entryStop);
        return true;
    }

    private static bool TryWholeUnits(double value, out long units)
    {
        units = 0;
        if (!double.IsFinite(value) || Math.Truncate(value) != value)
            return false;
        try
        {
            units = checked((long)value);
        }
        catch (OverflowException)
        {
            return false;
        }
        return (double)units == value;
    }

    private static bool TryPrice(double? value, out ScaledPrice? price)
    {
        price = null;
        if (value is null)
            return true;
        if (!double.IsFinite(value.Value) || value.Value <= 0d)
        {
            return false;
        }

        ScaledPrice quantized;
        try
        {
            quantized = ExecutionNumericBoundary.PriceFromDouble(value.Value, PriceScale);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
        var exactDecimal = ExecutionNumericBoundary.ToDecimal(quantized);
        if ((double)exactDecimal != value.Value ||
            !ExecutionNumericBoundary.TryPriceFromDecimal(exactDecimal, out var narrowed))
        {
            return false;
        }

        price = narrowed;
        return price.Value.IsValid && price.Value.Coefficient > 0;
    }

    private void Publish(SandboxExecutionReplicationOutcome outcome)
    {
        Action<SandboxExecutionReplicationOutcome>? handlers;
        lock (_gate)
        {
            _lastOutcome = outcome;
            handlers = SubmissionCompleted;
        }

        if (handlers is null)
            return;
        ThreadPool.QueueUserWorkItem(
            static dispatch => InvokeHandlers(dispatch.Handlers, dispatch.Outcome),
            new CallbackDispatch(handlers, outcome),
            preferLocal: false);
    }

    private static void InvokeHandlers(
        Action<SandboxExecutionReplicationOutcome> handlers,
        SandboxExecutionReplicationOutcome outcome)
    {
        foreach (Action<SandboxExecutionReplicationOutcome> handler in handlers.GetInvocationList())
        {
            try
            {
                handler(outcome);
            }
            catch
            {
                // A consumer callback cannot break the serialized replication pump.
            }
        }
    }

    private readonly record struct CallbackDispatch(
        Action<SandboxExecutionReplicationOutcome> Handlers,
        SandboxExecutionReplicationOutcome Outcome);
}
