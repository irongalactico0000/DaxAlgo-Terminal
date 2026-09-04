# TradingTerminal.Accounts — public API surface (macOS/Avalonia)

Generated from source fingerprint `e91d50e75733`. Declaration lines only;
multi-line signatures show their first line. `[ObservableProperty]` generated properties are not listed.

## src/linux/Shell/TradingTerminal.Accounts/AccountGateCoordinator.cs
```cs
   23: public bool IsGranted => Failure == AccountGateAttemptFailure.None && Decision?.IsGranted == true;
   45: public Task<AccountGateAttempt> AcquireAccessAsync(CancellationToken ct = default) =>
   48: public Task<AccountGateAttempt> AcquireLocalDevelopmentAccessAsync(
  164: public async Task<AccountGateSignOutResult> SignOutAsync(CancellationToken ct = default)
  201: public static string ForDenial(EntitlementAccessDecision decision)
```

## src/linux/Shell/TradingTerminal.Accounts/AccountGateDiagnostics.cs
```cs
    5: public enum AccountGateDiagnosticCategory
   15: public readonly record struct AccountGateDiagnosticSignal(
   19: public interface IAccountGateDiagnostics
   21:     void Record(AccountGateDiagnosticSignal signal);
   26: public static TraceAccountGateDiagnostics Instance { get; } = new();
   28: public void Record(AccountGateDiagnosticSignal signal) =>
   37: public static void RecordSafely(
```

## src/linux/Shell/TradingTerminal.Accounts/AccountGateEditionProfile.cs
```cs
   11: public static AccountGateEditionProfile For(AppEdition edition) => edition switch
```

## src/linux/Shell/TradingTerminal.Accounts/AccountGateRunner.cs
```cs
    7: public static class AccountGateRunner
   11: public static AccountGateWindow CreateWindow(AppEdition requiredEdition) =>
   14: public static AccountGateWindow CreateWindow(
   32: public static bool ClearStoredAccount()
   38: public static AccountGateWindow CreateWindow(
```

## src/linux/Shell/TradingTerminal.Accounts/AccountGateServices.cs
```cs
   30: public static AccountGateServices Create(
  109: public static bool CanUseLocalAdapter(string environmentName, bool isDebugBuild) =>
  128: public DevelopmentAccountAuthenticationService(TimeProvider timeProvider)
  137: public DevelopmentAccountAuthenticationService(
  149: public Task<AccountSessionSnapshot?> GetCurrentSessionAsync(CancellationToken ct = default)
  173: public async Task<AccountSessionSnapshot> AuthenticateAsync(CancellationToken ct = default)
  194: public Task<AccountSessionSnapshot> AuthenticateLocallyAsync(
  208: public Task SignOutAsync(CancellationToken ct = default)
  220: public Task<SubscriptionEntitlement?> GetEntitlementAsync(
  238: public Task<AccountSessionSnapshot?> GetCurrentSessionAsync(CancellationToken ct = default)
  244: public Task<AccountSessionSnapshot> AuthenticateAsync(CancellationToken ct = default)
  250: public Task SignOutAsync(CancellationToken ct = default)
  259: public Task<SubscriptionEntitlement?> GetEntitlementAsync(
```

## src/linux/Shell/TradingTerminal.Accounts/AccountGateViewModel.cs
```cs
   15: public AccountGateViewModel(
   53: public string PlanName { get; }
   55: public string PlanPrice { get; }
   57: public string PlanSummary { get; }
   59: public string EnvironmentNotice { get; }
   61: public bool HasEnvironmentNotice { get; }
   63: public bool HasLocalDeveloperAccess { get; }
   89: public event Action<bool>? Completed;
  211: public void Dispose()
```

## src/linux/Shell/TradingTerminal.Accounts/AccountGateWindow.axaml.cs
```cs
    6: public sealed partial class AccountGateWindow : Window
   10: public AccountGateWindow()
   24: public event Action<bool>? AccessCompleted;
```

## src/linux/Shell/TradingTerminal.Accounts/DevelopmentAccountSessionStore.cs
```cs
   37: public static DevelopmentAccountSessionStore CreateDefault()
   49: public AccountSessionSnapshot? Load()
   89: public bool Save(AccountSessionSnapshot session)
  139: public bool Clear()
  177: protected abstract byte[] GetKey();
  179: public byte[] Protect(byte[] plaintext)
  207: public byte[] Unprotect(byte[] ciphertext)
  249: public static EphemeralAccountSessionProtector Instance { get; } = new();
  251: protected override byte[] GetKey() => ProcessKey.ToArray();
  269: public static MacKeychainAccountSessionProtector Instance { get; } = new();
  271: protected override byte[] GetKey()
```

## src/linux/Shell/TradingTerminal.Accounts/GoogleOAuthClient.cs
```cs
   65: public async Task<GoogleIdentity> AuthenticateAsync(CancellationToken ct = default)
  450: public static SystemGoogleOAuthBrowser Instance { get; } = new();
  452: public void Open(Uri authorizationUri) =>
  462: public static LoopbackGoogleOAuthCallbackReceiverFactory Instance { get; } = new();
  464: public IGoogleOAuthCallbackReceiver Create() =>
  481: public LoopbackGoogleOAuthCallbackReceiver(int port)
  488: public Uri RedirectUri { get; }
  490: public async Task<GoogleOAuthCallback> WaitForCallbackAsync(CancellationToken ct)
  532: public ValueTask DisposeAsync()
```
