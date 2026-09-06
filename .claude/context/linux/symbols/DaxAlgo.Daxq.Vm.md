# DaxAlgo.Daxq.Vm — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/AI/DaxAlgo.Daxq.Vm/DaxqNativeVm.cs
```cs
    8: public sealed unsafe class DaxqNativeVm : IDisposable
   78: public ReadOnlySpan<DaxqSignal> EmittedSignals => _signals.AsSpan(0, _visibleSignalCount);
   81: public ReadOnlySpan<DaxqLogRecord> Logs => _logs.AsSpan(0, _visibleLogCount);
  117: public static DaxqFault TryCreate(
  162: public DaxqInvocationResult Initialize() => Invoke(DaxqEntrypoint.Initialize, []);
  164: public DaxqInvocationResult OnBar(long barIndex)
  171: public DaxqInvocationResult OnTick(
  194: public DaxqInvocationResult Invoke(DaxqEntrypoint entrypoint, ReadOnlySpan<DaxqValue> arguments)
  290: public void Dispose()
  644: public NativeBlob(byte* data, int length)
  650: public readonly byte* Data;
  651: public readonly uint Length;
  657: public uint AbiVersion;
  658: public uint StructSize;
  659: public NativeBlob Bytecode;
  660: public NativeBlob ConstantPool;
  661: public NativeBlob OpcodeMap;
  662: public NativeBlob HostMap;
  663: public NativeBlob Entrypoints;
  669: public uint AbiVersion;
  670: public uint StructSize;
  671: public nint Context;
  672: public nint Bar;
  673: public nint Indicator;
  674: public nint Parameter;
  675: public nint Emit;
  676: public nint State;
  677: public nint TimeIndex;
  678: public nint Rng;
  679: public nint Log;
  685: public uint AbiVersion;
  686: public uint StructSize;
  687: public NativeBlob Payload;
  688: public fixed byte Signature[64];
  689: public fixed byte PublicKey[64];
  696: public byte Tag;
  699: public long Bits;
  705: public uint AbiVersion;
  706: public uint StructSize;
  707: public byte EntrypointId;
  708: public byte Reserved0;
  709: public byte Reserved1;
  710: public byte Reserved2;
  711: public byte Reserved3;
  712: public byte Reserved4;
  713: public byte Reserved5;
  714: public byte Reserved6;
  715: public uint ArgCount;
  716: public uint Reserved;
  717: public NativeValue* Args;
  723: public uint AbiVersion;
  724: public uint StructSize;
  725: public int Fault;
  726: public uint ExecutedInstructions;
  727: public uint MaxStackDepth;
  728: public uint Reserved;
  735: public static extern int Create(NativeCreateOptions* options, nint* handle);
  739: public static extern int SetHostCallbacks(nint handle, NativeHostCallbacks* callbacks);
  743: public static extern int ApplyLicenseEvidence(nint handle, NativeLicenseEvidence* evidence);
  747: public static extern int RevokeLicense(nint handle);
  751: public static extern int VerifyIntegrity();
  755: public static extern int Invoke(
  762: public static extern void Destroy(nint handle);
```

## src/linux/AI/DaxAlgo.Daxq.Vm/DaxqProgram.cs
```cs
   11: public sealed class DaxqProgram : IDisposable
   36: public int StateSlotCount => StateTypes.Length;
   39: public bool HasEntrypoint(DaxqEntrypoint entrypoint) =>
   47: public static DaxqFault TryLoad(ReadOnlySpan<byte> plaintext, out DaxqProgram? program)
  141: public void Dispose()
 1255: public long I64 => Bits;
 1257: public double F64 => BitConverter.Int64BitsToDouble(Bits);
 1259: public bool Bool => Bits != 0;
 1261: public static AbstractValue Unknown(VmType type) => new(type, false, 0);
 1263: public static AbstractValue FromI64(long value) => new(VmType.I64, true, value);
 1265: public static AbstractValue FromF64(double value) => new(
 1270: public static AbstractValue FromBool(bool value) => new(VmType.Bool, true, value ? 1 : 0);
 1272: public static AbstractValue FromBuffer(VmType type, int length) => new(type, true, length);
 1274: public static AbstractValue FromConstant(DaxqValue value) => value.Type switch
 1281: public static AbstractValue Merge(AbstractValue left, AbstractValue right) =>
 1289: public FlowState(int localCount)
 1306: public bool[] InitializedLocals { get; }
 1308: public AbstractValue[] LocalValues { get; }
 1310: public List<AbstractValue> Stack { get; }
 1312: public FlowState Clone() => new(
 1347: public VerifiedEntrypoint(
 1359: public DaxqEntrypoint Id { get; }
 1361: public VmType[] ArgumentTypes { get; }
 1363: public VmType[] LocalTypes { get; }
 1365: public DecodedInstruction[] Instructions { get; }
```

## src/linux/AI/DaxAlgo.Daxq.Vm/DaxqReferenceVm.cs
```cs
   12: public sealed class DaxqReferenceVm : IDisposable
   48: public DaxqReferenceVm(
   74: public ReadOnlySpan<DaxqSignal> EmittedSignals => _signals.AsSpan(0, _visibleSignalCount);
   77: public ReadOnlySpan<DaxqLogRecord> Logs => _logs.AsSpan(0, _visibleLogCount);
   79: public DaxqInvocationResult Initialize() => Invoke(DaxqEntrypoint.Initialize, []);
   81: public DaxqInvocationResult OnBar(long barIndex)
   88: public DaxqInvocationResult OnTick(
  111: public DaxqInvocationResult Invoke(DaxqEntrypoint entrypoint, ReadOnlySpan<DaxqValue> arguments)
  152: public void Dispose()
  168: public DaxqFault TryGetState(int slot, out DaxqValue value)
  808: public VmType Type { get; }
  810: public long Bits { get; }
  812: public long I64 => Bits;
  814: public double F64 => BitConverter.Int64BitsToDouble(Bits);
  816: public bool Bool => Bits != 0;
  818: public static RuntimeValue FromInt64(long value) => new(VmType.I64, value);
  820: public static RuntimeValue FromDouble(double value) => new(
  824: public static RuntimeValue FromBool(bool value) => new(VmType.Bool, value ? 1 : 0);
  826: public static RuntimeValue FromBuffer(VmType type, int index) => new(type, index);
  828: public static RuntimeValue FromPublic(DaxqValue value) => new((VmType)value.Type, value.RawBits);
  830: public static RuntimeValue Zero(VmType type) => new(type, 0);
  832: public DaxqValue ToPublicScalar() => DaxqValue.FromRaw((DaxqValueType)Type, Bits);
  842: public static Xoshiro256StarStar Create(ulong seed)
  854: public double NextDouble()
```

## src/linux/AI/DaxAlgo.Daxq.Vm/DaxqSdkAbi3FrameHost.cs
```cs
    6: public readonly record struct DaxqBar
    8: public DaxqBar(double open, double high, double low, double close, double volume)
   23: public double Open { get; }
   25: public double High { get; }
   27: public double Low { get; }
   29: public double Close { get; }
   31: public double Volume { get; }
   39: public sealed class DaxqSdkAbi3FrameHost : IDaxqHost
   46: public DaxqSdkAbi3FrameHost(
   73: public int CurrentCompletedBarIndex
   85: public int MaximumIndicatorSamples => _maximumIndicatorSamples;
   87: public DaxqFault ReadBar(long field, long lookback, out double value)
  101: public DaxqFault ReadIndicator(
  127: public DaxqFault ReadParameter(long parameterId, out double value)
```

## src/linux/AI/DaxAlgo.Daxq.Vm/DaxqTypes.cs
```cs
    6: public enum DaxqFault : int
   30: public enum DaxqEntrypoint : byte
   38: public enum DaxqValueType : byte
   46: public readonly struct DaxqValue : IEquatable<DaxqValue>
   56: public DaxqValueType Type { get; }
   58: public static DaxqValue FromInt64(long value) => new(DaxqValueType.I64, value);
   60: public static DaxqValue FromDouble(double value)
   67: public static DaxqValue FromBoolean(bool value) => new(DaxqValueType.Bool, value ? 1 : 0);
   69: public long AsInt64() => Type == DaxqValueType.I64
   73: public double AsDouble() => Type == DaxqValueType.F64
   77: public bool AsBoolean() => Type == DaxqValueType.Bool
   81: public bool Equals(DaxqValue other) => Type == other.Type && _bits == other._bits;
   83: public override bool Equals(object? obj) => obj is DaxqValue other && Equals(other);
   85: public override int GetHashCode() => HashCode.Combine(Type, _bits);
   87: public static bool operator ==(DaxqValue left, DaxqValue right) => left.Equals(right);
   89: public static bool operator !=(DaxqValue left, DaxqValue right) => !left.Equals(right);
   99: public readonly record struct DaxqSignal(long Kind, double Strength, long NoteId);
  102: public readonly record struct DaxqLogRecord(long MessageId, double Value);
  105: public readonly record struct DaxqInvocationResult(
  112: public bool Succeeded => Fault == DaxqFault.Ok;
  119: public interface IDaxqHost
  121:     DaxqFault ReadBar(long field, long lookback, out double value);
  123:     DaxqFault ReadIndicator(long indicator, long period, long sourceField, out double value);
  125:     DaxqFault ReadParameter(long parameterId, out double value);
  129: public sealed class DaxqNullHost : IDaxqHost
  135: public static DaxqNullHost Instance { get; } = new();
  137: public DaxqFault ReadBar(long field, long lookback, out double value)
  143: public DaxqFault ReadIndicator(long indicator, long period, long sourceField, out double value)
  149: public DaxqFault ReadParameter(long parameterId, out double value)
  157: public readonly record struct DaxqEntrypointBudget(
  166: public sealed class DaxqExecutionLimits
  168: public static DaxqExecutionLimits Contract { get; } = new(
  173: public DaxqExecutionLimits(
  198: public DaxqEntrypointBudget Initialize { get; }
  200: public DaxqEntrypointBudget OnBar { get; }
  202: public DaxqEntrypointBudget OnTick { get; }
  237: public interface IDaxqMonotonicClock
  239:     long Frequency { get; }
  241:     long GetTimestamp();
  250: public static StopwatchDaxqClock Instance { get; } = new();
  252: public long Frequency => Stopwatch.Frequency;
  254: public long GetTimestamp() => Stopwatch.GetTimestamp();
```
