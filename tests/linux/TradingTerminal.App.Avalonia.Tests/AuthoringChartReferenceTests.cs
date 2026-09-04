using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using TradingTerminal.App.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class AuthoringChartReferenceTests
{
    [Fact]
    public async Task Import_copies_and_hash_binds_the_exact_reference_artifact()
    {
        var directory = NewDirectory();
        var source = Path.Combine(directory, "source-chart.png");
        var artifacts = Path.Combine(directory, "artifacts");
        var bytes = Encoding.UTF8.GetBytes("not-real-png-but-exact-reference-bytes");
        await File.WriteAllBytesAsync(source, bytes);

        try
        {
            var repository = new FileAuthoringChartReferenceRepository(artifacts);
            var imported = await repository.ImportAsync(
                source,
                ChartReferenceSimilarityV1.IndicatorComposition);
            var expectedHash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            imported.Reference.ReferenceId.Should().Be($"chart-ref-{expectedHash[..16]}");
            imported.Reference.ContentHashSha256.Should().Be(expectedHash);
            imported.Reference.MediaType.Should().Be("image/png");
            imported.Reference.Similarity.Should().Equal(
                ChartReferenceSimilarityV1.IndicatorComposition);
            imported.OriginalFileName.Should().Be("source-chart.png");
            imported.ArtifactPath.Should().NotBe(source);
            (await File.ReadAllBytesAsync(imported.ArtifactPath)).Should().Equal(bytes);
            repository.Exists(imported).Should().BeTrue();

            await File.WriteAllTextAsync(imported.ArtifactPath, "modified");
            repository.Exists(imported).Should().BeFalse(
                "a later stage must never analyze bytes different from the saved SHA-256 identity");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("chart.svg")]
    [InlineData("chart.txt")]
    [InlineData("chart.exe")]
    public async Task Import_rejects_non_chart_file_types(string fileName)
    {
        var directory = NewDirectory();
        var source = Path.Combine(directory, fileName);
        await File.WriteAllTextAsync(source, "content");

        try
        {
            var repository = new FileAuthoringChartReferenceRepository(Path.Combine(directory, "artifacts"));
            var act = () => repository.ImportAsync(source, ChartReferenceSimilarityV1.VisualStyle);

            await act.Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*PNG, JPEG, WebP, PDF, or DaxAlgo chart JSON*");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Authoring_surface_requires_an_explicit_similarity_meaning()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            "src/linux/Shell/TradingTerminal.App.Avalonia/Settings/StrategyAuthoringWindow.axaml");
        var xaml = File.ReadAllText(path);

        xaml.Should().Contain("Reference chart");
        xaml.Should().Contain("Appearance &amp; layout");
        xaml.Should().Contain("Indicators and overlays");
        xaml.Should().Contain("Historical price pattern");
        xaml.Should().Contain("Find related instruments / indexes");
        xaml.Should().Contain("CommandParameter=\"MarketPattern\"");
        xaml.Should().Contain("CommandParameter=\"RelatedInstruments\"");
    }

    private static string NewDirectory()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "daxalgo-chart-reference-tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TradingTerminal.Mac.slnx")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
