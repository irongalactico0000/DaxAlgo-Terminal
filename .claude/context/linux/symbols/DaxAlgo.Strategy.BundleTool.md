# DaxAlgo.Strategy.BundleTool — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Tools/DaxAlgo.Strategy.BundleTool/Program.cs
```cs
   42: public static int Run(string[] arguments)
  693: public static ParsedOptions Parse(IEnumerable<string> arguments)
  743: public void RequireOnly(IReadOnlySet<string> allowed)
  749: public string RequiredSingle(string name) =>
  752: public string? OptionalSingle(string name)
  762: public IReadOnlyList<string> Many(string name) =>
  770: public BundleSignatureException(string message) : base(message) { }
  771: public BundleSignatureException(string message, Exception innerException) : base(message, innerException) { }
```
