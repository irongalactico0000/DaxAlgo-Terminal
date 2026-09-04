# DaxAlgo.Package — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Sdk/DaxAlgo.Package/DaxPackage.cs
```cs
   31: public static class DaxPackage
   34: public const string StrategyExtension = ".daxalgostrategy";
   37: public const string VisualizerExtension = ".daxalgovisualizer";
   40: public const string ManifestEntryPath = "package.manifest.json";
   43: public static string ExtensionFor(DaxPackageKind kind) => kind switch
   51: public static DaxPackageKind? KindFor(string path) =>
   60: public static IReadOnlyList<string> AcceptedExtensions { get; } = [StrategyExtension, VisualizerExtension];
   63: public static string OpenFileFilter =>
   74: public static bool IsAccepted(string path, out string reason)
  106: public static DaxPackageWriteResult Write(string outputPath, DaxPackageRequest request,
  125: public static DaxPackageWriteResult Write(Stream output, DaxPackageRequest request,
  202: public static DaxPackageContents Read(string packagePath, DaxPackageLimits? limits = null)
  217: public static DaxPackageContents Read(Stream input, DaxPackageLimits? limits = null)
  290: public static DaxPackageManifest Inspect(string packagePath)
  305: public static DaxPackageManifest Extract(string packagePath, string destinationDirectory,
```

## src/linux/Sdk/DaxAlgo.Package/DaxPackageManifestCodec.cs
```cs
   16: public static byte[] Write(DaxPackageManifest manifest)
   47: public static DaxPackageManifest Read(byte[] utf8)
```

## src/linux/Sdk/DaxAlgo.Package/DaxPackageModels.cs
```cs
    8: public enum DaxPackageKind
   21: public enum DaxPayloadRole
   46: public sealed record DaxPayloadDescriptor(
   53: public sealed class DaxPayloadSource(string path, DaxPayloadRole role, Func<Stream> open)
   55: public string Path { get; } = path;
   56: public DaxPayloadRole Role { get; } = role;
   57: public Func<Stream> Open { get; } = open;
   59: public static DaxPayloadSource FromFile(string packagePath, DaxPayloadRole role, string filePath) =>
   62: public static DaxPayloadSource FromBytes(string packagePath, DaxPayloadRole role, byte[] bytes) =>
   70: public sealed record DaxPackageManifest(
   82: public const string CurrentFormat = "daxpackage";
   83: public const int CurrentFormatVersion = 1;
   87: public sealed record DaxPackageRequest
   89: public required DaxPackageKind Kind { get; init; }
   90: public required string Id { get; init; }
   91: public required string Version { get; init; }
   92: public required string DisplayName { get; init; }
   95: public required string EntryTypeName { get; init; }
   97: public string? Description { get; init; }
   98: public string? Publisher { get; init; }
   99: public IReadOnlyList<DaxPayloadSource> Payloads { get; init; } = [];
  103: public sealed record DaxPackageWriteResult(
  110: public sealed record DaxPackageContents(
  115: public enum DaxPackageError
  129: public sealed class DaxPackageException(DaxPackageError error, string message)
  132: public DaxPackageError Error { get; } = error;
  136: public sealed record DaxPackageLimits
  138: public static DaxPackageLimits Default { get; } = new();
  141: public long MaximumPayloadBytes { get; init; } = 64L * 1024 * 1024;
  144: public long MaximumTotalBytes { get; init; } = 256L * 1024 * 1024;
  146: public int MaximumPayloadCount { get; init; } = 4096;
  148: public int MaximumPathLength { get; init; } = 200;
```

## src/linux/Sdk/DaxAlgo.Package/ExtensionsPackageInspection.cs
```cs
   13: public static class ExtensionsPackageInspection
   19: public static bool TryVerify(string path, out string status)
   46: public static OpenPackageMarketplaceHandoff CreateMarketplaceHandoff(string packagePath)
   92: public sealed record OpenPackageMarketplaceHandoff(
```

## src/linux/Sdk/DaxAlgo.Package/StrategyBuilderPackageBridge.cs
```cs
   14: public static class StrategyBuilderPackageBridge
   16: public const string ProvenanceLogicalPath = "provenance/strategybuilder-confirmed-run.v1.json";
   17: public const string ProvenanceSchema = "daxalgo.strategybuilder-package-provenance/1";
   23: public static DaxPackageRequest CreateStrategySubmission(
   76: public static string? TryReadConfirmedInputSha256(ReadOnlySpan<byte> provenanceBytes)
```
