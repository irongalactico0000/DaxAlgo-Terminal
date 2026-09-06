# DaxAlgo.Daxq.Host — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqCryptography.cs
```cs
    8: public sealed class DaxqEs256PublicKeyRing
   12: public DaxqEs256PublicKeyRing Add(string keyId, ReadOnlySpan<byte> subjectPublicKeyInfo)
   85: public static string Base64Url(ReadOnlySpan<byte> value) => Convert.ToBase64String(value)
   90: public static byte[] DecodeBase64Url(string? value, int? expectedLength = null)
  114: public static string Sha256Hex(ReadOnlySpan<byte> value) =>
  117: public static byte[] BuildDeviceProof(
  143: public static byte[] DeriveContentKeyEncryptionKey(
  162: public static byte[] WrapKey(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> plaintext)
  200: public static byte[] UnwrapKey(ReadOnlySpan<byte> kek, ReadOnlySpan<byte> wrapped)
  241: public static T VerifyEnvelope<T>(DaxqSignedEnvelope envelope, DaxqEs256PublicKeyRing trust)
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqDevelopmentLicensing.cs
```cs
   38: public DaxqDevelopmentLicensing(
   57: public DaxqEs256PublicKeyRing VerificationKeys { get; }
   59: public int ChallengeCalls => Volatile.Read(ref _challengeCalls);
   61: public int KeyReleaseCalls => Volatile.Read(ref _keyReleaseCalls);
   63: public int HeartbeatCalls => Volatile.Read(ref _heartbeatCalls);
   65: public int FeedCalls => Volatile.Read(ref _feedCalls);
   67: public ValueTask<DaxqDeliveryContext> ResolveAsync(
   92: public async ValueTask<DaxqChallengeResponse> CreateChallengeAsync(
  123: public async ValueTask<DaxqContentKeyResponse> ReleaseContentKeyAsync(
  213: public async ValueTask<DaxqHeartbeatResponse> HeartbeatAsync(
  272: public ValueTask<DaxqSignedEnvelope> GetRevocationsAsync(
  303: public void RevokeEntitlement() => Volatile.Write(ref _entitlementActive, 0);
  305: public void SetUnavailable(bool unavailable) => Volatile.Write(ref _unavailable, unavailable ? 1 : 0);
  307: public void ForceRollback(long sequence) => Interlocked.Exchange(ref _rollbackSequence, sequence);
  309: public long PublishRevocation(string targetType, string targetId, string reason)
  324: public void Dispose()
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqDeviceIdentity.cs
```cs
    9: public sealed class DaxqDeviceIdentity : IDisposable
   33: public Guid DeviceId { get; }
   35: public bool NonExportable { get; }
   37: public DaxqDeviceRegistration Registration { get; }
   39: public byte[] Sign(ReadOnlySpan<byte> canonicalProof)
   53: public void Dispose()
   60: public sealed class DaxqDeviceIdentityProvider : IDaxqDeviceIdentityProvider, IDisposable
   76: public DaxqDeviceIdentityProvider(string filePath, bool forceSoftwareKey = false)
   83: public static DaxqDeviceIdentityProvider CreateDefault()
   91: public ValueTask<DaxqDeviceIdentity> GetAsync(CancellationToken cancellationToken)
  102: public void Dispose()
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqLicensingContracts.cs
```cs
    9: public sealed record DaxqDeliveryContext(
   15: public interface IDaxqDeliveryContextResolver
   17:     ValueTask<DaxqDeliveryContext> ResolveAsync(
   18:     string strategyId,
   19:     string version,
   20:     string contentKeyId,
   21:     CancellationToken cancellationToken);
   24: public sealed record DaxqDeviceRegistration(
   29: public interface IDaxqDeviceIdentityProvider
   31:     ValueTask<DaxqDeviceIdentity> GetAsync(CancellationToken cancellationToken);
   34: public interface IDaxqLicensingTransport
   36:     ValueTask<DaxqChallengeResponse> CreateChallengeAsync(
   37:     DaxqDeliveryContext context,
   38:     DaxqChallengeRequest request,
   39:     string idempotencyKey,
   40:     CancellationToken cancellationToken);
   42:     ValueTask<DaxqContentKeyResponse> ReleaseContentKeyAsync(
   43:     DaxqDeliveryContext context,
   44:     Guid licenseId,
   45:     DaxqContentKeyRequest request,
   46:     string idempotencyKey,
   47:     CancellationToken cancellationToken);
   49:     ValueTask<DaxqHeartbeatResponse> HeartbeatAsync(
   50:     DaxqDeliveryContext context,
   51:     Guid licenseId,
   52:     DaxqHeartbeatRequest request,
   53:     string idempotencyKey,
   54:     CancellationToken cancellationToken);
   56:     ValueTask<DaxqSignedEnvelope> GetRevocationsAsync(
   57:     DaxqDeliveryContext context,
   58:     long afterSequence,
   59:     CancellationToken cancellationToken);
   62: public sealed record DaxqChallengeRequest(
   69: public sealed record DaxqChallengeResponse(
   74: public sealed record DaxqContentKeyRequest(
   81: public sealed record DaxqContentKeyResponse(
   91: public sealed record DaxqHeartbeatRequest(
   98: public sealed record DaxqHeartbeatResponse(
  103: public sealed record DaxqSignedEnvelope(
  138: public sealed class DaxqLicenseDeniedException : InvalidOperationException
  140: public DaxqLicenseDeniedException(string message) : base(message)
  145: public sealed class HttpDaxqLicensingTransport : IDaxqLicensingTransport
  150: public HttpDaxqLicensingTransport(HttpClient httpClient) =>
  153: public ValueTask<DaxqChallengeResponse> CreateChallengeAsync(
  166: public ValueTask<DaxqContentKeyResponse> ReleaseContentKeyAsync(
  180: public ValueTask<DaxqHeartbeatResponse> HeartbeatAsync(
  194: public async ValueTask<DaxqSignedEnvelope> GetRevocationsAsync(
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqLicensingRuntime.cs
```cs
   17: public bool IsAuthorized => Volatile.Read(ref _authorized) != 0;
   19: public string Reason => Volatile.Read(ref _reason) ?? "The DAXQ license is no longer active.";
   21: public Task<string> Revoked => _revoked.Task;
   23: public void Revoke(string reason)
   40: public DaxqLicensedProgramSession(DaxqProgram program, DaxqHeartbeatController heartbeat)
   47: public DaxqProgram Program => _program ??
   50: public DaxqLicenseGate Gate { get; }
   52: public void AttachNativeVm(DaxqNativeVm vm)
   58: public void StartReferenceVm() => StartHeartbeat();
   60: public void ReleaseManagedProgram()
   66: public void Dispose()
  100: public DaxqLicensingRuntime(
  141: public async ValueTask<DaxqLicensedProgramSession> ActivateAsync(
  623: public DaxqHeartbeatController(
  652: public DaxqLicenseGate Gate { get; } = new();
  654: public void Start() => _loop = Task.Run(() => RunAsync(_stop.Token));
  656: public void AttachNativeVm(DaxqNativeVm vm)
  670: public void Dispose()
  874: public DaxqRevocationState(string? statePath, DaxqEs256PublicKeyRing trust)
  882: public long LastSequence
  890: public IReadOnlyList<DaxqRevocationEntry> Entries
  898: public void Accept(
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqLiveSignalStrategyViewModel.cs
```cs
   16: public DaxqLiveSignalStrategyViewModel(
   35: protected override StrategyDataRequirement DataRequirement => _definition.DataRequirement;
   37: protected override IBacktestStrategy BuildStrategy(Contract contract)
   44: protected override Task OnWarmupBarsLoadedAsync(IReadOnlyList<Bar> bars)
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqNativeVmIntegrity.cs
```cs
   26: public static string? VerifyAndConfigure(DaxqProtectedStrategyEngineOptions options)
  150: public static MacCodeSignatureVerifier Instance { get; } = new();
  152: public bool TryVerify(string path, out string teamIdentifier, out string failure)
  227: public static WindowsAuthenticodeVerifier Instance { get; } = new();
  229: public bool TryVerify(string path, out string signerCertificateSha256, out string failure)
  296: public uint StructSize;
  297: public nint FilePath;
  298: public nint FileHandle;
  299: public nint KnownSubject;
  305: public uint StructSize;
  306: public nint PolicyCallbackData;
  307: public nint SipClientData;
  308: public uint UiChoice;
  309: public uint RevocationChecks;
  310: public uint UnionChoice;
  311: public nint FileInfo;
  312: public uint StateAction;
  313: public nint StateData;
  314: public nint UrlReference;
  315: public uint ProviderFlags;
  316: public uint UiContext;
  317: public nint SignatureSettings;
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqPackageReader.cs
```cs
   36: public static LoadedDaxqPackage Read(string daxqPath, DaxqEs256PublicKeyRing releaseTrust)
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqPlatformDataProtection.cs
```cs
   27: public static byte[] Protect(
   62: public static byte[] Unprotect(
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqProtectedStrategyEngine.cs
```cs
   11: public sealed class DaxqProtectedStrategyEngineOptions
   14: public bool ForceReferenceVm { get; set; }
   20: public string? NativeLibraryPath { get; set; }
   23: public string? ExpectedNativeLibrarySha256 { get; set; }
   26: public string? ExpectedNativePublisherCertificateSha256 { get; set; }
   31: public string? ExpectedNativeMacTeamIdentifier { get; set; }
   39: public bool EnableLocalDevelopmentProtocol { get; set; }
   41: public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromMinutes(30);
   43: public TimeSpan OfflineGraceLimit { get; set; } = TimeSpan.FromHours(24);
   45: public TimeSpan MaximumRevocationFeedAge { get; set; } = TimeSpan.FromHours(2);
   47: public string ExpectedIssuer { get; set; } = "daxalgo-platform-development";
   49: public string ExpectedAudience { get; set; } = "daxalgo-daxq-host";
   55: public string? RevocationStatePath { get; set; }
   57: public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
   59: public IDaxqDeliveryContextResolver? DeliveryContextResolver { get; set; }
   61: public IDaxqDeviceIdentityProvider? DeviceIdentityProvider { get; set; }
   63: public IDaxqLicensingTransport? LicensingTransport { get; set; }
   65: public DaxqEs256PublicKeyRing? ReleaseSigningKeys { get; set; }
   67: public DaxqEs256PublicKeyRing? LicensingSigningKeys { get; set; }
   71: public sealed class DaxqProtectedStrategyEngine : IProtectedStrategyEngine
   79: public DaxqProtectedStrategyEngine(ILogger<DaxqProtectedStrategyEngine> logger)
   84: public DaxqProtectedStrategyEngine(
  155: public IReadOnlyList<ProtectedStrategyRegistration> LoadStrategies(string daxqPath)
  191: public static FailClosedLicensing Instance { get; } = new();
  193: public ValueTask<DaxqDeliveryContext> ResolveAsync(
  200: public ValueTask<DaxqChallengeResponse> CreateChallengeAsync(
  207: public ValueTask<DaxqContentKeyResponse> ReleaseContentKeyAsync(
  215: public ValueTask<DaxqHeartbeatResponse> HeartbeatAsync(
  223: public ValueTask<DaxqSignedEnvelope> GetRevocationsAsync(
  233: public static class DaxqHostServiceCollectionExtensions
  236: public static IServiceCollection AddDaxqProtectedStrategyEngine(
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqRemoteSignalStrategy.cs
```cs
   19: public sealed record DaxqSignalStrategyMetadata
   21: public DaxqSignalStrategyMetadata(
   53: public string StrategyId { get; }
   55: public string DisplayName { get; }
   57: public string Description { get; }
   59: public string Version { get; }
   61: public Guid LicenseId { get; }
   63: public Guid ReleaseId { get; }
   65: public StrategyDataRequirement DataRequirement { get; }
   67: public string? LinkUrl { get; }
   83: public interface IDaxqSignalSessionContextResolver
   85:     ValueTask<DaxqDeliveryContext> ResolveAsync(
   86:     DaxqSignalStrategyMetadata strategy,
   87:     CancellationToken cancellationToken);
   95: public sealed class DaxqSignalStrategyRegistrationService
  101: public DaxqSignalStrategyRegistrationService(
  111: public ITradingStrategy Register(DaxqSignalStrategyMetadata metadata)
  131: public string Id => metadata.StrategyId;
  133: public string? BacktestStrategyId => null;
  135: public string DisplayName => metadata.DisplayName;
  137: public string Description =>
  140: public StrategyDataRequirement DataRequirement => metadata.DataRequirement;
  142: public string? LinkUrl => metadata.LinkUrl;
  151: public DaxqRemoteSignalStrategyViewModel(
  174: protected override StrategyDataRequirement DataRequirement => _metadata.DataRequirement;
  176: protected override IBacktestStrategy BuildStrategy(Contract contract) =>
  216: public DaxqRemoteSignalStrategy(
  228: public async Task OnStartAsync(IClock clock, IOrderRouter router, CancellationToken ct)
  254: public Task OnTickAsync(Tick tick, IClock clock, IOrderRouter router, CancellationToken ct) =>
  257: public Task OnBarAsync(Bar bar, IClock clock, IOrderRouter router, CancellationToken ct) =>
  260: public Task OnDepthAsync(DepthSnapshot depth, IClock clock, IOrderRouter router, CancellationToken ct) =>
  263: public Task OnTradeAsync(TradePrint trade, IClock clock, IOrderRouter router, CancellationToken ct) =>
  266: public Task OnOrderEventAsync(OrderEvent evt, CancellationToken ct) => Task.CompletedTask;
  268: public Task OnEndAsync(IClock clock, IOrderRouter router, CancellationToken ct) => StopAsync();
  270: public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
  333: public static FailClosedSignalSessionContextResolver Instance { get; } = new();
  335: public ValueTask<DaxqDeliveryContext> ResolveAsync(
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqSignalSessionClient.cs
```cs
   13: public sealed class DaxqSignalSessionClientOptions
   16: public bool AllowInsecureLoopback { get; set; }
   18: public int MaximumMessageBytes { get; set; } = 16 * 1024;
   20: public TimeSpan MaximumSessionTokenLifetime { get; set; } = TimeSpan.FromMinutes(15);
   22: public int MaximumHeartbeatIntervalSeconds { get; set; } = 300;
   24: public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
   31: public interface IDaxqSignalSessionClient
   33:     ValueTask<DaxqSignalSession> OpenAsync(
   34:     DaxqDeliveryContext context,
   35:     CancellationToken cancellationToken = default);
   38: public sealed class DaxqSignalSessionClient : IDaxqSignalSessionClient
   46: public DaxqSignalSessionClient(
   86: public async ValueTask<DaxqSignalSession> OpenAsync(
  220: public sealed record DaxqSignalSessionTermination(
  226: public sealed class DaxqSignalSession : IAsyncDisposable
  249: public Guid SessionId { get; }
  251: public DateTimeOffset ExpiresAt { get; }
  253: public TimeSpan HeartbeatInterval { get; }
  255: public async Task<DaxqSignalSessionTermination> ReceiveAsync(
  302: public async ValueTask DisposeAsync()
  510: public async ValueTask<DaxqSignalSessionOpenResponse> OpenAsync(
  614: public static ClientWebSocketFactory Instance { get; } = new();
  616: public IDaxqSignalSocket Create() => new ClientWebSocketAdapter();
  623: public WebSocketState State => _socket.State;
  625: public async ValueTask ConnectAsync(
  634: public async ValueTask<DaxqSocketReceiveResult> ReceiveAsync(
  647: public async ValueTask CloseOutputAsync(
  653: public ValueTask DisposeAsync()
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqStrategyDefinition.cs
```cs
   11: public DaxqStrategyDefinition(
   27: public DaxqManifest Manifest => Package.Manifest;
   29: public LoadedDaxqPackage Package { get; }
   31: public DaxqLicensingRuntime LicensingRuntime { get; }
   33: public string PluginName { get; }
   35: public bool ForceReferenceVm { get; }
   37: public string? NativeRuntimeFailure { get; }
   39: public StrategyParameterSchema Schema { get; }
   41: public StrategyDataRequirement DataRequirement { get; }
   43: public DaxqStrategyKernel CreateKernel(Contract contract, StrategyParameters? parameters = null) =>
   46: public ValueTask<DaxqLicensedProgramSession> ActivateAsync(CancellationToken cancellationToken) =>
   49: public double[] CreateParameterValues(StrategyParameters parameters)
  109: public string Id => definition.Manifest.StrategyId;
  111: public string BacktestStrategyId => Id;
  113: public string DisplayName => definition.Manifest.StrategyId;
  115: public string Description => $"Protected DAXQ strategy {definition.Manifest.Version}.";
  117: public StrategyDataRequirement DataRequirement => definition.DataRequirement;
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqStrategyInstaller.cs
```cs
   10: public sealed record DaxqStrategyInstallResult(
   21: public sealed class DaxqStrategyInstaller(
   28: public DaxqStrategyInstallResult Install(string sourcePath)
```

## src/linux/AI/DaxAlgo.Daxq.Host/DaxqStrategyKernel.cs
```cs
   35: public DaxqStrategyKernel(
   47: public async Task OnStartAsync(IClock clock, IOrderRouter router, CancellationToken ct)
   77: public Task OnBarAsync(Bar bar, IClock clock, IOrderRouter router, CancellationToken ct)
  102: public Task OnTickAsync(Tick tick, IClock clock, IOrderRouter router, CancellationToken ct)
  131: public Task OnDepthAsync(DepthSnapshot depth, IClock clock, IOrderRouter router, CancellationToken ct)
  139: public Task OnTradeAsync(TradePrint trade, IClock clock, IOrderRouter router, CancellationToken ct)
  147: public Task OnOrderEventAsync(OrderEvent evt, CancellationToken ct)
  155: public Task OnEndAsync(IClock clock, IOrderRouter router, CancellationToken ct)
  162: public void SeedBars(IReadOnlyList<Bar> bars)
  187: public void Dispose()
  355: public DaxqStrategyRuntimeException(string pluginName, string message, Exception? inner = null)
  358: public string PluginName { get; }
```
