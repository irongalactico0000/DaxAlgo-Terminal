using FluentAssertions;
using TradingTerminal.Backtest.Engine;
using TradingTerminal.Backtest.Engine.Accounting;
using TradingTerminal.Backtest.Engine.Cost;
using TradingTerminal.Backtest.Engine.Execution;
using TradingTerminal.Backtest.Engine.TradeIr;
using TradingTerminal.Core.Backtesting;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Strategies.Definition;
using TradingTerminal.Core.Trading;
using TradingTerminal.TradeIr.Runtime;
using Xunit;

namespace TradingTerminal.Tests.Backtesting;

public sealed class TradeIrRiskGatewayV1Tests
{
    [Fact]
    public void Allowed_intent_records_policy_before_exactly_one_book_submission_and_fill()
    {
        using var fixture = CreateFixture();
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, bid: 99d, ask: 101d));
        var decisionsVisibleAtEffect = new List<int>();
        fixture.Book.Event += (_, _) => decisionsVisibleAtEffect.Add(fixture.Gateway.Decisions.Count);

        var admission = fixture.Gateway.Admit(Intent(
            fixture,
            sequence: 1,
            target: 5,
            side: TradeIrOrderSideV1.Buy,
            quantity: 5,
            reduceOnly: false,
            quoteTime));

        admission.IsAdmitted.Should().BeTrue();
        admission.Decision.Code.Should().Be(TradeIrGatewayDecisionCodesV1.Allowed);
        admission.Decision.RiskDecision!.Code.Should().Be(RiskDecisionCode.Allowed);
        admission.Decision.PolicyEvidence!.PolicyVersion.Should().Be(RiskPolicy.PolicyVersion);
        admission.Command!.Metadata.InstrumentId.Should().Be(fixture.Instrument.Id);
        admission.Command.Metadata.TradingAccountId.Should().Be(fixture.Policy.AccountId);
        admission.Command.Terms.Quantity.Should().Be(ScaledQuantity.FromWhole(5));
        fixture.Gateway.SubmittedOrderCount.Should().Be(1);
        decisionsVisibleAtEffect.Should().ContainSingle().Which.Should().Be(1,
            "the append-only policy observation must exist before the book sees the order");

        fixture.Gateway.DrainFeedback().Should().ContainSingle()
            .Which.Status.Should().Be(TradeIrOrderFeedbackStatusV1.Working);

        fixture.ObserveQuote(Quote(quoteTime.AddSeconds(1), bid: 109d, ask: 111d));

        fixture.Portfolio.SnapshotOf(fixture.Instrument.Id).Quantity.Should().Be(5);
        fixture.Gateway.DrainFeedback().Should().ContainSingle()
            .Which.Status.Should().Be(TradeIrOrderFeedbackStatusV1.Filled);
    }

    [Theory]
    [InlineData(DenialKind.KillSwitch, RiskDecisionCode.KillSwitchActive)]
    [InlineData(DenialKind.MaximumOrder, RiskDecisionCode.MaximumOrderQuantityExceeded)]
    [InlineData(DenialKind.BuyingPower, RiskDecisionCode.InsufficientBuyingPower)]
    public void Host_risk_denial_returns_terminal_feedback_and_never_submits(
        DenialKind denial,
        RiskDecisionCode expectedCode)
    {
        using var fixture = denial switch
        {
            DenialKind.KillSwitch => CreateFixture(killSwitch: true),
            DenialKind.MaximumOrder => CreateFixture(limits: Limits(maximumOrderQuantity: 1m)),
            DenialKind.BuyingPower => CreateFixture(startingCash: 100d),
            _ => throw new ArgumentOutOfRangeException(nameof(denial)),
        };
        var effects = 0;
        fixture.Book.Event += (_, _) => effects++;
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 99d, 101d));

        var admission = fixture.Gateway.Admit(Intent(
            fixture,
            sequence: 1,
            target: 5,
            side: TradeIrOrderSideV1.Buy,
            quantity: 5,
            reduceOnly: false,
            quoteTime));

        admission.IsAdmitted.Should().BeFalse();
        admission.Command.Should().NotBeNull("structurally valid intent reached host policy evaluation");
        admission.Submission.Should().BeNull();
        admission.Decision.Code.Should().Be(TradeIrGatewayDecisionCodesV1.RiskDenied);
        admission.Decision.RiskDecision!.Code.Should().Be(expectedCode);
        admission.Decision.PolicyEvidence.Should().NotBeNull();
        fixture.Gateway.SubmittedOrderCount.Should().Be(0);
        effects.Should().Be(0);
        fixture.Gateway.DrainFeedback().Should().ContainSingle().Which.Should().Match<TradeIrOrderFeedbackV1>(
            feedback => feedback.IntentSequence == 1 &&
                        feedback.Status == TradeIrOrderFeedbackStatusV1.Denied &&
                        feedback.CumulativeFilledQuantity == 0);
    }

    [Fact]
    public void Forged_delta_is_rejected_before_command_or_policy_and_cannot_reach_the_book()
    {
        using var fixture = CreateFixture();
        var effects = 0;
        fixture.Book.Event += (_, _) => effects++;
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 99d, 101d));
        var forged = Intent(
            fixture,
            sequence: 1,
            target: 5,
            side: TradeIrOrderSideV1.Sell,
            quantity: 5,
            reduceOnly: false,
            quoteTime);

        var admission = fixture.Gateway.Admit(forged);

        admission.IsAdmitted.Should().BeFalse();
        admission.Decision.Code.Should().Be(TradeIrGatewayDecisionCodesV1.DeltaMismatch);
        admission.Decision.RiskDecision.Should().BeNull();
        admission.Decision.PolicyEvidence.Should().BeNull();
        admission.Command.Should().BeNull();
        effects.Should().Be(0);
        fixture.Gateway.SubmittedOrderCount.Should().Be(0);
    }

    [Fact]
    public void Intent_from_a_different_admission_manifest_is_rejected_before_effect()
    {
        using var fixture = CreateFixture();
        var effects = 0;
        fixture.Book.Event += (_, _) => effects++;
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 99d, 101d));
        var foreignAdmissionIntent = Intent(
            fixture,
            sequence: 1,
            target: 5,
            side: TradeIrOrderSideV1.Buy,
            quantity: 5,
            reduceOnly: false,
            quoteTime,
            admissionManifestSha256: new string('f', 64));

        var admission = fixture.Gateway.Admit(foreignAdmissionIntent);

        admission.IsAdmitted.Should().BeFalse();
        admission.Decision.Code.Should().Be(TradeIrGatewayDecisionCodesV1.AdmissionManifestMismatch);
        admission.Decision.RiskDecision.Should().BeNull();
        admission.Decision.PolicyEvidence.Should().BeNull();
        admission.Command.Should().BeNull();
        effects.Should().Be(0);
        fixture.Gateway.SubmittedOrderCount.Should().Be(0);
    }

    [Fact]
    public void Working_reservation_and_attempt_rate_are_host_derived_and_second_intent_is_denied()
    {
        using var fixture = CreateFixture(limits: Limits(maximumExposureCommandsPerWindow: 1));
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 99d, 101d));
        fixture.Gateway.Admit(Intent(
            fixture,
            sequence: 1,
            target: 5,
            side: TradeIrOrderSideV1.Buy,
            quantity: 5,
            reduceOnly: false,
            quoteTime)).IsAdmitted.Should().BeTrue();
        fixture.Gateway.DrainFeedback();

        var second = fixture.Gateway.Admit(Intent(
            fixture,
            sequence: 2,
            target: 10,
            side: TradeIrOrderSideV1.Buy,
            quantity: 10,
            reduceOnly: false,
            quoteTime));

        second.IsAdmitted.Should().BeFalse();
        second.Decision.RiskDecision!.Code.Should().Be(RiskDecisionCode.RateLimitExceeded);
        second.Decision.PolicyEvidence!.Context.CurrentBuyReservedQuantity.Should().Be(ScaledQuantity.FromWhole(5));
        second.Decision.PolicyEvidence.Context.CurrentGrossReservedNotional.Should().Be(new ScaledMoney(505, 0));
        fixture.Gateway.SubmittedOrderCount.Should().Be(1);
    }

    [Fact]
    public void Aggregate_realized_pnl_survives_flat_position_for_future_risk_contexts()
    {
        using var fixture = CreateFixture();
        var firstTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(firstTime, 99d, 101d));
        fixture.Gateway.Admit(Intent(
            fixture, 1, 5, TradeIrOrderSideV1.Buy, 5, reduceOnly: false, firstTime));
        fixture.Gateway.DrainFeedback();

        var entryFillTime = firstTime.AddSeconds(1);
        fixture.ObserveQuote(Quote(entryFillTime, 109d, 111d));
        fixture.Gateway.DrainFeedback();
        fixture.Gateway.Admit(Intent(
            fixture, 2, 0, TradeIrOrderSideV1.Sell, 5, reduceOnly: true, entryFillTime));
        fixture.Gateway.DrainFeedback();

        fixture.ObserveQuote(Quote(entryFillTime.AddSeconds(1), 120d, 121d));

        fixture.Portfolio.SnapshotOf(fixture.Instrument.Id).IsFlat.Should().BeTrue();
        fixture.Portfolio.TotalRealizedPnl.Should().Be(45d);
    }

    [Theory]
    [InlineData(true, OrderState.Working)]
    [InlineData(true, OrderState.Filled)]
    [InlineData(true, OrderState.Cancelled)]
    [InlineData(false, OrderState.Working)]
    [InlineData(false, OrderState.Filled)]
    [InlineData(false, OrderState.Cancelled)]
    public void Throwing_diagnostic_observer_is_isolated_from_required_order_transitions(
        bool subscribeBeforeRequiredBinding,
        OrderState throwAtState)
    {
        var throwingObserver = new ThrowOnceDiagnosticObserver(throwAtState);
        var survivingObserverStates = new List<OrderState>();
        void Subscribe(SimulatedOrderBook book)
        {
            book.Event += throwingObserver.Observe;
            book.Event += (_, orderEvent) => survivingObserverStates.Add(orderEvent.State);
        }

        using var fixture = CreateFixture(
            configureBookBeforeGateway: subscribeBeforeRequiredBinding ? Subscribe : null);
        if (!subscribeBeforeRequiredBinding) Subscribe(fixture.Book);
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 99d, 101d));

        fixture.Gateway.Admit(Intent(
            fixture,
            sequence: 1,
            target: 5,
            side: TradeIrOrderSideV1.Buy,
            quantity: 5,
            reduceOnly: false,
            quoteTime)).IsAdmitted.Should().BeTrue();
        fixture.Gateway.DrainFeedback().Select(static feedback =>
            (feedback.Status, feedback.CumulativeFilledQuantity)).Should().Equal(
            (TradeIrOrderFeedbackStatusV1.Working, 0L));

        var fillTime = quoteTime.AddSeconds(1);
        fixture.ObserveQuote(Quote(fillTime, 109d, 111d));

        fixture.Portfolio.SnapshotOf(fixture.Instrument.Id).Quantity.Should().Be(5);
        fixture.Portfolio.Trades.Should().BeEmpty(
            "an entry fill opens a lot; round-trip trades are recorded only when a lot closes");
        fixture.Gateway.DrainFeedback().Should().ContainSingle()
            .Which.Status.Should().Be(TradeIrOrderFeedbackStatusV1.Filled);

        var cancellation = fixture.Gateway.Admit(Intent(
            fixture,
            sequence: 2,
            target: 0,
            side: TradeIrOrderSideV1.Sell,
            quantity: 5,
            reduceOnly: true,
            fillTime));
        cancellation.IsAdmitted.Should().BeTrue();
        fixture.Gateway.DrainFeedback().Should().ContainSingle()
            .Which.Status.Should().Be(TradeIrOrderFeedbackStatusV1.Working);

        fixture.Book.Cancel(cancellation.Command!.ClientOrderId.Value);

        fixture.Gateway.DrainFeedback().Should().ContainSingle()
            .Which.Status.Should().Be(TradeIrOrderFeedbackStatusV1.Cancelled);
        fixture.Portfolio.SnapshotOf(fixture.Instrument.Id).Quantity.Should().Be(5);
        var afterCancelTime = fillTime.AddSeconds(1);
        fixture.ObserveQuote(Quote(afterCancelTime, 120d, 121d));
        fixture.Portfolio.SnapshotOf(fixture.Instrument.Id).Quantity.Should().Be(5,
            "the cancelled terminal order must already be absent from the book");

        var afterCancellation = fixture.Gateway.Admit(Intent(
            fixture,
            sequence: 3,
            target: 6,
            side: TradeIrOrderSideV1.Buy,
            quantity: 1,
            reduceOnly: false,
            afterCancelTime));
        afterCancellation.IsAdmitted.Should().BeTrue();
        afterCancellation.Decision.PolicyEvidence!.Context.CurrentSellReservedQuantity.Should().Be(ScaledQuantity.Zero);
        afterCancellation.Decision.PolicyEvidence.Context.CurrentGrossReservedNotional.Should().Be(ScaledMoney.Zero);

        var expectedStates = new[]
        {
            OrderState.Working,
            OrderState.Filled,
            OrderState.Working,
            OrderState.Cancelled,
            OrderState.Working,
        };
        throwingObserver.States.Should().Equal(expectedStates);
        survivingObserverStates.Should().Equal(expectedStates,
            "one failing diagnostic observer cannot suppress later observers");
        fixture.Book.DiagnosticFailures.Should().ContainSingle().Which.Should().Match<SimulatedOrderBookDiagnosticFailure>(
            failure => failure.Sequence == 1 &&
                       failure.State == throwAtState &&
                       failure.Message == "throw-once observer");
        fixture.Gateway.SubmittedOrderCount.Should().Be(3);
        fixture.Gateway.Decisions.Select(static decision => decision.Code)
            .Should().OnlyContain(code => code == TradeIrGatewayDecisionCodesV1.Allowed);
    }

    [Fact]
    public void Backward_quote_is_rejected_before_clock_book_or_accounting_mutation()
    {
        using var fixture = CreateFixture();
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 99d, 101d));
        fixture.Gateway.Admit(Intent(
            fixture, 1, 5, TradeIrOrderSideV1.Buy, 5, reduceOnly: false, quoteTime));
        fixture.Gateway.DrainFeedback();
        var bookEvents = 0;
        fixture.Book.Event += (_, _) => bookEvents++;
        var cashBefore = fixture.Portfolio.Cash;

        var observe = () => fixture.ObserveQuote(
            Quote(quoteTime.AddMilliseconds(-1), 109d, 111d));

        observe.Should().Throw<InvalidOperationException>().WithMessage("*cannot move backward*");
        fixture.Clock.UtcNow.Should().Be(quoteTime);
        bookEvents.Should().Be(0);
        fixture.Portfolio.SnapshotOf(fixture.Instrument.Id).IsFlat.Should().BeTrue();
        fixture.Portfolio.Cash.Should().Be(cashBefore);
        fixture.Gateway.DrainFeedback().Should().BeEmpty();

        fixture.ObserveQuote(Quote(quoteTime.AddSeconds(1), 109d, 111d));
        bookEvents.Should().Be(1, "the valid pending order remains live until a forward quote");
        fixture.Portfolio.SnapshotOf(fixture.Instrument.Id).Quantity.Should().Be(5);
    }

    [Fact]
    public void Equal_event_times_are_ordered_by_increasing_source_sequence()
    {
        using var fixture = CreateFixture();
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 99d, 101d), sourceSequence: 10);

        var nextAtSameTime = () => fixture.ObserveQuote(
            Quote(quoteTime, 100d, 102d),
            sourceSequence: 11);
        nextAtSameTime.Should().NotThrow();

        var duplicateSequence = () => fixture.ObserveQuote(
            Quote(quoteTime, 101d, 103d),
            sourceSequence: 11);
        duplicateSequence.Should().Throw<InvalidOperationException>()
            .WithMessage("*source sequence*increase strictly*");
    }

    [Fact]
    public void Stale_same_time_intent_source_sequence_is_rejected_without_consuming_intent_sequence()
    {
        using var fixture = CreateFixture();
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 99d, 101d), sourceSequence: 10);
        var stale = Intent(
            fixture,
            sequence: 1,
            target: 5,
            side: TradeIrOrderSideV1.Buy,
            quantity: 5,
            reduceOnly: false,
            quoteTime,
            sourceEventSequence: 10);
        fixture.ObserveQuote(Quote(quoteTime, 100d, 102d), sourceSequence: 11);

        var rejection = fixture.Gateway.Admit(stale);

        rejection.IsAdmitted.Should().BeFalse();
        rejection.Decision.Code.Should().Be(TradeIrGatewayDecisionCodesV1.SourceSequenceMismatch);
        rejection.Command.Should().BeNull();
        fixture.Gateway.SubmittedOrderCount.Should().Be(0);

        var current = fixture.Gateway.Admit(Intent(
            fixture,
            sequence: 1,
            target: 5,
            side: TradeIrOrderSideV1.Buy,
            quantity: 5,
            reduceOnly: false,
            quoteTime,
            sourceEventSequence: 11));
        current.IsAdmitted.Should().BeTrue("a stale frame cannot consume the next intent sequence");
    }

    [Fact]
    public void Decimal_overflow_quote_is_rejected_before_clock_book_or_accounting_mutation()
    {
        using var fixture = CreateFixture();
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 99d, 101d));
        fixture.Gateway.Admit(Intent(
            fixture, 1, 5, TradeIrOrderSideV1.Buy, 5, reduceOnly: false, quoteTime));
        fixture.Gateway.DrainFeedback();
        var bookEvents = 0;
        fixture.Book.Event += (_, _) => bookEvents++;
        var cashBefore = fixture.Portfolio.Cash;

        var observe = () => fixture.ObserveQuote(
            Quote(quoteTime.AddSeconds(1), 109d, 1e30d));

        observe.Should().Throw<InvalidOperationException>().WithMessage("*ask price*finite decimal*");
        fixture.Clock.UtcNow.Should().Be(quoteTime);
        bookEvents.Should().Be(0);
        fixture.Portfolio.SnapshotOf(fixture.Instrument.Id).IsFlat.Should().BeTrue();
        fixture.Portfolio.Cash.Should().Be(cashBefore);
        fixture.Gateway.DrainFeedback().Should().BeEmpty();

        fixture.ObserveQuote(Quote(quoteTime.AddSeconds(1), 109d, 111d));
        bookEvents.Should().Be(1, "the invalid quote cannot consume or mutate the pending order");
        fixture.Portfolio.SnapshotOf(fixture.Instrument.Id).Quantity.Should().Be(5);
    }

    [Fact]
    public void Huge_target_and_notional_overflow_return_stable_maximum_order_denial()
    {
        using var fixture = CreateFixture(contractMultiplier: 1e20d);
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 1e20d, 1e20d));
        var maximumTarget = TradeIrRuntimeLimitsV1.MaximumAbsolutePositionQuantity;

        var admit = () => fixture.Gateway.Admit(Intent(
            fixture,
            sequence: 1,
            target: maximumTarget,
            side: TradeIrOrderSideV1.Buy,
            quantity: maximumTarget,
            reduceOnly: false,
            quoteTime));

        var admission = admit.Should().NotThrow().Which;
        admission.IsAdmitted.Should().BeFalse();
        admission.Decision.RiskDecision!.Code.Should().Be(RiskDecisionCode.MaximumOrderQuantityExceeded);
        admission.Decision.RiskDecision.ProjectedGrossNotional.Should().Be(new ScaledMoney(long.MaxValue, 0));
        admission.Submission.Should().BeNull();
        fixture.Gateway.SubmittedOrderCount.Should().Be(0);
    }

    [Fact]
    public void Maximum_rate_window_saturates_at_minimum_instant_without_throwing()
    {
        using var fixture = CreateFixture(limits: Limits(rateLimitWindow: TimeSpan.MaxValue));
        var quoteTime = DateTime.UnixEpoch.AddSeconds(1);
        fixture.ObserveQuote(Quote(quoteTime, 99d, 101d));

        var admit = () => fixture.Gateway.Admit(Intent(
            fixture,
            sequence: 1,
            target: 5,
            side: TradeIrOrderSideV1.Buy,
            quantity: 5,
            reduceOnly: false,
            quoteTime));

        admit.Should().NotThrow().Which.IsAdmitted.Should().BeTrue();
    }

    [Fact]
    public void Constructor_rejects_plan_or_host_instrument_binding_drift()
    {
        var wrongPlanKey = () => CreateFixture(planInstrumentKey: "equity/xnas/BETA");
        var wrongSymbol = () => CreateFixture(contract: Contract.UsStock("BETA", "XNAS"));
        var wrongVenue = () => CreateFixture(contract: Contract.UsStock("S1", "XNYS"));

        wrongPlanKey.Should().Throw<ArgumentException>().WithMessage("*exactly one portable instrument*");
        wrongSymbol.Should().Throw<ArgumentException>().WithMessage("*does not exactly resolve portable instrument*");
        wrongVenue.Should().Throw<ArgumentException>().WithMessage("*does not exactly resolve portable instrument*");
    }

    [Theory]
    [MemberData(nameof(InvalidContractMultipliers))]
    public void Constructor_rejects_nonpositive_nonfinite_or_nondecimal_contract_multiplier(double multiplier)
    {
        var create = () => CreateFixture(contractMultiplier: multiplier);

        create.Should().Throw<ArgumentOutOfRangeException>()
            .Where(exception => exception.ParamName == "instrument");
    }

    [Fact]
    public void Constructor_rejects_empty_host_account_and_venue_identifiers()
    {
        var emptyAccount = () => CreateFixture(accountId: default(TradingAccountId));
        var emptyVenue = () => CreateFixture(venueId: default(VenueId));

        emptyAccount.Should().Throw<ArgumentException>().WithMessage("*trading account id*");
        emptyVenue.Should().Throw<ArgumentException>().WithMessage("*venue id*");
    }

    public static IEnumerable<object[]> InvalidContractMultipliers()
    {
        yield return [0d];
        yield return [-1d];
        yield return [double.Epsilon];
        yield return [double.NaN];
        yield return [double.PositiveInfinity];
        yield return [double.NegativeInfinity];
        yield return [1e30d];
    }

    private static Fixture CreateFixture(
        bool killSwitch = false,
        RiskLimits? limits = null,
        double startingCash = 100_000d,
        string? planInstrumentKey = null,
        Contract? contract = null,
        double contractMultiplier = 1d,
        TradingAccountId? accountId = null,
        VenueId? venueId = null,
        Action<SimulatedOrderBook>? configureBookBeforeGateway = null)
    {
        var admitted = BacktestTradeIrTargetV1Tests.CreateFixture();
        var definition = admitted.Definition;
        var compilation = TradeIrExecutionPlanCompilerV1.Compile(
            definition,
            admitted.Target,
            admitted.Pins,
            [admitted.Capability],
            [admitted.Binding]);
        compilation.Succeeded.Should().BeTrue();
        var compiledPlan = compilation.Plan!;
        var requirement = definition.DataRequirements.Single();
        var portableInstrument = requirement.InstrumentSelector.References.Single();
        var instrumentKey = portableInstrument.InstrumentKey;
        var plan = planInstrumentKey is null
            ? compiledPlan
            : new CompiledTradeIrPlanV1(
                compiledPlan.DefinitionSha256,
                compiledPlan.AdmissionManifestSha256,
                compiledPlan.RuntimeSemanticsVersion,
                planInstrumentKey,
                compiledPlan.Instructions,
                compiledPlan.OrderIntentOutputId,
                compiledPlan.OrderIntentNodeId,
                compiledPlan.FlattenOnEnd);
        var instrument = new InstrumentSpec(
            new InstrumentId(101),
            contract ?? Contract.UsStock(portableInstrument.Symbol, portableInstrument.Venue),
            TickSize: 0.01,
            ContractMultiplier: contractMultiplier);
        var clock = new SimClock();
        var book = new SimulatedOrderBook(clock, new L1TouchFillModel(slippageTicks: 0), _ => 0.01);
        configureBookBeforeGateway?.Invoke(book);
        var portfolio = new Portfolio(
            startingCash,
            new Dictionary<InstrumentId, double> { [instrument.Id] = instrument.ContractMultiplier },
            FeeModels.From(new CostSpec()));
        var policy = new BacktestTradeIrHostPolicyV1(
            "risk/backtest-default-v1",
            accountId ?? new TradingAccountId("account/backtest"),
            venueId ?? new VenueId("venue/simulated"),
            limits ?? Limits(),
            RiskControlMode.Active,
            killSwitch);
        var gateway = new TradeIrRiskGatewayV1(
            compilation.AdmissionManifest!,
            plan,
            instrument,
            policy,
            book,
            portfolio,
            clock);
        return new Fixture(definition, plan, instrument, policy, clock, book, portfolio, gateway);
    }

    private static RiskLimits Limits(
        decimal maximumOrderQuantity = 100m,
        int maximumExposureCommandsPerWindow = 100,
        TimeSpan? rateLimitWindow = null) => new(
        ExecutionNumericBoundary.QuantityFromDecimal(maximumOrderQuantity),
        maximumAbsolutePosition: ScaledQuantity.FromWhole(100),
        maximumGrossNotional: new ScaledMoney(1_000_000, 0),
        minimumBuyingPower: ScaledMoney.Zero,
        maximumDailyLoss: new ScaledMoney(10_000, 0),
        maximumDrawdown: new ScaledMoney(10_000, 0),
        maximumExposureCommandsPerWindow,
        rateLimitWindow: rateLimitWindow ?? TimeSpan.FromMinutes(1));

    private static TradeIrOrderIntentV1 Intent(
        Fixture fixture,
        long sequence,
        long target,
        TradeIrOrderSideV1 side,
        long quantity,
        bool reduceOnly,
        DateTime quoteTime,
        long? sourceEventSequence = null,
        string? admissionManifestSha256 = null) => new(
        fixture.Plan.DefinitionSha256,
        admissionManifestSha256 ?? fixture.Plan.AdmissionManifestSha256,
        sequence,
        sourceEventSequence ?? fixture.CurrentSourceSequence,
        fixture.Plan.OrderIntentOutputId,
        fixture.Plan.OrderIntentNodeId,
        fixture.Plan.InstrumentKey,
        side,
        quantity,
        target,
        TradeIrTimeInForceV1.Day,
        reduceOnly,
        UnixMicroseconds(quoteTime));

    private static Tick Quote(DateTime time, double bid, double ask) =>
        new(time, bid, ask, BidSize: 10, AskSize: 10);

    private static long UnixMicroseconds(DateTime time) => checked(
        (time.Ticks - DateTime.UnixEpoch.Ticks) / 10);

    public enum DenialKind
    {
        KillSwitch,
        MaximumOrder,
        BuyingPower,
    }

    private sealed record Fixture(
        StrategyIntermediateRepresentationV1 Definition,
        CompiledTradeIrPlanV1 Plan,
        InstrumentSpec Instrument,
        BacktestTradeIrHostPolicyV1 Policy,
        SimClock Clock,
        SimulatedOrderBook Book,
        Portfolio Portfolio,
        TradeIrRiskGatewayV1 Gateway) : IDisposable
    {
        private long _nextSourceSequence;

        public long CurrentSourceSequence => _nextSourceSequence;

        public void ObserveQuote(Tick quote) => ObserveQuote(quote, checked(_nextSourceSequence + 1));

        public void ObserveQuote(Tick quote, long sourceSequence)
        {
            Gateway.ObserveQuote(quote, sourceSequence);
            _nextSourceSequence = sourceSequence;
        }

        public void Dispose() => Gateway.Dispose();
    }

    private sealed class ThrowOnceDiagnosticObserver
    {
        private readonly OrderState _throwAtState;
        private bool _hasThrown;

        public ThrowOnceDiagnosticObserver(OrderState throwAtState)
        {
            _throwAtState = throwAtState;
        }

        public List<OrderState> States { get; } = [];

        public void Observe(InstrumentId _, OrderEvent orderEvent)
        {
            States.Add(orderEvent.State);
            if (_hasThrown || orderEvent.State != _throwAtState) return;
            _hasThrown = true;
            throw new InvalidOperationException("throw-once observer");
        }
    }
}
