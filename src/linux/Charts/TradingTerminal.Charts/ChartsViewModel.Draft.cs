using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Strategies.Generation;

namespace TradingTerminal.Charts;

/// <summary>
/// Parallel to research Capture: author stop/target price levels into <see cref="StrategyDraftV1"/>
/// and send them to Strategy Builder. Does not place orders and does not replace B/C/N samples.
/// </summary>
public sealed partial class ChartsViewModel
{
    [ObservableProperty] private string _draftStopPriceText = string.Empty;
    [ObservableProperty] private string _draftTargetPriceText = string.Empty;

    public bool CanSendStrategyDraft =>
        SelectedInstrument is not null &&
        SelectedTimeframe is not null &&
        TryParseDraftPrice(DraftStopPriceText, out _) &&
        TryParseDraftPrice(DraftTargetPriceText, out _);

    public string StrategyDraftSummary =>
        CanSendStrategyDraft
            ? $"Stop {DraftStopPriceText.Trim()} · Target {DraftTargetPriceText.Trim()} — ready to send (draft only, no orders)."
            : "Enter stop and target prices, then Send draft to Builder.";

    public event EventHandler<StrategyDraftRequestedEventArgs>? StrategyDraftRequested;

    [RelayCommand(CanExecute = nameof(CanSendStrategyDraftAction))]
    private void SendStrategyDraft()
    {
        if (SelectedInstrument is not { } instrument || SelectedTimeframe is not { } timeframe)
            return;
        if (!TryParseDraftPrice(DraftStopPriceText, out var stop) ||
            !TryParseDraftPrice(DraftTargetPriceText, out var target))
        {
            Status = "Stop and target must be finite decimal prices.";
            return;
        }

        BrokerKind broker;
        try { broker = ResolveBroker(instrument); }
        catch (InvalidOperationException exception)
        {
            Status = exception.Message;
            return;
        }

        var scope = new StrategyDraftScopeV1(
            _ingest.Resolve(instrument.Contract, broker),
            instrument.Contract.Symbol,
            timeframe.BarSize);
        var draft = StrategyDraftV1.Create(scope);
        draft = StrategyDraftGestureApplierV1.UpsertStop(draft, "chart-stop", stop);
        draft = StrategyDraftGestureApplierV1.UpsertTarget(draft, "chart-target", target);
        StrategyDraftValidatorV1.RequireStructurallyValid(draft);

        StrategyDraftRequested?.Invoke(this, new StrategyDraftRequestedEventArgs(draft));
        DraftSentToBuilder = true;
            Status =
                "Strategy draft (stop/target) sent to Strategy Builder. Next: Lock draft when TradeIR exists, then Historical BT. No orders placed.";
        NotifyResearchShellStateChanged();
    }

    private bool CanSendStrategyDraftAction() => CanSendStrategyDraft;

    [RelayCommand]
    private void ClearStrategyDraftLevels()
    {
        DraftStopPriceText = string.Empty;
        DraftTargetPriceText = string.Empty;
        DraftSentToBuilder = false;
        Status = "Chart strategy draft levels cleared.";
        NotifyResearchShellStateChanged();
    }

    partial void OnDraftStopPriceTextChanged(string value)
    {
        DraftSentToBuilder = false;
        NotifyStrategyDraftStateChanged();
        NotifyResearchShellStateChanged();
    }

    partial void OnDraftTargetPriceTextChanged(string value)
    {
        DraftSentToBuilder = false;
        NotifyStrategyDraftStateChanged();
        NotifyResearchShellStateChanged();
    }

    private void NotifyStrategyDraftStateChanged()
    {
        OnPropertyChanged(nameof(CanSendStrategyDraft));
        OnPropertyChanged(nameof(StrategyDraftSummary));
        OnPropertyChanged(nameof(DraftStopPrice));
        OnPropertyChanged(nameof(DraftTargetPrice));
        SendStrategyDraftCommand.NotifyCanExecuteChanged();
    }

    private static bool TryParseDraftPrice(string? text, out decimal price)
    {
        price = 0m;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        if (!decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out price) &&
            !decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out price))
            return false;
        return price > 0m;
    }
}
