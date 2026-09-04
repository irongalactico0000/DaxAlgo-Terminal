using DaxAlgo.Sdk;
using TradingTerminal.Core.Domain;
using TradingTerminal.Sandbox.Portfolio;

namespace TradingTerminal.Sandbox.Runtime;

/// <summary>
/// Routes one canonical strategy callback across bounded per-instrument model accounts. This is a
/// target-generation model, not a shared-cash portfolio: durable Paper cash, equity, positions and
/// P&amp;L remain authoritative in the Paper OMS ledger.
/// </summary>
public sealed class MultiInstrumentModelPortfolioAccount : IModelPortfolioAccount
{
    private readonly InstrumentId[] _instruments;
    private readonly Dictionary<InstrumentId, ModelPortfolioAccount> _accounts;
    private readonly Dictionary<InstrumentId, MarketReference> _latestReferences = [];
    private readonly HashSet<InstrumentId> _openAccounts = [];
    private readonly RecordingVirtualBook _book;
    private InstrumentId _currentInstrument;
    private bool _windowOpen;

    public MultiInstrumentModelPortfolioAccount(
        IReadOnlySet<InstrumentId> declaredInstruments,
        ModelPortfolioAccountConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(declaredInstruments);
        if (declaredInstruments.Count < 2)
        {
            throw new ArgumentException(
                "A multi-instrument model account requires at least two declared instruments.",
                nameof(declaredInstruments));
        }
        if (declaredInstruments.Any(static instrument => instrument.IsNone))
            throw new ArgumentException("Every declared instrument must be resolved.", nameof(declaredInstruments));

        _instruments = declaredInstruments.OrderBy(static instrument => instrument.Value).ToArray();
        _accounts = _instruments.ToDictionary(
            static instrument => instrument,
            instrument => new ModelPortfolioAccount(instrument, config));
        _book = new RecordingVirtualBook(declaredInstruments);
        _currentInstrument = _instruments[0];
    }

    public IVirtualBook Book => _book;

    public ModelPortfolioFault LastFault { get; private set; }

    /// <summary>
    /// Compatibility snapshot for consumers that display one row. Basket-aware consumers must use
    /// <see cref="Snapshots"/> and must not treat this leg's equity as aggregate portfolio equity.
    /// </summary>
    public SandboxPortfolioSnapshot Snapshot => _accounts[_currentInstrument].Snapshot;

    public IReadOnlyList<SandboxPortfolioSnapshot> Snapshots =>
        _instruments.Select(instrument => _accounts[instrument].Snapshot).ToArray();

    public void BeginBar(double close) => BeginBar(_currentInstrument, close);

    public void BeginTick(double bid, double ask, double last) =>
        BeginTick(_currentInstrument, bid, ask, last);

    public void BeginBar(InstrumentId instrument, double close) =>
        Begin(instrument, new MarketReference(true, 0d, 0d, close));

    public void BeginTick(InstrumentId instrument, double bid, double ask, double last) =>
        Begin(instrument, new MarketReference(false, bid, ask, last));

    public void ReconcileToTargets()
    {
        if (!_windowOpen || LastFault != ModelPortfolioFault.None)
            return;

        try
        {
            foreach (var intent in _book.RecordedIntents.OrderBy(static intent => intent.Instrument.Value))
            {
                if (!_accounts.TryGetValue(intent.Instrument, out var account))
                    continue;

                if (!_openAccounts.Contains(intent.Instrument) && !TryOpenFromLatest(intent.Instrument, account))
                {
                    LastFault = ModelPortfolioFault.InvalidReferencePrice;
                    return;
                }

                account.Book.SubmitTarget(intent);
            }

            foreach (var instrument in _openAccounts.OrderBy(static instrument => instrument.Value))
            {
                var account = _accounts[instrument];
                account.ReconcileToTargets();
                if (account.LastFault != ModelPortfolioFault.None)
                {
                    LastFault = account.LastFault;
                    return;
                }
            }
        }
        finally
        {
            _book.Reset();
        }
    }

    public void Commit()
    {
        if (!_windowOpen || LastFault != ModelPortfolioFault.None)
            return;

        foreach (var instrument in _openAccounts.OrderBy(static instrument => instrument.Value))
        {
            var account = _accounts[instrument];
            account.Commit();
            if (account.LastFault != ModelPortfolioFault.None)
            {
                LastFault = account.LastFault;
                break;
            }
        }

        CloseWindow();
    }

    public void Rollback()
    {
        foreach (var instrument in _openAccounts)
            _accounts[instrument].Rollback();
        CloseWindow(clearFault: false);
        _book.Reset();
    }

    public void Complete()
    {
        if (_windowOpen)
            Rollback();

        LastFault = ModelPortfolioFault.None;
        foreach (var instrument in _instruments)
        {
            var account = _accounts[instrument];
            account.Complete();
            if (LastFault == ModelPortfolioFault.None && account.LastFault != ModelPortfolioFault.None)
                LastFault = account.LastFault;
        }
    }

    private void Begin(InstrumentId instrument, MarketReference reference)
    {
        if (_windowOpen)
        {
            LastFault = ModelPortfolioFault.InvalidCallbackState;
            Rollback();
            return;
        }
        if (!_accounts.TryGetValue(instrument, out var account))
        {
            LastFault = ModelPortfolioFault.InvalidConfiguration;
            return;
        }

        _book.Reset();
        _openAccounts.Clear();
        _currentInstrument = instrument;
        _latestReferences[instrument] = reference;
        Open(account, reference);
        LastFault = account.LastFault;
        _windowOpen = LastFault == ModelPortfolioFault.None;
        if (_windowOpen)
            _openAccounts.Add(instrument);
        else
            account.Rollback();
    }

    private bool TryOpenFromLatest(InstrumentId instrument, ModelPortfolioAccount account)
    {
        if (!_latestReferences.TryGetValue(instrument, out var reference))
            return false;

        Open(account, reference);
        if (account.LastFault != ModelPortfolioFault.None)
        {
            LastFault = account.LastFault;
            account.Rollback();
            return false;
        }

        _openAccounts.Add(instrument);
        return true;
    }

    private static void Open(ModelPortfolioAccount account, MarketReference reference)
    {
        if (reference.IsBar)
            account.BeginBar(reference.Last);
        else
            account.BeginTick(reference.Bid, reference.Ask, reference.Last);
    }

    private void CloseWindow(bool clearFault = false)
    {
        _windowOpen = false;
        _openAccounts.Clear();
        if (clearFault)
            LastFault = ModelPortfolioFault.None;
    }

    private readonly record struct MarketReference(bool IsBar, double Bid, double Ask, double Last);
}
