using TradingTerminal.Core.Backtest;
using TradingTerminal.Core.Domain;
using TradingTerminal.Core.Trading;

namespace TradingTerminal.Infrastructure.Backtest;

/// <summary>
/// Shared-cash, per-contract backtest accounting. Each contract owns its own FIFO lots and mark;
/// cash and fees belong to the run. This prevents a pair leg from closing or valuing another leg.
/// </summary>
internal sealed class TradeLedger
{
    private readonly double _defaultMultiplier;
    private readonly IFeeModel _feeModel;
    private readonly List<Trade> _trades = [];
    private readonly Dictionary<Contract, InstrumentState> _states = [];

    public TradeLedger(double multiplier, double startingCash, IFeeModel? feeModel = null)
    {
        if (!double.IsFinite(multiplier) || multiplier <= 0d)
            throw new ArgumentOutOfRangeException(nameof(multiplier));
        _defaultMultiplier = multiplier;
        _feeModel = feeModel ?? ZeroFeeModel.Instance;
        Cash = startingCash;
    }

    public double Cash { get; private set; }
    public long NetPosition => _states.Values.Sum(state => state.NetPosition);
    public double TotalFees { get; private set; }
    public IReadOnlyList<Trade> Trades => _trades;
    public IReadOnlyDictionary<string, long> Positions => _states
        .GroupBy(pair => pair.Key.Symbol, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.Sum(pair => pair.Value.NetPosition), StringComparer.Ordinal);

    public void OnFill(
        Contract contract,
        DateTime utc,
        OrderSide side,
        long qty,
        double price,
        LiquidityFlag liquidity = LiquidityFlag.Taker,
        double? contractMultiplier = null)
    {
        ArgumentNullException.ThrowIfNull(contract);
        var multiplier = contractMultiplier ?? _defaultMultiplier;
        if (!double.IsFinite(multiplier) || multiplier <= 0d)
            throw new ArgumentOutOfRangeException(nameof(contractMultiplier));

        if (!_states.TryGetValue(contract, out var state))
        {
            state = new InstrumentState(multiplier);
            _states.Add(contract, state);
        }
        else if (state.Multiplier != multiplier)
        {
            throw new InvalidOperationException($"Contract multiplier changed during replay for {contract.Symbol}.");
        }

        var signed = side == OrderSide.Buy ? qty : -qty;
        Cash -= signed * price * multiplier;

        var fee = _feeModel.Fee(side, qty, price, liquidity);
        Cash -= fee;
        TotalFees += fee;

        var remaining = qty;
        while (remaining > 0 && state.OpenLots.Count > 0 &&
               Math.Sign(state.OpenLots.Peek().SignedQty) != Math.Sign(signed))
        {
            var head = state.OpenLots.Peek();
            var closeable = Math.Min(remaining, Math.Abs(head.SignedQty));
            var headSide = head.SignedQty > 0 ? OrderSide.Buy : OrderSide.Sell;
            var grossPoints = headSide == OrderSide.Buy
                ? (price - head.Price) * closeable
                : (head.Price - price) * closeable;

            _trades.Add(new Trade(
                head.OpenedUtc,
                utc,
                headSide,
                closeable,
                head.Price,
                price,
                grossPoints,
                contract.Symbol,
                multiplier));

            remaining -= closeable;
            if (Math.Abs(head.SignedQty) == closeable)
            {
                state.OpenLots.Dequeue();
            }
            else
            {
                state.OpenLots.Dequeue();
                state.OpenLots.Enqueue(head with
                {
                    SignedQty = head.SignedQty - (Math.Sign(head.SignedQty) * closeable),
                });
            }
        }

        if (remaining > 0)
        {
            var lotSign = signed > 0 ? +1 : -1;
            state.OpenLots.Enqueue(new Lot(utc, price, lotSign * remaining));
        }

        state.NetPosition += signed;
    }

    public double Equity(IReadOnlyDictionary<Contract, double> marks)
    {
        var equity = Cash;
        foreach (var (contract, state) in _states)
        {
            if (state.NetPosition == 0) continue;
            if (!marks.TryGetValue(contract, out var mid) || !double.IsFinite(mid) || mid <= 0d)
                throw new InvalidOperationException($"No valid final mark is available for open {contract.Symbol} exposure.");
            equity += state.NetPosition * mid * state.Multiplier;
        }
        return equity;
    }

    public double Equity(double mid)
    {
        if (_states.Count > 1)
            throw new InvalidOperationException("A single mark cannot value a multi-contract ledger.");
        if (_states.Count == 0) return Cash;
        return Equity(new Dictionary<Contract, double> { [_states.Keys.Single()] = mid });
    }

    private sealed class InstrumentState(double multiplier)
    {
        public double Multiplier { get; } = multiplier;
        public long NetPosition { get; set; }
        public Queue<Lot> OpenLots { get; } = new();
    }

    private readonly record struct Lot(DateTime OpenedUtc, double Price, long SignedQty);
}
