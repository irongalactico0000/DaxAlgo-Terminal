# TradingTerminal.Ai.Coordinator — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/AI/TradingTerminal.Ai.Coordinator/Datasets/CoordinatorDatasetTools.cs
```cs
   10: public sealed record DatasetRights(
   17: public sealed record DatasetSourceAsset(
   28: public sealed record CoordinatorDatasetExample
   30: public required string SchemaVersion { get; init; }
   31: public required string Id { get; init; }
   32: public required string Split { get; init; }
   33: public required string ContaminationGroup { get; init; }
   34: public required CoordinatorRole Role { get; init; }
   35: public required string Objective { get; init; }
   36: public IReadOnlyList<DatasetSourceAsset> Sources { get; init; } = [];
   37: public IReadOnlyList<CoordinatorRoleOutput> PriorOutputs { get; init; } = [];
   38: public required CoordinatorRoleOutput ReferenceOutput { get; init; }
   39: public IReadOnlyList<string> RequiredConcepts { get; init; } = [];
   40: public IReadOnlyList<string> ForbiddenConcepts { get; init; } = [];
   41: public required string Provenance { get; init; }
   42: public required string License { get; init; }
   43: public required DatasetRights Rights { get; init; }
   44: public required string ReviewStatus { get; init; }
   45: public required DateTimeOffset CutoffUtc { get; init; }
   46: public bool UseForTraining { get; init; }
   49: public sealed record DatasetValidationIssue(int Line, string? ExampleId, string Message);
   51: public sealed record DatasetValidationReport(
   56: public bool IsValid => Issues.Count == 0;
   59: public static class CoordinatorDatasetTools
   61: public const long MaxDatasetBytes = 100_000_000;
   62: public const int MaxDatasetExamples = 100_000;
   63: public const int MaxJsonLineCharacters = 2_000_000;
   64: public const int MaxReportedIssues = 10_000;
   65: public const long MaxSftExportBytes = 100_000_000;
   76: public static async Task<DatasetValidationReport> ValidateJsonLinesAsync(
  225: public static async Task<int> ExportSftJsonLinesAsync(
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Datasets/ExpertModelDatasetTools.cs
```cs
   11: public enum ExpertTaskKind
   26: public enum ExpertDomain
   38: public enum ExpertDatasetOrigin
   46: public sealed record ExpertModelLineage
   48: public required ExpertDatasetOrigin Origin { get; init; }
   49: public required string Producer { get; init; }
   50: public string? ModelId { get; init; }
   51: public string? ModelRevision { get; init; }
   52: public string? PromptSha256 { get; init; }
   53: public string? ProfileVersion { get; init; }
   54: public required DateTimeOffset ProducedAtUtc { get; init; }
   57: public sealed record ExpertVerificationEvidence
   59: public bool Verified { get; init; }
   60: public required string Reviewer { get; init; }
   61: public required DateTimeOffset VerifiedAtUtc { get; init; }
   62: public required string EvidenceSha256 { get; init; }
   63: public IReadOnlyList<string> Evidence { get; init; } = [];
   64: public bool CompileSucceeded { get; init; }
   65: public IReadOnlyList<string> CompileEvidence { get; init; } = [];
   66: public bool TestsPassed { get; init; }
   67: public IReadOnlyList<string> TestEvidence { get; init; } = [];
   70: public sealed record ExpertModelDatasetExample
   72: public required string SchemaVersion { get; init; }
   73: public required string Id { get; init; }
   74: public required ExpertTaskKind TaskKind { get; init; }
   75: public IReadOnlyList<ExpertDomain> Domains { get; init; } = [];
   76: public IReadOnlyList<LlmMessage> Messages { get; init; } = [];
   77: public required string ConversationSha256 { get; init; }
   78: public IReadOnlyList<DatasetSourceAsset> Sources { get; init; } = [];
   79: public required ExpertModelLineage Lineage { get; init; }
   80: public required ExpertVerificationEvidence Verification { get; init; }
   81: public required string Split { get; init; }
   82: public required string ContaminationGroup { get; init; }
   83: public required string Provenance { get; init; }
   84: public required string License { get; init; }
   85: public required DatasetRights Rights { get; init; }
   86: public required string ReviewStatus { get; init; }
   87: public required DateTimeOffset CutoffUtc { get; init; }
   88: public bool UseForTraining { get; init; }
   91: public sealed record ExpertDatasetCoverageCell(
   98: public sealed record ExpertDatasetCoverageReport(
  107: public static class ExpertModelDatasetTools
  109: public const long MaxDatasetBytes = 100_000_000;
  110: public const int MaxDatasetExamples = 100_000;
  111: public const int MaxJsonLineCharacters = 2_000_000;
  112: public const int MaxReportedIssues = 10_000;
  113: public const long MaxSftExportBytes = 100_000_000;
  114: public const int MaxMessageCount = 64;
  115: public const int MaxMessageCharacters = 500_000;
  116: public const int MaxAggregateMessageCharacters = 2_000_000;
  117: public const int MaxSourceCount = 64;
  118: public const int MaxSourceCharacters = 500_000;
  119: public const int MaxAggregateSourceCharacters = 2_000_000;
  120: public const int MaxEvidenceItems = 128;
  121: public const int MaxEvidenceCharacters = 2_000;
  138: public static async Task<DatasetValidationReport> ValidateJsonLinesAsync(
  143: public static string ComputeConversationSha256(IReadOnlyList<LlmMessage> messages)
  156: public static string ComputeVerificationEvidenceSha256(
  171: public static async Task<ExpertDatasetCoverageReport> BuildCoverageReportAsync(
  223: public static async Task<int> ExportSftJsonLinesAsync(
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Models/DeterministicMockLlmProvider.cs
```cs
   12: public sealed class DeterministicMockLlmProvider : ILlmProvider
   14: public DeterministicMockLlmProvider(LlmProviderDescriptor? descriptor = null)
   23: public LlmProviderDescriptor Descriptor { get; }
   25: public Task<LlmCallResult> CompleteAsync(LlmRequest request, CancellationToken ct = default)
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Models/ILlmProvider.cs
```cs
    9: public interface ILlmProvider
   11:     LlmProviderDescriptor Descriptor { get; }
   13:     string ProviderId => Descriptor.ProviderId;
   15:     string ModelId => Descriptor.ModelId;
   17:     Task<LlmCallResult> CompleteAsync(LlmRequest request, CancellationToken ct = default);
   21: public interface IResumableLlmProvider
   23:     void ResumeAfter(int consumedRequestCount);
   27: public static class LlmFailureKinds
   29: public const string InvalidRequest = "invalid_request";
   30: public const string Authentication = "authentication";
   31: public const string RateLimited = "rate_limited";
   32: public const string Timeout = "timeout";
   33: public const string Transport = "transport";
   34: public const string ProviderUnavailable = "provider_unavailable";
   35: public const string ProviderRejected = "provider_rejected";
   36: public const string InvalidResponse = "invalid_response";
   37: public const string ResponseTooLarge = "response_too_large";
   38: public const string ReplayMismatch = "replay_mismatch";
   39: public const string ReplayExhausted = "replay_exhausted";
   44: public static LlmCallResult? Validate(LlmRequest? request)
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Models/LlmProviderValidation.cs
```cs
    4: public static class LlmProviderValidation
    6: public static bool TryValidateEndpoint(string? endpoint, out Uri? endpointUri, out string? safeError)
   43: public static Uri ValidateEndpoint(string? endpoint, string parameterName = "endpoint")
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Models/OpenAiCompatibleLlmProvider.cs
```cs
   12: public sealed class OpenAiCompatibleLlmProvider : ILlmProvider
   25: public OpenAiCompatibleLlmProvider(
   50: public LlmProviderDescriptor Descriptor { get; }
   52: public async Task<LlmCallResult> CompleteAsync(LlmRequest request, CancellationToken ct = default)
  244: public long BytesRead { get; } = bytesRead;
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Models/ReplayLlmProvider.cs
```cs
   10: public sealed record LlmReplayEntry
   12: public required string SchemaVersion { get; init; }
   13: public required int Sequence { get; init; }
   14: public required string ProviderId { get; init; }
   15: public required string ModelId { get; init; }
   16: public required string RequestSha256 { get; init; }
   17: public LlmCompletion? Completion { get; init; }
   18: public LlmFailure? Failure { get; init; }
   25: public sealed class ReplayLlmProvider : ILlmProvider, IResumableLlmProvider
   27: public const string ReplaySchemaVersion = "llm-replay/v1";
   28: public const int MaxReplayFileBytes = 100_000_000;
   29: public const int MaxReplayEntries = 1_000;
   30: public const int MaxReplayLineCharacters = 2_000_000;
   38: public ReplayLlmProvider(string replayPath)
   43: public ReplayLlmProvider(LlmProviderDescriptor descriptor, string replayPath)
   48: public ReplayLlmProvider(LlmProviderDescriptor descriptor, ReadOnlyMemory<byte> replayBytes)
   53: public ReplayLlmProvider(LlmProviderDescriptor descriptor, IEnumerable<LlmReplayEntry> entries)
   72: public LlmProviderDescriptor Descriptor { get; }
   74: public int RemainingEntries
   79: public void ResumeAfter(int consumedRequestCount)
   93: public Task<LlmCallResult> CompleteAsync(LlmRequest request, CancellationToken ct = default)
  128: public static string ComputeRequestSha256(LlmProviderDescriptor descriptor, LlmRequest request)
  146: public static string SerializeEntry(LlmReplayEntry entry)
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/CoordinatorInvocationStillActiveException.cs
```cs
    3: public sealed class CoordinatorInvocationStillActiveException(string message)
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/CoordinatorPromptCatalog.cs
```cs
    6: public static class CoordinatorPromptCatalog
    8: public const string BuilderFormatVersion = "role-prompt-builder/v1";
    9: public const string PromptNewLine = "\n";
   10: public const string SourceElementName = "source";
   11: public const string SourceCloseTag = "</source>";
   12: public const string PriorOutputSerializationVersion = "coordinator-json/v1";
   13: public const string ResearchObjectiveHeading = "RESEARCH OBJECTIVE";
   14: public const string SourcesHeading = "UNTRUSTED REFERENCE SOURCES";
   15: public const string SourceSafetyInstruction =
   17: public const string PriorOutputsHeading = "PRIOR VERIFIED ROLE OUTPUTS";
   18: public const string CurrentRolePrefix = "CURRENT ROLE:";
   19: public const string ReturnInstruction =
   21: public const string EvidenceInstruction =
   23: public const string RiskDecisionInstruction =
   25: public const string OutputContract =
   28: public static string SystemInstruction(CoordinatorRole role) => $"""
   35: public static string RoleInstruction(CoordinatorRole role) => role switch
   45: public static string Sha256 { get; } = ComputeSha256();
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/CoordinatorPromptRenderer.cs
```cs
    8: public sealed record CoordinatorPromptSource(string Id, string Title, string Content);
   10: public static class CoordinatorPromptRenderer
   12: public static string BuildUserPrompt(
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/CoordinatorValidation.cs
```cs
    8: public sealed class CoordinatorValidationException(string message) : Exception(message);
   10: public static class CoordinatorValidation
   12: public const int MaxObjectiveCharacters = 20_000;
   13: public const int MaxSourceCount = 64;
   14: public const int MaxSourceContentCharacters = 500_000;
   15: public const int MaxAggregateSourceCharacters = 2_000_000;
   16: public const int MaxSourceUriCharacters = 2_048;
   17: public const int MaxSourceLicenseCharacters = 200;
   19: public static void ValidateSpec(CoordinatorRunSpec spec)
  147: public static CoordinatorRoleOutput ParseRoleOutput(
  250: public static void ValidateBudget(CoordinatorBudget budget)
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/ResearchCoordinator.cs
```cs
   11: public sealed class ResearchCoordinator(
   16: public static IReadOnlyList<CoordinatorRole> Workflow { get; } =
   27: public async Task<CoordinatorRunSnapshot> CreateAsync(
   51: public async Task<CoordinatorRunSnapshot> ApproveStartAsync(
   89: public async Task<CoordinatorRunSnapshot> ApproveReleaseAsync(
  135: public Task<CoordinatorRunSnapshot> ResumeAsync(
  140: public async Task<CoordinatorRunSnapshot> ResumeAsync(
  472: public async Task<CoordinatorRunSnapshot> CancelAsync(
  479: public async Task<CoordinatorRunSnapshot> RejectAsync(
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Orchestration/RolePromptBuilder.cs
```cs
    6: public sealed record CoordinatorPrompt(string SystemPrompt, string UserPrompt);
    8: public sealed class RolePromptBuilder(ICoordinatorArtifactStore artifactStore)
   10: public async Task<CoordinatorPrompt> BuildAsync(
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Persistence/ContentAddressedArtifactStore.cs
```cs
    8: public sealed class ContentAddressedArtifactStore : ICoordinatorArtifactStore
   10: public ContentAddressedArtifactStore(string rootPath)
   16: public string RootPath { get; }
   18: public async Task<StoredArtifact> PutJsonAsync<T>(T value, CancellationToken cancellationToken = default)
   65: public async Task<T> ReadJsonAsync<T>(
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Persistence/CoordinatorPersistence.cs
```cs
    5: public interface ICoordinatorStore
    7:     Task InitializeAsync(CancellationToken cancellationToken = default);
    9:     Task<CoordinatorRunSnapshot> CreateAsync(
   10:     CoordinatorRunSnapshot snapshot,
   11:     string eventType,
   12:     object eventPayload,
   13:     CancellationToken cancellationToken = default);
   15:     Task<CoordinatorRunSnapshot?> GetAsync(Guid runId, CancellationToken cancellationToken = default);
   17:     Task<IReadOnlyList<CoordinatorRunSnapshot>> ListAsync(CancellationToken cancellationToken = default);
   19:     Task<CoordinatorRunSnapshot> AppendAsync(
   20:     CoordinatorRunSnapshot snapshot,
   21:     long expectedVersion,
   22:     string eventType,
   23:     object eventPayload,
   24:     CancellationToken cancellationToken = default);
   26:     Task<IReadOnlyList<CoordinatorEventRecord>> ReadEventsAsync(
   27:     Guid runId,
   28:     CancellationToken cancellationToken = default);
   30:     Task<CoordinatorRunSnapshot> GetVerifiedAsync(
   31:     Guid runId,
   32:     CancellationToken cancellationToken = default);
   34:     Task VerifyEventChainAsync(Guid runId, CancellationToken cancellationToken = default);
   37: public interface ICoordinatorArtifactStore
   39:     string RootPath { get; }
   41:     Task<StoredArtifact> PutJsonAsync<T>(T value, CancellationToken cancellationToken = default);
   43:     Task<T> ReadJsonAsync<T>(string relativePath, string expectedSha256, CancellationToken cancellationToken = default);
   46: public sealed class CoordinatorConcurrencyException(string message) : InvalidOperationException(message);
   48: public sealed class CoordinatorIntegrityException(string message) : Exception(message);
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Persistence/SqliteCoordinatorStore.cs
```cs
   10: public sealed class SqliteCoordinatorStore : ICoordinatorStore
   38: public SqliteCoordinatorStore(string databasePath)
   51: public string DatabasePath { get; }
   53: public async Task InitializeAsync(CancellationToken cancellationToken = default)
   62: public async Task<CoordinatorRunSnapshot> CreateAsync(
  104: public async Task<CoordinatorRunSnapshot?> GetAsync(Guid runId, CancellationToken cancellationToken = default)
  114: public async Task<IReadOnlyList<CoordinatorRunSnapshot>> ListAsync(CancellationToken cancellationToken = default)
  129: public async Task<CoordinatorRunSnapshot> AppendAsync(
  212: public async Task<IReadOnlyList<CoordinatorEventRecord>> ReadEventsAsync(
  250: public async Task VerifyEventChainAsync(Guid runId, CancellationToken cancellationToken = default) =>
  253: public async Task<CoordinatorRunSnapshot> GetVerifiedAsync(
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Retrieval/ExpertContextContracts.cs
```cs
    5: public sealed record ExpertContextManifest
    7: public required string SchemaVersion { get; init; }
    8: public required string CorpusId { get; init; }
    9: public required string CorpusVersion { get; init; }
   10: public required string NormalizerVersion { get; init; }
   11: public required string ChunkerVersion { get; init; }
   12: public required string TokenizerVersion { get; init; }
   13: public required string ScorerVersion { get; init; }
   14: public required DateTimeOffset CreatedAtUtc { get; init; }
   15: public required DateTimeOffset CutoffUtc { get; init; }
   16: public IReadOnlyList<ExpertContextManifestSource> Sources { get; init; } = [];
   19: public sealed record ExpertContextManifestSource
   21: public required string Id { get; init; }
   22: public required string RelativePath { get; init; }
   23: public required string Title { get; init; }
   24: public required string Provenance { get; init; }
   25: public required string License { get; init; }
   26: public IReadOnlyList<ExpertDomain> Domains { get; init; } = [];
   27: public required DatasetRights Rights { get; init; }
   28: public required DateTimeOffset AvailableAtUtc { get; init; }
   29: public required string RawSha256 { get; init; }
   32: public sealed record ExpertContextPack
   34: public required string SchemaVersion { get; init; }
   35: public required string CorpusId { get; init; }
   36: public required string CorpusVersion { get; init; }
   37: public required string NormalizerVersion { get; init; }
   38: public required string ChunkerVersion { get; init; }
   39: public required string TokenizerVersion { get; init; }
   40: public required string ScorerVersion { get; init; }
   41: public required DateTimeOffset CreatedAtUtc { get; init; }
   42: public required DateTimeOffset CutoffUtc { get; init; }
   43: public required string ManifestSha256 { get; init; }
   44: public required string IndexSha256 { get; init; }
   45: public required string PackSha256 { get; init; }
   46: public IReadOnlyList<ExpertContextPackSource> Sources { get; init; } = [];
   47: public IReadOnlyList<ExpertContextChunk> Chunks { get; init; } = [];
   50: public sealed record ExpertContextPackSource
   52: public required string Id { get; init; }
   53: public required string RelativePath { get; init; }
   54: public required string Title { get; init; }
   55: public required string Provenance { get; init; }
   56: public required string License { get; init; }
   57: public IReadOnlyList<ExpertDomain> Domains { get; init; } = [];
   58: public required DatasetRights Rights { get; init; }
   59: public required DateTimeOffset AvailableAtUtc { get; init; }
   60: public required string RawSha256 { get; init; }
   61: public required string ContentSha256 { get; init; }
   62: public required int RawByteCount { get; init; }
   63: public required int LineCount { get; init; }
   66: public sealed record ExpertContextChunk
   68: public required string Id { get; init; }
   69: public required string SourceId { get; init; }
   70: public required int Ordinal { get; init; }
   71: public required int StartLine { get; init; }
   72: public required int EndLine { get; init; }
   73: public required string Content { get; init; }
   74: public required string ContentSha256 { get; init; }
   75: public IReadOnlyList<ExpertDomain> Domains { get; init; } = [];
   78: public sealed record ExpertContextQueryPolicy
   80: public required string SchemaVersion { get; init; }
   81: public int TopK { get; init; } = 12;
   82: public int MaxCharacters { get; init; } = 40_000;
   83: public int MaxPerSource { get; init; } = 4;
   86: public sealed record ExpertContextActivation
   88: public required string TenantId { get; init; }
   89: public required string CorpusId { get; init; }
   90: public required string CorpusVersion { get; init; }
   91: public required string PackSha256 { get; init; }
   94: public sealed record ExpertContextResolutionRequest
   96: public required string TenantId { get; init; }
   97: public required string CorpusId { get; init; }
   98: public required string CorpusVersion { get; init; }
   99: public required string PackSha256 { get; init; }
  100: public required string Query { get; init; }
  101: public required ExpertContextQueryPolicy Policy { get; init; }
  104: public sealed record ExpertContextSelection
  106: public required string SchemaVersion { get; init; }
  107: public required string CorpusId { get; init; }
  108: public required string CorpusVersion { get; init; }
  109: public required string PackSha256 { get; init; }
  110: public required string IndexSha256 { get; init; }
  111: public required string TenantScopeSha256 { get; init; }
  112: public required string QuerySha256 { get; init; }
  113: public required string PolicySha256 { get; init; }
  114: public required string SelectionSha256 { get; init; }
  115: public IReadOnlyList<ExpertContextHit> Hits { get; init; } = [];
  118: public sealed record ExpertContextHit
  120: public required int Rank { get; init; }
  121: public required string CitationId { get; init; }
  122: public required string ChunkId { get; init; }
  123: public required string SourceId { get; init; }
  124: public required string Title { get; init; }
  125: public required int StartLine { get; init; }
  126: public required int EndLine { get; init; }
  127: public required int Score { get; init; }
  128: public required string Content { get; init; }
  129: public required string ContentSha256 { get; init; }
  130: public required string License { get; init; }
  133: public interface IExpertContextPackResolver
  135:     ExpertContextSelection Resolve(ExpertContextResolutionRequest request);
```

## src/linux/AI/TradingTerminal.Ai.Coordinator/Retrieval/ExpertContextPackTools.cs
```cs
   11: public static class ExpertContextPackTools
   13: public const string ManifestSchemaVersion = "daxalgo-expert-context-manifest/v1";
   14: public const string PackSchemaVersion = "daxalgo-expert-context-pack/v1";
   15: public const string QueryPolicySchemaVersion = "daxalgo-expert-context-query-policy/v1";
   16: public const string SelectionSchemaVersion = "daxalgo-expert-context-selection/v1";
   17: public const string IndexSchemaVersion = "daxalgo-expert-context-lexical-index/v1";
   18: public const string NormalizerVersion = "utf8-lf-nfc/v1";
   19: public const string ChunkerVersion = "line-window-4000-80-overlap6/v1";
   20: public const string TokenizerVersion = "code-camel-alnum/v1";
   21: public const string ScorerVersion = "integer-exact-title-domain/v1";
   22: public const string ReservedSourcePrefix = "daxctx.";
   24: public const int MaxSourceCount = 2_048;
   25: public const int MaxSourceBytes = 5_000_000;
   26: public const long MaxAggregateSourceBytes = 100_000_000;
   27: public const int MaxChunkCount = 100_000;
   28: public const int MaxChunkCharacters = 4_000;
   29: public const int MaxChunkLines = 80;
   30: public const int ChunkOverlapLines = 6;
   31: public const int MaxQueryCharacters = 100_000;
   32: public const int MaxSelectionCount = 32;
   33: public const int MaxSelectionCharacters = 100_000;
   34: public const int MaxPackBytes = 200_000_000;
   43: public static async Task<ExpertContextPack> BuildAsync(
  123: public static ExpertContextPack Load(string path, string expectedPackSha256)
  132: public static PreparedExpertContextPack LoadPrepared(
  220: public static void ValidateManifest(ExpertContextManifest manifest)
  282: public static void ValidatePack(ExpertContextPack pack) =>
  463: public static string ComputePackSha256(ExpertContextPack pack)
  469: public static void ValidateSelection(ExpertContextPack pack, ExpertContextSelection selection)
  556: public static IReadOnlyList<CoordinatorContextSource> MaterializeExecutionSources(
  656: public static void ValidateQueryPolicy(ExpertContextQueryPolicy policy)
 1284: public sealed class PreparedExpertContextPack
 1289: public PreparedExpertContextPack(ExpertContextPack pack)
 1296: public string CorpusId => pack.CorpusId;
 1298: public string CorpusVersion => pack.CorpusVersion;
 1300: public string PackSha256 => pack.PackSha256;
 1302: public PinnedExpertContextPackResolver Activate(ExpertContextActivation activation) =>
 1305: public IReadOnlyList<CoordinatorContextSource> MaterializeExecutionSources(
 1315: public sealed class PinnedExpertContextPackResolver : IExpertContextPackResolver
 1321: public PinnedExpertContextPackResolver(
 1371: public ExpertContextSelection Resolve(ExpertContextResolutionRequest request)
```
