using FluentAssertions;
using TradingTerminal.Backtest.Engine;
using TradingTerminal.Backtest.Engine.Accounting;
using TradingTerminal.Backtest.Engine.Cost;
using TradingTerminal.Backtest.Engine.Execution;
using TradingTerminal.Backtest.Engine.TradeIr;
using TradingTerminal.Core.Backtesting;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Trading;
using TradingTerminal.TradeIr.Runtime;
using Xunit;

namespace TradingTerminal.Tests.Backtesting;

public sealed class TradeIrClosedLaneIntegrationTests
{
    [Fact]
    public void Admitted_definition_replays_identically_through_compiler_evaluator_risk_and_book()
    {
        var first = RunReplay();
        var second = RunReplay();

        first.IntentHashes.Should().HaveCount(2, "the EMA entry and end-of-run flatten are both inert intents");
        first.DecisionCodes.Should().Equal(
            TradeIrGatewayDecisionCodesV1.Allowed,
            TradeIrGatewayDecisionCodesV1.Allowed);
        first.BookStates.Should().Equal(
            OrderState.Working,
            OrderState.Filled,
            OrderState.Working,
            OrderState.Filled);
        first.TerminalQuantity.Should().Be(0);
        first.SubmittedOrderCount.Should().Be(2);
        second.Should().BeEquivalentTo(first, options => options.WithStrictOrdering(),
            "equal canonical definition, data, host policy, and feedback must produce equal evidence");
    }

    private static ReplayEvidence RunReplay()
    {
        var admitted = BacktestTradeIrTargetV1Tests.CreateFixture();
        var compilation = TradeIrExecutionPlanCompilerV1.Compile(
            admitted.Definition,
            admitted.Target,
            admitted.Pins,
            [admitted.Capability],
            [admitted.Binding]);
        compilation.Succeeded.Should().BeTrue();
        var plan = compilation.Plan!;
        var evaluator = new TradeIrEvaluatorV1(plan);

        var portable = admitted.Definition.DataRequirements.Single()
            .InstrumentSelector.References.Single();
        var instrument = new InstrumentSpec(
            new InstrumentId(707),
            Contract.UsStock(portable.Symbol, portable.Venue),
            TickSize: 0.01,
            ContractMultiplier: 1d);
        var clock = new SimClock();
        var book = new SimulatedOrderBook(clock, new L1TouchFillModel(slippageTicks: 0), _ => 0.01);
        var portfolio = new Portfolio(
            startingCash: 100_000d,
            new Dictionary<InstrumentId, double> { [instrument.Id] = instrument.ContractMultiplier },
            FeeModels.From(new CostSpec()));
        var policy = new BacktestTradeIrHostPolicyV1(
            "risk/backtest-integration-v1",
            new TradingAccountId("account/backtest-integration"),
            new VenueId("venue/simulated"),
            new RiskLimits(
                maximumOrderQuantity: ScaledQuantity.FromWhole(100),
                maximumAbsolutePosition: ScaledQuantity.FromWhole(100),
                maximumGrossNotional: new ScaledMoney(1_000_000, 0),
                minimumBuyingPower: ScaledMoney.Zero,
                maximumDailyLoss: new ScaledMoney(10_000, 0),
                maximumDrawdown: new ScaledMoney(10_000, 0),
                maximumExposureCommandsPerWindow: 100,
                rateLimitWindow: TimeSpan.FromMinutes(1)),
            RiskControlMode.Active,
            KillSwitchActive: false);
        using var gateway = new TradeIrRiskGatewayV1(
            compilation.AdmissionManifest!,
            plan,
            instrument,
            policy,
            book,
            portfolio,
            clock);

        var intentHashes = new List<string>();
        var feedbackHashes = new List<string>();
        var bookEventHashes = new List<string>();
        var bookStates = new List<OrderState>();
        book.Event += (_, orderEvent) =>
        {
            bookStates.Add(orderEvent.State);
            bookEventHashes.Add(ExecutionCanonicalJson.Hash(orderEvent));
        };

        DateTime latestQuoteTime = default;
        for (var sequence = 1L; sequence <= 14L; sequence++)
        {
            // Adjacent events deliberately share a timestamp; the admitted
            // EventTimeThenSourceSequence contract must preserve both in sequence order.
            latestQuoteTime = DateTime.UnixEpoch.AddSeconds((sequence + 1) / 2);
            var mid = 100d + sequence;
            var bid = mid - 0.5d;
            var ask = mid + 0.5d;
            gateway.ObserveQuote(
                new Tick(latestQuoteTime, bid, ask, BidSize: 100, AskSize: 100),
                sourceSequence: sequence);
            ApplyFeedback(gateway, evaluator, feedbackHashes);

            var position = portfolio.SnapshotOf(instrument.Id).Quantity;
            var intent = evaluator.EvaluateQuote(new TradeIrQuoteFrameV1(
                plan.InstrumentKey,
                plan.AdmissionManifestSha256,
                sequence,
                UnixMicroseconds(latestQuoteTime),
                bid,
                ask,
                position));
            if (intent is null) continue;

            intentHashes.Add(ExecutionCanonicalJson.Hash(intent));
            gateway.Admit(intent).IsAdmitted.Should().BeTrue();
        }

        var flatten = evaluator.End(new TradeIrPortfolioFrameV1(
            plan.InstrumentKey,
            eventSequence: 15,
            eventTimeUnixMicroseconds: UnixMicroseconds(latestQuoteTime),
            currentPositionQuantity: portfolio.SnapshotOf(instrument.Id).Quantity));
        flatten.Should().NotBeNull();
        intentHashes.Add(ExecutionCanonicalJson.Hash(flatten!));
        gateway.Admit(flatten!).IsAdmitted.Should().BeTrue();

        gateway.ObserveQuote(new Tick(
            latestQuoteTime.AddSeconds(1),
            Bid: 114.5d,
            Ask: 115.5d,
            BidSize: 100,
            AskSize: 100),
            sourceSequence: 15);
        ApplyFeedback(gateway, evaluator, feedbackHashes);

        return new ReplayEvidence(
            plan.DefinitionSha256,
            plan.RuntimeSemanticsVersion,
            plan.Instructions.Select(DescribeInstruction).ToArray(),
            intentHashes,
            gateway.Decisions.Select(ExecutionCanonicalJson.Hash).ToArray(),
            gateway.Decisions.Select(static decision => decision.Code).ToArray(),
            feedbackHashes,
            bookEventHashes,
            bookStates,
            portfolio.SnapshotOf(instrument.Id).Quantity,
            gateway.SubmittedOrderCount);
    }

    private static void ApplyFeedback(
        TradeIrRiskGatewayV1 gateway,
        TradeIrEvaluatorV1 evaluator,
        ICollection<string> feedbackHashes)
    {
        foreach (var feedback in gateway.DrainFeedback())
        {
            feedbackHashes.Add(ExecutionCanonicalJson.Hash(feedback));
            evaluator.ApplyOrderFeedback(feedback);
        }
    }

    private static string DescribeInstruction(TradeIrInstructionV1 instruction) => instruction switch
    {
        QuoteMidInstructionV1 value => $"quote:{value.Slot}:{value.NodeId}:{value.RequirementId}",
        EmaInstructionV1 value => $"ema:{value.Slot}:{value.NodeId}:{value.ValueSlot}:{value.Period}",
        GreaterThanInstructionV1 value =>
            $"gt:{value.Slot}:{value.NodeId}:{value.LeftSlot}:{value.RightSlot}",
        FixedQuantityInstructionV1 value =>
            $"qty:{value.Slot}:{value.NodeId}:{value.DecisionSlot}:{value.WhenFalse}:{value.WhenTrue}",
        TrailingFractionInstructionV1 value =>
            $"trail:{value.Slot}:{value.NodeId}:{value.PriceSlot}:{value.TargetSlot}:{value.Fraction:R}",
        MarketIntentInstructionV1 value =>
            $"market:{value.Slot}:{value.NodeId}:{value.TargetSlot}:{value.ExitSlot}:{value.TimeInForce}",
        _ => throw new InvalidOperationException(instruction.GetType().FullName),
    };

    private static long UnixMicroseconds(DateTime time) => checked(
        (time.Ticks - DateTime.UnixEpoch.Ticks) / 10);

    private sealed record ReplayEvidence(
        string DefinitionSha256,
        string RuntimeSemanticsVersion,
        IReadOnlyList<string> Instructions,
        IReadOnlyList<string> IntentHashes,
        IReadOnlyList<string> DecisionHashes,
        IReadOnlyList<string> DecisionCodes,
        IReadOnlyList<string> FeedbackHashes,
        IReadOnlyList<string> BookEventHashes,
        IReadOnlyList<OrderState> BookStates,
        long TerminalQuantity,
        int SubmittedOrderCount);
}
