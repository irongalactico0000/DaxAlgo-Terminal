using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TradingTerminal.App.Authoring;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Strategies.Authoring;
using TradingTerminal.Infrastructure.Backtest;
using TradingTerminal.Infrastructure.Strategies.Authoring;
using Xunit;

namespace TradingTerminal.App.Avalonia.Tests;

public sealed class StrategyAuthoringFreshSessionTests
{
    [Fact]
    public void Generation_lane_row_exposes_real_phases_and_rejects_late_regression()
    {
        var row = new StrategyGenerationLaneProgressRow(StrategyGenerationLaneV1.TypedGraph);

        row.Apply(new StrategyGenerationLaneProgressV1(
            row.Lane,
            StrategyGenerationLaneProgressStateV1.PreparingRequest));
        row.StateLabel.Should().Be("PREPARING");
        row.PipelineText.Should().Contain("● PREPARE");

        row.Apply(new StrategyGenerationLaneProgressV1(
            row.Lane,
            StrategyGenerationLaneProgressStateV1.WaitingForModel));
        row.StateLabel.Should().Be("WAITING FOR MODEL");
        row.StateDetail.Should().Contain("waiting for the model response");

        row.Apply(new StrategyGenerationLaneProgressV1(
            row.Lane,
            StrategyGenerationLaneProgressStateV1.ValidatingArtifact));
        row.StateDetail.Should().Contain("installed package check");

        row.Apply(new StrategyGenerationLaneProgressV1(
            row.Lane,
            StrategyGenerationLaneProgressStateV1.Completed,
            "Installed package validation passed; nothing was tested or run."));
        row.StateLabel.Should().Be("READY");
        row.PipelineText.Should().Be("✓ PREPARE   ✓ MODEL   ✓ PARSE   ✓ CHECK");

        row.Apply(new StrategyGenerationLaneProgressV1(
            row.Lane,
            StrategyGenerationLaneProgressStateV1.ParsingResponse,
            "late callback"));
        row.State.Should().Be(StrategyGenerationLaneProgressStateV1.Completed);
        row.StateDetail.Should().Be("Installed package validation passed; nothing was tested or run.");
    }

    [Fact]
    public void Generation_lane_row_exposes_terminal_artifact_and_raw_failure_for_inspection()
    {
        const string source = "def on_event(state, event):\n    return state\n";
        var lane = StrategyGenerationLaneV1.VibePython;
        var candidate = new StrategyGenerationCandidateV1(
            StrategyGenerationCandidateV1.CurrentSchemaVersion,
            "preview/vibe-python",
            lane,
            new string('a', 64),
            StrategyGenerationPackageCatalogV1.RequireBinding(lane),
            "Preview candidate",
            "Inspect the exact source before choosing it.",
            [],
            [],
            [],
            [],
            new StrategyGenerationArtifactV1(
                StrategyGenerationArtifactKindV1.VibePythonSource,
                "strategy.py",
                "python",
                source,
                null),
            "Review-only fixture.",
            []);
        var generated = new StrategyGenerationLaneResultV1(
            lane,
            StrategyGenerationReadinessV1.Generated,
            candidate,
            StrategyGenerationCandidateCanonicalJsonV1.Hash(candidate),
            [],
            new StrategyGenerationAgentRunV1(
                "vibe-agent",
                "test-provider",
                null,
                true,
                null,
                null,
                CodegenUsage.None));
        var generatedRow = new StrategyGenerationLaneProgressRow(lane);

        generatedRow.Apply(new StrategyGenerationLaneProgressV1(
            lane,
            StrategyGenerationLaneProgressStateV1.Completed,
            "Artifact ready for review.",
            generated));

        generatedRow.HasResult.Should().BeTrue();
        generatedRow.ResultOption.Should().NotBeNull();
        generatedRow.ResultOption!.Result.Should().BeSameAs(generated);
        generatedRow.InspectablePreview.Should().Be(source);
        generatedRow.PreviewHeading.Should().Be("strategy.py · exact generated artifact");

        const string rawResponse = "{ broken model response";
        var issue = new StrategyCandidateGenerationIssueV1(
            StrategyCandidateGenerationIssueSeverityV1.Error,
            "LANE_JSON_INVALID",
            "candidate",
            "The candidate envelope could not be parsed.");
        var failed = new StrategyGenerationLaneResultV1(
            StrategyGenerationLaneV1.TypedGraph,
            StrategyGenerationReadinessV1.Failed,
            null,
            null,
            [issue],
            new StrategyGenerationAgentRunV1(
                "graph-agent",
                "test-provider",
                null,
                false,
                "The model returned invalid JSON.",
                rawResponse,
                CodegenUsage.None));
        var failedRow = new StrategyGenerationLaneProgressRow(failed.Lane);

        failedRow.Apply(new StrategyGenerationLaneProgressV1(
            failed.Lane,
            StrategyGenerationLaneProgressStateV1.Failed,
            "Candidate envelope invalid.",
            failed));

        failedRow.HasResult.Should().BeTrue();
        failedRow.ResultOption!.IsFailed.Should().BeTrue();
        failedRow.ResultOption.FirstIssue.Should().BeSameAs(issue);
        failedRow.InspectablePreview.Should().Be(rawResponse,
            "the exact failed provider response is more inspectable than a flattened status string");
        failedRow.PreviewHeading.Should().Be("Raw model response · candidate envelope invalid");

        var whitespaceArtifact = candidate with
        {
            Artifact = candidate.Artifact with { Source = "   " },
        };
        var whitespaceResult = failed with
        {
            Lane = lane,
            Candidate = whitespaceArtifact,
            CandidateHashSha256 = StrategyGenerationCandidateCanonicalJsonV1.Hash(whitespaceArtifact),
        };
        var whitespaceOption = new StrategyGenerationCandidateOption(whitespaceResult);
        whitespaceOption.InspectablePreview.Should().Be(rawResponse);
        whitespaceOption.PreviewHeading.Should().Be("Raw model response · candidate envelope invalid",
            "the heading must describe the content actually displayed, not a blank artifact shell");
    }

    [Fact]
    public void New_strategy_clears_session_transients_without_reselecting_the_saved_chat()
    {
        var saved = new AuthoringSessionSnapshot(
            StrategyId: "saved-strategy",
            DisplayName: "Saved strategy",
            Chat:
            [
                new AuthoringChatEntry(
                    AuthoringChatEntry.User,
                    "Keep this saved conversation",
                    DateTime.Now),
            ],
            Thread: [],
            Files: [new StrategyFile("Saved.cs", "// saved")],
            InputTokens: 120,
            OutputTokens: 30,
            GenerateCandidateFirst: false,
            AuthoringUxVersion: AuthoringSessionSnapshot.CurrentAuthoringUxVersion,
            UpdatedUtc: DateTime.UtcNow);
        var sessions = new MemoryAuthoringSessionRepository(saved);
        using var viewModel = new StrategyAuthoringViewModel(
            new StubCompiler(),
            new StubRegistry(),
            NullLogger<StrategyAuthoringViewModel>.Instance,
            sessionRepository: sessions);

        viewModel.SelectedSavedSession.Should().NotBeNull();
        viewModel.Composer = "unsent follow-up";
        viewModel.StarterSearchText = "futures";
        viewModel.SelectedStarterFamily = viewModel.StarterFamilyOptions[1];
        viewModel.SelectedStarterHorizon = viewModel.StarterHorizonOptions[1];
        viewModel.SelectedStarterData = viewModel.StarterDataOptions[1];
        viewModel.InputTokens = 321;
        viewModel.OutputTokens = 123;
        viewModel.CachedTokens = 99;
        viewModel.WorkbenchTab = 3;
        viewModel.Activity.Add("old activity");
        viewModel.Tasks.Add(new BuildTask("old task"));
        viewModel.AiStatus = "old provider status";
        viewModel.ElapsedText = "2m elapsed";
        viewModel.ElapsedCompact = "2:00";
        viewModel.WorkingVerb = "Generating…";
        viewModel.StepText = "step 3 of 4";
        viewModel.CompiledOk = true;
        viewModel.IsRegistered = true;
        viewModel.AwaitingAnswer = true;
        viewModel.ReviewOpen = true;
        var rawResult = new StrategyGenerationLaneResultV1(
            StrategyGenerationLaneV1.TypedGraph,
            StrategyGenerationReadinessV1.Failed,
            null,
            null,
            [new StrategyCandidateGenerationIssueV1(
                StrategyCandidateGenerationIssueSeverityV1.Error,
                "RAW_FAILURE",
                "candidate",
                "Transient raw response fixture.")],
            new StrategyGenerationAgentRunV1(
                "graph-agent",
                "test-provider",
                null,
                false,
                "Transient failure.",
                "transient raw provider payload",
                CodegenUsage.None));
        var rawRow = new StrategyGenerationLaneProgressRow(rawResult.Lane);
        rawRow.Apply(new StrategyGenerationLaneProgressV1(
            rawResult.Lane,
            StrategyGenerationLaneProgressStateV1.Failed,
            "Transient failure.",
            rawResult));
        viewModel.GenerationLaneProgressRows.Add(rawRow);
        viewModel.SelectedGenerationLaneProgressRow = rawRow;

        viewModel.NewChatCommand.Execute(null);

        sessions.SaveCalls.Should().Be(1, "the outgoing saved conversation must remain available");
        viewModel.SavedSessions.Should().ContainSingle(session => session.StrategyId == "saved-strategy");
        viewModel.SelectedSavedSession.Should().BeNull("a new strategy is not a restored session");
        viewModel.StrategyId.Should().Be("myStrategy");
        viewModel.DisplayName.Should().Be("My custom strategy");
        viewModel.GenerateCandidateFirst.Should().BeTrue();
        viewModel.Composer.Should().BeEmpty();
        viewModel.StarterSearchText.Should().BeEmpty();
        viewModel.SelectedStarterFamily.Should().Be(viewModel.StarterFamilyOptions[0]);
        viewModel.SelectedStarterHorizon.Should().Be(viewModel.StarterHorizonOptions[0]);
        viewModel.SelectedStarterData.Should().Be(viewModel.StarterDataOptions[0]);
        viewModel.VisibleStarterBriefs.Should().HaveCount(viewModel.AllStarterBriefs.Count);
        viewModel.InputTokens.Should().Be(0);
        viewModel.OutputTokens.Should().Be(0);
        viewModel.CachedTokens.Should().Be(0);
        viewModel.WorkbenchTab.Should().Be(3, "a fresh Design screen must select the visible Request tab");
        viewModel.IsDesignScreen.Should().BeTrue();
        viewModel.IsBriefStage.Should().BeTrue();
        viewModel.ActiveScreenTitle.Should().Be("Brief");
        viewModel.StrategyWorkspace.WorkspaceId.Should().Be("myStrategy");
        viewModel.StrategyWorkspace.Stages.Should().HaveCount(6);
        viewModel.ValidateStageState.Should().Be("LOCKED");
        viewModel.PaperStageState.Should().Be("LOCKED");
        viewModel.Messages.Should().BeEmpty();
        viewModel.Activity.Should().BeEmpty();
        viewModel.Tasks.Should().BeEmpty();
        viewModel.Diagnostics.Should().BeEmpty();
        viewModel.AiStatus.Should().BeNull();
        viewModel.ElapsedText.Should().BeNull();
        viewModel.ElapsedCompact.Should().BeNull();
        viewModel.WorkingVerb.Should().BeNull();
        viewModel.StepText.Should().BeNull();
        viewModel.CompiledOk.Should().BeFalse();
        viewModel.IsRegistered.Should().BeFalse();
        viewModel.AwaitingAnswer.Should().BeFalse();
        viewModel.ReviewOpen.Should().BeFalse();
        viewModel.GenerationLaneProgressRows.Should().BeEmpty();
        viewModel.SelectedGenerationLaneProgressRow.Should().BeNull(
            "starting a new strategy must release transient raw provider output");
        viewModel.Files.Should().ContainSingle(file => file.Name == StrategyFile.DefaultName);
        viewModel.SelectedFile.Should().BeSameAs(viewModel.Files[0]);
    }

    private sealed class MemoryAuthoringSessionRepository(params AuthoringSessionSnapshot[] sessions)
        : IAuthoringSessionRepository
    {
        private readonly List<AuthoringSessionSnapshot> _sessions = [.. sessions];

        public int SaveCalls { get; private set; }

        public IReadOnlyList<AuthoringSessionSnapshot> List() =>
            [.. _sessions.OrderByDescending(static session => session.UpdatedUtc)];

        public bool Save(AuthoringSessionSnapshot session)
        {
            SaveCalls++;
            _sessions.RemoveAll(existing => existing.StrategyId == session.StrategyId);
            _sessions.Add(session with { UpdatedUtc = DateTime.UtcNow });
            return true;
        }

        public void Delete(string strategyId) =>
            _sessions.RemoveAll(session => session.StrategyId == strategyId);
    }

    private sealed class StubCompiler : IStrategyCompiler
    {
        public StrategyCompileResult Compile(StrategyScript script) => StrategyCompileResult.Failed([]);
    }

    private sealed class StubRegistry : IBacktestStrategyRegistry
    {
        public IReadOnlyList<BacktestStrategyOption> All => [];
        public BacktestStrategyOption? Find(string id) => null;
        public void Register(BacktestStrategyOption option)
        {
        }

        public bool Remove(string id) => false;

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }
    }
}
