using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using DaxAlgo.Sdk;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Core.Time;
using TradingTerminal.Infrastructure.Plugins;

namespace TradingTerminal.Infrastructure.Strategies.Authoring;

/// <summary>
/// Roslyn compiler for canonical SDK visualizers and strategy kernels. Unlike the legacy strategy
/// compiler, success means the loaded runtime type has been checked against the complete reviewed
/// authored-unit specification.
/// </summary>
public sealed class RoslynAuthoredUnitCompilerV1 : IAuthoredUnitCompilerV1
{
    private const string GlobalUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        global using DaxAlgo.Sdk;
        global using DaxAlgo.Sdk.Drawing;
        global using TradingTerminal.Core.Domain;
        global using TradingTerminal.Core.Strategies;
        global using TradingTerminal.Core.Strategies.Parameters;
        global using TradingTerminal.Core.Time;
        """;

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    public AuthoredUnitCompilationResultV1 Compile(
        AuthoredUnitSpecificationV1 specification,
        StrategyScript script)
    {
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(script);

        var diagnostics = new List<StrategyDiagnostic>();
        var files = script.Files ?? [];
        foreach (var issue in AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification))
            diagnostics.Add(Error(issue.Code, $"{issue.Path}: {issue.Message}"));

        if (!string.Equals(script.Id, specification.UnitId, StringComparison.Ordinal))
            diagnostics.Add(Error("DAXU100", "The script id does not match the authored-unit specification."));
        if (!string.Equals(script.DisplayName, specification.Name, StringComparison.Ordinal))
            diagnostics.Add(Error("DAXU101", "The script display name does not match the authored-unit specification."));
        if (files.Count == 0)
            diagnostics.Add(Error("DAXU102", "There is no authored-unit source to compile."));
        if (diagnostics.Count > 0)
            return AuthoredUnitCompilationResultV1.Failed(diagnostics);

        var specificationHash = AuthoredUnitSpecificationCanonicalJsonV1.Hash(specification);
        var specificationJson = AuthoredUnitSpecificationCanonicalJsonV1.Serialize(specification);
        var trees = new List<SyntaxTree>(files.Count + 2)
        {
            CSharpSyntaxTree.ParseText(GlobalUsings, ParseOptions, path: "GlobalUsings.g.cs"),
            CSharpSyntaxTree.ParseText(PluginEntryPoint(script, specificationJson), ParseOptions, path: "Plugin.g.cs"),
        };
        trees.AddRange(files.Select(file =>
            CSharpSyntaxTree.ParseText(file.Content, ParseOptions, path: FileName(file, script))));

        var compilation = CSharpCompilation.Create(
            BuildAssemblyName(script, specificationHash, specificationJson),
            trees,
            BuildReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable,
                allowUnsafe: false).WithDeterministic(true));

        using var peStream = new MemoryStream();
        var emit = compilation.Emit(peStream);
        diagnostics.AddRange(emit.Diagnostics
            .Where(static item => item.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)
            .Select(Map));
        if (!emit.Success)
            return AuthoredUnitCompilationResultV1.Failed(diagnostics);

        var image = peStream.ToArray();
        var scan = PluginPolicyScanner.ScanImage(image, $"{Sanitize(script.Id)}.dll");
        AddPolicyDiagnostics(diagnostics, scan);
        if (scan.Verdict == PluginScanSeverity.Block)
            return AuthoredUnitCompilationResultV1.Failed(diagnostics);

        try
        {
            var assembly = Assembly.Load(image);
            var binding = Bind(specification, specificationHash, assembly);
            diagnostics.AddRange(binding.Diagnostics);
            if (binding.RuntimeType is null || binding.Schema is null || binding.DataRequirement is null)
                return AuthoredUnitCompilationResultV1.Failed(diagnostics);

            return AuthoredUnitCompilationResultV1.Succeeded(
                new CompiledAuthoredUnitV1(
                    image,
                    assembly,
                    binding.RuntimeType,
                    specification.Kind,
                    specificationHash,
                    binding.Schema,
                    binding.DataRequirement.Value),
                diagnostics);
        }
        catch (Exception exception)
        {
            diagnostics.Add(Error("DAXU199", $"Authored-unit load failed: {exception.Message}"));
            return AuthoredUnitCompilationResultV1.Failed(diagnostics);
        }
    }

    private static BindingResult Bind(
        AuthoredUnitSpecificationV1 specification,
        string specificationHash,
        Assembly assembly)
    {
        var diagnostics = new List<StrategyDiagnostic>();
        var expectedInterface = specification.Kind == AuthoredUnitKindV1.Visualizer
            ? typeof(IVisualizer)
            : typeof(IStrategyKernel);
        var candidates = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false, IsPublic: true } &&
                           expectedInterface.IsAssignableFrom(type))
            .ToArray();
        if (candidates.Length != 1)
        {
            diagnostics.Add(Error("DAXU200",
                $"Expected exactly one public {expectedInterface.Name} class; found {candidates.Length}."));
            return new(null, null, null, diagnostics);
        }

        var runtimeType = candidates[0];
        if (runtimeType.GetConstructor(Type.EmptyTypes) is null)
        {
            diagnostics.Add(Error("DAXU201", $"'{runtimeType.Name}' needs a public parameterless constructor."));
            return new(null, null, null, diagnostics);
        }

        var hashProperty = runtimeType.GetProperty(
            "SpecificationHashSha256",
            BindingFlags.Public | BindingFlags.Static);
        var boundHash = hashProperty?.PropertyType == typeof(string)
            ? hashProperty.GetValue(null) as string
            : null;
        if (!string.Equals(boundHash, specificationHash, StringComparison.Ordinal))
        {
            diagnostics.Add(Error("DAXU202",
                "The runtime type is not bound to the exact reviewed specification hash."));
            return new(null, null, null, diagnostics);
        }

        var instance = Activator.CreateInstance(runtimeType)!;
        var schema = instance switch
        {
            IVisualizer visualizer => visualizer.Schema,
            IStrategyKernel strategy => strategy.Schema,
            _ => null,
        };
        var dataRequirement = instance switch
        {
            IVisualizer visualizer => visualizer.DataRequirement,
            IStrategyKernel strategy => strategy.DataRequirement,
            _ => (StrategyDataRequirement?)null,
        };
        if (schema is null)
            diagnostics.Add(Error("DAXU203", "The runtime parameter schema is null."));
        if (dataRequirement != specification.DataRequirement)
            diagnostics.Add(Error("DAXU204",
                $"Runtime data requirement '{dataRequirement}' does not equal specification '{specification.DataRequirement}'."));
        if (schema is not null)
            ValidateSchema(specification.Parameters, schema, diagnostics);
        ValidateRequiredCallbacks(specification, runtimeType, expectedInterface, diagnostics);
        if (schema is not null)
            ValidateRuntimeProbe(specification, instance, schema, diagnostics);

        return diagnostics.Any(static item => item.Severity == StrategyDiagnosticSeverity.Error)
            ? new(null, null, null, diagnostics)
            : new(runtimeType, schema, dataRequirement, diagnostics);
    }

    private static void ValidateRequiredCallbacks(
        AuthoredUnitSpecificationV1 specification,
        Type runtimeType,
        Type contractType,
        ICollection<StrategyDiagnostic> diagnostics)
    {
        var interfaceMap = runtimeType.GetInterfaceMap(contractType);
        Require(StrategyDataRequirement.Bars, nameof(IVisualizer.OnBarAsync));
        Require(StrategyDataRequirement.L1, nameof(IVisualizer.OnQuoteAsync));
        Require(StrategyDataRequirement.TradeTape, nameof(IVisualizer.OnTradeAsync));
        Require(StrategyDataRequirement.Depth, nameof(IVisualizer.OnDepthAsync));
        return;

        void Require(StrategyDataRequirement requirement, string methodName)
        {
            if (!specification.DataRequirement.HasFlag(requirement)) return;

            var implementation = interfaceMap.InterfaceMethods
                .Select((method, index) => new { Method = method, Target = interfaceMap.TargetMethods[index] })
                .FirstOrDefault(item => string.Equals(item.Method.Name, methodName, StringComparison.Ordinal));
            if (implementation is null || implementation.Target.DeclaringType?.IsInterface != false)
            {
                diagnostics.Add(Error("DAXU211",
                    $"The runtime declares {requirement} data but does not implement {methodName}."));
            }
        }
    }

    private static void ValidateRuntimeProbe(
        AuthoredUnitSpecificationV1 specification,
        object instance,
        StrategyParameterSchema schema,
        ICollection<StrategyDiagnostic> diagnostics)
    {
        if (instance is not IAuthoredDrawingManifest manifest)
        {
            diagnostics.Add(Error("DAXU207",
                "The runtime type does not implement IAuthoredDrawingManifest."));
            return;
        }

        IReadOnlyList<string>? actualLayers;
        try
        {
            actualLayers = manifest.DrawingLayerTypeIds;
        }
        catch (Exception exception)
        {
            diagnostics.Add(Error("DAXU208", $"Reading the drawing-layer manifest failed: {exception.Message}"));
            return;
        }

        var expectedLayers = specification.Drawing.Layers.Select(static layer => layer.TypeId).ToArray();
        if (actualLayers is null || !expectedLayers.SequenceEqual(actualLayers, StringComparer.Ordinal))
        {
            diagnostics.Add(Error("DAXU208",
                "The runtime drawing-layer manifest does not exactly match the reviewed specification."));
            return;
        }

        var data = new ProbeMarketDataView(
            specification.Instruments.Select(static instrument => instrument.InstrumentId),
            specification.DataRequirement);
        var context = new ProbeRuntimeContext(data, new ProbeParameters(schema));
        var initial = new DrawingProbeSurface();
        var afterData = new DrawingProbeSurface();
        try
        {
            switch (instance)
            {
                case IVisualizer visualizer:
                    Await(visualizer.OnStartAsync(context, CancellationToken.None));
                    visualizer.Draw(initial);
                    FeedVisualizer(specification, visualizer, context);
                    visualizer.Draw(afterData);
                    Await(visualizer.OnStopAsync(context, CancellationToken.None));
                    break;
                case IStrategyKernel strategy:
                    Await(strategy.OnStartAsync(context, CancellationToken.None));
                    strategy.Draw(initial);
                    FeedStrategy(specification, strategy, context);
                    strategy.Draw(afterData);
                    Await(strategy.OnStopAsync(context, CancellationToken.None));
                    break;
            }
        }
        catch (Exception exception)
        {
            diagnostics.Add(Error("DAXU209", $"The authored unit failed its bounded lifecycle/data/draw probe: {Unwrap(exception).Message}"));
            return;
        }

        if (initial.OperationCount == 0)
            diagnostics.Add(Error("DAXU210",
                "The authored unit drew a blank first frame; it must render a bounded waiting or empty state before live data arrives."));
        if (afterData.OperationCount == 0)
            diagnostics.Add(Error("DAXU212",
                "The authored unit drew a blank frame after receiving its declared market data."));
        else if (string.Equals(initial.Fingerprint, afterData.Fingerprint, StringComparison.Ordinal))
            diagnostics.Add(Error("DAXU213",
                "The rendered frame did not change after deterministic samples reached every declared data callback."));

        foreach (var layer in specification.Drawing.Layers)
        {
            if (!afterData.EmittedLayers.TryGetValue(layer.LayerId, out var emitted))
            {
                diagnostics.Add(Error(
                    "DAXU214",
                    $"Reviewed drawing layer '{layer.LayerId}' ({layer.TypeId}) was not emitted by Draw after data arrived."));
                continue;
            }

            if (!string.Equals(emitted.TypeId, layer.TypeId, StringComparison.Ordinal))
            {
                diagnostics.Add(Error(
                    "DAXU217",
                    $"Reviewed drawing layer '{layer.LayerId}' emitted type '{emitted.TypeId}' instead of '{layer.TypeId}'."));
                continue;
            }

            if (!afterData.HasRequiredPrimitives(layer.LayerId, layer.Kind))
            {
                diagnostics.Add(Error(
                    "DAXU215",
                    $"Reviewed drawing layer '{layer.LayerId}' ({layer.TypeId}) did not emit the primitives required for {layer.Kind}."));
            }
        }

        foreach (var unreviewed in afterData.EmittedLayers.Keys.Except(
                     specification.Drawing.Layers.Select(static layer => layer.LayerId),
                     StringComparer.Ordinal))
        {
            diagnostics.Add(Error(
                "DAXU216",
                $"Draw emitted unreviewed layer instance '{unreviewed}', which is absent from the specification."));
        }
    }

    private static void FeedVisualizer(
        AuthoredUnitSpecificationV1 specification,
        IVisualizer visualizer,
        ProbeRuntimeContext context)
    {
        foreach (var sample in ProbeSamples(specification, context))
        {
            if (sample.Bar is not null) Await(visualizer.OnBarAsync(sample.Bar, context, CancellationToken.None));
            if (sample.Quote is not null) Await(visualizer.OnQuoteAsync(sample.Quote, context, CancellationToken.None));
            if (sample.Trade is not null) Await(visualizer.OnTradeAsync(sample.Trade, context, CancellationToken.None));
            if (sample.Depth is not null) Await(visualizer.OnDepthAsync(sample.Instrument, sample.Depth, context, CancellationToken.None));
        }
    }

    private static void FeedStrategy(
        AuthoredUnitSpecificationV1 specification,
        IStrategyKernel strategy,
        ProbeRuntimeContext context)
    {
        foreach (var sample in ProbeSamples(specification, context))
        {
            if (sample.Bar is not null) Await(strategy.OnBarAsync(sample.Bar, context, CancellationToken.None));
            if (sample.Quote is not null) Await(strategy.OnQuoteAsync(sample.Quote, context, CancellationToken.None));
            if (sample.Trade is not null) Await(strategy.OnTradeAsync(sample.Trade, context, CancellationToken.None));
            if (sample.Depth is not null) Await(strategy.OnDepthAsync(sample.Instrument, sample.Depth, context, CancellationToken.None));
        }
    }

    private static IEnumerable<ProbeSample> ProbeSamples(
        AuthoredUnitSpecificationV1 specification,
        ProbeRuntimeContext context)
    {
        var barSize = ToBarSize(specification.Timeframe.BarSize);
        var step = barSize.ToTimeSpan();
        var start = new DateTime(2024, 1, 2, 14, 30, 0, DateTimeKind.Utc);
        for (var index = 0; index < 32; index++)
        {
            foreach (var instrument in specification.Instruments)
            {
                var time = start + TimeSpan.FromTicks(step.Ticks * index);
                var price = 100d + index + instrument.InstrumentId.Value * 0.001d;
                context.Clock.Set(time + step);
                var bar = specification.DataRequirement.HasFlag(StrategyDataRequirement.Bars)
                    ? new OhlcvBar(
                        instrument.InstrumentId, barSize, time, price - 0.5d, price + 1d,
                        price - 1d, price, 1_000 + index, instrument.PreferredBroker ?? BrokerKind.Simulated, true)
                    : null;
                var quote = specification.DataRequirement.HasFlag(StrategyDataRequirement.L1)
                    ? new Quote(
                        instrument.InstrumentId, time, time, price - 0.05d, price + 0.05d,
                        100 + index, 120 + index, instrument.PreferredBroker ?? BrokerKind.Simulated,
                        index + 1, false)
                    : null;
                var trade = specification.DataRequirement.HasFlag(StrategyDataRequirement.TradeTape)
                    ? new TradePrint(
                        instrument.InstrumentId, time, time, price, 10 + index,
                        index % 2 == 0 ? AggressorSide.Buy : AggressorSide.Sell,
                        instrument.PreferredBroker ?? BrokerKind.Simulated, index + 1, false)
                    : null;
                var depth = specification.DataRequirement.HasFlag(StrategyDataRequirement.Depth)
                    ? new DepthSnapshot(
                        time,
                        [new DepthLevel(price - 0.05d, 100 + index)],
                        [new DepthLevel(price + 0.05d, 120 + index)])
                    : null;
                context.Data.Add(bar, quote, trade, instrument.InstrumentId, depth);
                yield return new ProbeSample(instrument.InstrumentId, bar, quote, trade, depth);
            }
        }
    }

    private static BarSize ToBarSize(TimeSpan? duration) => duration switch
    {
        { } value when value == TimeSpan.FromMinutes(3) => BarSize.ThreeMinutes,
        { } value when value == TimeSpan.FromMinutes(5) => BarSize.FiveMinutes,
        { } value when value == TimeSpan.FromMinutes(15) => BarSize.FifteenMinutes,
        { } value when value == TimeSpan.FromHours(1) => BarSize.OneHour,
        { } value when value == TimeSpan.FromDays(1) => BarSize.OneDay,
        _ => BarSize.OneMinute,
    };

    private static void Await(Task task) =>
        task.WaitAsync(TimeSpan.FromSeconds(2)).GetAwaiter().GetResult();

    private static Exception Unwrap(Exception exception) =>
        exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;

    private static void ValidateSchema(
        IReadOnlyList<AuthoredUnitParameterV1> expected,
        StrategyParameterSchema actual,
        ICollection<StrategyDiagnostic> diagnostics)
    {
        if (actual.Parameters.Count != expected.Count)
        {
            diagnostics.Add(Error("DAXU205",
                $"Runtime schema has {actual.Parameters.Count} parameters; specification has {expected.Count}."));
            return;
        }

        for (var index = 0; index < expected.Count; index++)
        {
            var left = expected[index];
            var right = actual.Parameters[index];
            var prefix = $"Parameter {index + 1} ('{left.Key}')";
            if (!string.Equals(left.Key, right.Key, StringComparison.Ordinal) ||
                !string.Equals(left.DisplayName, right.DisplayName, StringComparison.Ordinal) ||
                left.Kind != right.Kind ||
                !EquivalentDefault(left, right.Default) ||
                !EquivalentNumber(left.CanonicalMinimum, right.Min) ||
                !EquivalentNumber(left.CanonicalMaximum, right.Max) ||
                !SequenceEqual(left.Choices, right.Choices) ||
                !string.Equals(left.Unit, right.Unit, StringComparison.Ordinal) ||
                !string.Equals(left.Description, right.Description, StringComparison.Ordinal))
            {
                diagnostics.Add(Error("DAXU206", $"{prefix} does not exactly implement the reviewed schema."));
            }
        }
    }

    private static bool EquivalentDefault(AuthoredUnitParameterV1 expected, object? actual) => expected.Kind switch
    {
        ParameterKind.Integer =>
            long.TryParse(expected.CanonicalDefault, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            TryInt64(actual, out var actualValue) && value == actualValue,
        ParameterKind.Number =>
            double.TryParse(expected.CanonicalDefault, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) &&
            TryDouble(actual, out var actualValue) && value.Equals(actualValue),
        ParameterKind.Boolean =>
            bool.TryParse(expected.CanonicalDefault, out var value) && actual is bool actualValue && value == actualValue,
        ParameterKind.Instrument =>
            int.TryParse(expected.CanonicalDefault.TrimStart('#'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
            actual is InstrumentId actualValue && actualValue.Value == value,
        _ => string.Equals(expected.CanonicalDefault, actual?.ToString(), StringComparison.Ordinal),
    };

    private static bool EquivalentNumber(string? expected, double? actual)
    {
        if (expected is null) return actual is null;
        return actual is not null &&
               double.TryParse(expected, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) &&
               parsed.Equals(actual.Value);
    }

    private static bool SequenceEqual(IReadOnlyList<string>? expected, IReadOnlyList<string>? actual) =>
        expected is null ? actual is null : actual is not null && expected.SequenceEqual(actual, StringComparer.Ordinal);

    private static bool TryInt64(object? value, out long result)
    {
        try
        {
            result = Convert.ToInt64(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            result = default;
            return false;
        }
    }

    private static bool TryDouble(object? value, out double result)
    {
        try
        {
            result = Convert.ToDouble(value, CultureInfo.InvariantCulture);
            return double.IsFinite(result);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException)
        {
            result = default;
            return false;
        }
    }

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        var references = new List<MetadataReference>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tpa = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        foreach (var path in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) &&
                seen.Add(Path.GetFileNameWithoutExtension(path)))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        Add(typeof(IVisualizer).Assembly);
        Add(typeof(StrategyDataRequirement).Assembly);
        return references;

        void Add(Assembly assembly)
        {
            if (!string.IsNullOrEmpty(assembly.Location) &&
                seen.Add(Path.GetFileNameWithoutExtension(assembly.Location)))
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }
    }

    private static StrategyDiagnostic Map(Diagnostic diagnostic)
    {
        var span = diagnostic.Location.GetLineSpan();
        var position = span.StartLinePosition;
        return new StrategyDiagnostic(
            diagnostic.Severity == DiagnosticSeverity.Error
                ? StrategyDiagnosticSeverity.Error
                : StrategyDiagnosticSeverity.Warning,
            diagnostic.Id,
            diagnostic.GetMessage(),
            position.Line + 1,
            position.Character + 1,
            span.Path ?? string.Empty);
    }

    private static void AddPolicyDiagnostics(
        ICollection<StrategyDiagnostic> diagnostics,
        PluginScanReport scan)
    {
        foreach (var finding in scan.Findings)
        {
            var severity = finding.Severity switch
            {
                PluginScanSeverity.Block => StrategyDiagnosticSeverity.Error,
                PluginScanSeverity.Warn => StrategyDiagnosticSeverity.Warning,
                _ => StrategyDiagnosticSeverity.Info,
            };
            diagnostics.Add(new StrategyDiagnostic(
                severity,
                $"DAXU3{(int)finding.Severity:D2}",
                finding.Detail,
                1,
                1));
        }
    }

    private static string FileName(StrategyFile file, StrategyScript script) =>
        string.IsNullOrWhiteSpace(file.Name) ? $"{Sanitize(script.Id)}.cs" : file.Name;

    private static string PluginEntryPoint(StrategyScript script, string specificationJson) => $$"""
        public sealed class DaxAlgoAuthoredUnitPlugin : DaxAlgo.Sdk.IStrategyPlugin
        {
            public string Name => {{Literal(script.DisplayName)}};
            public string TargetSdkVersion => {{Literal(SdkInfo.Version)}};

            public void Register(DaxAlgo.Sdk.IPluginRegistrar registrar) =>
                DaxAlgo.Sdk.AuthoredPluginBootstrap.Register(
                    registrar,
                    typeof(DaxAlgoAuthoredUnitPlugin).Assembly,
                    {{Literal(script.Id)}},
                    {{Literal(script.DisplayName)}},
                    {{Literal(specificationJson)}},
                    DaxAlgo.Sdk.AuthoredPluginBootstrap.CurrentVerificationContractVersion);
        }
        """;

    private static string Literal(string value) =>
        SyntaxFactory.Literal(value).ToFullString();

    private static string BuildAssemblyName(
        StrategyScript script,
        string specificationHash,
        string specificationJson)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Append(specificationHash);
        Append(GlobalUsings);
        Append(PluginEntryPoint(script, specificationJson));
        foreach (var file in script.Files)
        {
            Append(FileName(file, script));
            Append(file.Content);
        }
        var suffix = Convert.ToHexString(hash.GetHashAndReset())[..24].ToLowerInvariant();
        return $"DaxAlgo.AuthoredUnit.{Sanitize(script.Id)}.{suffix}";

        void Append(string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[sizeof(int)];
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
    }

    private static string Sanitize(string value) =>
        new(value.Select(static character => char.IsLetterOrDigit(character) ? character : '_').ToArray());

    private static StrategyDiagnostic Error(string id, string message) =>
        new(StrategyDiagnosticSeverity.Error, id, message, 1, 1);

    private sealed record BindingResult(
        Type? RuntimeType,
        StrategyParameterSchema? Schema,
        StrategyDataRequirement? DataRequirement,
        IReadOnlyList<StrategyDiagnostic> Diagnostics);

    private sealed record ProbeSample(
        InstrumentId Instrument,
        OhlcvBar? Bar,
        Quote? Quote,
        TradePrint? Trade,
        DepthSnapshot? Depth);

    private sealed class ProbeRuntimeContext : IStrategyRuntimeContext, IVisualizerContext
    {
        public ProbeRuntimeContext(ProbeMarketDataView data, IParameters parameters)
        {
            Data = data;
            Parameters = parameters;
        }

        public ProbeMarketDataView Data { get; }
        IMarketDataView IStrategyRuntimeContext.Data => Data;
        IMarketDataView IVisualizerContext.Data => Data;
        public ProbeClock Clock { get; } = new();
        IClock IStrategyRuntimeContext.Clock => Clock;
        IClock IVisualizerContext.Clock => Clock;
        public IParameters Parameters { get; }
        public IVirtualBook Book { get; } = new ProbeBook();
        public IAlertSink Alerts { get; } = new ProbeAlertSink();
    }

    private sealed class ProbeClock : IClock
    {
        public DateTime UtcNow { get; private set; } =
            new(2024, 1, 2, 14, 30, 0, DateTimeKind.Utc);

        public void Set(DateTime value) => UtcNow = value;
    }

    private sealed class ProbeParameters : IParameters
    {
        private readonly StrategyParameters _values;

        public ProbeParameters(StrategyParameterSchema schema) => _values = new StrategyParameters(schema);
        public StrategyParameterSchema Schema => _values.Schema;
        public int GetInt(string name) => _values.GetInt(name);
        public long GetLong(string name) => _values.GetLong(name);
        public double GetDouble(string name) => _values.GetDouble(name);
        public bool GetBool(string name) => _values.GetBool(name);
        public string GetString(string name) => _values.GetString(name);
        public string GetText(string name) => _values.GetText(name);
        public TEnum GetEnum<TEnum>(string name) where TEnum : struct, Enum => _values.GetEnum<TEnum>(name);
        public InstrumentId GetInstrument(string name) => _values.GetInstrument(name);
    }

    private sealed class ProbeBook : IVirtualBook
    {
        public List<VirtualTargetIntent> Targets { get; } = [];
        public void SubmitTarget(VirtualTargetIntent intent) => Targets.Add(intent);
    }

    private sealed class ProbeAlertSink : IAlertSink
    {
        public void Alert(string message, AlertLevel level, string? dedupeKey = null)
        {
        }
    }

    private sealed class ProbeMarketDataView : IMarketDataView
    {
        private readonly Dictionary<InstrumentId, List<OhlcvBar>> _bars = [];
        private readonly Dictionary<InstrumentId, List<Quote>> _quotes = [];
        private readonly Dictionary<InstrumentId, List<TradePrint>> _trades = [];
        private readonly Dictionary<InstrumentId, DepthSnapshot> _depth = [];

        public ProbeMarketDataView(
            IEnumerable<InstrumentId> instruments,
            StrategyDataRequirement dataRequirement)
        {
            Instruments = instruments.ToHashSet();
            DataRequirement = dataRequirement;
        }

        public IReadOnlySet<InstrumentId> Instruments { get; }
        public StrategyDataRequirement DataRequirement { get; }

        public void Add(
            OhlcvBar? bar,
            Quote? quote,
            TradePrint? trade,
            InstrumentId instrument,
            DepthSnapshot? depth)
        {
            if (bar is not null) Add(_bars, instrument, bar);
            if (quote is not null) Add(_quotes, instrument, quote);
            if (trade is not null) Add(_trades, instrument, trade);
            if (depth is not null) _depth[instrument] = depth;
        }

        public IReadOnlyList<OhlcvBar> RecentBars(InstrumentId instrument, BarSize size, int maxCount) =>
            Recent(_bars, instrument, maxCount).Where(bar => bar.Size == size).ToArray();

        public IReadOnlyList<Quote> RecentQuotes(InstrumentId instrument, int maxCount) =>
            Recent(_quotes, instrument, maxCount);

        public DepthSnapshot? LatestDepth(InstrumentId instrument) =>
            _depth.GetValueOrDefault(instrument);

        public IReadOnlyList<TradePrint> RecentTrades(InstrumentId instrument, int maxCount) =>
            Recent(_trades, instrument, maxCount);

        private static void Add<T>(IDictionary<InstrumentId, List<T>> values, InstrumentId instrument, T value)
        {
            if (!values.TryGetValue(instrument, out var items))
                values[instrument] = items = [];
            items.Add(value);
        }

        private static IReadOnlyList<T> Recent<T>(
            IReadOnlyDictionary<InstrumentId, List<T>> values,
            InstrumentId instrument,
            int maxCount)
        {
            if (maxCount <= 0 || !values.TryGetValue(instrument, out var items)) return [];
            return items.Skip(Math.Max(0, items.Count - maxCount)).ToArray();
        }
    }

    private sealed class DrawingProbeSurface : IRenderSurface
    {
        private readonly StringBuilder _operations = new();
        private readonly Stack<string> _layers = new();
        private readonly Dictionary<string, ProbeLayerEmission> _layerEmissions =
            new(StringComparer.Ordinal);

        public int OperationCount { get; private set; }
        public IReadOnlyDictionary<string, ProbeLayerEmission> EmittedLayers => _layerEmissions;
        public string Fingerprint => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(_operations.ToString()))).ToLowerInvariant();
        public RenderViewport Viewport => new(960, 640, 1);
        public RenderCursor Cursor => new(0, 0, false, false);
        public RenderColor Theme(RenderThemeColor token) => new(128, 128, 128);
        public IDisposable Layer(string layerId, string typeId)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(layerId);
            ArgumentException.ThrowIfNullOrWhiteSpace(typeId);
            if (_layerEmissions.TryGetValue(layerId, out var existing) &&
                !string.Equals(existing.TypeId, typeId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Authored drawing layer '{layerId}' changed type from '{existing.TypeId}' to '{typeId}'.");
            }
            _layerEmissions.TryAdd(layerId, new ProbeLayerEmission(typeId, []));
            _layers.Push(layerId);
            return new CallbackScope(() =>
            {
                if (_layers.Count == 0 || !string.Equals(_layers.Peek(), layerId, StringComparison.Ordinal))
                    throw new InvalidOperationException("Authored drawing layer scopes were disposed out of order.");
                _layers.Pop();
            });
        }
        public void SetStyle(RenderStyle style) => Record(ProbeDrawingPrimitive.Style, $"style:{style}");
        public IDisposable Panel(string title, RenderPanelKind kind)
        {
            Record(ProbeDrawingPrimitive.Panel, $"panel:{kind}:{title}");
            return NoopDisposable.Instance;
        }
        public void AxisX(double minimum, double maximum, string? format = null) =>
            Record(ProbeDrawingPrimitive.Axis, FormattableString.Invariant($"axis-x:{minimum:R}:{maximum:R}:{format}"));
        public void AxisY(double minimum, double maximum, string? format = null) =>
            Record(ProbeDrawingPrimitive.Axis, FormattableString.Invariant($"axis-y:{minimum:R}:{maximum:R}:{format}"));
        public IDisposable Series(string name, RenderSeriesKind kind)
        {
            Record(kind == RenderSeriesKind.Scatter ? ProbeDrawingPrimitive.ScatterSeries : ProbeDrawingPrimitive.Series,
                $"series:{kind}:{name}");
            return NoopDisposable.Instance;
        }
        public void Push(double x, double y) =>
            Record(ProbeDrawingPrimitive.Push, FormattableString.Invariant($"push:{x:R}:{y:R}"));
        public void Line(double x1, double y1, double x2, double y2) =>
            Record(ProbeDrawingPrimitive.Line, FormattableString.Invariant($"line:{x1:R}:{y1:R}:{x2:R}:{y2:R}"));
        public void Rect(double x, double y, double width, double height, bool filled = true) =>
            Record(ProbeDrawingPrimitive.Rect, FormattableString.Invariant($"rect:{x:R}:{y:R}:{width:R}:{height:R}:{filled}"));
        public void Text(double x, double y, string text) =>
            Record(ProbeDrawingPrimitive.Text, FormattableString.Invariant($"text:{x:R}:{y:R}:{text}"));
        public void Marker(double x, double y, RenderMarkerShape shape) =>
            Record(ProbeDrawingPrimitive.Marker, FormattableString.Invariant($"marker:{x:R}:{y:R}:{shape}"));

        public bool HasRequiredPrimitives(string layerId, AuthoredChartLayerKindV1 kind)
        {
            if (!_layerEmissions.TryGetValue(layerId, out var emission)) return false;
            var primitives = emission.Primitives;
            return kind switch
            {
                AuthoredChartLayerKindV1.Candles =>
                    primitives.Contains(ProbeDrawingPrimitive.Line) &&
                    primitives.Contains(ProbeDrawingPrimitive.Rect),
                AuthoredChartLayerKindV1.PriceLine or AuthoredChartLayerKindV1.IndicatorLine =>
                    primitives.Contains(ProbeDrawingPrimitive.Line) ||
                    primitives.Contains(ProbeDrawingPrimitive.Push),
                AuthoredChartLayerKindV1.Volume or AuthoredChartLayerKindV1.Histogram =>
                    primitives.Contains(ProbeDrawingPrimitive.Rect) ||
                    primitives.Contains(ProbeDrawingPrimitive.Push),
                AuthoredChartLayerKindV1.Scatter =>
                    primitives.Contains(ProbeDrawingPrimitive.Marker) ||
                    (primitives.Contains(ProbeDrawingPrimitive.ScatterSeries) &&
                     primitives.Contains(ProbeDrawingPrimitive.Push)),
                AuthoredChartLayerKindV1.OrderBook or AuthoredChartLayerKindV1.Footprint =>
                    primitives.Contains(ProbeDrawingPrimitive.Rect) &&
                    primitives.Contains(ProbeDrawingPrimitive.Text),
                AuthoredChartLayerKindV1.TradeTape =>
                    primitives.Contains(ProbeDrawingPrimitive.Text) ||
                    primitives.Contains(ProbeDrawingPrimitive.Marker) ||
                    primitives.Contains(ProbeDrawingPrimitive.Rect),
                AuthoredChartLayerKindV1.Annotation =>
                    primitives.Contains(ProbeDrawingPrimitive.Text) ||
                    primitives.Contains(ProbeDrawingPrimitive.Marker) ||
                    primitives.Contains(ProbeDrawingPrimitive.Line),
                _ => primitives.Any(static primitive => primitive is
                    ProbeDrawingPrimitive.Push or ProbeDrawingPrimitive.Line or ProbeDrawingPrimitive.Rect or
                    ProbeDrawingPrimitive.Text or ProbeDrawingPrimitive.Marker),
            };
        }

        private void Record(ProbeDrawingPrimitive primitive, string operation)
        {
            OperationCount++;
            _operations.Append(operation).Append('\n');
            if (_layers.Count > 0)
                _layerEmissions[_layers.Peek()].Primitives.Add(primitive);
        }

        public sealed record ProbeLayerEmission(
            string TypeId,
            HashSet<ProbeDrawingPrimitive> Primitives);

        public enum ProbeDrawingPrimitive
        {
            Style,
            Panel,
            Axis,
            Series,
            ScatterSeries,
            Push,
            Line,
            Rect,
            Text,
            Marker,
        }

        private sealed class CallbackScope(Action dispose) : IDisposable
        {
            private int _disposed;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) dispose();
            }
        }

        private sealed class NoopDisposable : IDisposable
        {
            public static NoopDisposable Instance { get; } = new();
            public void Dispose() { }
        }
    }
}
