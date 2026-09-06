# DaxAlgo.Daxq.Contracts — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/AI/DaxAlgo.Daxq.Contracts/DaxqFormat.cs
```cs
    4: public static class DaxqFormat
    7: public const string PackageExtension = ".daxq";
   10: public const string ManifestEntryName = "manifest.json";
   13: public const string CiphertextEntryName = "strategy.dqx";
   16: public const string PackageIndexEntryName = "package.json";
   19: public const string SignatureEntryName = "signature.json";
   22: public const int FormatVersion = 1;
   25: public const int PlaintextContainerVersion = 1;
   28: public const int VmAbiVersion = 3;
   31: public const int SdkAbiVersion = 3;
   34: public const string Kind = "daxq";
   37: public const string CipherAlgorithm = "AES-256-GCM";
   40: public const string SignatureAlgorithm = "ES256";
   43: public const int NonceSizeBytes = 12;
   46: public const int AuthenticationTagSizeBytes = 16;
   49: public const int SignatureSizeBytes = 64;
```

## src/linux/AI/DaxAlgo.Daxq.Contracts/DaxqManifest.cs
```cs
    8: public sealed record DaxqManifest
   13: public required int FormatVersion { get; init; }
   18: public required string Kind { get; init; }
   23: public required string StrategyId { get; init; }
   28: public required string Version { get; init; }
   33: public required int SdkAbiVersion { get; init; }
   38: public required ExecutionClass ExecutionClass { get; init; }
   43: public required string[] DataRequirements { get; init; }
   48: public required DaxqParameterManifest[] Parameters { get; init; }
   53: public required DaxqProtectionManifest Protection { get; init; }
   58: public required DaxqWatermarkManifest Watermark { get; init; }
   63: public required int VmMin { get; init; }
   68: public required Dictionary<string, string> Files { get; init; }
   72: public sealed record DaxqParameterManifest
   77: public required string Id { get; init; }
   82: public required string Type { get; init; }
   88: public JsonElement? Min { get; init; }
   94: public JsonElement? Max { get; init; }
   99: public required JsonElement Default { get; init; }
  103: public sealed record DaxqProtectionManifest
  108: public required string Algorithm { get; init; }
  113: public required string ContentKeyId { get; init; }
  118: public required string Nonce { get; init; }
  123: public required string CipherSha256 { get; init; }
  127: public sealed record DaxqWatermarkManifest
  132: public required string Scheme { get; init; }
  137: public required string Slot { get; init; }
```

## src/linux/AI/DaxAlgo.Daxq.Contracts/ExecutionClass.cs
```cs
    7: public enum ExecutionClass : byte
```

## src/linux/AI/DaxAlgo.Daxq.Contracts/ExecutionClassJsonConverter.cs
```cs
    9: public override ExecutionClass Read(
   26: public override void Write(
```

## src/linux/AI/DaxAlgo.Daxq.Contracts/HostFn.cs
```cs
    4: public enum HostFn : ushort
```

## src/linux/AI/DaxAlgo.Daxq.Contracts/Opcode.cs
```cs
    4: public enum Opcode : byte
```
