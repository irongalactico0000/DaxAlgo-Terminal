using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Trading;
using System.Text.Json.Serialization;

namespace TradingTerminal.Core.Execution;

public enum TradeIntentQuantityMode : byte
{
    TargetPosition = 0,
    Delta = 1,
}

/// <summary>The strategy/operator-owned economics before account or venue routing.</summary>
public readonly record struct TradeIntent(
    InstrumentId Instrument,
    TradeIntentQuantityMode QuantityMode,
    ScaledQuantity SignedUnits,
    ScaledPrice? ProtectiveStopPrice,
    ScaledPrice? ProfitTargetPrice,
    ScaledMoney EstimatedRoundTripCostPerUnit,
    string StrategyId,
    long StrategyNoteId,
    string PolicyVersion,
    ScaledPrice? EntryLimitPrice = null,
    ScaledPrice? EntryStopPrice = null);

public enum CanonicalOrderType : byte
{
    Market = 0,
    Limit = 1,
    Stop = 2,
    StopLimit = 3,
}

public enum CanonicalTimeInForce : byte
{
    Day = 0,
    GoodTillCancelled = 1,
    ImmediateOrCancel = 2,
    FillOrKill = 3,
}

public enum OrderDomainFault : byte
{
    None = 0,
    InvalidIdentity = 1,
    InvalidQuantity = 2,
    InvalidPriceTerms = 3,
    InvalidTradeIntent = 4,
    UnsupportedOrderType = 5,
    UnsupportedTimeInForce = 6,
    InvalidClassification = 7,
}

/// <summary>Windows-compatible native-order terms, kept separate from the older Mac command DTO.</summary>
public readonly record struct CanonicalOrderTerms(
    OrderSide Side,
    CanonicalOrderType OrderType,
    CanonicalTimeInForce TimeInForce,
    ScaledQuantity Quantity,
    ScaledPrice? LimitPrice,
    ScaledPrice? StopPrice)
{
    public OrderDomainFault Validate()
    {
        if (!Enum.IsDefined(Side) || !Enum.IsDefined(OrderType) || !Enum.IsDefined(TimeInForce))
            return OrderDomainFault.InvalidClassification;
        if (!Quantity.IsValid || Quantity.Coefficient <= 0)
            return OrderDomainFault.InvalidQuantity;
        if (LimitPrice.HasValue && (!LimitPrice.Value.IsValid || LimitPrice.Value.Coefficient <= 0) ||
            StopPrice.HasValue && (!StopPrice.Value.IsValid || StopPrice.Value.Coefficient <= 0))
            return OrderDomainFault.InvalidPriceTerms;

        var validShape = OrderType switch
        {
            CanonicalOrderType.Market => !LimitPrice.HasValue && !StopPrice.HasValue,
            CanonicalOrderType.Limit => LimitPrice.HasValue && !StopPrice.HasValue,
            CanonicalOrderType.Stop => !LimitPrice.HasValue && StopPrice.HasValue,
            CanonicalOrderType.StopLimit => LimitPrice.HasValue && StopPrice.HasValue,
            _ => false,
        };
        return validShape ? OrderDomainFault.None : OrderDomainFault.InvalidPriceTerms;
    }
}

/// <summary>One immutable binding of intent, identity, and the exact native order to be released.</summary>
public sealed record CanonicalOrderInstruction(
    OrderIdentity Identity,
    TradeIntent TradeIntent,
    CanonicalOrderTerms Terms)
{
    [JsonIgnore]
    public string CanonicalJson => ExecutionCanonicalJson.Serialize(this);

    [JsonIgnore]
    public string InstructionHashSha256 => ExecutionCanonicalJson.Sha256(CanonicalJson);

    public OrderDomainFault Validate()
    {
        if (Identity is null || !Identity.IsValid)
            return OrderDomainFault.InvalidIdentity;
        if (!Enum.IsDefined(TradeIntent.QuantityMode) ||
            TradeIntent.Instrument.IsNone ||
            !TradeIntent.SignedUnits.IsValid ||
            !TradeIntent.EstimatedRoundTripCostPerUnit.IsValid ||
            TradeIntent.EstimatedRoundTripCostPerUnit.Coefficient < 0 ||
            !ValidOptionalPrice(TradeIntent.ProtectiveStopPrice) ||
            !ValidOptionalPrice(TradeIntent.ProfitTargetPrice) ||
            !ValidOptionalPrice(TradeIntent.EntryLimitPrice) ||
            !ValidOptionalPrice(TradeIntent.EntryStopPrice) ||
            string.IsNullOrWhiteSpace(TradeIntent.StrategyId) ||
            string.IsNullOrWhiteSpace(TradeIntent.PolicyVersion))
            return OrderDomainFault.InvalidTradeIntent;

        var termsFault = Terms.Validate();
        if (termsFault != OrderDomainFault.None)
            return termsFault;
        return EntryTermsAgree() ? OrderDomainFault.None : OrderDomainFault.InvalidTradeIntent;
    }

    public static CanonicalOrderType EntryOrderTypeOf(in TradeIntent intent) =>
        (intent.EntryLimitPrice.HasValue, intent.EntryStopPrice.HasValue) switch
        {
            (false, false) => CanonicalOrderType.Market,
            (true, false) => CanonicalOrderType.Limit,
            (false, true) => CanonicalOrderType.Stop,
            (true, true) => CanonicalOrderType.StopLimit,
        };

    private bool EntryTermsAgree()
    {
        if (!TradeIntent.EntryLimitPrice.HasValue && !TradeIntent.EntryStopPrice.HasValue)
            return true;
        return EntryOrderTypeOf(TradeIntent) == Terms.OrderType &&
               ExactlyEquals(TradeIntent.EntryLimitPrice, Terms.LimitPrice) &&
               ExactlyEquals(TradeIntent.EntryStopPrice, Terms.StopPrice);
    }

    private static bool ValidOptionalPrice(ScaledPrice? value) =>
        !value.HasValue || value.Value.IsValid && value.Value.Coefficient > 0;

    private static bool ExactlyEquals(ScaledPrice? left, ScaledPrice? right) =>
        !left.HasValue || !right.HasValue
            ? left.HasValue == right.HasValue
            : left.Value.Coefficient == right.Value.Coefficient && left.Value.Scale == right.Value.Scale;
}

/// <summary>Host-owned intent fields that are absent from the older Mac submit DTO.</summary>
public sealed record CanonicalInstructionMappingContext
{
    public CanonicalInstructionMappingContext(
        IntentId intentId,
        BucketId? bucketId,
        LegId legId,
        ExecutionLeaseId executionLeaseId,
        FencingToken fencingToken,
        TradeIntentQuantityMode quantityMode,
        ScaledQuantity signedUnits,
        ScaledQuantity currentPosition,
        ScaledPrice? protectiveStopPrice,
        ScaledPrice? profitTargetPrice,
        ScaledMoney estimatedRoundTripCostPerUnit,
        long strategyNoteId,
        string policyVersion,
        ScaledPrice? entryLimitPrice = null,
        ScaledPrice? entryStopPrice = null)
    {
        ExecutionIdentifier.Require(intentId, nameof(intentId));
        if (bucketId is { } bucket) ExecutionIdentifier.Require(bucket, nameof(bucketId));
        ExecutionIdentifier.Require(legId, nameof(legId));
        ExecutionIdentifier.Require(executionLeaseId, nameof(executionLeaseId));
        if (!fencingToken.IsValid) throw new ArgumentOutOfRangeException(nameof(fencingToken));
        if (!Enum.IsDefined(quantityMode)) throw new ArgumentOutOfRangeException(nameof(quantityMode));
        if (!signedUnits.IsValid) throw new ArgumentOutOfRangeException(nameof(signedUnits));
        if (!currentPosition.IsValid) throw new ArgumentOutOfRangeException(nameof(currentPosition));
        if (!estimatedRoundTripCostPerUnit.IsValid || estimatedRoundTripCostPerUnit.Coefficient < 0)
            throw new ArgumentOutOfRangeException(nameof(estimatedRoundTripCostPerUnit));
        IntentId = intentId;
        BucketId = bucketId;
        LegId = legId;
        ExecutionLeaseId = executionLeaseId;
        FencingToken = fencingToken;
        QuantityMode = quantityMode;
        SignedUnits = signedUnits;
        CurrentPosition = currentPosition;
        ProtectiveStopPrice = protectiveStopPrice;
        ProfitTargetPrice = profitTargetPrice;
        EstimatedRoundTripCostPerUnit = estimatedRoundTripCostPerUnit;
        StrategyNoteId = strategyNoteId;
        PolicyVersion = ExecutionValidation.RequireText(policyVersion, nameof(policyVersion), 128);
        EntryLimitPrice = entryLimitPrice;
        EntryStopPrice = entryStopPrice;
    }

    public IntentId IntentId { get; }
    public BucketId? BucketId { get; }
    public LegId LegId { get; }
    public ExecutionLeaseId ExecutionLeaseId { get; }
    public FencingToken FencingToken { get; }
    public TradeIntentQuantityMode QuantityMode { get; }
    public ScaledQuantity SignedUnits { get; }
    public ScaledQuantity CurrentPosition { get; }
    public ScaledPrice? ProtectiveStopPrice { get; }
    public ScaledPrice? ProfitTargetPrice { get; }
    public ScaledMoney EstimatedRoundTripCostPerUnit { get; }
    public long StrategyNoteId { get; }
    public string PolicyVersion { get; }
    public ScaledPrice? EntryLimitPrice { get; }
    public ScaledPrice? EntryStopPrice { get; }
}

public static class CanonicalOrderInstructionMapper
{
    public static OrderDomainFault TryCreate(
        ExecutionCommandMetadata metadata,
        ClientOrderId clientOrderId,
        OrderTerms terms,
        CanonicalInstructionMappingContext context,
        out CanonicalOrderInstruction? instruction)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(context);
        instruction = null;
        var causation = metadata.CausationId ?? new CausationId(metadata.CommandId.Value);
        var identity = new OrderIdentity(
            context.IntentId,
            context.BucketId,
            context.LegId,
            clientOrderId,
            brokerOrderId: null,
            exchangeOrderId: null,
            metadata.CorrelationId,
            causation,
            context.ExecutionLeaseId,
            context.FencingToken);
        var tradeIntent = new TradeIntent(
            metadata.InstrumentId,
            context.QuantityMode,
            context.SignedUnits,
            context.ProtectiveStopPrice,
            context.ProfitTargetPrice,
            context.EstimatedRoundTripCostPerUnit,
            metadata.StrategyId.Value,
            context.StrategyNoteId,
            context.PolicyVersion,
            context.EntryLimitPrice,
            context.EntryStopPrice);
        var canonicalTerms = ToCanonicalTerms(terms);
        var candidate = new CanonicalOrderInstruction(identity, tradeIntent, canonicalTerms);
        var fault = candidate.Validate();
        if (fault != OrderDomainFault.None)
            return fault;
        if (!SignedEconomicsAgree(candidate, context.CurrentPosition))
            return OrderDomainFault.InvalidTradeIntent;
        instruction = candidate;
        return OrderDomainFault.None;
    }

    public static bool MatchesCommand(
        CanonicalOrderInstruction instruction,
        ExecutionCommandMetadata metadata,
        ClientOrderId clientOrderId,
        OrderTerms terms)
    {
        ArgumentNullException.ThrowIfNull(instruction);
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(terms);
        var causation = metadata.CausationId ?? new CausationId(metadata.CommandId.Value);
        return instruction.Validate() == OrderDomainFault.None &&
               instruction.Identity.ClientOrderId == clientOrderId &&
               instruction.Identity.CorrelationId == metadata.CorrelationId &&
               instruction.Identity.CausationId == causation &&
               instruction.TradeIntent.Instrument == metadata.InstrumentId &&
               string.Equals(instruction.TradeIntent.StrategyId, metadata.StrategyId.Value, StringComparison.Ordinal) &&
               instruction.Terms == ToCanonicalTerms(terms);
    }

    public static bool SignedEconomicsAgree(CanonicalOrderInstruction instruction, ScaledQuantity currentPosition)
    {
        if (!currentPosition.IsValid || !instruction.TradeIntent.SignedUnits.IsValid)
            return false;
        ScaledQuantity signedOrder;
        if (instruction.TradeIntent.QuantityMode == TradeIntentQuantityMode.TargetPosition)
        {
            if (!ScaledValueMath.TrySubtractQuantity(
                    instruction.TradeIntent.SignedUnits,
                    currentPosition,
                    out signedOrder))
                return false;
        }
        else
        {
            signedOrder = instruction.TradeIntent.SignedUnits;
        }
        if (signedOrder.Coefficient == 0 ||
            !ScaledValueMath.TryNarrow(
                Int128.Abs((Int128)signedOrder.Coefficient),
                signedOrder.Scale,
                out var magnitudeCoefficient,
                out var magnitudeScale) ||
            !ScaledValueMath.TryCompare(
                magnitudeCoefficient,
                magnitudeScale,
                instruction.Terms.Quantity.Coefficient,
                instruction.Terms.Quantity.Scale,
                out var quantityComparison) ||
            quantityComparison != 0)
            return false;
        return signedOrder.Coefficient > 0
            ? instruction.Terms.Side == OrderSide.Buy
            : instruction.Terms.Side == OrderSide.Sell;
    }

    /// <summary>
    /// Converts the legacy Mac command shape into the canonical economic terms retained by the
    /// event projection. This is a structural conversion only; no numeric value is rounded.
    /// </summary>
    public static CanonicalOrderTerms ToCanonicalTerms(OrderTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        return new(
        terms.Side,
        terms.Type switch
        {
            OrderType.Market => CanonicalOrderType.Market,
            OrderType.Limit => CanonicalOrderType.Limit,
            OrderType.Stop => CanonicalOrderType.Stop,
            OrderType.StopLimit => CanonicalOrderType.StopLimit,
            _ => (CanonicalOrderType)byte.MaxValue,
        },
        terms.TimeInForce switch
        {
            TimeInForce.Day => CanonicalTimeInForce.Day,
            TimeInForce.Gtc => CanonicalTimeInForce.GoodTillCancelled,
            TimeInForce.Ioc => CanonicalTimeInForce.ImmediateOrCancel,
            TimeInForce.Fok => CanonicalTimeInForce.FillOrKill,
            _ => (CanonicalTimeInForce)byte.MaxValue,
        },
        terms.Quantity,
        terms.LimitPrice,
        terms.StopPrice);
    }
}
