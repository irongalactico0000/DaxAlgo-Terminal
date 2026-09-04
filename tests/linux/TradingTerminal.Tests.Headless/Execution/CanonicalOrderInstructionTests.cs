using FluentAssertions;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Trading;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class CanonicalOrderInstructionTests
{
    private static readonly DateTimeOffset Now = DateTime.UnixEpoch.AddHours(2);

    [Fact]
    public void Target_reversal_binds_every_identity_provenance_and_economic_assumption()
    {
        var metadata = Metadata("strategy-alpha");
        var terms = new OrderTerms(
            OrderSide.Sell,
            OrderType.StopLimit,
            Q(8),
            limitPrice: P(99),
            stopPrice: P(100),
            timeInForce: TimeInForce.Gtc);
        var mapping = Context(
            TradeIntentQuantityMode.TargetPosition,
            signedUnits: Q(-3),
            currentPosition: Q(5),
            protectiveStopPrice: P(105),
            profitTargetPrice: P(90),
            cost: new ScaledMoney(25, 2),
            entryLimitPrice: P(99),
            entryStopPrice: P(100));

        var fault = CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            new ClientOrderId("client-reversal"),
            terms,
            mapping,
            out var instruction);

        fault.Should().Be(OrderDomainFault.None);
        instruction!.Validate().Should().Be(OrderDomainFault.None);
        instruction.Identity.Should().BeEquivalentTo(new
        {
            IntentId = new IntentId("intent-test"),
            LegId = new LegId("leg-test"),
            ClientOrderId = new ClientOrderId("client-reversal"),
            CorrelationId = metadata.CorrelationId,
            CausationId = metadata.CausationId!.Value,
            ExecutionLeaseId = new ExecutionLeaseId("lease-test"),
            FencingToken = new FencingToken(7),
        });
        instruction.TradeIntent.Should().BeEquivalentTo(new
        {
            Instrument = metadata.InstrumentId,
            QuantityMode = TradeIntentQuantityMode.TargetPosition,
            SignedUnits = Q(-3),
            ProtectiveStopPrice = (ScaledPrice?)P(105),
            ProfitTargetPrice = (ScaledPrice?)P(90),
            EstimatedRoundTripCostPerUnit = new ScaledMoney(25, 2),
            StrategyId = "strategy-alpha",
            StrategyNoteId = 501L,
            PolicyVersion = "policy-test-v1",
            EntryLimitPrice = (ScaledPrice?)P(99),
            EntryStopPrice = (ScaledPrice?)P(100),
        });
        instruction.Terms.Should().Be(new CanonicalOrderTerms(
            OrderSide.Sell,
            CanonicalOrderType.StopLimit,
            CanonicalTimeInForce.GoodTillCancelled,
            Q(8),
            P(99),
            P(100)));
    }

    [Fact]
    public void Numerically_equal_but_differently_encoded_entry_price_is_rejected()
    {
        var terms = new OrderTerms(
            OrderSide.Buy,
            OrderType.Limit,
            Q(1),
            limitPrice: P(10));
        var mapping = Context(
            TradeIntentQuantityMode.Delta,
            signedUnits: Q(1),
            entryLimitPrice: new ScaledPrice(100, 1));

        var fault = CanonicalOrderInstructionMapper.TryCreate(
            Metadata(),
            new ClientOrderId("client-price-shape"),
            terms,
            mapping,
            out var instruction);

        fault.Should().Be(OrderDomainFault.InvalidTradeIntent);
        instruction.Should().BeNull();
    }

    [Fact]
    public void Target_that_does_not_produce_native_side_and_quantity_is_rejected()
    {
        var fault = CanonicalOrderInstructionMapper.TryCreate(
            Metadata(),
            new ClientOrderId("client-target-mismatch"),
            new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1)),
            Context(TradeIntentQuantityMode.TargetPosition, signedUnits: Q(2), currentPosition: Q(0)),
            out var instruction);

        fault.Should().Be(OrderDomainFault.InvalidTradeIntent);
        instruction.Should().BeNull();
    }

    [Fact]
    public void Submit_constructor_rejects_instruction_from_another_client_order()
    {
        var metadata = Metadata();
        var terms = new OrderTerms(OrderSide.Buy, OrderType.Market, Q(1));
        CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            new ClientOrderId("client-original"),
            terms,
            Context(TradeIntentQuantityMode.Delta, Q(1)),
            out var instruction).Should().Be(OrderDomainFault.None);

        var construct = () => new SubmitOrderCommand(
            metadata,
            new OrderId("order-substitution"),
            new ClientOrderId("client-substituted"),
            terms,
            instruction!);

        construct.Should().Throw<ArgumentException>()
            .WithParameterName("canonicalInstruction");
    }

    [Fact]
    public void Risk_rejects_target_instruction_when_position_changed_after_mapping()
    {
        var metadata = Metadata();
        var terms = new OrderTerms(OrderSide.Buy, OrderType.Market, Q(2));
        CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            new ClientOrderId("client-stale-position"),
            terms,
            Context(TradeIntentQuantityMode.TargetPosition, Q(2), currentPosition: Q(0)),
            out var instruction).Should().Be(OrderDomainFault.None);
        var command = new SubmitOrderCommand(
            metadata,
            new OrderId("order-stale-position"),
            new ClientOrderId("client-stale-position"),
            terms,
            instruction!);

        var decision = RiskPolicy.Evaluate(command, RiskContext(currentPosition: Q(1)));

        decision.IsAllowed.Should().BeFalse();
        decision.Code.Should().Be(RiskDecisionCode.CanonicalInstructionMismatch);
    }

    [Fact]
    public void Fractional_delta_remains_exact_in_canonical_instruction()
    {
        var quantity = new ScaledQuantity(1, 3);
        var terms = new OrderTerms(OrderSide.Buy, OrderType.Market, quantity);

        var fault = CanonicalOrderInstructionMapper.TryCreate(
            Metadata(),
            new ClientOrderId("client-fractional"),
            terms,
            Context(TradeIntentQuantityMode.Delta, quantity),
            out var instruction);

        fault.Should().Be(OrderDomainFault.None);
        instruction!.TradeIntent.SignedUnits.Should().Be(quantity);
        instruction.Terms.Quantity.Should().Be(quantity);
    }

    [Fact]
    public void Instruction_hash_is_deterministic_and_binds_every_economic_field()
    {
        var metadata = Metadata();
        var terms = new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(100));
        var originalContext = Context(
            TradeIntentQuantityMode.Delta,
            Q(2),
            protectiveStopPrice: P(95),
            profitTargetPrice: P(110),
            cost: new ScaledMoney(25, 2),
            entryLimitPrice: P(100));
        CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            new ClientOrderId("client-hash"),
            terms,
            originalContext,
            out var original).Should().Be(OrderDomainFault.None);
        var changedContext = Context(
            TradeIntentQuantityMode.Delta,
            Q(2),
            protectiveStopPrice: P(95),
            profitTargetPrice: P(111),
            cost: new ScaledMoney(25, 2),
            entryLimitPrice: P(100));
        CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            new ClientOrderId("client-hash"),
            terms,
            changedContext,
            out var changed).Should().Be(OrderDomainFault.None);

        original!.InstructionHashSha256.Should().Be(ExecutionCanonicalJson.Hash(original));
        original.InstructionHashSha256.Should().HaveLength(64);
        changed!.InstructionHashSha256.Should().NotBe(original.InstructionHashSha256);
    }

    [Fact]
    public void Canonical_instruction_and_submit_command_golden_hashes_are_frozen()
    {
        var metadata = new ExecutionCommandMetadata(
            new CommandId("command-golden"),
            new CorrelationId("correlation-golden"),
            new CausationId("causation-golden"),
            new TradingAccountId("account-golden"),
            new StrategyId("strategy-golden"),
            new StrategyVersion("1.0.0"),
            new VenueId("venue-golden"),
            new InstrumentId(9001),
            ExecutionEnvironment.SimulatedPaper,
            DateTimeOffset.UnixEpoch.AddHours(2),
            expectedOrderSequence: 0);
        var terms = new OrderTerms(
            OrderSide.Buy,
            OrderType.Limit,
            Q(2),
            limitPrice: new ScaledPrice(10_025, 2),
            timeInForce: TimeInForce.Gtc);
        var mapping = new CanonicalInstructionMappingContext(
            new IntentId("intent-golden"),
            bucketId: null,
            new LegId("leg-golden"),
            new ExecutionLeaseId("lease-golden"),
            new FencingToken(7),
            TradeIntentQuantityMode.Delta,
            Q(2),
            ScaledQuantity.Zero,
            new ScaledPrice(9_500, 2),
            new ScaledPrice(11_000, 2),
            new ScaledMoney(25, 2),
            strategyNoteId: 501,
            policyVersion: "policy-golden-v1",
            entryLimitPrice: new ScaledPrice(10_025, 2));
        var clientOrderId = new ClientOrderId("client-golden");
        CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            clientOrderId,
            terms,
            mapping,
            out var instruction).Should().Be(OrderDomainFault.None);
        var command = new SubmitOrderCommand(
            metadata,
            new OrderId("order-golden"),
            clientOrderId,
            terms,
            instruction!);

        instruction!.InstructionHashSha256.Should().Be(
            "0d40ca28afa29a47c7ff4ecae59acaeeb9d68bd5e7b73513ab63784eb5d9fbc8");
        command.PayloadHashSha256.Should().Be(
            "556a332b755f82b2a1753c7be878abe7eba6c4dea593d2c88f2bfb2683020337");
    }

    private static ExecutionCommandMetadata Metadata(string strategyId = "strategy-test") => new(
        new CommandId("command-test"),
        new CorrelationId("correlation-test"),
        new CausationId("causation-test"),
        new TradingAccountId("account-test"),
        new StrategyId(strategyId),
        new StrategyVersion("1.0.0"),
        new VenueId("venue-test"),
        new InstrumentId(9001),
        ExecutionEnvironment.SimulatedPaper,
        Now,
        expectedOrderSequence: 0);

    private static CanonicalInstructionMappingContext Context(
        TradeIntentQuantityMode mode,
        ScaledQuantity signedUnits,
        ScaledQuantity? currentPosition = null,
        ScaledPrice? protectiveStopPrice = null,
        ScaledPrice? profitTargetPrice = null,
        ScaledMoney? cost = null,
        ScaledPrice? entryLimitPrice = null,
        ScaledPrice? entryStopPrice = null) => new(
        new IntentId("intent-test"),
        bucketId: null,
        new LegId("leg-test"),
        new ExecutionLeaseId("lease-test"),
        new FencingToken(7),
        mode,
        signedUnits,
        currentPosition ?? ScaledQuantity.Zero,
        protectiveStopPrice,
        profitTargetPrice,
        cost ?? ScaledMoney.Zero,
        strategyNoteId: 501,
        policyVersion: "policy-test-v1",
        entryLimitPrice,
        entryStopPrice);

    private static RiskEvaluationContext RiskContext(ScaledQuantity currentPosition) => new(
        new RiskLimits(
            Q(100),
            Q(100),
            new ScaledMoney(1_000_000, 0),
            ScaledMoney.Zero,
            new ScaledMoney(10_000, 0),
            new ScaledMoney(10_000, 0),
            100,
            TimeSpan.FromMinutes(1)),
        RiskControlMode.Active,
        killSwitchActive: false,
        currentPosition,
        ScaledQuantity.Zero,
        ScaledQuantity.Zero,
        ScaledMoney.Zero,
        ScaledQuantity.Zero,
        ScaledMoney.Zero,
        ScaledQuantity.Zero,
        new ScaledMoney(1_000_000, 0),
        ScaledMoney.Zero,
        new ScaledMoney(100_000, 0),
        new ScaledMoney(100_000, 0),
        P(100),
        exposureCommandsInWindow: 0,
        Now);

    private static ScaledQuantity Q(long value) => ScaledQuantity.FromWhole(value);
    private static ScaledPrice P(long value) => new(value, 0);
}
