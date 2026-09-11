using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TradingTerminal.Charts;

/// <summary>
/// Option 1 research shell (instrument → range → draft → lock → historical) with Option 2 click-to-place.
/// Does not place broker orders.
/// </summary>
public sealed partial class ChartsViewModel
{
    [ObservableProperty] private string _historyFromText = string.Empty;
    [ObservableProperty] private string _historyToText = string.Empty;
    [ObservableProperty] private bool _hasExplicitHistoryRange;
    [ObservableProperty] private bool _draftSentToBuilder;
    [ObservableProperty] private bool _historicalBacktestRoomOpened;
    [ObservableProperty] private ChartInteractionMode _draftPlacementMode = ChartInteractionMode.Pan;

    private DateTimeOffset? _explicitHistoryFromUtc;
    private DateTimeOffset? _explicitHistoryToUtc;

    public bool ShellStep1Done => SelectedInstrument is not null && SelectedTimeframe is not null;
    /// <summary>True only after Load history applied an explicit From–To window (not TF lookback alone).</summary>
    public bool ShellStep2Done => HasExplicitHistoryRange;
    public bool ShellStep3Done => CanSendStrategyDraft;
    public bool ShellStep4Done => DraftSentToBuilder;
    /// <summary>True only after Charts opened Validate/Studio for a runnable locked hash — not on “compile first” assist.</summary>
    public bool ShellStep5Done => HistoricalBacktestRoomOpened;
    public string ResearchShellProgressText =>
        $"① Instrument {(ShellStep1Done ? "✓" : "·")}  " +
        $"② Range {(ShellStep2Done ? "✓" : "·")}  " +
        $"③ Draft {(ShellStep3Done ? "✓" : "·")}  " +
        $"④ Sent {(ShellStep4Done ? "✓" : "·")}  " +
        $"⑤ Historical BT {(ShellStep5Done ? "✓" : "·")}";

    public bool IsPlaceStopMode => DraftPlacementMode == ChartInteractionMode.PlaceStop;
    public bool IsPlaceTargetMode => DraftPlacementMode == ChartInteractionMode.PlaceTarget;
    public bool CanLoadExplicitHistory =>
        SelectedInstrument is not null &&
        SelectedTimeframe is not null &&
        TryParseHistoryBound(HistoryFromText, out _) &&
        TryParseHistoryBound(HistoryToText, out _);

    public event EventHandler? HistoricalBacktestRequested;
    public event EventHandler? StrategyDraftLockRequested;

    public double? DraftStopPrice =>
        TryParseDraftPrice(DraftStopPriceText, out var price) ? (double)price : null;

    public double? DraftTargetPrice =>
        TryParseDraftPrice(DraftTargetPriceText, out var price) ? (double)price : null;

    /// <summary>Last loaded close for draft preview / click-price smoke (not a live tick).</summary>
    public decimal? LastLoadedClosePrice =>
        _lastBars.Count > 0 && double.IsFinite(_lastBars[^1].Close) && _lastBars[^1].Close > 0
            ? (decimal)_lastBars[^1].Close
            : null;

    [RelayCommand(CanExecute = nameof(CanLoadExplicitHistoryAction))]
    private void LoadExplicitHistory()
    {
        if (!TryParseHistoryBound(HistoryFromText, out var from) ||
            !TryParseHistoryBound(HistoryToText, out var to) ||
            from >= to)
        {
            Status = "History From must be earlier than To (UTC dates, e.g. 2026-01-02).";
            return;
        }

        _explicitHistoryFromUtc = from;
        _explicitHistoryToUtc = to;
        HasExplicitHistoryRange = true;
        DraftSentToBuilder = false;
        Status = $"Loading explicit history {from:u} → {to:u}…";
        NotifyResearchShellStateChanged();
        QueueReload();
    }

    private bool CanLoadExplicitHistoryAction() => CanLoadExplicitHistory;

    [RelayCommand]
    private void ArmPlaceStop()
    {
        CancelResearchSelection();
        DraftPlacementMode = ChartInteractionMode.PlaceStop;
        Status = "Click the price pane to place STOP. Does not send orders.";
        NotifyResearchShellStateChanged();
    }

    [RelayCommand]
    private void ArmPlaceTarget()
    {
        CancelResearchSelection();
        DraftPlacementMode = ChartInteractionMode.PlaceTarget;
        Status = "Click the price pane to place TARGET. Does not send orders.";
        NotifyResearchShellStateChanged();
    }

    [RelayCommand]
    private void ArmPanMode()
    {
        DraftPlacementMode = ChartInteractionMode.Pan;
        Status = "Chart pan restored.";
        NotifyResearchShellStateChanged();
    }

    public void ApplyClickedDraftPrice(decimal price)
    {
        if (price <= 0m)
            return;

        var text = price.ToString("0.########", CultureInfo.InvariantCulture);
        if (DraftPlacementMode == ChartInteractionMode.PlaceStop)
        {
            DraftStopPriceText = text;
            Status = $"STOP set to {text}. Send draft when target is ready.";
        }
        else if (DraftPlacementMode == ChartInteractionMode.PlaceTarget)
        {
            DraftTargetPriceText = text;
            Status = $"TARGET set to {text}. Send draft when stop is ready.";
        }

        DraftPlacementMode = ChartInteractionMode.Pan;
        NotifyResearchShellStateChanged();
    }

    [RelayCommand]
    private void RequestStrategyDraftLock()
    {
        if (!DraftSentToBuilder)
        {
            Status = "Send draft to Builder before lock. Lock needs an active TradeIR hash in Builder.";
            return;
        }

        StrategyDraftLockRequested?.Invoke(this, EventArgs.Empty);
        Status = "Lock requested — Builder will bind the draft to the active TradeIR hash when available.";
    }

    [RelayCommand]
    private void RequestHistoricalBacktest()
    {
        HistoricalBacktestRequested?.Invoke(this, EventArgs.Empty);
        if (!HistoricalBacktestRoomOpened)
            Status = "Historical BT requested — opening Validate/Studio when a locked TradeIR hash is ready.";
    }

    /// <summary>Called by shell when Validate/Studio actually opened on a runnable locked hash.</summary>
    public void MarkHistoricalBacktestRoomOpened(string? status = null)
    {
        HistoricalBacktestRoomOpened = true;
        Status = string.IsNullOrWhiteSpace(status)
            ? "Historical validation opened from Charts research shell."
            : status;
        NotifyResearchShellStateChanged();
    }

    /// <summary>Compile-first / locked-hash missing — do not checkmark step ⑤.</summary>
    public void MarkHistoricalBacktestBlocked(string message)
    {
        HistoricalBacktestRoomOpened = false;
        Status = string.IsNullOrWhiteSpace(message)
            ? "Compile and register a TradeIR hash before Historical BT."
            : message;
        NotifyResearchShellStateChanged();
    }

    partial void OnHistoryFromTextChanged(string value)
    {
        LoadExplicitHistoryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanLoadExplicitHistory));
    }

    partial void OnHistoryToTextChanged(string value)
    {
        LoadExplicitHistoryCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanLoadExplicitHistory));
    }

    partial void OnDraftPlacementModeChanged(ChartInteractionMode value) => NotifyResearchShellStateChanged();

    partial void OnHasExplicitHistoryRangeChanged(bool value) => NotifyResearchShellStateChanged();

    partial void OnDraftSentToBuilderChanged(bool value) => NotifyResearchShellStateChanged();

    partial void OnHistoricalBacktestRoomOpenedChanged(bool value) => NotifyResearchShellStateChanged();

    private void NotifyResearchShellStateChanged()
    {
        OnPropertyChanged(nameof(ShellStep1Done));
        OnPropertyChanged(nameof(ShellStep2Done));
        OnPropertyChanged(nameof(ShellStep3Done));
        OnPropertyChanged(nameof(ShellStep4Done));
        OnPropertyChanged(nameof(ShellStep5Done));
        OnPropertyChanged(nameof(ResearchShellProgressText));
        OnPropertyChanged(nameof(IsPlaceStopMode));
        OnPropertyChanged(nameof(IsPlaceTargetMode));
        OnPropertyChanged(nameof(DraftStopPrice));
        OnPropertyChanged(nameof(DraftTargetPrice));
        OnPropertyChanged(nameof(CanLoadExplicitHistory));
        LoadExplicitHistoryCommand.NotifyCanExecuteChanged();
    }

    private void ClearExplicitHistoryWindow()
    {
        _explicitHistoryFromUtc = null;
        _explicitHistoryToUtc = null;
        HasExplicitHistoryRange = false;
    }

    /// <summary>TF default lookback only. Explicit From/To uses repository range API.</summary>
    private static TimeSpan ResolveHistoryLookback(ChartTimeframe timeframe) => timeframe.Lookback;

    private static bool TryParseHistoryBound(string? text, out DateTimeOffset value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (DateTimeOffset.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out value))
            return true;

        if (DateTime.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var date))
        {
            value = new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc));
            return true;
        }

        return false;
    }
}
