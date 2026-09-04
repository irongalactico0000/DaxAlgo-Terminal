using TradingTerminal.Core.Brokers;

namespace TradingTerminal.Core.Strategies;

/// <summary>
/// The broker capability matrix that backs <see cref="ITradingStrategy.SupportedBrokers"/>'s default:
/// which connected backends can actually serve the informative-extra data feeds. Bars + L1 are the
/// universal baseline (every broker), so a strategy that needs only those is broker-agnostic and
/// declares no specific brokers. Informative-extra eligibility is derived from
/// <see cref="BrokerCapabilityCatalog"/> so broker support has one source of truth.
/// </summary>
public static class StrategyBrokerCapability
{
    private const StrategyDataRequirement KnownRequirements =
        StrategyDataRequirement.L1 |
        StrategyDataRequirement.Bars |
        StrategyDataRequirement.Depth |
        StrategyDataRequirement.TradeTape;

    private static readonly IReadOnlyList<BrokerKind> AllBrokers = Enum.GetValues<BrokerKind>();

    /// <summary>
    /// Backends whose source implementation exposes a live trade tape.
    /// </summary>
    public static readonly IReadOnlyList<BrokerKind> TapeBrokers =
        Filter(static capabilities => capabilities.SupportsLiveTrades);

    /// <summary>
    /// Backends whose source implementation exposes Level-2 market depth.
    /// </summary>
    public static readonly IReadOnlyList<BrokerKind> DepthBrokers =
        Filter(static capabilities => capabilities.SupportsLevel2Depth);

    /// <summary>
    /// The brokers that can fully drive a strategy with the given data appetite. Tape-requiring
    /// strategies map to <see cref="TapeBrokers"/>, depth-requiring to <see cref="DepthBrokers"/>,
    /// and L1/Bars-only strategies to the empty list (broker-agnostic — runs on any backend).
    /// </summary>
    public static IReadOnlyList<BrokerKind> ForRequirement(StrategyDataRequirement requirement)
    {
        if ((requirement & ~KnownRequirements) != 0)
            throw new ArgumentOutOfRangeException(
                nameof(requirement), requirement, "Unknown strategy data requirement flags.");

        var requiresTape = requirement.HasFlag(StrategyDataRequirement.TradeTape);
        var requiresDepth = requirement.HasFlag(StrategyDataRequirement.Depth);
        if (!requiresTape && !requiresDepth) return Array.Empty<BrokerKind>();

        return Filter(capabilities =>
            (!requiresTape || capabilities.SupportsLiveTrades) &&
            (!requiresDepth || capabilities.SupportsLevel2Depth));
    }

    private static IReadOnlyList<BrokerKind> Filter(Func<MarketDataCapabilities, bool> predicate) =>
        AllBrokers
            .Where(broker => predicate(BrokerCapabilityCatalog.MarketDataFor(broker)))
            .ToArray();
}
