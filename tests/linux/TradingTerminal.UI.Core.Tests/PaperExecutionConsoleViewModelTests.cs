using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Trading;
using TradingTerminal.UI.Execution;
using Xunit;

namespace TradingTerminal.UI.Core.Tests;

public sealed class PaperExecutionConsoleViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 8, 0, 0, TimeSpan.Zero);
    private static readonly ExecutionResource Resource = new(
        new VenueId("paper-simulator"),
        new TradingAccountId("local-paper"),
        ExecutionEnvironment.SimulatedPaper);
    private static readonly PaperExecutionInstrumentChoice Instrument = new(
        new InstrumentId(7), "SPY", "Equity", "ARCA", "USD");

    [Fact]
    public async Task Submit_crosses_the_execution_client_and_refreshes_truthful_status()
    {
        using var client = new FakeClient(Snapshot(leaseHeld: true));
        var factory = new FakeFactory(CreateSubmit());
        using var viewModel = new PaperExecutionConsoleViewModel(client, factory);

        await viewModel.SubmitCommand.ExecuteAsync(null);

        Assert.Equal(1, client.SubmitCalls);
        Assert.True(viewModel.LeaseHeld);
        Assert.Equal("PAPER · SIMULATED · NO LIVE ROUTE", viewModel.ModeText);
        Assert.Contains("Paper", viewModel.LastMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Kill_requires_two_explicit_actions_before_crossing_the_client()
    {
        using var client = new FakeClient(Snapshot(leaseHeld: true));
        using var viewModel = new PaperExecutionConsoleViewModel(client, new FakeFactory(CreateSubmit()));

        await viewModel.KillCommand.ExecuteAsync(null);

        Assert.True(viewModel.KillConfirmationArmed);
        Assert.Equal(0, client.KillCalls);
        Assert.Equal("CONFIRM KILL + FLATTEN", viewModel.KillActionText);

        await viewModel.KillCommand.ExecuteAsync(null);

        Assert.False(viewModel.KillConfirmationArmed);
        Assert.Equal(1, client.KillCalls);
    }

    [Fact]
    public void Lease_loss_keeps_the_console_readable_but_disables_submission()
    {
        using var client = new FakeClient(Snapshot(leaseHeld: false));
        using var viewModel = new PaperExecutionConsoleViewModel(client, new FakeFactory(CreateSubmit()));

        Assert.False(viewModel.CanIssueCommands);
        Assert.False(viewModel.SubmitCommand.CanExecute(null));
        Assert.Contains("Read-only", viewModel.WriterStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Open_reconciliation_case_blocks_exposure_and_resolution_crosses_the_client()
    {
        var opened = new ReconciliationCase(
            new ReconciliationCaseId("console-case"),
            Resource,
            ReconciliationSubjectKind.Position,
            "instrument:7",
            null,
            ReconciliationCaseKind.PositionMismatch,
            ReconciliationCaseStatus.Open,
            "ledger quantity=2",
            "venue quantity=1",
            Now);
        using var client = new FakeClient(Snapshot(
            leaseHeld: true,
            reconciliationCases: [opened],
            reconciliationAdmissionBlocked: true));
        using var viewModel = new PaperExecutionConsoleViewModel(client, new FakeFactory(CreateSubmit()));

        Assert.False(viewModel.CanIssueCommands);
        Assert.False(viewModel.SubmitCommand.CanExecute(null));
        Assert.Equal("1", viewModel.ReconciliationCaseCount);
        Assert.Contains("BLOCKED", viewModel.ReconciliationStatus, StringComparison.Ordinal);
        viewModel.SelectedReconciliationCase = Assert.Single(viewModel.ReconciliationCases);
        Assert.Equal("ledger quantity=2", viewModel.SelectedReconciliationCase.LocalEvidence);
        Assert.Equal("venue quantity=1", viewModel.SelectedReconciliationCase.VenueEvidence);
        viewModel.ResolutionOperator = "console-operator";
        viewModel.ResolutionEvidence = "The Paper venue was independently reconstructed and agrees.";

        await viewModel.ResolveSelectedReconciliationCaseCommand.ExecuteAsync(null);

        Assert.Equal(1, client.ResolveCalls);
        Assert.Equal(opened.CaseId, client.LastResolvedCaseId);
        Assert.False(viewModel.ReconciliationAdmissionBlocked);
        Assert.True(viewModel.CanIssueCommands);
        Assert.Equal(ReconciliationCaseStatus.Resolved.ToString(), Assert.Single(viewModel.ReconciliationCases).Status);
        Assert.Contains("console-operator", Assert.Single(viewModel.ReconciliationCases).Resolution, StringComparison.Ordinal);
    }

    [Fact]
    public void Committed_risk_decision_exposes_policy_hashes_projected_economics_and_inputs()
    {
        var submit = CreateSubmit();
        var decision = RiskPolicy.Evaluate(submit.Command, submit.RiskContext);
        var observation = OrderRiskObservation.Capture(submit.Command, decision, submit.RiskContext);
        var riskFact = new PaperRiskDecisionSnapshot(
            submit.Command.ClientOrderId,
            2,
            OrderEventKind.RiskAccepted,
            Now.AddSeconds(1),
            observation);
        using var client = new FakeClient(Snapshot(leaseHeld: true, riskDecisions: [riskFact]));
        using var viewModel = new PaperExecutionConsoleViewModel(client, new FakeFactory(submit));

        Assert.Equal("1", viewModel.RiskDecisionCount);
        var row = Assert.Single(viewModel.RiskDecisions);
        Assert.Same(row, viewModel.SelectedRiskDecision);
        Assert.Equal("SUBMIT", row.Phase);
        Assert.Equal("ALLOW", row.Decision);
        Assert.Equal(RiskDecisionCode.Allowed.ToString(), row.Code);
        Assert.Equal(observation.CommandPayloadHashSha256, row.CommandPayloadHash);
        Assert.Equal(observation.Evidence.LimitsHashSha256, row.LimitsHash);
        Assert.Equal(RiskPolicy.PolicyVersion, row.PolicyVersion);
        Assert.Contains("buying power 1000000 USD", row.AccountEvidence, StringComparison.Ordinal);
        Assert.Contains("commands 0/100", row.MarketEvidence, StringComparison.Ordinal);
        Assert.Contains("order ≤ 100", row.LimitsEvidence, StringComparison.Ordinal);
    }

    [Fact]
    public void Execution_quality_exposes_verified_rates_counts_latency_and_truthful_slippage()
    {
        var quality = new PaperExecutionQualitySnapshot(
            Orders: 8,
            FilledOrders: 6,
            Rejects: 1,
            Cancels: 2,
            ReconciliationCases: 3,
            UnknownOutcomes: 1,
            SlippageObservationCount: 0,
            TotalSlippageTicks: 0d,
            AcknowledgementObservationCount: 4,
            TotalAcknowledgementLatencyMilliseconds: 100d);
        using var client = new FakeClient(Snapshot(leaseHeld: true, executionQuality: quality));
        using var viewModel = new PaperExecutionConsoleViewModel(client, new FakeFactory(CreateSubmit()));

        Assert.Equal("75.0%", viewModel.ExecutionQuality.FillRate);
        Assert.Equal("12.5%", viewModel.ExecutionQuality.RejectRate);
        Assert.Equal("25 ms", viewModel.ExecutionQuality.AverageAcknowledgement);
        Assert.Equal("n/a", viewModel.ExecutionQuality.AverageSlippage);
        Assert.Equal("8", viewModel.ExecutionQuality.Orders);
        Assert.Equal("2", viewModel.ExecutionQuality.Cancels);
        Assert.Equal("3", viewModel.ExecutionQuality.ReconciliationCases);
        Assert.Equal("1", viewModel.ExecutionQuality.UnknownOutcomes);
        Assert.Contains("arrival-price benchmark", viewModel.ExecutionQuality.Provenance, StringComparison.Ordinal);
    }

    [Fact]
    public void Portfolio_analytics_exposes_account_basis_ranges_and_mark_provenance()
    {
        var analytics = new PaperPortfolioAnalyticsSnapshot(
            100_000m,
            -9_000m,
            91_000m,
            500m,
            1_500m,
            102_000m,
            11_000m,
            11_000m,
            1,
            [],
            [new PaperInstrumentExposureSnapshot(
                Instrument.InstrumentId, 100m, 90m, 110m, 11_000m, 2_000m,
                PaperMarkBasis.CurrentPaperMark)],
            [new PaperPerformancePeriodSnapshot(
                PaperExecutionTimeRange.SevenDays, "7D", 100_000m, 100_500m, 500m, 0.5m,
                1.25d, -0.2m, 50m, 2, 1, [], [])],
            "SIM",
            "Current Paper marks");
        using var client = new FakeClient(Snapshot(leaseHeld: true, portfolioAnalytics: analytics));
        using var viewModel = new PaperExecutionConsoleViewModel(client, new FakeFactory(CreateSubmit()));

        Assert.Equal("102000.00 SIM", viewModel.MarkedEquity);
        Assert.Equal("+500.00", viewModel.Portfolio.RealizedProfitAndLoss);
        Assert.Equal("+1500.00", viewModel.Portfolio.UnrealizedProfitAndLoss);
        Assert.Contains("no broker/FX claim", viewModel.Portfolio.ValuationBasis, StringComparison.Ordinal);
        Assert.Equal("7D", Assert.Single(viewModel.PerformancePeriods).Range);
        Assert.Equal("Current Paper mark", Assert.Single(viewModel.PortfolioExposures).MarkBasis);
    }

    private static PaperExecutionClientSnapshot Snapshot(
        bool leaseHeld,
        IReadOnlyList<ReconciliationCase>? reconciliationCases = null,
        bool reconciliationAdmissionBlocked = false,
        IReadOnlyList<PaperRiskDecisionSnapshot>? riskDecisions = null,
        PaperExecutionQualitySnapshot? executionQuality = null,
        PaperPortfolioAnalyticsSnapshot? portfolioAnalytics = null) => new(
        Resource,
        new ExecutionLeaseId("desktop-paper-writer"),
        new FencingToken(1),
        leaseHeld,
        false,
        0,
        [],
        new ExecutionReconciliationSnapshot(Resource, Now, [], [], [], []),
        [],
        Now,
        "Paper execution state synchronized.",
        reconciliationCases,
        reconciliationAdmissionBlocked,
        riskDecisions,
        executionQuality,
        portfolioAnalytics);

    private static ExecutionSubmitRequest CreateSubmit()
    {
        var terms = new OrderTerms(OrderSide.Buy, OrderType.Market, ScaledQuantity.FromWhole(1));
        var metadata = new ExecutionCommandMetadata(
            new CommandId("ui-command"),
            new CorrelationId("ui-correlation"),
            new CausationId("ui-cause"),
            Resource.TradingAccountId,
            new StrategyId("manual-ticket"),
            new StrategyVersion("1.0.0"),
            Resource.VenueId,
            Instrument.InstrumentId,
            Resource.Environment,
            Now,
            0);
        var clientId = new ClientOrderId("ui-client-order");
        var mapping = new CanonicalInstructionMappingContext(
            new IntentId("ui-intent"),
            null,
            new LegId("ui-leg"),
            new ExecutionLeaseId("desktop-paper-writer"),
            new FencingToken(1),
            TradeIntentQuantityMode.Delta,
            ScaledQuantity.FromWhole(1),
            ScaledQuantity.Zero,
            null,
            null,
            ScaledMoney.Zero,
            1,
            "ui-policy");
        Assert.Equal(OrderDomainFault.None, CanonicalOrderInstructionMapper.TryCreate(
            metadata, clientId, terms, mapping, out var instruction));
        var command = new SubmitOrderCommand(
            metadata,
            new OrderId("ui-order"),
            clientId,
            terms,
            instruction!);
        var limits = new RiskLimits(
            ScaledQuantity.FromWhole(100),
            ScaledQuantity.FromWhole(100),
            new ScaledMoney(1_000_000, 0),
            ScaledMoney.Zero,
            new ScaledMoney(100_000, 0),
            new ScaledMoney(100_000, 0),
            100,
            TimeSpan.FromMinutes(1));
        var risk = new RiskEvaluationContext(
            limits,
            RiskControlMode.Active,
            false,
            ScaledQuantity.Zero,
            ScaledQuantity.Zero,
            ScaledQuantity.Zero,
            ScaledMoney.Zero,
            ScaledQuantity.Zero,
            ScaledMoney.Zero,
            ScaledQuantity.Zero,
            new ScaledMoney(1_000_000, 0),
            ScaledMoney.Zero,
            new ScaledMoney(100_000, 0),
            new ScaledMoney(100_000, 0),
            new ScaledPrice(100, 0),
            0,
            Now);
        return new ExecutionSubmitRequest(command, risk);
    }

    private sealed class FakeFactory(ExecutionSubmitRequest submit) : IPaperExecutionOrderFactory
    {
        public IReadOnlyList<PaperExecutionInstrumentChoice> Instruments { get; } = [Instrument];

        public bool TryCreateSubmit(PaperOrderTicketDraft draft, PaperExecutionClientSnapshot snapshot,
            out ExecutionSubmitRequest? request, out string? reason)
        {
            request = submit;
            reason = null;
            return true;
        }

        public bool TryCreateReplacementTerms(PaperOrderTicketDraft draft, out OrderTerms? terms, out string? reason)
        {
            terms = submit.Command.Terms;
            reason = null;
            return true;
        }

        public RiskEvaluationContext CreateReplacementRisk(PaperOrderTicketDraft draft, OmsOrderProjection order,
            PaperExecutionClientSnapshot snapshot) => submit.RiskContext;
    }

    private sealed class FakeClient(PaperExecutionClientSnapshot snapshot) : IPaperExecutionClient
    {
        private PaperExecutionClientSnapshot _snapshot = snapshot;
        public int SubmitCalls { get; private set; }
        public int KillCalls { get; private set; }
        public int ResolveCalls { get; private set; }
        public ReconciliationCaseId? LastResolvedCaseId { get; private set; }
        public event EventHandler? SnapshotInvalidated;
        public PaperExecutionClientSnapshot GetSnapshot() => _snapshot;

        public ValueTask<PaperExecutionClientResult> RefreshAsync(CancellationToken cancellationToken = default) =>
            Result("Paper execution state synchronized.");
        public ValueTask<PaperExecutionClientResult> SetIntakePausedAsync(bool paused, CancellationToken cancellationToken = default)
        {
            _snapshot = _snapshot with { IntakePaused = paused };
            return Result(paused ? "Paper order intake paused." : "Paper order intake resumed.");
        }
        public ValueTask<PaperExecutionClientResult> SubmitAsync(ExecutionSubmitRequest request, CancellationToken cancellationToken = default)
        {
            SubmitCalls++;
            return Result("Order accepted by the Paper OMS.");
        }
        public ValueTask<PaperExecutionClientResult> CancelAsync(ClientOrderId clientOrderId, CancellationToken cancellationToken = default) => Result("Cancelled.");
        public ValueTask<PaperExecutionClientResult> ReplaceAsync(ClientOrderId clientOrderId, OrderTerms replacementTerms, RiskEvaluationContext riskContext, CancellationToken cancellationToken = default) => Result("Replaced.");
        public ValueTask<PaperExecutionClientResult> ReconcileAsync(CancellationToken cancellationToken = default) => Result("Reconciled.");
        public ValueTask<PaperExecutionClientResult> ResolveReconciliationCaseAsync(
            ReconciliationCaseId caseId,
            string resolvedBy,
            string resolutionEvidence,
            CancellationToken cancellationToken = default)
        {
            ResolveCalls++;
            LastResolvedCaseId = caseId;
            var cases = _snapshot.CaseFacts
                .Select(item => item.CaseId == caseId
                    ? item.Resolve(Now.AddMinutes(1), resolvedBy, resolutionEvidence)
                    : item)
                .ToArray();
            _snapshot = _snapshot with
            {
                ReconciliationCases = cases,
                ReconciliationAdmissionBlocked = cases.Any(item =>
                    item.IsMaterial && item.Status != ReconciliationCaseStatus.Resolved),
            };
            return Result("Reconciliation resolution appended.");
        }
        public ValueTask<PaperExecutionClientResult> KillAsync(CancellationToken cancellationToken = default)
        {
            KillCalls++;
            _snapshot = _snapshot with { IntakePaused = true };
            return Result("Paper positions are flat and intake remains paused.");
        }
        private ValueTask<PaperExecutionClientResult> Result(string message)
        {
            SnapshotInvalidated?.Invoke(this, EventArgs.Empty);
            return ValueTask.FromResult(PaperExecutionClientResult.Success(message));
        }
        public void Dispose() { }
    }
}
