using System.Text;
using System.Text.RegularExpressions;
using DaxAlgo.Package;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.App.Plugins;

/// <summary>
/// Lane 3: export a registered authored unit as an installable <c>.daxalgostrategy</c>
/// (specification + sources). Same contract as <see cref="OpenPackageBuyOnceSample"/>.
/// </summary>
public static class AuthoredUnitOpenPackageExporter
{
    private static readonly Regex TypeName = new(
        @"\b(?:public\s+)?(?:sealed\s+)?class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryWrite(
        string outputPath,
        AuthoredUnitSpecificationV1 specification,
        IReadOnlyList<StrategyFile> sources,
        out string message,
        string? publisher = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(specification);
        ArgumentNullException.ThrowIfNull(sources);

        if (sources.Count == 0 || sources.All(static f => string.IsNullOrWhiteSpace(f.Content)))
        {
            message = "Export needs at least one C# source file.";
            return false;
        }

        var launchIssues = AuthoredUnitSpecificationValidatorV1.ValidateForLaunch(specification);
        if (launchIssues.Count > 0)
        {
            message = $"Specification is not launch-valid: {launchIssues[0].Message}";
            return false;
        }

        if (!outputPath.EndsWith(DaxPackage.StrategyExtension, StringComparison.OrdinalIgnoreCase))
            outputPath += DaxPackage.StrategyExtension;

        var entryType = ResolveEntryTypeName(sources);
        var payloads = new List<DaxPayloadSource>
        {
            DaxPayloadSource.FromBytes(
                OpenPackageHostRegistrar.SpecificationRelativePath,
                DaxPayloadRole.Resource,
                Encoding.UTF8.GetBytes(AuthoredUnitSpecificationCanonicalJsonV1.Serialize(specification))),
        };
        foreach (var file in sources)
        {
            if (string.IsNullOrWhiteSpace(file.Content)) continue;
            var logical = file.Name.Replace('\\', '/').TrimStart('/');
            if (!logical.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                logical += ".cs";
            payloads.Add(DaxPayloadSource.FromBytes(logical, DaxPayloadRole.Source, Encoding.UTF8.GetBytes(file.Content)));
        }

        try
        {
            var write = DaxPackage.Write(outputPath, new DaxPackageRequest
            {
                Id = specification.UnitId,
                Version = "1.0.0",
                DisplayName = specification.Name,
                Publisher = string.IsNullOrWhiteSpace(publisher) ? "builder-export" : publisher.Trim(),
                EntryTypeName = entryType,
                Kind = DaxPackageKind.Strategy,
                Description = specification.RawRequest,
                Payloads = payloads,
            });
            message =
                $"Exported open package to {write.Path ?? outputPath}. " +
                "Lane 3 · Install via Strategy Manager or --install-open-package= (your registry/keys only).";
            return true;
        }
        catch (Exception exception)
        {
            message = $"Export failed: {exception.Message}";
            return false;
        }
    }

    public static string ResolveEntryTypeName(IReadOnlyList<StrategyFile> sources)
    {
        foreach (var file in sources)
        {
            var match = TypeName.Match(file.Content ?? string.Empty);
            if (match.Success)
                return match.Groups["name"].Value;
        }

        var fromName = Path.GetFileNameWithoutExtension(sources[0].Name);
        if (!string.IsNullOrWhiteSpace(fromName) &&
            Regex.IsMatch(fromName, @"^[A-Za-z_][A-Za-z0-9_]*$"))
            return fromName;

        return "AuthoredStrategy";
    }
}
