# TradingTerminal.Core / Execution — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Core/TradingTerminal.Core/Execution/CanonicalOrderInstruction.cs
```cs
    7: public enum TradeIntentQuantityMode : byte
   14: public readonly record struct TradeIntent(
   27: public enum CanonicalOrderType : byte
   35: public enum CanonicalTimeInForce : byte
   43: public enum OrderDomainFault : byte
   56: public readonly record struct CanonicalOrderTerms(
   64: public OrderDomainFault Validate()
   87: public sealed record CanonicalOrderInstruction(
   93: public string CanonicalJson => ExecutionCanonicalJson.Serialize(this);
   96: public string InstructionHashSha256 => ExecutionCanonicalJson.Sha256(CanonicalJson);
   98: public OrderDomainFault Validate()
  121: public static CanonicalOrderType EntryOrderTypeOf(in TradeIntent intent) =>
  149: public sealed record CanonicalInstructionMappingContext
  151: public CanonicalInstructionMappingContext(
  195: public IntentId IntentId { get; }
  196: public BucketId? BucketId { get; }
  197: public LegId LegId { get; }
  198: public ExecutionLeaseId ExecutionLeaseId { get; }
  199: public FencingToken FencingToken { get; }
  200: public TradeIntentQuantityMode QuantityMode { get; }
  201: public ScaledQuantity SignedUnits { get; }
  202: public ScaledQuantity CurrentPosition { get; }
  203: public ScaledPrice? ProtectiveStopPrice { get; }
  204: public ScaledPrice? ProfitTargetPrice { get; }
  205: public ScaledMoney EstimatedRoundTripCostPerUnit { get; }
  206: public long StrategyNoteId { get; }
  207: public string PolicyVersion { get; }
  208: public ScaledPrice? EntryLimitPrice { get; }
  209: public ScaledPrice? EntryStopPrice { get; }
  212: public static class CanonicalOrderInstructionMapper
  214: public static OrderDomainFault TryCreate(
  260: public static bool MatchesCommand(
  279: public static bool SignedEconomicsAgree(CanonicalOrderInstruction instruction, ScaledQuantity currentPosition)
  319: public static CanonicalOrderTerms ToCanonicalTerms(OrderTerms terms)
```

## src/linux/Core/TradingTerminal.Core/Execution/DeterministicPaperVenue.cs
```cs
    7: public sealed record PaperMarketSnapshot
    9: public PaperMarketSnapshot(
   25: public PaperMarketSnapshot(
   51: public InstrumentId InstrumentId { get; }
   52: public ScaledPrice Bid { get; }
   53: public ScaledPrice Ask { get; }
   54: public ScaledQuantity BidAvailableQuantity { get; }
   55: public ScaledQuantity AskAvailableQuantity { get; }
   56: public DateTimeOffset ObservedAtUtc { get; }
   59: public enum PaperVenueRecoveryFault : byte
   75: public sealed record PaperVenueRecoveryResult(
   83: public bool IsSuccess => Fault == PaperVenueRecoveryFault.None;
   90: public sealed class DeterministicPaperVenue : IPaperExecutionDispatcher, IExecutionReconciliationSnapshotProvider
  107: public ExecutionDispatchResult Submit(
  150: public ExecutionDispatchResult Cancel(
  175: public ExecutionDispatchResult Replace(
  213: public void OnMarket(PaperMarketSnapshot market)
  252: public void ExpireDayOrders(DateTimeOffset expiredAtUtc)
  272: public IReadOnlyList<PaperVenueEvent> DrainEvents()
  288: public PaperVenueRecoveryResult RestoreFromLedger(
  554: public ExecutionReconciliationSnapshot CaptureReconciliationSnapshot(
  854: public PaperOrder(
  868: public SubmitOrderCommand SubmitCommand { get; }
  869: public BrokerOrderId BrokerOrderId { get; }
  870: public ExecutionDispatchReceipt SubmitReceipt { get; }
  871: public long SubmissionSequence { get; }
  872: public OrderTerms Terms { get; set; }
  873: public ScaledQuantity FilledQuantity { get; set; } = ScaledQuantity.Zero;
  874: public bool StopActivated { get; set; }
  875: public CausationId CausationId { get; set; }
  876: public OrderLifecycleState State { get; set; } = OrderLifecycleState.Working;
  877: public PaperRecoveryAction RecoveryAction { get; set; }
  878: public bool RecoveryDispatchWasRecorded { get; set; }
```

## src/linux/Core/TradingTerminal.Core/Execution/ExecutionCommands.cs
```cs
    7: public sealed record ExecutionCommandMetadata
    9: public ExecutionCommandMetadata(
   63: public int SchemaVersion { get; }
   64: public CommandId CommandId { get; }
   65: public CorrelationId CorrelationId { get; }
   66: public CausationId? CausationId { get; }
   67: public TradingAccountId TradingAccountId { get; }
   68: public StrategyId StrategyId { get; }
   69: public StrategyVersion StrategyVersion { get; }
   70: public VenueId VenueId { get; }
   71: public InstrumentId InstrumentId { get; }
   72: public ExecutionEnvironment Environment { get; }
   73: public DateTimeOffset CreatedAtUtc { get; }
   74: public long ExpectedOrderSequence { get; }
   75: public DateTimeOffset? ExpiresAtUtc { get; }
   78: public sealed record OrderTerms
   80: public OrderTerms(
  118: public OrderSide Side { get; }
  119: public OrderType Type { get; }
  120: public ScaledQuantity Quantity { get; }
  121: public ScaledPrice? LimitPrice { get; }
  122: public ScaledPrice? StopPrice { get; }
  123: public TimeInForce TimeInForce { get; }
  124: public bool ReduceOnly { get; }
  127: public enum ExecutionCommandKind
  135: public enum ExecutionSafetyClassification
  147: public abstract record ExecutionCommand
  149: protected ExecutionCommand(ExecutionCommandMetadata metadata, OrderId orderId)
  157: public ExecutionCommandMetadata Metadata { get; }
  158: public OrderId OrderId { get; }
  160: public abstract ExecutionCommandKind Kind { get; }
  161: public abstract ExecutionSafetyClassification Safety { get; }
  163: public string CanonicalJson => ExecutionCanonicalJson.Serialize<ExecutionCommand>(this);
  165: public string PayloadHashSha256 => ExecutionCanonicalJson.Sha256(CanonicalJson);
  168: public sealed record SubmitOrderCommand : ExecutionCommand
  170: public SubmitOrderCommand(
  190: public ClientOrderId ClientOrderId { get; }
  191: public OrderTerms Terms { get; }
  192: public CanonicalOrderInstruction CanonicalInstruction { get; }
  193: public override ExecutionCommandKind Kind => ExecutionCommandKind.Submit;
  194: public override ExecutionSafetyClassification Safety =>
  198: public sealed record ReplaceOrderCommand : ExecutionCommand
  200: public ReplaceOrderCommand(ExecutionCommandMetadata metadata, OrderId orderId, OrderTerms replacementTerms)
  209: public OrderTerms ReplacementTerms { get; }
  210: public override ExecutionCommandKind Kind => ExecutionCommandKind.Replace;
  211: public override ExecutionSafetyClassification Safety =>
  215: public sealed record CancelOrderCommand : ExecutionCommand
  217: public CancelOrderCommand(ExecutionCommandMetadata metadata, OrderId orderId) : base(metadata, orderId)
  223: public override ExecutionCommandKind Kind => ExecutionCommandKind.Cancel;
  224: public override ExecutionSafetyClassification Safety => ExecutionSafetyClassification.ExposureReducingOrNeutral;
  227: public sealed record QueryOrderCommand : ExecutionCommand
  229: public QueryOrderCommand(ExecutionCommandMetadata metadata, OrderId orderId) : base(metadata, orderId)
  235: public override ExecutionCommandKind Kind => ExecutionCommandKind.Query;
  236: public override ExecutionSafetyClassification Safety => ExecutionSafetyClassification.QueryOnly;
```

## src/linux/Core/TradingTerminal.Core/Execution/ExecutionFrameTransport.cs
```cs
    8: public interface IExecutionFrameTransport : IAsyncDisposable
   10:     ValueTask WriteAsync<TFrame>(TFrame frame, CancellationToken cancellationToken = default);
   11:     ValueTask<TFrame> ReadAsync<TFrame>(CancellationToken cancellationToken = default);
   18: public sealed class StreamExecutionFrameTransport : IExecutionFrameTransport
   20: public const int DefaultMaximumFrameBytes = 1024 * 1024;
   35: public StreamExecutionFrameTransport(
   49: public async ValueTask WriteAsync<TFrame>(TFrame frame, CancellationToken cancellationToken = default)
   81: public async ValueTask<TFrame> ReadAsync<TFrame>(CancellationToken cancellationToken = default)
  111: public async ValueTask DisposeAsync()
```

## src/linux/Core/TradingTerminal.Core/Execution/ExecutionIdentifiers.cs
```cs
    7: public interface IExecutionIdentifier
    9:     string Value { get; }
   10:     bool IsEmpty { get; }
   14: public interface IExecutionIdentifier<TSelf> : IExecutionIdentifier
   15:     where TSelf : struct, IExecutionIdentifier<TSelf>
   17:     static abstract TSelf Parse(string value);
   24: public static string Validate(string value, string parameterName)
   36: public static void Require<T>(T value, string parameterName)
   45: public sealed class ExecutionIdentifierJsonConverterFactory : JsonConverterFactory
   47: public override bool CanConvert(Type typeToConvert) =>
   51: public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
   63: public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
   78: public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
   88: public readonly record struct OrderId : IExecutionIdentifier<OrderId>
   90: public OrderId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
   91: public string Value { get; }
   92: public bool IsEmpty => string.IsNullOrEmpty(Value);
   93: public static OrderId Parse(string value) => new(value);
   94: public override string ToString() => Value ?? string.Empty;
   98: public readonly record struct CommandId : IExecutionIdentifier<CommandId>
  100: public CommandId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  101: public string Value { get; }
  102: public bool IsEmpty => string.IsNullOrEmpty(Value);
  103: public static CommandId Parse(string value) => new(value);
  104: public override string ToString() => Value ?? string.Empty;
  108: public readonly record struct DispatchAttemptId : IExecutionIdentifier<DispatchAttemptId>
  110: public DispatchAttemptId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  111: public string Value { get; }
  112: public bool IsEmpty => string.IsNullOrEmpty(Value);
  113: public static DispatchAttemptId Parse(string value) => new(value);
  114: public override string ToString() => Value ?? string.Empty;
  118: public readonly record struct ExecutionEventId : IExecutionIdentifier<ExecutionEventId>
  120: public ExecutionEventId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  121: public string Value { get; }
  122: public bool IsEmpty => string.IsNullOrEmpty(Value);
  123: public static ExecutionEventId Parse(string value) => new(value);
  124: public override string ToString() => Value ?? string.Empty;
  128: public readonly record struct TradeId : IExecutionIdentifier<TradeId>
  130: public TradeId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  131: public string Value { get; }
  132: public bool IsEmpty => string.IsNullOrEmpty(Value);
  133: public static TradeId Parse(string value) => new(value);
  134: public override string ToString() => Value ?? string.Empty;
  138: public readonly record struct ClientOrderId : IExecutionIdentifier<ClientOrderId>
  140: public ClientOrderId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  141: public string Value { get; }
  142: public bool IsEmpty => string.IsNullOrEmpty(Value);
  143: public static ClientOrderId Parse(string value) => new(value);
  144: public override string ToString() => Value ?? string.Empty;
  148: public readonly record struct VenueOrderId : IExecutionIdentifier<VenueOrderId>
  150: public VenueOrderId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  151: public string Value { get; }
  152: public bool IsEmpty => string.IsNullOrEmpty(Value);
  153: public static VenueOrderId Parse(string value) => new(value);
  154: public override string ToString() => Value ?? string.Empty;
  158: public readonly record struct VenueId : IExecutionIdentifier<VenueId>
  160: public VenueId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  161: public string Value { get; }
  162: public bool IsEmpty => string.IsNullOrEmpty(Value);
  163: public static VenueId Parse(string value) => new(value);
  164: public override string ToString() => Value ?? string.Empty;
  168: public readonly record struct TradingAccountId : IExecutionIdentifier<TradingAccountId>
  170: public TradingAccountId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  171: public string Value { get; }
  172: public bool IsEmpty => string.IsNullOrEmpty(Value);
  173: public static TradingAccountId Parse(string value) => new(value);
  174: public override string ToString() => Value ?? string.Empty;
  178: public readonly record struct StrategyId : IExecutionIdentifier<StrategyId>
  180: public StrategyId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  181: public string Value { get; }
  182: public bool IsEmpty => string.IsNullOrEmpty(Value);
  183: public static StrategyId Parse(string value) => new(value);
  184: public override string ToString() => Value ?? string.Empty;
  188: public readonly record struct StrategyVersion : IExecutionIdentifier<StrategyVersion>
  190: public StrategyVersion(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  191: public string Value { get; }
  192: public bool IsEmpty => string.IsNullOrEmpty(Value);
  193: public static StrategyVersion Parse(string value) => new(value);
  194: public override string ToString() => Value ?? string.Empty;
  198: public readonly record struct CorrelationId : IExecutionIdentifier<CorrelationId>
  200: public CorrelationId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  201: public string Value { get; }
  202: public bool IsEmpty => string.IsNullOrEmpty(Value);
  203: public static CorrelationId Parse(string value) => new(value);
  204: public override string ToString() => Value ?? string.Empty;
  208: public readonly record struct CausationId : IExecutionIdentifier<CausationId>
  210: public CausationId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  211: public string Value { get; }
  212: public bool IsEmpty => string.IsNullOrEmpty(Value);
  213: public static CausationId Parse(string value) => new(value);
  214: public override string ToString() => Value ?? string.Empty;
  218: public readonly record struct OutboxId : IExecutionIdentifier<OutboxId>
  220: public OutboxId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  221: public string Value { get; }
  222: public bool IsEmpty => string.IsNullOrEmpty(Value);
  223: public static OutboxId Parse(string value) => new(value);
  224: public override string ToString() => Value ?? string.Empty;
  228: public readonly record struct RuntimeInstanceId : IExecutionIdentifier<RuntimeInstanceId>
  230: public RuntimeInstanceId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  231: public string Value { get; }
  232: public bool IsEmpty => string.IsNullOrEmpty(Value);
  233: public static RuntimeInstanceId Parse(string value) => new(value);
  234: public override string ToString() => Value ?? string.Empty;
  241: public readonly record struct IntentId : IExecutionIdentifier<IntentId>
  243: public IntentId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  244: public string Value { get; }
  245: public bool IsEmpty => string.IsNullOrEmpty(Value);
  246: public static IntentId Parse(string value) => new(value);
  247: public override string ToString() => Value ?? string.Empty;
  251: public readonly record struct BucketId : IExecutionIdentifier<BucketId>
  253: public BucketId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  254: public string Value { get; }
  255: public bool IsEmpty => string.IsNullOrEmpty(Value);
  256: public static BucketId Parse(string value) => new(value);
  257: public override string ToString() => Value ?? string.Empty;
  261: public readonly record struct LegId : IExecutionIdentifier<LegId>
  263: public LegId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  264: public string Value { get; }
  265: public bool IsEmpty => string.IsNullOrEmpty(Value);
  266: public static LegId Parse(string value) => new(value);
  267: public override string ToString() => Value ?? string.Empty;
  271: public readonly record struct BrokerOrderId : IExecutionIdentifier<BrokerOrderId>
  273: public BrokerOrderId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  274: public string Value { get; }
  275: public bool IsEmpty => string.IsNullOrEmpty(Value);
  276: public static BrokerOrderId Parse(string value) => new(value);
  277: public override string ToString() => Value ?? string.Empty;
  281: public readonly record struct ExchangeOrderId : IExecutionIdentifier<ExchangeOrderId>
  283: public ExchangeOrderId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  284: public string Value { get; }
  285: public bool IsEmpty => string.IsNullOrEmpty(Value);
  286: public static ExchangeOrderId Parse(string value) => new(value);
  287: public override string ToString() => Value ?? string.Empty;
  291: public readonly record struct ExecutionLeaseId : IExecutionIdentifier<ExecutionLeaseId>
  293: public ExecutionLeaseId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  294: public string Value { get; }
  295: public bool IsEmpty => string.IsNullOrEmpty(Value);
  296: public static ExecutionLeaseId Parse(string value) => new(value);
  297: public override string ToString() => Value ?? string.Empty;
  300: public readonly record struct FencingToken(long Value)
  302: public bool IsValid => Value > 0;
  303: public bool IsNewerThan(FencingToken older) => IsValid && Value > older.Value;
  307: public readonly record struct DeduplicationKey : IExecutionIdentifier<DeduplicationKey>
  309: public DeduplicationKey(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  310: public string Value { get; }
  311: public bool IsEmpty => string.IsNullOrEmpty(Value);
  312: public static DeduplicationKey Parse(string value) => new(value);
  313: public DeduplicationKey Derive(string suffix) => new($"{Value}:{ExecutionIdentifier.Validate(suffix, nameof(suffix))}");
  314: public override string ToString() => Value ?? string.Empty;
  318: public readonly record struct ReconciliationCaseId : IExecutionIdentifier<ReconciliationCaseId>
  320: public ReconciliationCaseId(string value) => Value = ExecutionIdentifier.Validate(value, nameof(value));
  321: public string Value { get; }
  322: public bool IsEmpty => string.IsNullOrEmpty(Value);
  323: public static ReconciliationCaseId Parse(string value) => new(value);
  324: public override string ToString() => Value ?? string.Empty;
  328: public sealed record OrderIdentity
  330: public OrderIdentity(
  365: public IntentId IntentId { get; }
  366: public BucketId? BucketId { get; }
  367: public LegId LegId { get; }
  368: public ClientOrderId ClientOrderId { get; }
  369: public BrokerOrderId? BrokerOrderId { get; }
  370: public ExchangeOrderId? ExchangeOrderId { get; }
  371: public CorrelationId CorrelationId { get; }
  372: public CausationId CausationId { get; }
  373: public ExecutionLeaseId ExecutionLeaseId { get; }
  374: public FencingToken FencingToken { get; }
  375: public bool IsValid =>
```

## src/linux/Core/TradingTerminal.Core/Execution/ExecutionIpcAuthentication.cs
```cs
    7: public static class ExecutionIpcProtocol
    9: public const int Version1 = 1;
   10: public const int SecretSize = 32;
   11: public const int NonceSize = 32;
   12: public const int ProofSize = 32;
   15: public interface IExecutionNonceSource
   17:     byte[] CreateNonce(int length);
   20: public sealed class CryptographicExecutionNonceSource : IExecutionNonceSource
   22: public static CryptographicExecutionNonceSource Instance { get; } = new();
   24: public byte[] CreateNonce(int length) => length > 0
   29: public enum ExecutionHandshakeFailure : byte
   38: public readonly record struct ExecutionHandshakeResult(
   43: public bool IsAuthenticated => Failure == ExecutionHandshakeFailure.None;
   46: public sealed record ExecutionClientHello(int ProtocolVersion, byte[] ClientNonce);
   47: public sealed record ExecutionServerChallenge(
   53: public sealed record ExecutionClientProof(byte[] Proof);
   54: public sealed record ExecutionHandshakeCompletion(bool Accepted, int ProtocolVersion, string? FailureReason);
   60: public sealed class ExecutionIpcAuthenticator : IDisposable
   72: public ExecutionIpcAuthenticator(
   86: public async ValueTask<ExecutionHandshakeResult> AuthenticateServerAsync(
  136: public async ValueTask<ExecutionHandshakeResult> AuthenticateClientAsync(
  183: public void Dispose()
```

## src/linux/Core/TradingTerminal.Core/Execution/ExecutionLease.cs
```cs
    4: public readonly record struct ExecutionResource(
    9: public bool IsValid =>
   16: public readonly record struct ExecutionLeaseClaim(
   21: public bool IsValid => Resource.IsValid && !LeaseId.IsEmpty && FencingToken.IsValid;
   25: public readonly record struct ExecutionLeaseGrant(
   31: public bool IsValid =>
   39: public enum ExecutionLeaseFault : byte
   51: public readonly record struct ExecutionLeaseAcquireResult(
   56: public bool IsSuccess => Fault == ExecutionLeaseFault.None && Grant is { IsValid: true };
   59: public readonly record struct ExecutionLeaseMutationResult(
   64: public bool IsSuccess => Fault == ExecutionLeaseFault.None;
   67: public readonly record struct ExecutionLeaseValidationResult(
   72: public bool IsSuccess => Fault == ExecutionLeaseFault.None && IsCurrent;
   75: public interface IExecutionLeaseValidator
   77:     ExecutionLeaseValidationResult Validate(in ExecutionLeaseClaim claim, DateTimeOffset atUtc);
   80: public interface IExecutionLeaseStore : IExecutionLeaseValidator
   82:     ExecutionLeaseAcquireResult Acquire(
   83:     ExecutionResource resource,
   84:     ExecutionLeaseId leaseId,
   85:     RuntimeInstanceId ownerId,
   86:     DateTimeOffset acquiredAtUtc,
   87:     DateTimeOffset expiresAtUtc);
   89:     ExecutionLeaseMutationResult Renew(
   90:     in ExecutionLeaseGrant grant,
   91:     DateTimeOffset renewedAtUtc,
   92:     DateTimeOffset expiresAtUtc);
   94:     ExecutionLeaseMutationResult Release(
   95:     in ExecutionLeaseGrant grant,
   96:     DateTimeOffset releasedAtUtc);
  100: public sealed class InMemoryExecutionLeaseStore : IExecutionLeaseStore
  106: public ExecutionLeaseAcquireResult Acquire(
  138: public ExecutionLeaseMutationResult Renew(
  157: public ExecutionLeaseMutationResult Release(
  173: public ExecutionLeaseValidationResult Validate(in ExecutionLeaseClaim claim, DateTimeOffset atUtc)
```

## src/linux/Core/TradingTerminal.Core/Execution/ExecutionSchema.cs
```cs
    8: public static class ExecutionSchema
   10: public const int CurrentVersion = 1;
   12: public static void RequireSupported(int version, string parameterName = "schemaVersion")
   19: public enum ExecutionEnvironment
   25: public enum ExecutionRuntimeMode
   34: public static class ExecutionCanonicalJson
   38: public static string Serialize<T>(T value)
   44: public static string Canonicalize(string json)
   54: public static T Deserialize<T>(string json)
   61: public static string Sha256(string canonicalJson)
   67: public static string Hash<T>(T value) => Sha256(Serialize(value));
  129: public static DateTimeOffset RequireUtc(DateTimeOffset value, string parameterName)
  136: public static string RequireText(string value, string parameterName, int maximumLength = 1024)
  148: public static string RequireSha256(string value, string parameterName)
```

## src/linux/Core/TradingTerminal.Core/Execution/ExecutionServiceContract.cs
```cs
    4: public static class ExecutionServiceProtocol
    6: public const int CurrentVersion = 2;
    7: public const int MaximumRequestIdLength = 128;
    8: public const int MaximumEventsPerExchange = 256;
    9: public const int MaximumReconciliationCasesPerExchange = 128;
   16: public enum ExecutionServiceRequestKind : byte
   28: public sealed record ExecutionSubmitRequest(
   32: public sealed record ExecutionCancelRequest(CancelOrderCommand Command);
   34: public sealed record ExecutionReplaceRequest(
   39: public sealed record ExecutionReconciliationResolutionRequest(
   45: public sealed record ExecutionServiceRequest(
   60: public bool HasValidEnvelope =>
   74: public enum ExecutionServiceFault : byte
   91: public sealed record ExecutionServiceResponse(
  107: public bool IsSuccess => Fault == ExecutionServiceFault.None;
  110: public sealed record ExecutionServiceEvent(long OutboxSequence, OmsOrderEvent Event);
  112: public sealed record ExecutionServiceExchange(
  117: public IReadOnlyList<ReconciliationCase> CaseFacts =>
  125: public interface IExecutionServiceEndpoint
  127:     ExecutionResource Resource { get; }
  128:     ExecutionLeaseGrant LeaseGrant { get; }
  129:     ExecutionServiceExchange Handle(ExecutionServiceRequest request);
  133: public interface IExecutionServiceReconciliationRunner
  135:     ReconciliationCycleResult Run(ReconciliationTrigger trigger, ExecutionResource resource);
  136:     IReadOnlyList<ReconciliationCase> ReadLatestCases(ExecutionResource resource);
  137:     bool CanAdmitNewExposure(ExecutionResource resource);
  138:     bool ResolveCase(
  139:     ExecutionResource resource,
  140:     ReconciliationCaseId caseId,
  141:     string resolvedBy,
  142:     string resolutionEvidence,
  143:     DateTimeOffset resolvedAtUtc);
```

## src/linux/Core/TradingTerminal.Core/Execution/ExecutionServiceEngine.cs
```cs
    9: public sealed class ExecutionServiceEngine : IExecutionServiceEndpoint
   21: public ExecutionServiceEngine(
   39: public ExecutionResource Resource => LeaseGrant.Claim.Resource;
   40: public ExecutionLeaseGrant LeaseGrant { get; }
   46: public ExecutionServiceExchange Handle(ExecutionServiceRequest request)
  426: public sealed class PaperExecutionServiceReconciliationRunner : IExecutionServiceReconciliationRunner
  434: public PaperExecutionServiceReconciliationRunner(
  448: public ReconciliationCycleResult Run(ReconciliationTrigger trigger, ExecutionResource resource)
  473: public IReadOnlyList<ReconciliationCase> ReadLatestCases(ExecutionResource resource)
  483: public bool CanAdmitNewExposure(ExecutionResource resource) =>
  486: public bool ResolveCase(
```

## src/linux/Core/TradingTerminal.Core/Execution/OrderEventStore.cs
```cs
    5: public enum OrderEventAppendStatus : byte
   12: public enum OrderEventAppendFault : byte
   31: public readonly record struct OrderEventAppendResult(
   37: public bool IsSuccess => Fault == OrderEventAppendFault.None && Event is not null;
   38: public bool WasAppended => Status == OrderEventAppendStatus.Appended && IsSuccess;
   39: public bool IsExactReplay => Status == OrderEventAppendStatus.ExactReplay && IsSuccess;
   43: public sealed record OrderEventOutboxEntry(long OutboxSequence, OmsOrderEvent Event);
   49: public interface IOrderEventStore
   51:     OrderEventAppendResult Append(OrderEventDraft draft, DateTimeOffset recordedAtUtc);
   52:     IReadOnlyList<OmsOrderEvent> Read(ClientOrderId aggregateId);
   53:     OmsOrderProjection? ReadProjection(ClientOrderId aggregateId);
   54:     IReadOnlyList<OrderEventOutboxEntry> ReadOutbox(long afterExclusiveSequence = 0);
   58: public interface IExecutionAdmissionGate
   60:     bool CanAdmitNewOrders { get; }
   61:     bool CanAdmitAfterStartupReconciliation { get; }
   68: public interface IExecutionStartupRecoveryGate : IExecutionAdmissionGate
   70:     bool TryCompleteStartupReconciliation(ReconciliationCycleResult result);
   77: public sealed class InMemoryOrderEventStore : IOrderEventStore
   86: public OrderEventAppendResult Append(OrderEventDraft draft, DateTimeOffset recordedAtUtc)
  183: public IReadOnlyList<OmsOrderEvent> Read(ClientOrderId aggregateId)
  194: public OmsOrderProjection? ReadProjection(ClientOrderId aggregateId)
  201: public IReadOnlyList<OrderEventOutboxEntry> ReadOutbox(long afterExclusiveSequence = 0)
```

## src/linux/Core/TradingTerminal.Core/Execution/OrderEvents.cs
```cs
    6: public sealed record OrderFill
    8: public OrderFill(
   27: public TradeId TradeId { get; }
   28: public ScaledQuantity Quantity { get; }
   29: public ScaledPrice Price { get; }
   30: public ScaledMoney Fee { get; }
   31: public DateTimeOffset OccurredAtUtc { get; }
   35: public sealed record OrderRiskObservation
   37: public OrderRiskObservation(
   51: public string CommandPayloadHashSha256 { get; }
   52: public RiskDecision Decision { get; }
   53: public RiskPolicyEvidence Evidence { get; }
   55: public static OrderRiskObservation Capture(
   65: public enum ReconciliationOutcome : byte
   75: public sealed record OrderReconciliationEvidence
   77: public OrderReconciliationEvidence(
   91: public ReconciliationCaseId CaseId { get; }
   92: public ReconciliationOutcome Outcome { get; }
   93: public string Evidence { get; }
   94: public DateTimeOffset ObservedAtUtc { get; }
   98: public sealed record OrderCommissionObservation
  100: public OrderCommissionObservation(
  114: public TradeId TradeId { get; }
  115: public ScaledMoney Fee { get; }
  116: public string Currency { get; }
  117: public DateTimeOffset ObservedAtUtc { get; }
  121: public sealed record OrderPositionObservation
  123: public OrderPositionObservation(
  135: public TradingAccountId TradingAccountId { get; }
  136: public ScaledQuantity NetQuantity { get; }
  137: public DateTimeOffset ObservedAtUtc { get; }
  144: public sealed record OrderEventDraft(
  165: public sealed record OmsOrderEvent
  244: public ClientOrderId AggregateId { get; }
  245: public long AggregateSequence { get; }
  246: public OrderEventKind Kind { get; }
  247: public OrderLifecycleState? StateBefore { get; }
  248: public OrderLifecycleState StateAfter { get; }
  249: public OrderEventSource Source { get; }
  250: public DeduplicationKey DeduplicationKey { get; }
  251: public DateTimeOffset OccurredAtUtc { get; }
  252: public DateTimeOffset RecordedAtUtc { get; }
  253: public CausationId CausationId { get; }
  254: public string PreviousEventHash { get; }
  255: public string EventHash { get; }
  256: public SubmitOrderCommand? SubmitCommand { get; }
  257: public OrderRiskObservation? RiskObservation { get; }
  258: public BrokerOrderId? BrokerOrderId { get; }
  259: public ExchangeOrderId? ExchangeOrderId { get; }
  260: public OrderFill? Fill { get; }
  261: public OrderTerms? ReplacementTerms { get; }
  262: public OrderReconciliationEvidence? Reconciliation { get; }
  263: public OrderCommissionObservation? Commission { get; }
  264: public OrderPositionObservation? Position { get; }
  265: public string? Reason { get; }
  266: public ExecutionDispatchReceipt? DispatchReceipt { get; }
```

## src/linux/Core/TradingTerminal.Core/Execution/OrderLifecycle.cs
```cs
    7: public enum OrderLifecycleState : byte
   29: public readonly record struct OrderLifecycleTransition(
   34: public enum OrderEventKind : byte
   68: public enum OrderEventSource : byte
   81: public static class OrderLifecycle
  155: public static IReadOnlyList<OrderLifecycleTransition> LegalTransitions => ReadOnlyTransitions;
  157: public static bool IsTerminal(OrderLifecycleState state) => state is
  164: public static bool CanTransition(OrderLifecycleState from, OrderLifecycleState to) =>
  172: public static bool CanApplyEvent(
  314: public static bool IsEventSourceAllowed(OrderEventKind kind, OrderEventSource source)
  355: public static bool BlocksRetry(OrderLifecycleState state) =>
  359: public static OrderLifecycleState Transition(OrderLifecycleState from, OrderLifecycleState to)
```

## src/linux/Core/TradingTerminal.Core/Execution/OrderManagementService.cs
```cs
    6: public readonly record struct OrderCommandContext(
   10: public bool IsValid => !CausationId.IsEmpty && !DeduplicationKey.IsEmpty;
   13: public enum OmsCommandFault : byte
   32: public readonly record struct OmsCommandResult(
   38: public bool IsSuccess => Fault == OmsCommandFault.None && Projection is not null;
   39: public bool CanRetrySameClientOrderId =>
   48: public sealed class OrderManagementService
   56: public OrderManagementService(
   70: public bool CanAdmitNewOrders =>
   74: public OmsCommandResult Submit(
  153: public OmsCommandResult Cancel(
  197: public OmsCommandResult Replace(
  268: public OmsOrderProjection? Query(ClientOrderId clientOrderId) =>
  275: public IReadOnlyList<OmsCommandResult> ProcessVenueEvents()
  287: public OmsCommandResult ApplyVenueEvent(PaperVenueEvent venueEvent)
```

## src/linux/Core/TradingTerminal.Core/Execution/OrderProjection.cs
```cs
    5: public enum OrderEventChainFault : byte
   20: public readonly record struct OrderEventChainVerification(
   24: public bool IsValid => Fault == OrderEventChainFault.None;
   28: public static class OrderEventChainVerifier
   30: public static OrderEventChainVerification Verify(IReadOnlyList<OmsOrderEvent>? events)
   99: public enum OrderStopActivationState : byte
  175: public enum OrderProjectionFault : byte
  193: public readonly record struct OrderProjectionResult(
  199: public bool IsSuccess => Fault == OrderProjectionFault.None && Projection is not null;
  203: public sealed record OmsOrderProjection(
  222: public bool BlocksRetry => OrderLifecycle.BlocksRetry(State);
  224: public static OrderProjectionResult Rebuild(IReadOnlyList<OmsOrderEvent>? events) =>
  229: public static class OmsOrderProjector
  231: public static OrderProjectionResult Rebuild(IReadOnlyList<OmsOrderEvent>? events)
```

## src/linux/Core/TradingTerminal.Core/Execution/PaperExecutionDispatch.cs
```cs
    3: public enum ExecutionDispatchStatus : byte
   11: public sealed record ExecutionDispatchReceipt
   13: public ExecutionDispatchReceipt(
   28: public DispatchAttemptId DispatchAttemptId { get; }
   29: public DateTimeOffset DispatchedAtUtc { get; }
   30: public BrokerOrderId? BrokerOrderId { get; }
   31: public ExchangeOrderId? ExchangeOrderId { get; }
   34: public sealed record ExecutionDispatchResult
   49: public ExecutionDispatchStatus Status { get; }
   50: public ExecutionDispatchReceipt? Receipt { get; }
   51: public string? Reason { get; }
   53: public static ExecutionDispatchResult Dispatched(ExecutionDispatchReceipt receipt) =>
   56: public static ExecutionDispatchResult RejectedBeforeDispatch(string reason) =>
   59: public static ExecutionDispatchResult Unknown(string reason) =>
   63: public enum PaperVenueEventKind : byte
   76: public sealed record PaperVenueEvent(
   92: public interface IPaperExecutionDispatcher
   94:     ExecutionDispatchResult Submit(SubmitOrderCommand command, OmsOrderProjection projection);
   95:     ExecutionDispatchResult Cancel(CancelOrderCommand command, OmsOrderProjection projection);
   96:     ExecutionDispatchResult Replace(ReplaceOrderCommand command, OmsOrderProjection projection);
  102:     IReadOnlyList<PaperVenueEvent> DrainEvents();
```

## src/linux/Core/TradingTerminal.Core/Execution/Reconciliation.cs
```cs
    7: public enum ReconciliationTrigger : byte
   16: public enum ReconciliationSubjectKind : byte
   25: public enum ReconciliationCaseKind : byte
   41: public enum ReconciliationCaseStatus : byte
   49: public sealed record ReconciliationCase
   51: public ReconciliationCase(
   82: public ReconciliationCaseId CaseId { get; }
   83: public ExecutionResource Resource { get; }
   84: public ReconciliationSubjectKind SubjectKind { get; }
   85: public string SubjectKey { get; }
   86: public ClientOrderId? ClientOrderId { get; }
   87: public ReconciliationCaseKind Kind { get; }
   88: public ReconciliationCaseStatus Status { get; }
   89: public string LocalEvidence { get; }
   90: public string BrokerEvidence { get; }
   91: public DateTimeOffset OpenedAtUtc { get; }
   92: public DateTimeOffset? ResolvedAtUtc { get; }
   93: public string? ResolvedBy { get; }
   94: public string? ResolutionEvidence { get; }
   95: public bool IsMaterial => Kind != ReconciliationCaseKind.Matched;
   97: public bool IsValid =>
  115: public ReconciliationCase Resolve(DateTimeOffset resolvedAtUtc, string resolvedBy, string evidence) =>
  132: public interface IReconciliationCaseStore
  134:     bool TryAppend(ReconciliationCase reconciliationCase);
  135:     IReadOnlyList<ReconciliationCase> Read(ExecutionResource resource);
  136:     IReadOnlyList<ReconciliationCase> Read(ReconciliationCaseId caseId);
  139: public sealed class InMemoryReconciliationCaseStore : IReconciliationCaseStore
  144: public bool TryAppend(ReconciliationCase reconciliationCase)
  169: public IReadOnlyList<ReconciliationCase> Read(ExecutionResource resource)
  181: public IReadOnlyList<ReconciliationCase> Read(ReconciliationCaseId caseId)
  201: public sealed record ReconciliationOrderSnapshot(
  210: public ClientOrderId ClientOrderId => Instruction.Identity.ClientOrderId;
  211: public bool RequiresBrokerPresence => WasDispatched && State is not OrderLifecycleState.Reconciled;
  212: public bool IsValid =>
  224: public sealed record ReconciliationFillSnapshot(
  236: public bool IsValid =>
  246: public sealed record ReconciliationPositionSnapshot(
  251: public bool IsValid => !InstrumentId.IsNone && Quantity.IsValid && ObservedAtUtc.Offset == TimeSpan.Zero;
  254: public sealed record ReconciliationCashSnapshot(
  260: public bool IsValid =>
  267: public sealed record ExecutionReconciliationSnapshot(
  275: public bool TryValidate(out string? reason)
  305: public interface IExecutionReconciliationSnapshotProvider
  307:     ExecutionReconciliationSnapshot CaptureReconciliationSnapshot(
  308:     ExecutionResource resource,
  309:     DateTimeOffset capturedAtUtc);
  313: public static class ExecutionReconciliationSnapshotBuilder
  315: public static ExecutionReconciliationSnapshot FromLedger(
  417: public enum ReconciliationCycleFault : byte
  425: public sealed record ReconciliationCycleResult(
  434: public bool IsSuccess => Fault == ReconciliationCycleFault.None;
  435: public bool IsAdmissionBlocked => !IsSuccess || UnresolvedMaterialCaseCount > 0;
  438: public interface IExecutionReconciliationAdmissionGate
  440:     bool CanAdmitNewExposure(ExecutionResource resource);
  444: public sealed class ReconciliationEngine : IExecutionReconciliationAdmissionGate
  451: public ReconciliationEngine(IReconciliationCaseStore store) =>
  454: public bool CanAdmitNewExposure(ExecutionResource resource)
  461: public ReconciliationCycleResult RunCycle(
  514: public bool ResolveCase(
  532: public ReconciliationCycleResult ReportAcquisitionFailure(
  694: public static ObservationKey Of(ReconciliationSubjectKind kind, string key, ReconciliationCaseKind caseKind) => new(kind, key, caseKind);
  705: public ObservationKey Key => ObservationKey.Of(SubjectKind, SubjectKey, Kind);
  707: public ReconciliationCase ToCase(ExecutionResource resource, DateTimeOffset openedAtUtc)
  715: public static Observation Order(ClientOrderId id, ReconciliationCaseKind kind, object? local, object? broker) =>
  717: public static Observation Fill(ReconciliationFillSnapshot fill, ReconciliationCaseKind kind, object? local, object? broker) =>
  719: public static Observation Position(InstrumentId id, object? local, object? broker) =>
  722: public static Observation Cash(string currency, object? local, object? broker) =>
  732: public sealed class PaperExecutionReconciliationCoordinator
  740: public PaperExecutionReconciliationCoordinator(
  755: public ReconciliationCycleResult RunStartup()
  769: public ReconciliationCycleResult RunReconnect() => Run(ReconciliationTrigger.Reconnect);
  771: public ReconciliationCycleResult Run(ReconciliationTrigger trigger)
```

## src/linux/Core/TradingTerminal.Core/Execution/RiskPolicy.cs
```cs
    6: public enum RiskControlMode
   13: public enum RiskDecisionCode
   33: public sealed record RiskDecision(
   40: public static RiskDecision Allow(
   47: public static RiskDecision Deny(
   55: public sealed record RiskLimits
   57: public RiskLimits(
   85: public ScaledQuantity MaximumOrderQuantity { get; }
   86: public ScaledQuantity MaximumAbsolutePosition { get; }
   87: public ScaledMoney MaximumGrossNotional { get; }
   88: public ScaledMoney MinimumBuyingPower { get; }
   89: public ScaledMoney MaximumDailyLoss { get; }
   90: public ScaledMoney MaximumDrawdown { get; }
   91: public int MaximumExposureCommandsPerWindow { get; }
   92: public TimeSpan RateLimitWindow { get; }
   95: public sealed record RiskEvaluationContext
   97: public RiskEvaluationContext(
  143: public RiskEvaluationContext(
  214: public RiskLimits Limits { get; }
  215: public RiskControlMode ControlMode { get; }
  216: public bool KillSwitchActive { get; }
  217: public ScaledQuantity CurrentPositionQuantity { get; }
  218: public ScaledQuantity CurrentBuyReservedQuantity { get; }
  219: public ScaledQuantity CurrentSellReservedQuantity { get; }
  220: public ScaledQuantity CurrentNetReservedQuantity =>
  224: public ScaledMoney CurrentGrossReservedNotional { get; }
  225: public ScaledQuantity ExistingOrderSignedReservation { get; }
  226: public ScaledMoney ExistingOrderGrossReservation { get; }
  227: public ScaledQuantity ExistingOrderFilledQuantity { get; }
  228: public ScaledMoney AvailableBuyingPower { get; }
  229: public ScaledMoney DailyNetRealizedPnl { get; }
  230: public ScaledMoney CurrentEquity { get; }
  231: public ScaledMoney PeakEquity { get; }
  232: public ScaledPrice MarketPrice { get; }
  233: public int ExposureCommandsInWindow { get; }
  234: public DateTimeOffset EvaluatedAtUtc { get; }
  235: public DateTimeOffset TradingDayStartedAtUtc { get; }
  236: public ScaledRatio ContractMultiplier { get; }
  237: public string AccountCurrency { get; }
  242: public bool HasUnrepresentableMarketEconomics { get; }
  268: public sealed record RiskPolicyEvidence
  270: public RiskPolicyEvidence(string policyVersion, string limitsHashSha256, RiskEvaluationContext context)
  280: public string PolicyVersion { get; }
  281: public string LimitsHashSha256 { get; }
  282: public RiskEvaluationContext Context { get; }
  284: public static RiskPolicyEvidence Capture(RiskEvaluationContext context) =>
  289: public static class RiskPolicy
  291: public const string PolicyVersion = "daxalgo-risk-policy-v3-scaled";
  296: public static RiskDecision Evaluate(ExecutionCommand command, RiskEvaluationContext context)
```

## src/linux/Core/TradingTerminal.Core/Execution/ScaledValues.cs
```cs
    4: public readonly record struct ScaledQuantity(long Coefficient, byte Scale = 0)
    6: public static ScaledQuantity Zero => new(0, 0);
    7: public static ScaledQuantity FromWhole(long units) => new(units, 0);
    8: public bool IsValid => Scale <= ScaledValueMath.MaximumScale;
    9: public bool TryGetWholeUnits(out long units) =>
   14: public readonly record struct ScaledPrice(long Coefficient, byte Scale)
   16: public bool IsValid => Scale <= ScaledValueMath.MaximumScale;
   20: public readonly record struct ScaledMoney(long Coefficient, byte Scale)
   22: public static ScaledMoney Zero => new(0, 0);
   23: public bool IsValid => Scale <= ScaledValueMath.MaximumScale;
   27: public readonly record struct ScaledRatio(long Coefficient, byte Scale)
   29: public bool IsValid => Scale <= ScaledValueMath.MaximumScale;
  396: public static class ExecutionNumericBoundary
  398: public static ScaledQuantity QuantityFromDecimal(decimal value)
  404: public static ScaledPrice PriceFromDecimal(decimal value)
  410: public static ScaledMoney MoneyFromDecimal(decimal value)
  416: public static ScaledRatio RatioFromDecimal(decimal value)
  422: public static bool TryQuantityFromDecimal(decimal value, out ScaledQuantity quantity)
  431: public static bool TryPriceFromDecimal(decimal value, out ScaledPrice price)
  440: public static bool TryMoneyFromDecimal(decimal value, out ScaledMoney money)
  449: public static bool TryRatioFromDecimal(decimal value, out ScaledRatio ratio)
  458: public static ScaledPrice PriceFromDouble(double value, byte scale)
  465: public static decimal ToDecimal(ScaledQuantity value) =>
  468: public static decimal ToDecimal(ScaledPrice value) =>
  471: public static decimal ToDecimal(ScaledMoney value) =>
  474: public static decimal ToDecimal(ScaledRatio value) =>
```
