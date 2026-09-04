using FluentAssertions;
using System.Text.Json;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class PaperOmsLifecycleTests
{
    private static readonly InstrumentId Instrument = new(101);

    [Fact]
    public void Fractional_order_economics_remain_exact_through_command_event_and_projection()
    {
        var fixture = new Fixture();
        fixture.Venue.OnMarket(new PaperMarketSnapshot(
            Instrument,
            bid: P(100),
            ask: new ScaledPrice(100_125, 3),
            availableQuantity: new ScaledQuantity(1, 3),
            fixture.Now));
        var terms = new OrderTerms(
            OrderSide.Buy,
            OrderType.Market,
            new ScaledQuantity(1, 3));

        var submit = fixture.Submit(terms);
        var result = fixture.Oms.Submit(
            submit,
            fixture.RiskContext(),
            fixture.Context("fractional-submit"));

        result.IsSuccess.Should().BeTrue(result.Reason);
        result.Projection!.FilledQuantity.Should().Be(new ScaledQuantity(1, 3));
        result.Projection.AverageFillPrice.Should().Be(new ScaledPrice(100_125, 3));
        using var commandJson = JsonDocument.Parse(submit.CanonicalJson);
        var quantityJson = commandJson.RootElement.GetProperty("terms").GetProperty("quantity");
        quantityJson.GetProperty("coefficient").GetInt64().Should().Be(1);
        quantityJson.GetProperty("scale").GetByte().Should().Be(3);
        fixture.Store.Read(submit.ClientOrderId).Last().Fill.Should().BeEquivalentTo(new
        {
            Quantity = new ScaledQuantity(1, 3),
            Price = new ScaledPrice(100_125, 3),
            Fee = ScaledMoney.Zero,
        });
    }

    [Fact]
    public void Immediate_submits_cannot_consume_the_same_stored_liquidity_twice()
    {
        var fixture = new Fixture();
        fixture.Venue.OnMarket(new PaperMarketSnapshot(
            Instrument,
            bid: P(99),
            ask: P(100),
            availableQuantity: Q(3),
            fixture.Now));

        var first = fixture.Oms.Submit(
            fixture.Submit(new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2))),
            fixture.RiskContext(),
            fixture.Context("first-submit"));
        fixture.Advance();
        var second = fixture.Oms.Submit(
            fixture.Submit(new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2))),
            fixture.RiskContext(),
            fixture.Context("second-submit"));

        first.Projection!.FilledQuantity.Should().Be(Q(2));
        second.Projection!.FilledQuantity.Should().Be(Q(1));
        second.Projection.State.Should().Be(OrderLifecycleState.PartiallyFilled);
    }

    [Fact]
    public void Successive_fractional_fills_accumulate_exact_quantity_and_weighted_price()
    {
        var fixture = new Fixture();
        fixture.Venue.OnMarket(new PaperMarketSnapshot(
            Instrument,
            bid: P(99),
            ask: P(100),
            availableQuantity: new ScaledQuantity(1, 3),
            fixture.Now));
        var submit = fixture.Submit(new OrderTerms(
            OrderSide.Buy,
            OrderType.Limit,
            new ScaledQuantity(3, 3),
            limitPrice: P(110)));

        var first = fixture.Oms.Submit(submit, fixture.RiskContext(), fixture.Context("fractional-first"));
        first.Projection!.FilledQuantity.Should().Be(new ScaledQuantity(1, 3));

        fixture.Advance();
        fixture.Venue.OnMarket(new PaperMarketSnapshot(
            Instrument,
            bid: P(102),
            ask: P(103),
            availableQuantity: new ScaledQuantity(2, 3),
            fixture.Now));
        fixture.Oms.ProcessVenueEvents().Should().ContainSingle(result => result.IsSuccess);

        var completed = fixture.Oms.Query(submit.ClientOrderId)!;
        completed.State.Should().Be(OrderLifecycleState.Filled);
        completed.FilledQuantity.Should().Be(new ScaledQuantity(3, 3));
        completed.AverageFillPrice.Should().Be(P(102));
    }

    [Fact]
    public void Market_submit_records_receipt_before_ack_and_fill()
    {
        var fixture = new Fixture();
        fixture.Venue.OnMarket(new PaperMarketSnapshot(
            Instrument,
            bid: P(99),
            ask: P(100),
            availableQuantity: Q(10),
            fixture.Now));

        var submit = fixture.Submit(
            new OrderTerms(OrderSide.Buy, OrderType.Market, Q(5)));

        var result = fixture.Oms.Submit(submit, fixture.RiskContext(), fixture.Context("submit"));

        result.IsSuccess.Should().BeTrue();
        result.Projection!.State.Should().Be(OrderLifecycleState.Filled);
        result.Projection.Instruction.Should().BeSameAs(submit.CanonicalInstruction);
        result.Projection.CanonicalTerms.Should().Be(submit.CanonicalInstruction.Terms);
        result.Projection.FilledQuantity.Should().Be(Q(5));
        result.Projection.AverageFillPrice.Should().Be(P(100));
        var events = fixture.Store.Read(submit.ClientOrderId);
        events.Select(orderEvent => orderEvent.Kind).Should().Equal(
            OrderEventKind.DraftCreated,
            OrderEventKind.RiskAccepted,
            OrderEventKind.Prepared,
            OrderEventKind.Armed,
            OrderEventKind.SendStarted,
            OrderEventKind.SubmissionRecorded,
            OrderEventKind.VenueAcknowledged,
            OrderEventKind.FillReceived);
        events.IndexOfKind(OrderEventKind.SubmissionRecorded)
            .Should().BeLessThan(events.IndexOfKind(OrderEventKind.VenueAcknowledged));
        OrderEventChainVerifier.Verify(events).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Partial_fill_can_be_cancelled_through_pending_cancel_confirmation()
    {
        var fixture = new Fixture();
        fixture.Venue.OnMarket(new PaperMarketSnapshot(
            Instrument,
            bid: P(99),
            ask: P(100),
            availableQuantity: Q(2),
            fixture.Now));
        var submit = fixture.Submit(
            new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(5), limitPrice: P(101)));
        var submitted = fixture.Oms.Submit(
            submit,
            fixture.RiskContext(),
            fixture.Context("submit"));
        submitted.Projection!.State.Should().Be(OrderLifecycleState.PartiallyFilled);
        submitted.Projection.FilledQuantity.Should().Be(Q(2));

        fixture.Advance();
        var cancel = new CancelOrderCommand(
            fixture.Metadata("cancel", submitted.Projection.LastSequence),
            submit.OrderId);
        var cancelled = fixture.Oms.Cancel(cancel, fixture.Context("cancel"));

        cancelled.IsSuccess.Should().BeTrue();
        cancelled.Projection!.State.Should().Be(OrderLifecycleState.Cancelled);
        cancelled.Projection.FilledQuantity.Should().Be(Q(2));
        fixture.Store.Read(submit.ClientOrderId)
            .Select(orderEvent => orderEvent.Kind)
            .Should().ContainInOrder(
                OrderEventKind.FillReceived,
                OrderEventKind.CancelRequested,
                OrderEventKind.CancelConfirmed);
    }

    [Fact]
    public void Replace_confirmation_updates_terms_before_fill()
    {
        var fixture = new Fixture();
        fixture.Venue.OnMarket(new PaperMarketSnapshot(
            Instrument,
            bid: P(99),
            ask: P(100),
            availableQuantity: Q(10),
            fixture.Now));
        var submit = fixture.Submit(
            new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(4), limitPrice: P(90)));
        var submitted = fixture.Oms.Submit(
            submit,
            fixture.RiskContext(),
            fixture.Context("submit"));
        submitted.Projection!.State.Should().Be(OrderLifecycleState.Working);

        fixture.Advance();
        var replacementTerms = new OrderTerms(
            OrderSide.Buy,
            OrderType.Limit,
            Q(4),
            limitPrice: P(101));
        var replace = new ReplaceOrderCommand(
            fixture.Metadata("replace", submitted.Projection.LastSequence),
            submit.OrderId,
            replacementTerms);
        var replaced = fixture.Oms.Replace(
            replace,
            fixture.RiskContext(),
            fixture.Context("replace"));

        replaced.IsSuccess.Should().BeTrue();
        replaced.Projection!.State.Should().Be(OrderLifecycleState.Filled);
        replaced.Projection.Terms.Should().Be(replacementTerms);
        fixture.Store.Read(submit.ClientOrderId)
            .Select(orderEvent => orderEvent.Kind)
            .Should().ContainInOrder(
                OrderEventKind.ReplaceRiskAccepted,
                OrderEventKind.ReplaceRequested,
                OrderEventKind.ReplaceConfirmed,
                OrderEventKind.FillReceived);
    }

    [Fact]
    public void Malformed_or_illegal_venue_callback_fails_closed()
    {
        var fixture = new Fixture();
        var unknown = new PaperVenueEvent(
            new ExecutionEventId("event-unknown"),
            PaperVenueEventKind.Fill,
            new ClientOrderId("missing-order"),
            fixture.Now,
            new CausationId("cause-unknown"),
            Fill: new OrderFill(new TradeId("trade-unknown"), Q(1), P(100), ScaledMoney.Zero, fixture.Now));

        var result = fixture.Oms.ApplyVenueEvent(unknown);

        result.Fault.Should().Be(OmsCommandFault.OrderNotFound);
        fixture.Store.Read(new ClientOrderId("missing-order")).Should().BeEmpty();
    }

    [Fact]
    public void Stop_activation_requires_durable_monitoring_evidence_first()
    {
        var dispatcher = new ControlledDispatcher();
        var fixture = new Fixture(dispatcher);
        var submit = fixture.Submit(new OrderTerms(
            OrderSide.Buy,
            OrderType.StopLimit,
            Q(2),
            limitPrice: P(100),
            stopPrice: P(105)));
        var working = fixture.Oms.Submit(
            submit,
            fixture.RiskContext(),
            fixture.Context("stop-evidence-submit"));
        working.Projection!.State.Should().Be(OrderLifecycleState.Working);

        var activation = new PaperVenueEvent(
            new ExecutionEventId("controlled-stop-activation"),
            PaperVenueEventKind.StopActivated,
            submit.ClientOrderId,
            fixture.Now,
            new CausationId("cause-stop-activation"),
            dispatcher.BrokerOrderId);

        var rejected = fixture.Oms.ApplyVenueEvent(activation);

        rejected.Fault.Should().Be(OmsCommandFault.PersistenceRejected);
        rejected.Reason.Should().Contain(nameof(OrderProjectionFault.InvalidStopActivationEvidence));
        fixture.Oms.Query(submit.ClientOrderId)!.LastSequence.Should().Be(working.Projection.LastSequence);

        fixture.Oms.ApplyVenueEvent(new PaperVenueEvent(
            new ExecutionEventId("controlled-stop-monitoring"),
            PaperVenueEventKind.StopMonitoringStarted,
            submit.ClientOrderId,
            fixture.Now,
            new CausationId("cause-stop-monitoring"),
            dispatcher.BrokerOrderId)).IsSuccess.Should().BeTrue();
        fixture.Oms.ApplyVenueEvent(activation).IsSuccess.Should().BeTrue();
        fixture.Store.Read(submit.ClientOrderId).Select(item => item.Kind).Should().ContainInOrder(
            OrderEventKind.StopMonitoringStarted,
            OrderEventKind.StopActivated);
    }

    [Fact]
    public void Venue_callback_cannot_substitute_an_assigned_broker_order_identity()
    {
        var dispatcher = new ControlledDispatcher();
        var fixture = new Fixture(dispatcher);
        var submit = fixture.Submit(new OrderTerms(
            OrderSide.Buy,
            OrderType.Limit,
            Q(2),
            limitPrice: P(90)));
        var submitted = fixture.Oms.Submit(
            submit,
            fixture.RiskContext(),
            fixture.Context("identity-submit"));
        submitted.IsSuccess.Should().BeTrue();
        submitted.Projection!.BrokerOrderId.Should().Be(dispatcher.BrokerOrderId);
        var sequenceBeforeAttack = submitted.Projection.LastSequence;

        dispatcher.Queue(new PaperVenueEvent(
            new ExecutionEventId("event-broker-id-substitution"),
            PaperVenueEventKind.Fill,
            submit.ClientOrderId,
            fixture.Now,
            new CausationId("cause-broker-id-substitution"),
            BrokerOrderId: new BrokerOrderId("ATTACKER-ORDER-ID"),
            Fill: new OrderFill(
                new TradeId("trade-broker-id-substitution"),
                Q(1),
                P(90),
                ScaledMoney.Zero,
                fixture.Now)));

        var result = fixture.Oms.ProcessVenueEvents().Should().ContainSingle().Subject;

        result.Fault.Should().Be(OmsCommandFault.PersistenceRejected);
        result.Reason.Should().Contain(nameof(OrderProjectionFault.ExternalIdentityChanged));
        var unchanged = fixture.Oms.Query(submit.ClientOrderId)!;
        unchanged.LastSequence.Should().Be(sequenceBeforeAttack);
        unchanged.BrokerOrderId.Should().Be(dispatcher.BrokerOrderId);
        unchanged.FilledQuantity.Should().Be(ScaledQuantity.Zero);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Fill_is_accepted_while_cancel_or_replace_is_pending(bool replace)
    {
        var dispatcher = new ControlledDispatcher();
        var fixture = new Fixture(dispatcher);
        var submit = fixture.Submit(new OrderTerms(
            OrderSide.Buy,
            OrderType.Limit,
            Q(5),
            limitPrice: P(100)));
        var submitted = fixture.Oms.Submit(
            submit,
            fixture.RiskContext(),
            fixture.Context("submit"));
        submitted.Projection!.State.Should().Be(OrderLifecycleState.Working);

        fixture.Advance();
        OmsCommandResult pending;
        if (replace)
        {
            var command = new ReplaceOrderCommand(
                fixture.Metadata("replace", submitted.Projection.LastSequence),
                submit.OrderId,
                new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(5), limitPrice: P(101)));
            pending = fixture.Oms.Replace(
                command,
                fixture.RiskContext(),
                fixture.Context("replace"));
            pending.Projection!.State.Should().Be(OrderLifecycleState.PendingReplace);
        }
        else
        {
            var command = new CancelOrderCommand(
                fixture.Metadata("cancel", submitted.Projection.LastSequence),
                submit.OrderId);
            pending = fixture.Oms.Cancel(command, fixture.Context("cancel"));
            pending.Projection!.State.Should().Be(OrderLifecycleState.PendingCancel);
        }

        dispatcher.Queue(new PaperVenueEvent(
            new ExecutionEventId($"event-fill-pending-{replace}"),
            PaperVenueEventKind.Fill,
            submit.ClientOrderId,
            fixture.Now,
            new CausationId("cause-fill-pending"),
            BrokerOrderId: dispatcher.BrokerOrderId,
            Fill: new OrderFill(
                new TradeId($"trade-fill-pending-{replace}"),
                Q(2),
                P(100),
                ScaledMoney.Zero,
                fixture.Now)));

        fixture.Oms.ProcessVenueEvents().Should().ContainSingle(result => result.IsSuccess);
        var afterFill = fixture.Oms.Query(submit.ClientOrderId);
        afterFill!.State.Should().Be(replace
            ? OrderLifecycleState.PendingReplace
            : OrderLifecycleState.PendingCancel);
        afterFill.FilledQuantity.Should().Be(Q(2));
    }

    private static ScaledQuantity Q(long value) => ScaledQuantity.FromWhole(value);
    private static ScaledPrice P(long value) => new(value, 0);
    private static ScaledMoney M(long value) => new(value, 0);

    private sealed class Fixture
    {
        private readonly MutableClock _clock = new(new DateTime(2026, 8, 23, 4, 0, 0, DateTimeKind.Utc));
        private int _nextOrderSequence;

        public Fixture(IPaperExecutionDispatcher? dispatcher = null)
        {
            Store = new InMemoryOrderEventStore();
            Venue = new DeterministicPaperVenue();
            var leases = new InMemoryExecutionLeaseStore();
            leases.Acquire(
                new ExecutionResource(new VenueId("paper"), new TradingAccountId("paper-account"), ExecutionEnvironment.SimulatedPaper),
                new ExecutionLeaseId("paper-test-lease"),
                new RuntimeInstanceId("paper-test-runtime"),
                Now,
                Now.AddHours(1)).IsSuccess.Should().BeTrue();
            Oms = new OrderManagementService(Store, dispatcher ?? Venue, leases, _clock);
        }

        public InMemoryOrderEventStore Store { get; }
        public DeterministicPaperVenue Venue { get; }
        public OrderManagementService Oms { get; }
        public DateTimeOffset Now => new(_clock.UtcNow);

        public void Advance() => _clock.UtcNow = _clock.UtcNow.AddSeconds(1);

        public SubmitOrderCommand Submit(OrderTerms terms)
        {
            var sequence = checked(++_nextOrderSequence);
            var metadata = Metadata($"submit-{sequence}", 0);
            var clientOrderId = new ClientOrderId($"client-order-{sequence}");
            var signedUnits = terms.Side == OrderSide.Buy
                ? terms.Quantity
                : new ScaledQuantity(-terms.Quantity.Coefficient, terms.Quantity.Scale);
            var mappingContext = new CanonicalInstructionMappingContext(
                new IntentId($"intent-{sequence}"),
                bucketId: null,
                new LegId($"leg-{sequence}"),
                new ExecutionLeaseId("paper-test-lease"),
                new FencingToken(1),
                TradeIntentQuantityMode.Delta,
                signedUnits,
                currentPosition: ScaledQuantity.Zero,
                protectiveStopPrice: null,
                profitTargetPrice: null,
                estimatedRoundTripCostPerUnit: ScaledMoney.Zero,
                strategyNoteId: sequence,
                policyVersion: "paper-test-policy");
            CanonicalOrderInstructionMapper.TryCreate(
                metadata,
                clientOrderId,
                terms,
                mappingContext,
                out var instruction).Should().Be(OrderDomainFault.None);
            return new SubmitOrderCommand(
                metadata,
                new OrderId($"order-{sequence}"),
                clientOrderId,
                terms,
                instruction!);
        }

        public ExecutionCommandMetadata Metadata(string operation, long expectedSequence) =>
            new(
                new CommandId($"command-{operation}"),
                new CorrelationId("correlation-1"),
                new CausationId($"cause-{operation}"),
                new TradingAccountId("paper-account"),
                new StrategyId("strategy-1"),
                new StrategyVersion("1.0.0"),
                new VenueId("paper"),
                Instrument,
                ExecutionEnvironment.SimulatedPaper,
                Now,
                expectedSequence);

        public OrderCommandContext Context(string operation) =>
            new(
                new CausationId($"cause-{operation}"),
                new DeduplicationKey($"dedupe-{operation}"));

        public RiskEvaluationContext RiskContext() =>
            new(
                new RiskLimits(
                    maximumOrderQuantity: Q(1_000),
                    maximumAbsolutePosition: Q(1_000),
                    maximumGrossNotional: M(10_000_000),
                    minimumBuyingPower: ScaledMoney.Zero,
                    maximumDailyLoss: M(100_000),
                    maximumDrawdown: M(100_000),
                    maximumExposureCommandsPerWindow: 1_000,
                    rateLimitWindow: TimeSpan.FromMinutes(1)),
                RiskControlMode.Active,
                killSwitchActive: false,
                currentPositionQuantity: ScaledQuantity.Zero,
                currentBuyReservedQuantity: ScaledQuantity.Zero,
                currentSellReservedQuantity: ScaledQuantity.Zero,
                currentGrossReservedNotional: ScaledMoney.Zero,
                existingOrderSignedReservation: ScaledQuantity.Zero,
                existingOrderGrossReservation: ScaledMoney.Zero,
                existingOrderFilledQuantity: ScaledQuantity.Zero,
                availableBuyingPower: M(10_000_000),
                dailyNetRealizedPnl: ScaledMoney.Zero,
                currentEquity: M(1_000_000),
                peakEquity: M(1_000_000),
                marketPrice: P(100),
                exposureCommandsInWindow: 0,
                evaluatedAtUtc: Now);
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class ControlledDispatcher : IPaperExecutionDispatcher
    {
        private readonly Queue<PaperVenueEvent> _events = [];
        private long _dispatchSequence;

        public BrokerOrderId BrokerOrderId { get; } = new("CONTROLLED-1");

        public ExecutionDispatchResult Submit(
            SubmitOrderCommand command,
            OmsOrderProjection projection)
        {
            Queue(new PaperVenueEvent(
                new ExecutionEventId("controlled-ack"),
                PaperVenueEventKind.Acknowledged,
                command.ClientOrderId,
                command.Metadata.CreatedAtUtc,
                command.Metadata.CausationId ?? new CausationId(command.Metadata.CommandId.Value),
                BrokerOrderId));
            return Dispatched("submit", command.Metadata.CreatedAtUtc);
        }

        public ExecutionDispatchResult Cancel(
            CancelOrderCommand command,
            OmsOrderProjection projection) =>
            Dispatched("cancel", command.Metadata.CreatedAtUtc);

        public ExecutionDispatchResult Replace(
            ReplaceOrderCommand command,
            OmsOrderProjection projection) =>
            Dispatched("replace", command.Metadata.CreatedAtUtc);

        public IReadOnlyList<PaperVenueEvent> DrainEvents()
        {
            var events = _events.ToArray();
            _events.Clear();
            return Array.AsReadOnly(events);
        }

        public void Queue(PaperVenueEvent venueEvent) => _events.Enqueue(venueEvent);

        private ExecutionDispatchResult Dispatched(
            string operation,
            DateTimeOffset timestamp) =>
            ExecutionDispatchResult.Dispatched(new ExecutionDispatchReceipt(
                new DispatchAttemptId($"controlled-{operation}-{++_dispatchSequence}"),
                timestamp,
                BrokerOrderId));
    }
}

internal static class OmsOrderEventTestExtensions
{
    internal static int IndexOfKind(
        this IReadOnlyList<OmsOrderEvent> events,
        OrderEventKind kind)
    {
        for (var index = 0; index < events.Count; index++)
        {
            if (events[index].Kind == kind) return index;
        }
        return -1;
    }
}
