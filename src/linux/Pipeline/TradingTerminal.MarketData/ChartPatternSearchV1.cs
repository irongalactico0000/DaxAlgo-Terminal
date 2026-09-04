using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.Infrastructure.MarketData;

/// <summary>
/// Searches the latest persisted OHLCV window for every eligible canonical instrument. It never
/// substitutes a visual-model guess for market history and never labels similarity as a forecast.
/// </summary>
public sealed class ChartPatternSearchV1 : IChartPatternSearchV1
{
    private readonly IMarketDataStore _store;
    private readonly IInstrumentRegistry _registry;
    private readonly IMarketDataRepository? _repository;
    private readonly IBrokerSelector? _selector;

    public ChartPatternSearchV1(IMarketDataStore store, IInstrumentRegistry registry)
        : this(store, registry, repository: null, selector: null)
    {
    }

    public ChartPatternSearchV1(
        IMarketDataStore store,
        IInstrumentRegistry registry,
        IMarketDataRepository? repository,
        IBrokerSelector? selector)
    {
        _store = store;
        _registry = registry;
        _repository = repository;
        _selector = selector;
    }

    public async Task<ChartPatternSearchResultV1> SearchAsync(
        ChartPatternSearchRequestV1 request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ReferenceId))
            throw new ArgumentException("A reference id is required.", nameof(request));
        if (request.ReferenceContentHashSha256 is not { Length: 64 } ||
            request.ReferenceContentHashSha256.Any(static character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
            throw new ArgumentException("A lowercase reference SHA-256 hash is required.", nameof(request));
        if (!Enum.IsDefined(request.Timeframe))
            throw new ArgumentException("A known bar timeframe is required.", nameof(request));
        if (!Enum.IsDefined(request.Scope))
            throw new ArgumentException("A known candidate scope is required.", nameof(request));
        if (request.CandidateBarCount is < 16 or > 5_000)
            throw new ArgumentOutOfRangeException(nameof(request), "Candidate bar count must be between 16 and 5,000.");
        if (request.MaxResults is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(request), "Maximum results must be between 1 and 50.");
        if (request.MaxRemoteHydrations is < 0 or > 500)
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "Maximum remote history hydrations must be between 0 and 500.");
        if (request.Scope == ChartPatternCandidateScopeV1.SameAssetClass && request.ReferenceAssetClass is null)
            throw new ArgumentException("Same-asset-class search requires the reference asset class.", nameof(request));
        ChartPatternSimilarityCalculatorV1.ValidateFingerprint(request.Fingerprint);

        var eligible = _registry.All()
            .Where(instrument => !instrument.Id.IsNone)
            .Where(instrument => request.ExcludeInstrumentId is not { } excluded || instrument.Id != excluded)
            .Where(instrument => request.Scope switch
            {
                ChartPatternCandidateScopeV1.IndexesOnly => instrument.AssetClass == AssetClass.Index,
                ChartPatternCandidateScopeV1.SameAssetClass => instrument.AssetClass == request.ReferenceAssetClass,
                _ => true,
            })
            .OrderBy(static instrument => instrument.CanonicalSymbol, StringComparer.Ordinal)
            .ThenBy(static instrument => instrument.Id.Value)
            .ToArray();

        var matches = new List<ChartPatternMatchV1>();
        var withHistory = 0;
        var hydrationAttempts = 0;
        var hydrationSuccesses = 0;
        var hydrationFailures = 0;
        var hydrationLimitReached = false;
        foreach (var instrument in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var recent = await _store.GetRecentBarsAsync(
                instrument.Id,
                request.Timeframe,
                request.CandidateBarCount,
                request.PreferredSource,
                cancellationToken).ConfigureAwait(false);

            var usable = SelectOneProvenance(recent, request.PreferredSource, request.CandidateBarCount);
            if (usable.Count < 16 && request.HydrateMissingHistory &&
                _repository is not null && _selector is not null)
            {
                if (hydrationAttempts >= request.MaxRemoteHydrations)
                {
                    hydrationLimitReached = true;
                }
                else if (TryResolveHistoricalRoute(instrument, request.PreferredSource, out var broker, out var contract))
                {
                    hydrationAttempts++;
                    try
                    {
                        var duration = TimeSpan.FromTicks(
                            checked(request.Timeframe.ToTimeSpan().Ticks * request.CandidateBarCount));
                        var fetched = await _repository.GetHistoricalBarsAsync(
                            contract,
                            broker,
                            request.Timeframe,
                            duration,
                            cancellationToken).ConfigureAwait(false);
                        var canonical = fetched.Select(bar => OhlcvBar.FromBar(
                            bar,
                            instrument.Id,
                            request.Timeframe,
                            broker,
                            isFinal: true)).ToArray();
                        usable = SelectOneProvenance(canonical, broker, request.CandidateBarCount);
                        if (usable.Count >= 16)
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
            if (usable.Count < 16) continue;
            withHistory++;

            ChartPatternScoreV1 score;
            try
            {
                score = ChartPatternSimilarityCalculatorV1.Score(request.Fingerprint, usable);
            }
            catch (ArgumentException)
            {
                // A flat or corrupt candidate contains no comparable shape. It is coverage, not a match.
                continue;
            }

            matches.Add(new ChartPatternMatchV1(
                instrument.Id,
                instrument.CanonicalSymbol,
                instrument.AssetClass,
                instrument.Exchange,
                request.Timeframe,
                usable[0].Source,
                usable.Count,
                usable[0].OpenTimeUtc,
                usable[^1].OpenTimeUtc,
                score.Score,
                score.ShapeSimilarity,
                score.ReturnCorrelation,
                score.VolatilitySimilarity,
                score.DrawdownSimilarity));
        }

        var ranked = matches
            .OrderByDescending(static match => match.Score)
            .ThenBy(static match => match.CanonicalSymbol, StringComparer.Ordinal)
            .ThenBy(static match => match.InstrumentId.Value)
            .Take(request.MaxResults)
            .ToArray();
        var scope = request.Scope switch
        {
            ChartPatternCandidateScopeV1.IndexesOnly => "known indexes",
            ChartPatternCandidateScopeV1.SameAssetClass => $"known {request.ReferenceAssetClass} instruments",
            _ => "known instruments",
        };
        var hydration = hydrationAttempts == 0
            ? string.Empty
            : $" Missing local history triggered {hydrationAttempts} connected-broker request(s): " +
              $"{hydrationSuccesses} supplied enough bars and {hydrationFailures} did not.";
        var limit = hydrationLimitReached
            ? $" The remote hydration safety limit ({request.MaxRemoteHydrations}) was reached; narrow the scope or populate more local history for complete coverage."
            : string.Empty;
        var explanation =
            $"Compared the reference shape with the latest {request.CandidateBarCount} local or connected-broker " +
            $"{request.Timeframe.ToDisplayString()} bars for {withHistory} of {eligible.Length} eligible {scope}." +
            hydration + limit +
            " Scores describe historical shape similarity only; they do not predict direction or return.";

        return new ChartPatternSearchResultV1(
            request.ReferenceId,
            request.ReferenceContentHashSha256,
            request.Timeframe,
            request.Scope,
            ranked,
            eligible.Length,
            withHistory,
            explanation,
            hydrationAttempts,
            hydrationSuccesses,
            hydrationFailures,
            hydrationLimitReached);
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
        AssetClass.Option => "OPT",
        AssetClass.Index => "IND",
        _ => "UNKNOWN",
    };

    private static IReadOnlyList<OhlcvBar> SelectOneProvenance(
        IReadOnlyList<OhlcvBar> bars,
        BrokerKind? preferredSource,
        int maximumCount)
    {
        var candidates = bars
            .Where(static bar => bar.IsFinal && double.IsFinite(bar.Close) && bar.Close > 0)
            .Where(bar => preferredSource is null || bar.Source == preferredSource)
            .GroupBy(static bar => bar.Source)
            .OrderByDescending(static group => group.Count())
            .ThenByDescending(static group => group.Max(bar => bar.OpenTimeUtc))
            .ThenBy(static group => group.Key)
            .FirstOrDefault();
        if (candidates is null) return [];

        return candidates
            .GroupBy(static bar => bar.OpenTimeUtc)
            .Select(static group => group.OrderByDescending(bar => bar.IsFinal).First())
            .OrderBy(static bar => bar.OpenTimeUtc)
            .TakeLast(maximumCount)
            .ToArray();
    }
}
