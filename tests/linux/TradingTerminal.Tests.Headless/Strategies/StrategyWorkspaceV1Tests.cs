using TradingTerminal.Core.Strategies.Generation;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class StrategyWorkspaceV1Tests
{
    private static readonly string A = new('a', 64);
    private static readonly string B = new('b', 64);
    private static readonly string C = new('c', 64);
    private static readonly string D = new('d', 64);
    private static readonly string E = new('e', 64);
    private static readonly string F = new('f', 64);

    [Fact]
    public void New_workspace_exposes_all_six_truthful_stages()
    {
        var workspace = StrategyWorkspaceRevisionPolicyV1.Create(
            "btc-breakout",
            new StrategyWorkspaceBindingsV1(BriefHashSha256: A),
            researchRequirement: StrategyWorkspaceStageRequirementV1.Required,
            designRequirement: StrategyWorkspaceStageRequirementV1.Optional);

        Assert.Equal(1, workspace.Revision);
        Assert.Null(workspace.PreviousRevisionHashSha256);
        Assert.Equal(Enum.GetValues<StrategyWorkspaceStageV1>(), workspace.Stages.Select(stage => stage.Stage));
        Assert.Equal(StrategyWorkspaceStageStateV1.Completed, workspace.Stage(StrategyWorkspaceStageV1.Brief).State);
        Assert.Equal(StrategyWorkspaceStageStateV1.NeedsReview, workspace.Stage(StrategyWorkspaceStageV1.Research).State);
        Assert.Equal(StrategyWorkspaceStageStateV1.Pending, workspace.Stage(StrategyWorkspaceStageV1.Design).State);
        Assert.Equal(StrategyWorkspaceStageStateV1.Unavailable, workspace.Stage(StrategyWorkspaceStageV1.Validate).State);
        Assert.Equal(StrategyWorkspaceStageStateV1.Unavailable, workspace.Stage(StrategyWorkspaceStageV1.Paper).State);
    }

    [Fact]
    public void Semantic_feature_change_invalidates_strategy_build_validation_and_paper()
    {
        var current = CompleteWorkspace();
        var requested = current.Bindings with { FeatureSetHashSha256 = F };

        var revised = StrategyWorkspaceRevisionPolicyV1.Revise(
            current,
            StrategyWorkspaceChangeKindV1.DatasetOrFeatureDefinition,
            requested,
            StrategyWorkspaceStageV1.Research,
            revisionReason: "Changed trade-intensity window");

        Assert.Equal(2, revised.Revision);
        Assert.Equal(StrategyWorkspaceCanonicalJsonV1.Hash(current), revised.PreviousRevisionHashSha256);
        Assert.Equal(F, revised.Bindings.FeatureSetHashSha256);
        Assert.Null(revised.Bindings.ConfirmedIntentHashSha256);
        Assert.Null(revised.Bindings.AuthoredUnitSpecificationHashSha256);
        Assert.Null(revised.Bindings.BuildArtifactHashSha256);
        Assert.Null(revised.Bindings.ValidationEvidenceHashSha256);
        Assert.Null(revised.Bindings.PaperBindingHashSha256);
        Assert.Equal(StrategyWorkspaceStageStateV1.Unavailable, revised.Stage(StrategyWorkspaceStageV1.Validate).State);
    }

    [Fact]
    public void Appearance_change_preserves_semantic_validation_but_requires_new_executable()
    {
        var current = CompleteWorkspace();
        var requested = current.Bindings with { AppearanceHashSha256 = F };

        var revised = StrategyWorkspaceRevisionPolicyV1.Revise(
            current,
            StrategyWorkspaceChangeKindV1.AppearanceOnly,
            requested,
            StrategyWorkspaceStageV1.Design,
            revisionReason: "Changed candle color");

        Assert.Equal(F, revised.Bindings.AppearanceHashSha256);
        Assert.Equal(C, revised.Bindings.ConfirmedIntentHashSha256);
        Assert.Equal(E, revised.Bindings.ValidationEvidenceHashSha256);
        Assert.Null(revised.Bindings.AuthoredUnitSpecificationHashSha256);
        Assert.Null(revised.Bindings.BuildArtifactHashSha256);
        Assert.Null(revised.Bindings.PaperBindingHashSha256);
        Assert.Equal(StrategyWorkspaceStageStateV1.Completed, revised.Stage(StrategyWorkspaceStageV1.Validate).State);
        Assert.Equal(StrategyWorkspaceStageStateV1.Pending, revised.Stage(StrategyWorkspaceStageV1.Build).State);
        Assert.Equal(StrategyWorkspaceStageStateV1.Pending, revised.Stage(StrategyWorkspaceStageV1.Paper).State);
    }

    [Fact]
    public void Canonical_round_trip_preserves_revision_and_rejects_tampered_stage_state()
    {
        var workspace = CompleteWorkspace();
        var json = StrategyWorkspaceCanonicalJsonV1.Serialize(workspace);

        var restored = StrategyWorkspaceCanonicalJsonV1.Deserialize(json);

        Assert.Equal(workspace.WorkspaceId, restored.WorkspaceId);
        Assert.Equal(workspace.Revision, restored.Revision);
        Assert.Equal(workspace.Bindings, restored.Bindings);
        Assert.Equal(workspace.Stages, restored.Stages);
        Assert.Equal(StrategyWorkspaceCanonicalJsonV1.Hash(workspace), StrategyWorkspaceCanonicalJsonV1.Hash(restored));

        var tampered = workspace with
        {
            Stages = workspace.Stages.Select(stage => stage.Stage == StrategyWorkspaceStageV1.Paper
                ? stage with { State = StrategyWorkspaceStageStateV1.Unavailable }
                : stage).ToArray(),
        };
        Assert.Contains(
            StrategyWorkspaceRevisionPolicyV1.Validate(tampered),
            issue => issue.Code == "WORKSPACE_STAGE_STATE_MISMATCH");
    }

    [Fact]
    public void Historical_validation_evidence_round_trips_and_rejects_invalid_artifact_bindings()
    {
        var evidence = new HistoricalValidationEvidenceV1(
            HistoricalValidationEvidenceV1.CurrentSchemaVersion,
            new HistoricalValidationContextV1("workspace", A, B, C),
            D,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            "BarSynthetic",
            "Completed broker bars with synthetic L1 fills.",
            12,
            100_000d,
            101_250d,
            42d,
            new DateTime(2026, 2, 1, 0, 1, 0, DateTimeKind.Utc));

        var restored = HistoricalValidationEvidenceCanonicalJsonV1.Deserialize(
            HistoricalValidationEvidenceCanonicalJsonV1.Serialize(evidence));

        Assert.Equal(evidence, restored);
        Assert.Equal(
            HistoricalValidationEvidenceCanonicalJsonV1.Hash(evidence),
            HistoricalValidationEvidenceCanonicalJsonV1.Hash(restored));
        Assert.Throws<ArgumentException>(() => HistoricalValidationEvidenceValidatorV1.RequireValid(
            evidence with { Context = evidence.Context with { BuildArtifactHashSha256 = "stale" } }));
    }

    private static StrategyWorkspaceRevisionV1 CompleteWorkspace() =>
        StrategyWorkspaceRevisionPolicyV1.Create(
            "complete-workspace",
            new StrategyWorkspaceBindingsV1(
                BriefHashSha256: A,
                ResearchCaseHashSha256: B,
                ConfirmedIntentHashSha256: C,
                DrawingSemanticsHashSha256: D,
                AppearanceHashSha256: D,
                AuthoredUnitSpecificationHashSha256: A,
                BuildArtifactHashSha256: B,
                ValidationEvidenceHashSha256: E,
                PaperBindingHashSha256: C),
            activeStage: StrategyWorkspaceStageV1.Paper,
            researchRequirement: StrategyWorkspaceStageRequirementV1.Required,
            designRequirement: StrategyWorkspaceStageRequirementV1.Optional);
}
