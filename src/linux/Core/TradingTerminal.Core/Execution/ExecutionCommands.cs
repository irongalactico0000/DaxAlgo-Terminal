using System.Text.Json.Serialization;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Trading;

namespace TradingTerminal.Core.Execution;

public sealed record ExecutionCommandMetadata
{
    public ExecutionCommandMetadata(
        CommandId commandId,
        CorrelationId correlationId,
        CausationId? causationId,
        TradingAccountId tradingAccountId,
        StrategyId strategyId,
        StrategyVersion strategyVersion,
        VenueId venueId,
        InstrumentId instrumentId,
        ExecutionEnvironment environment,
        DateTimeOffset createdAtUtc,
        long expectedOrderSequence,
        DateTimeOffset? expiresAtUtc = null,
        int schemaVersion = ExecutionSchema.CurrentVersion)
    {
        ExecutionSchema.RequireSupported(schemaVersion);
        ExecutionIdentifier.Require(commandId, nameof(commandId));
        ExecutionIdentifier.Require(correlationId, nameof(correlationId));
        if (causationId is { } cause)
            ExecutionIdentifier.Require(cause, nameof(causationId));
        ExecutionIdentifier.Require(tradingAccountId, nameof(tradingAccountId));
        ExecutionIdentifier.Require(strategyId, nameof(strategyId));
        ExecutionIdentifier.Require(strategyVersion, nameof(strategyVersion));
        ExecutionIdentifier.Require(venueId, nameof(venueId));
        if (instrumentId.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(instrumentId), "A resolved canonical instrument id is required.");
        if (!Enum.IsDefined(environment))
            throw new ArgumentOutOfRangeException(nameof(environment));
        if (expectedOrderSequence < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedOrderSequence));

        var created = ExecutionValidation.RequireUtc(createdAtUtc, nameof(createdAtUtc));
        if (expiresAtUtc is { } expires)
        {
            ExecutionValidation.RequireUtc(expires, nameof(expiresAtUtc));
            if (expires <= created)
                throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Expiry must be later than command creation.");
        }

        SchemaVersion = schemaVersion;
        CommandId = commandId;
        CorrelationId = correlationId;
        CausationId = causationId;
        TradingAccountId = tradingAccountId;
        StrategyId = strategyId;
        StrategyVersion = strategyVersion;
        VenueId = venueId;
        InstrumentId = instrumentId;
        Environment = environment;
        CreatedAtUtc = created;
        ExpectedOrderSequence = expectedOrderSequence;
        ExpiresAtUtc = expiresAtUtc;
    }

    public int SchemaVersion { get; }
    public CommandId CommandId { get; }
    public CorrelationId CorrelationId { get; }
    public CausationId? CausationId { get; }
    public TradingAccountId TradingAccountId { get; }
    public StrategyId StrategyId { get; }
    public StrategyVersion StrategyVersion { get; }
    public VenueId VenueId { get; }
    public InstrumentId InstrumentId { get; }
    public ExecutionEnvironment Environment { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public long ExpectedOrderSequence { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }
}

public sealed record OrderTerms
{
    public OrderTerms(
        OrderSide side,
        OrderType type,
        ScaledQuantity quantity,
        ScaledPrice? limitPrice = null,
        ScaledPrice? stopPrice = null,
        TimeInForce timeInForce = TimeInForce.Day,
        bool reduceOnly = false)
    {
        if (!Enum.IsDefined(side))
            throw new ArgumentOutOfRangeException(nameof(side));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));
        if (!Enum.IsDefined(timeInForce))
            throw new ArgumentOutOfRangeException(nameof(timeInForce));
        if (!quantity.IsValid || quantity.Coefficient <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));
        if (limitPrice is { } limit && (!limit.IsValid || limit.Coefficient <= 0))
            throw new ArgumentOutOfRangeException(nameof(limitPrice));
        if (stopPrice is { } stop && (!stop.IsValid || stop.Coefficient <= 0))
            throw new ArgumentOutOfRangeException(nameof(stopPrice));

        var requiresLimit = type is OrderType.Limit or OrderType.StopLimit;
        var requiresStop = type is OrderType.Stop or OrderType.StopLimit;
        if (requiresLimit != limitPrice.HasValue)
            throw new ArgumentException(requiresLimit ? "A limit price is required." : "A limit price is not valid for this order type.", nameof(limitPrice));
        if (requiresStop != stopPrice.HasValue)
            throw new ArgumentException(requiresStop ? "A stop price is required." : "A stop price is not valid for this order type.", nameof(stopPrice));

        Side = side;
        Type = type;
        Quantity = quantity;
        LimitPrice = limitPrice;
        StopPrice = stopPrice;
        TimeInForce = timeInForce;
        ReduceOnly = reduceOnly;
    }

    public OrderSide Side { get; }
    public OrderType Type { get; }
    public ScaledQuantity Quantity { get; }
    public ScaledPrice? LimitPrice { get; }
    public ScaledPrice? StopPrice { get; }
    public TimeInForce TimeInForce { get; }
    public bool ReduceOnly { get; }
}

public enum ExecutionCommandKind
{
    Submit = 1,
    Replace = 2,
    Cancel = 3,
    Query = 4,
}

public enum ExecutionSafetyClassification
{
    ExposureIncreasing = 1,
    ExposureReducingOrNeutral = 2,
    QueryOnly = 3,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SubmitOrderCommand), "submit")]
[JsonDerivedType(typeof(ReplaceOrderCommand), "replace")]
[JsonDerivedType(typeof(CancelOrderCommand), "cancel")]
[JsonDerivedType(typeof(QueryOrderCommand), "query")]
public abstract record ExecutionCommand
{
    protected ExecutionCommand(ExecutionCommandMetadata metadata, OrderId orderId)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ExecutionIdentifier.Require(orderId, nameof(orderId));
        Metadata = metadata;
        OrderId = orderId;
    }

    public ExecutionCommandMetadata Metadata { get; }
    public OrderId OrderId { get; }
    [JsonIgnore]
    public abstract ExecutionCommandKind Kind { get; }
    public abstract ExecutionSafetyClassification Safety { get; }
    [JsonIgnore]
    public string CanonicalJson => ExecutionCanonicalJson.Serialize<ExecutionCommand>(this);
    [JsonIgnore]
    public string PayloadHashSha256 => ExecutionCanonicalJson.Sha256(CanonicalJson);
}

public sealed record SubmitOrderCommand : ExecutionCommand
{
    public SubmitOrderCommand(
        ExecutionCommandMetadata metadata,
        OrderId orderId,
        ClientOrderId clientOrderId,
        OrderTerms terms,
        CanonicalOrderInstruction canonicalInstruction)
        : base(metadata, orderId)
    {
        if (metadata.ExpectedOrderSequence != 0)
            throw new ArgumentException("Submit expects an uninitialized order sequence of zero.", nameof(metadata));
        ExecutionIdentifier.Require(clientOrderId, nameof(clientOrderId));
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(canonicalInstruction);
        if (!CanonicalOrderInstructionMapper.MatchesCommand(canonicalInstruction, metadata, clientOrderId, terms))
            throw new ArgumentException("Canonical instruction does not match the submit command economics or provenance.", nameof(canonicalInstruction));
        ClientOrderId = clientOrderId;
        Terms = terms;
        CanonicalInstruction = canonicalInstruction;
    }

    public ClientOrderId ClientOrderId { get; }
    public OrderTerms Terms { get; }
    public CanonicalOrderInstruction CanonicalInstruction { get; }
    public override ExecutionCommandKind Kind => ExecutionCommandKind.Submit;
    public override ExecutionSafetyClassification Safety =>
        Terms.ReduceOnly ? ExecutionSafetyClassification.ExposureReducingOrNeutral : ExecutionSafetyClassification.ExposureIncreasing;
}

public sealed record ReplaceOrderCommand : ExecutionCommand
{
    public ReplaceOrderCommand(ExecutionCommandMetadata metadata, OrderId orderId, OrderTerms replacementTerms)
        : base(metadata, orderId)
    {
        if (metadata.ExpectedOrderSequence <= 0)
            throw new ArgumentException("Replace requires an existing order sequence.", nameof(metadata));
        ArgumentNullException.ThrowIfNull(replacementTerms);
        ReplacementTerms = replacementTerms;
    }

    public OrderTerms ReplacementTerms { get; }
    public override ExecutionCommandKind Kind => ExecutionCommandKind.Replace;
    public override ExecutionSafetyClassification Safety =>
        ReplacementTerms.ReduceOnly ? ExecutionSafetyClassification.ExposureReducingOrNeutral : ExecutionSafetyClassification.ExposureIncreasing;
}

public sealed record CancelOrderCommand : ExecutionCommand
{
    public CancelOrderCommand(ExecutionCommandMetadata metadata, OrderId orderId) : base(metadata, orderId)
    {
        if (metadata.ExpectedOrderSequence <= 0)
            throw new ArgumentException("Cancel requires an existing order sequence.", nameof(metadata));
    }

    public override ExecutionCommandKind Kind => ExecutionCommandKind.Cancel;
    public override ExecutionSafetyClassification Safety => ExecutionSafetyClassification.ExposureReducingOrNeutral;
}

public sealed record QueryOrderCommand : ExecutionCommand
{
    public QueryOrderCommand(ExecutionCommandMetadata metadata, OrderId orderId) : base(metadata, orderId)
    {
        if (metadata.ExpectedOrderSequence <= 0)
            throw new ArgumentException("Query requires an existing order sequence.", nameof(metadata));
    }

    public override ExecutionCommandKind Kind => ExecutionCommandKind.Query;
    public override ExecutionSafetyClassification Safety => ExecutionSafetyClassification.QueryOnly;
}
