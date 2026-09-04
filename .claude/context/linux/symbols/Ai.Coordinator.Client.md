# TradingTerminal.Ai.Coordinator.Client — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/AI/TradingTerminal.Ai.Coordinator.Client/Client/VibeQuantApiClient.cs
```cs
   10: public interface IVibeQuantApiClient
   12:     Task<VibeQuantCreditBalanceResponse> GetCreditsAsync(CancellationToken cancellationToken = default);
   14:     Task<VibeQuantRunSpecResponse> CreateRunAsync(
   15:     CreateVibeQuantRunRequest request,
   16:     string idempotencyKey,
   17:     CancellationToken cancellationToken = default);
   19:     Task<VibeQuantRunSpecResponse> GetSpecificationAsync(
   20:     Guid runId,
   21:     CancellationToken cancellationToken = default);
   23:     Task<VibeQuantRunStatusResponse> StartAsync(
   24:     Guid runId,
   25:     string specSha256,
   26:     CancellationToken cancellationToken = default);
   28:     Task<VibeQuantRunStatusResponse> GetStatusAsync(
   29:     Guid runId,
   30:     CancellationToken cancellationToken = default);
   32:     Task<IReadOnlyList<VibeQuantRunStatusResponse>> ListAsync(
   33:     CancellationToken cancellationToken = default);
   35:     Task<VibeQuantArtifactResponse> GetArtifactAsync(
   36:     Guid runId,
   37:     string artifactSha256,
   38:     CancellationToken cancellationToken = default);
   40:     Task<VibeQuantRunStatusResponse> ReleaseAsync(
   41:     Guid runId,
   42:     string artifactSha256,
   43:     CancellationToken cancellationToken = default);
   45:     Task<VibeQuantRunStatusResponse> CancelAsync(
   46:     Guid runId,
   47:     CancellationToken cancellationToken = default);
   50: public sealed class VibeQuantApiClient(HttpClient httpClient) : IVibeQuantApiClient
   54: public async Task<VibeQuantCreditBalanceResponse> GetCreditsAsync(CancellationToken cancellationToken = default) =>
   62: public async Task<VibeQuantRunSpecResponse> CreateRunAsync(
   89: public async Task<VibeQuantRunSpecResponse> GetSpecificationAsync(
  102: public async Task<VibeQuantRunStatusResponse> StartAsync(
  124: public async Task<VibeQuantRunStatusResponse> GetStatusAsync(
  137: public async Task<IReadOnlyList<VibeQuantRunStatusResponse>> ListAsync(
  146: public async Task<VibeQuantArtifactResponse> GetArtifactAsync(
  161: public async Task<VibeQuantRunStatusResponse> ReleaseAsync(
  182: public async Task<VibeQuantRunStatusResponse> CancelAsync(
  463: public sealed class VibeQuantApiException : Exception
  465: public VibeQuantApiException(HttpStatusCode statusCode, string message, Exception? innerException = null)
  468: public HttpStatusCode StatusCode { get; }
```

## src/linux/AI/TradingTerminal.Ai.Coordinator.Client/Client/VibeQuantApiContracts.cs
```cs
    6: public static class VibeQuantApiVersions
    8: public const string RunSpecification = "vibe-quant-run-spec/v1";
    9: public const int MaximumRequestBytes = 8_000_000;
   10: public const int MaximumResponseBytes = 4_000_000;
   14: public sealed record CreateVibeQuantRunRequest(
   18: public sealed record VibeQuantProviderView(
   23: public sealed record VibeQuantSourceView(
   32: public sealed record VibeQuantBudgetView(
   43: public sealed record VibeQuantRunSpecification(
   58: public sealed record VibeQuantRunSpecResponse(
   63: public sealed record StartVibeQuantRunRequest(string SpecSha256);
   66: public sealed record ReleaseVibeQuantRunRequest(string ArtifactSha256);
   68: public sealed record VibeQuantRunStatusResponse(
   81: public sealed record VibeQuantArtifactResponse(
   87: public sealed record VibeQuantCreditBalanceResponse(
```
