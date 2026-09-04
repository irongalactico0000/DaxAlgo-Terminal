using System.Security.Cryptography;

namespace DaxAlgo.Package;

/// <summary>
/// Extensions (Strategy Manager) open-package gate shared by every edition.
///
/// <para>Public Windows verifies <c>.daxalgostrategy</c> / <c>.daxalgovisualizer</c> and does not
/// install them yet. Mac Avalonia reuses this exact gate so StrategyBuilder publications and
/// Marketplace submissions agree on acceptance before any installer or sealed <c>.daxq</c> path
/// runs.</para>
/// </summary>
public static class ExtensionsPackageInspection
{
    /// <summary>
    /// Verifies a path the way Extensions does: refuse retired formats by name, then
    /// <see cref="DaxPackage.Read(string, DaxPackageLimits?)"/> so every payload digest is checked. Never loads assemblies.
    /// </summary>
    public static bool TryVerify(string path, out string status)
    {
        if (!DaxPackage.IsAccepted(path, out var reason))
        {
            status = reason;
            return false;
        }

        try
        {
            var manifest = DaxPackage.Read(path).Manifest;
            status =
                $"{manifest.DisplayName} {manifest.Version} ({WireKind(manifest.Kind)}) verified — "
                + $"{manifest.Payloads.Count} payload(s). Installing artifacts is not wired up yet.";
            return true;
        }
        catch (DaxPackageException ex)
        {
            status = $"Rejected: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Projects a verified open package into the Marketplace / native-catalog handoff identity.
    /// Callers must already have verified the file (or this re-verifies via <see cref="DaxPackage.Read(string, DaxPackageLimits?)"/>).
    /// </summary>
    public static OpenPackageMarketplaceHandoff CreateMarketplaceHandoff(string packagePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(packagePath);

        var contents = DaxPackage.Read(packagePath);
        var bytes = File.ReadAllBytes(packagePath);
        var packageSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var manifestSha256 = Convert.ToHexString(
            SHA256.HashData(DaxPackageManifestCodec.Write(contents.Manifest))).ToLowerInvariant();

        string? confirmedInputSha256 = null;
        foreach (var payload in contents.Manifest.Payloads)
        {
            if (payload.Role != DaxPayloadRole.Provenance) continue;
            if (!contents.Payloads.TryGetValue(payload.Path, out var data)) continue;
            confirmedInputSha256 = StrategyBuilderPackageBridge.TryReadConfirmedInputSha256(data)
                ?? confirmedInputSha256;
        }

        return new OpenPackageMarketplaceHandoff(
            ArtifactExtension: DaxPackage.ExtensionFor(contents.Manifest.Kind),
            PackageId: contents.Manifest.Id,
            Version: contents.Manifest.Version,
            DisplayName: contents.Manifest.DisplayName,
            Publisher: contents.Manifest.Publisher,
            EntryTypeName: contents.Manifest.EntryTypeName,
            Kind: contents.Manifest.Kind,
            ManifestSha256: manifestSha256,
            PackageSha256: packageSha256,
            PackageLength: bytes.LongLength,
            Payloads: contents.Manifest.Payloads,
            StrategyBuilderConfirmedInputSha256: confirmedInputSha256);
    }

    internal static string WireKind(DaxPackageKind kind) => kind switch
    {
        DaxPackageKind.Strategy => "strategy",
        DaxPackageKind.Visualizer => "visualizer",
        _ => kind.ToString().ToLowerInvariant(),
    };
}

/// <summary>
/// Identity a Marketplace authoring session or native Extensions catalog can bind without trusting
/// the file name alone. Digests are lowercase hex SHA-256.
/// </summary>
public sealed record OpenPackageMarketplaceHandoff(
    string ArtifactExtension,
    string PackageId,
    string Version,
    string DisplayName,
    string? Publisher,
    string EntryTypeName,
    DaxPackageKind Kind,
    string ManifestSha256,
    string PackageSha256,
    long PackageLength,
    IReadOnlyList<DaxPayloadDescriptor> Payloads,
    string? StrategyBuilderConfirmedInputSha256);
