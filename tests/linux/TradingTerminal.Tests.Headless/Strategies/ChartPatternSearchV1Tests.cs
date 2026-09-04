using FluentAssertions;
using NSubstitute;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies.Generation;
using TradingTerminal.Infrastructure.MarketData;
using Xunit;

namespace TradingTerminal.Tests.Headless.Strategies;

public sealed class ChartPatternSearchV1Tests
{
    private const string ReferenceHash =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public async Task Search_ranks_actual_matching_history_and_preserves_provenance()
    {
        var matching = Instrument.New("NDX", AssetClass.Index, "NASDAQ") with { Id = new InstrumentId(1) };
        var opposite = Instrument.New("VIX", AssetClass.Index, "CBOE") with { Id = new InstrumentId(2) };
        var registry = Substitute.For<IInstrumentRegistry>();
        registry.All().Returns([matching, opposite]);
        var store = Substitute.For<IMarketDataStore>();
        store.GetRecentBarsAsync(
                Arg.Any<InstrumentId>(), BarSize.OneHour, Arg.Any<int>(), Arg.Any<BrokerKind?>(), Arg.Any<CancellationToken>())
            .Returns(call => Bars(
                (InstrumentId)call[0] == matching.Id
                    ? [1, 2, 3, 2.5, 4, 5, 4.5, 6, 7, 8, 7.5, 9, 10, 9.5, 11, 12]
                    : [12, 11, 10, 10.5, 9, 8, 8.5, 7, 6, 5, 5.5, 4, 3, 3.5, 2, 1],
                (InstrumentId)call[0]));
        var fingerprint = new ChartPatternFingerprintV1(
            [1, 2, 3, 2.5, 4, 5, 4.5, 6, 7, 8, 7.5, 9, 10, 9.5, 11, 12]);

        var result = await new ChartPatternSearchV1(store, registry).SearchAsync(
            new ChartPatternSearchRequestV1(
                "reference", ReferenceHash, fingerprint, BarSize.OneHour,
                CandidateBarCount: 16, MaxResults: 2));

        result.Matches.Should().HaveCount(2);
        result.Matches[0].CanonicalSymbol.Should().Be("NDX");
        result.Matches[0].Source.Should().Be(BrokerKind.InteractiveBrokers);
        result.Matches[0].Score.Should().BeGreaterThan(result.Matches[1].Score);
        result.Explanation.Should().Contain("do not predict");
    }

    [Fact]
    public async Task Index_scope_never_returns_equities_even_when_their_shape_is_closer()
    {
        var index = Instrument.New("SPX", AssetClass.Index, "CBOE") with { Id = new InstrumentId(1) };
        var equity = Instrument.New("AAPL", AssetClass.Equity, "NASDAQ") with { Id = new InstrumentId(2) };
        var registry = Substitute.For<IInstrumentRegistry>();
        registry.All().Returns([index, equity]);
        var store = Substitute.For<IMarketDataStore>();
        store.GetRecentBarsAsync(
                Arg.Any<InstrumentId>(), BarSize.OneDay, Arg.Any<int>(), Arg.Any<BrokerKind?>(), Arg.Any<CancellationToken>())
            .Returns(call => Bars(
                [1, 1.2, 1.1, 1.4, 1.3, 1.6, 1.5, 1.9, 1.8, 2.1, 2, 2.4, 2.2, 2.6, 2.5, 3],
                (InstrumentId)call[0],
                BarSize.OneDay));

        var result = await new ChartPatternSearchV1(store, registry).SearchAsync(
            new ChartPatternSearchRequestV1(
                "reference", ReferenceHash,
                new ChartPatternFingerprintV1([1, 1.2, 1.1, 1.4, 1.3, 1.6, 1.5, 1.9]),
                BarSize.OneDay,
                CandidateBarCount: 16,
                Scope: ChartPatternCandidateScopeV1.IndexesOnly));

        result.Matches.Should().ContainSingle().Which.AssetClass.Should().Be(AssetClass.Index);
        await store.DidNotReceive().GetRecentBarsAsync(
            equity.Id, Arg.Any<BarSize>(), Arg.Any<int>(), Arg.Any<BrokerKind?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Empty_local_cache_hydrates_from_connected_historical_broker_and_scores_fresh_bars()
    {
        const BrokerKind broker = BrokerKind.InteractiveBrokers;
        var index = Instrument.New("SPX", AssetClass.Index, "CBOE") with { Id = new InstrumentId(9) };
        var registry = Substitute.For<IInstrumentRegistry>();
        registry.All().Returns([index]);
        registry.ToBrokerSymbol(index.Id, broker).Returns("SPX");
        var store = Substitute.For<IMarketDataStore>();
        store.GetRecentBarsAsync(
                index.Id, BarSize.OneHour, 16, null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<OhlcvBar>());
        var selector = Substitute.For<IBrokerSelector>();
        var client = Substitute.For<IBrokerClient>();
        selector.Connected.Returns([broker]);
        selector.IsAvailable(broker).Returns(true);
        selector.IsConnected(broker).Returns(true);
        selector.Get(broker).Returns(client);
        client.MarketDataCapabilities.Returns(BrokerCapabilityCatalog.MarketDataFor(broker));
        var repository = Substitute.For<IMarketDataRepository>();
        var fetched = Bars(
                [1, 2, 3, 2.5, 4, 5, 4.5, 6, 7, 8, 7.5, 9, 10, 9.5, 11, 12],
                index.Id)
            .Select(static bar => bar.ToBar())
            .ToArray();
        repository.GetHistoricalBarsAsync(
                Arg.Any<Contract>(), broker, BarSize.OneHour, TimeSpan.FromHours(16), Arg.Any<CancellationToken>())
            .Returns(fetched);

        var result = await new ChartPatternSearchV1(store, registry, repository, selector).SearchAsync(
            new ChartPatternSearchRequestV1(
                "reference",
                ReferenceHash,
                new ChartPatternFingerprintV1([1, 2, 3, 2.5, 4, 5, 4.5, 6]),
                BarSize.OneHour,
                CandidateBarCount: 16,
                Scope: ChartPatternCandidateScopeV1.IndexesOnly));

        result.Matches.Should().ContainSingle().Which.CanonicalSymbol.Should().Be("SPX");
        result.RemoteHydrationAttempts.Should().Be(1);
        result.RemoteHydrationSuccesses.Should().Be(1);
        result.RemoteHydrationFailures.Should().Be(0);
        result.RemoteHydrationLimitReached.Should().BeFalse();
        result.Explanation.Should().Contain("connected-broker");
        await repository.Received(1).GetHistoricalBarsAsync(
            Arg.Is<Contract>(contract => contract.Symbol == "SPX" && contract.SecType == "IND"),
            broker,
            BarSize.OneHour,
            TimeSpan.FromHours(16),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Remote_hydration_is_bounded_and_reports_partial_coverage()
    {
        const BrokerKind broker = BrokerKind.InteractiveBrokers;
        var first = Instrument.New("AAA", AssetClass.Index, "TEST") with { Id = new InstrumentId(1) };
        var second = Instrument.New("BBB", AssetClass.Index, "TEST") with { Id = new InstrumentId(2) };
        var registry = Substitute.For<IInstrumentRegistry>();
        registry.All().Returns([first, second]);
        registry.ToBrokerSymbol(Arg.Any<InstrumentId>(), broker)
            .Returns(call => ((InstrumentId)call[0]).Value == 1 ? "AAA" : "BBB");
        var store = Substitute.For<IMarketDataStore>();
        store.GetRecentBarsAsync(
                Arg.Any<InstrumentId>(), BarSize.OneDay, 16, null, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<OhlcvBar>());
        var selector = Substitute.For<IBrokerSelector>();
        var client = Substitute.For<IBrokerClient>();
        selector.Connected.Returns([broker]);
        selector.IsAvailable(broker).Returns(true);
        selector.IsConnected(broker).Returns(true);
        selector.Get(broker).Returns(client);
        client.MarketDataCapabilities.Returns(BrokerCapabilityCatalog.MarketDataFor(broker));
        var repository = Substitute.For<IMarketDataRepository>();
        repository.GetHistoricalBarsAsync(
                Arg.Any<Contract>(), broker, BarSize.OneDay, TimeSpan.FromDays(16), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Bar>());

        var result = await new ChartPatternSearchV1(store, registry, repository, selector).SearchAsync(
            new ChartPatternSearchRequestV1(
                "reference",
                ReferenceHash,
                new ChartPatternFingerprintV1([1, 2, 1.5, 3, 2.5, 4, 3.5, 5]),
                BarSize.OneDay,
                CandidateBarCount: 16,
                Scope: ChartPatternCandidateScopeV1.IndexesOnly,
                MaxRemoteHydrations: 1));

        result.Matches.Should().BeEmpty();
        result.RemoteHydrationAttempts.Should().Be(1);
        result.RemoteHydrationFailures.Should().Be(1);
        result.RemoteHydrationLimitReached.Should().BeTrue();
        result.Explanation.Should().Contain("safety limit");
        await repository.Received(1).GetHistoricalBarsAsync(
            Arg.Any<Contract>(), broker, BarSize.OneDay, TimeSpan.FromDays(16), Arg.Any<CancellationToken>());
    }

    private static IReadOnlyList<OhlcvBar> Bars(
        IReadOnlyList<double> closes,
        InstrumentId instrumentId,
        BarSize size = BarSize.OneHour) =>
        closes.Select((close, index) => new OhlcvBar(
            instrumentId,
            size,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).Add(size.ToTimeSpan() * index),
            close,
            close,
            close,
            close,
            100 + index,
            BrokerKind.InteractiveBrokers,
            true)).ToArray();
}
