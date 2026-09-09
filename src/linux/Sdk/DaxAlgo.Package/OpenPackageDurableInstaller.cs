using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace DaxAlgo.Package;

/// <summary>
/// Result of placing a verified open package into the durable install root (not yet a live kernel load).
/// </summary>
public sealed record OpenPackageInstallResult(
    bool Success,
    string Message,
    OpenPackageMarketplaceHandoff? Handoff = null,
    string? InstallDirectory = null);

/// <summary>
/// Durable Marketplace / Extensions return path for <c>.daxalgostrategy</c> /
/// <c>.daxalgovisualizer</c>: verify → handoff identity → extract → index.
/// Assembly load / <c>IStrategyKernelRegistry</c> registration is a separate host step.
/// </summary>
public static class OpenPackageDurableInstaller
{
    public const string IndexFileName = "index.v1.json";
    public const string HandoffFileName = "handoff.v1.json";
    public const string ContentDirectoryName = "content";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Verifies <paramref name="packagePath"/>, extracts payloads under
    /// <paramref name="installRoot"/>, and upserts the package into <see cref="IndexFileName"/>.
    /// Idempotent on the same <c>PackageSha256</c>.
    /// </summary>
    public static OpenPackageInstallResult Install(string packagePath, string installRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);

        if (!ExtensionsPackageInspection.TryVerify(packagePath, out var verifyStatus))
            return new OpenPackageInstallResult(false, verifyStatus);

        OpenPackageMarketplaceHandoff handoff;
        try
        {
            handoff = ExtensionsPackageInspection.CreateMarketplaceHandoff(packagePath);
        }
        catch (Exception exception) when (exception is DaxPackageException or IOException or UnauthorizedAccessException)
        {
            return new OpenPackageInstallResult(false, $"Handoff failed: {exception.Message}");
        }

        var packageDir = PackageDirectory(installRoot, handoff);
        var contentDir = Path.Combine(packageDir, ContentDirectoryName);

        try
        {
            Directory.CreateDirectory(installRoot);
            if (Directory.Exists(contentDir))
                Directory.Delete(contentDir, recursive: true);
            Directory.CreateDirectory(packageDir);

            DaxPackage.Extract(packagePath, contentDir);
            File.WriteAllText(
                Path.Combine(packageDir, HandoffFileName),
                JsonSerializer.Serialize(ToRecord(handoff, packageDir), JsonOptions));

            var index = ReadIndex(installRoot);
            index.Packages = index.Packages
                .Where(item => !string.Equals(item.PackageSha256, handoff.PackageSha256, StringComparison.OrdinalIgnoreCase))
                .Append(ToRecord(handoff, packageDir))
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Version, StringComparer.OrdinalIgnoreCase)
                .ToList();
            WriteIndex(installRoot, index);
        }
        catch (Exception exception) when (exception is DaxPackageException or IOException or UnauthorizedAccessException or JsonException)
        {
            return new OpenPackageInstallResult(false, $"Install failed: {exception.Message}", handoff);
        }

        var kind = ExtensionsPackageInspection.WireKind(handoff.Kind);
        return new OpenPackageInstallResult(
            true,
            $"{handoff.DisplayName} {handoff.Version} ({kind}) installed under open-packages. " +
            "Durable catalog entry recorded; host registry open/run registration is the next step.",
            handoff,
            packageDir);
    }

    public static OpenPackageIndexV1 ReadIndex(string installRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installRoot);
        var path = Path.Combine(installRoot, IndexFileName);
        if (!File.Exists(path))
            return new OpenPackageIndexV1();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<OpenPackageIndexV1>(json, JsonOptions) ?? new OpenPackageIndexV1();
        }
        catch (JsonException)
        {
            return new OpenPackageIndexV1();
        }
    }

    private static void WriteIndex(string installRoot, OpenPackageIndexV1 index)
    {
        Directory.CreateDirectory(installRoot);
        File.WriteAllText(
            Path.Combine(installRoot, IndexFileName),
            JsonSerializer.Serialize(index, JsonOptions));
    }

    private static string PackageDirectory(string installRoot, OpenPackageMarketplaceHandoff handoff)
    {
        var id = SanitizePathSegment(handoff.PackageId);
        var version = SanitizePathSegment(handoff.Version);
        var digest = handoff.PackageSha256.ToLowerInvariant();
        return Path.GetFullPath(Path.Combine(installRoot, id, version, digest));
    }

    private static string SanitizePathSegment(string value)
    {
        var cleaned = Regex.Replace(value.Trim(), @"[^\w\.\-]+", "_", RegexOptions.CultureInvariant);
        return string.IsNullOrWhiteSpace(cleaned) ? "package" : cleaned;
    }

    private static OpenPackageIndexEntryV1 ToRecord(OpenPackageMarketplaceHandoff handoff, string installDirectory) =>
        new(
            handoff.PackageId,
            handoff.Version,
            handoff.DisplayName,
            handoff.Publisher,
            handoff.EntryTypeName,
            handoff.Kind.ToString(),
            handoff.ArtifactExtension,
            handoff.ManifestSha256,
            handoff.PackageSha256,
            handoff.PackageLength,
            handoff.StrategyBuilderConfirmedInputSha256,
            installDirectory,
            DateTimeOffset.UtcNow);
}

public sealed class OpenPackageIndexV1
{
    public int SchemaVersion { get; set; } = 1;
    public List<OpenPackageIndexEntryV1> Packages { get; set; } = [];
}

public sealed record OpenPackageIndexEntryV1(
    string PackageId,
    string Version,
    string DisplayName,
    string? Publisher,
    string EntryTypeName,
    string Kind,
    string ArtifactExtension,
    string ManifestSha256,
    string PackageSha256,
    long PackageLength,
    string? StrategyBuilderConfirmedInputSha256,
    string InstallDirectory,
    DateTimeOffset InstalledAtUtc);
