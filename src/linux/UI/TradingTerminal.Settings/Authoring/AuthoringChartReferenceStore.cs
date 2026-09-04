using System.Security.Cryptography;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.App.Authoring;

/// <summary>
/// Durable host-owned copy of a chart supplied to Hyperion. Generated specifications carry the
/// stable reference id and content hash; they never depend on the user's original file continuing
/// to exist at the same path.
/// </summary>
public sealed record AuthoringChartReferenceSnapshot(
    AuthoredChartReferenceV1 Reference,
    string ArtifactPath,
    string OriginalFileName,
    long ByteLength);

public interface IAuthoringChartReferenceRepository
{
    Task<AuthoringChartReferenceSnapshot> ImportAsync(
        string sourcePath,
        ChartReferenceSimilarityV1 similarity,
        CancellationToken cancellationToken = default);

    bool Exists(AuthoringChartReferenceSnapshot snapshot);
}

public sealed class FileAuthoringChartReferenceRepository : IAuthoringChartReferenceRepository
{
    private const long MaxReferenceBytes = 25L * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, string> MediaTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [".png"] = "image/png",
            [".jpg"] = "image/jpeg",
            [".jpeg"] = "image/jpeg",
            [".webp"] = "image/webp",
            [".pdf"] = "application/pdf",
            [".json"] = "application/vnd.daxalgo.chart+json",
        };

    private readonly string _directory;

    public FileAuthoringChartReferenceRepository(string? directory = null) =>
        _directory = directory ?? Path.Combine(AuthoringSessionStore.Directory, "references");

    public async Task<AuthoringChartReferenceSnapshot> ImportAsync(
        string sourcePath,
        ChartReferenceSimilarityV1 similarity,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("A reference-chart path is required.", nameof(sourcePath));
        if (!Enum.IsDefined(similarity))
            throw new ArgumentOutOfRangeException(nameof(similarity));

        var fullPath = Path.GetFullPath(sourcePath);
        var extension = Path.GetExtension(fullPath);
        if (!MediaTypes.TryGetValue(extension, out var mediaType))
            throw new InvalidOperationException(
                "Reference charts must be PNG, JPEG, WebP, PDF, or DaxAlgo chart JSON.");

        await using var source = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (source.Length <= 0 || source.Length > MaxReferenceBytes)
            throw new InvalidOperationException(
                $"Reference chart must contain 1 to {MaxReferenceBytes / (1024 * 1024)} MB of data.");

        var hashBytes = await SHA256.HashDataAsync(source, cancellationToken).ConfigureAwait(false);
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        var referenceId = $"chart-ref-{hash[..16]}";
        var artifactPath = Path.Combine(_directory, hash + extension.ToLowerInvariant());

        Directory.CreateDirectory(_directory);
        if (!File.Exists(artifactPath))
        {
            source.Position = 0;
            var temporaryPath = artifactPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await using (var destination = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
                    await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                File.Move(temporaryPath, artifactPath, overwrite: false);
            }
            catch
            {
                try { File.Delete(temporaryPath); } catch (IOException) { }
                throw;
            }
        }

        return new AuthoringChartReferenceSnapshot(
            new AuthoredChartReferenceV1(referenceId, hash, mediaType, [similarity]),
            artifactPath,
            Path.GetFileName(fullPath),
            source.Length);
    }

    public bool Exists(AuthoringChartReferenceSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!File.Exists(snapshot.ArtifactPath)) return false;

        try
        {
            using var stream = File.OpenRead(snapshot.ArtifactPath);
            var actual = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            return string.Equals(actual, snapshot.Reference.ContentHashSha256, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
