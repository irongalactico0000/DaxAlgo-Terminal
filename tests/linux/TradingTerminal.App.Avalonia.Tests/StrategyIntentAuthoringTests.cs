using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingTerminal.App.Authoring;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Core.Strategies.Specification;
using TradingTerminal.Infrastructure.Backtest;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class StrategyIntentAuthoringTests
{
    [Fact]
    public void Expert_C_sharp_keeps_chat_beside_code_without_semantic_confirmation()
    {
        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: new EmptySessionRepository());

        viewModel.IsDesignScreen.Should().BeTrue();
        viewModel.ToggleGenerationModeCommand.Execute(null);

        viewModel.GenerateCandidateFirst.Should().BeFalse();
        viewModel.IsDesignScreen.Should().BeTrue();
        viewModel.ShowScreenNavigation.Should().BeFalse();
        viewModel.ShowDesignRequestHeader.Should().BeFalse();
        viewModel.ShowImplementationHeader.Should().BeTrue();
        viewModel.WorkbenchTab.Should().Be(0);
        viewModel.HasExpertCSharpFiles.Should().BeTrue();
        viewModel.CanCompileCurrentSource.Should().BeTrue();

        viewModel.Files[0].Name = "Strategy.py";
        viewModel.CanCompileCurrentSource.Should().BeFalse();
        viewModel.Files[0].Name = "Strategy.cs";
        viewModel.CanCompileCurrentSource.Should().BeTrue();

        viewModel.HasDetachedImplementationSource = true;
        viewModel.CanCompileCurrentSource.Should().BeFalse();

        viewModel.ToggleGenerationModeCommand.Execute(null);

        viewModel.GenerateCandidateFirst.Should().BeTrue();
        viewModel.IsDesignScreen.Should().BeTrue();
        viewModel.WorkbenchTab.Should().Be(3);
    }

    [Fact]
    public void Restored_expert_session_reopens_chat_and_code_without_a_strategy_request()
    {
        var repository = new RecordingSessionRepository();
        repository.Save(new AuthoringSessionSnapshot(
            "expert-restored",
            "Expert restored",
            [new AuthoringChatEntry(AuthoringChatEntry.User, "Keep editing this C# strategy.", DateTime.Now)],
            [],
            [new StrategyFile("Strategy.cs", "public sealed class Strategy { }")],
            GenerateCandidateFirst: false,
            ActiveScreen: StrategyAuthoringScreen.Design,
            AuthoringUxVersion: AuthoringSessionSnapshot.CurrentAuthoringUxVersion,
            UpdatedUtc: DateTime.UtcNow));

        using var restored = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: repository);

        restored.GenerateCandidateFirst.Should().BeFalse();
        restored.IsDesignScreen.Should().BeTrue();
        restored.ShowImplementationHeader.Should().BeTrue();
        restored.WorkbenchTab.Should().Be(0);
        restored.CanCompileCurrentSource.Should().BeTrue();
    }

    [Fact]
    public void Edited_combined_TradeIR_origin_survives_proof_loss_and_detaches_on_request_change()
    {
        var repository = new RecordingSessionRepository();
        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: repository);
        var candidate = Candidate();
        var research = Research(candidate);
        var classification = Classification();
        var draft = Draft(candidate, research, classification);
        viewModel.CurrentCandidate = candidate;
        viewModel.EditorOriginatedFromCombinedTradeIr = true;

        viewModel.ReviewStrategyIntent(research, classification, draft);

        viewModel.HasDetachedImplementationSource.Should().BeTrue();
        viewModel.EditorOriginatedFromCombinedTradeIr.Should().BeTrue();
        repository.Saved.Should().NotBeNull();
        repository.Saved!.HasDetachedImplementationSource.Should().BeTrue();
        repository.Saved.EditorOriginatedFromCombinedTradeIr.Should().BeTrue();
    }

    [Fact]
    public void Exact_review_confirms_without_provider_and_candidate_change_relocks_the_gate()
    {
        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: new EmptySessionRepository());
        var candidate = Candidate();
        var research = Research(candidate);
        var classification = Classification();
        var draft = Draft(candidate, research, classification);
        viewModel.CurrentCandidate = candidate;

        viewModel.IsDesignScreen.Should().BeTrue();
        viewModel.IsBuildScreen.Should().BeFalse();
        viewModel.WorkbenchTab.Should().Be(3, "Design must select its visible Request tab");
        viewModel.OpenBuildScreenCommand.CanExecute(null).Should().BeFalse();

        viewModel.ReviewStrategyIntent(research, classification, draft);

        viewModel.HasStrategyIntentReview.Should().BeTrue();
        viewModel.CanConfirmStrategyIntentReview.Should().BeTrue();
        viewModel.HasConfirmedStrategyIntent.Should().BeFalse();
        viewModel.StrategyIntentQuestions.Should().BeEmpty();
        viewModel.ConfirmStrategyIntentReviewCommand.Execute(null);
        viewModel.HasConfirmedStrategyIntent.Should().BeTrue();
        viewModel.ConfirmedStrategyIntentHash.Should().MatchRegex("^[0-9a-f]{64}$");
        viewModel.CanConfirmStrategyIntentReview.Should().BeFalse();
        viewModel.CanEnterFourLaneConformance.Should().BeTrue();
        viewModel.CanGenerateStrategyImplementations.Should().BeTrue();
        viewModel.StrategyIntentStatusText.Should().Contain("No backtest");
        viewModel.StrategyIntentStatusText.Should().Contain("live authority");
        viewModel.OpenBuildScreenCommand.CanExecute(null).Should().BeTrue();

        viewModel.OpenBuildScreenCommand.Execute(null);

        viewModel.IsBuildScreen.Should().BeTrue();
        viewModel.IsDesignScreen.Should().BeFalse();
        viewModel.WorkbenchGridColumn.Should().Be(1);
        viewModel.WorkbenchGridColumnSpan.Should().Be(3);
        viewModel.WorkbenchTab.Should().Be(3);

        viewModel.IsSynthesizingTradeIr = true;
        viewModel.IsGenerating = true;
        viewModel.ShowBuildBusyStop.Should().BeTrue();
        viewModel.ShowBuildGenerationProgress.Should().BeFalse();
        viewModel.IsGenerating = false;

        viewModel.CurrentCandidate = candidate with { Title = "Semantically changed title" };

        viewModel.HasStrategyIntentReview.Should().BeFalse();
        viewModel.HasConfirmedStrategyIntent.Should().BeFalse();
        viewModel.CanEnterFourLaneConformance.Should().BeFalse();
        viewModel.StrategyIntentStatusText.Should().Contain("invalidated");
        viewModel.IsDesignScreen.Should().BeTrue();
        viewModel.IsBuildScreen.Should().BeFalse();
    }

    [Fact]
    public void Missing_signal_publication_uses_current_research_provenance_and_restores_confirmation()
    {
        var repository = new RecordingSessionRepository();
        var candidate = Candidate();
        var research = Research(candidate);
        var classification = Classification();
        var draft = Draft(candidate, research, classification) with
        {
            Requirements = Draft(candidate, research, classification).Requirements
                .Where(requirement => requirement.RequirementId != "decision.signal_publication")
                .ToArray(),
        };
        using (var viewModel = new StrategyAuthoringViewModel(
                   new StubCompiler(),
                   new StubRegistry(),
                   NullLogger<StrategyAuthoringViewModel>.Instance,
                   sessionRepository: repository))
        {
            viewModel.StrategyId = candidate.CandidateId;
            viewModel.CurrentCandidate = candidate;

            viewModel.ReviewStrategyIntent(research, classification, draft);

            viewModel.CanConfirmStrategyIntentReview.Should().BeFalse();
            viewModel.StrategyIntentQuestions.Should().ContainSingle(question =>
                question.RequirementId == "decision.signal_publication");
            var publication = viewModel.StrategyIntentRequirements.Should().ContainSingle(row =>
                row.RequirementId == "decision.signal_publication" &&
                row.SelectedApplicability.Disposition == StrategySemanticDispositionV1.Unresolved).Subject;
            viewModel.ConfirmStrategyIntentReviewCommand.CanExecute(null).Should().BeFalse();
            viewModel.HasConfirmedStrategyIntent.Should().BeFalse();

            publication.Answer = "Publish value, confidence, audience, and a one-bar expiry.";

            var answered = viewModel.StrategyIntentDraft!.Requirements.Single(requirement =>
                requirement.RequirementId == publication.RequirementId);
            answered.Provenance.Should().BeEquivalentTo(new StrategyRequirementProvenanceV1(
                ["rule-signal"],
                ["evidence-signal"],
                "Entered and reviewed by the user in Strategy request review."));
            answered.Provenance!.ResearchEvidenceIds.Should().NotContain("review-evidence");
            viewModel.StrategyIntentIssues.Should().NotContain(issue =>
                issue.Code == "INTENT_EVIDENCE_REFERENCE_UNKNOWN");
            viewModel.StrategyIntentQuestions.Should().BeEmpty();
            viewModel.CanConfirmStrategyIntentReview.Should().BeTrue();

            viewModel.ConfirmStrategyIntentReviewCommand.Execute(null);

            viewModel.HasConfirmedStrategyIntent.Should().BeTrue();
            repository.Saved.Should().NotBeNull();
            var savedDraft = StrategyIntentCanonicalJsonV1.DeserializeDraft(
                repository.Saved!.StrategyIntentDraftJson!);
            savedDraft.Requirements.Single(requirement =>
                    requirement.RequirementId == publication.RequirementId)
                .Provenance.Should().BeEquivalentTo(answered.Provenance);
        }

        using var restored = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: repository);
        restored.HasConfirmedStrategyIntent.Should().BeTrue();
        restored.CanEnterFourLaneConformance.Should().BeTrue();
        restored.StrategyIntentDraft!.Requirements.Single(requirement =>
                requirement.RequirementId == "decision.signal_publication")
            .Provenance!.ResearchEvidenceIds.Should().Equal("evidence-signal");
    }

    [Fact]
    public void Local_signal_only_interview_requires_research_and_answers_but_explicitly_marks_order_work_not_relevant()
    {
        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: new EmptySessionRepository());
        var candidate = Candidate();
        var profile = new StrategyStarterBrief(
            "signal-review-profile",
            "Signal review",
            "Publish a signal without orders.",
            "Publish a point-in-time signal.",
            Classification(),
            []);
        viewModel.AddStrategyIntentProfile(profile);
        viewModel.SelectStrategyIntentProfile(profile);
        viewModel.CurrentCandidate = candidate;

        viewModel.BeginStrategyIntentReview();

        viewModel.HasStrategyIntentReview.Should().BeTrue();
        viewModel.SelectedStrategyIntentShape!.Kind.Should().Be(StrategyIntentKindV1.SignalOnly);
        viewModel.CanConfirmStrategyIntentReview.Should().BeFalse();
        viewModel.StrategyIntentRequirements.Should().NotBeEmpty();
        viewModel.StrategyIntentRequirements.Where(row => row.MustBeNotApplicable)
            .Should().HaveCount(4)
            .And.OnlyContain(row =>
                row.SelectedApplicability.Disposition == StrategySemanticDispositionV1.NotApplicable &&
                !row.CanChangeApplicability);
        viewModel.StrategyIntentIssues.Should().Contain(issue =>
            issue.Code == "RESEARCH_EVIDENCE_DESCRIPTION_REQUIRED");

        viewModel.StrategyResearchEvidence = "Completed bars and point-in-time comparison data.";
        viewModel.StrategyResearchPointInTimeRule = "Only completed inputs at or before publication time.";
        viewModel.StrategyResearchQualificationRule = "Publish only after confirmation; otherwise publish nothing.";
        viewModel.StrategyResearchFalsifier = "Reject if the signal cannot be reproduced without future data.";
        foreach (var row in viewModel.StrategyIntentRequirements.Where(row => !row.MustBeNotApplicable))
            row.Answer = $"Reviewed answer for {row.Question}";

        viewModel.StrategyIntentQuestions.Should().BeEmpty();
        viewModel.StrategyIntentIssues.Should().BeEmpty();
        viewModel.CanConfirmStrategyIntentReview.Should().BeTrue();
        viewModel.ConfirmStrategyIntentReviewCommand.Execute(null);
        viewModel.HasConfirmedStrategyIntent.Should().BeTrue();
        viewModel.CanConfirmStrategyIntentReview.Should().BeFalse();

        viewModel.StrategyIntentRequirements.First(row => !row.MustBeNotApplicable).Answer += " changed";

        viewModel.HasConfirmedStrategyIntent.Should().BeFalse();
        viewModel.CanGenerateStrategyImplementations.Should().BeFalse();
        viewModel.StrategyIntentStatusText.Should().Contain("ready for your confirmation");
    }

    [Fact]
    public void Cross_asset_chart_confirmation_stays_directional_while_risk_parity_is_a_portfolio()
    {
        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: new EmptySessionRepository());
        var directionalBrief = new StrategyStarterBrief(
            "cross-asset-directional-review",
            "Chart move confirmed by an index",
            "Trade one directional target only after a comparison index confirms the chart move.",
            "Use the selected chart and its comparison index to decide long, short, or no trade.",
            CrossAssetDirectionalClassification(),
            ["chart confirmation", "comparison index"]);

        viewModel.SelectStrategyIntentProfile(directionalBrief);
        viewModel.CurrentCandidate = Candidate();
        viewModel.BeginStrategyIntentReview();

        viewModel.SelectedStrategyIntentShape!.Kind.Should().Be(StrategyIntentKindV1.PositionTarget);
        viewModel.StrategyIntentFamilyText.Should().Contain("Directional");
        viewModel.StrategyIntentRequirements.Should().Contain(row =>
            row.RequirementId == "evidence.comparison_inputs");
        viewModel.StrategyIntentRequirements.Should().Contain(row =>
            row.RequirementId == "evidence.cross_instrument");

        var riskParity = viewModel.AllStarterBriefs.Single(brief =>
            brief.Id == "starter.macro-risk-parity");
        viewModel.SelectStrategyIntentProfile(riskParity);

        viewModel.SelectedStrategyIntentShape!.Kind.Should().Be(StrategyIntentKindV1.PortfolioTarget);
        viewModel.StrategyIntentFamilyText.Should().Contain("Portfolio");
    }

    [Fact]
    public void Confirmed_build_screen_is_persisted_and_restored_only_with_its_valid_request()
    {
        var repository = new RecordingSessionRepository();
        var candidate = Candidate();
        var research = Research(candidate);
        var classification = Classification();
        var draft = Draft(candidate, research, classification);

        using (var viewModel = new StrategyAuthoringViewModel(
                   new StubCompiler(),
                   new StubRegistry(),
                   NullLogger<StrategyAuthoringViewModel>.Instance,
                   sessionRepository: repository))
        {
            viewModel.StrategyId = candidate.CandidateId;
            viewModel.CurrentCandidate = candidate;
            viewModel.ReviewStrategyIntent(research, classification, draft);
            viewModel.ConfirmStrategyIntentReviewCommand.Execute(null);
            viewModel.OpenBuildScreenCommand.Execute(null);

            repository.Saved.Should().NotBeNull();
            repository.Saved!.ActiveScreen.Should().Be(StrategyAuthoringScreen.Build);
            repository.Saved.StrategyWorkspaceJson.Should().NotBeNullOrWhiteSpace();
            var workspace = StrategyWorkspaceCanonicalJsonV1.Deserialize(repository.Saved.StrategyWorkspaceJson!);
            workspace.ActiveStage.Should().Be(StrategyWorkspaceStageV1.Build);
            workspace.Bindings.ResearchCaseHashSha256.Should().Be(ResearchCaseCanonicalJsonV1.Hash(research));
            workspace.Bindings.ConfirmedIntentHashSha256.Should().Be(viewModel.ConfirmedStrategyIntentHash);
            workspace.Stage(StrategyWorkspaceStageV1.Validate).State
                .Should().Be(StrategyWorkspaceStageStateV1.Unavailable);
        }

        using var restored = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: repository);

        restored.HasConfirmedStrategyIntent.Should().BeTrue();
        restored.CanEnterFourLaneConformance.Should().BeTrue();
        restored.IsBuildScreen.Should().BeTrue();
        restored.IsBuildStage.Should().BeTrue();
        restored.StrategyWorkspace.ActiveStage.Should().Be(StrategyWorkspaceStageV1.Build);
        restored.StrategyWorkspace.PreviousRevisionHashSha256.Should().MatchRegex("^[0-9a-f]{64}$");
        restored.WorkbenchGridColumn.Should().Be(1);
        restored.WorkbenchGridColumnSpan.Should().Be(3);
    }

    [Fact]
    public void Restore_relocks_when_displayed_draft_differs_from_saved_confirmation()
    {
        var repository = new RecordingSessionRepository();
        var candidate = Candidate();
        var research = Research(candidate);
        var classification = Classification();
        var draft = Draft(candidate, research, classification);

        using (var original = new StrategyAuthoringViewModel(
                   new StubCompiler(),
                   new StubRegistry(),
                   NullLogger<StrategyAuthoringViewModel>.Instance,
                   sessionRepository: repository))
        {
            original.StrategyId = candidate.CandidateId;
            original.CurrentCandidate = candidate;
            original.ReviewStrategyIntent(research, classification, draft);
            original.ConfirmStrategyIntentReviewCommand.Execute(null);
        }

        var saved = repository.Saved!;
        var changedRequirements = draft.Requirements
            .Select((requirement, index) => index == 0
                ? requirement with
                {
                    Description = "A different displayed trigger was restored.",
                    Value = new StrategyCandidateValueV1(
                        "core.semantic_clause@1",
                        "A different displayed trigger was restored."),
                }
                : requirement)
            .ToArray();
        repository.Save(saved with
        {
            StrategyIntentDraftJson = StrategyIntentCanonicalJsonV1.Serialize(
                draft with { Requirements = changedRequirements }),
        });

        using var restored = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: repository);

        restored.HasStrategyIntentReview.Should().BeTrue();
        restored.HasConfirmedStrategyIntent.Should().BeFalse();
        restored.CanEnterFourLaneConformance.Should().BeFalse();
        restored.StrategyIntentIssues.Should().Contain(issue =>
            issue.Code == "CONFIRMED_INTENT_DRAFT_MISMATCH");
        restored.StrategyIntentStatusText.Should().Contain("relocked");
    }

    [Fact]
    public void Rich_research_and_typed_requirement_round_trip_without_hidden_loss()
    {
        var repository = new RecordingSessionRepository();
        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: repository,
            strategyIntentExtensionRegistry: new StubIntentExtensionRegistry(
                (StrategyIntentExtensionRoleV1.Requirement, "daxalgo.index_threshold@1")));
        var candidate = Candidate();
        var baseResearch = Research(candidate);
        var research = baseResearch with
        {
            EvidenceRequirements =
            [
                .. baseResearch.EvidenceRequirements,
                new ResearchEvidenceRequirementV1(
                    "evidence-index",
                    "A comparison index confirms the primary move.",
                    "Use only the last completed comparison-index bar.",
                    "Require agreement; stale or conflicting evidence means no signal.",
                    false,
                    ["rule-signal"]),
            ],
            Falsifiers =
            [
                .. baseResearch.Falsifiers,
                new ResearchFalsifierV1(
                    "falsifier-index",
                    "Reject when index confirmation disappears out of sample.",
                    false,
                    ["rule-signal"]),
            ],
        };
        var classification = Classification();
        var baseDraft = Draft(candidate, research, classification);
        var preciseProvenance = new StrategyRequirementProvenanceV1(
            ["rule-signal"],
            ["evidence-signal", "evidence-index"],
            "Preserve this exact host-reviewed provenance.");
        var typedRequirement = new StrategySemanticRequirementV1(
            "daxalgo.index_threshold@1",
            StrategySemanticStageV1.QualifyEvidence,
            StrategySemanticDispositionV1.Applicable,
            "Supporting comparison-index threshold reviewed by the user.",
            false,
            preciseProvenance,
            new StrategyCandidateValueV1("core.percentage@1", "0.8", "%"));
        var draft = baseDraft with
        {
            Requirements = [.. baseDraft.Requirements, typedRequirement],
        };
        viewModel.StrategyId = candidate.CandidateId;
        viewModel.CurrentCandidate = candidate;

        viewModel.ReviewStrategyIntent(research, classification, draft);

        viewModel.StrategyResearchEvidenceRows.Should().HaveCount(2);
        viewModel.StrategyResearchFalsifierRows.Should().HaveCount(2);
        viewModel.StrategyResearchEvidenceRows[1].Description =
            "The reviewed comparison index confirms the primary move.";

        viewModel.StrategyIntentDraft.Should().NotBeNull();
        var rebuilt = viewModel.StrategyIntentDraft!.Requirements.Single(requirement =>
            requirement.RequirementId == typedRequirement.RequirementId);
        rebuilt.Description.Should().Be(typedRequirement.Description);
        rebuilt.IsMaterial.Should().BeFalse();
        rebuilt.Provenance.Should().Be(preciseProvenance);
        rebuilt.Value.Should().Be(new StrategyCandidateValueV1("core.percentage@1", "0.8", "%"));
        viewModel.StrategyIntentIssues.Should().BeEmpty();
        viewModel.StrategyIntentQuestions.Should().BeEmpty();
        viewModel.CanConfirmStrategyIntentReview.Should().BeTrue();

        viewModel.ConfirmStrategyIntentReviewCommand.Execute(null);

        viewModel.HasConfirmedStrategyIntent.Should().BeTrue();
        repository.Saved.Should().NotBeNull();
        var savedResearch = ResearchCaseCanonicalJsonV1.Deserialize(repository.Saved!.ResearchCaseJson!);
        savedResearch.EvidenceRequirements.Should().HaveCount(2);
        savedResearch.EvidenceRequirements[1].Should().BeEquivalentTo(new ResearchEvidenceRequirementV1(
            "evidence-index",
            "The reviewed comparison index confirms the primary move.",
            "Use only the last completed comparison-index bar.",
            "Require agreement; stale or conflicting evidence means no signal.",
            false,
            ["rule-signal"]));
        savedResearch.Falsifiers.Should().BeEquivalentTo(research.Falsifiers, options => options.WithStrictOrdering());

        using var restored = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: repository,
            strategyIntentExtensionRegistry: new StubIntentExtensionRegistry(
                (StrategyIntentExtensionRoleV1.Requirement, "daxalgo.index_threshold@1")));
        restored.StrategyResearchEvidenceRows.Should().HaveCount(2);
        restored.StrategyResearchFalsifierRows.Should().HaveCount(2);
        restored.StrategyIntentDraft!.Requirements.Single(requirement =>
                requirement.RequirementId == typedRequirement.RequirementId)
            .Should().BeEquivalentTo(rebuilt);
        restored.HasConfirmedStrategyIntent.Should().BeTrue();
    }

    [Fact]
    public void Governed_extension_id_survives_shape_stashing_restore_and_normal_edits()
    {
        const string extensionA = "example.alpha-intent@1";
        const string extensionB = "example.beta-intent@1";
        var repository = new RecordingSessionRepository();
        var registry = new StubIntentExtensionRegistry(
            (StrategyIntentExtensionRoleV1.IntentModel, extensionA),
            (StrategyIntentExtensionRoleV1.IntentModel, extensionB));
        var candidate = Candidate();
        var research = Research(candidate);
        var classification = Classification();
        var emptyDraft = Draft(candidate, research, classification) with
        {
            IntentModel = new StrategyIntentModelV1(StrategyIntentKindV1.Extension, extensionA),
            Requirements = [],
        };
        var draft = emptyDraft with
        {
            Requirements = StrategyIntentCompletenessV1.Questions(emptyDraft, classification)
                .Select(question => Applicable(
                    question.RequirementId,
                    question.Stage,
                    $"Reviewed extension answer for {question.RequirementId}."))
                .ToArray(),
        };

        using (var original = new StrategyAuthoringViewModel(
                   new StubCompiler(),
                   new StubRegistry(),
                   NullLogger<StrategyAuthoringViewModel>.Instance,
                   sessionRepository: repository,
                   strategyIntentExtensionRegistry: registry))
        {
            original.StrategyId = candidate.CandidateId;
            original.CurrentCandidate = candidate;
            original.ReviewStrategyIntent(research, classification, draft);

            var shapeA = original.SelectedStrategyIntentShape!;
            shapeA.ExtensionId.Should().Be(extensionA);
            original.StrategyIntentIssues.Should().BeEmpty();
            original.CanConfirmStrategyIntentReview.Should().BeTrue();

            original.SelectedStrategyIntentShape = new StrategyIntentShapeOption(
                StrategyIntentKindV1.Extension,
                "Beta extension",
                "A second governed extension context.",
                extensionB);
            original.StrategyIntentDraft!.IntentModel.ExtensionId.Should().Be(extensionB);
            original.StrategyIntentRequirements.Should().OnlyContain(row =>
                string.IsNullOrWhiteSpace(row.Answer));
            original.StrategyIntentRequirements[0].Answer = "Beta-only reviewed answer.";

            original.SelectedStrategyIntentShape = shapeA;
            original.StrategyIntentDraft!.IntentModel.ExtensionId.Should().Be(extensionA);
            original.StrategyIntentRequirements.Should().OnlyContain(row =>
                row.Answer.StartsWith("Reviewed extension answer", StringComparison.Ordinal));
            original.StrategyIntentIssues.Should().BeEmpty();
            original.CanConfirmStrategyIntentReview.Should().BeTrue();
            original.ConfirmStrategyIntentReviewCommand.Execute(null);
            original.HasConfirmedStrategyIntent.Should().BeTrue();
        }

        using var restored = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: repository,
            strategyIntentExtensionRegistry: registry);
        restored.HasConfirmedStrategyIntent.Should().BeTrue();
        restored.StrategyIntentDraft!.IntentModel.ExtensionId.Should().Be(extensionA);

        restored.StrategyResearchHypothesis += " Reviewed again.";

        restored.HasConfirmedStrategyIntent.Should().BeFalse();
        restored.StrategyIntentDraft!.IntentModel.ExtensionId.Should().Be(extensionA);
        restored.StrategyIntentIssues.Should().BeEmpty();
        restored.CanConfirmStrategyIntentReview.Should().BeTrue();
        restored.ConfirmStrategyIntentReviewCommand.Execute(null);
        restored.HasConfirmedStrategyIntent.Should().BeTrue();
    }

    [Fact]
    public void Material_research_item_requires_a_reviewed_resolution_that_persists_and_restores()
    {
        var repository = new RecordingSessionRepository();
        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: repository);
        var candidate = Candidate();
        var research = Research(candidate) with
        {
            UnresolvedItems =
            [
                new ResearchUnresolvedItemV1(
                    "research-choice",
                    "Choose whether stale comparison-index data forces no signal.",
                    true,
                    ["rule-signal"]),
            ],
        };
        var classification = Classification();
        var draft = Draft(candidate, research, classification);
        viewModel.StrategyId = candidate.CandidateId;
        viewModel.CurrentCandidate = candidate;

        viewModel.ReviewStrategyIntent(research, classification, draft);

        viewModel.CanConfirmStrategyIntentReview.Should().BeFalse();
        viewModel.StrategyIntentIssues.Should().Contain(issue =>
            issue.Code == "RESEARCH_MATERIAL_ITEM_UNRESOLVED");
        var unresolved = viewModel.StrategyResearchUnresolvedRows.Should().ContainSingle().Subject;

        unresolved.ResolveCommand.CanExecute(null).Should().BeFalse();
        unresolved.ResolveCommand.Execute(null);
        viewModel.StrategyResearchUnresolvedRows.Should().ContainSingle();
        viewModel.CanConfirmStrategyIntentReview.Should().BeFalse();

        unresolved.Resolution = "Comparison-index data older than one completed bar forces no signal.";
        unresolved.ResolveCommand.CanExecute(null).Should().BeTrue();
        unresolved.ResolveCommand.Execute(null);

        viewModel.StrategyResearchUnresolvedRows.Should().BeEmpty();
        var resolved = viewModel.StrategyResearchResolvedRows.Should().ContainSingle().Subject;
        resolved.OriginalDescription.Should().Be(
            "Choose whether stale comparison-index data forces no signal.");
        resolved.Resolution.Should().Be(
            "Comparison-index data older than one completed bar forces no signal.");
        resolved.ResolutionProvenance.Should().Be(
            "Reviewed and recorded by the user in Strategy request review.");
        viewModel.StrategyIntentIssues.Should().BeEmpty();
        viewModel.CanConfirmStrategyIntentReview.Should().BeTrue();
        repository.Saved.Should().NotBeNull();
        var savedResearch = ResearchCaseCanonicalJsonV1.Deserialize(repository.Saved!.ResearchCaseJson!);
        savedResearch.UnresolvedItems.Should().BeEmpty();
        savedResearch.ResolvedItems.Should().ContainSingle().Which.Should().BeEquivalentTo(
            resolved.ToResolvedItem());

        viewModel.ConfirmStrategyIntentReviewCommand.Execute(null);
        using var restored = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: repository);

        restored.StrategyResearchUnresolvedRows.Should().BeEmpty();
        restored.StrategyResearchResolvedRows.Should().ContainSingle().Which
            .Should().BeEquivalentTo(resolved);
        restored.HasConfirmedStrategyIntent.Should().BeTrue();
    }

    [Fact]
    public void Profile_and_shape_selection_isolate_contexts_and_restore_exact_typed_stashes()
    {
        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: new EmptySessionRepository(),
            strategyIntentExtensionRegistry: new StubIntentExtensionRegistry(
                (StrategyIntentExtensionRoleV1.Requirement, "daxalgo.comparison_threshold@1"),
                (StrategyIntentExtensionRoleV1.ValueType, "daxalgo.order_policy@1"),
                (StrategyIntentExtensionRoleV1.ValueType, "daxalgo.price_move@1")));
        var candidate = Candidate();
        var research = Research(candidate);
        var classification = CrossAssetDirectionalClassification();
        var preciseProvenance = new StrategyRequirementProvenanceV1(
            ["rule-signal"],
            ["evidence-signal"],
            "Preserve this selector-reviewed provenance.");
        var typedTrigger = new StrategySemanticRequirementV1(
            "observe.trigger",
            StrategySemanticStageV1.ObserveOrTrigger,
            StrategySemanticDispositionV1.Applicable,
            "A precisely typed primary-chart trigger.",
            false,
            preciseProvenance,
            new StrategyCandidateValueV1("daxalgo.price_move@1", "0.8", "%"));
        var governedExtension = new StrategySemanticRequirementV1(
            "daxalgo.comparison_threshold@1",
            StrategySemanticStageV1.QualifyEvidence,
            StrategySemanticDispositionV1.Applicable,
            "A governed comparison-index threshold.",
            false,
            preciseProvenance,
            new StrategyCandidateValueV1("core.percentage@1", "0.5", "%"));
        var typedOrderPolicy = new StrategySemanticRequirementV1(
            "execution.order_policy",
            StrategySemanticStageV1.Execution,
            StrategySemanticDispositionV1.Applicable,
            "A typed position-target order policy.",
            false,
            preciseProvenance,
            new StrategyCandidateValueV1("daxalgo.order_policy@1", "one-active-order", "orders"));
        var draft = Draft(candidate, research, classification) with
        {
            IntentModel = new StrategyIntentModelV1(StrategyIntentKindV1.PositionTarget),
            Requirements = [typedTrigger, typedOrderPolicy, governedExtension],
        };
        viewModel.CurrentCandidate = candidate;
        viewModel.ReviewStrategyIntent(research, classification, draft);
        var originalProfile = viewModel.SelectedStrategyIntentProfile!;
        var alternateClassification = classification with
        {
            Id = "cross-asset-directional-alternate",
            Name = "Cross-asset directional alternate",
        };
        var alternateProfile = new StrategyStarterBrief(
            "cross-asset-directional-alternate",
            "Cross-asset directional alternate",
            "Keep the same directional research shape.",
            "Use an alternate profile without losing reviewed answers.",
            alternateClassification,
            []);
        viewModel.AddStrategyIntentProfile(alternateProfile);

        viewModel.SelectStrategyIntentProfile(alternateProfile);
        viewModel.SelectedStrategyIntentShape!.Kind.Should().Be(StrategyIntentKindV1.PositionTarget);
        var firstAlternateTrigger = viewModel.StrategyIntentRequirements.Single(row =>
            row.RequirementId == typedTrigger.RequirementId);
        firstAlternateTrigger.Answer.Should().BeEmpty();
        firstAlternateTrigger.SelectedApplicability.Disposition.Should().Be(
            StrategySemanticDispositionV1.Unresolved);
        viewModel.StrategyIntentRequirements.Should().NotContain(row =>
            row.RequirementId == governedExtension.RequirementId);
        firstAlternateTrigger.Answer = "Alternate-profile trigger";
        viewModel.StrategyIntentRequirements.Single(row =>
            row.RequirementId == typedOrderPolicy.RequirementId).Answer = "alternate-profile-policy";

        viewModel.SelectedStrategyIntentShape = viewModel.StrategyIntentShapes.Single(shape =>
            shape.Kind == StrategyIntentKindV1.MultiLegTarget);

        var firstMultiLegTrigger = viewModel.StrategyIntentRequirements.Single(row =>
            row.RequirementId == typedTrigger.RequirementId);
        firstMultiLegTrigger.Answer.Should().BeEmpty();
        firstMultiLegTrigger.SelectedApplicability.Disposition.Should().Be(
            StrategySemanticDispositionV1.Unresolved);
        viewModel.StrategyIntentDraft!.Requirements.Should().NotContain(requirement =>
            requirement.RequirementId == typedOrderPolicy.RequirementId,
            "inactive built-ins stay in the review-session stash, never the current draft");
        viewModel.StrategyIntentRequirements.Should().NotContain(row =>
            row.RequirementId == governedExtension.RequirementId,
            "governed extensions belong only to their exact profile and shape context");
        firstMultiLegTrigger.Answer = "Multi-leg-specific trigger";

        viewModel.SelectedStrategyIntentShape = viewModel.StrategyIntentShapes.Single(shape =>
            shape.Kind == StrategyIntentKindV1.PositionTarget);

        viewModel.StrategyIntentDraft.Requirements.Single(requirement =>
                requirement.RequirementId == typedOrderPolicy.RequirementId)
            .Value!.CanonicalValue.Should().Be("alternate-profile-policy");
        viewModel.StrategyIntentDraft.Requirements.Single(requirement =>
                requirement.RequirementId == typedTrigger.RequirementId)
            .Value!.CanonicalValue.Should().Be("Alternate-profile trigger");

        viewModel.SelectedStrategyIntentShape = viewModel.StrategyIntentShapes.Single(shape =>
            shape.Kind == StrategyIntentKindV1.MultiLegTarget);
        viewModel.StrategyIntentDraft.Requirements.Single(requirement =>
                requirement.RequirementId == typedTrigger.RequirementId)
            .Value!.CanonicalValue.Should().Be("Multi-leg-specific trigger");
        viewModel.SelectedStrategyIntentShape = viewModel.StrategyIntentShapes.Single(shape =>
            shape.Kind == StrategyIntentKindV1.PositionTarget);

        viewModel.SelectedStrategyIntentProfile = originalProfile;
        var rebuiltTrigger = viewModel.StrategyIntentDraft!.Requirements.Single(requirement =>
            requirement.RequirementId == typedTrigger.RequirementId);
        rebuiltTrigger.Description.Should().Be(typedTrigger.Description);
        rebuiltTrigger.IsMaterial.Should().BeFalse();
        rebuiltTrigger.Provenance.Should().Be(preciseProvenance);
        rebuiltTrigger.Value.Should().Be(typedTrigger.Value);
        viewModel.StrategyIntentDraft.Requirements.Single(requirement =>
                requirement.RequirementId == typedOrderPolicy.RequirementId)
            .Should().BeEquivalentTo(typedOrderPolicy);
        viewModel.StrategyIntentDraft.Requirements.Single(requirement =>
                requirement.RequirementId == governedExtension.RequirementId)
            .Should().BeEquivalentTo(governedExtension);

        viewModel.SelectStrategyIntentProfile(alternateProfile);
        var alternateOrderPolicy = viewModel.StrategyIntentDraft.Requirements.Single(requirement =>
            requirement.RequirementId == typedOrderPolicy.RequirementId);
        alternateOrderPolicy.Value!.CanonicalValue.Should().Be("alternate-profile-policy");
        alternateOrderPolicy.Provenance!.ResearchEvidenceIds.Should().Equal("evidence-signal");
        viewModel.StrategyIntentDraft.Requirements.Single(requirement =>
                requirement.RequirementId == typedTrigger.RequirementId)
            .Value!.CanonicalValue.Should().Be("Alternate-profile trigger");
        viewModel.StrategyIntentRequirements.Should().NotContain(row =>
            row.RequirementId == governedExtension.RequirementId);
    }

    private static StrategyCandidateV1 Candidate() => new(
        StrategyCandidateV1.CurrentSchemaVersion,
        "signal-review",
        2,
        new string('a', 64),
        "Publish a point-in-time signal and never place an order.",
        "Signal review",
        StrategyCandidateStatusV1.Confirmed,
        new StrategyCandidateInterpretationV1(
            "Publish a signal without sizing, order, or position authority.",
            StrategyInterpretationConfidenceV1.High,
            []),
        [
            new StrategyCandidateGroupV1(
                "signal",
                StrategyCandidateGroupKindV1.SignalAndAlpha,
                "Signal",
                "Reviewed signal-only behavior.",
                [
                    new StrategyCandidateStatementV1(
                        "rule-signal",
                        StrategyCandidateStatementKindV1.Rule,
                        "Publish the qualified signal and do not create orders.",
                        StrategyCandidateStatementSourceV1.User,
                        StrategyCandidateStatementStateV1.Confirmed,
                        true),
                ],
                []),
        ],
        []);

    private static ResearchCaseV1 Research(StrategyCandidateV1 candidate) => new(
        ResearchCaseV1.CurrentSchemaVersion,
        "research/signal-review",
        candidate.CandidateId,
        StrategyCandidateCanonicalJsonV1.Hash(candidate),
        "Evaluate the usefulness of a published signal.",
        "Qualified point-in-time evidence may produce a useful signal.",
        [
            new ResearchEvidenceRequirementV1(
                "evidence-signal",
                "Completed observations at publication time.",
                "Only completed observations timestamped no later than publication are visible.",
                "Publish only when the threshold passes; otherwise publish no signal.",
                true,
                ["rule-signal"]),
        ],
        [
            new ResearchFalsifierV1(
                "falsifier-signal",
                "Reject if point-in-time publication cannot be reproduced.",
                true,
                ["rule-signal"]),
        ],
        []);

    private static StrategySpec Classification() => new(
        "signal-review",
        "Signal review",
        StrategyObjectiveKind.ReturnSeeking,
        new StrategyContextSpec(
            [AssetClass.Equity],
            MarketTopologyKind.SingleInstrument,
            ExposureGeometryKind.LongOnly,
            [StrategyInformationKind.Bar],
            new StrategyTimeSemantics(StrategyHorizonKind.Intraday, TimeSpan.FromMinutes(1))),
        new StrategySignalSpec(
            [ReturnHypothesisKind.Momentum],
            [StrategyTriggerKind.Bar],
            [SignalModelKind.DeterministicRule]),
        new StrategyPortfolioSpec(PortfolioConstructionKind.NotApplicable),
        new StrategyRiskSpec([StrategyRiskExitKind.NotApplicable]),
        new StrategyExecutionSpec([StrategyExecutionPolicyKind.NotApplicable]),
        new StrategyStateSpec([StrategyStateKind.Stateless], StrategyAdaptationKind.Fixed),
        []);

    private static StrategySpec CrossAssetDirectionalClassification() => new(
        "cross-asset-directional-review",
        "Chart move confirmed by an index",
        StrategyObjectiveKind.ReturnSeeking,
        new StrategyContextSpec(
            [AssetClass.Equity, AssetClass.Index],
            MarketTopologyKind.CrossAsset,
            ExposureGeometryKind.DirectionalLongShort,
            [StrategyInformationKind.Bar, StrategyInformationKind.Trade],
            new StrategyTimeSemantics(
                StrategyHorizonKind.Intraday,
                TimeSpan.FromMinutes(1),
                TimeSpan.FromHours(2))),
        new StrategySignalSpec(
            [ReturnHypothesisKind.Momentum],
            [StrategyTriggerKind.Bar],
            [SignalModelKind.DeterministicRule]),
        new StrategyPortfolioSpec(PortfolioConstructionKind.RiskBudget),
        new StrategyRiskSpec(
            [StrategyRiskExitKind.StopLoss, StrategyRiskExitKind.TakeProfit, StrategyRiskExitKind.SignalReversal]),
        new StrategyExecutionSpec(
            [StrategyExecutionPolicyKind.Market, StrategyExecutionPolicyKind.Limit]),
        new StrategyStateSpec(
            [StrategyStateKind.PositionAware, StrategyStateKind.FiniteState],
            StrategyAdaptationKind.Fixed),
        []);

    private static StrategyIntentDraftV1 Draft(
        StrategyCandidateV1 candidate,
        ResearchCaseV1 research,
        StrategySpec classification) => new(
        StrategyIntentDraftV1.CurrentSchemaVersion,
        "intent/signal-review",
        candidate.CandidateId,
        candidate.Revision,
        StrategyCandidateCanonicalJsonV1.Hash(candidate),
        ResearchCaseCanonicalJsonV1.Hash(research),
        new StrategyClassificationBindingV1(
            classification.Id,
            StrategySpecCanonicalJsonV1.Hash(classification)),
        new StrategyIntentModelV1(StrategyIntentKindV1.SignalOnly),
        StrategyIntentCompletenessV1.CatalogVersion,
        [
            Applicable("observe.trigger", StrategySemanticStageV1.ObserveOrTrigger, "Evaluate completed bars."),
            Applicable("evidence.qualification", StrategySemanticStageV1.QualifyEvidence, "Qualify or publish no signal."),
            Applicable("decision.intent", StrategySemanticStageV1.DecideIntent, "Emit signal or no signal."),
            Applicable("decision.signal_publication", StrategySemanticStageV1.DecideIntent, "Publish value, confidence, and expiry."),
            NotApplicable("exposure.not_applicable", StrategySemanticStageV1.SizeOrExposure, "No sizing authority."),
            NotApplicable("execution.not_applicable", StrategySemanticStageV1.Execution, "No order authority."),
            NotApplicable("lifecycle.fill_handling_not_applicable", StrategySemanticStageV1.ManageLifecycle, "No fill-handling authority."),
            Applicable("lifecycle.publication", StrategySemanticStageV1.ManageLifecycle, "Publish, update, expire, or withdraw."),
            NotApplicable("finish.not_applicable", StrategySemanticStageV1.FinishOrUnwind, "No position exists to unwind."),
        ]);

    private static StrategySemanticRequirementV1 Applicable(
        string id,
        StrategySemanticStageV1 stage,
        string value) => new(
        id,
        stage,
        StrategySemanticDispositionV1.Applicable,
        value,
        true,
        new StrategyRequirementProvenanceV1(["rule-signal"], ["evidence-signal"], "Reviewed source."),
        new StrategyCandidateValueV1("core.semantic_clause@1", value));

    private static StrategySemanticRequirementV1 NotApplicable(
        string id,
        StrategySemanticStageV1 stage,
        string rationale) => new(
        id,
        stage,
        StrategySemanticDispositionV1.NotApplicable,
        rationale,
        true,
        new StrategyRequirementProvenanceV1(["rule-signal"], ["evidence-signal"], "Explicitly reviewed."),
        DispositionRationale: rationale);

    private sealed class EmptySessionRepository : IAuthoringSessionRepository
    {
        public IReadOnlyList<AuthoringSessionSnapshot> List() => [];
        public bool Save(AuthoringSessionSnapshot session) => true;
        public void Delete(string strategyId) { }
    }

    private sealed class RecordingSessionRepository : IAuthoringSessionRepository
    {
        public AuthoringSessionSnapshot? Saved { get; private set; }

        public IReadOnlyList<AuthoringSessionSnapshot> List() => Saved is null ? [] : [Saved];

        public bool Save(AuthoringSessionSnapshot session)
        {
            Saved = session;
            return true;
        }

        public void Delete(string strategyId)
        {
            if (string.Equals(Saved?.StrategyId, strategyId, StringComparison.Ordinal)) Saved = null;
        }
    }

    private sealed class StubCompiler : IStrategyCompiler
    {
        public StrategyCompileResult Compile(StrategyScript script) => StrategyCompileResult.Failed([]);
    }

    private sealed class StubRegistry : IBacktestStrategyRegistry
    {
        public IReadOnlyList<BacktestStrategyOption> All => [];
        public BacktestStrategyOption? Find(string id) => null;
        public void Register(BacktestStrategyOption option) { }
        public bool Remove(string id) => false;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }

    private sealed class StubIntentExtensionRegistry(
        params (StrategyIntentExtensionRoleV1 Role, string Id)[] ownedIds)
        : IStrategyIntentExtensionRegistryV1
    {
        private readonly HashSet<(StrategyIntentExtensionRoleV1 Role, string Id)> _ownedIds =
            new(ownedIds);

        public bool Owns(StrategyIntentExtensionRoleV1 role, string extensionId) =>
            _ownedIds.Contains((role, extensionId));
    }
}
