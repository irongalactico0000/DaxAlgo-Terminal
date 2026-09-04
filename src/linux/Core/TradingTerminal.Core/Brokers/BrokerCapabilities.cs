namespace TradingTerminal.Core.Brokers;

/// <summary>
/// Describes where the instrument universe exposed by a broker client comes from.
/// This is intentionally separate from feed delivery because a locally curated picker
/// can still address native broker market-data channels.
/// </summary>
public enum InstrumentCatalogMode
{
    Unsupported,
    Curated,
    Remote,
    Synthetic,
}

/// <summary>
/// Describes how one market-data channel is produced by this build.
/// </summary>
public enum MarketDataDeliveryMode
{
    Unsupported,
    Native,
    LocalAggregation,
    Synthetic,
}

/// <summary>
/// The market-data behavior implemented by one broker client. This is source capability,
/// not proof of account entitlement, symbol permission, or current vendor availability.
/// </summary>
public sealed record MarketDataCapabilities(
    InstrumentCatalogMode Instruments,
    MarketDataDeliveryMode HistoricalBars,
    MarketDataDeliveryMode LiveBars,
    MarketDataDeliveryMode Level1Quotes,
    MarketDataDeliveryMode Level2Depth,
    MarketDataDeliveryMode LiveTrades,
    MarketDataDeliveryMode HistoricalTrades)
{
    public bool SupportsInstruments => Instruments != InstrumentCatalogMode.Unsupported;
    public bool SupportsHistoricalBars => HistoricalBars != MarketDataDeliveryMode.Unsupported;
    public bool SupportsLiveBars => LiveBars != MarketDataDeliveryMode.Unsupported;
    public bool SupportsLevel1Quotes => Level1Quotes != MarketDataDeliveryMode.Unsupported;
    public bool SupportsLevel2Depth => Level2Depth != MarketDataDeliveryMode.Unsupported;
    public bool SupportsLiveTrades => LiveTrades != MarketDataDeliveryMode.Unsupported;
    public bool SupportsHistoricalTrades => HistoricalTrades != MarketDataDeliveryMode.Unsupported;
}

/// <summary>
/// Order/account behavior available independently of a market-data connection.
/// Every value is false in the current Mac build; the type exists so callers never infer
/// trading readiness from <c>IBrokerClient.ConnectionState</c>.
/// </summary>
public sealed record ExecutionCapabilities(
    bool CanSubmitOrders,
    bool CanCancelOrders,
    bool CanReplaceOrders,
    bool CanQueryOrders,
    bool CanReceiveExecutions,
    bool CanReadPositions,
    bool CanReadCash,
    bool CanReadMargin,
    bool CanReconcile)
{
    public static ExecutionCapabilities Unavailable { get; } = new(
        CanSubmitOrders: false,
        CanCancelOrders: false,
        CanReplaceOrders: false,
        CanQueryOrders: false,
        CanReceiveExecutions: false,
        CanReadPositions: false,
        CanReadCash: false,
        CanReadMargin: false,
        CanReconcile: false);

    public bool IsAvailable =>
        CanSubmitOrders || CanCancelOrders || CanReplaceOrders || CanQueryOrders ||
        CanReceiveExecutions || CanReadPositions || CanReadCash || CanReadMargin || CanReconcile;
}

/// <summary>
/// Data and execution are deliberately separate facts. A broker may provide native live data
/// while <see cref="Execution"/> remains unavailable.
/// </summary>
public sealed record BrokerCapabilities(
    MarketDataCapabilities MarketData,
    ExecutionCapabilities Execution);

/// <summary>
/// Authoritative source-capability matrix for the Mac product. Unknown broker values fail closed;
/// adding a <see cref="BrokerKind"/> requires adding an explicit catalog row and test row.
/// </summary>
public static class BrokerCapabilityCatalog
{
    public static BrokerCapabilities For(BrokerKind broker) => broker switch
    {
        BrokerKind.InteractiveBrokers => DataOnly(
            InstrumentCatalogMode.Curated,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Unsupported),

        BrokerKind.NinjaTrader => DataOnly(
            InstrumentCatalogMode.Curated,
            MarketDataDeliveryMode.Synthetic,
            MarketDataDeliveryMode.LocalAggregation,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported),

        BrokerKind.CTrader => DataOnly(
            InstrumentCatalogMode.Remote,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.LocalAggregation,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported),

        BrokerKind.Alpaca => DataOnly(
            InstrumentCatalogMode.Remote,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.LocalAggregation,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported),

        BrokerKind.Simulated => DataOnly(
            InstrumentCatalogMode.Synthetic,
            MarketDataDeliveryMode.Synthetic,
            MarketDataDeliveryMode.Synthetic,
            MarketDataDeliveryMode.Synthetic,
            MarketDataDeliveryMode.Synthetic,
            MarketDataDeliveryMode.Synthetic,
            MarketDataDeliveryMode.Unsupported),

        BrokerKind.Binance => PublicCrypto(historicalTrades: MarketDataDeliveryMode.Native),

        BrokerKind.IronBeam => DataOnly(
            InstrumentCatalogMode.Unsupported,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Unsupported),

        BrokerKind.LondonStrategicEdge => DataOnly(
            InstrumentCatalogMode.Remote,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported),

        BrokerKind.Upstox => DataOnly(
            InstrumentCatalogMode.Remote,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Unsupported,
            MarketDataDeliveryMode.Unsupported),

        BrokerKind.Coinbase or
        BrokerKind.Bybit or
        BrokerKind.Kraken or
        BrokerKind.Okx => PublicCrypto(historicalTrades: MarketDataDeliveryMode.Unsupported),

        _ => throw new ArgumentOutOfRangeException(nameof(broker), broker, "Unknown broker kind."),
    };

    public static MarketDataCapabilities MarketDataFor(BrokerKind broker) => For(broker).MarketData;

    public static ExecutionCapabilities ExecutionFor(BrokerKind broker) => For(broker).Execution;

    private static BrokerCapabilities PublicCrypto(MarketDataDeliveryMode historicalTrades) =>
        DataOnly(
            InstrumentCatalogMode.Curated,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Native,
            MarketDataDeliveryMode.Native,
            historicalTrades);

    private static BrokerCapabilities DataOnly(
        InstrumentCatalogMode instruments,
        MarketDataDeliveryMode historicalBars,
        MarketDataDeliveryMode liveBars,
        MarketDataDeliveryMode level1Quotes,
        MarketDataDeliveryMode level2Depth,
        MarketDataDeliveryMode liveTrades,
        MarketDataDeliveryMode historicalTrades) =>
        new(
            new MarketDataCapabilities(
                instruments,
                historicalBars,
                liveBars,
                level1Quotes,
                level2Depth,
                liveTrades,
                historicalTrades),
            ExecutionCapabilities.Unavailable);
}
