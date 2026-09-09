using System.Security.Cryptography;
using System.Text;
using DaxAlgo.Package;
using FluentAssertions;
using Xunit;

namespace DaxAlgo.PackageTests;

/// <summary>
/// StrategyBuilder → Extensions verify → Marketplace handoff. This is the portable publication
/// boundary Mac must share with public Windows before any Avalonia installer work.
/// </summary>
public sealed class StrategyBuilderMarketplaceBridgeTests : IDisposable
{
    private readonly string _dir = Path.Combine(
        Path.GetTempPath(), "daxpackage-bridge-tests", Guid.NewGuid().ToString("N"));

    public StrategyBuilderMarketplaceBridgeTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp */ }
    }

    private string Path_(string name) => Path.Combine(_dir, name);

    private static string HexSha256(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    [Fact]
    public void Extensions_gate_matches_windows_verify_status_for_open_packages()
    {
        var path = Path_("bridge" + DaxPackage.StrategyExtension);
        var request = StrategyBuilderPackageBridge.CreateStrategySubmission(
            packageId: "sb.meanreversion",
            version: "0.1.0",
            displayName: "SB Mean Reversion",
            entryTypeName: "Sb.MeanReversionStrategy",
            confirmedRunId: "run-001",
            confirmedInputSha256: HexSha256("confirmed-input"),
            reviewSha256: HexSha256("review"),
            confirmedBy: "operator@daxalgo",
            confirmedAt: "2026-08-21T00:00:00Z",
            strategyPayloads:
            [
                DaxPayloadSource.FromBytes(
                    "src/Strategy.cs", DaxPayloadRole.Source, Encoding.UTF8.GetBytes("class S {}")),
            ],
            publisher: "StrategyBuilder");

        DaxPackage.Write(path, request);

        ExtensionsPackageInspection.TryVerify(path, out var status).Should().BeTrue();
        status.Should().Contain("SB Mean Reversion 0.1.0 (strategy) verified");
        status.Should().Contain("Durable install is available");
    }

    [Fact]
    public void Extensions_gate_refuses_legacy_daxplugin_and_raw_dll_by_name()
    {
        ExtensionsPackageInspection.TryVerify(Path_("x.daxplugin"), out var pluginStatus)
            .Should().BeFalse();
        pluginStatus.Should().Contain(".daxplugin format was retired");

        ExtensionsPackageInspection.TryVerify(Path_("x.dll"), out var dllStatus)
            .Should().BeFalse();
        dllStatus.Should().Contain("Raw assemblies are no longer installed");
    }

    [Fact]
    public void StrategyBuilder_confirmed_run_survives_package_verify_into_marketplace_handoff()
    {
        var confirmedInput = HexSha256("confirmed-input-v1");
        var review = HexSha256("review-v1");
        var path = Path_("sb" + DaxPackage.StrategyExtension);

        var request = StrategyBuilderPackageBridge.CreateStrategySubmission(
            packageId: "sb.breakout",
            version: "2.0.0",
            displayName: "Breakout",
            entryTypeName: "Sb.BreakoutStrategy",
            confirmedRunId: "run-42",
            confirmedInputSha256: confirmedInput,
            reviewSha256: review,
            confirmedBy: "builder",
            confirmedAt: "2026-08-21T01:02:03Z",
            strategyPayloads:
            [
                DaxPayloadSource.FromBytes(
                    "src/Breakout.cs", DaxPayloadRole.Source, Encoding.UTF8.GetBytes("class B {}")),
                DaxPayloadSource.FromBytes(
                    "bin/Breakout.dll", DaxPayloadRole.Assembly, [0x4D, 0x5A, 0x00]),
            ],
            description: "From StrategyBuilder confirmed run",
            publisher: "StrategyBuilder");

        var written = DaxPackage.Write(path, request);
        written.Manifest.Payloads.Should().Contain(p =>
            p.Role == DaxPayloadRole.Provenance
            && p.Path.EndsWith("strategybuilder-confirmed-run.v1.json", StringComparison.Ordinal));

        ExtensionsPackageInspection.TryVerify(path, out _).Should().BeTrue();

        var handoff = ExtensionsPackageInspection.CreateMarketplaceHandoff(path);
        handoff.ArtifactExtension.Should().Be(DaxPackage.StrategyExtension);
        handoff.PackageId.Should().Be("sb.breakout");
        handoff.Version.Should().Be("2.0.0");
        handoff.DisplayName.Should().Be("Breakout");
        handoff.Publisher.Should().Be("StrategyBuilder");
        handoff.EntryTypeName.Should().Be("Sb.BreakoutStrategy");
        handoff.Kind.Should().Be(DaxPackageKind.Strategy);
        handoff.PackageLength.Should().BeGreaterThan(0);
        handoff.PackageSha256.Should().HaveLength(64);
        handoff.ManifestSha256.Should().Be(written.ManifestSha256);
        handoff.StrategyBuilderConfirmedInputSha256.Should().Be(confirmedInput);
        handoff.Payloads.Should().HaveCount(3);
    }

    [Fact]
    public void Marketplace_handoff_package_digest_changes_when_strategybuilder_confirmation_changes()
    {
        DaxPayloadSource[] payloads =
        [
            DaxPayloadSource.FromBytes(
                "src/S.cs", DaxPayloadRole.Source, Encoding.UTF8.GetBytes("class S {}")),
        ];

        var aPath = Path_("a" + DaxPackage.StrategyExtension);
        var bPath = Path_("b" + DaxPackage.StrategyExtension);

        DaxPackage.Write(aPath, StrategyBuilderPackageBridge.CreateStrategySubmission(
            "sb.x", "1.0.0", "X", "Sb.X",
            "run-a", HexSha256("input-a"), HexSha256("review-a"),
            "builder", "2026-08-21T00:00:00Z", payloads));

        DaxPackage.Write(bPath, StrategyBuilderPackageBridge.CreateStrategySubmission(
            "sb.x", "1.0.0", "X", "Sb.X",
            "run-b", HexSha256("input-b"), HexSha256("review-b"),
            "builder", "2026-08-21T00:00:00Z", payloads));

        var a = ExtensionsPackageInspection.CreateMarketplaceHandoff(aPath);
        var b = ExtensionsPackageInspection.CreateMarketplaceHandoff(bPath);

        a.PackageId.Should().Be(b.PackageId);
        a.PackageSha256.Should().NotBe(b.PackageSha256);
        a.StrategyBuilderConfirmedInputSha256.Should().NotBe(b.StrategyBuilderConfirmedInputSha256);
    }

    [Fact]
    public void Open_package_durable_install_extracts_and_indexes_by_package_sha()
    {
        var path = Path_("install" + DaxPackage.StrategyExtension);
        var request = StrategyBuilderPackageBridge.CreateStrategySubmission(
            packageId: "sb.installable",
            version: "1.2.3",
            displayName: "Installable",
            entryTypeName: "Sb.InstallableStrategy",
            confirmedRunId: "run-install",
            confirmedInputSha256: HexSha256("install-input"),
            reviewSha256: HexSha256("install-review"),
            confirmedBy: "builder",
            confirmedAt: "2026-09-08T00:00:00Z",
            strategyPayloads:
            [
                DaxPayloadSource.FromBytes(
                    "src/Installable.cs", DaxPayloadRole.Source, Encoding.UTF8.GetBytes("class Installable {}")),
            ],
            publisher: "StrategyBuilder");
        DaxPackage.Write(path, request);

        var root = Path_("open-packages");
        var first = OpenPackageDurableInstaller.Install(path, root);
        first.Success.Should().BeTrue(first.Message);
        first.Handoff.Should().NotBeNull();
        first.InstallDirectory.Should().NotBeNullOrWhiteSpace();
        Directory.Exists(Path.Combine(first.InstallDirectory!, OpenPackageDurableInstaller.ContentDirectoryName))
            .Should().BeTrue();
        File.Exists(Path.Combine(first.InstallDirectory!, OpenPackageDurableInstaller.HandoffFileName))
            .Should().BeTrue();

        var second = OpenPackageDurableInstaller.Install(path, root);
        second.Success.Should().BeTrue(second.Message);
        second.Handoff!.PackageSha256.Should().Be(first.Handoff!.PackageSha256);

        var index = OpenPackageDurableInstaller.ReadIndex(root);
        index.Packages.Should().ContainSingle(item =>
            item.PackageId == "sb.installable" &&
            item.PackageSha256 == first.Handoff.PackageSha256);
    }
}

