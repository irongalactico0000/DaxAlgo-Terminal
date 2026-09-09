using System.Net.Http;
using System.Net.Http.Headers;

namespace TradingTerminal.App.Plugins;

/// <summary>
/// Downloads a public HTTPS open package (.<c>daxalgostrategy</c> / .<c>daxalgovisualizer</c>)
/// into a temp file for <see cref="DaxAlgo.Package.OpenPackageDurableInstaller"/>.
/// Marketplace release APIs that require platform-service auth stay out of scope here.
/// </summary>
public static class OpenPackageRemoteInstaller
{
    public const long MaxPackageBytes = 64L * 1024 * 1024;
    private const int CopyBufferBytes = 81920;

    public sealed record DownloadResult(bool Success, string Message, string? LocalPath = null);

    public static async Task<DownloadResult> DownloadAsync(
        HttpClient http,
        string packageUrl,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(http);
        if (string.IsNullOrWhiteSpace(packageUrl))
            return new DownloadResult(false, "A package URL is required.");

        if (!Uri.TryCreate(packageUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return new DownloadResult(false, "Open-package download requires an http(s) URL.");
        }

        // Prefer HTTPS; allow plain HTTP only for local loopback smoke/dev.
        if (uri.Scheme == Uri.UriSchemeHttp &&
            !string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase))
        {
            return new DownloadResult(false, "Non-local open-package downloads must use HTTPS.");
        }

        try
        {
            using var response = await http
                .GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return new DownloadResult(false,
                    $"Download returned HTTP {(int)response.StatusCode} for {uri}.");
            }

            if (response.Content.Headers.ContentLength is > MaxPackageBytes)
            {
                return new DownloadResult(false,
                    $"Package is larger than the {MaxPackageBytes / (1024 * 1024)} MB limit.");
            }

            var extension = ResolveExtension(uri, response.Content.Headers.ContentDisposition);
            if (extension is null)
            {
                return new DownloadResult(false,
                    "URL must end with .daxalgostrategy or .daxalgovisualizer " +
                    "(or Content-Disposition must name one).");
            }

            var tempPath = Path.Combine(
                Path.GetTempPath(),
                "daxalgo-open-dl-" + Guid.NewGuid().ToString("N") + extension);

            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken)
                             .ConfigureAwait(false))
            await using (var dest = File.Create(tempPath))
            {
                var buffer = new byte[CopyBufferBytes];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                           .ConfigureAwait(false)) > 0)
                {
                    total += read;
                    if (total > MaxPackageBytes)
                    {
                        try { File.Delete(tempPath); } catch { /* best effort */ }
                        return new DownloadResult(false,
                            $"Package exceeds the {MaxPackageBytes / (1024 * 1024)} MB limit.");
                    }

                    await dest.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                        .ConfigureAwait(false);
                }
            }

            return new DownloadResult(true, "Downloaded open package.", tempPath);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            return new DownloadResult(false, $"Download failed: {exception.Message}");
        }
    }

    private static string? ResolveExtension(Uri uri, ContentDispositionHeaderValue? disposition)
    {
        static string? FromName(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (name.EndsWith(".daxalgostrategy", StringComparison.OrdinalIgnoreCase))
                return ".daxalgostrategy";
            if (name.EndsWith(".daxalgovisualizer", StringComparison.OrdinalIgnoreCase))
                return ".daxalgovisualizer";
            return null;
        }

        return FromName(disposition?.FileNameStar) ??
               FromName(disposition?.FileName?.Trim('"')) ??
               FromName(Path.GetFileName(uri.AbsolutePath));
    }
}
