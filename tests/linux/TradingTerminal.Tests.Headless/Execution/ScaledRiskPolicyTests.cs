using FluentAssertions;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Trading;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class ScaledRiskPolicyTests
{
    private static readonly DateTimeOffset Now = DateTime.UnixEpoch.AddHours(1);

    [Fact]
    public void Fractional_quantity_price_and_notional_remain_exact()
    {
        var command = Submit(new OrderTerms(
            OrderSide.Buy,
            OrderType.Market,
            new ScaledQuantity(125, 3)));

        var decision = RiskPolicy.Evaluate(command, Context(
            marketPrice: new ScaledPrice(100_125, 3)));

        decision.IsAllowed.Should().BeTrue();
        decision.ProjectedNetQuantity.Should().Be(new ScaledQuantity(125, 3));
        decision.ProjectedGrossNotional.Should().Be(new ScaledMoney(12_515_625, 6));
    }

    [Fact]
    public void Equivalent_quantity_scales_compare_equal_at_the_limit()
    {
        var command = Submit(new OrderTerms(
            OrderSide.Buy,
            OrderType.Market,
            new ScaledQuantity(1_000, 3)));

        var decision = RiskPolicy.Evaluate(command, Context(
            limits: Limits(maximumOrderQuantity: new ScaledQuantity(1, 0))));

        decision.IsAllowed.Should().BeTrue();
        decision.Code.Should().Be(RiskDecisionCode.Allowed);
    }

    [Fact]
    public void Fractional_buying_power_shortfall_is_denied_without_rounding()
    {
        var command = Submit(new OrderTerms(
            OrderSide.Buy,
            OrderType.Market,
            new ScaledQuantity(5, 1)));

        var decision = RiskPolicy.Evaluate(command, Context(
            marketPrice: new ScaledPrice(10_025, 2),
            availableBuyingPower: new ScaledMoney(5_012, 2)));

        decision.IsAllowed.Should().BeFalse();
        decision.Code.Should().Be(RiskDecisionCode.InsufficientBuyingPower);
        decision.ProjectedGrossNotional.Should().Be(new ScaledMoney(50_125, 3));
    }

    [Fact]
    public void Daily_loss_boundary_is_denied_at_exact_equality()
    {
        var decision = RiskPolicy.Evaluate(
            Submit(new OrderTerms(OrderSide.Buy, OrderType.Market, ScaledQuantity.FromWhole(1))),
            Context(dailyNetRealizedPnl: new ScaledMoney(-10_000, 2)));

        decision.IsAllowed.Should().BeFalse();
        decision.Code.Should().Be(RiskDecisionCode.DailyLossLimitExceeded);
    }

    [Fact]
    public void Non_crossing_reduce_only_order_is_admitted_despite_exposure_limits()
    {
        var command = Submit(new OrderTerms(
            OrderSide.Sell,
            OrderType.Market,
            new ScaledQuantity(5, 1),
            reduceOnly: true));

        var decision = RiskPolicy.Evaluate(command, Context(
            currentPositionQuantity: ScaledQuantity.FromWhole(1),
            availableBuyingPower: ScaledMoney.Zero,
            dailyNetRealizedPnl: new ScaledMoney(-10_000, 2),
            currentEquity: new ScaledMoney(900, 0),
            peakEquity: new ScaledMoney(1_000, 0),
            limits: Limits(
                maximumGrossNotional: new ScaledMoney(1, 0),
                maximumDailyLoss: new ScaledMoney(100, 0),
                maximumDrawdown: new ScaledMoney(100, 0))));

        decision.IsAllowed.Should().BeTrue();
        decision.ProjectedNetQuantity.Should().Be(new ScaledQuantity(5, 1));
    }

    [Fact]
    public void Exact_notional_overflow_is_denied_instead_of_wrapping_or_throwing()
    {
        var command = Submit(new OrderTerms(
            OrderSide.Buy,
            OrderType.Market,
            new ScaledQuantity(long.MaxValue, 0)));

        var evaluate = () => RiskPolicy.Evaluate(command, Context(
            marketPrice: new ScaledPrice(long.MaxValue, 0),
            limits: Limits(
                maximumOrderQuantity: new ScaledQuantity(long.MaxValue, 0),
                maximumAbsolutePosition: new ScaledQuantity(long.MaxValue, 0),
                maximumGrossNotional: new ScaledMoney(long.MaxValue, 0))));

        var decision = evaluate.Should().NotThrow().Which;
        decision.IsAllowed.Should().BeFalse();
        decision.Code.Should().Be(RiskDecisionCode.MaximumGrossNotionalExceeded);
        decision.ProjectedGrossNotional.Should().Be(new ScaledMoney(long.MaxValue, 0));
    }

    [Fact]
    public void Legacy_values_outside_scaled_range_are_explicitly_non_representable()
    {
        ExecutionNumericBoundary.TryPriceFromDecimal(100_000_000_000_000_000_000m, out _)
            .Should().BeFalse();
        ExecutionNumericBoundary.TryRatioFromDecimal(100_000_000_000_000_000_000m, out _)
            .Should().BeFalse();
    }

    private static SubmitOrderCommand Submit(OrderTerms terms)
    {
        var metadata = Metadata();
        var clientOrderId = new ClientOrderId("client-risk");
        var signedUnits = terms.Side == OrderSide.Buy
            ? terms.Quantity
            : new ScaledQuantity(-terms.Quantity.Coefficient, terms.Quantity.Scale);
        var context = new CanonicalInstructionMappingContext(
            new IntentId("intent-risk"),
            bucketId: null,
            new LegId("leg-risk"),
            new ExecutionLeaseId("lease-risk"),
            new FencingToken(1),
            TradeIntentQuantityMode.Delta,
            signedUnits,
            currentPosition: ScaledQuantity.Zero,
            protectiveStopPrice: null,
            profitTargetPrice: null,
            estimatedRoundTripCostPerUnit: ScaledMoney.Zero,
            strategyNoteId: 1,
            policyVersion: "risk-test-policy");
        CanonicalOrderInstructionMapper.TryCreate(
            metadata,
            clientOrderId,
            terms,
            context,
            out var instruction).Should().Be(OrderDomainFault.None);
        return new SubmitOrderCommand(
            metadata,
            new OrderId("order-risk"),
            clientOrderId,
            terms,
            instruction!);
    }

    private static ExecutionCommandMetadata Metadata() => new(
        new CommandId("command-risk"),
        new CorrelationId("correlation-risk"),
        causationId: null,
        new TradingAccountId("account-risk"),
        new StrategyId("strategy-risk"),
        new StrategyVersion("version-risk"),
        new VenueId("venue-risk"),
        new InstrumentId(101),
        ExecutionEnvironment.Backtest,
        Now,
        expectedOrderSequence: 0);

    private static RiskEvaluationContext Context(
        RiskLimits? limits = null,
        ScaledQuantity? currentPositionQuantity = null,
        ScaledMoney? availableBuyingPower = null,
        ScaledMoney? dailyNetRealizedPnl = null,
        ScaledMoney? currentEquity = null,
        ScaledMoney? peakEquity = null,
        ScaledPrice? marketPrice = null) => new(
        limits ?? Limits(),
        RiskControlMode.Active,
        killSwitchActive: false,
        currentPositionQuantity ?? ScaledQuantity.Zero,
        currentBuyReservedQuantity: ScaledQuantity.Zero,
        currentSellReservedQuantity: ScaledQuantity.Zero,
        currentGrossReservedNotional: ScaledMoney.Zero,
        existingOrderSignedReservation: ScaledQuantity.Zero,
        existingOrderGrossReservation: ScaledMoney.Zero,
        existingOrderFilledQuantity: ScaledQuantity.Zero,
        availableBuyingPower ?? new ScaledMoney(1_000_000, 0),
        dailyNetRealizedPnl ?? ScaledMoney.Zero,
        currentEquity ?? new ScaledMoney(1_000, 0),
        peakEquity ?? new ScaledMoney(1_000, 0),
        marketPrice ?? new ScaledPrice(100, 0),
        exposureCommandsInWindow: 0,
        evaluatedAtUtc: Now);

    private static RiskLimits Limits(
        ScaledQuantity? maximumOrderQuantity = null,
        ScaledQuantity? maximumAbsolutePosition = null,
        ScaledMoney? maximumGrossNotional = null,
        ScaledMoney? maximumDailyLoss = null,
        ScaledMoney? maximumDrawdown = null) => new(
        maximumOrderQuantity ?? new ScaledQuantity(1_000_000, 0),
        maximumAbsolutePosition ?? new ScaledQuantity(1_000_000, 0),
        maximumGrossNotional ?? new ScaledMoney(1_000_000, 0),
        minimumBuyingPower: ScaledMoney.Zero,
        maximumDailyLoss ?? new ScaledMoney(100, 0),
        maximumDrawdown ?? new ScaledMoney(100, 0),
        maximumExposureCommandsPerWindow: 100,
        rateLimitWindow: TimeSpan.FromMinutes(1));
}
