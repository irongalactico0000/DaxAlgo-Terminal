using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingTerminal.Core.Execution;

/// <summary>Shared contract implemented by the strongly typed execution identifiers.</summary>
public interface IExecutionIdentifier
{
    string Value { get; }
    bool IsEmpty { get; }
}

/// <summary>Static parsing contract used by the scalar JSON converter.</summary>
public interface IExecutionIdentifier<TSelf> : IExecutionIdentifier
    where TSelf : struct, IExecutionIdentifier<TSelf>
{
    static abstract TSelf Parse(string value);
}

internal static class ExecutionIdentifier
{
    private const int MaximumLength = 256;

    public static string Validate(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Length > MaximumLength)
            throw new ArgumentOutOfRangeException(parameterName, value.Length, $"Identifier cannot exceed {MaximumLength} characters.");
        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Identifier cannot have leading or trailing whitespace.", parameterName);
        if (value.Any(char.IsControl))
            throw new ArgumentException("Identifier cannot contain control characters.", parameterName);
        return value;
    }

    public static void Require<T>(T value, string parameterName)
        where T : struct, IExecutionIdentifier =>
        ArgumentException.ThrowIfNullOrEmpty(value.Value, parameterName);
}

/// <summary>
/// Writes every execution identifier as one JSON string rather than an object containing a
/// <c>Value</c> property. This is the stable journal/wire representation.
/// </summary>
public sealed class ExecutionIdentifierJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsValueType && typeToConvert.GetInterfaces().Any(
            i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IExecutionIdentifier<>));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (!CanConvert(typeToConvert))
            throw new NotSupportedException($"{typeToConvert} is not an execution identifier.");

        return (JsonConverter)Activator.CreateInstance(
            typeof(ExecutionIdentifierJsonConverter<>).MakeGenericType(typeToConvert))!;
    }

    private sealed class ExecutionIdentifierJsonConverter<T> : JsonConverter<T>
        where T : struct, IExecutionIdentifier<T>
    {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException($"{typeof(T).Name} must be a JSON string.");

            try
            {
                return T.Parse(reader.GetString()!);
            }
            catch (ArgumentException ex)
            {
                throw new JsonException($"Invalid {typeof(T).Name}.", ex);
            }
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            if (value.IsEmpty)
                throw new JsonException($"Default/empty {typeof(T).Name} cannot be serialized.");
            writer.WriteStringValue(value.Value);
        }
    }
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct OrderId : IExecutionIdentifier<OrderId>
{
    public OrderId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static OrderId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct CommandId : IExecutionIdentifier<CommandId>
{
    public CommandId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static CommandId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct DispatchAttemptId : IExecutionIdentifier<DispatchAttemptId>
{
    public DispatchAttemptId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static DispatchAttemptId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct ExecutionEventId : IExecutionIdentifier<ExecutionEventId>
{
    public ExecutionEventId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static ExecutionEventId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct TradeId : IExecutionIdentifier<TradeId>
{
    public TradeId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static TradeId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct ClientOrderId : IExecutionIdentifier<ClientOrderId>
{
    public ClientOrderId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static ClientOrderId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct VenueOrderId : IExecutionIdentifier<VenueOrderId>
{
    public VenueOrderId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static VenueOrderId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct VenueId : IExecutionIdentifier<VenueId>
{
    public VenueId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static VenueId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct TradingAccountId : IExecutionIdentifier<TradingAccountId>
{
    public TradingAccountId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static TradingAccountId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct StrategyId : IExecutionIdentifier<StrategyId>
{
    public StrategyId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static StrategyId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct StrategyVersion : IExecutionIdentifier<StrategyVersion>
{
    public StrategyVersion(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static StrategyVersion Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct CorrelationId : IExecutionIdentifier<CorrelationId>
{
    public CorrelationId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static CorrelationId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct CausationId : IExecutionIdentifier<CausationId>
{
    public CausationId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static CausationId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct OutboxId : IExecutionIdentifier<OutboxId>
{
    public OutboxId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static OutboxId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct RuntimeInstanceId : IExecutionIdentifier<RuntimeInstanceId>
{
    public RuntimeInstanceId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static RuntimeInstanceId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

// Canonical OMS identities. These coexist with the earlier TradeIR/command identifiers while the
// execution paths converge; each uses the same validated scalar JSON representation.

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct IntentId : IExecutionIdentifier<IntentId>
{
    public IntentId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static IntentId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct BucketId : IExecutionIdentifier<BucketId>
{
    public BucketId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static BucketId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct LegId : IExecutionIdentifier<LegId>
{
    public LegId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static LegId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct BrokerOrderId : IExecutionIdentifier<BrokerOrderId>
{
    public BrokerOrderId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static BrokerOrderId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct ExchangeOrderId : IExecutionIdentifier<ExchangeOrderId>
{
    public ExchangeOrderId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static ExchangeOrderId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct ExecutionLeaseId : IExecutionIdentifier<ExecutionLeaseId>
{
    public ExecutionLeaseId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static ExecutionLeaseId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct FencingToken(long Value)
{
    public bool IsValid => Value > 0;
    public bool IsNewerThan(FencingToken older) => IsValid && Value > older.Value;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct DeduplicationKey : IExecutionIdentifier<DeduplicationKey>
{
    public DeduplicationKey(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static DeduplicationKey Parse(string value) => new(value);
    public DeduplicationKey Derive(string suffix) => new($"{Value}:{ExecutionIdentifier.Validate(suffix, nameof(suffix))}");
    public override string ToString() => Value ?? string.Empty;
}

[JsonConverter(typeof(ExecutionIdentifierJsonConverterFactory))]
public readonly record struct ReconciliationCaseId : IExecutionIdentifier<ReconciliationCaseId>
{
    public ReconciliationCaseId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
    public string Value { get; }
    public bool IsEmpty => string.IsNullOrEmpty(Value);
    public static ReconciliationCaseId Parse(string value) => new(value);
    public override string ToString() => Value ?? string.Empty;
}

/// <summary>All identity and ownership fields frozen into one canonical native order.</summary>
public sealed record OrderIdentity
{
    public OrderIdentity(
        IntentId intentId,
        BucketId? bucketId,
        LegId legId,
        ClientOrderId clientOrderId,
        BrokerOrderId? brokerOrderId,
        ExchangeOrderId? exchangeOrderId,
        CorrelationId correlationId,
        CausationId causationId,
        ExecutionLeaseId executionLeaseId,
        FencingToken fencingToken)
    {
        ExecutionIdentifier.Require(intentId, nameof(intentId));
        if (bucketId is { } bucket) ExecutionIdentifier.Require(bucket, nameof(bucketId));
        ExecutionIdentifier.Require(legId, nameof(legId));
        ExecutionIdentifier.Require(clientOrderId, nameof(clientOrderId));
        if (brokerOrderId is { } brokerOrder) ExecutionIdentifier.Require(brokerOrder, nameof(brokerOrderId));
        if (exchangeOrderId is { } exchangeOrder) ExecutionIdentifier.Require(exchangeOrder, nameof(exchangeOrderId));
        ExecutionIdentifier.Require(correlationId, nameof(correlationId));
        ExecutionIdentifier.Require(causationId, nameof(causationId));
        ExecutionIdentifier.Require(executionLeaseId, nameof(executionLeaseId));
        if (!fencingToken.IsValid) throw new ArgumentOutOfRangeException(nameof(fencingToken));

        IntentId = intentId;
        BucketId = bucketId;
        LegId = legId;
        ClientOrderId = clientOrderId;
        BrokerOrderId = brokerOrderId;
        ExchangeOrderId = exchangeOrderId;
        CorrelationId = correlationId;
        CausationId = causationId;
        ExecutionLeaseId = executionLeaseId;
        FencingToken = fencingToken;
    }

    public IntentId IntentId { get; }
    public BucketId? BucketId { get; }
    public LegId LegId { get; }
    public ClientOrderId ClientOrderId { get; }
    public BrokerOrderId? BrokerOrderId { get; }
    public ExchangeOrderId? ExchangeOrderId { get; }
    public CorrelationId CorrelationId { get; }
    public CausationId CausationId { get; }
    public ExecutionLeaseId ExecutionLeaseId { get; }
    public FencingToken FencingToken { get; }
    public bool IsValid =>
        !IntentId.IsEmpty &&
        (!BucketId.HasValue || !BucketId.Value.IsEmpty) &&
        !LegId.IsEmpty &&
        !ClientOrderId.IsEmpty &&
        (!BrokerOrderId.HasValue || !BrokerOrderId.Value.IsEmpty) &&
        (!ExchangeOrderId.HasValue || !ExchangeOrderId.Value.IsEmpty) &&
        !CorrelationId.IsEmpty &&
        !CausationId.IsEmpty &&
        !ExecutionLeaseId.IsEmpty &&
        FencingToken.IsValid;
}
