using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using TradingTerminal.Sandbox.Runtime;
using TradingTerminal.UI.Execution;

namespace TradingTerminal.App.Avalonia.Execution;

/// <summary>
/// Constructs a canonical strategy-target request without dispatching it. The desktop session owns
/// this boundary because it binds the one local Paper account, lease and instrument registry.
/// </summary>
public interface IPaperStrategyTargetOrderFactory
{
    bool TryCreateTargetSubmit(
        TradeIntent intent,
        ScaledPrice marketPrice,
        PaperExecutionClientSnapshot snapshot,
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        out ExecutionSubmitRequest? request,
        out string? reason);
}

/// <summary>
/// Authenticated desktop implementation of the sandbox target intake. It refreshes the verified
/// IPC projection before every decision and sends the resulting request through the same client as
/// the manual console. A newer target cancels one conflicting working order, refreshes the verified
/// position, and only then submits the remaining delta; this type never reaches the OMS, event store
/// or venue dispatcher directly.
/// </summary>
public sealed class AuthenticatedPaperExecutionBookTargetIntake : IExecutionBookTargetIntake, IDisposable
{
    private readonly string _bookId;
    private readonly StrategyId _strategyId;
    private readonly StrategyVersion _strategyVersion;
    private readonly IPaperExecutionClient _client;
    private readonly IPaperStrategyTargetOrderFactory _orderFactory;
    private readonly IClock _clock;
    private readonly TryResolvePaperReferencePrice _referencePriceResolver;
    private readonly TimeSpan _maximumReferencePriceAge;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private int _disposed;

    public AuthenticatedPaperExecutionBookTargetIntake(
        string bookId,
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        IPaperExecutionClient client,
        IPaperStrategyTargetOrderFactory orderFactory,
        IClock clock,
        TryResolvePaperReferencePrice referencePriceResolver,
        TimeSpan? maximumReferencePriceAge = null)
    {
        if (string.IsNullOrWhiteSpace(bookId))
            throw new ArgumentException("A bound execution book id is required.", nameof(bookId));
        if (strategyId.IsEmpty) throw new ArgumentException("A strategy id is required.", nameof(strategyId));
        if (strategyVersion.IsEmpty) throw new ArgumentException("A strategy version is required.", nameof(strategyVersion));
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _orderFactory = orderFactory ?? throw new ArgumentNullException(nameof(orderFactory));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _referencePriceResolver = referencePriceResolver ?? throw new ArgumentNullException(nameof(referencePriceResolver));
        var priceAge = maximumReferencePriceAge ?? TimeSpan.FromSeconds(15);
        if (priceAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumReferencePriceAge));

        _bookId = bookId.Trim();
        _strategyId = strategyId;
        _strategyVersion = strategyVersion;
        _maximumReferencePriceAge = priceAge;
    }

    public async ValueTask<ExecutionTargetSubmissionResult> SubmitTargetAsync(
        string bookId,
        TradeIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return ExecutionTargetSubmissionResult.Failure("Strategy target refused because the authenticated Paper intake is disposed.");
        if (!string.Equals(bookId?.Trim(), _bookId, StringComparison.Ordinal))
            return ExecutionTargetSubmissionResult.Failure("Strategy target refused because the execution-book binding does not match.");
        if (intent.QuantityMode != TradeIntentQuantityMode.TargetPosition ||
            !string.Equals(intent.StrategyId, _strategyId.Value, StringComparison.Ordinal))
        {
            return ExecutionTargetSubmissionResult.Failure("Strategy target refused because its intent or provenance is not bound to this runner.");
        }
        if (intent.Instrument.IsNone || !intent.SignedUnits.IsValid)
            return ExecutionTargetSubmissionResult.Failure("Strategy target refused an unresolved instrument or invalid exact target.");

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var clockNow = _clock.UtcNow;
            if (clockNow.Kind != DateTimeKind.Utc)
                throw new InvalidOperationException("The authenticated Paper intake clock must return UTC.");
            var now = new DateTimeOffset(clockNow);
            if (!_referencePriceResolver(intent.Instrument, out var price, out var observedAtUtc) ||
                !price.IsValid || price.Coefficient <= 0 || observedAtUtc.Offset != TimeSpan.Zero ||
                observedAtUtc > now || now - observedAtUtc > _maximumReferencePriceAge)
            {
                return ExecutionTargetSubmissionResult.Failure(
                    "Strategy target refused because no fresh exact Paper reference price is available.");
            }

            var refreshed = await _client.RefreshAsync(cancellationToken).ConfigureAwait(false);
            if (!refreshed.IsSuccess)
                return ExecutionTargetSubmissionResult.Failure($"Strategy target could not refresh the authenticated Paper view: {refreshed.Message}");
            var snapshot = _client.GetSnapshot();
            if (!snapshot.AdmissionOpen)
                return ExecutionTargetSubmissionResult.Failure("Strategy target refused because the Paper lease or intake gate is closed.");

            var cancelledConflictingOrder = false;
            var active = ActiveOrders(snapshot, intent.Instrument);
            if (active.Length != 0)
            {
                var current = ExecutionNumericBoundary.ToDecimal(Position(snapshot, intent.Instrument));
                var reserved = ExecutionNumericBoundary.ToDecimal(ProjectWorkingReservation(active));
                var target = ExecutionNumericBoundary.ToDecimal(intent.SignedUnits);
                if (checked(current + reserved) == target)
                {
                    return ExecutionTargetSubmissionResult.Success(
                        "A nonterminal Paper order is already converging to this strategy target.");
                }
                if (active.Length != 1)
                {
                    return ExecutionTargetSubmissionResult.Failure(
                        "Strategy target found multiple nonterminal Paper orders for one asset; reconcile before retargeting.");
                }
                if (active[0].State is not (OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled))
                {
                    return ExecutionTargetSubmissionResult.Failure(
                        $"Strategy target cannot safely retarget an order in {active[0].State}; reconcile before retargeting.");
                }

                var cancelled = await _client.CancelAsync(active[0].ClientOrderId, cancellationToken).ConfigureAwait(false);
                if (!cancelled.IsSuccess)
                {
                    return ExecutionTargetSubmissionResult.Failure(
                        $"Strategy target could not cancel the conflicting Paper order: {cancelled.Message}");
                }

                refreshed = await _client.RefreshAsync(cancellationToken).ConfigureAwait(false);
                if (!refreshed.IsSuccess)
                {
                    return ExecutionTargetSubmissionResult.Failure(
                        $"Strategy target could not verify the cancelled Paper order: {refreshed.Message}");
                }
                snapshot = _client.GetSnapshot();
                if (!snapshot.AdmissionOpen)
                    return ExecutionTargetSubmissionResult.Failure("Strategy target stopped because the Paper lease or intake gate closed during retargeting.");
                if (ActiveOrders(snapshot, intent.Instrument).Length != 0)
                {
                    return ExecutionTargetSubmissionResult.Failure(
                        "Strategy target cancellation is not durably terminal; no replacement order was created.");
                }
                cancelledConflictingOrder = true;
            }

            if (!_orderFactory.TryCreateTargetSubmit(
                    intent,
                    price,
                    snapshot,
                    _strategyId,
                    _strategyVersion,
                    out var request,
                    out var reason))
            {
                return ExecutionTargetSubmissionResult.Failure(reason ?? "The canonical strategy target was rejected.");
            }
            if (request is null)
            {
                return ExecutionTargetSubmissionResult.Success(
                    cancelledConflictingOrder
                        ? reason ?? "The conflicting Paper order was cancelled and the verified position now matches the strategy target."
                        : reason ?? "The Paper book already matches the strategy target.");
            }

            var submitted = await _client.SubmitAsync(request, cancellationToken).ConfigureAwait(false);
            return submitted.IsSuccess
                ? ExecutionTargetSubmissionResult.Success(
                    cancelledConflictingOrder
                        ? "Strategy target cancelled the conflicting order and submitted the remaining delta through authenticated IPC."
                        : "Strategy target was accepted through authenticated IPC by the Paper OMS.")
                : ExecutionTargetSubmissionResult.Failure($"Strategy target failed closed at the Paper service: {submitted.Message}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return ExecutionTargetSubmissionResult.Failure(
                $"Strategy target failed closed at the authenticated Paper boundary ({exception.GetType().Name}).");
        }
        finally
        {
            _operationGate.Release();
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        GC.SuppressFinalize(this);
    }

    private static OmsOrderProjection[] ActiveOrders(
        PaperExecutionClientSnapshot snapshot,
        InstrumentId instrument) =>
        snapshot.Orders
            .Where(order =>
                order.Instruction.TradeIntent.Instrument == instrument &&
                !OrderLifecycle.IsTerminal(order.State))
            .ToArray();

    private static ScaledQuantity Position(
        PaperExecutionClientSnapshot snapshot,
        InstrumentId instrument) =>
        snapshot.Economics.Positions.FirstOrDefault(item => item.InstrumentId == instrument)?.Quantity ??
        ScaledQuantity.Zero;

    private static ScaledQuantity ProjectWorkingReservation(IEnumerable<OmsOrderProjection> activeOrders)
    {
        decimal reservation = 0m;
        foreach (var order in activeOrders)
        {
            var remaining = checked(
                ExecutionNumericBoundary.ToDecimal(order.Terms.Quantity) -
                ExecutionNumericBoundary.ToDecimal(order.FilledQuantity));
            if (remaining < 0m)
                throw new InvalidDataException("A nonterminal Paper order is overfilled; reconcile before retargeting.");
            reservation = checked(reservation +
                (order.Terms.Side == OrderSide.Buy ? remaining : -remaining));
        }
        return ExecutionNumericBoundary.QuantityFromDecimal(reservation);
    }
}
