# DaxAlgo.Daxq.Compiler — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Authoring/DaxqAuthoringContracts.cs
```cs
    4: public enum Ind : long
   13: public enum BarField : long
   23: public enum SignalKind : long
   34: public interface IStrategyContext
   36:     double Indicator(Ind indicator, long period, BarField sourceField = BarField.Close);
   38:     void Emit(SignalKind kind, double strength, long noteId = 0);
   40:     double Param(long parameterId);
   42:     double Bar(BarField field, long lookback = 0);
   44:     long TimeIndex();
   46:     double Random();
   48:     void Log(long messageId, double value);
   55: public interface IBacktestStrategy
   57:     void Initialize(IStrategyContext context)
   61:     void OnBar(IStrategyContext context)
   65:     void OnTick(
   66:     IStrategyContext context,
   67:     double bid,
   68:     double ask,
   69:     double last,
   70:     double volume)
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Compilation/DaxqRoslynCompiler.cs
```cs
   10: public sealed class DaxqRoslynCompiler
   14: public DaxqLoweringResult CompileAndLower(string source, string fileName = "Strategy.cs")
   94: public sealed class DaxqCompilationException : Exception
   96: public DaxqCompilationException(IReadOnlyList<DaxqCompilerDiagnostic> diagnostics)
  104: public IReadOnlyList<DaxqCompilerDiagnostic> Diagnostics { get; }
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/DaxqCompiler.cs
```cs
    8: public sealed class DaxqCompiler
   12: public DaxqCompilationArtifact Compile(string source, DaxqCompilerOptions options)
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/DaxqCompilerModels.cs
```cs
    8: public sealed record DaxqCompilerDiagnostic(
   16: public sealed record DaxqLoweringResult(
   22: public sealed record DaxqCompilerOptions
   24: public string SourceFileName { get; init; } = "Strategy.cs";
   26: public required string StrategyId { get; init; }
   28: public required string Version { get; init; }
   30: public required IReadOnlyList<string> DataRequirements { get; init; }
   32: public IReadOnlyList<DaxqParameterManifest> Parameters { get; init; } =
   36: public required byte[] DiversificationSeed { get; init; }
   38: public byte[] Watermark { get; init; } = new byte[32];
   40: public required string ContentKeyId { get; init; }
   42: public required byte[] ContentKey { get; init; }
   44: public required byte[] Nonce { get; init; }
   46: public required string ReleaseKeyId { get; init; }
   48: public required ECDsa ReleaseSigningKey { get; init; }
   52: public sealed record DaxqCompilationArtifact(
   59: public sealed record DaxqReleaseMetadata(
   67: public readonly record struct DaxqConstant(DaxqValueType Type, long Bits)
   69: public static DaxqConstant FromInt64(long value) => new(DaxqValueType.I64, value);
   71: public static DaxqConstant FromDouble(double value)
   80: public sealed record DaxqCanonicalEntrypoint(
   86: public sealed record DaxqCanonicalProgram(
   92: public IReadOnlySet<HostFn> ReferencedHostFunctions { get; init; } = new HashSet<HostFn>();
   95: public IReadOnlyList<long> ReferencedParameterIds { get; init; } = Array.Empty<long>();
   97: public byte[] Bytecode => Entrypoints
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Lowering/DaxqIlLowerer.cs
```cs
   27: public static DaxqCanonicalProgram Lower(byte[] assemblyImage, string sourceName)
 1259: public IReadOnlyList<DaxqConstant> Values => _values;
 1261: public ushort Add(DaxqConstant value)
 1274: public IlType GetArrayType(IlType elementType, ArrayShape shape) => IlType.Unsupported;
 1275: public IlType GetByReferenceType(IlType elementType) => IlType.Unsupported;
 1276: public IlType GetFunctionPointerType(MethodSignature<IlType> signature) => IlType.Unsupported;
 1277: public IlType GetGenericInstantiation(IlType genericType, ImmutableArray<IlType> typeArguments) => IlType.Unsupported;
 1278: public IlType GetGenericMethodParameter(object? genericContext, int index) => IlType.Unsupported;
 1279: public IlType GetGenericTypeParameter(object? genericContext, int index) => IlType.Unsupported;
 1280: public IlType GetModifiedType(IlType modifier, IlType unmodifiedType, bool isRequired) => unmodifiedType;
 1281: public IlType GetPinnedType(IlType elementType) => IlType.Unsupported;
 1282: public IlType GetPointerType(IlType elementType) => IlType.Unsupported;
 1283: public IlType GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
 1292: public IlType GetSZArrayType(IlType elementType) => IlType.Unsupported;
 1293: public IlType GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind) =>
 1295: public IlType GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind) =>
 1297: public IlType GetTypeFromSpecification(MetadataReader reader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
 1310: public static IReadOnlyList<IlInstruction> Decode(ImmutableArray<byte> bytes, string sourceName, string methodName)
 1423: public static IlType Unsupported => new(IlTypeKind.Unsupported);
 1424: public static IlType Void => new(IlTypeKind.Void);
 1425: public static IlType I4 => new(IlTypeKind.I4);
 1426: public static IlType I64 => new(IlTypeKind.I64);
 1427: public static IlType F64 => new(IlTypeKind.F64);
 1428: public static IlType Bool => new(IlTypeKind.Bool);
 1429: public static IlType This => new(IlTypeKind.This);
 1430: public static IlType Context => new(IlTypeKind.Context);
 1431: public bool IsInteger => Kind is IlTypeKind.I4 or IlTypeKind.I64;
 1432: public bool IsNumeric => IsInteger || Kind == IlTypeKind.F64;
 1433: public bool IsScalar => Kind is IlTypeKind.I64 or IlTypeKind.F64 or IlTypeKind.Bool;
 1434: public override string ToString() => Kind.ToString().ToLowerInvariant();
 1439: public int TokenOperand => Operand is int value ? value : 0;
 1440: public int VariableOperand => Operand is int value ? value : -1;
 1441: public int Int32Operand => Operand is int value ? value : 0;
 1442: public long Int64Operand => Operand is long value ? value : 0;
 1443: public double DoubleOperand => Operand is double value ? value : double.NaN;
 1444: public int BranchTarget => Operand is int value ? value : -1;
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Lowering/DaxqPlaintextBuilder.cs
```cs
   10: public readonly record struct DaxqOpcodeMapEntry(byte Encoded, Opcode Canonical);
   13: public readonly record struct DaxqHostMapEntry(ushort Encoded, HostFn Canonical);
   20: public sealed record DaxqPlaintextBuildResult(
   31: public static class DaxqPlaintextBuilder
   47: public static DaxqPlaintextBuildResult BuildCanonical(DaxqCanonicalProgram program) =>
   51: public static DaxqPlaintextBuildResult BuildCanonical(
   77: public static DaxqPlaintextBuildResult BuildDiversified(
   86: public static DaxqPlaintextBuildResult BuildDiversified(
  679: public StablePermutation(ReadOnlySpan<byte> seed, string domain)
  689: public int Next(int exclusiveMaximum)
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Packaging/DaxqCanonicalJson.cs
```cs
    9: public static byte[] WriteManifest(
   50: public static byte[] WritePackageIndex(string manifestSha256, string cipherSha256)
   66: public static byte[] WriteSignature(string releaseKeyId, string signature)
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Packaging/DaxqPackageModels.cs
```cs
    7: public sealed record DaxqPackageWriteOptions
   10: public required byte[] PlaintextBytes { get; init; }
   13: public required string StrategyId { get; init; }
   16: public required string Version { get; init; }
   19: public required IReadOnlyList<string> DataRequirements { get; init; }
   22: public IReadOnlyList<DaxqParameterManifest> Parameters { get; init; } =
   26: public required string ContentKeyId { get; init; }
   29: public required byte[] ContentKey { get; init; }
   32: public required byte[] Nonce { get; init; }
   35: public required string ReleaseKeyId { get; init; }
   38: public required ECDsa ReleaseSigningKey { get; init; }
   42: public sealed record DaxqPackageArtifact
   44: public required byte[] PackageBytes { get; init; }
   46: public required byte[] PlaintextBytes { get; init; }
   48: public required byte[] CiphertextAndTagBytes { get; init; }
   50: public required byte[] ManifestJsonBytes { get; init; }
   52: public required byte[] PackageIndexJsonBytes { get; init; }
   54: public required byte[] SignatureInputBytes { get; init; }
   56: public required byte[] SignatureBytes { get; init; }
   58: public required byte[] SignatureJsonBytes { get; init; }
   60: public required string CipherSha256 { get; init; }
   62: public required string ManifestSha256 { get; init; }
   64: public required string PackageIndexSha256 { get; init; }
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Packaging/DaxqPackageValidation.cs
```cs
   24: public static NormalizedDaxqPackageOptions Normalize(DaxqPackageWriteOptions options)
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Packaging/DaxqPackageWriter.cs
```cs
   11: public static class DaxqPackageWriter
   17: public static DaxqPackageArtifact Write(DaxqPackageWriteOptions options)
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqBacktestParityGate.cs
```cs
   11: public sealed class DaxqBacktestParityGate
   17: public DaxqBacktestParityResult Evaluate(
  387: public ManagedStrategyHandle(
  397: public IBacktestStrategy Strategy { get; private set; }
  399: public IReadOnlySet<DaxqEntrypoint> Entrypoints { get; }
  401: public void Dispose()
  410: public SellerAssemblyLoadContext()
  415: protected override Assembly? Load(AssemblyName assemblyName) =>
  428: public ManagedParityContext(DaxqSdkAbi3FrameHost host, ulong launchSeed)
  434: public IReadOnlyList<DaxqSignal> Signals => _signals;
  436: public void BeginCallback(long timeIndex)
  443: public double Indicator(Ind indicator, long period, BarField sourceField = BarField.Close)
  449: public void Emit(SignalKind kind, double strength, long noteId = 0)
  461: public double Param(long parameterId)
  467: public double Bar(BarField field, long lookback = 0)
  473: public long TimeIndex() => _timeIndex;
  475: public double Random() => _rng.NextDouble();
  477: public void Log(long messageId, double value)
  511: public void Observe(
  547: public DaxqBacktestStatistics Build()
  582: public static ReferenceDataSnapshot Create(DaxqBacktestReferenceData source)
  701: public static ParityRng Create(ulong seed)
  713: public double NextDouble() =>
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqBacktestParityModels.cs
```cs
    6: public enum DaxqPublicationDecision
   13: public sealed record DaxqParityTolerance
   16: public double MaximumAbsoluteSignalStrengthDifference { get; init; }
   20: public readonly record struct DaxqBacktestCallback(
   29: public static DaxqBacktestCallback Bar(int completedBarIndex) =>
   32: public static DaxqBacktestCallback Tick(
   43: public sealed record DaxqBacktestReferenceData
   45: public required IReadOnlyList<DaxqBar> Bars { get; init; }
   47: public IReadOnlyList<double> Parameters { get; init; } = Array.Empty<double>();
   49: public required IReadOnlyList<DaxqBacktestCallback> Callbacks { get; init; }
   51: public ulong LaunchSeed { get; init; }
   55: public sealed record DaxqParityDiagnostic(
   66: public sealed record DaxqBacktestStatistics(
   82: public const string CurrentSchemaVersion = "daxq-backtest-parity-stats-v1";
   90: public sealed record DaxqListingMetrics(
  111: public const string CurrentSchemaVersion = "daxq-listing-metrics-v1";
  113: public const string PolicyCurrency = "USD";
  115: public const string PolicyFillModel =
  118: public const string PolicySizingModel =
  121: public const string PolicyProfitLossModel =
  124: public const double PolicyStartingEquity = 100_000d;
  126: public const double PolicyMaximumGrossNotional = 10_000d;
  128: public const double PolicyCommissionBasisPointsPerFill = 1d;
  130: public const double PolicyAdverseSlippageBasisPointsPerFill = 1d;
  134: public sealed record DaxqBacktestParityResult(
  141: public bool PublicationAllowed => Decision == DaxqPublicationDecision.Pass;
  143: public DaxqListingMetrics? ListingMetrics { get; init; }
  145: public byte[]? CanonicalListingMetricsJson { get; init; }
  147: public string? ListingMetricsSha256 { get; init; }
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqBacktestParityOutputJson.cs
```cs
    7: public static byte[] Write(DaxqBacktestParityResult result)
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqBacktestStatisticsJson.cs
```cs
    7: public static byte[] Write(DaxqBacktestStatistics statistics)
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqListingMetricsAccumulator.cs
```cs
   30: public void BeginCallback(double referencePrice)
   42: public void ObserveSignals(ReadOnlySpan<DaxqSignal> signals)
   62: public DaxqListingMetrics Complete(double finalReferencePrice)
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Parity/DaxqListingMetricsJson.cs
```cs
    7: public static byte[] Write(DaxqListingMetrics metrics)
```

## src/linux/Tools/DaxAlgo.Daxq.Compiler/Program.cs
```cs
   12: public static int Main(string[] args) => DaxqCompilerCli.Run(args);
   25: public static int Run(string[] args)
  367: public string[] DataRequirements { get; init; } = [];
  369: public ReferenceBarDocument[] Bars { get; init; } = [];
  371: public ReferenceParameterDocument[] Parameters { get; init; } = [];
  373: public ReferenceCallbackDocument[] Callbacks { get; init; } = [];
  375: public ulong LaunchSeed { get; init; }
  380: public string Id { get; init; } = string.Empty;
  382: public string Type { get; init; } = string.Empty;
  384: public JsonElement? Min { get; init; }
  386: public JsonElement? Max { get; init; }
  388: public JsonElement Default { get; init; }
  393: public double Open { get; init; }
  395: public double High { get; init; }
  397: public double Low { get; init; }
  399: public double Close { get; init; }
  401: public double Volume { get; init; }
  406: public string Entrypoint { get; init; } = string.Empty;
  408: public long TimeIndex { get; init; }
  410: public int CompletedBarIndex { get; init; } = -1;
  412: public double Bid { get; init; }
  414: public double Ask { get; init; }
  416: public double Last { get; init; }
  418: public double Volume { get; init; }
```
