# DaxAlgo.Codegen — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Tools/DaxAlgo.Codegen/AgentCliCodegenClient.cs
```cs
   10: public sealed record AgentCliAdapter(
   29: public static AgentCliAdapter ClaudeCode { get; } =
   42: public static AgentCliAdapter Codex { get; } =
   47: public static IReadOnlyList<AgentCliAdapter> All { get; } = [ClaudeCode, Codex];
   52: public IReadOnlyList<string>? StreamFlags { get; init; }
   56: public IReadOnlyList<string> ArgumentsFor(
  101: public sealed class AgentCliCodegenClient : IStrategyCodegenClient
  110: public AgentCliCodegenClient(
  124: public string ProviderId => _adapter.ProviderId;
  125: public string DisplayName => _adapter.DisplayName;
  126: public bool IsAvailable => _resolveOnPath(_adapter.Executable) is not null;
  129: public string Model => _model ?? string.Empty;
  130: public CodegenEffort Effort => _effort;
  131: public IReadOnlyList<string> KnownModels => AiModelCatalog.Offer(ProviderId, _model);
  139: public async IAsyncEnumerable<CodegenEvent> StreamAsync(
  287: public async Task<StrategyCodegenResponse> GenerateAsync(StrategyCodegenRequest request, CancellationToken ct = default)
```

## src/linux/Tools/DaxAlgo.Codegen/AiModelCatalog.cs
```cs
   16: public static class AiModelCatalog
   29: public static IReadOnlyList<string> For(string providerId) => providerId.ToLowerInvariant() switch
   40: public static IReadOnlyList<string> Offer(string providerId, string? configuredModel)
   55: public static bool SupportsEffort(string providerId) => providerId.ToLowerInvariant() switch
```

## src/linux/Tools/DaxAlgo.Codegen/AiStrategyBuilder.cs
```cs
   13: public interface IAiStrategyBuilder
   17:     IReadOnlyList<IStrategyCodegenClient> Providers { get; }
   20:     IStrategyCodegenClient? DefaultProvider { get; }
   25:     IStrategyCodegenClient? WithSettings(string providerId, string? model, CodegenEffort effort);
   29:     IReadOnlyList<string> ModelsFor(string providerId);
   37:     IReadOnlyList<AiModelChoice> AllModels();
   45:     StrategyBuildSession StartSession(
   46:     IStrategyCodegenClient provider, string strategyId, string displayName,
   47:     IReadOnlyList<CodegenMessage>? history = null, CodegenUsage? priorUsage = null,
   48:     StrategyBuildProfile? profile = null);
   53:     Task<StrategyBuildLoopResult> BuildAsync(
   54:     IStrategyCodegenClient provider, string instruction, string strategyId, string displayName,
   55:     CancellationToken ct = default);
   58: public sealed class AiStrategyBuilder(
   64: public IReadOnlyList<IStrategyCodegenClient> Providers => factory.BuildAll();
   66: public IStrategyCodegenClient? DefaultProvider => factory.SelectDefault();
   68: public IStrategyCodegenClient? WithSettings(string providerId, string? model, CodegenEffort effort) =>
   71: public IReadOnlyList<string> ModelsFor(string providerId) => factory.ModelsFor(providerId);
   73: public IReadOnlyList<AiModelChoice> AllModels()
   95: public StrategyBuildSession StartSession(
  102: public Task<StrategyBuildLoopResult> BuildAsync(
```

## src/linux/Tools/DaxAlgo.Codegen/AnthropicCodegenClient.cs
```cs
   14: public sealed class AnthropicCodegenClient : IStrategyCodegenClient
   25: public AnthropicCodegenClient(
   36: public string ProviderId => "anthropic";
   37: public string DisplayName => "Anthropic (API key)";
   38: public bool IsAvailable => !string.IsNullOrWhiteSpace(_model) && !string.IsNullOrWhiteSpace(_apiKey);
   39: public string Model => _model;
   40: public CodegenEffort Effort => _effort;
   41: public IReadOnlyList<string> KnownModels => AiModelCatalog.Offer(ProviderId, _model);
   45: public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
   72: public async IAsyncEnumerable<CodegenEvent> StreamAsync(
  145: public async Task<StrategyCodegenResponse> GenerateAsync(StrategyCodegenRequest request, CancellationToken ct = default)
  272: public WireCacheControl? CacheControl { get; init; }
  289: public static WireCacheControl Ephemeral { get; } = new("ephemeral");
```

## src/linux/Tools/DaxAlgo.Codegen/AnthropicStreamParser.cs
```cs
   21: public string Text => _text.ToString();
   23: public CodegenUsage Usage => new(_input, _output, _cached);
   29: public IEnumerable<CodegenEvent> Consume(JsonElement evt)
   81: public static async IAsyncEnumerable<JsonElement> ReadAsync(
```

## src/linux/Tools/DaxAlgo.Codegen/AuthoredUnitIntentClassifierV1.cs
```cs
   11: public sealed class AuthoredUnitIntentClassifierV1 : IAuthoredUnitIntentClassifierV1
   13: public async Task<AuthoredUnitIntentClassificationResultV1> ClassifyAsync(
```

## src/linux/Tools/DaxAlgo.Codegen/AuthoredUnitSourceGeneratorV1.cs
```cs
   12: public sealed class AuthoredUnitSourceGeneratorV1 : IAuthoredUnitSourceGeneratorV1
   14: public const int MaxFiles = 8;
   15: public const int MaxSourceCharacters = 500_000;
   17: public async Task<AuthoredUnitSourceGenerationResultV1> GenerateAsync(
```

## src/linux/Tools/DaxAlgo.Codegen/AuthoredUnitSpecificationGeneratorV1.cs
```cs
   14: public sealed class AuthoredUnitSpecificationGeneratorV1 : IAuthoredUnitSpecificationGeneratorV1
   16: public const int MaxInputCharacters = 100_000;
   17: public const int MaxResponseCharacters = 500_000;
   18: public const int MaxAvailableInstruments = 512;
   20: public async Task<AuthoredUnitSpecificationGenerationResultV1> GenerateAsync(
```

## src/linux/Tools/DaxAlgo.Codegen/ChartReferenceInspectorV1.cs
```cs
    8: public sealed record ChartReferenceInspectionRequestV1(
   12: public sealed record ChartReferenceInspectionResultV1(
   17: public bool Success => Inspection is not null && string.IsNullOrWhiteSpace(Error);
   20: public interface IChartReferenceInspectorV1
   22:     Task<ChartReferenceInspectionResultV1> InspectAsync(
   23:     IStrategyCodegenClient provider,
   24:     ChartReferenceInspectionRequestV1 request,
   25:     CancellationToken cancellationToken = default);
   32: public sealed class ChartReferenceInspectorV1 : IChartReferenceInspectorV1
   37: public async Task<ChartReferenceInspectionResultV1> InspectAsync(
```

## src/linux/Tools/DaxAlgo.Codegen/CliWorkspaceLauncher.cs
```cs
   11: public sealed record CliLaunchResult(bool Success, string Message, string WorkspacePath);
   20: public interface ICliWorkspaceLauncher
   24:     IReadOnlyList<AgentCliAdapter> AvailableClis();
   28:     CliLaunchResult Launch(AgentCliAdapter adapter, string strategyId, string displayName, StrategyBuildEffort effort);
   37: public sealed class CliWorkspaceLauncher(
   42: public IReadOnlyList<AgentCliAdapter> AvailableClis() =>
   45: public CliLaunchResult Launch(AgentCliAdapter adapter, string strategyId, string displayName, StrategyBuildEffort effort)
  332: public sealed class MyStrategy : IBacktestStrategy
  334: public static StrategyParameterSchema Schema { get; } = new(
  338: public static IBacktestStrategy Create(Contract contract, StrategyParameters p) =>
  345: public MyStrategy(Contract contract) : this(contract, 20, 1.5) { }
  347: public MyStrategy(Contract contract, int lookback, double threshold)
  354: public Task OnStartAsync(IClock clock, IOrderRouter router, CancellationToken ct)
  357: public Task OnTickAsync(Tick tick, IClock clock, IOrderRouter router, CancellationToken ct)
  365: public Task OnOrderEventAsync(OrderEvent evt, CancellationToken ct) => Task.CompletedTask;
  367: public Task OnEndAsync(IClock clock, IOrderRouter router, CancellationToken ct)
```

## src/linux/Tools/DaxAlgo.Codegen/CodegenCodeExtractor.cs
```cs
   15: public static partial class CodegenCodeExtractor
   31: public static string Extract(string? reply)
   44: public static string StripCode(string? reply)
   66: public static IReadOnlyList<StrategyFile> ExtractFiles(string? reply)
```

## src/linux/Tools/DaxAlgo.Codegen/FakeCodegenClient.cs
```cs
   11: public sealed class FakeCodegenClient : IStrategyCodegenClient
   19: public FakeCodegenClient(params string[] replies)
   24: public string ProviderId => "fake";
   25: public string DisplayName => "Fake (deterministic)";
   26: public bool IsAvailable => true;
   29: public int CallCount { get; private set; }
   32: public CodegenUsage Usage { get; init; } = new(100, 50);
   36: public StrategyCodegenRequest? LastRequest { get; private set; }
   38: public Task<StrategyCodegenResponse> GenerateAsync(StrategyCodegenRequest request, CancellationToken ct = default)
   58: public const string DefaultKernel = """
   60: public sealed class GeneratedStrategy(Contract contract) : IBacktestStrategy
   66: public Task OnStartAsync(IClock clock, IOrderRouter router, CancellationToken ct) => Task.CompletedTask;
   68: public async Task OnTickAsync(Tick tick, IClock clock, IOrderRouter router, CancellationToken ct)
   77: public Task OnOrderEventAsync(OrderEvent evt, CancellationToken ct) => Task.CompletedTask;
   78: public Task OnEndAsync(IClock clock, IOrderRouter router, CancellationToken ct) => Task.CompletedTask;
```

## src/linux/Tools/DaxAlgo.Codegen/OpenAiCompatibleCodegenClient.cs
```cs
   15: public sealed class OpenAiCompatibleCodegenClient : IStrategyCodegenClient
   28: public OpenAiCompatibleCodegenClient(
   45: public string ProviderId { get; }
   46: public string DisplayName { get; }
   48: public bool IsAvailable =>
   52: public string Model => _model;
   53: public CodegenEffort Effort => _effort;
   54: public IReadOnlyList<string> KnownModels => AiModelCatalog.Offer(ProviderId, _model);
   58: public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken ct = default)
   86: public async IAsyncEnumerable<CodegenEvent> StreamAsync(
  218: public async Task<StrategyCodegenResponse> GenerateAsync(StrategyCodegenRequest request, CancellationToken ct = default)
```

## src/linux/Tools/DaxAlgo.Codegen/ParallelStrategyCandidateGeneratorV1.cs
```cs
   19: public static StrategyGenerationAutomaticRepairDispositionV1 Classify(
  122: public sealed class StrategyGenerationLaneAgentV1(
  126: public StrategyGenerationLaneV1 Lane { get; } = lane;
  128: public async Task<StrategyGenerationLaneResultV1> GenerateAsync(
  704: public sealed class ParallelStrategyCandidateGeneratorV1 : IParallelStrategyCandidateGeneratorV1
  706: public const int MaxUserPromptCharacters = 100_000;
  712: public ParallelStrategyCandidateGeneratorV1(
  739: public async Task<ParallelStrategyGenerationResultV1> GenerateAsync(
```

## src/linux/Tools/DaxAlgo.Codegen/ParallelStrategyGenerationContractsV1.cs
```cs
   10: public enum StrategyGenerationLaneV1
   19: public enum StrategyGenerationLaneProgressStateV1
   32: public sealed record StrategyGenerationLaneProgressV1(
   38: public enum StrategyGenerationArtifactKindV1
   50: public enum StrategyGenerationReadinessV1
   70: public enum StrategyGenerationAutomaticRepairDispositionV1
   89: public enum StrategyGenerationSemanticRoleV1
   96: public enum StrategyGenerationLoweringModeV1
  115: public enum StrategyGenerationExternalCompatibilityV1
  127: public sealed record StrategyGenerationContractAuthorityV1(
  140: public sealed record StrategyGenerationPackageBindingV1(
  151: public enum StrategyVariationAxisKindV1
  164: public sealed record StrategyGenerationParameterV1(
  180: public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
  199: public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
  204: public sealed record StrategyVariationAxisV1(
  215: public sealed record StrategyGenerationArtifactV1(
  226: public sealed record StrategyGenerationCandidateV1(
  243: public const string CurrentSchemaVersion = "strategy-generation-candidate/v3";
  250: public sealed record StrategyGenerationConfirmedIntentContextV1(
  255: public sealed record ParallelStrategyGenerationRequestV1(
  262: public sealed record StrategyGenerationLaneResultV1(
  270: public StrategyGenerationAutomaticRepairDispositionV1 AutomaticRepairDisposition =>
  273: public bool Generated => Candidate is not null && CandidateHashSha256 is not null &&
  281: public bool PackageValidationAvailable =>
  289: public bool Selectable
  329: public bool PackageValid
  356: public sealed record ParallelStrategyGenerationResultV1(
  365: public bool HasPackageValidCandidate =>
  370: public interface IStrategyGenerationLaneAgentV1
  372:     StrategyGenerationLaneV1 Lane { get; }
  374:     Task<StrategyGenerationLaneResultV1> GenerateAsync(
  375:     IStrategyCodegenClient provider,
  376:     ParallelStrategyGenerationRequestV1 request,
  377:     string expectedCandidateId,
  378:     CancellationToken ct = default,
  379:     IProgress<StrategyGenerationLaneProgressV1>? progress = null);
  382: public interface IParallelStrategyCandidateGeneratorV1
  384:     Task<ParallelStrategyGenerationResultV1> GenerateAsync(
  385:     IStrategyCodegenClient provider,
  386:     ParallelStrategyGenerationRequestV1 request,
  387:     CancellationToken ct = default,
  388:     IProgress<StrategyGenerationLaneProgressV1>? progress = null);
  391: public static class StrategyGenerationCandidateCanonicalJsonV1
  393: public static string Serialize(StrategyGenerationCandidateV1 candidate) =>
  396: public static StrategyGenerationCandidateV1 Deserialize(string json) =>
  399: public static string Hash(StrategyGenerationCandidateV1 candidate) =>
  422: public static string SerializeBatch(ParallelStrategyGenerationResultV1 result) =>
  425: public static ParallelStrategyGenerationResultV1 DeserializeBatch(string json) =>
  428: public static string PromptHash(string strategyId, string userPrompt) =>
  431: public static string PromptHash(
  442: public static string RequestHash(
  455: public static string RequestHash(
  540: public const int MaxCanonicalJsonCharacters = 750_000;
  542: public static bool TryValidate(
  599: public static void ValidateOrThrow(
  704: public sealed record StrategyGenerationSelectionResultV1(
  709: public bool Success => Candidate is not null && CandidateHashSha256 is not null &&
  714: public sealed record StrategyGenerationRevalidationResultV1(
  719: public bool Applied => Batch is not null && LaneResult is not null && Issues is not null &&
  725: public static class StrategyGenerationBatchValidationV1
  727: public static IReadOnlyList<StrategyCandidateGenerationIssueV1> Validate(
  907: public static StrategyGenerationSelectionResultV1 Select(
  934: public static StrategyGenerationRevalidationResultV1 RevalidateArtifact(
 1026: public static class StrategyGenerationLaneCatalogV1
 1028: public static IReadOnlyList<StrategyGenerationLaneV1> Ordered { get; } = Array.AsReadOnly(
 1037: public static string WireName(StrategyGenerationLaneV1 lane) => lane switch
 1046: public static string DisplayName(StrategyGenerationLaneV1 lane) => lane switch
 1055: public static StrategyGenerationArtifactKindV1 ArtifactKind(StrategyGenerationLaneV1 lane) => lane switch
```

## src/linux/Tools/DaxAlgo.Codegen/ParallelStrategyGenerationPromptV1.cs
```cs
    8: public static string AgentId(StrategyGenerationLaneV1 lane) => lane switch
   17: public static string SystemContext(StrategyGenerationLaneV1 lane)
   20: public static string UserMessage(ParallelStrategyGenerationRequestV1 request)
   39: public static string RepairMessage(
  138: public const int MaxArtifactCharacters = 750_000;
  140: public static IReadOnlyList<StrategyCandidateGenerationIssueV1> Validate(
```

## src/linux/Tools/DaxAlgo.Codegen/StrategyBacktestSmoke.cs
```cs
   20: public static class StrategyBacktestSmoke
   35: public static async Task<string?> RunAsync(BacktestStrategyOption option, CancellationToken ct = default)
   91: public DateTime UtcNow { get; private set; } = start;
   92: public void Advance(TimeSpan by) => UtcNow += by;
   99: public Task<OrderResult> PlaceOrderAsync(OrderRequest request, CancellationToken ct = default) =>
  102: public Task CancelOrderAsync(string clientOrderId, CancellationToken ct = default) => Task.CompletedTask;
  104: public IObservable<OrderEvent> OrderEvents { get; } = new NeverObservable();
  108: public IDisposable Subscribe(IObserver<OrderEvent> observer) => new Nothing();
```

## src/linux/Tools/DaxAlgo.Codegen/StrategyBuildSession.cs
```cs
    8: public enum BuildTurnKind
   34: public sealed record StrategyBuildTurn(
   43: public bool Success => Kind == BuildTurnKind.Compiled;
   62: public sealed class StrategyBuildSession
  109: public IStrategyCodegenClient Provider { get; }
  112: public string BasePack { get; }
  117: public string SystemContext { get; private set; }
  120: public IReadOnlyList<StrategySkill> LoadedSkills { get; private set; } = [];
  121: public string StrategyId { get; }
  122: public string DisplayName { get; }
  123: public int MaxFixAttempts { get; }
  126: public StrategyBuildProfile? Profile { get; }
  129: public IReadOnlyList<CodegenMessage> Transcript => _messages;
  132: public IReadOnlyList<StrategyFile> Files { get; private set; } = [];
  135: public CodegenUsage TotalUsage { get; private set; } = CodegenUsage.None;
  147: public async Task<StrategyBuildTurn> SendAsync(
  257: public void SyncEditedFiles(IReadOnlyList<StrategyFile> files) => Files = files;
```

## src/linux/Tools/DaxAlgo.Codegen/StrategyCandidateGenerationOrchestratorV1.cs
```cs
    9: public enum StrategyCandidateGenerationIssueSeverityV1
   15: public sealed record StrategyCandidateGenerationIssueV1(
   21: public sealed record StrategyGenerationAgentRunV1(
   34: public sealed record StrategyCandidateGenerationRequestV1(
   44: public sealed record StrategyCandidateGenerationResultV1(
   52: public bool Success => Candidate is not null && Assessment is not null &&
   56: public interface IStrategyCandidateGeneratorV1
   58:     Task<StrategyCandidateGenerationResultV1> GenerateAsync(
   59:     IStrategyCodegenClient provider,
   60:     StrategyCandidateGenerationRequestV1 request,
   61:     CancellationToken ct = default);
   69: public interface IStrategyGenerationAgentRouterV1
   71:     IStrategyCodegenClient ResolveSpecialist(
   72:     StrategySpecialistRequestV1 request,
   73:     IStrategyCodegenClient selectedProvider);
   76: public sealed class SameProviderStrategyGenerationAgentRouterV1 : IStrategyGenerationAgentRouterV1
   78: public IStrategyCodegenClient ResolveSpecialist(
   89: public sealed class StrategyCandidateGenerationOrchestratorV1(
   93: public const int MaxSpecialists = 4;
   94: public const int MaxUserInputCharacters = 100_000;
   95: public const int MaxModelResponseCharacters = 1_000_000;
  101: public async Task<StrategyCandidateGenerationResultV1> GenerateAsync(
  642: public static bool TryDeserialize<T>(
```

## src/linux/Tools/DaxAlgo.Codegen/StrategyCandidateGenerationPromptV1.cs
```cs
    8: public const string IntakeAgentId = "strategy.intake@1";
   10: public static string IntakeSystemContext => """
  112: public static string CreateIntakeUserMessage(
  132: public static string SpecialistSystemContext() => """
  162: public static string CreateSpecialistUserMessage(
```

## src/linux/Tools/DaxAlgo.Codegen/StrategyCodegenClientFactory.cs
```cs
   19: public sealed class StrategyCodegenClientFactory
   28: public StrategyCodegenClientFactory(Func<HttpClient> httpFactory, AiCodegenOptions options, Func<string, string?> keyResolver)
   38: public IReadOnlyList<IStrategyCodegenClient> BuildAll()
   67: public IStrategyCodegenClient? Build(string providerId, string? model, CodegenEffort effort = CodegenEffort.Default)
   87: public IReadOnlyList<string> ModelsFor(string providerId) =>
   92: public IStrategyCodegenClient? SelectDefault()
```

## src/linux/Tools/DaxAlgo.Codegen/StrategyCodegenOrchestrator.cs
```cs
   10: public sealed record StrategyBuildLoopResult(
   32: public sealed class StrategyCodegenOrchestrator(
   46: public StrategyBuildSession CreateSession(
   58: public async Task<StrategyBuildLoopResult> BuildAsync(
```

## src/linux/Tools/DaxAlgo.Codegen/StrategyCodegenServiceCollectionExtensions.cs
```cs
   12: public static class StrategyCodegenServiceCollectionExtensions
   23: public static IServiceCollection AddStrategyCodegen(this IServiceCollection services, IConfiguration configuration)
```

## src/linux/Tools/DaxAlgo.Codegen/StrategyContextPack.cs
```cs
   11: public sealed class StrategyContextPack
   16: public string SystemPrompt { get; }
   22: public static StrategyContextPack Load()
```

## src/linux/Tools/DaxAlgo.Codegen/StrategyGenerationPackageCatalogV1.cs
```cs
   91: public static bool IsSupported(StrategyGenerationLaneV1 lane) =>
   97: public static bool PackageValidationAvailable(StrategyGenerationLaneV1 lane) =>
  100: public static StrategyGenerationPackageBindingV1 RequireBinding(StrategyGenerationLaneV1 lane) => lane switch
  109: public static string ArtifactFileName(StrategyGenerationLaneV1 lane) => lane switch
  118: public static string ArtifactLanguage(StrategyGenerationLaneV1 lane) => lane switch
  126: public static string UnsupportedReason(StrategyGenerationLaneV1 lane) => lane switch
  139: public static string PromptContract(StrategyGenerationLaneV1 lane)
  368: public static IReadOnlyList<StrategyCandidateGenerationIssueV1> ValidatePackage(
```

## src/linux/Tools/DaxAlgo.Codegen/StrategyGenerationSessionV1.cs
```cs
   11: public sealed class StrategyGenerationSessionV1
   18: public StrategyGenerationSessionV1(
   51: public string WorkspaceId { get; }
   52: public string WorkspaceName { get; }
   53: public string CandidateId { get; }
   54: public StrategyCandidateV1? CurrentCandidate { get; private set; }
   55: public IReadOnlyList<StrategyCandidateV1> Revisions => _revisions;
   57: public StrategyGenerationWorkspaceV1 Workspace => new(
   65: public async Task<StrategyCandidateGenerationResultV1> SendAsync(
  101: public StrategyCandidateConfirmationResultV1 Confirm(string expectedContentHashSha256)
```

## src/linux/Tools/DaxAlgo.Codegen/StrategySkillLibrary.cs
```cs
   11: public sealed record StrategySkill(string Id, string Name, IReadOnlyList<string> Triggers, string Body)
   15: public int Score(string text)
   41: public sealed class StrategySkillLibrary
   46: public const int MaxSkillsPerSession = 3;
   47: public const int MaxCharacters = 12_000;
   53: public IReadOnlyList<StrategySkill> All => _skills;
   57: public static StrategySkillLibrary Load()
   77: public IReadOnlyList<StrategySkill> SelectFor(string? brief) => SelectFor(brief, MaxSkillsPerSession);
   82: public IReadOnlyList<StrategySkill> SelectFor(string? brief, int maxSkills)
  109: public static string Compose(string basePack, IReadOnlyList<StrategySkill> skills)
```

## src/linux/Tools/DaxAlgo.Codegen/TradeIrCandidateSynthesisV1.cs
```cs
    7: public sealed record StrategySynthesisSourceV1(
   14: public sealed record TradeIrCandidateSynthesisRequestV1(
   18: public sealed record TradeIrSynthesisReceiptV1(
   32: public const string CurrentSchemaVersion = "trade-ir-candidate-synthesis-receipt/v2";
   35: public sealed record TradeIrCandidateSynthesisResultV1(
   40: public bool Success => TradeIrCandidateSynthesisValidationV1.Validate(this).Count == 0;
   43: public interface ITradeIrCandidateSynthesizerV1
   45:     Task<TradeIrCandidateSynthesisResultV1> SynthesizeAsync(
   46:     IStrategyCodegenClient provider,
   47:     TradeIrCandidateSynthesisRequestV1 request,
   48:     CancellationToken ct = default);
   51: public static class TradeIrCandidateSynthesisCanonicalJsonV1
   53: public const string AgentId = "strategy.tradeir_synthesis@2";
   55: public static string ReceiptHash(TradeIrSynthesisReceiptV1 receipt) =>
   58: public static string RequestHash(
   79: public static string SynthesisId(string strategyId) => $"{strategyId.Trim()}/tradeir-synthesis/v1";
   95: public static class TradeIrCandidateSynthesisValidationV1
   97: public static IReadOnlyList<StrategyCandidateGenerationIssueV1> Validate(
  319: public sealed class TradeIrCandidateSynthesizerV1 : ITradeIrCandidateSynthesizerV1
  321: public const int MaxSourcePayloadCharacters = 1_000_000;
  323: public async Task<TradeIrCandidateSynthesisResultV1> SynthesizeAsync(
  555: public static string SystemContext { get; } = """
  569: public static string UserMessage(
```

## src/linux/Tools/DaxAlgo.Codegen/VibeQuantDeclarativeRulesContractV1.cs
```cs
   13: public const string SchemaVersion = "vibe-quant/declarative-rules/v1";
   16: public static IReadOnlyList<StrategyCandidateGenerationIssueV1> Validate(
```
