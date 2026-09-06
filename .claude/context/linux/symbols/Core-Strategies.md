# TradingTerminal.Core / Strategies — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Core/TradingTerminal.Core/Strategies/Authoring/AiModelChoice.cs
```cs
   14: public sealed record AiModelChoice(string ProviderId, string ProviderLabel, string ModelId)
   18: public bool IsAvailable { get; init; } = true;
   22: public string Display => string.IsNullOrEmpty(ModelId) ? ProviderLabel : $"{ModelId} · {ProviderLabel}";
```

## src/linux/Core/TradingTerminal.Core/Strategies/Authoring/IAiKeyResolver.cs
```cs
    9: public interface IAiKeyResolver
   13:     string? Resolve(string providerId);
   16:     public static IAiKeyResolver Null { get; } = new NullAiKeyResolver();
   21: public string? Resolve(string providerId) => null;
   29: public interface IAiKeyStore
   32:     IReadOnlyCollection<string> ConfiguredProviders { get; }
   34:     bool HasKey(string providerId);
   35:     void Set(string providerId, string apiKey);
   36:     void Remove(string providerId);
```

## src/linux/Core/TradingTerminal.Core/Strategies/Authoring/IAuthoredStrategyViewComposer.cs
```cs
   18: public interface IAuthoredStrategyViewComposer
   23:     object ComposeView(ITradingStrategy descriptor);
```

## src/linux/Core/TradingTerminal.Core/Strategies/Authoring/IStrategyCodegenClient.cs
```cs
    4: public enum CodegenRole
   16: public enum CodegenEffort
   32: public static class CodegenEfforts
   35: public static string? Wire(this CodegenEffort effort) => effort switch
   47: public static CodegenEffort Parse(string? value) => value?.Trim().ToLowerInvariant() switch
   59: public sealed record CodegenImageInput(
   69: public sealed record CodegenMessage(
   85: public sealed record CodegenUsage(int InputTokens, int OutputTokens, int CachedInputTokens = 0)
   87: public static CodegenUsage None { get; } = new(0, 0);
   89: public int TotalTokens => InputTokens + OutputTokens;
   92: public bool IsReported => InputTokens > 0 || OutputTokens > 0;
   94: public CodegenUsage Add(CodegenUsage? other) => other is null
  102: public enum StrategyCodegenOutputContract
  115: public sealed record StrategyCodegenRequest(
  123: public StrategyCodegenOutputContract OutputContract { get; init; } =
  139: public sealed record StrategyCodegenResponse(
  148: public IReadOnlyList<StrategyFile> FileList => Files ?? (string.IsNullOrWhiteSpace(Code)
  153: public bool HasFiles => FileList.Count > 0;
  155: public static StrategyCodegenResponse Ok(string code, string rawText) => new(true, code, rawText, null);
  157: public static StrategyCodegenResponse Ok(IReadOnlyList<StrategyFile> files, string rawText, CodegenUsage? usage = null) =>
  161: public static StrategyCodegenResponse Reply(string rawText, CodegenUsage? usage = null) =>
  164: public static StrategyCodegenResponse Fail(string error) => new(false, null, null, error);
  173: public abstract record CodegenEvent
  178: public sealed record TextDelta(string Text) : CodegenEvent;
  182: public sealed record UsageUpdate(CodegenUsage Usage) : CodegenEvent;
  185: public sealed record Completed(StrategyCodegenResponse Response) : CodegenEvent;
  200: public interface IStrategyCodegenClient
  204:     string ProviderId { get; }
  207:     string DisplayName { get; }
  211:     bool IsAvailable { get; }
  215:     string Model => string.Empty;
  219:     CodegenEffort Effort => CodegenEffort.Default;
  223:     IReadOnlyList<string> KnownModels => [];
  228:     Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default) =>
  229:     Task.FromResult<IReadOnlyList<string>>([]);
  235:     Task<StrategyCodegenResponse> GenerateAsync(StrategyCodegenRequest request, CancellationToken ct = default);
  248:     async IAsyncEnumerable<CodegenEvent> StreamAsync(
  249:     StrategyCodegenRequest request,
  252:     var response = await GenerateAsync(request, ct).ConfigureAwait(false);
  253:     if (response.Usage is { IsReported: true } usage) yield return new CodegenEvent.UsageUpdate(usage);
  254:     yield return new CodegenEvent.Completed(response);
```

## src/linux/Core/TradingTerminal.Core/Strategies/Authoring/IStrategyCompiler.cs
```cs
   18: public interface IStrategyCompiler
   20:     StrategyCompileResult Compile(StrategyScript script);
```

## src/linux/Core/TradingTerminal.Core/Strategies/Authoring/StrategyBuildEffort.cs
```cs
   10: public enum StrategyBuildEffort
   27: public static class StrategyBuildEfforts
   31: public static string Wire(this StrategyBuildEffort effort) => effort switch
   41: public static StrategyBuildEffort Parse(string? value) => value?.Trim().ToLowerInvariant() switch
   60: public sealed record StrategyBuildProfile(int MaxSkills, int MaxFixAttempts, bool SelfReview, bool BacktestSmoke)
   63: public static StrategyBuildProfile For(StrategyBuildEffort effort) => effort switch
```

## src/linux/Core/TradingTerminal.Core/Strategies/Authoring/StrategyCompileResult.cs
```cs
   23: public sealed record AuthoredStrategyAssembly(
   33: public bool HasLiveWindow => DescriptorType is not null && ViewModelType is not null && ViewType is not null;
   38: public bool CanComposeLiveWindow => DescriptorType is not null && ViewModelType is not null;
   43: public IReadOnlyList<string> MissingForCatalog =>
   57: public sealed record StrategyCompileResult(
   63: public IEnumerable<StrategyDiagnostic> Errors =>
   66: public static StrategyCompileResult Failed(IReadOnlyList<StrategyDiagnostic> diagnostics) =>
   69: public static StrategyCompileResult Succeeded(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Authoring/StrategyDiagnostic.cs
```cs
    4: public enum StrategyDiagnosticSeverity
   21: public sealed record StrategyDiagnostic(
   30: public string Location => string.IsNullOrEmpty(File)
   34: public override string ToString() =>
```

## src/linux/Core/TradingTerminal.Core/Strategies/Authoring/StrategyScript.cs
```cs
   10: public sealed record StrategyFile(string Name, string Content)
   13: public const string DefaultName = "Strategy.cs";
   26: public sealed record StrategyScript(
   32: public StrategyScript(string id, string displayName, string sourceCode)
```

## src/linux/Core/TradingTerminal.Core/Strategies/Authoring/TradeIrSimulatedBacktestContractsV1.cs
```cs
    9: public static class TradeIrSimulatedBacktestContractV1
   11: public const string ExecutionMode = "in_process_synthetic_quote_l1_smoke";
   12: public const int MaximumEventCount = 100_000;
   13: public const string SchemaId = "canonical.quote-l1";
   14: public const int SchemaVersion = 1;
   15: public const string SchemaSemanticContract =
   19: public static IReadOnlyList<string> PayloadFields { get; } = Array.AsReadOnly(
   22: public static string SchemaHashSha256 { get; } = Convert.ToHexString(
   26: public static CanonicalEventSchemaV1 CreateEventSchema() => new(
   33: public static class TradeIrSimulatedBacktestIssueCodesV1
   35: public const string RequestRequired = "TRADEIR_SMOKE_REQUEST_REQUIRED";
   36: public const string SourceCandidateHashInvalid = "TRADEIR_SMOKE_SOURCE_CANDIDATE_HASH_INVALID";
   37: public const string ModuleHashInvalid = "TRADEIR_SMOKE_MODULE_HASH_INVALID";
   38: public const string ModuleHashMismatch = "TRADEIR_SMOKE_MODULE_HASH_MISMATCH";
   39: public const string ModuleInvalid = "TRADEIR_SMOKE_MODULE_INVALID";
   40: public const string EventCountInvalid = "TRADEIR_SMOKE_EVENT_COUNT_INVALID";
   41: public const string DataRequirementInvalid = "TRADEIR_SMOKE_DATA_REQUIREMENT_INVALID";
   42: public const string ArtifactIdentityUnavailable = "TRADEIR_SMOKE_ARTIFACT_IDENTITY_UNAVAILABLE";
   43: public const string Cancelled = "TRADEIR_SMOKE_CANCELLED";
   44: public const string RuntimeFailed = "TRADEIR_SMOKE_RUNTIME_FAILED";
   52: public enum TradeIrSimulatedBacktestStatusV1
   61: public sealed record TradeIrSimulatedBacktestIssueV1(
   71: public sealed record TradeIrSimulatedBacktestRequestV1(
   82: public sealed record TradeIrSimulatedBacktestEvidenceV1(
   99: public sealed record TradeIrSimulatedBacktestResultV1(
  105: public bool Succeeded =>
  116: public interface ITradeIrSimulatedBacktestRunnerV1
  118:     Task<TradeIrSimulatedBacktestResultV1> RunAsync(
  119:     TradeIrSimulatedBacktestRequestV1 request,
  120:     CancellationToken ct = default);
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/ExecutableStrategyDefinitionCanonicalJson.cs
```cs
   12: public static class ExecutableStrategyDefinitionCanonicalJson
   14: public const string AlgorithmVersion = "rfc8785-jcs/v1";
   18: public static string Serialize(object value)
   25: public static string Canonicalize(string json)
   39: public static T Deserialize<T>(string json)
   46: public static string Sha256(string json)
   52: public static string Hash(object value) => HashCanonical(Serialize(value));
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/OperatorGraphModuleCanonicalJsonV1.cs
```cs
    8: public static class OperatorGraphModuleCanonicalJsonV1
   10: public static string Serialize(OperatorGraphModuleV1 module)
   16: public static string Hash(OperatorGraphModuleV1 module)
   22: public static OperatorGraphModuleV1 Deserialize(string json)
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyCompilationAdmissionManifestV1.cs
```cs
    4: public sealed record StrategyCompilationDataPinV1(
   17: public sealed record StrategyCompilationAdmissionDocumentV1(
   26: public const int CurrentSchemaVersion = 1;
   27: public const string CurrentAdmissionRulesVersion = "trade-ir/compilation-admission/v1";
   35: public sealed class StrategyCompilationAdmissionManifestV1
   49: public string CanonicalDefinitionJson { get; }
   50: public string CanonicalTargetProfileJson { get; }
   51: public string CanonicalManifestJson { get; }
   52: public string ManifestHashSha256 { get; }
  101: public StrategyIntermediateRepresentationV1 ReadDefinitionForCompilation()
  111: public StrategyCompilationAdmissionDocumentV1 ReadDocument()
  204: public sealed record StrategyCompilationAdmissionOutcomeV1(
  208: public bool CanCompile => Assessment.CanCompile && Manifest is not null;
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyCompilationAdmissionV1.cs
```cs
    3: public sealed record StrategyCompilationAdmissionIssueV1(string Code, string Path, string Message);
    5: public sealed record StrategyCompilationAdmissionResultV1(
   12: public bool CanCompile =>
   24: public static class StrategyCompilationAdmissionV1
   30: public static StrategyCompilationAdmissionOutcomeV1 AssessAndFreeze(
   96: public static StrategyCompilationAdmissionResultV1 Assess(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyIntermediateRepresentationV1.cs
```cs
    8: public sealed record StrategyIntermediateRepresentationV1(
   19: public const int CurrentSchemaVersion = 1;
   26: public sealed record StrategyOperatorCatalogReferenceV1(
   31: public enum StrategyClockKindV1
   40: public sealed record StrategyIrNodeV1(
   51: public sealed record StrategyLiteralV1(
   58: public static StrategyLiteralV1 FromBoolean(bool value) =>
   61: public static StrategyLiteralV1 FromInteger(long value) =>
   64: public static StrategyLiteralV1 FromNumber(double value) =>
   67: public static StrategyLiteralV1 FromText(string value) =>
   71: public enum StrategyLiteralKindV1
   80: public enum StrategyIrOutputKindV1
   92: public sealed record StrategyIrOutputBindingV1(
  102: public sealed record StrategyAxisV1(
  112: public sealed record StrategyValueTypeV1(
  119: public enum StrategyValueAvailabilityV1
  126: public enum StrategyOperatorStateKindV1
  137: public enum StrategyOperatorPlacementV1
  145: public sealed record StrategyOperatorKeyV1(string OperatorId, int Version);
  147: public sealed record StrategyCapabilityRequirementV1(string CapabilityId, string Reason);
  149: public sealed record StrategyIrIssueV1(string Code, string Path, string Message);
  151: public sealed record StrategyIrNodeAnalysisV1(
  161: public sealed record StrategyIrValidationResultV1(
  166: public bool IsValid => Issues.Count == 0;
  168: public IReadOnlyList<StrategyCapabilityRequirementV1> RequiredCapabilities => Nodes
  178: public static class StrategyIrCanonicalJsonV1
  180: public const string AlgorithmVersion = ExecutableStrategyDefinitionCanonicalJson.AlgorithmVersion;
  182: public static string Serialize(StrategyIntermediateRepresentationV1 definition) =>
  185: public static StrategyIntermediateRepresentationV1 Deserialize(string json) =>
  188: public static string Hash(StrategyIntermediateRepresentationV1 definition) =>
  191: public static string Canonicalize(string json) =>
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyIrTargetAssessmentV1.cs
```cs
    7: public sealed record StrategyIrTargetProfileV1(
   22: public sealed record StrategyIrTargetAssessmentV1(
   30: public bool IsDeclaredCompatible => SemanticValidation.IsValid && Limitations.Count == 0;
   33: public static class StrategyIrTargetAssessorV1
   35: public static StrategyIrTargetAssessmentV1 Assess(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyIrValidatorV1.cs
```cs
    7: public static class StrategyIrValidatorV1
    9: public static StrategyIrValidationResultV1 Validate(
   59: public static StrategyIrValidationResultV1 ReadAndValidate(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyOperatorRegistryV1.cs
```cs
    3: public static class StrategyIrTypeIdsV1
    5: public const string Number = "core.number@1";
    6: public const string Boolean = "core.boolean@1";
    7: public const string PortfolioTarget = "portfolio.target@1";
    8: public const string ExitIntent = "risk.exit_intent@1";
    9: public const string QuoteIntent = "strategy.quote_intent@1";
   10: public const string OrderIntent = "strategy.order_intent@1";
   13: public sealed record StrategyOperatorBindingContextV1(
   18: public sealed record StrategyOperatorBindingResultV1(
   23: public bool IsValid => OutputType is not null && Issues.Count == 0;
   26: public delegate StrategyOperatorBindingResultV1 StrategyOperatorBinderV1(
   34: public sealed class StrategyOperatorDescriptorV1
   36: public StrategyOperatorDescriptorV1(
   91: public StrategyOperatorKeyV1 Key { get; }
   92: public IReadOnlyList<string> RequiredInputPorts { get; }
   93: public IReadOnlyList<string> OptionalInputPorts { get; }
   94: public StrategyOperatorStateKindV1 StateKind { get; }
   95: public StrategyOperatorPlacementV1 Placement { get; }
   96: public IReadOnlyList<StrategyCapabilityRequirementV1> Capabilities { get; }
   97: public string SemanticContractHashSha256 { get; }
   98: public string BinderIdentityHashSha256 { get; }
   99: public StrategyOperatorBinderV1 Binder { get; }
  134: public interface IStrategyOperatorRegistryV1
  136:     StrategyOperatorCatalogReferenceV1 Catalog { get; }
  137:     IReadOnlyList<StrategyOperatorKeyV1> Keys { get; }
  138:     bool TryResolve(string operatorId, int version, out StrategyOperatorDescriptorV1 descriptor);
  146: public sealed class StrategyOperatorRegistryV1 : IStrategyOperatorRegistryV1
  150: public StrategyOperatorRegistryV1(
  162: public StrategyOperatorRegistryV1(
  204: public StrategyOperatorCatalogReferenceV1 Catalog { get; }
  205: public IReadOnlyList<StrategyOperatorKeyV1> Keys { get; }
  207: public bool TryResolve(string operatorId, int version, out StrategyOperatorDescriptorV1 descriptor) =>
  210: public static StrategyOperatorRegistryV1 CreateDefault() => new(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/StrategyValueTypeRulesV1.cs
```cs
   10: public static void Validate(
   64: public static bool IsCompatible(StrategyIrOutputKindV1 kind, StrategyValueTypeV1 valueType) => kind switch
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/TradeIrDataAdmissionValidator.cs
```cs
    6: public static class DataAdmissionIssueCodes
    8: public const string DataKindUnsupported = "DATA_KIND_UNSUPPORTED";
    9: public const string InstrumentUnbound = "INSTRUMENT_UNBOUND";
   10: public const string SchemaVersionUnsupported = "SCHEMA_VERSION_UNSUPPORTED";
   11: public const string TemporalSemanticsMismatch = "TEMPORAL_SEMANTICS_MISMATCH";
   12: public const string SnapshotHashMissing = "SNAPSHOT_HASH_MISSING";
   13: public const string CapabilityStale = "CAPABILITY_STALE";
   20: public sealed record DataAdmissionIssue(
   28: public sealed record DataAdmissionResult(
   34: public bool IsAdmitted => Issues.Count == 0;
   42: public static class DataAdmissionValidator
   47: public static DataAdmissionResult Assess(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/TradeIrDataContracts.cs
```cs
    6: public enum TradeIrDataKindV1
   21: public enum TradeIrEventTimeBasisV1
   32: public enum TradeIrTimestampPrecisionV1
   42: public enum TradeIrEventOrderingV1
   50: public enum TradeIrNormalizationPolicyV1
   60: public enum TradeIrMissingDataPolicyV1
   69: public enum TradeIrRevisionPolicyV1
   79: public enum CanonicalEventQualityFlagsV1
   94: public sealed record DataTemporalSemanticsV1(
  107: public sealed record SourceIndependentInstrumentRef(
  118: public sealed record SourceIndependentInstrumentSelectorV1(
  126: public sealed record CanonicalEventSchemaV1(
  139: public sealed record CanonicalEventEnvelopeV1(
  153: public sealed record DataRequirementV1(
  168: public sealed record DataSourceCapabilityV1(
  188: public sealed record DataBindingManifestV1(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/TradeIrModuleValidatorV1.cs
```cs
    3: public sealed record StrategyModuleIssueV1(string Code, string Path, string Message);
    5: public sealed record StrategyModuleValidationV1(IReadOnlyList<StrategyModuleIssueV1> Issues)
    7: public bool IsValid => Issues.Count == 0;
   15: public static class TradeIrModuleValidatorV1
   17: public static StrategyModuleValidationV1 Validate(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Definition/TradeIrModules.cs
```cs
    5: public enum TradeIrDigestAlgorithmV1
   11: public sealed record TradeIrContentAddressV1(
   16: public sealed record StrategyModuleInputV1(
   21: public sealed record StrategyModuleOutputV1(
   26: public enum StrategyModuleDeterminismV1
   37: public sealed record StrategyModuleRuntimeContractV1(
   54: public abstract record TradeIrModuleV1
   56: public const string CurrentSchemaVersion = "trade-ir/module/v1";
   64: public string SchemaVersion { get; init; }
   65: public string ModuleId { get; init; }
   72: public sealed record OperatorGraphModuleV1 : TradeIrModuleV1
   74: public OperatorGraphModuleV1(
   83: public StrategyIntermediateRepresentationV1 Definition { get; init; }
   90: public sealed record CSharpModuleV1 : TradeIrModuleV1
   92: public CSharpModuleV1(
  111: public IReadOnlyList<StrategyModuleInputV1> Inputs { get; init; }
  112: public IReadOnlyList<StrategyModuleOutputV1> Outputs { get; init; }
  113: public TradeIrContentAddressV1 SourceAddress { get; init; }
  114: public string LanguageVersion { get; init; }
  115: public string EntryPoint { get; init; }
  116: public StrategyModuleRuntimeContractV1 Runtime { get; init; }
  123: public sealed record ModelArtifactModuleV1 : TradeIrModuleV1
  125: public ModelArtifactModuleV1(
  148: public IReadOnlyList<StrategyModuleInputV1> Inputs { get; init; }
  149: public IReadOnlyList<StrategyModuleOutputV1> Outputs { get; init; }
  150: public TradeIrContentAddressV1 ArtifactAddress { get; init; }
  151: public string Format { get; init; }
  152: public string EntryPoint { get; init; }
  153: public string FeatureSchemaHashSha256 { get; init; }
  154: public string OutputSchemaHashSha256 { get; init; }
  155: public StrategyModuleRuntimeContractV1 Runtime { get; init; }
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/AuthoredUnitCompilationV1.cs
```cs
    8: public sealed record CompiledAuthoredUnitV1(
   17: public sealed record AuthoredUnitCompilationResultV1(
   22: public IEnumerable<StrategyDiagnostic> Errors =>
   25: public static AuthoredUnitCompilationResultV1 Failed(IReadOnlyList<StrategyDiagnostic> diagnostics) =>
   28: public static AuthoredUnitCompilationResultV1 Succeeded(
   38: public interface IAuthoredUnitCompilerV1
   40:     AuthoredUnitCompilationResultV1 Compile(
   41:     AuthoredUnitSpecificationV1 specification,
   42:     StrategyScript script);
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/AuthoredUnitSpecificationV1.cs
```cs
   10: public enum AuthoredUnitKindV1
   17: public enum AuthoredUnitSourceKindV1
   28: public enum ChartReferenceSimilarityV1
   39: public enum AuthoredChartPaneRoleV1
   49: public enum AuthoredChartLayerKindV1
   65: public enum AuthoredUnitExecutionIntentV1
   76: public sealed record AuthoredChartReferenceV1(
   88: public sealed record AuthoredChartReferenceResolutionV1(
   99: public sealed record AuthoredChartReferenceInspectionV1(
  115: public sealed record AuthoredInstrumentRequestV1(
  123: public sealed record AuthoredUnitTimeframeV1(string UserText, TimeSpan? BarSize);
  129: public sealed record AuthoredUnitParameterV1(
  140: public sealed record AuthoredChartPaneV1(
  150: public sealed record AuthoredChartLayerV1(
  158: public sealed record AuthoredChartCompositionV1(
  166: public sealed record AuthoredUnitSpecificationV1(
  184: public const string CurrentSchemaVersion = "authored-unit-spec/v1";
  187: public sealed record AuthoredUnitSpecificationIssueV1(string Code, string Path, string Message);
  193: public sealed record AuthoredUnitInstrumentCandidateV1(
  206: public sealed record AuthoredUnitSpecificationGenerationRequestV1(
  216: public sealed record AuthoredUnitSpecificationGenerationIssueV1(
  221: public sealed record AuthoredUnitSpecificationGenerationResultV1(
  227: public bool Success => Specification is not null && Issues.Count == 0;
  234: public interface IAuthoredUnitSpecificationGeneratorV1
  236:     Task<AuthoredUnitSpecificationGenerationResultV1> GenerateAsync(
  237:     IStrategyCodegenClient provider,
  238:     AuthoredUnitSpecificationGenerationRequestV1 request,
  239:     CancellationToken cancellationToken = default);
  243: public sealed record AuthoredUnitIntentClassificationV1(
  249: public sealed record AuthoredUnitIntentClassificationResultV1(
  254: public bool Success => Classification is not null &&
  259: public interface IAuthoredUnitIntentClassifierV1
  261:     Task<AuthoredUnitIntentClassificationResultV1> ClassifyAsync(
  262:     IStrategyCodegenClient provider,
  263:     string rawRequest,
  264:     IReadOnlyList<AuthoredChartReferenceV1>? chartReferences = null,
  265:     CancellationToken cancellationToken = default);
  272: public sealed record AuthoredUnitSourceGenerationRequestV1(
  275: public sealed record AuthoredUnitSourceGenerationIssueV1(
  285: public sealed record AuthoredUnitSourceGenerationResultV1(
  292: public bool Success => Script is not null &&
  301: public interface IAuthoredUnitSourceGeneratorV1
  303:     Task<AuthoredUnitSourceGenerationResultV1> GenerateAsync(
  304:     IStrategyCodegenClient provider,
  305:     AuthoredUnitSourceGenerationRequestV1 request,
  306:     CancellationToken cancellationToken = default);
  310: public static class AuthoredUnitSpecificationValidatorV1
  318: public static IReadOnlyList<AuthoredUnitSpecificationIssueV1> Validate(
  322: public static IReadOnlyList<AuthoredUnitSpecificationIssueV1> ValidateForLaunch(
  762: public static class AuthoredUnitSpecificationCanonicalJsonV1
  764: public static string Serialize(AuthoredUnitSpecificationV1 specification) =>
  767: public static AuthoredUnitSpecificationV1 Deserialize(string json) =>
  770: public static string Hash(AuthoredUnitSpecificationV1 specification) =>
  773: public static string Canonicalize(string json) =>
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/ChartPatternSimilarityV1.cs
```cs
   11: public sealed record ChartPatternFingerprintV1(
   15: public enum ChartPatternCandidateScopeV1
   23: public sealed record ChartPatternSearchRequestV1(
   41: public sealed record ChartPatternMatchV1(
   57: public sealed record ChartPatternSearchResultV1(
   72: public sealed record ChartPatternSelectionV1(
   77: public interface IChartPatternSearchV1
   79:     Task<ChartPatternSearchResultV1> SearchAsync(
   80:     ChartPatternSearchRequestV1 request,
   81:     CancellationToken cancellationToken = default);
   85: public static class ChartPatternSimilarityCalculatorV1
   87: public const int MinimumPathPoints = 8;
   88: public const int MaximumPathPoints = 64;
   90: public static void ValidateFingerprint(ChartPatternFingerprintV1 fingerprint)
  112: public static ChartPatternScoreV1 Score(
  227: public sealed record ChartPatternScoreV1(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/ConfirmedStrategyIntentV1.cs
```cs
    9: public enum StrategyIntentKindV1
   24: public enum StrategyIntentFamilyV1
   38: public enum StrategySemanticStageV1
   53: public enum StrategySemanticDispositionV1
   61: public sealed record StrategyIntentModelV1(
   69: public enum StrategyIntentExtensionRoleV1
   80: public interface IStrategyIntentExtensionRegistryV1
   82:     bool Owns(StrategyIntentExtensionRoleV1 role, string extensionId);
   89: public static class StrategyIntentValueTypesV1
   91: public const string SemanticClause = "core.semantic_clause@1";
   92: public const string Percentage = "core.percentage@1";
   93: public const string Duration = "core.duration@1";
   94: public const string Timeframe = "core.timeframe@1";
  104: public static bool IsBuiltIn(string? typeId) =>
  109: public sealed record StrategyRequirementProvenanceV1(
  115: public sealed record StrategySemanticRequirementV1(
  125: public sealed record ResearchEvidenceRequirementV1(
  133: public sealed record ResearchFalsifierV1(
  139: public sealed record ResearchUnresolvedItemV1(
  146: public sealed record ResearchResolvedItemV1(
  158: public sealed record ResearchCaseV1(
  171: public const string CurrentSchemaVersion = "research-case/v1";
  174: public sealed record StrategyClassificationBindingV1(
  179: public sealed record StrategyIntentDraftV1(
  191: public const string CurrentSchemaVersion = "strategy-intent-draft/v1";
  198: public sealed record ConfirmedStrategyIntentV1(
  211: public const string CurrentSchemaVersion = "confirmed-strategy-intent/v1";
  214: public sealed record StrategyIntentQuestionV1(
  221: public sealed record StrategyIntentIssueV1(
  226: public sealed record StrategyIntentConfirmationResultV1(
  231: public bool Success => Intent is not null && Questions.Count == 0 && Issues.Count == 0;
  234: public static class ResearchCaseCanonicalJsonV1
  236: public static string Serialize(ResearchCaseV1 value) =>
  239: public static string Hash(ResearchCaseV1 value) =>
  242: public static ResearchCaseV1 Deserialize(string json) =>
  245: public static string Canonicalize(string json) =>
  249: public static class StrategySpecCanonicalJsonV1
  251: public static string Serialize(StrategySpec value) =>
  254: public static string Hash(StrategySpec value) =>
  257: public static StrategySpec Deserialize(string json) =>
  260: public static string Canonicalize(string json) =>
  264: public static class StrategyIntentCanonicalJsonV1
  266: public static string Serialize(StrategyIntentDraftV1 value) =>
  269: public static string Serialize(ConfirmedStrategyIntentV1 value) =>
  272: public static string Hash(StrategyIntentDraftV1 value) =>
  275: public static string Hash(ConfirmedStrategyIntentV1 value) =>
  278: public static StrategyIntentDraftV1 DeserializeDraft(string json) =>
  281: public static ConfirmedStrategyIntentV1 DeserializeConfirmed(string json) =>
  284: public static string Canonicalize(string json) =>
  292: public static class StrategyIntentConfirmationV1
  294: public static StrategyIntentConfirmationResultV1 Confirm(
  363: public static IReadOnlyList<StrategyIntentIssueV1> ValidateConfirmed(
  976: public static class StrategyIntentCompletenessV1
  978: public const string CatalogVersion = "strategy-intent-requirements/v1";
 1013: public static bool IsKnownRequirementId(string? requirementId) =>
 1016: public static IReadOnlyList<string> ActiveRequirementIds(
 1033: public static StrategyIntentFamilyV1 ClassifyFamily(
 1064: public static IReadOnlyList<StrategyIntentQuestionV1> Questions(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/HistoricalValidationEvidenceV1.cs
```cs
    9: public sealed record HistoricalValidationContextV1(
   19: public sealed record HistoricalValidationEvidenceV1(
   33: public const string CurrentSchemaVersion = "historical-validation-evidence/v1";
   36: public static class HistoricalValidationEvidenceCanonicalJsonV1
   38: public static string Serialize(HistoricalValidationEvidenceV1 value) =>
   41: public static HistoricalValidationEvidenceV1 Deserialize(string json)
   48: public static string Hash(HistoricalValidationEvidenceV1 value) =>
   52: public static class HistoricalValidationEvidenceValidatorV1
   54: public static void RequireValid(HistoricalValidationEvidenceV1 value)
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/ResearchDatasetV1.cs
```cs
    7: public enum ResearchEventLabelKindV1
   15: public enum ResearchEventLabelSourceV1
   26: public sealed record ResearchChartSelectionV1(
   36: public sealed record ResearchEventSampleV1(
   45: public const string CurrentSchemaVersion = "research-event-sample/v1";
   49: public sealed record ResearchLeakagePolicyV1(
   55: public static ResearchLeakagePolicyV1 SafeDefault { get; } = new(
   62: public sealed record ResearchDatasetDefinitionV1(
   70: public const string CurrentSchemaVersion = "research-dataset/v1";
   73: public sealed record ResearchDatasetIssueV1(string Code, string Path, string Message);
   75: public static class ResearchDatasetCanonicalJsonV1
   77: public static string Serialize(ResearchDatasetDefinitionV1 value) =>
   80: public static ResearchDatasetDefinitionV1 Deserialize(string json)
   87: public static string Hash(ResearchDatasetDefinitionV1 value) =>
   91: public static class ResearchDatasetValidatorV1
   93: public static IReadOnlyList<ResearchDatasetIssueV1> Validate(
  141: public static void RequireStructurallyValid(ResearchDatasetDefinitionV1 dataset)
  148: public static void RequireValidSelection(ResearchChartSelectionV1 selection)
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/ResearchExperimentV1.cs
```cs
    5: public sealed record ResearchFeatureDefinitionV1(
   10: public sealed record ResearchFeatureTransformV1(
   15: public sealed record ResearchFeatureWeightV1(
   20: public sealed record ResearchFeatureVectorV1(
   32: public sealed record ResearchChronologicalSplitV1(
   44: public sealed record ResearchExperimentEvidenceV1(
   58: public const string CurrentSchemaVersion = "research-experiment/v1";
   59: public const string NonPromotionalStatement =
   63: public interface IResearchExperimentRunnerV1
   65:     Task<ResearchExperimentEvidenceV1> RunAsync(
   66:     ResearchDatasetDefinitionV1 dataset,
   67:     CancellationToken cancellationToken = default);
   70: public static class ResearchExperimentCanonicalJsonV1
   72: public static string Serialize(ResearchExperimentEvidenceV1 value) =>
   75: public static ResearchExperimentEvidenceV1 Deserialize(string json)
   82: public static string Hash(ResearchExperimentEvidenceV1 value) =>
   86: public static class ResearchExperimentValidatorV1
   88: public static void RequireValid(ResearchExperimentEvidenceV1 evidence)
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyCandidateCompositionV1.cs
```cs
    8: public sealed record StrategySpecialistRequestV1(
   16: public sealed record StrategyCandidateDraftV1(
   24: public sealed record StrategyCandidateAmendmentV1(
   31: public sealed record StrategyCandidateCompositionIssueV1(
   36: public sealed record StrategyCandidateCompositionResultV1(
   41: public bool Success => Candidate is not null && Assessment is not null && Issues.Count == 0;
   49: public static class StrategyCandidateComposerV1
   51: public static StrategyCandidateCompositionResultV1 Compose(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyCandidateConfirmationV1.cs
```cs
    3: public sealed record StrategyCandidateConfirmationIssueV1(
   13: public sealed record StrategyCandidateConfirmationResultV1(
   18: public bool Success => Candidate is not null && Assessment is not null && Issues.Count == 0;
   28: public static class StrategyCandidateConfirmationV1
   30: public static StrategyCandidateConfirmationResultV1 Confirm(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyCandidateLoweringRequestV1.cs
```cs
    7: public sealed record StrategyCandidateLoweringRequestV1(
   14: public const string CurrentSchemaVersion = "strategy-candidate-lowering-request/v1";
   17: public sealed record StrategyCandidateLoweringRequestResultV1(
   21: public bool Success => Request is not null && Issues.Count == 0;
   30: public static class StrategyCandidateLoweringBoundaryV1
   32: public static StrategyCandidateLoweringRequestResultV1 Create(StrategyCandidateV1? candidate)
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyCandidateV1.cs
```cs
    6: public enum StrategyCandidateStatusV1
   19: public enum StrategyCandidateGroupKindV1
   33: public enum StrategyCandidateStatementKindV1
   46: public enum StrategyCandidateStatementSourceV1
   58: public enum StrategyCandidateStatementStateV1
   68: public enum StrategyBuildSupportStatusV1
   77: public enum StrategyInterpretationConfidenceV1
   90: public sealed record StrategyCandidateValueV1(
   95: public sealed record StrategyInterpretationAlternativeV1(
  100: public sealed record StrategyCandidateInterpretationV1(
  109: public sealed record StrategyCandidateStatementV1(
  122: public sealed record StrategyCandidateGroupV1(
  135: public sealed record StrategyBuildSupportItemV1(
  148: public sealed record StrategyCandidateV1(
  160: public const string CurrentSchemaVersion = "strategy-candidate/v1";
  164: public sealed record StrategyGenerationWorkspaceV1(
  172: public const string CurrentSchemaVersion = "strategy-generation-workspace/v1";
  176: public static class StrategyCandidateCanonicalJsonV1
  178: public static string Serialize(StrategyCandidateV1 candidate) =>
  181: public static StrategyCandidateV1 Deserialize(string json) =>
  184: public static string Hash(StrategyCandidateV1 candidate) =>
  187: public static string Canonicalize(string json) =>
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyCandidateValidatorV1.cs
```cs
    3: public enum StrategyCandidateIssueScopeV1
   10: public sealed record StrategyCandidateIssueV1(
   21: public sealed record StrategyCandidateAssessmentV1(
   25: public bool IsStructurallyValid => Issues.All(static issue => issue.Scope != StrategyCandidateIssueScopeV1.Structure);
   27: public bool CanConfirm => IsStructurallyValid &&
   30: public bool CanLower => Status == StrategyCandidateStatusV1.Confirmed && CanConfirm &&
   34: public static class StrategyCandidateValidatorV1
   36: public static StrategyCandidateAssessmentV1 Assess(StrategyCandidateV1? candidate)
   92: public static IReadOnlyList<StrategyCandidateIssueV1> ValidateWorkspace(StrategyGenerationWorkspaceV1? workspace)
```

## src/linux/Core/TradingTerminal.Core/Strategies/Generation/StrategyWorkspaceV1.cs
```cs
    6: public enum StrategyWorkspaceStageV1
   16: public enum StrategyWorkspaceStageRequirementV1
   23: public enum StrategyWorkspaceStageStateV1
   36: public enum StrategyWorkspaceChangeKindV1
   55: public sealed record StrategyWorkspaceBindingsV1(
   68: public sealed record StrategyWorkspaceStageSnapshotV1(
   79: public sealed record StrategyWorkspaceRevisionV1(
   91: public const string CurrentSchemaVersion = "strategy-workspace/v1";
   93: public StrategyWorkspaceStageSnapshotV1 Stage(StrategyWorkspaceStageV1 stage) =>
   97: public sealed record StrategyWorkspaceIssueV1(string Code, string Path, string Message);
   99: public static class StrategyWorkspaceCanonicalJsonV1
  101: public static string Serialize(StrategyWorkspaceRevisionV1 value) =>
  104: public static StrategyWorkspaceRevisionV1 Deserialize(string json)
  111: public static string Hash(StrategyWorkspaceRevisionV1 value) =>
  114: public static string HashArtifact(object value) =>
  119: public static class StrategyWorkspaceRevisionPolicyV1
  121: public static StrategyWorkspaceRevisionV1 Create(
  147: public static StrategyWorkspaceRevisionV1 Revise(
  185: public static StrategyWorkspaceBindingsV1 InvalidateDownstream(
  255: public static IReadOnlyList<StrategyWorkspaceIssueV1> Validate(StrategyWorkspaceRevisionV1? value)
  294: public static void RequireValid(StrategyWorkspaceRevisionV1 value)
```

## src/linux/Core/TradingTerminal.Core/Strategies/IPluginFaultAttribution.cs
```cs
    7: public interface IPluginFaultAttribution
    9:     string PluginName { get; }
```

## src/linux/Core/TradingTerminal.Core/Strategies/IStrategyFactory.cs
```cs
   13: public interface IStrategyFactory
   15:     IReadOnlyList<ITradingStrategy> All { get; }
   21:     StrategyHost Create(string strategyId);
   28:     void Register(ITradingStrategy strategy, StrategyFactoryRegistration registration);
   31:     event EventHandler<StrategyCatalogChange>? Changed;
   36: public sealed record StrategyCatalogChange(ITradingStrategy Strategy, bool Replaced);
```

## src/linux/Core/TradingTerminal.Core/Strategies/ITradingStrategy.cs
```cs
   10: public interface ITradingStrategy
   13:     string Id { get; }
   23:     string? BacktestStrategyId => null;
   25:     string DisplayName { get; }
   27:     string Description { get; }
   36:     StrategyDataRequirement DataRequirement =>
   37:     StrategyDataRequirement.L1 | StrategyDataRequirement.Bars;
   44:     string? ResearchPaperUrl => null;
   52:     string? LinkUrl => ResearchPaperUrl;
   61:     IReadOnlyList<AssetClass> AssetClasses => Array.Empty<AssetClass>();
   69:     StrategyAssetScope AssetScope => StrategyAssetScope.SingleAsset;
   79:     IReadOnlyList<BrokerKind> SupportedBrokers => StrategyBrokerCapability.ForRequirement(DataRequirement);
```

## src/linux/Core/TradingTerminal.Core/Strategies/Parameters/ParameterKind.cs
```cs
    9: public enum ParameterKind
```

## src/linux/Core/TradingTerminal.Core/Strategies/Parameters/StrategyParameter.cs
```cs
   16: public sealed record StrategyParameter
   19: public required string Key { get; init; }
   22: public string Name => Key;
   25: public required string DisplayName { get; init; }
   28: public ParameterKind Kind { get; init; }
   31: public object? Default { get; init; }
   34: public double? Min { get; init; }
   37: public double? Max { get; init; }
   40: public double? Step { get; init; }
   43: public IReadOnlyList<string>? Choices { get; init; }
   46: public string? Description { get; init; }
   49: public string? Group { get; init; }
   52: public string? Unit { get; init; }
   57: public static StrategyParameter Int(
   68: public static StrategyParameter Number(
   79: public static StrategyParameter Bool(
   88: public static StrategyParameter Choice(
   97: public static StrategyParameter Enum<TEnum>(
  119: public static StrategyParameter Instrument(
  132: public static StrategyParameter Text(
```

## src/linux/Core/TradingTerminal.Core/Strategies/Parameters/StrategyParameterSchema.cs
```cs
   11: public sealed class StrategyParameterSchema
   14: public static StrategyParameterSchema Empty { get; } = new(Array.Empty<StrategyParameter>());
   16: public StrategyParameterSchema(IEnumerable<StrategyParameter> parameters)
   31: public StrategyParameterSchema(params StrategyParameter[] parameters)
   36: public IReadOnlyList<StrategyParameter> Parameters { get; }
   38: public bool IsEmpty => Parameters.Count == 0;
   40: public StrategyParameter? Find(string key) =>
   44: public StrategyParameters CreateDefaults() => new(this);
```

## src/linux/Core/TradingTerminal.Core/Strategies/Parameters/StrategyParameters.cs
```cs
   16: public sealed class StrategyParameters
   18: public StrategyParameters(StrategyParameterSchema schema, IReadOnlyDictionary<string, object?>? values = null)
   38: public StrategyParameterSchema Schema { get; }
   41: public void Set(string key, object? value)
   47: public int GetInt(string key) => (int)GetLong(key);
   49: public long GetLong(string key) =>
   52: public double GetDouble(string key) =>
   55: public bool GetBool(string key) =>
   58: public string GetString(string key) =>
   62: public string GetText(string key) => GetString(key);
   65: public TEnum GetEnum<TEnum>(string key) where TEnum : struct, System.Enum
   75: public InstrumentId GetInstrument(string key) =>
   81: public object? GetRaw(string key) => _values[Require(key).Key];
   84: public IReadOnlyDictionary<string, object?> ToDictionary() =>
   92: public IReadOnlyList<string> Validate()
```

## src/linux/Core/TradingTerminal.Core/Strategies/PluginFaultEvents.cs
```cs
    8: public static class PluginFaultEvents
   10: public static event Action<Exception>? Reported;
   12: public static void Report(Exception exception)
```

## src/linux/Core/TradingTerminal.Core/Strategies/Specification/StrategyCapabilityProfile.cs
```cs
    8: public enum StrategyRuntimeCapability
   43: public sealed record StrategyCapabilityRequirement(
   48: public sealed record StrategyCapabilityProfile(
   53: public StrategyCapabilityAssessment Assess(StrategySpec spec)
   72: public static class StrategyCapabilityInference
   74: public static IReadOnlyList<StrategyCapabilityRequirement> Infer(StrategySpec spec)
  260: public sealed record StrategyCapabilityAssessment(
  266: public bool IsSupported => Issues.Count == 0 && Missing.Count == 0;
```

## src/linux/Core/TradingTerminal.Core/Strategies/Specification/StrategySpec.cs
```cs
    6: public enum StrategyObjectiveKind
   17: public enum ReturnHypothesisKind
   36: public enum StrategyTriggerKind
   50: public enum StrategyHorizonKind
   60: public enum MarketTopologyKind
   73: public enum ExposureGeometryKind
   86: public enum StrategyInformationKind
  101: public enum SignalModelKind
  115: public enum PortfolioConstructionKind
  129: public enum StrategyExecutionPolicyKind
  146: public enum StrategyStateKind
  158: public enum StrategyRiskExitKind
  174: public enum StrategyAdaptationKind
  189: public sealed record StrategyTimeSemantics(
  195: public sealed record StrategyContextSpec(
  203: public sealed record StrategySignalSpec(
  209: public sealed record StrategyPortfolioSpec(PortfolioConstructionKind Construction);
  212: public sealed record StrategyRiskSpec(IReadOnlyList<StrategyRiskExitKind> Rules);
  215: public sealed record StrategyExecutionSpec(IReadOnlyList<StrategyExecutionPolicyKind> Policies);
  218: public sealed record StrategyStateSpec(
  229: public sealed record StrategySpec(
  246: public IReadOnlyList<StrategyCapabilityRequirement> Requirements =>
  251: public sealed record StrategySpecIssue(string Code, string Path, string Message);
  254: public static class StrategySpecValidator
  256: public static IReadOnlyList<StrategySpecIssue> Validate(StrategySpec? spec)
```

## src/linux/Core/TradingTerminal.Core/Strategies/StrategyAssetScope.cs
```cs
    9: public enum StrategyAssetScope
```

## src/linux/Core/TradingTerminal.Core/Strategies/StrategyBrokerCapability.cs
```cs
   12: public static class StrategyBrokerCapability
   25: public static readonly IReadOnlyList<BrokerKind> TapeBrokers =
   31: public static readonly IReadOnlyList<BrokerKind> DepthBrokers =
   39: public static IReadOnlyList<BrokerKind> ForRequirement(StrategyDataRequirement requirement)
```

## src/linux/Core/TradingTerminal.Core/Strategies/StrategyDataRequirement.cs
```cs
   22: public enum StrategyDataRequirement
```

## src/linux/Core/TradingTerminal.Core/Strategies/StrategyFactoryRegistration.cs
```cs
    8: public sealed record StrategyFactoryRegistration(
```

## src/linux/Core/TradingTerminal.Core/Strategies/StrategyHost.cs
```cs
    8: public sealed record StrategyHost(
```

## src/linux/Core/TradingTerminal.Core/Strategies/StrategySignal.cs
```cs
    4: public enum StrategySignalKind : long
   15: public readonly record struct StrategySignal(
   21: public readonly record struct StrategySignalEvent(
   30: public interface IStrategySignalSink
   32:     Task EmitSignalAsync(StrategySignal signal, CancellationToken ct = default);
```
