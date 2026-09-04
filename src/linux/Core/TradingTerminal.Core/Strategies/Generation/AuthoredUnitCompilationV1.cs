using System.Reflection;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Parameters;

namespace TradingTerminal.Core.Strategies.Generation;

/// <summary>The verified emitted assembly for one exact authored-unit specification.</summary>
public sealed record CompiledAuthoredUnitV1(
    byte[] Image,
    Assembly Assembly,
    Type RuntimeType,
    AuthoredUnitKindV1 Kind,
    string SpecificationHashSha256,
    StrategyParameterSchema Schema,
    StrategyDataRequirement DataRequirement);

public sealed record AuthoredUnitCompilationResultV1(
    bool Success,
    CompiledAuthoredUnitV1? Unit,
    IReadOnlyList<StrategyDiagnostic> Diagnostics)
{
    public IEnumerable<StrategyDiagnostic> Errors =>
        Diagnostics.Where(static diagnostic => diagnostic.Severity == StrategyDiagnosticSeverity.Error);

    public static AuthoredUnitCompilationResultV1 Failed(IReadOnlyList<StrategyDiagnostic> diagnostics) =>
        new(false, null, diagnostics);

    public static AuthoredUnitCompilationResultV1 Succeeded(
        CompiledAuthoredUnitV1 unit,
        IReadOnlyList<StrategyDiagnostic> diagnostics) =>
        new(true, unit, diagnostics);
}

/// <summary>
/// Compiles untrusted generated source and proves that the resulting runtime contract implements the
/// exact reviewed specification before it may be installed or launched.
/// </summary>
public interface IAuthoredUnitCompilerV1
{
    AuthoredUnitCompilationResultV1 Compile(
        AuthoredUnitSpecificationV1 specification,
        StrategyScript script);
}
