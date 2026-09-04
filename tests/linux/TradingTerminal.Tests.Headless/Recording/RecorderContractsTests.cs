using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Reactive.Subjects;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Configuration;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.MarketData.Archive;
using TradingTerminal.Recording;
using TradingTerminal.UI;
using TradingTerminal.UI.Logging;
using Xunit;

namespace TradingTerminal.Tests.Recording;

public sealed class RecorderContractsTests
{
    [Fact]
    public void L3_is_explicitly_unavailable()
    {
        RecorderEntry.SupportsL3.Should().BeFalse(
            "no broker client, canonical store stream, or feed currently produces market-by-order data");
    }

    [Fact]
    public void Watchlist_item_round_trips_the_full_instrument_and_pinned_broker()
    {
        var instrument = new SignalInstrument(
            "Bitcoin / US Dollar",
            "Crypto",
            new Contract("BTCUSDT", "CRYPTO", "BINANCE", "USD", "BINANCE"),
            BrokerKind.Binance);

        var saved = RecorderWatchlistItem.From(instrument, BrokerKind.Binance);

        saved.PinnedBroker.Should().Be(BrokerKind.Binance);
        saved.ToInstrument().Should().Be(instrument);
    }

    [Fact]
    public void Registration_uses_one_singleton_for_the_service_and_hosted_lifetime()
    {
        var descriptors = new ServiceCollection();

        descriptors.AddRecordingSurface();

        var recorderDescriptor = descriptors.Single(d => d.ServiceType == typeof(TickRecordingService));
        recorderDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);

        var hostedDescriptor = descriptors.Single(d => d.ServiceType == typeof(IHostedService));
        hostedDescriptor.Lifetime.Should().Be(ServiceLifetime.Singleton);
        hostedDescriptor.ImplementationFactory.Should().NotBeNull();

        var archiveOptions = Substitute.For<IOptionsMonitor<ArchiveOptions>>();
        archiveOptions.CurrentValue.Returns(new ArchiveOptions());
        var telegramOptions = Substitute.For<IOptionsMonitor<TelegramArchiveOptions>>();
        telegramOptions.CurrentValue.Returns(new TelegramArchiveOptions());

        using var recorder = new TickRecordingService(
            Substitute.For<IMarketDataIngest>(),
            Substitute.For<IMarketDataHub>(),
            Substitute.For<IBrokerSelector>(),
            Substitute.For<IMarketDataArchiver>(),
            archiveOptions,
            telegramOptions,
            new InMemoryLogSink(),
            NullLogger<TickRecordingService>.Instance);

        var provider = Substitute.For<IServiceProvider>();
        provider.GetService(typeof(TickRecordingService)).Returns(recorder);

        hostedDescriptor.ImplementationFactory!(provider).Should().BeSameAs(recorder);
    }

    [Fact]
    public void LondonStrategicEdge_records_only_its_declared_l1_channel()
    {
        using var fixture = RecorderFixture.Start(BrokerKind.LondonStrategicEdge);

        fixture.Entry.SupportsQuotes.Should().BeTrue();
        fixture.Entry.SupportsBars.Should().BeFalse();
        fixture.Entry.SupportsDepth.Should().BeFalse();
        fixture.Entry.SupportsTape.Should().BeFalse();
        fixture.Entry.Status.Should().Be("Recording L1 — unavailable: bars · L2 · tape");

        fixture.Ingest.Received(1).Subscribe(fixture.Contract, BrokerKind.LondonStrategicEdge);
        fixture.Ingest.DidNotReceiveWithAnyArgs().SubscribeBars(default!, default, default);
        fixture.Ingest.DidNotReceiveWithAnyArgs().SubscribeTrades(default!, default);
        fixture.Hub.Received(1).Quotes(fixture.InstrumentId);
        fixture.Hub.DidNotReceiveWithAnyArgs().Bars(default, default);
        fixture.Hub.DidNotReceiveWithAnyArgs().Depth(default);
        fixture.Hub.DidNotReceiveWithAnyArgs().Trades(default);
    }

    [Fact]
    public void IronBeam_keeps_l1_depth_and_tape_without_opening_absent_bars()
    {
        using var fixture = RecorderFixture.Start(BrokerKind.IronBeam);

        fixture.Entry.SupportsQuotes.Should().BeTrue();
        fixture.Entry.SupportsBars.Should().BeFalse();
        fixture.Entry.SupportsDepth.Should().BeTrue();
        fixture.Entry.SupportsTape.Should().BeTrue();
        fixture.Entry.Status.Should().Be("Recording L1 · L2 · tape — unavailable: bars");

        fixture.Ingest.Received(1).Subscribe(fixture.Contract, BrokerKind.IronBeam);
        fixture.Ingest.DidNotReceiveWithAnyArgs().SubscribeBars(default!, default, default);
        fixture.Ingest.Received(1).SubscribeTrades(fixture.Contract, BrokerKind.IronBeam);
        fixture.Hub.Received(1).Quotes(fixture.InstrumentId);
        fixture.Hub.DidNotReceiveWithAnyArgs().Bars(default, default);
        fixture.Hub.Received(1).Depth(fixture.InstrumentId);
        fixture.Hub.Received(1).Trades(fixture.InstrumentId);

        fixture.Service.StopRecording("test complete");
        fixture.Entry.IsLive.Should().BeFalse();
        fixture.Entry.ActiveBroker.Should().BeNull();
        fixture.Entry.SupportsQuotes.Should().BeFalse();
        fixture.Entry.SupportsBars.Should().BeFalse();
        fixture.Entry.SupportsDepth.Should().BeFalse();
        fixture.Entry.SupportsTape.Should().BeFalse();
    }

    [Fact]
    public void Simulated_opens_every_declared_live_channel()
    {
        using var fixture = RecorderFixture.Start(BrokerKind.Simulated);

        fixture.Entry.SupportsQuotes.Should().BeTrue();
        fixture.Entry.SupportsBars.Should().BeTrue();
        fixture.Entry.SupportsDepth.Should().BeTrue();
        fixture.Entry.SupportsTape.Should().BeTrue();
        fixture.Entry.Status.Should().Be("Recording L1 · bars · L2 · tape");

        fixture.Ingest.Received(1).Subscribe(fixture.Contract, BrokerKind.Simulated);
        fixture.Ingest.Received(1).SubscribeBars(
            fixture.Contract, BrokerKind.Simulated, BarSize.OneMinute);
        fixture.Ingest.Received(1).SubscribeTrades(fixture.Contract, BrokerKind.Simulated);
        fixture.Hub.Received(1).Quotes(fixture.InstrumentId);
        fixture.Hub.Received(1).Bars(fixture.InstrumentId, BarSize.OneMinute);
        fixture.Hub.Received(1).Depth(fixture.InstrumentId);
        fixture.Hub.Received(1).Trades(fixture.InstrumentId);
    }

    [Fact]
    public void Client_with_no_live_channels_performs_zero_ingest_or_hub_work()
    {
        var unavailable = new MarketDataCapabilities(
            InstrumentCatalogMode.Unsupported,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported);
        using var fixture = RecorderFixture.Start(BrokerKind.Alpaca, unavailable);

        fixture.Entry.IsLive.Should().BeFalse();
        fixture.Entry.Status.Should().Be(
            "No supported live streams — unavailable: L1 · bars · L2 · tape");
        fixture.Ingest.DidNotReceiveWithAnyArgs().Resolve(default!, default);
        fixture.Ingest.DidNotReceiveWithAnyArgs().Subscribe(default!, default);
        fixture.Ingest.DidNotReceiveWithAnyArgs().SubscribeBars(default!, default, default);
        fixture.Ingest.DidNotReceiveWithAnyArgs().SubscribeTrades(default!, default);
        fixture.Hub.DidNotReceiveWithAnyArgs().Quotes(default);
        fixture.Hub.DidNotReceiveWithAnyArgs().Bars(default, default);
        fixture.Hub.DidNotReceiveWithAnyArgs().Depth(default);
        fixture.Hub.DidNotReceiveWithAnyArgs().Trades(default);
    }

    private sealed class RecorderFixture : IDisposable
    {
        private RecorderFixture(BrokerKind broker, MarketDataCapabilities? capabilities)
        {
            Contract = Contract.UsStock("AAPL");
            InstrumentId = new InstrumentId(73);

            Ingest = Substitute.For<IMarketDataIngest>();
            Ingest.Resolve(Contract, broker).Returns(InstrumentId);
            Ingest.Subscribe(Contract, broker).Returns(Substitute.For<IDisposable>());
            Ingest.SubscribeBars(Contract, broker, Arg.Any<BarSize>())
                .Returns(Substitute.For<IDisposable>());
            Ingest.SubscribeTrades(Contract, broker).Returns(Substitute.For<IDisposable>());

            Hub = Substitute.For<IMarketDataHub>();
            Hub.Quotes(InstrumentId).Returns(new Subject<Quote>());
            Hub.Bars(InstrumentId, Arg.Any<BarSize>()).Returns(new Subject<OhlcvBar>());
            Hub.Depth(InstrumentId).Returns(new Subject<DepthSnapshot>());
            Hub.Trades(InstrumentId).Returns(new Subject<TradePrint>());

            var client = Substitute.For<IBrokerClient>();
            client.Kind.Returns(broker);
            client.MarketDataCapabilities.Returns(
                capabilities ?? BrokerCapabilityCatalog.MarketDataFor(broker));

            var selector = Substitute.For<IBrokerSelector>();
            selector.Connected.Returns(new[] { broker });
            selector.IsConnected(broker).Returns(true);
            selector.Get(broker).Returns(client);

            var archiveOptions = Substitute.For<IOptionsMonitor<ArchiveOptions>>();
            archiveOptions.CurrentValue.Returns(new ArchiveOptions());
            var telegramOptions = Substitute.For<IOptionsMonitor<TelegramArchiveOptions>>();
            telegramOptions.CurrentValue.Returns(new TelegramArchiveOptions());

            Service = new TickRecordingService(
                Ingest,
                Hub,
                selector,
                Substitute.For<IMarketDataArchiver>(),
                archiveOptions,
                telegramOptions,
                new InMemoryLogSink(),
                NullLogger<TickRecordingService>.Instance);

            Entry = new RecorderEntry(
                new SignalInstrument("Apple", "Equity", Contract, broker),
                broker);
            Service.Instruments.Add(Entry);
        }

        public Contract Contract { get; }
        public InstrumentId InstrumentId { get; }
        public IMarketDataIngest Ingest { get; }
        public IMarketDataHub Hub { get; }
        public TickRecordingService Service { get; }
        public RecorderEntry Entry { get; }

        public static RecorderFixture Start(
            BrokerKind broker,
            MarketDataCapabilities? capabilities = null)
        {
            var fixture = new RecorderFixture(broker, capabilities);
            fixture.Service.StartRecording();
            return fixture;
        }

        public void Dispose() => Service.Dispose();
    }
}
