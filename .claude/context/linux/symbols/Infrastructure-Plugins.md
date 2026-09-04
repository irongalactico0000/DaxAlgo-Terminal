# TradingTerminal.Infrastructure / Plugins — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/AuthenticodeSignatureInspector.cs
```cs
   16: public sealed class AuthenticodeSignatureInspector : IPluginSignatureInspector
   18: public PluginSignature Inspect(string assemblyPath)
  104: public uint cbStruct;
  106: public IntPtr hFile;
  107: public IntPtr pgKnownSubject;
  113: public uint cbStruct;
  114: public IntPtr pPolicyCallbackData;
  115: public IntPtr pSIPClientData;
  116: public uint dwUIChoice;
  117: public uint fdwRevocationChecks;
  118: public uint dwUnionChoice;
  119: public IntPtr pFile;
  120: public uint dwStateAction;
  121: public IntPtr hWVTStateData;
  122: public IntPtr pwszURLReference;
  123: public uint dwProvFlags;
  124: public uint dwUIContext;
  125: public IntPtr pSignatureSettings;
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/DaxPluginPackage.cs
```cs
   15: public static class DaxPluginPackage
   17: public const string Extension = ".daxplugin";
   18: public const string IndexEntryName = "package.json";
   82: public static void Write(string pluginDirectory, string mainAssemblyFileName, string outputPath)
  130: public static (string ExtractedDir, string MainAssemblyName) ExtractAndVerify(string packagePath) =>
  666: public void Add(ValidatedPath path)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/DaxqPackageDetector.cs
```cs
   22: public static bool HasPackageExtension(string path) =>
   28: public static DaxqPackageMetadata? TryRead(string path)
  109: public ProtectedStrategyManifestException(string packagePath, string reason, Exception? inner = null)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/Feed/FeedSignatureVerifier.cs
```cs
    8: public enum FeedVerifyOutcome
   18: public sealed record FeedVerifyResult(FeedVerifyOutcome Outcome, PluginIndex? Index, string? Detail)
   20: public bool Success => Outcome == FeedVerifyOutcome.Ok && Index is not null;
   31: public sealed class FeedSignatureVerifier
   43: public FeedSignatureVerifier(string pinnedPublicKeyBase64) => _pinnedPublicKeyBase64 = pinnedPublicKeyBase64;
   45: public bool IsConfigured => !string.IsNullOrWhiteSpace(_pinnedPublicKeyBase64);
   50: public FeedVerifyResult Verify(byte[] indexBytes, byte[] signatureBytes)
  101: public FeedVerifyResult Verify(byte[] indexBytes, string signatureBase64)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/Feed/FeedSigner.cs
```cs
   18: public static class FeedSigner
   24: public sealed record FeedKeyPair(string PrivateKeyBase64, string PublicKeyBase64);
   27: public static FeedKeyPair GenerateKeyPair()
   38: public static string Sign(byte[] indexBytes, string privateKeyBase64)
   48: public static string SignIndexFile(string indexPath, string privateKeyBase64)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/Feed/PluginCatalog.cs
```cs
    6: public enum PluginInstallState
   23: public sealed record PluginCatalogItem(
   30: public string Id => Entry.Id;
   31: public string Name => Entry.Name;
   32: public string Publisher => Entry.Publisher;
   33: public string Description => Entry.Description;
   34: public string LatestVersion => Entry.Latest.Version;
   35: public PluginFeedVersion Latest => Entry.Latest;
   36: public IReadOnlyList<string> Tags => Entry.Tags ?? [];
   37: public string? PaperUrl => Entry.PaperUrl;
   40: public bool CanInstall => State == PluginInstallState.NotInstalled && !Revoked;
   43: public bool CanUpdate => State == PluginInstallState.UpdateAvailable && !Revoked;
   46: public string StateLabel => Revoked
   63: public static class PluginCatalog
   67: public static IReadOnlyList<PluginCatalogItem> Build(PluginIndex? index, string pluginsRoot)
   92: public static IReadOnlyList<PluginCatalogItem> Search(IReadOnlyList<PluginCatalogItem> items, string? query)
  103: public static IReadOnlyList<PluginCatalogItem> Updatable(IReadOnlyList<PluginCatalogItem> items) =>
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/Feed/PluginCatalogInstaller.cs
```cs
   16: public static class PluginCatalogInstaller
   21: public const long MaxPackageBytes = 64L * 1024 * 1024;
   27: public static Task<PluginInstallResult> InstallAsync(
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/Feed/PluginFeedClient.cs
```cs
   14: public sealed record FeedRefreshResult(PluginIndex? Index, bool Updated, bool FromCache, string? Detail);
   24: public sealed class PluginFeedClient
   36: public PluginFeedClient(HttpClient http, FeedSignatureVerifier verifier, string feedUrl, string cacheDirectory, ILogger? logger = null)
   46: public PluginIndex? Current { get; private set; }
   49: public bool IsConfigured => !string.IsNullOrWhiteSpace(_feedUrl) && _verifier.IsConfigured;
   53: public async Task<FeedRefreshResult> RefreshAsync(CancellationToken ct = default)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/Feed/PluginFeedRefreshService.cs
```cs
   13: public sealed class PluginFeedRefreshService : IHostedService
   19: public PluginFeedRefreshService(PluginFeedClient feed, PluginHostContext host, ILogger? logger = null)
   26: public Task StartAsync(CancellationToken cancellationToken)
   33: public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/Feed/PluginFeedServiceCollectionExtensions.cs
```cs
   10: public static class PluginFeedServiceCollectionExtensions
   20: public static IServiceCollection AddPluginFeed(this IServiceCollection services, PluginsOptions options)
   40: public const string FeedHttpClientName = "daxalgo-plugin-feed";
   43: public static string FeedCacheDirectory() => Path.Combine(
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/Feed/PluginIndex.cs
```cs
    6: public sealed record PluginFeedVersion(
   28: public sealed record PluginFeedEntry(
   39: public sealed record PluginFeedRevocation(
   52: public sealed record PluginIndex(
   60: public const int SupportedFeedVersion = 1;
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/Feed/PluginRevocationSync.cs
```cs
   10: public static class PluginRevocationSync
   15: public static int Apply(string pluginsRoot, PluginIndex? index)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/FeedAttestedPluginSignatureInspector.cs
```cs
   26: public sealed class FeedAttestedPluginSignatureInspector : IPluginSignatureInspector
   30: public FeedAttestedPluginSignatureInspector(string? pinnedFeedPublicKey) =>
   33: public PluginSignature Inspect(string assemblyPath) =>
   45: public PluginSignature Signature { get; } =
  436: public PluginSignature Inspect(string assemblyPath)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/GuardedServiceCollection.cs
```cs
   36: public sealed class GuardedServiceCollection : IServiceCollection
   41: public static readonly IReadOnlyList<Type> MultiRegistrationAllowlist =
   58: public GuardedServiceCollection(IServiceCollection host, string plugin, IEnumerable<Type>? allowlist = null)
   68: public IReadOnlyList<ServiceDescriptor> Staged => _staged;
   73: public IReadOnlyList<string> Commit()
  104: public int Count => _host.Count + _staged.Count;
  106: public bool IsReadOnly => false;
  108: public ServiceDescriptor this[int index]
  118: public void Add(ServiceDescriptor item) => _staged.Add(Validate(item));
  120: public void Insert(int index, ServiceDescriptor item)
  127: public void Clear() => throw HostMutation("clear", typeof(IServiceCollection));
  129: public bool Remove(ServiceDescriptor item)
  136: public void RemoveAt(int index)
  142: public bool Contains(ServiceDescriptor item) => _host.Contains(item) || _staged.Contains(item);
  144: public int IndexOf(ServiceDescriptor item)
  152: public void CopyTo(ServiceDescriptor[] array, int arrayIndex)
  158: public IEnumerator<ServiceDescriptor> GetEnumerator() => _host.Concat(_staged).GetEnumerator();
  166: public sealed class PluginPolicyViolationException(string pluginName, Type serviceType, string reason)
  169: public string PluginName { get; } = pluginName;
  170: public Type ServiceType { get; } = serviceType;
  171: public string Reason { get; } = reason;
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/IProtectedStrategyEngine.cs
```cs
   11: public interface IProtectedStrategyEngine
   14:     IReadOnlyList<ProtectedStrategyRegistration> LoadStrategies(string daxqPath);
   22: public sealed record ProtectedStrategyRegistration(
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginConsent.cs
```cs
    6: public sealed record PluginConsentRequest(
   25: public interface IPluginConsentPrompt
   28:     bool RequestConsent(PluginConsentRequest request);
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginInstaller.cs
```cs
   14: public sealed record PluginHostContext(
   28: public IReadOnlyList<LoadedPlugin> AuthoredThisSession
   35: public void AddAuthored(LoadedPlugin plugin)
   49: public IReadOnlyList<LoadedPlugin> RuntimeInstalledThisSession
   55: public void AddRuntimeInstalled(LoadedPlugin plugin)
   70: public IReadOnlySet<string> UnsignedStrategyTypeNames { get; } =
   78: public sealed record PluginInstallResult(bool Success, string Message, string? InstalledPath = null);
   92: public static class PluginInstaller
  100: public static PluginInstallResult InstallFromDll(
  131: public static PluginInstallResult InstallFromPackage(
  171: public static PluginInstallResult Uninstall(
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginIntegrity.cs
```cs
    9: public enum PluginPinResult
   25: public static class PluginIntegrity
   29: public static string Sha256(string path)
   44: public sealed record TrustedPlugin(
   63: public sealed class PluginTrustedHashes
   65: public const string FileName = "plugins-trusted.json";
   75: public static PluginTrustedHashes Empty { get; } = new([]);
   77: public bool IsEmpty => _pinned.Count == 0;
   81: public static PluginTrustedHashes Load(string pluginsRoot)
  108: public PluginPinResult Verify(string pluginFolderName, string pluginDirectory, out string? detail)
  145: public PluginPinResult VerifyArtifact(string pluginName, string artifactPath, out string? detail)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginLoadContext.cs
```cs
   21: public PluginLoadContext(string pluginMainAssemblyPath)
   28: protected override Assembly? Load(AssemblyName assemblyName)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginLoadReport.cs
```cs
    4: public enum PluginLoadOutcome
   54: public sealed record PluginLoadProblem(
   65: public sealed record PluginLoadReport(
   69: public static PluginLoadReport Empty { get; } = new([], []);
   73: public int AttentionCount => Problems.Count(p => p.Outcome is not PluginLoadOutcome.Disabled);
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginLoader.cs
```cs
   14: public sealed record LoadedPlugin(
   44: public static class PluginLoader
   60: public static IReadOnlyList<LoadedPlugin> LoadInto(
   74: public static IReadOnlyList<LoadedPlugin> LoadInto(
   85: public static PluginLoadReport LoadWithReport(
   98: public static PluginLoadReport LoadWithReport(
  117: public static PluginLoadReport LoadWithReport(
  534: public static LoadedPlugin? RegisterFromAssembly(Assembly assembly, IServiceCollection services, string hostSdkVersion)
  593: public static bool IsCompatible(string pluginVersion, string hostVersion)
  626: public IServiceCollection Services { get; } = services;
  627: public PluginContext Context { get; } = context;
  631: public sealed class PluginIncompatibleException(string pluginName, string pluginVersion, string hostVersion)
  634: public string PluginName { get; } = pluginName;
  635: public string PluginVersion { get; } = pluginVersion;
  636: public string HostVersion { get; } = hostVersion;
  641: public sealed class PluginRejectedException(string assemblyPath, string reason)
  644: public string AssemblyPath { get; } = assemblyPath;
  645: public string Reason { get; } = reason;
  651: public sealed class PluginBlockedException(string assemblyPath, PluginScanReport scan)
  654: public string AssemblyPath { get; } = assemblyPath;
  655: public PluginScanReport Scan { get; } = scan;
  656: public string Reason { get; } = scan.Summary;
  662: public sealed class PluginTamperedException(string assemblyPath, string reason)
  665: public string AssemblyPath { get; } = assemblyPath;
  666: public string Reason { get; } = reason;
  671: public sealed class PluginRevokedException(string assemblyPath, string reason)
  674: public string AssemblyPath { get; } = assemblyPath;
  675: public string Reason { get; } = reason;
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginManifest.cs
```cs
   18: public sealed record PluginManifest(
   26: public const string FileName = "plugin.json";
   37: public static PluginManifest? TryRead(string pluginDirectory)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginPolicyScanner.cs
```cs
   10: public enum PluginScanSeverity
   26: public sealed record PluginScanFinding(
   33: public sealed record PluginScanReport(PluginScanSeverity Verdict, IReadOnlyList<PluginScanFinding> Findings)
   35: public static PluginScanReport Clean { get; } = new(PluginScanSeverity.Clean, []);
   38: public string Summary =>
   68: public static class PluginPolicyScanner
  109: public static PluginScanReport Scan(string pluginDirectory, IEnumerable<string>? declaredPermissions = null)
  129: public static PluginScanReport ScanImage(byte[] assemblyImage, string name, IEnumerable<string>? declaredPermissions = null)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginRevocationList.cs
```cs
   10: public sealed record RevokedPlugin(
   22: public sealed class PluginRevocationList
   24: public const string FileName = "revoked.json";
   33: public static PluginRevocationList Empty { get; } = new([]);
   35: public bool IsEmpty => _revoked.Count == 0;
   37: public static PluginRevocationList Load(string pluginsRoot)
   55: public bool IsRevoked(string sha256, string? pluginId, out string? reason)
   84: public static int Merge(string pluginsRoot, IEnumerable<RevokedPlugin> additional)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginSignature.cs
```cs
   12: public sealed record PluginSignature(bool IsSigned, bool IsValid, string? Thumbprint, string? Subject)
   15: public static PluginSignature Unsigned { get; } = new(false, false, null, null);
   24: public interface IPluginSignatureInspector
   26:     PluginSignature Inspect(string assemblyPath);
   31: public sealed class NullSignatureInspector : IPluginSignatureInspector
   33: public PluginSignature Inspect(string assemblyPath) => PluginSignature.Unsigned;
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginStateStore.cs
```cs
    9: public sealed record PluginInstallRecord(
   15: public sealed record PluginConsentRecord(
   21: public sealed record PluginQuarantine(
   40: public sealed class PluginStateStore
   42: public const string FileName = "plugins-state.json";
   54: public PluginStateStore(string pluginsRoot)
   62: public string? LoadError { get; }
   64: public IReadOnlyList<string> Disabled { get { lock (_gate) return [.. _state.Disabled]; } }
   65: public IReadOnlyList<PluginQuarantine> Quarantined { get { lock (_gate) return [.. _state.Quarantined]; } }
   66: public IReadOnlyList<string> PendingUninstalls { get { lock (_gate) return [.. _state.PendingUninstall]; } }
   68: public bool IsDisabled(string plugin)
   73: public void SetDisabled(string plugin, bool disabled)
   83: public PluginQuarantine? QuarantineFor(string plugin)
   90: public void Quarantine(string plugin, string reason)
  100: public bool ClearQuarantine(string plugin)
  114: public bool HasConsent(string plugin, string sha256)
  122: public void GrantConsent(string plugin, string sha256)
  133: public void ClearConsent(string plugin)
  150: public string? InstalledHash(string plugin)
  157: public void SetInstalledHash(string plugin, string sha256)
  167: public void ClearInstalledHash(string plugin)
  176: public void MarkPendingUninstall(string plugin)
  188: public bool ClearPendingUninstall(string plugin)
```

## src/linux/Pipeline/TradingTerminal.Infrastructure/Plugins/PluginTrustPolicy.cs
```cs
   15: public sealed record PluginTrustPolicy(
   23: public static PluginTrustPolicy Permissive { get; } =
   28: public static PluginTrustPolicy Curated(
   38: public static PluginTrustPolicy From(PluginsOptions options) => options.TrustPolicy switch
   46: public bool Allows(PluginSignature signature, bool hasManifest, out string? reason)
```
