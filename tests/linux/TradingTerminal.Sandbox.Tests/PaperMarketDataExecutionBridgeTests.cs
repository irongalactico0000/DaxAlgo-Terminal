using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;
using Xunit;

namespace TradingTerminal.Sandbox.Tests;

public sealed class PaperMarketDataExecutionBridgeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 1, 0, 0, TimeSpan.Zero);
    private static readonly InstrumentId Instrument = new(4242);
    private static readonly ExecutionResource Resource = new(
        new VenueId("paper"),
        new TradingAccountId("paper-bridge-account"),
        ExecutionEnvironment.SimulatedPaper);

    [Fact]
    public void Canonical_quotes_fill_waiting_order_and_commit_callbacks_through_oms()
    {
        var fixture = new Fixture();
        var observed = new List<OmsCommandResult>();
        using var bridge = new PaperMarketDataExecutionBridge(
            fixture.Hub,
            [Instrument],
            fixture.Venue,
            fixture.Oms,
            resultObserver: observed.Add);
        var submit = fixture.Submit(
            "buy",
            new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(3), limitPrice: P(101)));

        var working = fixture.Oms.Submit(submit, RiskContext(), Context("buy-submit"));
        fixture.Hub.PublishQuote(QuoteAt(sequence: 1, bidSize: 1, askSize: 2));
        var partial = fixture.Oms.Query(submit.ClientOrderId)!;
        fixture.Clock.UtcNow = Now.AddSeconds(1).UtcDateTime;
        fixture.Hub.PublishQuote(QuoteAt(sequence: 2, bidSize: 1, askSize: 2, seconds: 1));
        var filled = fixture.Oms.Query(submit.ClientOrderId)!;

        Assert.Equal(OrderLifecycleState.Working, working.Projection!.State);
        Assert.Equal(OrderLifecycleState.PartiallyFilled, partial.State);
        Assert.Equal(Q(2), partial.FilledQuantity);
        Assert.Equal(OrderLifecycleState.Filled, filled.State);
        Assert.Equal(Q(3), filled.FilledQuantity);
        Assert.Equal(P(100), filled.AverageFillPrice);
        Assert.Equal(2, bridge.ProcessedQuoteCount);
        Assert.Equal(2, observed.Count);
        Assert.All(observed, result => Assert.True(result.IsSuccess, result.Reason));
        Assert.Null(bridge.LastFault);
    }

    [Fact]
    public void Bid_and_ask_liquidity_are_consumed_independently()
    {
        var fixture = new Fixture();
        using var bridge = new PaperMarketDataExecutionBridge(
            fixture.Hub,
            [Instrument],
            fixture.Venue,
            fixture.Oms);
        var buy = fixture.Submit(
            "buy-side",
            new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(101)));
        var sell = fixture.Submit(
            "sell-side",
            new OrderTerms(OrderSide.Sell, OrderType.Limit, Q(2), limitPrice: P(98)));
        fixture.Oms.Submit(buy, RiskContext(), Context("buy-side-submit"));
        fixture.Oms.Submit(sell, RiskContext(), Context("sell-side-submit"));

        fixture.Hub.PublishQuote(QuoteAt(sequence: 1, bidSize: 1, askSize: 2));

        var bought = fixture.Oms.Query(buy.ClientOrderId)!;
        var sold = fixture.Oms.Query(sell.ClientOrderId)!;
        Assert.Equal(OrderLifecycleState.Filled, bought.State);
        Assert.Equal(Q(2), bought.FilledQuantity);
        Assert.Equal(OrderLifecycleState.PartiallyFilled, sold.State);
        Assert.Equal(Q(1), sold.FilledQuantity);
        Assert.Equal(P(99), sold.AverageFillPrice);
    }

    [Fact]
    public void Latest_exact_midpoint_is_exposed_only_after_an_accepted_quote()
    {
        var fixture = new Fixture();
        using var bridge = new PaperMarketDataExecutionBridge(
            fixture.Hub,
            [Instrument],
            fixture.Venue,
            fixture.Oms);

        Assert.False(bridge.TryGetLatestReferencePrice(Instrument, out _, out _));
        fixture.Hub.PublishQuote(QuoteAt(sequence: 1, bidSize: 2, askSize: 2));

        Assert.True(bridge.TryGetLatestReferencePrice(Instrument, out var price, out var observedAt));
        Assert.Equal(new ScaledPrice(995, 1), price);
        Assert.Equal(Now, observedAt);
        Assert.False(bridge.TryGetLatestReferencePrice(new InstrumentId(999), out _, out _));
    }

    [Fact]
    public void Invalid_duplicate_and_post_disposal_quotes_cannot_change_paper_state()
    {
        var fixture = new Fixture();
        var faults = new List<PaperMarketDataBridgeFault>();
        var bridge = new PaperMarketDataExecutionBridge(
            fixture.Hub,
            [Instrument],
            fixture.Venue,
            fixture.Oms,
            faultObserver: faults.Add);
        var submit = fixture.Submit(
            "guarded",
            new OrderTerms(OrderSide.Buy, OrderType.Limit, Q(2), limitPrice: P(101)));
        fixture.Oms.Submit(submit, RiskContext(), Context("guarded-submit"));

        fixture.Hub.PublishQuote(QuoteAt(sequence: 1, bid: 101, ask: 100, bidSize: 5, askSize: 5));
        Assert.Equal(OrderLifecycleState.Working, fixture.Oms.Query(submit.ClientOrderId)!.State);
        Assert.Equal(PaperMarketDataBridgeFaultKind.InvalidQuote, bridge.LastFault!.Kind);

        fixture.Hub.PublishQuote(QuoteAt(sequence: 2, bidSize: 1, askSize: 1));
        Assert.Equal(Q(1), fixture.Oms.Query(submit.ClientOrderId)!.FilledQuantity);
        fixture.Hub.PublishQuote(QuoteAt(sequence: 2, bidSize: 5, askSize: 5, seconds: 1));
        Assert.Equal(Q(1), fixture.Oms.Query(submit.ClientOrderId)!.FilledQuantity);
        Assert.Equal(PaperMarketDataBridgeFaultKind.StaleOrDuplicateQuote, bridge.LastFault!.Kind);

        bridge.Dispose();
        fixture.Hub.PublishQuote(QuoteAt(sequence: 3, bidSize: 5, askSize: 5, seconds: 2));

        Assert.Equal(OrderLifecycleState.PartiallyFilled, fixture.Oms.Query(submit.ClientOrderId)!.State);
        Assert.Equal(Q(1), fixture.Oms.Query(submit.ClientOrderId)!.FilledQuantity);
        Assert.Equal(1, bridge.ProcessedQuoteCount);
        Assert.Equal(2, bridge.RejectedQuoteCount);
        Assert.Equal(2, faults.Count);
    }

    private static Quote QuoteAt(
        long sequence,
        long bidSize,
        long askSize,
        double bid = 99,
        double ask = 100,
        int seconds = 0) =>
        new(
            Instrument,
            Now.AddSeconds(seconds).UtcDateTime,
            Now.AddSeconds(seconds).UtcDateTime,
            bid,
            ask,
            bidSize,
            askSize,
            BrokerKind.Simulated,
            sequence,
            EventTimeApproximate: false);

    private static RiskEvaluationContext RiskContext() =>
        new(
            new RiskLimits(
                Q(100),
                Q(100),
                M(1_000_000),
                ScaledMoney.Zero,
                M(100_000),
                M(100_000),
                100,
                TimeSpan.FromMinutes(1)),
            RiskControlMode.Active,
            false,
            ScaledQuantity.Zero,
            ScaledQuantity.Zero,
            ScaledQuantity.Zero,
            ScaledMoney.Zero,
            ScaledQuantity.Zero,
            ScaledMoney.Zero,
            ScaledQuantity.Zero,
            M(1_000_000),
            ScaledMoney.Zero,
            M(100_000),
            M(100_000),
            P(100),
            0,
            Now);

    private static OrderCommandContext Context(string suffix) =>
        new(new CausationId($"cause-{suffix}"), new DeduplicationKey($"dedupe-{suffix}"));

    private static ScaledQuantity Q(long value) => ScaledQuantity.FromWhole(value);
    private static ScaledPrice P(long value) => new(value, 0);
    private static ScaledMoney M(long value) => new(value, 0);

    private sealed class Fixture
    {
        private readonly ExecutionLeaseGrant _grant;

        public Fixture()
        {
            var leases = new InMemoryExecutionLeaseStore();
            _grant = leases.Acquire(
                Resource,
                new ExecutionLeaseId("paper-bridge-lease"),
                new RuntimeInstanceId("paper-bridge-runtime"),
                Now,
                Now.AddHours(1)).Grant!.Value;
            Hub = new TestMarketDataHub();
            Venue = new DeterministicPaperVenue();
            Clock = new TestClock(Now.UtcDateTime);
            Oms = new OrderManagementService(
                new InMemoryOrderEventStore(),
                Venue,
                leases,
                Clock);
        }

        public TestMarketDataHub Hub { get; }
        public DeterministicPaperVenue Venue { get; }
        public TestClock Clock { get; }
        public OrderManagementService Oms { get; }

        public SubmitOrderCommand Submit(string suffix, OrderTerms terms)
        {
            var metadata = new ExecutionCommandMetadata(
                new CommandId($"command-{suffix}"),
                new CorrelationId("paper-bridge-correlation"),
                new CausationId($"cause-{suffix}"),
                Resource.TradingAccountId,
                new StrategyId("paper-bridge-strategy"),
                new StrategyVersion("1.0.0"),
                Resource.VenueId,
                Instrument,
                Resource.Environment,
                Now,
                0);
            var signedQuantity = terms.Side == OrderSide.Buy
                ? terms.Quantity
                : new ScaledQuantity(-terms.Quantity.Coefficient, terms.Quantity.Scale);
            var mapping = new CanonicalInstructionMappingContext(
                new IntentId($"intent-{suffix}"),
                null,
                new LegId($"leg-{suffix}"),
                _grant.Claim.LeaseId,
                _grant.Claim.FencingToken,
                TradeIntentQuantityMode.Delta,
                signedQuantity,
                ScaledQuantity.Zero,
                null,
                null,
                ScaledMoney.Zero,
                1,
                "paper-bridge-policy",
                entryLimitPrice: terms.LimitPrice,
                entryStopPrice: terms.StopPrice);
            var fault = CanonicalOrderInstructionMapper.TryCreate(
                metadata,
                new ClientOrderId($"client-{suffix}"),
                terms,
                mapping,
                out var instruction);
            Assert.Equal(OrderDomainFault.None, fault);
            return new SubmitOrderCommand(
                metadata,
                new OrderId($"order-{suffix}"),
                new ClientOrderId($"client-{suffix}"),
                terms,
                instruction!);
        }
    }

    private sealed class TestMarketDataHub : IMarketDataHub
    {
        private readonly Dictionary<InstrumentId, TestObservable<Quote>> _quotes = [];

        public IObservable<Quote> Quotes(InstrumentId instrumentId) => Stream(instrumentId);
        public IObservable<TradePrint> Trades(InstrumentId instrumentId) => new TestObservable<TradePrint>();
        public IObservable<OhlcvBar> Bars(InstrumentId instrumentId, BarSize size) => new TestObservable<OhlcvBar>();
        public IObservable<DepthSnapshot> Depth(InstrumentId instrumentId) => new TestObservable<DepthSnapshot>();
        public void PublishQuote(Quote quote) => Stream(quote.InstrumentId).Publish(quote);
        public void PublishTrade(TradePrint trade) { }
        public void PublishBar(OhlcvBar bar) { }
        public void PublishDepth(InstrumentId instrumentId, DepthSnapshot snapshot) { }

        private TestObservable<Quote> Stream(InstrumentId instrument) =>
            _quotes.TryGetValue(instrument, out var stream)
                ? stream
                : _quotes[instrument] = new TestObservable<Quote>();
    }

    private sealed class TestClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; set; } = utcNow;
    }

    private sealed class TestObservable<T> : IObservable<T>
    {
        private readonly object _gate = new();
        private readonly List<IObserver<T>> _observers = [];

        public IDisposable Subscribe(IObserver<T> observer)
        {
            lock (_gate) _observers.Add(observer);
            return new Subscription(this, observer);
        }

        public void Publish(T value)
        {
            IObserver<T>[] observers;
            lock (_gate) observers = [.. _observers];
            foreach (var observer in observers) observer.OnNext(value);
        }

        private void Remove(IObserver<T> observer)
        {
            lock (_gate) _observers.Remove(observer);
        }

        private sealed class Subscription(TestObservable<T> owner, IObserver<T> observer) : IDisposable
        {
            private int _disposed;
            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0) owner.Remove(observer);
            }
        }
    }
}
