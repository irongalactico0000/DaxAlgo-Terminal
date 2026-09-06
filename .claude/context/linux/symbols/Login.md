# TradingTerminal.Login — public API surface (macOS/Avalonia)

Generated from source fingerprint `1ddf0170457d`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Shell/TradingTerminal.Login/AiKeyStore.cs
```cs
   16: public sealed class AiKeyStore : IAiKeyStore
   27: public AiKeyStore(ILogger<AiKeyStore> logger)
   34: public IReadOnlyCollection<string> ConfiguredProviders
   39: public bool HasKey(string providerId)
   46: public string? Get(string providerId)
   52: public void Set(string providerId, string apiKey)
   65: public void Remove(string providerId)
```

## src/linux/Shell/TradingTerminal.Login/BrokerLoginFormBase.cs
```cs
   23: public abstract class BrokerLoginFormBase : ViewModelBase, IBrokerLoginForm, IDisposable
   27: protected readonly IBrokerSelector Selector;
   28: protected readonly ILogger Logger;
   31: protected BrokerLoginFormBase(IBrokerSelector selector, ILogger logger)
   40: public abstract BrokerKind Broker { get; }
   41: public abstract string DisplayName { get; }
   42: public abstract bool CanSubmit { get; }
   43: public abstract void ApplyToOptions();
   44: public abstract string GetSessionAccountLabel();
   45: public abstract string GetTimeoutErrorMessage();
   46: public abstract string GetFailureMessage();
   47: public abstract void Load();
   48: public abstract void Save();
   51: public ConnectionState CurrentState
   67: public bool IsConnected => CurrentState == ConnectionState.Connected;
   68: public bool IsDisconnected => CurrentState is ConnectionState.Disconnected or ConnectionState.Failed;
   71: public bool IsConnecting
   85: public string? ErrorMessage
   94: public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
   96: public string StatusText => CurrentState switch
  112: public string Badge => Tile.Badge;
  114: public string BadgeColor => Tile.BadgeColor;
  116: public string BadgeForeground => Tile.BadgeForeground;
  118: public string Subtitle => Tile.Subtitle;
  120: public LoginCategory Category => Tile.Category;
  123: public string CategoryName => Category switch
  131: public int CategoryOrder => (int)Category;
  134: public bool IsKeyless => Category == LoginCategory.Keyless;
  138: public bool IsExpanded
  158: public virtual ServiceDependencyViewModel? Prerequisite => null;
  161: public bool HasPrerequisite => Prerequisite is not null;
  184: public IAsyncRelayCommand ConnectCommand { get; }
  185: public IAsyncRelayCommand DisconnectCommand { get; }
  189: public void Initialize()
  276: public void Dispose()
  286: public enum LoginCategory
```

## src/linux/Shell/TradingTerminal.Login/BrokerLoginFormFactory.cs
```cs
   10: public sealed class BrokerLoginFormFactory : IBrokerLoginFormFactory
   14: public BrokerLoginFormFactory(IServiceProvider services, IBrokerSelector selector)
   28: public IReadOnlyList<IBrokerLoginForm> All { get; }
   30: public IBrokerLoginForm Get(BrokerKind kind)
```

## src/linux/Shell/TradingTerminal.Login/CredentialStore.cs
```cs
    7: public sealed class CredentialStore
   22: public CredentialStore(ILogger<CredentialStore> logger) => _logger = logger;
   24: public StoredCredentials Load()
   40: public void Save(StoredCredentials credentials)
   54: public void Clear()
```

## src/linux/Shell/TradingTerminal.Login/CredentialStoreAiKeyResolver.cs
```cs
   11: public sealed class CredentialStoreAiKeyResolver(AiKeyStore store) : IAiKeyResolver
   15: public string? Resolve(string providerId)
```

## src/linux/Shell/TradingTerminal.Login/Forms/AlpacaLoginForm.axaml.cs
```cs
    5: public partial class AlpacaLoginForm : UserControl
    7: public AlpacaLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/AlpacaLoginFormViewModel.cs
```cs
    9: public sealed class AlpacaLoginFormViewModel : BrokerLoginFormBase
   14: public AlpacaLoginFormViewModel(
   25: public override BrokerKind Broker => BrokerKind.Alpaca;
   26: public override string DisplayName => "Alpaca";
   29: public string Username { get => _username; set => SetProperty(ref _username, value); }
   32: public string ApiKey
   39: public string ApiSecret
   46: public bool IsLive { get => _isLive; set => SetProperty(ref _isLive, value); }
   49: public IReadOnlyList<string> StockDataFeeds { get; } = new[] { "iex", "sip" };
   50: public string StockDataFeed { get => _stockDataFeed; set => SetProperty(ref _stockDataFeed, value); }
   52: public override bool CanSubmit =>
   62: public override void ApplyToOptions()
   70: public override string GetSessionAccountLabel() => IsLive ? "Alpaca · Live" : "Alpaca · Paper";
   72: public override string GetTimeoutErrorMessage() =>
   76: public override string GetFailureMessage() =>
   80: public override void Load()
   90: public override void Save()
```

## src/linux/Shell/TradingTerminal.Login/Forms/BinanceLoginForm.axaml.cs
```cs
    5: public partial class BinanceLoginForm : UserControl
    7: public BinanceLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/BinanceLoginFormViewModel.cs
```cs
   13: public sealed class BinanceLoginFormViewModel : BrokerLoginFormBase
   15: public BinanceLoginFormViewModel(
   22: public override BrokerKind Broker => BrokerKind.Binance;
   23: public override string DisplayName => "Binance (no login)";
   26: public override bool CanSubmit => true;
   28: public override void ApplyToOptions() { /* nothing to apply — endpoints come from config defaults */ }
   30: public override string GetSessionAccountLabel() => "Binance · Public data";
   32: public override string GetTimeoutErrorMessage() =>
   36: public override string GetFailureMessage() =>
   41: public override void Load() { /* no persisted credentials */ }
   43: public override void Save() { /* nothing to persist */ }
```

## src/linux/Shell/TradingTerminal.Login/Forms/BybitLoginForm.axaml.cs
```cs
    5: public partial class BybitLoginForm : UserControl
    7: public BybitLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/BybitLoginFormViewModel.cs
```cs
    8: public sealed class BybitLoginFormViewModel : BrokerLoginFormBase
   10: public BybitLoginFormViewModel(IBrokerSelector selector, ILogger<BybitLoginFormViewModel> logger)
   13: public override BrokerKind Broker => BrokerKind.Bybit;
   14: public override string DisplayName => "Bybit (no login)";
   15: public override bool CanSubmit => true;
   16: public override void ApplyToOptions() { }
   17: public override string GetSessionAccountLabel() => "Bybit · Public data";
   18: public override string GetTimeoutErrorMessage() =>
   20: public override string GetFailureMessage() =>
   22: public override void Load() { }
   23: public override void Save() { }
```

## src/linux/Shell/TradingTerminal.Login/Forms/CTraderLoginForm.axaml.cs
```cs
    5: public partial class CTraderLoginForm : UserControl
    7: public CTraderLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/CTraderLoginFormViewModel.cs
```cs
   12: public sealed class CTraderLoginFormViewModel : BrokerLoginFormBase
   18: public CTraderLoginFormViewModel(
   33: public override BrokerKind Broker => BrokerKind.CTrader;
   34: public override string DisplayName => "cTrader";
   37: public string Username { get => _username; set => SetProperty(ref _username, value); }
   40: public string ClientId
   47: public string ClientSecret
   54: public string AccessToken
   61: public long AccountId
   68: public bool IsLive { get => _isLive; set => SetProperty(ref _isLive, value); }
   75: public bool IsDiscovering
   88: public string? DiscoveryMessage
   97: public ObservableCollection<CTraderDiscoveredAccount> DiscoveredAccounts { get; } = new();
  102: public bool HasDiscoveredAccounts => DiscoveredAccounts.Count > 0;
  105: public CTraderDiscoveredAccount? SelectedDiscoveredAccount
  118: public IAsyncRelayCommand DiscoverAccountsCommand { get; }
  120: public override bool CanSubmit =>
  202: public override void ApplyToOptions()
  213: public override string GetSessionAccountLabel() => $"cTrader #{AccountId}";
  215: public override string GetTimeoutErrorMessage() =>
  219: public override string GetFailureMessage() =>
  223: public override void Load()
  234: public override void Save()
```

## src/linux/Shell/TradingTerminal.Login/Forms/CoinbaseLoginForm.axaml.cs
```cs
    5: public partial class CoinbaseLoginForm : UserControl
    7: public CoinbaseLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/CoinbaseLoginFormViewModel.cs
```cs
    8: public sealed class CoinbaseLoginFormViewModel : BrokerLoginFormBase
   10: public CoinbaseLoginFormViewModel(IBrokerSelector selector, ILogger<CoinbaseLoginFormViewModel> logger)
   13: public override BrokerKind Broker => BrokerKind.Coinbase;
   14: public override string DisplayName => "Coinbase (no login)";
   15: public override bool CanSubmit => true;
   16: public override void ApplyToOptions() { }
   17: public override string GetSessionAccountLabel() => "Coinbase · Public data";
   18: public override string GetTimeoutErrorMessage() =>
   20: public override string GetFailureMessage() =>
   22: public override void Load() { }
   23: public override void Save() { }
```

## src/linux/Shell/TradingTerminal.Login/Forms/IbLoginForm.axaml.cs
```cs
    5: public partial class IbLoginForm : UserControl
    7: public IbLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/IbLoginFormViewModel.cs
```cs
   12: public sealed class IbLoginFormViewModel : BrokerLoginFormBase
   18: public IbLoginFormViewModel(
   50: public override BrokerKind Broker => BrokerKind.InteractiveBrokers;
   51: public override string DisplayName => "Interactive Brokers";
   54: public override ServiceDependencyViewModel? Prerequisite => _prerequisite;
   56: public IReadOnlyList<string> AccountTypes { get; }
   57: public IReadOnlyList<MarketDataTypeOption> MarketDataTypes { get; }
   60: public string Username { get => _username; set => SetProperty(ref _username, value); }
   63: public string Password { get => _password; set => SetProperty(ref _password, value); }
   66: public string Host { get => _host; set { if (SetProperty(ref _host, value)) RaiseCanSubmit(); } }
   69: public int Port { get => _port; set { if (SetProperty(ref _port, value)) RaiseCanSubmit(); } }
   72: public int ClientId { get => _clientId; set => SetProperty(ref _clientId, value); }
   75: public string AccountType { get => _accountType; set => SetProperty(ref _accountType, value); }
   78: public MarketDataTypeOption? SelectedMarketDataType
   85: public bool RememberPassword { get => _rememberPassword; set => SetProperty(ref _rememberPassword, value); }
   87: public override bool CanSubmit => !string.IsNullOrWhiteSpace(Host) && Port > 0;
   95: public override void ApplyToOptions()
  104: public override string GetSessionAccountLabel() => AccountType;
  106: public override string GetTimeoutErrorMessage() =>
  110: public override string GetFailureMessage() =>
  115: public override void Load()
  129: public override void Save()
  145: public sealed record MarketDataTypeOption(int Value, string DisplayName);
```

## src/linux/Shell/TradingTerminal.Login/Forms/IronBeamLoginForm.axaml.cs
```cs
    5: public partial class IronBeamLoginForm : UserControl
    7: public IronBeamLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/IronBeamLoginFormViewModel.cs
```cs
    9: public sealed class IronBeamLoginFormViewModel : BrokerLoginFormBase
   14: public IronBeamLoginFormViewModel(
   25: public override BrokerKind Broker => BrokerKind.IronBeam;
   26: public override string DisplayName => "Ironbeam";
   29: public string Username
   36: public string ApiKey
   43: public bool IsLive { get => _isLive; set => SetProperty(ref _isLive, value); }
   45: public override bool CanSubmit =>
   55: public override void ApplyToOptions()
   62: public override string GetSessionAccountLabel() =>
   65: public override string GetTimeoutErrorMessage() =>
   69: public override string GetFailureMessage() =>
   73: public override void Load()
   84: public override void Save()
```

## src/linux/Shell/TradingTerminal.Login/Forms/KrakenLoginForm.axaml.cs
```cs
    5: public partial class KrakenLoginForm : UserControl
    7: public KrakenLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/KrakenLoginFormViewModel.cs
```cs
    8: public sealed class KrakenLoginFormViewModel : BrokerLoginFormBase
   10: public KrakenLoginFormViewModel(IBrokerSelector selector, ILogger<KrakenLoginFormViewModel> logger)
   13: public override BrokerKind Broker => BrokerKind.Kraken;
   14: public override string DisplayName => "Kraken (no login)";
   15: public override bool CanSubmit => true;
   16: public override void ApplyToOptions() { }
   17: public override string GetSessionAccountLabel() => "Kraken · Public data";
   18: public override string GetTimeoutErrorMessage() =>
   20: public override string GetFailureMessage() =>
   22: public override void Load() { }
   23: public override void Save() { }
```

## src/linux/Shell/TradingTerminal.Login/Forms/LondonStrategicEdgeLoginForm.axaml.cs
```cs
    5: public partial class LondonStrategicEdgeLoginForm : UserControl
    7: public LondonStrategicEdgeLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/LondonStrategicEdgeLoginFormViewModel.cs
```cs
    9: public sealed class LondonStrategicEdgeLoginFormViewModel : BrokerLoginFormBase
   14: public LondonStrategicEdgeLoginFormViewModel(
   25: public override BrokerKind Broker => BrokerKind.LondonStrategicEdge;
   26: public override string DisplayName => "London Strategic Edge";
   29: public string ApiKey
   42: public override bool CanSubmit => !string.IsNullOrWhiteSpace(ApiKey);
   44: public override void ApplyToOptions()
   49: public override string GetSessionAccountLabel() => "London Strategic Edge · Free data";
   51: public override string GetTimeoutErrorMessage() =>
   55: public override string GetFailureMessage() =>
   59: public override void Load()
   67: public override void Save()
```

## src/linux/Shell/TradingTerminal.Login/Forms/NinjaLoginForm.axaml.cs
```cs
    5: public partial class NinjaLoginForm : UserControl
    7: public NinjaLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/NinjaLoginFormViewModel.cs
```cs
    9: public sealed class NinjaLoginFormViewModel : BrokerLoginFormBase
   15: public NinjaLoginFormViewModel(
   38: public override BrokerKind Broker => BrokerKind.NinjaTrader;
   39: public override string DisplayName => "NinjaTrader";
   42: public override ServiceDependencyViewModel? Prerequisite => _prerequisite;
   45: public string Username { get => _username; set => SetProperty(ref _username, value); }
   48: public string AccountName
   55: public string DllPath { get => _dllPath; set => SetProperty(ref _dllPath, value); }
   58: public string FuturesContractMonth { get => _futuresContractMonth; set => SetProperty(ref _futuresContractMonth, value); }
   60: public override bool CanSubmit => !string.IsNullOrWhiteSpace(AccountName);
   62: public override void ApplyToOptions()
   69: public override string GetSessionAccountLabel() => AccountName;
   71: public override string GetTimeoutErrorMessage() =>
   74: public override string GetFailureMessage() =>
   78: public override void Load()
   87: public override void Save()
```

## src/linux/Shell/TradingTerminal.Login/Forms/OkxLoginForm.axaml.cs
```cs
    5: public partial class OkxLoginForm : UserControl
    7: public OkxLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/OkxLoginFormViewModel.cs
```cs
    8: public sealed class OkxLoginFormViewModel : BrokerLoginFormBase
   10: public OkxLoginFormViewModel(IBrokerSelector selector, ILogger<OkxLoginFormViewModel> logger)
   13: public override BrokerKind Broker => BrokerKind.Okx;
   14: public override string DisplayName => "OKX (no login)";
   15: public override bool CanSubmit => true;
   16: public override void ApplyToOptions() { }
   17: public override string GetSessionAccountLabel() => "OKX · Public data";
   18: public override string GetTimeoutErrorMessage() =>
   20: public override string GetFailureMessage() =>
   22: public override void Load() { }
   23: public override void Save() { }
```

## src/linux/Shell/TradingTerminal.Login/Forms/UpstoxLoginForm.axaml.cs
```cs
    5: public partial class UpstoxLoginForm : UserControl
    7: public UpstoxLoginForm() => InitializeComponent();
```

## src/linux/Shell/TradingTerminal.Login/Forms/UpstoxLoginFormViewModel.cs
```cs
   25: public sealed class UpstoxLoginFormViewModel : BrokerLoginFormBase
   31: public UpstoxLoginFormViewModel(
   47: public override BrokerKind Broker => BrokerKind.Upstox;
   48: public override string DisplayName => "Upstox";
   51: public string ApiKey
   58: public string ApiSecret
   65: public string RedirectUri
   72: public string AuthCode
   79: public string AccessToken
   87: public string? AuthMessage
   94: public bool IsExchanging
  100: public IRelayCommand AuthorizeCommand { get; }
  101: public IAsyncRelayCommand ExchangeCodeCommand { get; }
  103: public override bool CanSubmit => !string.IsNullOrWhiteSpace(AccessToken);
  165: public override void ApplyToOptions()
  173: public override string GetSessionAccountLabel() => "Upstox";
  175: public override string GetTimeoutErrorMessage() =>
  178: public override string GetFailureMessage() =>
  181: public override void Load()
  190: public override void Save()
```

## src/linux/Shell/TradingTerminal.Login/LoginClipboard.cs
```cs
    7: public interface ILoginClipboard
    9:     Task SetTextAsync(string text);
   14: public async Task SetTextAsync(string text)
```

## src/linux/Shell/TradingTerminal.Login/LoginServiceCollectionExtensions.cs
```cs
   23: public static class LoginServiceCollectionExtensions
   27: public static IServiceCollection AddLogin(this IServiceCollection services)
   60: public static IServiceCollection AddCredentialedLoginForms(this IServiceCollection services)
```

## src/linux/Shell/TradingTerminal.Login/LoginViewModel.cs
```cs
   30: public sealed partial class LoginViewModel : ViewModelBase, IDisposable
   47: public LoginViewModel(
  104: public IReadOnlyList<IBrokerLoginForm> AvailableForms { get; }
  108: public ObservableCollection<BrokerLoginFormBase> FormsView { get; }
  215: public bool CanLaunch => ConnectedCount > 0;
  266: public event EventHandler<bool>? LoginCompleted;
  420: public ObservableCollection<ServiceDependencyViewModel> Services { get; } = new();
  493: public void Dispose()
```

## src/linux/Shell/TradingTerminal.Login/LoginWindow.axaml.cs
```cs
   13: public partial class LoginWindow : Window
   17: public LoginWindow()
   51: public sealed class ServiceStateBrushConverter : IValueConverter
   58: public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
   66: public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
   71: public sealed class BrokerLogoConverter : IValueConverter
   92: public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
  109: public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
```

## src/linux/Shell/TradingTerminal.Login/PlatformSecretStore.cs
```cs
   32: public static string? Protect(string purpose, string? value, string? existingReference = null)
   50: public static string? Unprotect(string? reference)
   78: public static void Delete(string? reference)
```

## src/linux/Shell/TradingTerminal.Login/ServiceDependencyViewModel.cs
```cs
   12: public enum ServiceState
   38: public sealed partial class ServiceDependencyViewModel : ObservableObject
   40: public const string QuestDbDockerRunCommand =
   59: public ServiceDependencyViewModel(
   80: public string Name { get; }
   81: public string Purpose { get; }
   82: public string Requirement { get; }
   83: public string HowTo { get; }
   84: public string? StartCommand { get; }
   86: public bool HasStartCommand => !string.IsNullOrWhiteSpace(StartCommand);
   87: public bool CanProbe => _probe is not null;
   89: public string StartActionLabel { get; }
   90: public bool HasStartAction => _startAction is not null;
   93: public async Task RunStartAsync(CancellationToken ct = default)
  113: public async Task CheckAsync(CancellationToken ct = default)
  135: public static async Task<bool> HttpOkAsync(string url, CancellationToken ct)
  150: public static async Task<bool> TcpOpenAsync(string host, int[] ports, CancellationToken ct)
  186: public static Task<bool> ProcessRunningAsync(string processName, CancellationToken ct) => Task.Run(() =>
  201: public static Task<bool> DockerRunningAsync(CancellationToken ct) => Task.Run(() =>
  230: public static async Task<bool> QuestDbRunningAsync(CancellationToken ct)
  239: public static async Task StartQuestDbAsync(CancellationToken ct)
```

## src/linux/Shell/TradingTerminal.Login/StoredCredentials.cs
```cs
   10: public sealed class StoredCredentials
   13: public BrokerKind SelectedBroker { get; set; } = BrokerKind.InteractiveBrokers;
   17: public bool AutoConnect { get; set; }
   19: public string? Username { get; set; }
   20: public string Host { get; set; } = "127.0.0.1";
   21: public int Port { get; set; } = 7497;
   22: public int ClientId { get; set; } = 1;
   23: public string AccountType { get; set; } = "Paper";
   24: public int MarketDataType { get; set; } = 1;
   25: public bool RememberPassword { get; set; }
   28: public string NinjaAccountName { get; set; } = "Sim101";
   29: public string NinjaDllPath { get; set; } = string.Empty;
   30: public string NinjaFuturesContractMonth { get; set; } = string.Empty;
   33: public string CTraderClientId { get; set; } = string.Empty;
   34: public long CTraderAccountId { get; set; }
   35: public bool CTraderIsLive { get; set; }
   38: public string? CTraderClientSecretEncryptedBase64 { get; set; }
   40: public string? CTraderAccessTokenEncryptedBase64 { get; set; }
   43: public string? CTraderClientSecret
   51: public string? CTraderAccessToken
   59: public string? IronBeamUsername { get; set; }
   60: public bool IronBeamIsLive { get; set; }
   63: public string? IronBeamApiKeyEncryptedBase64 { get; set; }
   66: public string? IronBeamApiKey
   76: public string? LondonStrategicEdgeApiKeyEncryptedBase64 { get; set; }
   79: public string? LondonStrategicEdgeApiKey
   87: public string AlpacaApiKey { get; set; } = string.Empty;
   88: public bool AlpacaIsLive { get; set; }
   89: public string AlpacaStockDataFeed { get; set; } = "iex";
   92: public string? AlpacaApiSecretEncryptedBase64 { get; set; }
   95: public string? AlpacaApiSecret
  103: public string UpstoxApiKey { get; set; } = string.Empty;
  104: public string UpstoxRedirectUri { get; set; } = string.Empty;
  107: public string? UpstoxApiSecretEncryptedBase64 { get; set; }
  110: public string? UpstoxAccessTokenEncryptedBase64 { get; set; }
  113: public string? UpstoxApiSecret
  121: public string? UpstoxAccessToken
  129: public string? PasswordEncryptedBase64 { get; set; }
  132: public string? Password
```
