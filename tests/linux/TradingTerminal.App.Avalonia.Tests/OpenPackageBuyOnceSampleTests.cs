using DaxAlgo.Package;
using TradingTerminal.App.Plugins;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using TradingTerminal.UI.Strategies;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

/// <summary>
/// Lane 3: sample package carries authored specification + .cs and registers into the kernel.
/// </summary>
public sealed class OpenPackageBuyOnceSampleTests
{
    [Fact]
    public void WritePackage_install_and_register_compiles_into_strategy_kernel_registry()
    {
        var root = Path.Combine(Path.GetTempPath(), "daxalgo-buy-once-sample", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var packagePath = Path.Combine(root, $"buy-once{DaxPackage.StrategyExtension}");
            var write = OpenPackageBuyOnceSample.WritePackage(packagePath, new InstrumentId(42));
            Assert.True(File.Exists(packagePath), write.Path);

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
            Assert.Equal(AuthoredUnitKindV1.Strategy, found.AuthoredSpecification.Kind);
            Assert.NotNull(found.Create());
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { /* temp */ }
        }
    }

    [Fact]
    public void WriteContentTree_emits_specification_and_cs_paths_required_by_registrar()
    {
        var contentRoot = Path.Combine(Path.GetTempPath(), "daxalgo-buy-once-content", Guid.NewGuid().ToString("N"));
        try
        {
            OpenPackageBuyOnceSample.WriteContentTree(contentRoot, new InstrumentId(7));
            Assert.True(File.Exists(Path.Combine(contentRoot, "authored", "unit.specification.v1.json")));
            Assert.True(File.Exists(Path.Combine(contentRoot, "GeneratedPaperStrategy.cs")));
        }
        finally
        {
            try { Directory.Delete(contentRoot, recursive: true); } catch { /* temp */ }
        }
    }
}
