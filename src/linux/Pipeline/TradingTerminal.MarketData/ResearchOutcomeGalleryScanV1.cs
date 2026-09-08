using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.Infrastructure.MarketData;

/// <summary>
/// Scans S&amp;P 100 symbols present in the instrument registry against local (and optionally
/// broker-hydrated) daily bars for research outcome events. Research evidence only — not Paper.
/// </summary>
public sealed class ResearchOutcomeGalleryScanV1 : IResearchOutcomeGalleryScanV1
{
    private readonly IMarketDataStore _store;
    private readonly IInstrumentRegistry _registry;
    private readonly IMarketDataRepository? _repository;
    private readonly IBrokerSelector? _selector;

    public ResearchOutcomeGalleryScanV1(IMarketDataStore store, IInstrumentRegistry registry)
        : this(store, registry, repository: null, selector: null)
    {
    }

    public ResearchOutcomeGalleryScanV1(
        IMarketDataStore store,
        IInstrumentRegistry registry,
        IMarketDataRepository? repository,
        IBrokerSelector? selector)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        _repository = repository;
        _selector = selector;
    }

    public async Task<ResearchOutcomeGalleryResultV1> ScanAsync(
        ResearchOutcomeGalleryScanRequestV1 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!ResearchOutcomeEventFinderV1.TryDescribeScan(request.ScanId, out var displayName, out var prefix))
            throw new ArgumentException($"Unknown research scan id '{request.ScanId}'.", nameof(request));
        if (request.LookbackBars is < 30 or > 5_000)
            throw new ArgumentOutOfRangeException(nameof(request), "Lookback bars must be between 30 and 5,000.");
        if (request.MaxResults is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(request), "Maximum results must be between 1 and 50.");
        if (request.MaxRemoteHydrations is < 0 or > 500)
            throw new ArgumentOutOfRangeException(nameof(request), "Maximum remote hydrations must be between 0 and 500.");

        var universe = Sp100Sp500Catalog.Sp100
            .Select(static item => item.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var names = Sp100Sp500Catalog.Sp100
            .ToDictionary(static item => item.Symbol, static item => item.Name, StringComparer.OrdinalIgnoreCase);

        var eligible = _registry.All()
            .Where(instrument => !instrument.Id.IsNone)
            .Where(instrument => universe.Contains(instrument.CanonicalSymbol))
            .OrderBy(static instrument => instrument.CanonicalSymbol, StringComparer.Ordinal)
            .ThenBy(static instrument => instrument.Id.Value)
            .ToArray();

        var matches = new List<ResearchOutcomeGalleryMatchV1>();
        var withHistory = 0;
        var hydrationAttempts = 0;
        var hydrationSuccesses = 0;
        var hydrationFailures = 0;

        foreach (var instrument in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recent = await _store.GetRecentBarsAsync(
                instrument.Id,
                BarSize.OneDay,
                request.LookbackBars,
                request.PreferredSource,
                cancellationToken).ConfigureAwait(false);

            var usable = SelectOneProvenance(recent, request.PreferredSource, request.LookbackBars);
            if (usable.Count < ResearchOutcomeEventFinderV1.MinimumBars &&
                request.HydrateMissingHistory &&
                _repository is not null &&
                _selector is not null)
            {
                if (hydrationAttempts < request.MaxRemoteHydrations &&
                    TryResolveHistoricalRoute(instrument, request.PreferredSource, out var broker, out var contract))
                {
                    hydrationAttempts++;
                    try
                    {
                        var duration = TimeSpan.FromDays(request.LookbackBars + 5);
                        var fetched = await _repository.GetHistoricalBarsAsync(
                            contract,
                            broker,
                            BarSize.OneDay,
                            duration,
                            cancellationToken).ConfigureAwait(false);
                        var canonical = fetched.Select(bar => OhlcvBar.FromBar(
                            bar,
                            instrument.Id,
                            BarSize.OneDay,
                            broker,
                            isFinal: true)).ToArray();
                        usable = SelectOneProvenance(canonical, broker, request.LookbackBars);
                        if (usable.Count >= ResearchOutcomeEventFinderV1.MinimumBars)
                            hydrationSuccesses++;
                        else
                            hydrationFailures++;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception) when (exception is
                        InvalidOperationException or
                        NotSupportedException or
                        TimeoutException or
                        IOException or
                        HttpRequestException)
                    {
                        hydrationFailures++;
                    }
                }
            }

            if (usable.Count < ResearchOutcomeEventFinderV1.MinimumBars)
                continue;

            withHistory++;
            var symbol = instrument.CanonicalSymbol;
            names.TryGetValue(symbol, out var companyName);
            companyName ??= symbol;
            matches.AddRange(ResearchOutcomeEventFinderV1.FindEvents(
                request.ScanId,
                instrument.Id,
                symbol,
                companyName,
                usable));
        }

        var ranked = matches
            .OrderByDescending(static match => Math.Abs(match.OutcomeReturn))
            .ThenByDescending(static match => match.OutcomeFromUtc)
            .ThenBy(static match => match.CanonicalSymbol, StringComparer.Ordinal)
            .Take(request.MaxResults)
            .ToArray();

        var hydration = hydrationAttempts == 0
            ? string.Empty
            : $" Missing local history triggered {hydrationAttempts} connected-broker request(s): " +
              $"{hydrationSuccesses} supplied enough bars and {hydrationFailures} did not.";
        var explanation =
            $"{prefix} across S&P 100 symbols present in the instrument registry. " +
            $"Found {ranked.Length} event(s) from {withHistory} of {eligible.Length} eligible symbols " +
            $"(lookback {request.LookbackBars} daily bars).{hydration} " +
            "Gallery hits are research evidence for B/C/N labeling — not a trading signal and not Paper approval.";

        return new ResearchOutcomeGalleryResultV1(
            request.ScanId,
            displayName,
            ranked,
            eligible.Length,
            withHistory,
            explanation,
            hydrationAttempts,
            hydrationSuccesses,
            hydrationFailures);
    }

    private static IReadOnlyList<OhlcvBar> SelectOneProvenance(
        IReadOnlyList<OhlcvBar> bars,
        BrokerKind? preferred,
        int take)
    {
        if (bars.Count == 0)
            return Array.Empty<OhlcvBar>();

        IEnumerable<OhlcvBar> ordered = bars
            .Where(static bar => bar.Size == BarSize.OneDay)
            .OrderBy(static bar => bar.OpenTimeUtc);

        if (preferred is { } broker)
        {
            var filtered = ordered.Where(bar => bar.Source == broker).ToArray();
            if (filtered.Length > 0)
                ordered = filtered;
        }
        else
        {
            var bySource = ordered
                .GroupBy(static bar => bar.Source)
                .OrderByDescending(static group => group.Count())
                .FirstOrDefault();
            if (bySource is not null)
                ordered = bySource;
        }

        return ordered.TakeLast(take).ToArray();
    }

    private bool TryResolveHistoricalRoute(
        Instrument instrument,
        BrokerKind? preferredSource,
        out BrokerKind broker,
        out Contract contract)
    {
        broker = default;
        contract = null!;
        if (_selector is null) return false;

        var connected = preferredSource is { } preferred
            ? _selector.IsConnected(preferred) ? new[] { preferred } : []
            : _selector.Connected.OrderBy(static candidate => candidate).ToArray();
        foreach (var candidate in connected)
        {
            if (!_selector.IsAvailable(candidate) || !_selector.IsConnected(candidate)) continue;
            if (!_selector.Get(candidate).MarketDataCapabilities.SupportsHistoricalBars) continue;
            var symbol = _registry.ToBrokerSymbol(instrument.Id, candidate);
            if (string.IsNullOrWhiteSpace(symbol)) continue;

            broker = candidate;
            contract = new Contract(
                symbol,
                SecTypeFor(instrument.AssetClass),
                instrument.Exchange,
                instrument.Currency,
                instrument.Exchange);
            return true;
        }

        return false;
    }

    private static string SecTypeFor(AssetClass assetClass) => assetClass switch
    {
        AssetClass.Equity => "STK",
        AssetClass.Future => "FUT",
        AssetClass.Forex => "CASH",
        AssetClass.Crypto => "CRYPTO",
        AssetClass.Index => "IND",
        _ => "STK",
    };
}
