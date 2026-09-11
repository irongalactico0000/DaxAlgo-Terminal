using DaxAlgo.Package;
using TradingTerminal.App.Plugins;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.UI.Strategies;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

/// <summary>
/// Lane 3: Builder export writes the same installable contract as the buy-once sample.
/// </summary>
public sealed class AuthoredUnitOpenPackageExporterTests
{
    [Fact]
    public void TryWrite_then_install_registers_into_strategy_kernel_registry()
    {
        var root = Path.Combine(Path.GetTempPath(), "daxalgo-builder-export", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var specification = OpenPackageBuyOnceSample.CreateSpecification(new InstrumentId(99));
            var sources = new[]
            {
                new StrategyFile(
                    "GeneratedPaperStrategy.cs",
                    OpenPackageBuyOnceSample.CreateSource(specification)),
            };
            var packagePath = Path.Combine(root, $"exported{DaxPackage.StrategyExtension}");

            Assert.True(
                AuthoredUnitOpenPackageExporter.TryWrite(packagePath, specification, sources, out var message),
                message);
            Assert.True(File.Exists(packagePath));

            var installRoot = Path.Combine(root, "open-packages");
            var installed = OpenPackageDurableInstaller.Install(packagePath, installRoot);
            Assert.True(installed.Success, installed.Message);

            var registry = new StrategyKernelRegistry();
            var result = OpenPackageHostRegistrar.RegisterInstall(
                installed,
                registry,
                new RoslynAuthoredUnitCompilerV1());

            Assert.True(result.Registered, result.Message);
            var found = registry.Find(OpenPackageBuyOnceSample.UnitId);
            Assert.NotNull(found);
            Assert.Equal(OpenPackageBuyOnceSample.DisplayName, found!.DisplayName);
            Assert.NotNull(found.Create());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* temp */ }
        }
    }

    [Fact]
    public void TryWrite_refuses_empty_sources()
    {
        var specification = OpenPackageBuyOnceSample.CreateSpecification(new InstrumentId(1));
        var ok = AuthoredUnitOpenPackageExporter.TryWrite(
            Path.Combine(Path.GetTempPath(), "unused.daxalgostrategy"),
            specification,
            Array.Empty<StrategyFile>(),
            out var message);
        Assert.False(ok);
        Assert.Contains("C# source", message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveEntryTypeName_reads_public_class()
    {
        var name = AuthoredUnitOpenPackageExporter.ResolveEntryTypeName(
        [
            new StrategyFile("x.cs", "namespace N; public sealed class MyKernel : IStrategyKernel { }"),
        ]);
        Assert.Equal("MyKernel", name);
    }
}
