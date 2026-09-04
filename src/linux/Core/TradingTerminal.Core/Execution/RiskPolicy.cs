using System.Text.Json.Serialization;
using TradingTerminal.Core.Trading;

namespace TradingTerminal.Core.Execution;

public enum RiskControlMode
{
    Active = 1,
    Reducing = 2,
    Halted = 3,
}

public enum RiskDecisionCode
{
    Allowed = 1,
    CancelOrQueryAlwaysAllowed = 2,
    ExpiredCommand = 3,
    KillSwitchActive = 4,
    NewExposureHalted = 5,
    ReduceOnlyWouldIncreaseExposure = 6,
    MaximumOrderQuantityExceeded = 7,
    MaximumPositionExceeded = 8,
    MaximumGrossNotionalExceeded = 9,
    InsufficientBuyingPower = 10,
    DailyLossLimitExceeded = 11,
    DrawdownLimitExceeded = 12,
    RateLimitExceeded = 13,
    InvalidMarketPrice = 14,
    ReplacementQuantityBelowFilled = 15,
    CanonicalInstructionMismatch = 16,
}

public sealed record RiskDecision(
    bool IsAllowed,
    RiskDecisionCode Code,
    string Reason,
    ScaledQuantity ProjectedNetQuantity,
    ScaledMoney ProjectedGrossNotional)
{
    public static RiskDecision Allow(
        RiskDecisionCode code,
        string reason,
        ScaledQuantity projectedNetQuantity,
        ScaledMoney projectedGrossNotional) =>
        new(true, code, reason, projectedNetQuantity, projectedGrossNotional);

    public static RiskDecision Deny(
        RiskDecisionCode code,
        string reason,
        ScaledQuantity projectedNetQuantity,
        ScaledMoney projectedGrossNotional) =>
        new(false, code, reason, projectedNetQuantity, projectedGrossNotional);
}

public sealed record RiskLimits
{
    public RiskLimits(
        ScaledQuantity maximumOrderQuantity,
        ScaledQuantity maximumAbsolutePosition,
        ScaledMoney maximumGrossNotional,
        ScaledMoney minimumBuyingPower,
        ScaledMoney maximumDailyLoss,
        ScaledMoney maximumDrawdown,
        int maximumExposureCommandsPerWindow,
        TimeSpan rateLimitWindow)
    {
        if (!maximumOrderQuantity.IsValid || maximumOrderQuantity.Coefficient <= 0) throw new ArgumentOutOfRangeException(nameof(maximumOrderQuantity));
        if (!maximumAbsolutePosition.IsValid || maximumAbsolutePosition.Coefficient <= 0) throw new ArgumentOutOfRangeException(nameof(maximumAbsolutePosition));
        if (!maximumGrossNotional.IsValid || maximumGrossNotional.Coefficient <= 0) throw new ArgumentOutOfRangeException(nameof(maximumGrossNotional));
        if (!minimumBuyingPower.IsValid || minimumBuyingPower.Coefficient < 0) throw new ArgumentOutOfRangeException(nameof(minimumBuyingPower));
        if (!maximumDailyLoss.IsValid || maximumDailyLoss.Coefficient <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDailyLoss));
        if (!maximumDrawdown.IsValid || maximumDrawdown.Coefficient <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDrawdown));
        if (maximumExposureCommandsPerWindow <= 0) throw new ArgumentOutOfRangeException(nameof(maximumExposureCommandsPerWindow));
        if (rateLimitWindow <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(rateLimitWindow));
        MaximumOrderQuantity = maximumOrderQuantity;
        MaximumAbsolutePosition = maximumAbsolutePosition;
        MaximumGrossNotional = maximumGrossNotional;
        MinimumBuyingPower = minimumBuyingPower;
        MaximumDailyLoss = maximumDailyLoss;
        MaximumDrawdown = maximumDrawdown;
        MaximumExposureCommandsPerWindow = maximumExposureCommandsPerWindow;
        RateLimitWindow = rateLimitWindow;
    }

    public ScaledQuantity MaximumOrderQuantity { get; }
    public ScaledQuantity MaximumAbsolutePosition { get; }
    public ScaledMoney MaximumGrossNotional { get; }
    public ScaledMoney MinimumBuyingPower { get; }
    public ScaledMoney MaximumDailyLoss { get; }
    public ScaledMoney MaximumDrawdown { get; }
    public int MaximumExposureCommandsPerWindow { get; }
    public TimeSpan RateLimitWindow { get; }
}

public sealed record RiskEvaluationContext
{
    public RiskEvaluationContext(
        RiskLimits limits,
        RiskControlMode controlMode,
        bool killSwitchActive,
        ScaledQuantity currentPositionQuantity,
        ScaledQuantity currentBuyReservedQuantity,
        ScaledQuantity currentSellReservedQuantity,
        ScaledMoney currentGrossReservedNotional,
        ScaledQuantity existingOrderSignedReservation,
        ScaledMoney existingOrderGrossReservation,
        ScaledQuantity existingOrderFilledQuantity,
        ScaledMoney availableBuyingPower,
        ScaledMoney dailyNetRealizedPnl,
        ScaledMoney currentEquity,
        ScaledMoney peakEquity,
        ScaledPrice marketPrice,
        int exposureCommandsInWindow,
        DateTimeOffset evaluatedAtUtc,
        ScaledRatio? contractMultiplier = null,
        string accountCurrency = "USD",
        bool hasUnrepresentableMarketEconomics = false)
        : this(
            limits,
            controlMode,
            killSwitchActive,
            currentPositionQuantity,
            currentBuyReservedQuantity,
            currentSellReservedQuantity,
            currentGrossReservedNotional,
            existingOrderSignedReservation,
            existingOrderGrossReservation,
            existingOrderFilledQuantity,
            availableBuyingPower,
            dailyNetRealizedPnl,
            currentEquity,
            peakEquity,
            marketPrice,
            exposureCommandsInWindow,
            evaluatedAtUtc,
            contractMultiplier ?? new ScaledRatio(1, 0),
            accountCurrency,
            hasUnrepresentableMarketEconomics)
    {
    }

    [JsonConstructor]
    public RiskEvaluationContext(
        RiskLimits limits,
        RiskControlMode controlMode,
        bool killSwitchActive,
        ScaledQuantity currentPositionQuantity,
        ScaledQuantity currentBuyReservedQuantity,
        ScaledQuantity currentSellReservedQuantity,
        ScaledMoney currentGrossReservedNotional,
        ScaledQuantity existingOrderSignedReservation,
        ScaledMoney existingOrderGrossReservation,
        ScaledQuantity existingOrderFilledQuantity,
        ScaledMoney availableBuyingPower,
        ScaledMoney dailyNetRealizedPnl,
        ScaledMoney currentEquity,
        ScaledMoney peakEquity,
        ScaledPrice marketPrice,
        int exposureCommandsInWindow,
        DateTimeOffset evaluatedAtUtc,
        ScaledRatio contractMultiplier,
        string accountCurrency,
        bool hasUnrepresentableMarketEconomics)
    {
        ArgumentNullException.ThrowIfNull(limits);
        if (!Enum.IsDefined(controlMode)) throw new ArgumentOutOfRangeException(nameof(controlMode));
        var multiplier = contractMultiplier;
        RequireValid(currentPositionQuantity, nameof(currentPositionQuantity));
        RequireNonNegative(currentBuyReservedQuantity, nameof(currentBuyReservedQuantity));
        RequireNonNegative(currentSellReservedQuantity, nameof(currentSellReservedQuantity));
        RequireNonNegative(currentGrossReservedNotional, nameof(currentGrossReservedNotional));
        RequireValid(existingOrderSignedReservation, nameof(existingOrderSignedReservation));
        RequireNonNegative(existingOrderGrossReservation, nameof(existingOrderGrossReservation));
        RequireNonNegative(existingOrderFilledQuantity, nameof(existingOrderFilledQuantity));
        if (!multiplier.IsValid || multiplier.Coefficient <= 0) throw new ArgumentOutOfRangeException(nameof(contractMultiplier));
        RequireNonNegative(availableBuyingPower, nameof(availableBuyingPower));
        RequireValid(dailyNetRealizedPnl, nameof(dailyNetRealizedPnl));
        RequireValid(currentEquity, nameof(currentEquity));
        RequireValid(peakEquity, nameof(peakEquity));
        if (!marketPrice.IsValid) throw new ArgumentOutOfRangeException(nameof(marketPrice));
        if (!TryCompareMoney(peakEquity, currentEquity, out var equityComparison) || equityComparison < 0)
            throw new ArgumentOutOfRangeException(nameof(peakEquity), "Peak equity cannot be less than current equity.");
        if (exposureCommandsInWindow < 0) throw new ArgumentOutOfRangeException(nameof(exposureCommandsInWindow));
        Limits = limits;
        ControlMode = controlMode;
        KillSwitchActive = killSwitchActive;
        CurrentPositionQuantity = currentPositionQuantity;
        CurrentBuyReservedQuantity = currentBuyReservedQuantity;
        CurrentSellReservedQuantity = currentSellReservedQuantity;
        CurrentGrossReservedNotional = currentGrossReservedNotional;
        ExistingOrderSignedReservation = existingOrderSignedReservation;
        ExistingOrderGrossReservation = existingOrderGrossReservation;
        ExistingOrderFilledQuantity = existingOrderFilledQuantity;
        AvailableBuyingPower = availableBuyingPower;
        DailyNetRealizedPnl = dailyNetRealizedPnl;
        CurrentEquity = currentEquity;
        PeakEquity = peakEquity;
        MarketPrice = marketPrice;
        ExposureCommandsInWindow = exposureCommandsInWindow;
        EvaluatedAtUtc = ExecutionValidation.RequireUtc(evaluatedAtUtc, nameof(evaluatedAtUtc));
        TradingDayStartedAtUtc = new DateTimeOffset(
            EvaluatedAtUtc.Year,
            EvaluatedAtUtc.Month,
            EvaluatedAtUtc.Day,
            0,
            0,
            0,
            TimeSpan.Zero);
        ContractMultiplier = multiplier;
        AccountCurrency = ExecutionValidation.RequireText(accountCurrency.ToUpperInvariant(), nameof(accountCurrency), 16);
        HasUnrepresentableMarketEconomics = hasUnrepresentableMarketEconomics;
    }

    public RiskLimits Limits { get; }
    public RiskControlMode ControlMode { get; }
    public bool KillSwitchActive { get; }
    public ScaledQuantity CurrentPositionQuantity { get; }
    public ScaledQuantity CurrentBuyReservedQuantity { get; }
    public ScaledQuantity CurrentSellReservedQuantity { get; }
    public ScaledQuantity CurrentNetReservedQuantity =>
        ScaledValueMath.TrySubtractQuantity(CurrentBuyReservedQuantity, CurrentSellReservedQuantity, out var value)
            ? value
            : throw new OverflowException("Reserved quantity cannot be represented exactly.");
    public ScaledMoney CurrentGrossReservedNotional { get; }
    public ScaledQuantity ExistingOrderSignedReservation { get; }
    public ScaledMoney ExistingOrderGrossReservation { get; }
    public ScaledQuantity ExistingOrderFilledQuantity { get; }
    public ScaledMoney AvailableBuyingPower { get; }
    public ScaledMoney DailyNetRealizedPnl { get; }
    public ScaledMoney CurrentEquity { get; }
    public ScaledMoney PeakEquity { get; }
    public ScaledPrice MarketPrice { get; }
    public int ExposureCommandsInWindow { get; }
    public DateTimeOffset EvaluatedAtUtc { get; }
    public DateTimeOffset TradingDayStartedAtUtc { get; }
    public ScaledRatio ContractMultiplier { get; }
    public string AccountCurrency { get; }
    /// <summary>
    /// A legacy market-data/accounting boundary supplied a finite value outside the canonical
    /// coefficient/scale range. The placeholder values in this context must never be admitted.
    /// </summary>
    public bool HasUnrepresentableMarketEconomics { get; }

    private static void RequireValid(ScaledQuantity value, string name)
    {
        if (!value.IsValid) throw new ArgumentOutOfRangeException(name);
    }

    private static void RequireValid(ScaledMoney value, string name)
    {
        if (!value.IsValid) throw new ArgumentOutOfRangeException(name);
    }

    private static void RequireNonNegative(ScaledQuantity value, string name)
    {
        if (!value.IsValid || value.Coefficient < 0) throw new ArgumentOutOfRangeException(name);
    }

    private static void RequireNonNegative(ScaledMoney value, string name)
    {
        if (!value.IsValid || value.Coefficient < 0) throw new ArgumentOutOfRangeException(name);
    }

    private static bool TryCompareMoney(ScaledMoney left, ScaledMoney right, out int comparison) =>
        ScaledValueMath.TryCompare(left.Coefficient, left.Scale, right.Coefficient, right.Scale, out comparison);
}

public sealed record RiskPolicyEvidence
{
    public RiskPolicyEvidence(string policyVersion, string limitsHashSha256, RiskEvaluationContext context)
    {
        PolicyVersion = ExecutionValidation.RequireText(policyVersion, nameof(policyVersion), 128);
        LimitsHashSha256 = ExecutionValidation.RequireSha256(limitsHashSha256, nameof(limitsHashSha256));
        ArgumentNullException.ThrowIfNull(context);
        if (!string.Equals(ExecutionCanonicalJson.Hash(context.Limits), LimitsHashSha256, StringComparison.Ordinal))
            throw new ArgumentException("Risk limits hash does not match the captured evaluation context.", nameof(limitsHashSha256));
        Context = context;
    }

    public string PolicyVersion { get; }
    public string LimitsHashSha256 { get; }
    public RiskEvaluationContext Context { get; }

    public static RiskPolicyEvidence Capture(RiskEvaluationContext context) =>
        new(RiskPolicy.PolicyVersion, ExecutionCanonicalJson.Hash(context.Limits), context);
}

/// <summary>Stateless policy; callers persist a RiskObservation before applying the decision.</summary>
public static class RiskPolicy
{
    public const string PolicyVersion = "daxalgo-risk-policy-v3-scaled";

    private static readonly ScaledQuantity InvalidQuantity = new(long.MaxValue, 0);
    private static readonly ScaledMoney InvalidMoney = new(long.MaxValue, 0);

    public static RiskDecision Evaluate(ExecutionCommand command, RiskEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(context);

        var currentNetReserved = ScaledQuantity.Zero;
        var currentProjected = ScaledQuantity.Zero;
        var positionProjectionInvalid =
            !ScaledValueMath.TrySubtractQuantity(
                context.CurrentBuyReservedQuantity,
                context.CurrentSellReservedQuantity,
                out currentNetReserved) ||
            !ScaledValueMath.TryAddQuantity(
                context.CurrentPositionQuantity,
                currentNetReserved,
                out currentProjected);
        if (positionProjectionInvalid)
            currentProjected = InvalidQuantity;
        var currentGross = context.CurrentGrossReservedNotional;
        if (command is CancelOrderCommand or QueryOrderCommand)
            return RiskDecision.Allow(RiskDecisionCode.CancelOrQueryAlwaysAllowed, "Cancel and query remain admitted during recovery.", currentProjected, currentGross);

        var terms = command switch
        {
            SubmitOrderCommand submit => submit.Terms,
            ReplaceOrderCommand replace => replace.ReplacementTerms,
            _ => throw new NotSupportedException($"Unsupported risk command {command.GetType().Name}.")
        };
        if (command is SubmitOrderCommand submitCommand &&
            !CanonicalOrderInstructionMapper.SignedEconomicsAgree(
                submitCommand.CanonicalInstruction,
                context.CurrentPositionQuantity))
        {
            return RiskDecision.Deny(
                RiskDecisionCode.CanonicalInstructionMismatch,
                "Canonical intent does not produce the submitted side and quantity from the current position.",
                currentProjected,
                currentGross);
        }
        var price = terms.LimitPrice ?? terms.StopPrice ?? context.MarketPrice;
        if (!price.IsValid || price.Coefficient <= 0)
            return RiskDecision.Deny(RiskDecisionCode.InvalidMarketPrice, "A positive reservation price is required.", currentProjected, currentGross);

        var reservationQuantity = terms.Quantity;
        if (command is ReplaceOrderCommand)
        {
            if (!TryCompareQuantity(terms.Quantity, context.ExistingOrderFilledQuantity, out var replacementComparison) ||
                replacementComparison < 0 ||
                !ScaledValueMath.TrySubtractQuantity(
                    terms.Quantity,
                    context.ExistingOrderFilledQuantity,
                    out reservationQuantity))
            {
                return RiskDecision.Deny(
                    RiskDecisionCode.ReplacementQuantityBelowFilled,
                    "Replacement total quantity cannot be less than accepted fills.",
                    currentProjected,
                    currentGross);
            }
        }

        var existingBuy = context.ExistingOrderSignedReservation.Coefficient > 0
            ? context.ExistingOrderSignedReservation
            : ScaledQuantity.Zero;
        var existingSell = ScaledQuantity.Zero;
        if (context.ExistingOrderSignedReservation.Coefficient < 0 &&
            !TryNegateQuantity(context.ExistingOrderSignedReservation, out existingSell))
            positionProjectionInvalid = true;

        if (!TryProjectReservations(
                context,
                terms.Side,
                reservationQuantity,
                existingBuy,
                existingSell,
                out var projectedBuy,
                out var projectedSell,
                out var projectedNet,
                out var worstCaseAbsolutePosition))
        {
            positionProjectionInvalid = true;
            projectedNet = InvalidQuantity;
            worstCaseAbsolutePosition = InvalidQuantity;
        }

        var orderNotional = ScaledMoney.Zero;
        var grossWithoutExisting = ScaledMoney.Zero;
        var projectedGross = ScaledMoney.Zero;
        var grossProjectionInvalid =
            context.HasUnrepresentableMarketEconomics ||
            !TryOrderNotional(reservationQuantity, price, context.ContractMultiplier, out orderNotional) ||
            !TrySubtractMoney(currentGross, context.ExistingOrderGrossReservation, out grossWithoutExisting) ||
            !ScaledValueMath.TryAddMoney(grossWithoutExisting, orderNotional, out projectedGross) ||
            projectedGross.Coefficient < 0;
        if (grossProjectionInvalid)
            projectedGross = InvalidMoney;

        var isNonCrossingReduceOnly = !positionProjectionInvalid && terms.ReduceOnly &&
            IsNonCrossingReduction(
                context.CurrentPositionQuantity,
                terms.Side,
                projectedBuy,
                projectedSell);

        if (command.Metadata.ExpiresAtUtc is { } expiresAt && expiresAt <= context.EvaluatedAtUtc)
            return RiskDecision.Deny(RiskDecisionCode.ExpiredCommand, "Command has expired.", projectedNet, projectedGross);
        if (context.KillSwitchActive)
            return RiskDecision.Deny(RiskDecisionCode.KillSwitchActive, "Kill switch blocks new exposure.", projectedNet, projectedGross);
        if (context.ControlMode == RiskControlMode.Halted)
            return RiskDecision.Deny(RiskDecisionCode.NewExposureHalted, "Risk control is halted.", projectedNet, projectedGross);
        if (context.ControlMode == RiskControlMode.Reducing && !terms.ReduceOnly)
            return RiskDecision.Deny(RiskDecisionCode.NewExposureHalted, "Reducing mode admits only explicitly reduce-only exposure commands.", projectedNet, projectedGross);
        if (positionProjectionInvalid)
            return RiskDecision.Deny(RiskDecisionCode.MaximumPositionExceeded, "Position projection exceeded the supported exact range.", projectedNet, projectedGross);
        if ((context.ControlMode == RiskControlMode.Reducing || terms.ReduceOnly) &&
            !isNonCrossingReduceOnly)
            return RiskDecision.Deny(RiskDecisionCode.ReduceOnlyWouldIncreaseExposure, "Reduce-only reservations must reduce without crossing through flat.", projectedNet, projectedGross);
        if (!TryCompareQuantity(terms.Quantity, context.Limits.MaximumOrderQuantity, out var orderQuantityComparison) ||
            orderQuantityComparison > 0)
            return RiskDecision.Deny(RiskDecisionCode.MaximumOrderQuantityExceeded, "Order quantity exceeds the configured maximum.", projectedNet, projectedGross);
        if (!isNonCrossingReduceOnly &&
            (!TryCompareQuantity(worstCaseAbsolutePosition, context.Limits.MaximumAbsolutePosition, out var positionComparison) ||
             positionComparison > 0))
            return RiskDecision.Deny(RiskDecisionCode.MaximumPositionExceeded, "Worst-case directional fills exceed the configured position maximum.", projectedNet, projectedGross);
        if (grossProjectionInvalid)
            return RiskDecision.Deny(RiskDecisionCode.MaximumGrossNotionalExceeded, "Gross-notional projection exceeded the supported exact range.", projectedNet, projectedGross);
        if (!isNonCrossingReduceOnly &&
            (!TryCompareMoney(projectedGross, context.Limits.MaximumGrossNotional, out var grossComparison) ||
             grossComparison > 0))
            return RiskDecision.Deny(RiskDecisionCode.MaximumGrossNotionalExceeded, "Projected working-order notional exceeds the configured maximum.", projectedNet, projectedGross);
        if (!TrySubtractMoney(projectedGross, currentGross, out var incrementalGrossNotional))
            return RiskDecision.Deny(RiskDecisionCode.MaximumGrossNotionalExceeded, "Incremental gross-notional projection exceeded the supported exact range.", projectedNet, projectedGross);
        if (incrementalGrossNotional.Coefficient < 0)
            incrementalGrossNotional = ScaledMoney.Zero;
        if (!TrySubtractMoney(context.AvailableBuyingPower, context.Limits.MinimumBuyingPower, out var usableBuyingPower))
            return RiskDecision.Deny(RiskDecisionCode.InsufficientBuyingPower, "Protected buying power cannot be represented exactly.", projectedNet, projectedGross);
        if (usableBuyingPower.Coefficient < 0)
            usableBuyingPower = ScaledMoney.Zero;
        if (!isNonCrossingReduceOnly &&
            incrementalGrossNotional.Coefficient > 0 &&
            (!TryCompareMoney(incrementalGrossNotional, usableBuyingPower, out var buyingPowerComparison) ||
             buyingPowerComparison > 0))
            return RiskDecision.Deny(RiskDecisionCode.InsufficientBuyingPower, "Order would consume protected buying power.", projectedNet, projectedGross);
        if (!TryNegateMoney(context.Limits.MaximumDailyLoss, out var negativeDailyLossLimit))
            return RiskDecision.Deny(RiskDecisionCode.DailyLossLimitExceeded, "Daily-loss limit cannot be represented exactly.", projectedNet, projectedGross);
        if (!isNonCrossingReduceOnly &&
            (!TryCompareMoney(context.DailyNetRealizedPnl, negativeDailyLossLimit, out var lossComparison) ||
             lossComparison <= 0))
            return RiskDecision.Deny(RiskDecisionCode.DailyLossLimitExceeded, "Daily net realized-loss limit is active.", projectedNet, projectedGross);
        if (!TrySubtractMoney(context.PeakEquity, context.CurrentEquity, out var drawdown) ||
            !TryCompareMoney(drawdown, context.Limits.MaximumDrawdown, out var drawdownComparison) ||
            !isNonCrossingReduceOnly && drawdownComparison >= 0)
            return RiskDecision.Deny(RiskDecisionCode.DrawdownLimitExceeded, "Drawdown limit is active.", projectedNet, projectedGross);
        if (context.ExposureCommandsInWindow >= context.Limits.MaximumExposureCommandsPerWindow)
            return RiskDecision.Deny(RiskDecisionCode.RateLimitExceeded, "Exposure command rate limit is active.", projectedNet, projectedGross);

        return RiskDecision.Allow(RiskDecisionCode.Allowed, "Risk checks passed.", projectedNet, projectedGross);
    }

    private static bool IsNonCrossingReduction(
        ScaledQuantity currentPosition,
        OrderSide side,
        ScaledQuantity projectedBuy,
        ScaledQuantity projectedSell)
    {
        if (currentPosition.Coefficient > 0)
            return side == OrderSide.Sell &&
                   TryCompareQuantity(projectedSell, currentPosition, out var sellComparison) &&
                   sellComparison <= 0;
        if (currentPosition.Coefficient < 0)
        {
            return TryNegateQuantity(currentPosition, out var absolutePosition) &&
                   side == OrderSide.Buy &&
                   TryCompareQuantity(projectedBuy, absolutePosition, out var buyComparison) &&
                   buyComparison <= 0;
        }
        return false;
    }

    private static bool TryProjectReservations(
        RiskEvaluationContext context,
        OrderSide side,
        ScaledQuantity reservationQuantity,
        ScaledQuantity existingBuy,
        ScaledQuantity existingSell,
        out ScaledQuantity projectedBuy,
        out ScaledQuantity projectedSell,
        out ScaledQuantity projectedNet,
        out ScaledQuantity worstCaseAbsolutePosition)
    {
        projectedBuy = projectedSell = projectedNet = worstCaseAbsolutePosition = default;
        if (!ScaledValueMath.TrySubtractQuantity(context.CurrentBuyReservedQuantity, existingBuy, out var buyWithoutExisting) ||
            !ScaledValueMath.TryAddQuantity(
                buyWithoutExisting,
                side == OrderSide.Buy ? reservationQuantity : ScaledQuantity.Zero,
                out projectedBuy) ||
            !ScaledValueMath.TrySubtractQuantity(context.CurrentSellReservedQuantity, existingSell, out var sellWithoutExisting) ||
            !ScaledValueMath.TryAddQuantity(
                sellWithoutExisting,
                side == OrderSide.Sell ? reservationQuantity : ScaledQuantity.Zero,
                out projectedSell) ||
            projectedBuy.Coefficient < 0 ||
            projectedSell.Coefficient < 0 ||
            !ScaledValueMath.TryAddQuantity(context.CurrentPositionQuantity, projectedBuy, out var positionPlusBuys) ||
            !ScaledValueMath.TrySubtractQuantity(positionPlusBuys, projectedSell, out projectedNet) ||
            !ScaledValueMath.TryAddQuantity(context.CurrentPositionQuantity, projectedBuy, out var worstLong) ||
            !ScaledValueMath.TrySubtractQuantity(context.CurrentPositionQuantity, projectedSell, out var worstShort) ||
            !TryAbsQuantity(worstLong, out worstLong) ||
            !TryAbsQuantity(worstShort, out worstShort) ||
            !TryCompareQuantity(worstLong, worstShort, out var worstComparison))
            return false;
        worstCaseAbsolutePosition = worstComparison >= 0 ? worstLong : worstShort;
        return true;
    }

    private static bool TryOrderNotional(
        ScaledQuantity quantity,
        ScaledPrice price,
        ScaledRatio multiplier,
        out ScaledMoney notional)
    {
        notional = default;
        if (!quantity.IsValid || quantity.Coefficient < 0 ||
            !price.IsValid || price.Coefficient <= 0 ||
            !multiplier.IsValid || multiplier.Coefficient <= 0 ||
            !ScaledValueMath.TryMultiply(quantity.Coefficient, price.Coefficient, out var quantityPrice) ||
            !ScaledValueMath.TryMultiply(quantityPrice, multiplier.Coefficient, out var wide) ||
            !ScaledValueMath.TryNarrow(
                wide,
                quantity.Scale + price.Scale + multiplier.Scale,
                out var coefficient,
                out var scale))
            return false;
        notional = new ScaledMoney(coefficient, scale);
        return true;
    }

    private static bool TrySubtractMoney(ScaledMoney left, ScaledMoney right, out ScaledMoney difference)
    {
        difference = default;
        if (!ScaledValueMath.TryAdd(
                left.Coefficient,
                left.Scale,
                -(Int128)right.Coefficient,
                right.Scale,
                out var wide,
                out var wideScale) ||
            !ScaledValueMath.TryNarrow(wide, wideScale, out var coefficient, out var scale))
            return false;
        difference = new ScaledMoney(coefficient, scale);
        return true;
    }

    private static bool TryNegateQuantity(ScaledQuantity value, out ScaledQuantity negated)
    {
        negated = default;
        if (!ScaledValueMath.TryNarrow(-(Int128)value.Coefficient, value.Scale, out var coefficient, out var scale))
            return false;
        negated = new ScaledQuantity(coefficient, scale);
        return true;
    }

    private static bool TryAbsQuantity(ScaledQuantity value, out ScaledQuantity absolute) =>
        value.Coefficient < 0 ? TryNegateQuantity(value, out absolute) : Return(value, out absolute);

    private static bool TryNegateMoney(ScaledMoney value, out ScaledMoney negated)
    {
        negated = default;
        if (!ScaledValueMath.TryNarrow(-(Int128)value.Coefficient, value.Scale, out var coefficient, out var scale))
            return false;
        negated = new ScaledMoney(coefficient, scale);
        return true;
    }

    private static bool TryCompareQuantity(ScaledQuantity left, ScaledQuantity right, out int comparison) =>
        ScaledValueMath.TryCompare(left.Coefficient, left.Scale, right.Coefficient, right.Scale, out comparison);

    private static bool TryCompareMoney(ScaledMoney left, ScaledMoney right, out int comparison) =>
        ScaledValueMath.TryCompare(left.Coefficient, left.Scale, right.Coefficient, right.Scale, out comparison);

    private static bool Return<T>(T value, out T result)
    {
        result = value;
        return true;
    }
}
