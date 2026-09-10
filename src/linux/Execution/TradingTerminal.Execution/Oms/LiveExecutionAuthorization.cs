
namespace TradingTerminal.Execution.Oms;

/// <summary>The execution environment selected for one broker connection.</summary>
public enum ExecutionMode : byte
{
    /// <summary>Routes only to a broker's simulation, demo, testnet, or paper endpoint.</summary>
    Paper = 0,

    /// <summary>Routes to a real-money broker endpoint after explicit authorization.</summary>
    Live = 1,
}

/// <summary>
/// Persisted evidence that one identity deliberately acknowledged live execution for one exact
/// mode-neutral broker/account binding.
/// </summary>
public sealed record LiveExecutionConfirmation(
    string BrokerId,
    string AccountId,
    string Acknowledgement,
    DateTime ConfirmedAtUtc,
    string ConfirmedBy)
{
    public const string RequiredAcknowledgement = "LIVE";
    public const int MaximumBrokerIdLength = 64;
    public const int MaximumAccountIdLength = 128;
    public const int MaximumConfirmingIdentityLength = 256;

    /// <summary>Gets whether every field is exact, bounded, and suitable for live authorization.</summary>
    public bool IsValid =>
        IsBounded(BrokerId, MaximumBrokerIdLength) &&
        IsBounded(AccountId, MaximumAccountIdLength) &&
        string.Equals(Acknowledgement, RequiredAcknowledgement, StringComparison.Ordinal) &&
        ConfirmedAtUtc != default &&
        ConfirmedAtUtc.Kind == DateTimeKind.Utc &&
        IsBounded(ConfirmedBy, MaximumConfirmingIdentityLength);

    /// <summary>Gets whether this confirmation authorizes the exact broker/account binding.</summary>
    public bool Matches(string brokerId, string accountId) =>
        IsValid &&
        string.Equals(BrokerId, brokerId, StringComparison.Ordinal) &&
        string.Equals(AccountId, accountId, StringComparison.Ordinal);

    internal static bool IsLookupValid(string brokerId, string accountId) =>
        IsBounded(brokerId, MaximumBrokerIdLength) &&
        IsBounded(accountId, MaximumAccountIdLength);

    private static bool IsBounded(string? value, int maximumLength) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.Length <= maximumLength &&
        string.Equals(value, value.Trim(), StringComparison.Ordinal);
}

/// <summary>Reads and persists explicit live-execution confirmations.</summary>
public interface ILiveExecutionConfirmationStore
{
    /// <summary>Reads the confirmation for one exact mode-neutral broker/account binding.</summary>
    LiveExecutionConfirmation? Read(string brokerId, string accountId);

    /// <summary>Persists one validated confirmation, replacing only the same broker/account binding.</summary>
    void Save(LiveExecutionConfirmation confirmation);

    /// <summary>Revokes the confirmation for one exact broker/account binding.</summary>
    bool Remove(string brokerId, string accountId);
}

/// <summary>Bounded deterministic confirmation store for tests and explicitly ephemeral hosts.</summary>
public sealed class InMemoryLiveExecutionConfirmationStore : ILiveExecutionConfirmationStore
{
    public const int MaximumConfirmations = 64;

    private readonly object _gate = new();
    private readonly Dictionary<(string BrokerId, string AccountId), LiveExecutionConfirmation> _confirmations = [];

    /// <inheritdoc />
    public LiveExecutionConfirmation? Read(string brokerId, string accountId)
    {
        ValidateLookup(brokerId, accountId);
        lock (_gate)
            return _confirmations.GetValueOrDefault((brokerId, accountId));
    }

    /// <inheritdoc />
    public void Save(LiveExecutionConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        if (!confirmation.IsValid)
            throw new ArgumentException("The live-execution confirmation is invalid.", nameof(confirmation));

        lock (_gate)
        {
            var key = (confirmation.BrokerId, confirmation.AccountId);
            if (!_confirmations.ContainsKey(key) && _confirmations.Count >= MaximumConfirmations)
                throw new InvalidOperationException($"No more than {MaximumConfirmations} live confirmations may be retained.");
            _confirmations[key] = confirmation;
        }
    }

    /// <inheritdoc />
    public bool Remove(string brokerId, string accountId)
    {
        ValidateLookup(brokerId, accountId);
        lock (_gate)
            return _confirmations.Remove((brokerId, accountId));
    }

    internal static void ValidateLookup(string brokerId, string accountId)
    {
        if (!LiveExecutionConfirmation.IsLookupValid(brokerId, accountId))
            throw new ArgumentException("The live-confirmation broker/account lookup is invalid.");
    }
}

/// <summary>Shared fail-closed validation used before any live endpoint token is created.</summary>
internal static class LiveExecutionAuthorizationGate
{
    internal static LiveExecutionConfirmation Require(
        bool allowLiveExecution,
        bool hasRequiredCredentials,
        string brokerId,
        string accountId,
        ILiveExecutionConfirmationStore? confirmationStore)
    {
        if (!allowLiveExecution)
            throw new InvalidOperationException("Live execution is disabled because AllowLiveExecution defaults to false.");
        if (!hasRequiredCredentials)
            throw new InvalidOperationException("Live execution requires real, non-paper credentials before an endpoint can be created.");
        if (!LiveExecutionConfirmation.IsLookupValid(brokerId, accountId))
            throw new InvalidOperationException("Live execution requires one exact bounded broker/account binding.");
        if (confirmationStore is null)
            throw new InvalidOperationException("Live execution requires a persisted typed confirmation store.");

        var confirmation = confirmationStore.Read(brokerId, accountId);
        if (confirmation is null || !confirmation.Matches(brokerId, accountId))
        {
            throw new InvalidOperationException(
                $"Live execution for broker '{brokerId}' account '{accountId}' requires a persisted exact LIVE confirmation.");
        }
        return confirmation;
    }
}
