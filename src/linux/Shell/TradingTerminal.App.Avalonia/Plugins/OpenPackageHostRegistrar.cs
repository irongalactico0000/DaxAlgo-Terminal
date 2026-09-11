using System.Text;
using DaxAlgo.Package;
using DaxAlgo.Sdk;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.UI.Strategies;

namespace TradingTerminal.App.Plugins;

/// <summary>
/// Compiles durable open-packages (<c>open-packages/</c>) into live
/// <see cref="IStrategyKernelRegistry"/> entries when the package carries an authored unit
/// specification plus C# sources. Install without that payload stays durable-only.
/// </summary>
public static class OpenPackageHostRegistrar
{
    public const string SpecificationRelativePath = "authored/unit.specification.v1.json";
    public const string OpenPackagesFolderName = "open-packages";

    public sealed record Result(
        bool Registered,
        string PackageId,
        string DisplayName,
        string Message,
        StrategyKernelRegistration? Registration = null,
        VisualizerRegistration? Visualizer = null);

    public static string OpenPackagesRoot(string pluginsRoot) =>
        Path.Combine(pluginsRoot, OpenPackagesFolderName);

    public static IReadOnlyList<Result> RegisterInstalled(
        string pluginsRoot,
        IStrategyKernelRegistry kernelRegistry,
        IAuthoredUnitCompilerV1? compiler = null,
        IVisualizerRegistry? visualizerRegistry = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pluginsRoot);
        ArgumentNullException.ThrowIfNull(kernelRegistry);
        compiler ??= new RoslynAuthoredUnitCompilerV1();

        var results = new List<Result>();
        foreach (var entry in OpenPackageDurableInstaller.ReadIndex(OpenPackagesRoot(pluginsRoot)).Packages)
            results.Add(RegisterOne(entry, kernelRegistry, compiler, visualizerRegistry));
        return results;
    }

    public static Result RegisterInstall(
        OpenPackageInstallResult installed,
        IStrategyKernelRegistry kernelRegistry,
        IAuthoredUnitCompilerV1? compiler = null,
        IVisualizerRegistry? visualizerRegistry = null)
    {
        ArgumentNullException.ThrowIfNull(installed);
        ArgumentNullException.ThrowIfNull(kernelRegistry);
        if (!installed.Success || installed.Handoff is null || string.IsNullOrWhiteSpace(installed.InstallDirectory))
        {
            return new Result(false, "", "",
                string.IsNullOrWhiteSpace(installed.Message)
                    ? "Durable install did not produce an open-package directory."
                    : installed.Message);
        }

        compiler ??= new RoslynAuthoredUnitCompilerV1();
        var handoff = installed.Handoff;
        var entry = new OpenPackageIndexEntryV1(
            handoff.PackageId,
            handoff.Version,
            handoff.DisplayName,
            handoff.Publisher,
            handoff.EntryTypeName,
            handoff.Kind.ToString(),
            handoff.ArtifactExtension,
            handoff.ManifestSha256,
            handoff.PackageSha256,
            handoff.PackageLength,
            handoff.StrategyBuilderConfirmedInputSha256,
            installed.InstallDirectory,
            DateTimeOffset.UtcNow);
        return RegisterOne(entry, kernelRegistry, compiler, visualizerRegistry);
    }

    private static Result RegisterOne(
        OpenPackageIndexEntryV1 entry,
        IStrategyKernelRegistry kernelRegistry,
        IAuthoredUnitCompilerV1 compiler,
        IVisualizerRegistry? visualizerRegistry)
    {
        var isVisualizer = string.Equals(entry.Kind, nameof(DaxPackageKind.Visualizer), StringComparison.OrdinalIgnoreCase);
        var isStrategy = string.Equals(entry.Kind, nameof(DaxPackageKind.Strategy), StringComparison.OrdinalIgnoreCase);
        if (!isVisualizer && !isStrategy)
        {
            return new Result(false, entry.PackageId, entry.DisplayName,
                $"'{entry.DisplayName}' is a {entry.Kind} open package; host open/run supports strategy or visualizer only.");
        }

        var contentRoot = Path.Combine(entry.InstallDirectory, OpenPackageDurableInstaller.ContentDirectoryName);
        if (!TryResolveSpecificationPath(contentRoot, out var specificationPath))
        {
            return new Result(false, entry.PackageId, entry.DisplayName,
                $"'{entry.DisplayName}' is installed durably but missing '{SpecificationRelativePath}' " +
                $"(also checked under payload/) — host registry open/run requires the authored unit specification payload.");
        }

        AuthoredUnitSpecificationV1 specification;
        try
        {
            specification = AuthoredUnitSpecificationCanonicalJsonV1.Deserialize(File.ReadAllText(specificationPath));
        }
        catch (Exception exception)
        {
            return new Result(false, entry.PackageId, entry.DisplayName,
                $"Specification deserialize failed: {exception.Message}");
        }

        if (isStrategy && specification.Kind != AuthoredUnitKindV1.Strategy)
        {
            return new Result(false, entry.PackageId, entry.DisplayName,
                "Open-package specification Kind must be Strategy for kernel registry registration.");
        }

        if (isVisualizer && specification.Kind != AuthoredUnitKindV1.Visualizer)
        {
            return new Result(false, entry.PackageId, entry.DisplayName,
                "Open-package specification Kind must be Visualizer for visualizer registry registration.");
        }

        var launchIssues = AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification);
        if (launchIssues.Count > 0)
        {
            return new Result(false, entry.PackageId, entry.DisplayName,
                $"Specification is not launch-valid: {launchIssues[0].Message}");
        }

        if (!Directory.Exists(contentRoot))
        {
            return new Result(false, entry.PackageId, entry.DisplayName,
                "Open-package content directory is missing.");
        }

        var sources = Directory.EnumerateFiles(contentRoot, "*.cs", SearchOption.AllDirectories)
            .Select(path => new StrategyFile(
                Path.GetRelativePath(contentRoot, path).Replace('\\', '/'),
                File.ReadAllText(path, Encoding.UTF8)))
            .ToArray();
        if (sources.Length == 0)
        {
            return new Result(false, entry.PackageId, entry.DisplayName,
                "No C# sources found under the open-package content directory.");
        }

        var script = new StrategyScript(specification.UnitId, specification.Name, sources);
        var compilation = compiler.Compile(specification, script);
        if (!compilation.Success || compilation.Unit is not CompiledAuthoredUnitV1 compiled)
        {
            var diagnostic = compilation.Diagnostics.FirstOrDefault()?.Message ?? "compile failed";
            return new Result(false, entry.PackageId, entry.DisplayName, $"Compile failed: {diagnostic}");
        }

        if (isVisualizer)
        {
            if (visualizerRegistry is null)
            {
                return new Result(false, entry.PackageId, entry.DisplayName,
                    "Visualizer open-package is installed, but no visualizer registry was supplied.");
            }

            var discovered = VisualizerDescriptors.FromType(compiled.RuntimeType, specification.UnitId);
            var visualizer = discovered with
            {
                Descriptor = discovered.Descriptor with
                {
                    DisplayName = specification.Name,
                    Description = specification.RawRequest,
                },
                AuthoredSpecification = specification,
            };
            visualizerRegistry.Register(visualizer);
            return new Result(
                true,
                entry.PackageId,
                entry.DisplayName,
                $"'{entry.DisplayName}' registered for open/run as visualizer '{specification.UnitId}'.",
                Visualizer: visualizer);
        }

        var registration = new StrategyKernelRegistration(
            specification.UnitId,
            specification.Name,
            specification.RawRequest,
            () => (IStrategyKernel)Activator.CreateInstance(compiled.RuntimeType)!,
            specification,
            compiled.Schema);
        kernelRegistry.Register(registration);
        return new Result(
            true,
            entry.PackageId,
            entry.DisplayName,
            $"'{entry.DisplayName}' registered for open/run as '{specification.UnitId}'.",
            registration);
    }

    /// <summary>
    /// Spec may live at <c>content/authored/…</c> (hand-laid) or
    /// <c>content/payload/authored/…</c> (<see cref="DaxPackage.Extract"/> layout).
    /// </summary>
    internal static bool TryResolveSpecificationPath(string contentRoot, out string specificationPath)
    {
        var relative = SpecificationRelativePath.Replace('/', Path.DirectorySeparatorChar);
        var direct = Path.Combine(contentRoot, relative);
        if (File.Exists(direct))
        {
            specificationPath = direct;
            return true;
        }

        var underPayload = Path.Combine(contentRoot, "payload", relative);
        if (File.Exists(underPayload))
        {
            specificationPath = underPayload;
            return true;
        }

        specificationPath = direct;
        return false;
    }
}
