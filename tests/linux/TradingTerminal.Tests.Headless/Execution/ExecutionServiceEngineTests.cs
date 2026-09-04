using FluentAssertions;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class ExecutionServiceEngineTests
{
    [Fact]
    public void Status_reports_the_current_paper_resource_and_writer_generation()
    {
        var fixture = new Fixture();

        var exchange = fixture.Service.Handle(fixture.ReadRequest("status", ExecutionServiceRequestKind.Status));

        exchange.Response.IsSuccess.Should().BeTrue(exchange.Response.Reason);
        exchange.Response.Resource.Should().Be(Fixture.Resource);
        exchange.Response.ExecutionLeaseId.Should().Be(fixture.Grant.Claim.LeaseId);
        exchange.Response.FencingToken.Should().Be(fixture.Grant.Claim.FencingToken);
        exchange.Response.EventCount.Should().Be(0);
        exchange.Events.Should().BeEmpty();
    }

    [Fact]
    public void Submit_runs_the_same_oms_and_returns_the_durable_lifecycle_batch()
    {
        var fixture = new Fixture();
        fixture.Quote();
        var command = fixture.Submit("filled", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2)));

        var exchange = fixture.Service.Handle(fixture.MutationRequest(
            "submit-filled",
            ExecutionServiceRequestKind.Submit,
            submit: new ExecutionSubmitRequest(command, fixture.RiskContext())));

        exchange.Response.IsSuccess.Should().BeTrue(exchange.Response.Reason);
        exchange.Response.State.Should().Be(OrderLifecycleState.Filled);
        exchange.Events.Select(item => item.Event.Kind).Should().ContainInOrder(
            OrderEventKind.DraftCreated,
            OrderEventKind.RiskAccepted,
            OrderEventKind.Prepared,
            OrderEventKind.Armed,
            OrderEventKind.SendStarted,
            OrderEventKind.SubmissionRecorded,
            OrderEventKind.VenueAcknowledged,
            OrderEventKind.FillReceived);
        exchange.Response.EventCount.Should().Be(exchange.Events.Count);
        fixture.Store.ReadProjection(command.ClientOrderId)!.FilledQuantity.Should().Be(Q(2));
    }

    [Fact]
    public void Exact_request_replay_returns_the_original_exchange_but_conflicting_reuse_fails_closed()
    {
        var fixture = new Fixture();
        fixture.Quote();
        var command = fixture.Submit("dedupe", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1)));
        var request = fixture.MutationRequest(
            "same-request",
            ExecutionServiceRequestKind.Submit,
            submit: new ExecutionSubmitRequest(command, fixture.RiskContext()));

        var first = fixture.Service.Handle(request);
        var eventCount = fixture.Store.Read(command.ClientOrderId).Count;
        var replay = fixture.Service.Handle(request with { });
        var conflict = fixture.Service.Handle(fixture.ReadRequest(
            "same-request",
            ExecutionServiceRequestKind.Resync));

        replay.Should().BeSameAs(first);
        fixture.Store.Read(command.ClientOrderId).Should().HaveCount(eventCount);
        conflict.Response.Fault.Should().Be(ExecutionServiceFault.DuplicateRequestConflict);
        fixture.Store.Read(command.ClientOrderId).Should().HaveCount(eventCount);
    }

    [Fact]
    public void Protocol_resource_payload_and_fencing_failures_are_distinct_and_do_not_mutate()
    {
        var fixture = new Fixture();
        fixture.Quote();
        var command = fixture.Submit("blocked", new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1)));
        var valid = fixture.MutationRequest(
            "valid-shape",
            ExecutionServiceRequestKind.Submit,
            submit: new ExecutionSubmitRequest(command, fixture.RiskContext()));

        var protocol = fixture.Service.Handle(valid with
        {
            RequestId = "bad-version",
            ProtocolVersion = ExecutionServiceProtocol.CurrentVersion + 1,
        });
        var resource = fixture.Service.Handle(valid with
        {
            RequestId = "bad-resource",
            Resource = Fixture.Resource with { TradingAccountId = new TradingAccountId("other") },
        });
        var payload = fixture.Service.Handle(valid with
        {
            RequestId = "bad-payload",
            Submit = null,
        });
        var fencing = fixture.Service.Handle(valid with
        {
            RequestId = "bad-fence",
            FencingToken = new FencingToken(fixture.Grant.Claim.FencingToken.Value + 1),
        });

        protocol.Response.Fault.Should().Be(ExecutionServiceFault.ProtocolVersionMismatch);
        resource.Response.Fault.Should().Be(ExecutionServiceFault.InvalidResource);
        payload.Response.Fault.Should().Be(ExecutionServiceFault.InvalidRequest);
        fencing.Response.Fault.Should().Be(ExecutionServiceFault.StaleFencingToken);
        fixture.Store.Read(command.ClientOrderId).Should().BeEmpty();
    }

    [Fact]
    public void Replace_and_cancel_use_current_sequence_and_emit_normal_venue_confirmations()
    {
        var fixture = new Fixture();
        fixture.Quote();
        var replaceCommand = fixture.Submit(
            "replace",
            new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(90)));
        var working = fixture.SubmitThroughService("submit-replace", replaceCommand);
        working.State.Should().Be(OrderLifecycleState.Working);
        fixture.Advance();
        var replacement = new ReplaceOrderCommand(
            fixture.Metadata("replace", working.LastSequence),
            replaceCommand.OrderId,
            new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(101)));

        var replaced = fixture.Service.Handle(fixture.MutationRequest(
            "replace-working",
            ExecutionServiceRequestKind.Replace,
            replace: new ExecutionReplaceRequest(replacement, fixture.RiskContext())));

        replaced.Response.IsSuccess.Should().BeTrue(replaced.Response.Reason);
        replaced.Response.State.Should().Be(OrderLifecycleState.Filled);
        fixture.Advance();
        var cancelCommand = fixture.Submit(
            "cancel",
            new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(90)));
        var cancellable = fixture.SubmitThroughService("submit-cancel", cancelCommand);
        fixture.Advance();
        var cancel = new CancelOrderCommand(
            fixture.Metadata("cancel", cancellable.LastSequence),
            cancelCommand.OrderId);

        var cancelled = fixture.Service.Handle(fixture.MutationRequest(
            "cancel-working",
            ExecutionServiceRequestKind.Cancel,
            cancel: new ExecutionCancelRequest(cancel)));

        cancelled.Response.IsSuccess.Should().BeTrue(cancelled.Response.Reason);
        cancelled.Response.State.Should().Be(OrderLifecycleState.Cancelled);
        fixture.Store.Read(cancelCommand.ClientOrderId).Select(item => item.Kind)
            .Should().ContainInOrder(OrderEventKind.CancelRequested, OrderEventKind.CancelConfirmed);
    }

    [Fact]
    public void Reconcile_compares_ledger_with_paper_truth_and_resync_is_read_only_after_lease_loss()
    {
        var fixture = new Fixture();
        fixture.Quote();
        var command = fixture.Submit(
            "working",
            new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(90)));
        fixture.SubmitThroughService("submit-working", command);
        var reconcile = fixture.Service.Handle(fixture.MutationRequest(
            "reconcile",
            ExecutionServiceRequestKind.Reconcile,
            trigger: ReconciliationTrigger.OperatorRequest));
        var cursor = reconcile.Response.LastOutboxSequence;
        fixture.Leases.Release(fixture.Grant, fixture.Now).IsSuccess.Should().BeTrue();

        var status = fixture.Service.Handle(fixture.ReadRequest("status-lost", ExecutionServiceRequestKind.Status));
        var resync = fixture.Service.Handle(fixture.ReadRequest(
            "resync-lost",
            ExecutionServiceRequestKind.Resync,
            afterOutboxSequence: 0));
        var emptyTail = fixture.Service.Handle(fixture.ReadRequest(
            "resync-tail",
            ExecutionServiceRequestKind.Resync,
            afterOutboxSequence: cursor));

        reconcile.Response.IsSuccess.Should().BeTrue(reconcile.Response.Reason);
        status.Response.Fault.Should().Be(ExecutionServiceFault.LeaseLost);
        resync.Response.IsSuccess.Should().BeTrue(resync.Response.Reason);
        resync.Events.Should().Contain(item => item.Event.AggregateId == command.ClientOrderId);
        emptyTail.Events.Should().BeEmpty();
    }

    [Fact]
    public void Wrong_or_missing_reconciliation_runner_fails_without_an_oms_mutation()
    {
        var fixture = new Fixture(includeReconciliation: false);
        var exchange = fixture.Service.Handle(fixture.MutationRequest(
            "reconcile-unavailable",
            ExecutionServiceRequestKind.Reconcile,
            trigger: ReconciliationTrigger.OperatorRequest));

        exchange.Response.Fault.Should().Be(ExecutionServiceFault.ReconciliationFailed);
        exchange.Events.Should().BeEmpty();
    }

    [Fact]
    public void Reconciliation_cases_are_cursor_paged_without_retransmitting_order_events()
    {
        var fixture = new Fixture();
        for (var index = 0; index < 130; index++)
            fixture.OpenCase($"case-{index:D3}");

        var first = fixture.Service.Handle(fixture.ReadRequest(
            "cases-first",
            ExecutionServiceRequestKind.ReconciliationCases));
        var second = fixture.Service.Handle(fixture.ReadRequest(
            "cases-second",
            ExecutionServiceRequestKind.ReconciliationCases,
            afterReconciliationCaseId: first.Response.LastReconciliationCaseId));

        first.Response.IsSuccess.Should().BeTrue(first.Response.Reason);
        first.Events.Should().BeEmpty();
        first.Response.EventCount.Should().Be(0);
        first.CaseFacts.Should().HaveCount(ExecutionServiceProtocol.MaximumReconciliationCasesPerExchange);
        first.Response.HasMoreReconciliationCases.Should().BeTrue();
        first.Response.LastReconciliationCaseId.Should().Be(new ReconciliationCaseId("case-127"));
        first.Response.ReconciliationAdmissionBlocked.Should().BeTrue();
        second.CaseFacts.Select(item => item.CaseId.Value).Should().Equal("case-128", "case-129");
        second.Response.HasMoreReconciliationCases.Should().BeFalse();
        second.Events.Should().BeEmpty();
    }

    [Fact]
    public void Reconciliation_resolution_is_lease_fenced_append_only_and_exactly_replayable()
    {
        var fixture = new Fixture();
        var opened = fixture.OpenCase("case-resolution");
        var request = fixture.MutationRequest(
            "resolve-case",
            ExecutionServiceRequestKind.ResolveReconciliationCase,
            resolution: new ExecutionReconciliationResolutionRequest(
                opened.CaseId,
                "paper-operator",
                "Independent ledger and venue snapshots were inspected and now agree."));

        var stale = fixture.Service.Handle(request with
        {
            RequestId = "resolve-case-stale",
            FencingToken = new FencingToken(fixture.Grant.Claim.FencingToken.Value + 1),
        });
        var resolved = fixture.Service.Handle(request);
        var replay = fixture.Service.Handle(request with { });
        var cases = fixture.Service.Handle(fixture.ReadRequest(
            "cases-after-resolution",
            ExecutionServiceRequestKind.ReconciliationCases));

        stale.Response.Fault.Should().Be(ExecutionServiceFault.StaleFencingToken);
        resolved.Response.IsSuccess.Should().BeTrue(resolved.Response.Reason);
        resolved.Response.ReconciliationAdmissionBlocked.Should().BeFalse();
        replay.Should().BeSameAs(resolved);
        fixture.Cases.Read(opened.CaseId).Should().HaveCount(2);
        cases.CaseFacts.Should().ContainSingle().Which.Should().Match<ReconciliationCase>(item =>
            item.CaseId == opened.CaseId &&
            item.Status == ReconciliationCaseStatus.Resolved &&
            item.ResolvedBy == "paper-operator" &&
            item.ResolutionEvidence == "Independent ledger and venue snapshots were inspected and now agree.");
        cases.Response.ReconciliationAdmissionBlocked.Should().BeFalse();
    }

    private static ScaledQuantity Q(long value) => ScaledQuantity.FromWhole(value);
    private static ScaledPrice P(long value) => new(value, 0);
    private static ScaledMoney M(long value) => new(value, 0);

    private sealed class Fixture
    {
        public static readonly ExecutionResource Resource = new(
            new VenueId("paper"),
            new TradingAccountId("service-account"),
            ExecutionEnvironment.SimulatedPaper);
        private static readonly InstrumentId Instrument = new(811);
        private readonly MutableClock _clock = new(new DateTime(2026, 8, 24, 1, 0, 0, DateTimeKind.Utc));
        private int _orderSequence;

        public Fixture(bool includeReconciliation = true)
        {
            Store = new InMemoryOrderEventStore();
            Venue = new DeterministicPaperVenue();
            Leases = new InMemoryExecutionLeaseStore();
            Grant = Leases.Acquire(
                Resource,
                new ExecutionLeaseId("service-lease"),
                new RuntimeInstanceId("service-runtime"),
                Now,
                Now.AddHours(1)).Grant!.Value;
            Cases = new InMemoryReconciliationCaseStore();
            var reconciliationEngine = new ReconciliationEngine(Cases);
            Oms = new OrderManagementService(Store, Venue, Leases, _clock, reconciliationEngine);
            var runner = includeReconciliation
                ? new PaperExecutionServiceReconciliationRunner(
                    Store,
                    Cases,
                    Venue,
                    reconciliationEngine,
                    _clock)
                : null;
            Service = new ExecutionServiceEngine(Store, Oms, Leases, Grant, _clock, runner);
        }

        public InMemoryOrderEventStore Store { get; }
        public DeterministicPaperVenue Venue { get; }
        public InMemoryReconciliationCaseStore Cases { get; }
        public InMemoryExecutionLeaseStore Leases { get; }
        public ExecutionLeaseGrant Grant { get; }
        public OrderManagementService Oms { get; }
        public ExecutionServiceEngine Service { get; }
        public DateTimeOffset Now => new(_clock.UtcNow);

        public void Advance() => _clock.UtcNow = _clock.UtcNow.AddSeconds(1);

        public void Quote() => Venue.OnMarket(new PaperMarketSnapshot(
            Instrument,
            P(99),
            P(100),
            Q(100),
            Now));

        public ExecutionServiceRequest ReadRequest(
            string requestId,
            ExecutionServiceRequestKind kind,
            long afterOutboxSequence = 0,
            ReconciliationCaseId? afterReconciliationCaseId = null) =>
            new(
                ExecutionServiceProtocol.CurrentVersion,
                requestId,
                kind,
                Resource,
                Grant.Claim.LeaseId,
                Grant.Claim.FencingToken,
                afterOutboxSequence,
                AfterReconciliationCaseId: afterReconciliationCaseId);

        public ExecutionServiceRequest MutationRequest(
            string requestId,
            ExecutionServiceRequestKind kind,
            ExecutionSubmitRequest? submit = null,
            ExecutionCancelRequest? cancel = null,
            ExecutionReplaceRequest? replace = null,
            ReconciliationTrigger? trigger = null,
            ExecutionReconciliationResolutionRequest? resolution = null) =>
            new(
                ExecutionServiceProtocol.CurrentVersion,
                requestId,
                kind,
                Resource,
                Grant.Claim.LeaseId,
                Grant.Claim.FencingToken,
                Submit: submit,
                Cancel: cancel,
                Replace: replace,
                ReconciliationTrigger: trigger,
                ReconciliationResolution: resolution);

        public ReconciliationCase OpenCase(string caseId)
        {
            var opened = new ReconciliationCase(
                new ReconciliationCaseId(caseId),
                Resource,
                ReconciliationSubjectKind.Position,
                $"instrument:{Instrument.Value}",
                null,
                ReconciliationCaseKind.PositionMismatch,
                ReconciliationCaseStatus.Open,
                "ledger quantity=2",
                "venue quantity=1",
                Now);
            Cases.TryAppend(opened).Should().BeTrue();
            return opened;
        }

        public OmsOrderProjection SubmitThroughService(string requestId, SubmitOrderCommand command)
        {
            var exchange = Service.Handle(MutationRequest(
                requestId,
                ExecutionServiceRequestKind.Submit,
                submit: new ExecutionSubmitRequest(command, RiskContext())));
            exchange.Response.IsSuccess.Should().BeTrue(exchange.Response.Reason);
            return Store.ReadProjection(command.ClientOrderId)!;
        }

        public SubmitOrderCommand Submit(string suffix, OrderTerms terms)
        {
            var sequence = ++_orderSequence;
            var metadata = Metadata($"submit-{suffix}-{sequence}", 0);
            var clientOrderId = new ClientOrderId($"client-{suffix}-{sequence}");
            var signed = terms.Side == OrderSide.Buy
                ? terms.Quantity
                : new ScaledQuantity(-terms.Quantity.Coefficient, terms.Quantity.Scale);
            var mapping = new CanonicalInstructionMappingContext(
                new IntentId($"intent-{suffix}-{sequence}"),
                null,
                new LegId($"leg-{suffix}-{sequence}"),
                Grant.Claim.LeaseId,
                Grant.Claim.FencingToken,
                TradeIntentQuantityMode.Delta,
                signed,
                ScaledQuantity.Zero,
                null,
                null,
                ScaledMoney.Zero,
                sequence,
                "service-policy",
                terms.LimitPrice,
                terms.StopPrice);
            CanonicalOrderInstructionMapper.TryCreate(
                metadata,
                clientOrderId,
                terms,
                mapping,
                out var instruction).Should().Be(OrderDomainFault.None);
            return new SubmitOrderCommand(
                metadata,
                new OrderId($"order-{suffix}-{sequence}"),
                clientOrderId,
                terms,
                instruction!);
        }

        public ExecutionCommandMetadata Metadata(string suffix, long expectedSequence) =>
            new(
                new CommandId($"command-{suffix}"),
                new CorrelationId("service-correlation"),
                new CausationId($"cause-{suffix}"),
                Resource.TradingAccountId,
                new StrategyId("service-strategy"),
                new StrategyVersion("1.0.0"),
                Resource.VenueId,
                Instrument,
                Resource.Environment,
                Now,
                expectedSequence);

        public RiskEvaluationContext RiskContext() =>
            new(
                new RiskLimits(Q(1_000), Q(1_000), M(10_000_000), ScaledMoney.Zero,
                    M(1_000_000), M(1_000_000), 1_000, TimeSpan.FromMinutes(1)),
                RiskControlMode.Active,
                false,
                ScaledQuantity.Zero,
                ScaledQuantity.Zero,
                ScaledQuantity.Zero,
                ScaledMoney.Zero,
                ScaledQuantity.Zero,
                ScaledMoney.Zero,
                ScaledQuantity.Zero,
                M(10_000_000),
                ScaledMoney.Zero,
                M(1_000_000),
                M(1_000_000),
                P(100),
                0,
                Now);
    }

    private sealed class MutableClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }
}
