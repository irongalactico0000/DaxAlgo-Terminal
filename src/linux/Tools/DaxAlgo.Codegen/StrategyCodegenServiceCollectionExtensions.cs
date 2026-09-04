using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TradingTerminal.Core.Configuration;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.Infrastructure.Strategies.Authoring;

/// <summary>Wires the AI Strategy Builder into DI. Called once per shell from <c>AddStrategyPlugins</c>.</summary>
public static class StrategyCodegenServiceCollectionExtensions
{
    /// <summary>
    /// Registers the codegen backend: the bound <see cref="AiCodegenOptions"/>, the embedded context
    /// pack, the build-loop orchestrator, the provider factory, and <see cref="IAiStrategyBuilder"/>.
    /// Keys are resolved through <see cref="IAiKeyResolver"/> — a shell that can read its credential
    /// store registers one before calling this; otherwise the <see cref="IAiKeyResolver.Null"/> fallback
    /// leaves only keyless providers (installed agent CLIs, local Ollama) usable, which is a valid state.
    /// <see cref="StrategyCodegenOrchestrator"/> depends on <c>IStrategyCompiler</c>, already registered
    /// by <c>AddStrategyPlugins</c>.
    /// </summary>
    public static IServiceCollection AddStrategyCodegen(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiCodegenOptions>(configuration.GetSection(AiCodegenOptions.SectionName));
        services.TryAddSingleton(IAiKeyResolver.Null);

        // One HttpClient per keyed request via the factory (short-lived; codegen calls are infrequent).
        services.AddHttpClient();

        services.AddSingleton(_ => StrategyContextPack.Load());
        // On-demand domain packs (order flow, quant math, risk/exits, live window, instruments). Selected
        // once per conversation from the brief, so the system prompt stays cacheable.
        services.AddSingleton(_ => StrategySkillLibrary.Load());
        services.AddSingleton<StrategyCodegenOrchestrator>();
        services.TryAddSingleton<IStrategyGenerationAgentRouterV1, SameProviderStrategyGenerationAgentRouterV1>();
        services.AddSingleton<StrategyCandidateGenerationOrchestratorV1>();
        services.AddSingleton<IStrategyCandidateGeneratorV1>(sp =>
            sp.GetRequiredService<StrategyCandidateGenerationOrchestratorV1>());
        services.AddSingleton<ChartReferenceInspectorV1>();
        services.AddSingleton<IChartReferenceInspectorV1>(sp =>
            sp.GetRequiredService<ChartReferenceInspectorV1>());
        services.AddSingleton<AuthoredUnitSpecificationGeneratorV1>();
        services.AddSingleton<IAuthoredUnitSpecificationGeneratorV1>(sp =>
            sp.GetRequiredService<AuthoredUnitSpecificationGeneratorV1>());
        services.AddSingleton<AuthoredUnitIntentClassifierV1>();
        services.AddSingleton<IAuthoredUnitIntentClassifierV1>(sp =>
            sp.GetRequiredService<AuthoredUnitIntentClassifierV1>());
        services.AddSingleton<AuthoredUnitSourceGeneratorV1>();
        services.AddSingleton<IAuthoredUnitSourceGeneratorV1>(sp =>
            sp.GetRequiredService<AuthoredUnitSourceGeneratorV1>());
        services.AddSingleton<IStrategyGenerationLaneAgentV1>(
            sp => new StrategyGenerationLaneAgentV1(
                StrategyGenerationLaneV1.VibePython,
                sp.GetService<IStrategyIntentExtensionRegistryV1>()));
        services.AddSingleton<IStrategyGenerationLaneAgentV1>(
            sp => new StrategyGenerationLaneAgentV1(
                StrategyGenerationLaneV1.DeclarativeSpec,
                sp.GetService<IStrategyIntentExtensionRegistryV1>()));
        services.AddSingleton<IStrategyGenerationLaneAgentV1>(
            sp => new StrategyGenerationLaneAgentV1(
                StrategyGenerationLaneV1.TypedGraph,
                sp.GetService<IStrategyIntentExtensionRegistryV1>()));
        services.AddSingleton<IStrategyGenerationLaneAgentV1>(
            sp => new StrategyGenerationLaneAgentV1(
                StrategyGenerationLaneV1.CspPython,
                sp.GetService<IStrategyIntentExtensionRegistryV1>()));
        services.AddSingleton<ParallelStrategyCandidateGeneratorV1>();
        services.AddSingleton<IParallelStrategyCandidateGeneratorV1>(sp =>
            sp.GetRequiredService<ParallelStrategyCandidateGeneratorV1>());
        services.AddSingleton<TradeIrCandidateSynthesizerV1>();
        services.AddSingleton<ITradeIrCandidateSynthesizerV1>(sp =>
            sp.GetRequiredService<TradeIrCandidateSynthesizerV1>());
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiCodegenOptions>>().Value;
            var keys = sp.GetRequiredService<IAiKeyResolver>();
            var httpFactory = sp.GetRequiredService<IHttpClientFactory>();
            return new StrategyCodegenClientFactory(() => httpFactory.CreateClient("ai-codegen"), options, keys.Resolve);
        });
        services.AddSingleton(sp =>
            new AiStrategyBuilder(
                sp.GetRequiredService<StrategyCodegenClientFactory>(),
                sp.GetRequiredService<StrategyCodegenOrchestrator>(),
                sp.GetRequiredService<StrategyContextPack>(),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<AiCodegenOptions>>().Value));
        services.AddSingleton<IAiStrategyBuilder>(sp => sp.GetRequiredService<AiStrategyBuilder>());

        // The interactive escape hatch: scaffolds a Vibe Quant workspace (context pack + skill packs +
        // starter project) and opens an installed agent CLI in a real terminal there.
        services.AddSingleton<ICliWorkspaceLauncher, CliWorkspaceLauncher>();

        return services;
    }
}
