using DaxAlgo.Sdk;
using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.MarketData;
using TradingTerminal.Core.Strategies;
using TradingTerminal.Core.Strategies.Parameters;
using TradingTerminal.Core.Time;
using TradingTerminal.Core.Trading;

namespace TradingTerminal.Sandbox;

/// <summary>One reviewed canonical instrument and its backtest routing contract.</summary>
public sealed record SdkBacktestInstrument(InstrumentId InstrumentId, Contract Contract);

/// <summary>
/// Runs the canonical SDK strategy contract inside the deterministic backtest engine. Replay data,
/// desired targets, working orders, references, and filled positions are retained per instrument;
/// all instruments still share the engine's risk manager and cash ledger.
/// </summary>
public sealed class SdkStrategyBacktestAdapter :
    IBacktestStrategy,
    IInstrumentAwareBacktestStrategy,
    IAsyncDisposable
{
    private const int RetentionBound = 2_048;

    private readonly IStrategyKernel _kernel;
    private readonly IReadOnlyDictionary<InstrumentId, SdkBacktestInstrument> _instruments;
    private readonly BarSize _barSize;
    private readonly BrokerKind _source;
    private readonly ReplayMarketDataView _data;
    private readonly ReplayVirtualBook _book;
    private readonly SandboxParameters _parameters;
    private readonly Dictionary<InstrumentId, VirtualTargetIntent> _desiredTargets = [];
    private readonly Dictionary<InstrumentId, WorkingTarget> _workingTargets = [];
    private readonly Dictionary<string, InstrumentId> _workingInstrumentByOrder = new(StringComparer.Ordinal);
    private readonly Dictionary<InstrumentId, long> _positions = [];
    private readonly Dictionary<InstrumentId, double> _lastReferencePrices = [];
    private ReplayStrategyContext? _context;
    private long _nextOrderId;
    private int _started;
    private int _stopped;
    private int _kernelDisposed;

    public SdkStrategyBacktestAdapter(
        IStrategyKernel kernel,
        InstrumentId instrument,
        Contract contract,
        BarSize barSize,
        BrokerKind source,
        IReadOnlyDictionary<string, object?>? parameterValues = null)
        : this(kernel, [new SdkBacktestInstrument(instrument, contract)], barSize, source, parameterValues)
    {
    }

    public SdkStrategyBacktestAdapter(
        IStrategyKernel kernel,
        IReadOnlyList<SdkBacktestInstrument> instruments,
        BarSize barSize,
        BrokerKind source,
        IReadOnlyDictionary<string, object?>? parameterValues = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        ArgumentNullException.ThrowIfNull(instruments);
        if (instruments.Count == 0 || instruments.Any(item => item.InstrumentId.IsNone || item.Contract is null))
            throw new ArgumentException("At least one resolved canonical instrument is required.", nameof(instruments));
        if (instruments.Select(item => item.InstrumentId).Distinct().Count() != instruments.Count)
            throw new ArgumentException("Canonical backtest instruments must be unique.", nameof(instruments));
        if (instruments.Select(item => item.Contract).Distinct().Count() != instruments.Count)
            throw new ArgumentException("Backtest routing contracts must be unique.", nameof(instruments));

        _instruments = instruments.ToDictionary(item => item.InstrumentId);
        _barSize = barSize;
        _source = source;
        _data = new ReplayMarketDataView(_instruments.Keys, kernel.DataRequirement, RetentionBound);
        _book = new ReplayVirtualBook(_instruments.Keys);
        _parameters = new SandboxParameters(kernel.Schema, parameterValues);
        foreach (var instrument in _instruments.Keys)
            _positions[instrument] = 0L;
    }

    /// <summary>Compatibility total for single-instrument callers; use <see cref="PositionFor"/> for baskets.</summary>
    public long Position => _positions.Values.Sum();

    public long PositionFor(InstrumentId instrument) =>
        _positions.TryGetValue(instrument, out var position)
            ? position
            : throw new KeyNotFoundException($"Instrument {instrument} is outside this backtest.");

    public async Task OnStartAsync(IClock clock, IOrderRouter router, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(router);
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("The SDK backtest adapter has already started.");
        if ((_kernel.DataRequirement & StrategyDataRequirement.Depth) != 0)
            throw new NotSupportedException("Quick Backtest cannot replay Level-2 depth for an SDK strategy.");

        var alerts = new MediatedAlertSink(
            _kernel.GetType().Name,
            clock,
            static (_, _, _) => { },
            static _ => { });
        _context = new ReplayStrategyContext(_data, clock, _parameters, _book, alerts);
        await _kernel.OnStartAsync(_context, ct).ConfigureAwait(false);
        await ReconcileTargetsAsync(router, ct).ConfigureAwait(false);
    }

    public Task OnTickAsync(Tick tick, IClock clock, IOrderRouter router, CancellationToken ct) =>
        OnMarketEventBatchAsync([
            new BacktestInstrumentEvent(Primary.InstrumentId, Primary.Contract, tick.TimestampUtc, Quote: tick),
        ], clock, router, ct);

    public Task OnBarAsync(Bar bar, IClock clock, IOrderRouter router, CancellationToken ct) =>
        OnMarketEventBatchAsync([
            new BacktestInstrumentEvent(
                Primary.InstrumentId, Primary.Contract, bar.TimestampUtc, Bar: bar, BarSize: _barSize),
        ], clock, router, ct);

    public Task OnTradeAsync(TradePrint trade, IClock clock, IOrderRouter router, CancellationToken ct) =>
        OnMarketEventBatchAsync([
            new BacktestInstrumentEvent(Primary.InstrumentId, Primary.Contract, trade.EventTimeUtc, Trade: trade),
        ], clock, router, ct);

    public async Task OnMarketEventBatchAsync(
        IReadOnlyList<BacktestInstrumentEvent> events,
        IClock clock,
        IOrderRouter router,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(events);
        var context = RequireContext();

        foreach (var replayEvent in events)
        {
            var instrument = ResolveReplayInstrument(replayEvent);
            if (replayEvent.Quote is { } tick)
            {
                _lastReferencePrices[instrument] = Mid(tick.Bid, tick.Ask);
                if ((_kernel.DataRequirement & StrategyDataRequirement.L1) != 0)
                {
                    _data.Add(new Quote(
                        instrument,
                        tick.TimestampUtc,
                        tick.TimestampUtc,
                        tick.Bid,
                        tick.Ask,
                        tick.BidSize,
                        tick.AskSize,
                        _source,
                        _data.NextSequence(),
                        EventTimeApproximate: false));
                }
            }
            else if (replayEvent.Bar is { } bar)
            {
                _lastReferencePrices[instrument] = bar.Close;
                if ((_kernel.DataRequirement & StrategyDataRequirement.Bars) != 0)
                {
                    _data.Add(OhlcvBar.FromBar(
                        bar,
                        instrument,
                        replayEvent.BarSize ?? _barSize,
                        _source,
                        isFinal: true));
                }
            }
            else if (replayEvent.Trade is { } trade)
            {
                _lastReferencePrices[instrument] = trade.Price;
                if ((_kernel.DataRequirement & StrategyDataRequirement.TradeTape) != 0)
                    _data.Add(trade with { InstrumentId = instrument });
            }
        }

        // All same-timestamp values are now visible. No value from a later timestamp has entered the view.
        foreach (var replayEvent in events)
        {
            var instrument = ResolveReplayInstrument(replayEvent);
            if (replayEvent.Quote is { } tick &&
                (_kernel.DataRequirement & StrategyDataRequirement.L1) != 0)
            {
                var quote = _data.LatestQuote(instrument, tick.TimestampUtc);
                await _kernel.OnQuoteAsync(quote, context, ct).ConfigureAwait(false);
            }
            else if (replayEvent.Bar is { } bar &&
                     (_kernel.DataRequirement & StrategyDataRequirement.Bars) != 0)
            {
                var canonical = _data.LatestBar(
                    instrument,
                    replayEvent.BarSize ?? _barSize,
                    bar.TimestampUtc);
                await _kernel.OnBarAsync(canonical, context, ct).ConfigureAwait(false);
            }
            else if (replayEvent.Trade is { } trade &&
                     (_kernel.DataRequirement & StrategyDataRequirement.TradeTape) != 0)
            {
                var canonical = _data.LatestTrade(instrument, trade.EventTimeUtc);
                await _kernel.OnTradeAsync(canonical, context, ct).ConfigureAwait(false);
            }
        }

        await ReconcileTargetsAsync(router, ct).ConfigureAwait(false);
    }

    public Task OnOrderEventAsync(OrderEvent evt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_workingInstrumentByOrder.TryGetValue(evt.ClientOrderId, out var instrument))
        {
            if (evt.LastFillQuantity > 0)
            {
                var signedFill = evt.Side == OrderSide.Buy
                    ? evt.LastFillQuantity
                    : -evt.LastFillQuantity;
                _positions[instrument] = checked(_positions[instrument] + signedFill);
            }

            if (evt.State is OrderState.Filled or OrderState.Cancelled or OrderState.Rejected)
            {
                _workingInstrumentByOrder.Remove(evt.ClientOrderId);
                if (_workingTargets.TryGetValue(instrument, out var working) &&
                    string.Equals(working.ClientOrderId, evt.ClientOrderId, StringComparison.Ordinal))
                {
                    _workingTargets.Remove(instrument);
                }
            }
        }
        return Task.CompletedTask;
    }

    public async Task OnEndAsync(IClock clock, IOrderRouter router, CancellationToken ct)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;
        var context = _context;
        if (context is null) return;
        try
        {
            await _kernel.OnStopAsync(context, ct).ConfigureAwait(false);
        }
        finally
        {
            _context = null;
            await DisposeKernelAsync().ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 1) == 0 && _context is { } context)
        {
            try { await _kernel.OnStopAsync(context, CancellationToken.None).ConfigureAwait(false); }
            finally { _context = null; }
        }
        await DisposeKernelAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    private async Task ReconcileTargetsAsync(IOrderRouter router, CancellationToken ct)
    {
        foreach (var target in _book.TakeAll())
            _desiredTargets[target.Instrument] = target;

        foreach (var (instrument, target) in _desiredTargets.OrderBy(pair => pair.Key.Value).ToArray())
        {
            ValidateTarget(target);
            var targetUnits = checked((long)target.TargetUnits);

            if (_workingTargets.TryGetValue(instrument, out var working))
            {
                if (working.Target == target) continue;
                _workingTargets.Remove(instrument);
                _workingInstrumentByOrder.Remove(working.ClientOrderId);
                await router.CancelOrderAsync(working.ClientOrderId, ct).ConfigureAwait(false);
            }

            var delta = checked(targetUnits - _positions[instrument]);
            if (delta == 0) continue;
            if (!_lastReferencePrices.TryGetValue(instrument, out var reference) ||
                reference <= 0d || !double.IsFinite(reference))
            {
                continue;
            }

            var type = target.EntryKind switch
            {
                VirtualEntryKind.Market => OrderType.Market,
                VirtualEntryKind.Limit => OrderType.Limit,
                VirtualEntryKind.Stop => OrderType.Stop,
                _ => throw new ArgumentOutOfRangeException(nameof(target.EntryKind), target.EntryKind, null),
            };
            var binding = _instruments[instrument];
            var request = new OrderRequest(
                $"SDK-BT-{Interlocked.Increment(ref _nextOrderId):D8}",
                binding.Contract,
                delta > 0 ? OrderSide.Buy : OrderSide.Sell,
                type,
                checked(Math.Abs(delta)),
                LimitPrice: type == OrderType.Limit ? target.EntryTriggerPrice : null,
                StopPrice: type == OrderType.Stop ? target.EntryTriggerPrice : null,
                TimeInForce.Day);
            var result = await router.PlaceOrderAsync(request, ct).ConfigureAwait(false);
            if (result.State is OrderState.PendingNew or OrderState.Working or OrderState.PartiallyFilled)
            {
                _workingTargets[instrument] = new WorkingTarget(request.ClientOrderId, target);
                _workingInstrumentByOrder[request.ClientOrderId] = instrument;
            }
        }
    }

    private InstrumentId ResolveReplayInstrument(BacktestInstrumentEvent replayEvent)
    {
        var instrument = replayEvent.InstrumentId;
        if (instrument.IsNone && _instruments.Count == 1)
            instrument = Primary.InstrumentId;
        if (!_instruments.TryGetValue(instrument, out var binding) ||
            binding.Contract != replayEvent.Contract)
        {
            throw new InvalidOperationException("Replay data is outside the reviewed backtest instrument set.");
        }
        var payloadCount = (replayEvent.Quote is null ? 0 : 1) +
                           (replayEvent.Trade is null ? 0 : 1) +
                           (replayEvent.Bar is null ? 0 : 1);
        if (payloadCount != 1)
            throw new InvalidOperationException("A replay event must contain exactly one payload.");
        return instrument;
    }

    private void ValidateTarget(VirtualTargetIntent target)
    {
        if (!_instruments.ContainsKey(target.Instrument))
            throw new InvalidOperationException("The SDK strategy submitted a target outside its reviewed instruments.");
        if (!double.IsFinite(target.TargetUnits) || target.TargetUnits != Math.Truncate(target.TargetUnits) ||
            target.TargetUnits is > long.MaxValue or < long.MinValue)
        {
            throw new InvalidOperationException("Quick Backtest requires whole, finite target units.");
        }
        if (target.EntryKind != VirtualEntryKind.Market &&
            (target.EntryTriggerPrice is not (> 0d) || !double.IsFinite(target.EntryTriggerPrice.Value)))
        {
            throw new InvalidOperationException("A pending SDK target requires a finite positive trigger price.");
        }
    }

    private SdkBacktestInstrument Primary => _instruments.OrderBy(pair => pair.Key.Value).First().Value;

    private ReplayStrategyContext RequireContext() =>
        _context ?? throw new InvalidOperationException("The SDK backtest adapter has not started.");

    private async ValueTask DisposeKernelAsync()
    {
        if (Interlocked.Exchange(ref _kernelDisposed, 1) != 0) return;
        if (_kernel is IAsyncDisposable asyncDisposable)
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        else if (_kernel is IDisposable disposable)
            disposable.Dispose();
    }

    private static double Mid(double bid, double ask) => bid + ((ask - bid) * 0.5d);

    private sealed record WorkingTarget(string ClientOrderId, VirtualTargetIntent Target);

    private sealed class ReplayVirtualBook(IEnumerable<InstrumentId> instruments) : IVirtualBook
    {
        private readonly HashSet<InstrumentId> _instruments = instruments.ToHashSet();
        private readonly Dictionary<InstrumentId, VirtualTargetIntent> _pending = [];

        public void SubmitTarget(VirtualTargetIntent intent)
        {
            ArgumentNullException.ThrowIfNull(intent);
            if (!_instruments.Contains(intent.Instrument))
                throw new InvalidOperationException("The strategy target is outside the reviewed backtest instruments.");
            _pending[intent.Instrument] = intent;
        }

        public IReadOnlyList<VirtualTargetIntent> TakeAll()
        {
            var values = _pending.Values.OrderBy(value => value.Instrument.Value).ToArray();
            _pending.Clear();
            return values;
        }
    }

    private sealed record ReplayStrategyContext(
        IMarketDataView Data,
        IClock Clock,
        IParameters Parameters,
        IVirtualBook Book,
        IAlertSink Alerts) : IStrategyRuntimeContext;

    private sealed class ReplayMarketDataView : IMarketDataView
    {
        private readonly int _bound;
        private readonly List<OhlcvBar> _bars = [];
        private readonly List<Quote> _quotes = [];
        private readonly List<TradePrint> _trades = [];
        private long _sequence;

        public ReplayMarketDataView(
            IEnumerable<InstrumentId> instruments,
            StrategyDataRequirement requirement,
            int bound)
        {
            _bound = bound;
            Instruments = instruments.ToHashSet();
            DataRequirement = requirement;
        }

        public IReadOnlySet<InstrumentId> Instruments { get; }
        public StrategyDataRequirement DataRequirement { get; }
        public long NextSequence() => _sequence++;

        public IReadOnlyList<OhlcvBar> RecentBars(InstrumentId instrument, BarSize size, int maxCount)
        {
            Validate(instrument, StrategyDataRequirement.Bars, maxCount);
            return Tail(_bars.Where(bar => bar.InstrumentId == instrument && bar.Size == size), maxCount);
        }

        public IReadOnlyList<Quote> RecentQuotes(InstrumentId instrument, int maxCount)
        {
            Validate(instrument, StrategyDataRequirement.L1, maxCount);
            return Tail(_quotes.Where(quote => quote.InstrumentId == instrument), maxCount);
        }

        public DepthSnapshot? LatestDepth(InstrumentId instrument)
        {
            Validate(instrument, StrategyDataRequirement.Depth, 1);
            return null;
        }

        public IReadOnlyList<TradePrint> RecentTrades(InstrumentId instrument, int maxCount)
        {
            Validate(instrument, StrategyDataRequirement.TradeTape, maxCount);
            return Tail(_trades.Where(trade => trade.InstrumentId == instrument), maxCount);
        }

        public void Add(OhlcvBar bar) => AddBounded(_bars, bar);
        public void Add(Quote quote) => AddBounded(_quotes, quote);
        public void Add(TradePrint trade) => AddBounded(_trades, trade);

        public OhlcvBar LatestBar(InstrumentId instrument, BarSize size, DateTime timestamp) =>
            _bars.Last(bar => bar.InstrumentId == instrument && bar.Size == size && bar.OpenTimeUtc == timestamp);

        public Quote LatestQuote(InstrumentId instrument, DateTime timestamp) =>
            _quotes.Last(quote => quote.InstrumentId == instrument && quote.EventTimeUtc == timestamp);

        public TradePrint LatestTrade(InstrumentId instrument, DateTime timestamp) =>
            _trades.Last(trade => trade.InstrumentId == instrument && trade.EventTimeUtc == timestamp);

        private void Validate(InstrumentId instrument, StrategyDataRequirement requirement, int maxCount)
        {
            if (!Instruments.Contains(instrument))
                throw new InvalidOperationException("The requested replay instrument is not authorized.");
            if ((DataRequirement & requirement) == 0)
                throw new InvalidOperationException($"The SDK strategy did not declare {requirement}.");
            if (maxCount <= 0 || maxCount > _bound)
                throw new ArgumentOutOfRangeException(nameof(maxCount));
        }

        private void AddBounded<T>(List<T> items, T value)
        {
            if (items.Count == _bound) items.RemoveAt(0);
            items.Add(value);
        }

        private static IReadOnlyList<T> Tail<T>(IEnumerable<T> source, int maxCount)
        {
            var values = source as IReadOnlyList<T> ?? source.ToArray();
            return values.Skip(Math.Max(0, values.Count - maxCount)).ToArray();
        }
    }
}
