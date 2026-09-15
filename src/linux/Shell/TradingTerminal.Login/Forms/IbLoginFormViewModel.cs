using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradingTerminal.App.Login;
using TradingTerminal.Core.Brokers;
using TradingTerminal.Core.Configuration;

namespace TradingTerminal.App.Login.Forms;

// Note: NOT partial / no [ObservableProperty]. WPF's MarkupCompilePass1 runs in a temporary
// _wpftmp.csproj that doesn't always cooperate with source generators on partial classes used
// in <DataTemplate DataType="{x:Type ...}">. Using manual SetProperty avoids the issue.
public sealed class IbLoginFormViewModel : BrokerLoginFormBase
{
    public const string GatewayDownloadUrl =
        "https://www.interactivebrokers.com/en/trading/ibgateway-latest.php?p=stable";

    private readonly InteractiveBrokersOptions _options;
    private readonly CredentialStore _credentialStore;
    private readonly ServiceDependencyViewModel _prerequisite;

    public IbLoginFormViewModel(
        IOptions<InteractiveBrokersOptions> options,
        CredentialStore credentialStore,
        IBrokerSelector selector,
        ILogger<IbLoginFormViewModel> logger)
        : base(selector, logger)
    {
        _options = options.Value;
        _credentialStore = credentialStore;

        // The desktop app is IB's transport, so its status belongs on this form rather than in the
        // shell's shared services panel. Probes whatever Host/Port this form will actually dial.
        _prerequisite = new ServiceDependencyViewModel(
            name: "TWS / IB Gateway",
            purpose: "IB Gateway (or TWS) is a small IBKR desktop app that must stay open. DaxAlgo connects to it locally — not to the IB website.",
            requirement: "Required",
            howTo: "Install Gateway → Paper login → enable API socket clients + trust 127.0.0.1. " +
                   "Use a port preset on this form (TWS Paper 7497 / Gateway Paper 4002). " +
                   "With 2FA, finish the prompt inside Gateway/TWS first — the API has no separate 2FA step.",
            startCommand: null,
            probe: ct => ServiceDependencyViewModel.TcpOpenAsync(Host, new[] { Port }, ct));

        AccountTypes = new[] { "Paper", "Live" };
        MarketDataTypes = new[]
        {
            new MarketDataTypeOption(1, "Live (requires subscription)"),
            new MarketDataTypeOption(3, "Delayed (free, ~15 min lag)"),
            new MarketDataTypeOption(4, "Delayed-Frozen (free, last known)"),
        };
        _selectedMarketDataType = MarketDataTypes[0];

        ApplyPortPresetCommand = new RelayCommand<string?>(ApplyPortPreset);
        OpenGatewayDownloadCommand = new RelayCommand(OpenGatewayDownload);
        RecheckGatewayCommand = new AsyncRelayCommand(RecheckGatewayAsync);
    }

    public override BrokerKind Broker => BrokerKind.InteractiveBrokers;
    public override string DisplayName => "Interactive Brokers";

    /// <summary>TWS / IB Gateway must be up — surfaced inside this row (see the base class).</summary>
    public override ServiceDependencyViewModel? Prerequisite => _prerequisite;

    public IReadOnlyList<string> AccountTypes { get; }
    public IReadOnlyList<MarketDataTypeOption> MarketDataTypes { get; }

    public IRelayCommand ApplyPortPresetCommand { get; }
    public IRelayCommand OpenGatewayDownloadCommand { get; }
    public IAsyncRelayCommand RecheckGatewayCommand { get; }

    private string _portPresetHint =
        "Default is TWS Paper (7497). If you installed IB Gateway instead of TWS, tap Gateway Paper (4002).";
    public string PortPresetHint
    {
        get => _portPresetHint;
        private set => SetProperty(ref _portPresetHint, value);
    }

    private string _username = string.Empty;
    public string Username { get => _username; set => SetProperty(ref _username, value); }

    private string _password = string.Empty;
    public string Password { get => _password; set => SetProperty(ref _password, value); }

    private string _host = "127.0.0.1";
    public string Host { get => _host; set { if (SetProperty(ref _host, value)) RaiseCanSubmit(); } }

    private int _port = 7497;
    public int Port
    {
        get => _port;
        set
        {
            if (SetProperty(ref _port, value))
            {
                RaiseCanSubmit();
                RefreshPortPresetHint();
            }
        }
    }

    private int _clientId = 1;
    public int ClientId { get => _clientId; set => SetProperty(ref _clientId, value); }

    private string _accountType = "Paper";
    public string AccountType { get => _accountType; set => SetProperty(ref _accountType, value); }

    private MarketDataTypeOption? _selectedMarketDataType;
    public MarketDataTypeOption? SelectedMarketDataType
    {
        get => _selectedMarketDataType;
        set => SetProperty(ref _selectedMarketDataType, value);
    }

    private bool _rememberPassword;
    public bool RememberPassword { get => _rememberPassword; set => SetProperty(ref _rememberPassword, value); }

    public override bool CanSubmit => !string.IsNullOrWhiteSpace(Host) && Port > 0;

    private void RaiseCanSubmit()
    {
        OnPropertyChanged(nameof(CanSubmit));
        ConnectCommand.NotifyCanExecuteChanged();
    }

    private void ApplyPortPreset(string? preset)
    {
        switch (preset)
        {
            case "tws-paper":
                Port = 7497;
                AccountType = "Paper";
                break;
            case "gateway-paper":
                Port = 4002;
                AccountType = "Paper";
                break;
            case "tws-live":
                Port = 7496;
                AccountType = "Live";
                break;
            case "gateway-live":
                Port = 4001;
                AccountType = "Live";
                break;
            default:
                return;
        }

        RefreshPortPresetHint();
        _ = RecheckGatewayAsync();
    }

    private void RefreshPortPresetHint()
    {
        PortPresetHint = Port switch
        {
            7497 => "Using TWS Paper (7497). Leave Gateway closed if you are on TWS.",
            4002 => "Using IB Gateway Paper (4002). Gateway must be open and signed into Paper.",
            7496 => "Using TWS Live (7496). LIVE still needs the console LIVE gate + exact account id.",
            4001 => "Using IB Gateway Live (4001). LIVE still needs the console LIVE gate + exact account id.",
            _ => $"Custom port {Port}. Match Account Paper/Live to the session you opened in Gateway/TWS.",
        };
    }

    private void OpenGatewayDownload()
    {
        try
        {
            Process.Start(new ProcessStartInfo(GatewayDownloadUrl) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to open IB Gateway download URL");
            PortPresetHint =
                $"Could not open the browser. Download Gateway manually: {GatewayDownloadUrl}";
        }
    }

    private Task RecheckGatewayAsync() =>
        Prerequisite?.CheckAsync() ?? Task.CompletedTask;

    public override void ApplyToOptions()
    {
        _options.Host = Host;
        _options.Port = Port;
        _options.ClientId = ClientId;
        _options.AccountType = AccountType;
        _options.MarketDataType = SelectedMarketDataType?.Value ?? 1;
    }

    public override string GetSessionAccountLabel() => AccountType;

    public override string GetTimeoutErrorMessage() =>
        $"Connection timed out after 15s. Verify TWS / IB Gateway is running on {Host}:{Port} and that API access is enabled. " +
        "Use a port preset on this form if you are unsure (TWS Paper 7497 / Gateway Paper 4002). " +
        "If you have 2FA enabled, complete the 2FA prompt in Gateway/TWS before connecting here.";

    public override string GetFailureMessage() =>
        "IB reported a connection failure. Common causes: Gateway/TWS not running, API socket clients not enabled, " +
        "wrong port (tap a Paper preset), trusted IPs missing 127.0.0.1, " +
        "or client id already in use by another connection.";

    public override void Load()
    {
        var stored = _credentialStore.Load();
        Username = stored.Username ?? string.Empty;
        Host = stored.Host;
        Port = stored.Port;
        ClientId = stored.ClientId;
        AccountType = stored.AccountType;
        var storedType = stored.MarketDataType is 1 or 3 or 4 ? stored.MarketDataType : 1;
        SelectedMarketDataType = MarketDataTypes.First(o => o.Value == storedType);
        RememberPassword = stored.RememberPassword;
        Password = stored.Password ?? string.Empty;
        RefreshPortPresetHint();
    }

    public override void Save()
    {
        var stored = _credentialStore.Load();
        stored.SelectedBroker = BrokerKind.InteractiveBrokers;
        stored.Username = string.IsNullOrWhiteSpace(Username) ? null : Username.Trim();
        stored.Host = Host;
        stored.Port = Port;
        stored.ClientId = ClientId;
        stored.AccountType = AccountType;
        stored.MarketDataType = SelectedMarketDataType?.Value ?? 1;
        stored.RememberPassword = RememberPassword;
        stored.Password = (RememberPassword && !string.IsNullOrEmpty(Password)) ? Password : null;
        _credentialStore.Save(stored);
    }
}

public sealed record MarketDataTypeOption(int Value, string DisplayName);
