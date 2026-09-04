using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;

namespace TradingTerminal.Sandbox.Runtime;

/// <summary>Resolves the latest exact Paper reference price and its exchange observation time.</summary>
public delegate bool TryResolvePaperReferencePrice(
    InstrumentId instrumentId,
    out ScaledPrice price,
    out DateTimeOffset observedAtUtc);

/// <summary>Fixed safety and ownership binding for one strategy-to-Paper execution book.</summary>
public sealed record PaperExecutionBookTargetOptions
{
    public PaperExecutionBookTargetOptions(
        string bookId,
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        ExecutionResource resource,
        ExecutionLeaseClaim leaseClaim,
        RiskLimits riskLimits,
        ScaledMoney availableBuyingPower,
        ScaledMoney dailyNetRealizedPnl,
        ScaledMoney currentEquity,
        ScaledMoney peakEquity,
        TimeSpan? maximumReferencePriceAge = null,
        RiskControlMode controlMode = RiskControlMode.Active,
        bool killSwitchActive = false,
        ScaledRatio? contractMultiplier = null,
        string accountCurrency = "SIM")
    {
        if (string.IsNullOrWhiteSpace(bookId))
            throw new ArgumentException("A bound execution book id is required.", nameof(bookId));
        if (strategyId.IsEmpty) throw new ArgumentException("A strategy id is required.", nameof(strategyId));
        if (strategyVersion.IsEmpty) throw new ArgumentException("A strategy version is required.", nameof(strategyVersion));
        if (!resource.IsValid) throw new ArgumentException("The execution resource is invalid.", nameof(resource));
        if (!leaseClaim.IsValid || leaseClaim.Resource != resource)
            throw new ArgumentException("The current lease claim must own the bound execution resource.", nameof(leaseClaim));
        ArgumentNullException.ThrowIfNull(riskLimits);
        if (!availableBuyingPower.IsValid || availableBuyingPower.Coefficient < 0)
            throw new ArgumentOutOfRangeException(nameof(availableBuyingPower));
        if (!dailyNetRealizedPnl.IsValid) throw new ArgumentOutOfRangeException(nameof(dailyNetRealizedPnl));
        if (!currentEquity.IsValid) throw new ArgumentOutOfRangeException(nameof(currentEquity));
        if (!peakEquity.IsValid || CompareMoney(peakEquity, currentEquity) < 0)
            throw new ArgumentOutOfRangeException(nameof(peakEquity));
        if (!Enum.IsDefined(controlMode)) throw new ArgumentOutOfRangeException(nameof(controlMode));
        var multiplier = contractMultiplier ?? new ScaledRatio(1, 0);
        if (!multiplier.IsValid || multiplier.Coefficient <= 0)
            throw new ArgumentOutOfRangeException(nameof(contractMultiplier));
        var priceAge = maximumReferencePriceAge ?? TimeSpan.FromSeconds(15);
        if (priceAge <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(maximumReferencePriceAge));
        if (string.IsNullOrWhiteSpace(accountCurrency) || accountCurrency.Trim().Length > 16)
            throw new ArgumentException("A bounded account currency is required.", nameof(accountCurrency));

        BookId = bookId.Trim();
        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        Resource = resource;
        LeaseClaim = leaseClaim;
        RiskLimits = riskLimits;
        AvailableBuyingPower = availableBuyingPower;
        DailyNetRealizedPnl = dailyNetRealizedPnl;
        CurrentEquity = currentEquity;
        PeakEquity = peakEquity;
        MaximumReferencePriceAge = priceAge;
        ControlMode = controlMode;
        KillSwitchActive = killSwitchActive;
        ContractMultiplier = multiplier;
        AccountCurrency = accountCurrency.Trim().ToUpperInvariant();
    }

    public string BookId { get; }
    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public ExecutionResource Resource { get; }
    public ExecutionLeaseClaim LeaseClaim { get; }
    public RiskLimits RiskLimits { get; }
    public ScaledMoney AvailableBuyingPower { get; }
    public ScaledMoney DailyNetRealizedPnl { get; }
    public ScaledMoney CurrentEquity { get; }
    public ScaledMoney PeakEquity { get; }
    public TimeSpan MaximumReferencePriceAge { get; }
    public RiskControlMode ControlMode { get; }
    public bool KillSwitchActive { get; }
    public ScaledRatio ContractMultiplier { get; }
    public string AccountCurrency { get; }

    private static int CompareMoney(ScaledMoney left, ScaledMoney right) =>
        ExecutionNumericBoundary.ToDecimal(left).CompareTo(ExecutionNumericBoundary.ToDecimal(right));
}

/// <summary>
/// Guarded TargetPosition intake for the deterministic Paper OMS. It reconstructs current position
/// from immutable fills, treats every nonterminal order as reserved exposure, and converges a newer
/// target by cancelling one conflicting working order before calculating the replacement delta.
/// </summary>
public sealed class PaperExecutionBookTargetIntake : IExecutionBookTargetIntake, IDisposable
{
    private readonly PaperExecutionBookTargetOptions _options;
    private readonly OrderManagementService _oms;
    private readonly IOrderEventStore _eventStore;
    private readonly IClock _clock;
    private readonly TryResolvePaperReferencePrice _referencePriceResolver;
    private readonly SemaphoreSlim _operationGate = new(1, 1);
    private long _requestSequence;
    private int _disposed;

    public PaperExecutionBookTargetIntake(
        PaperExecutionBookTargetOptions options,
        OrderManagementService oms,
        IOrderEventStore eventStore,
        IClock clock,
        TryResolvePaperReferencePrice referencePriceResolver)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _oms = oms ?? throw new ArgumentNullException(nameof(oms));
        _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _referencePriceResolver = referencePriceResolver ?? throw new ArgumentNullException(nameof(referencePriceResolver));
    }

    public async ValueTask<ExecutionTargetSubmissionResult> SubmitTargetAsync(
        string bookId,
        TradeIntent intent,
        CancellationToken cancellationToken = default)
    {
        if (Volatile.Read(ref _disposed) != 0)
            return ExecutionTargetSubmissionResult.Failure("Sandbox target refused because the Paper intake is disposed.");
        if (!string.Equals(bookId?.Trim(), _options.BookId, StringComparison.Ordinal))
            return ExecutionTargetSubmissionResult.Failure("Sandbox target refused because the execution book binding does not match.");
        if (intent.QuantityMode != TradeIntentQuantityMode.TargetPosition)
            return ExecutionTargetSubmissionResult.Failure("Sandbox replication accepts only TargetPosition intents.");
        if (!string.Equals(intent.StrategyId, _options.StrategyId.Value, StringComparison.Ordinal))
            return ExecutionTargetSubmissionResult.Failure("Sandbox target refused because its strategy is not bound to this execution book.");
        if (intent.Instrument.IsNone || !intent.SignedUnits.IsValid)
            return ExecutionTargetSubmissionResult.Failure("Sandbox target refused an unresolved instrument or invalid exact target.");
        if (!intent.EstimatedRoundTripCostPerUnit.IsValid ||
            intent.EstimatedRoundTripCostPerUnit.Coefficient < 0)
        {
            return ExecutionTargetSubmissionResult.Failure("Sandbox target refused an invalid estimated cost.");
        }

        await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            return SubmitTarget(intent);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return ExecutionTargetSubmissionResult.Failure(
                $"Sandbox target failed closed while reconstructing OMS state ({exception.GetType().Name}).");
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

    private ExecutionTargetSubmissionResult SubmitTarget(TradeIntent intent)
    {
        var now = UtcNow();
        if (!_referencePriceResolver(intent.Instrument, out var marketPrice, out var observedAtUtc) ||
            !marketPrice.IsValid || marketPrice.Coefficient <= 0 ||
            observedAtUtc.Offset != TimeSpan.Zero || observedAtUtc > now ||
            now - observedAtUtc > _options.MaximumReferencePriceAge)
        {
            return ExecutionTargetSubmissionResult.Failure(
                "Sandbox target refused because no fresh exact Paper reference price is available.");
        }

        var target = ExecutionNumericBoundary.ToDecimal(intent.SignedUnits);
        var cancelledConflictingOrder = false;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var projections = ReadResourceProjections();
            var currentPosition = ProjectPosition(intent.Instrument, projections);
            var active = projections
                .Where(projection =>
                    projection.SubmitCommand.Metadata.InstrumentId == intent.Instrument &&
                    !OrderLifecycle.IsTerminal(projection.State))
                .ToArray();
            var workingReservation = ProjectWorkingReservation(active);
            var current = ExecutionNumericBoundary.ToDecimal(currentPosition);
            var reserved = ExecutionNumericBoundary.ToDecimal(workingReservation);
            var projected = checked(current + reserved);

            if (active.Length != 0)
            {
                if (projected == target)
                {
                    return ExecutionTargetSubmissionResult.Success(
                        "The Paper book already has a working order converging to the sandbox target.");
                }
                if (active.Length != 1)
                {
                    return ExecutionTargetSubmissionResult.Failure(
                        "Sandbox target found multiple nonterminal Paper orders for one asset; reconcile before retargeting.");
                }
                if (cancelledConflictingOrder)
                {
                    return ExecutionTargetSubmissionResult.Failure(
                        "Sandbox target cancellation did not produce a terminal Paper order; reconcile before retargeting.");
                }

                var conflicting = active[0];
                if (conflicting.State is not (OrderLifecycleState.Working or OrderLifecycleState.PartiallyFilled))
                {
                    return ExecutionTargetSubmissionResult.Failure(
                        $"Sandbox target cannot safely retarget an order in {conflicting.State}; reconcile before retargeting.");
                }

                var cancelled = CancelConflictingOrder(conflicting, now);
                if (!cancelled.IsSuccess)
                {
                    return ExecutionTargetSubmissionResult.Failure(
                        $"Sandbox target could not cancel the conflicting Paper order: {cancelled.Reason ?? cancelled.Fault.ToString()}.");
                }
                if (cancelled.Projection is null || !OrderLifecycle.IsTerminal(cancelled.Projection.State))
                {
                    return ExecutionTargetSubmissionResult.Failure(
                        "Sandbox target cancellation is not durably terminal; no replacement order was created.");
                }

                cancelledConflictingOrder = true;
                continue;
            }

            if (current == target)
            {
                return ExecutionTargetSubmissionResult.Success(
                    cancelledConflictingOrder
                        ? "The conflicting Paper order was cancelled and the Paper book now matches the sandbox target."
                        : "The Paper book already matches the sandbox target.");
            }

            return SubmitRemainingTarget(intent, marketPrice, now, currentPosition, current, target, cancelledConflictingOrder);
        }

        return ExecutionTargetSubmissionResult.Failure(
            "Sandbox target convergence exhausted its bounded cancel-and-replan cycle.");
    }

    private ExecutionTargetSubmissionResult SubmitRemainingTarget(
        TradeIntent intent,
        ScaledPrice marketPrice,
        DateTimeOffset now,
        ScaledQuantity currentPosition,
        decimal current,
        decimal target,
        bool cancelledConflictingOrder)
    {
        var delta = checked(target - current);
        if (!ExecutionNumericBoundary.TryQuantityFromDecimal(decimal.Abs(delta), out var quantity) ||
            quantity.Coefficient <= 0)
        {
            return ExecutionTargetSubmissionResult.Failure("Sandbox target delta cannot be represented exactly.");
        }

        var sequence = Interlocked.Increment(ref _requestSequence);
        var nonce = Guid.NewGuid().ToString("N");
        var clientOrderId = new ClientOrderId($"sbx-{sequence}-{nonce}");
        var causationId = new CausationId($"sbx-cause-{sequence}-{nonce}");
        var metadata = new ExecutionCommandMetadata(
            new CommandId($"sbx-command-{sequence}-{nonce}"),
            new CorrelationId($"sbx-correlation-{sequence}-{nonce}"),
            causationId,
            _options.Resource.TradingAccountId,
            _options.StrategyId,
            _options.StrategyVersion,
            _options.Resource.VenueId,
            intent.Instrument,
            _options.Resource.Environment,
            now,
            expectedOrderSequence: 0);
        var terms = new OrderTerms(
            delta > 0 ? OrderSide.Buy : OrderSide.Sell,
            EntryOrderType(intent),
            quantity,
            intent.EntryLimitPrice,
            intent.EntryStopPrice,
            TimeInForce.Day,
            reduceOnly: IsNonCrossingReduction(current, target));
        var mapping = new CanonicalInstructionMappingContext(
            new IntentId($"sbx-intent-{sequence}-{nonce}"),
            bucketId: null,
            new LegId($"sbx-leg-{sequence}-{nonce}"),
            _options.LeaseClaim.LeaseId,
            _options.LeaseClaim.FencingToken,
            TradeIntentQuantityMode.TargetPosition,
            intent.SignedUnits,
            currentPosition,
            intent.ProtectiveStopPrice,
            intent.ProfitTargetPrice,
            intent.EstimatedRoundTripCostPerUnit,
            intent.StrategyNoteId,
            intent.PolicyVersion,
            intent.EntryLimitPrice,
            intent.EntryStopPrice);
        var mappingFault = CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            clientOrderId,
            terms,
            mapping,
            out var instruction);
        if (mappingFault != OrderDomainFault.None || instruction is null)
        {
            return ExecutionTargetSubmissionResult.Failure(
                $"Sandbox target could not form a canonical order instruction ({mappingFault}).");
        }

        var command = new SubmitOrderCommand(
            metadata,
            new OrderId($"sbx-order-{sequence}-{nonce}"),
            clientOrderId,
            terms,
            instruction);
        var risk = new RiskEvaluationContext(
            _options.RiskLimits,
            _options.ControlMode,
            _options.KillSwitchActive,
            currentPosition,
            ScaledQuantity.Zero,
            ScaledQuantity.Zero,
            ScaledMoney.Zero,
            ScaledQuantity.Zero,
            ScaledMoney.Zero,
            ScaledQuantity.Zero,
            _options.AvailableBuyingPower,
            _options.DailyNetRealizedPnl,
            _options.CurrentEquity,
            _options.PeakEquity,
            marketPrice,
            CountExposureCommands(now),
            now,
            _options.ContractMultiplier,
            _options.AccountCurrency);
        var result = _oms.Submit(
            command,
            risk,
            new OrderCommandContext(
                causationId,
                new DeduplicationKey($"sbx-dedupe-{sequence}-{nonce}")));
        return result.IsSuccess
            ? ExecutionTargetSubmissionResult.Success(
                cancelledConflictingOrder
                    ? $"Sandbox target cancelled the conflicting order and reached {result.Projection!.State} through the guarded Paper OMS."
                    : $"Sandbox target reached {result.Projection!.State} through the guarded Paper OMS.")
            : ExecutionTargetSubmissionResult.Failure(
                $"Sandbox target failed closed: {result.Reason ?? result.Fault.ToString()}.");
    }

    private OmsCommandResult CancelConflictingOrder(OmsOrderProjection projection, DateTimeOffset now)
    {
        var sequence = Interlocked.Increment(ref _requestSequence);
        var nonce = Guid.NewGuid().ToString("N");
        var original = projection.SubmitCommand.Metadata;
        var causationId = new CausationId($"sbx-retarget-cancel-cause-{sequence}-{nonce}");
        var metadata = new ExecutionCommandMetadata(
            new CommandId($"sbx-retarget-cancel-command-{sequence}-{nonce}"),
            original.CorrelationId,
            causationId,
            original.TradingAccountId,
            original.StrategyId,
            original.StrategyVersion,
            original.VenueId,
            original.InstrumentId,
            original.Environment,
            now,
            projection.LastSequence);
        return _oms.Cancel(
            new CancelOrderCommand(metadata, projection.OrderId),
            new OrderCommandContext(
                causationId,
                new DeduplicationKey($"sbx-retarget-cancel-dedupe-{sequence}-{nonce}")));
    }

    private OmsOrderProjection[] ReadResourceProjections()
    {
        var projections = _eventStore.ReadOutbox()
            .Select(entry => entry.Event.AggregateId)
            .Distinct()
            .Select(aggregateId => _eventStore.ReadProjection(aggregateId) ??
                throw new InvalidDataException($"OMS projection '{aggregateId}' is missing."))
            .ToArray();
        if (projections.Any(projection => ResourceOf(projection) != _options.Resource))
            throw new InvalidDataException("The execution ledger contains a different venue/account/environment resource.");
        return projections;
    }

    private ScaledQuantity ProjectPosition(
        InstrumentId instrument,
        IReadOnlyList<OmsOrderProjection> projections)
    {
        decimal position = 0;
        foreach (var projection in projections.Where(item => item.SubmitCommand.Metadata.InstrumentId == instrument))
        {
            foreach (var orderEvent in _eventStore.Read(projection.ClientOrderId))
            {
                if (orderEvent.Fill is not { } fill) continue;
                var quantity = ExecutionNumericBoundary.ToDecimal(fill.Quantity);
                position = checked(position +
                    (projection.CanonicalTerms.Side == OrderSide.Buy ? quantity : -quantity));
            }
        }
        if (!ExecutionNumericBoundary.TryQuantityFromDecimal(position, out var result))
            throw new OverflowException("The exact Paper position cannot be represented.");
        return result;
    }

    private static ScaledQuantity ProjectWorkingReservation(IEnumerable<OmsOrderProjection> projections)
    {
        decimal reservation = 0;
        foreach (var projection in projections)
        {
            var remaining = checked(
                ExecutionNumericBoundary.ToDecimal(projection.Terms.Quantity) -
                ExecutionNumericBoundary.ToDecimal(projection.FilledQuantity));
            if (remaining < 0) throw new InvalidDataException("A working order is overfilled.");
            reservation = checked(reservation +
                (projection.Terms.Side == OrderSide.Buy ? remaining : -remaining));
        }
        if (!ExecutionNumericBoundary.TryQuantityFromDecimal(reservation, out var result))
            throw new OverflowException("The exact Paper working reservation cannot be represented.");
        return result;
    }

    private int CountExposureCommands(DateTimeOffset now)
    {
        var windowStart = now - _options.RiskLimits.RateLimitWindow;
        return _eventStore.ReadOutbox()
            .Select(entry => entry.Event)
            .Count(orderEvent =>
                orderEvent.Kind == OrderEventKind.DraftCreated &&
                orderEvent.OccurredAtUtc >= windowStart &&
                orderEvent.SubmitCommand is { } submit &&
                new ExecutionResource(
                    submit.Metadata.VenueId,
                    submit.Metadata.TradingAccountId,
                    submit.Metadata.Environment) == _options.Resource);
    }

    private static ExecutionResource ResourceOf(OmsOrderProjection projection) =>
        new(
            projection.SubmitCommand.Metadata.VenueId,
            projection.SubmitCommand.Metadata.TradingAccountId,
            projection.SubmitCommand.Metadata.Environment);

    private static OrderType EntryOrderType(TradeIntent intent) =>
        (intent.EntryLimitPrice.HasValue, intent.EntryStopPrice.HasValue) switch
        {
            (false, false) => OrderType.Market,
            (true, false) => OrderType.Limit,
            (false, true) => OrderType.Stop,
            (true, true) => OrderType.StopLimit,
        };

    private static bool IsNonCrossingReduction(decimal current, decimal target) =>
        current > 0 && target >= 0 && target < current ||
        current < 0 && target <= 0 && target > current;

    private DateTimeOffset UtcNow()
    {
        var value = _clock.UtcNow;
        if (value.Kind != DateTimeKind.Utc)
            throw new InvalidOperationException("The Paper target intake clock must return UTC.");
        return new DateTimeOffset(value);
    }
}
