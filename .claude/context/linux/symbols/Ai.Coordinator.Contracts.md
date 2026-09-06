# TradingTerminal.Ai.Coordinator.Contracts — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/AI/TradingTerminal.Ai.Coordinator.Contracts/Contracts/CoordinatorContracts.cs
```cs
    5: public static class CoordinatorVersions
    7: public const string Policy = "research-only/v1";
    8: public const string Workflow = "fixed-research/v1";
    9: public const string ArtifactSchema = "coordinator-role-output/v1";
   10: public const string DatasetSchema = "coordinator-dataset/v1";
   11: public const string ExpertModelDatasetSchema = "expert-model-dataset/v1";
   14: public enum CoordinatorRunStatus
   28: public enum CoordinatorRole
   37: public enum CoordinatorDecision
   45: public enum ApprovalGate
   52: public sealed record CoordinatorContextSource(
   60: public sealed record LlmProviderDescriptor(
   70: public sealed record CoordinatorBudget(
   83: public sealed record CoordinatorRunSpec(
   95: public sealed record CoordinatorUsage(
  103: public sealed record CoordinatorApproval(
  110: public sealed record CoordinatorArtifactReference(
  119: public sealed record CoordinatorInvocation(
  136: public sealed record CoordinatorRunSnapshot
  138: public required CoordinatorRunSpec Spec { get; init; }
  139: public required CoordinatorRunStatus Status { get; init; }
  140: public long Version { get; init; }
  141: public int CompletedRoleCount { get; init; }
  142: public CoordinatorUsage Usage { get; init; } = new();
  143: public IReadOnlyList<CoordinatorArtifactReference> Artifacts { get; init; } = [];
  144: public IReadOnlyList<CoordinatorInvocation> Invocations { get; init; } = [];
  145: public IReadOnlyList<CoordinatorApproval> Approvals { get; init; } = [];
  146: public string? FinalArtifactSha256 { get; init; }
  147: public string? SafeMessage { get; init; }
  148: public DateTimeOffset UpdatedAtUtc { get; init; }
  151: public sealed record CoordinatorClaim(
  156: public sealed record CoordinatorRoleOutput
  158: public required string SchemaVersion { get; init; }
  159: public required CoordinatorRole Role { get; init; }
  160: public required string Summary { get; init; }
  161: public IReadOnlyList<CoordinatorClaim> Claims { get; init; } = [];
  162: public IReadOnlyList<string> Risks { get; init; } = [];
  163: public IReadOnlyList<string> Recommendations { get; init; } = [];
  164: public IReadOnlyList<string> SourceIds { get; init; } = [];
  165: public CoordinatorDecision Decision { get; init; }
  168: public sealed record StoredArtifact(
  173: public sealed record CoordinatorEventRecord(
  182: public sealed record LlmMessage(string Role, string Content);
  184: public sealed record LlmRequest(
  193: public sealed record LlmUsage(long InputTokens, long OutputTokens);
  195: public sealed record LlmCompletion(
  202: public sealed record LlmFailure(
  208: public sealed record LlmCallResult(LlmCompletion? Completion, LlmFailure? Failure)
  210: public bool IsSuccess => Completion is not null && Failure is null;
  212: public static LlmCallResult Success(LlmCompletion completion) => new(completion, null);
  214: public static LlmCallResult Failed(
```

## src/linux/AI/TradingTerminal.Ai.Coordinator.Contracts/Security/ContentHasher.cs
```cs
    8: public static class ContentHasher
   10: public static string HashUtf8(string value) => HashBytes(Encoding.UTF8.GetBytes(value));
   12: public static string HashBytes(ReadOnlySpan<byte> value) =>
   15: public static string HashJson<T>(T value) =>
```

## src/linux/AI/TradingTerminal.Ai.Coordinator.Contracts/Serialization/CoordinatorJson.cs
```cs
    6: public static class CoordinatorJson
    8: public static JsonSerializerOptions Options { get; } = CreateOptions();
```
