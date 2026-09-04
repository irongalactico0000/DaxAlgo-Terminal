using FluentAssertions;
using System.Reactive.Linq;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using Xunit;

namespace TradingTerminal.Tests.Brokers;

public sealed class BrokerCapabilityCatalogTests
{
    public static TheoryData<
        BrokerKind,
        InstrumentCatalogMode,
        MarketDataDeliveryMode,
        MarketDataDeliveryMode,
        MarketDataDeliveryMode,
        MarketDataDeliveryMode,
        MarketDataDeliveryMode,
        MarketDataDeliveryMode> MarketDataMatrix => new()
    {
        { BrokerKind.InteractiveBrokers, InstrumentCatalogMode.Curated, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.NinjaTrader, InstrumentCatalogMode.Curated, MarketDataDeliveryMode.Synthetic, MarketDataDeliveryMode.LocalAggregation, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.CTrader, InstrumentCatalogMode.Remote, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.LocalAggregation, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.Alpaca, InstrumentCatalogMode.Remote, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.LocalAggregation, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.Simulated, InstrumentCatalogMode.Synthetic, MarketDataDeliveryMode.Synthetic, MarketDataDeliveryMode.Synthetic, MarketDataDeliveryMode.Synthetic, MarketDataDeliveryMode.Synthetic, MarketDataDeliveryMode.Synthetic, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.Binance, InstrumentCatalogMode.Curated, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native },
        { BrokerKind.IronBeam, InstrumentCatalogMode.Unsupported, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.LondonStrategicEdge, InstrumentCatalogMode.Remote, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.Upstox, InstrumentCatalogMode.Remote, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.Coinbase, InstrumentCatalogMode.Curated, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.Bybit, InstrumentCatalogMode.Curated, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.Kraken, InstrumentCatalogMode.Curated, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported },
        { BrokerKind.Okx, InstrumentCatalogMode.Curated, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Native, MarketDataDeliveryMode.Unsupported },
    };

    [Theory]
    [MemberData(nameof(MarketDataMatrix))]
    public void Market_data_matrix_is_explicit_for_every_broker(
        BrokerKind broker,
        InstrumentCatalogMode instruments,
        MarketDataDeliveryMode historicalBars,
        MarketDataDeliveryMode liveBars,
        MarketDataDeliveryMode level1Quotes,
        MarketDataDeliveryMode level2Depth,
        MarketDataDeliveryMode liveTrades,
        MarketDataDeliveryMode historicalTrades)
    {
        var actual = BrokerCapabilityCatalog.MarketDataFor(broker);

        actual.Should().Be(new MarketDataCapabilities(
            instruments,
            historicalBars,
            liveBars,
            level1Quotes,
            level2Depth,
            liveTrades,
            historicalTrades));
    }

    [Fact]
    public void Matrix_covers_every_declared_broker_exactly_once()
    {
        var matrixBrokers = MarketDataMatrix.Select(row => (BrokerKind)row[0]).ToArray();

        matrixBrokers.Should().BeEquivalentTo(Enum.GetValues<BrokerKind>());
        matrixBrokers.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Every_broker_is_explicitly_data_only_in_this_build()
    {
        foreach (var broker in Enum.GetValues<BrokerKind>())
        {
            var capabilities = BrokerCapabilityCatalog.For(broker);

            capabilities.MarketData.SupportsLevel1Quotes.Should().BeTrue($"{broker} supplies L1 data");
            capabilities.Execution.IsAvailable.Should().BeFalse(
                $"{broker} market-data availability must not imply order execution");
        }
    }

    [Fact]
    public void Broker_client_exposes_its_catalog_market_data_capability()
    {
        foreach (var broker in Enum.GetValues<BrokerKind>())
        {
            IBrokerClient client = new CapabilityProbeBrokerClient(broker);

            client.MarketDataCapabilities.Should().Be(
                BrokerCapabilityCatalog.MarketDataFor(broker),
                $"the {broker} client contract should use the authoritative catalog");
        }
    }

    [Fact]
    public void Unknown_broker_values_fail_closed()
    {
        var act = () => BrokerCapabilityCatalog.For((BrokerKind)int.MaxValue);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("broker");
    }

    [Fact]
    public void Support_flags_are_derived_from_delivery_modes()
    {
        var ironBeam = BrokerCapabilityCatalog.MarketDataFor(BrokerKind.IronBeam);
        var binance = BrokerCapabilityCatalog.MarketDataFor(BrokerKind.Binance);

        ironBeam.SupportsInstruments.Should().BeFalse();
        ironBeam.SupportsHistoricalBars.Should().BeFalse();
        ironBeam.SupportsLiveBars.Should().BeFalse();
        ironBeam.SupportsLevel1Quotes.Should().BeTrue();
        ironBeam.SupportsLevel2Depth.Should().BeTrue();
        ironBeam.SupportsLiveTrades.Should().BeTrue();
        ironBeam.SupportsHistoricalTrades.Should().BeFalse();

        binance.SupportsInstruments.Should().BeTrue();
        binance.SupportsHistoricalBars.Should().BeTrue();
        binance.SupportsLiveBars.Should().BeTrue();
        binance.SupportsLevel1Quotes.Should().BeTrue();
        binance.SupportsLevel2Depth.Should().BeTrue();
        binance.SupportsLiveTrades.Should().BeTrue();
        binance.SupportsHistoricalTrades.Should().BeTrue();
    }

    private sealed class CapabilityProbeBrokerClient(BrokerKind kind) : IBrokerClient
    {
        public BrokerKind Kind => kind;
        public IObservable<ConnectionState> ConnectionState => Observable.Never<ConnectionState>();

        public Task ConnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<TradableInstrument>> ListInstrumentsAsync(
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<TradableInstrument>>(Array.Empty<TradableInstrument>());

        public Task DisconnectAsync(CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Bar>> RequestHistoricalBarsAsync(
            Contract contract,
            BarSize barSize,
            TimeSpan duration,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Bar>>(Array.Empty<Bar>());

        public IAsyncEnumerable<Bar> SubscribeBarsAsync(
            Contract contract,
            BarSize barSize,
            CancellationToken ct = default) =>
            EmptyAsync<Bar>();

        public IAsyncEnumerable<Tick> SubscribeTicksAsync(
            Contract contract,
            CancellationToken ct = default) =>
            EmptyAsync<Tick>();

        public IAsyncEnumerable<DepthSnapshot> SubscribeDepthAsync(
            Contract contract,
            int levels = 10,
            CancellationToken ct = default) =>
            EmptyAsync<DepthSnapshot>();

        public IAsyncEnumerable<TradeTick> SubscribeTradesAsync(
            Contract contract,
            CancellationToken ct = default) =>
            EmptyAsync<TradeTick>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private static async IAsyncEnumerable<T> EmptyAsync<T>()
        {
            await Task.CompletedTask;
            yield break;
        }
    }
}
