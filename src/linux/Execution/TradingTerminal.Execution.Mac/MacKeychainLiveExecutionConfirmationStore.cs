using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TradingTerminal.Execution.Oms;

namespace TradingTerminal.Execution.Mac;

/// <summary>
/// Bounded current-user macOS Keychain store for explicit live confirmations. This is the macOS
/// counterpart of the Windows DPAPI store: the same bounded JSON document with the same validation,
/// held as a generic password in the login Keychain instead of a DPAPI-protected file. Corrupt,
/// oversized, or inaccessible state fails closed and is never silently replaced.
/// </summary>
public sealed class MacKeychainLiveExecutionConfirmationStore : ILiveExecutionConfirmationStore
{
    private const int DocumentVersion = 1;
    private const int MaximumPlaintextBytes = 128 * 1024;
    private const int Success = 0;
    private const int DuplicateItem = -25299;
    private const int ItemNotFound = -25300;

    private static readonly byte[] DefaultServiceName =
        Encoding.UTF8.GetBytes("com.daxalgo.terminal.execution.live-confirmations");
    private static readonly byte[] DefaultAccountName = Encoding.UTF8.GetBytes("live-confirmations-v1");
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly object _gate = new();
    private readonly byte[] _service;
    private readonly byte[] _account;

    /// <summary>Creates a store bound to the dedicated current-user Keychain item.</summary>
    public MacKeychainLiveExecutionConfirmationStore()
        : this(DefaultServiceName, DefaultAccountName)
    {
    }

    /// <summary>
    /// Creates a store bound to an injected Keychain account name, primarily for deterministic test
    /// isolation against the same service.
    /// </summary>
    public MacKeychainLiveExecutionConfirmationStore(string accountName)
        : this(DefaultServiceName, EncodeAccount(accountName))
    {
    }

    private MacKeychainLiveExecutionConfirmationStore(byte[] service, byte[] account)
    {
        _service = service;
        _account = account;
    }

    /// <summary>
    /// Gets the per-user execution state directory. Confirmations themselves live in the Keychain;
    /// this is the sibling location for the ledger and other non-secret execution state.
    /// </summary>
    public static string ExecutionSupportDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Library",
        "Application Support",
        "DaxAlgoTerminal",
        "Execution");

    /// <summary>Resolves one bounded file name under <see cref="ExecutionSupportDirectory"/>.</summary>
    public static string ExecutionSupportPath(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (fileName.Contains(Path.DirectorySeparatorChar) ||
            fileName.Contains(Path.AltDirectorySeparatorChar) ||
            !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal))
        {
            throw new ArgumentException("The execution support file name must be one bounded file name.", nameof(fileName));
        }
        return Path.Combine(ExecutionSupportDirectory, fileName);
    }

    /// <inheritdoc />
    public LiveExecutionConfirmation? Read(string brokerId, string accountId)
    {
        InMemoryLiveExecutionConfirmationStore.ValidateLookup(brokerId, accountId);
        lock (_gate)
            return ReadAll().SingleOrDefault(item => item.Matches(brokerId, accountId));
    }

    /// <inheritdoc />
    public void Save(LiveExecutionConfirmation confirmation)
    {
        ArgumentNullException.ThrowIfNull(confirmation);
        if (!confirmation.IsValid)
            throw new ArgumentException("The live-execution confirmation is invalid.", nameof(confirmation));

        lock (_gate)
        {
            var confirmations = ReadAll().ToList();
            var existing = confirmations.FindIndex(item =>
                string.Equals(item.BrokerId, confirmation.BrokerId, StringComparison.Ordinal) &&
                string.Equals(item.AccountId, confirmation.AccountId, StringComparison.Ordinal));
            if (existing >= 0)
            {
                confirmations[existing] = confirmation;
            }
            else
            {
                if (confirmations.Count >= InMemoryLiveExecutionConfirmationStore.MaximumConfirmations)
                {
                    throw new InvalidOperationException(
                        $"No more than {InMemoryLiveExecutionConfirmationStore.MaximumConfirmations} live confirmations may be retained.");
                }
                confirmations.Add(confirmation);
            }

            WriteAll(confirmations);
        }
    }

    /// <inheritdoc />
    public bool Remove(string brokerId, string accountId)
    {
        InMemoryLiveExecutionConfirmationStore.ValidateLookup(brokerId, accountId);
        lock (_gate)
        {
            var confirmations = ReadAll().ToList();
            var removed = confirmations.RemoveAll(item =>
                string.Equals(item.BrokerId, brokerId, StringComparison.Ordinal) &&
                string.Equals(item.AccountId, accountId, StringComparison.Ordinal)) != 0;
            if (removed)
                WriteAll(confirmations);
            return removed;
        }
    }

    private IReadOnlyList<LiveExecutionConfirmation> ReadAll()
    {
        var plaintext = Find();
        if (plaintext is null)
            return Array.Empty<LiveExecutionConfirmation>();

        try
        {
            if (plaintext.Length <= 0 || plaintext.Length > MaximumPlaintextBytes)
                throw new InvalidDataException("The Keychain live-confirmation document is empty or oversized.");

            ConfirmationDocument document;
            try
            {
                document = JsonSerializer.Deserialize<ConfirmationDocument>(plaintext, JsonOptions) ??
                    throw new InvalidDataException("The live-confirmation document is empty.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The live-confirmation document is malformed.", exception);
            }

            if (document.Version != DocumentVersion ||
                document.Confirmations is null ||
                document.Confirmations.Count > InMemoryLiveExecutionConfirmationStore.MaximumConfirmations ||
                document.Confirmations.Any(item => item is null || !item.IsValid))
            {
                throw new InvalidDataException("The live-confirmation document contains invalid or unsupported state.");
            }

            var duplicates = document.Confirmations
                .GroupBy(item => (item.BrokerId, item.AccountId))
                .Any(group => group.Count() != 1);
            if (duplicates)
                throw new InvalidDataException("The live-confirmation document contains duplicate broker/account bindings.");

            return Array.AsReadOnly(document.Confirmations.ToArray());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private void WriteAll(IReadOnlyList<LiveExecutionConfirmation> confirmations)
    {
        var document = new ConfirmationDocument(DocumentVersion, confirmations.ToArray());
        var plaintext = JsonSerializer.SerializeToUtf8Bytes(document, JsonOptions);
        if (plaintext.Length <= 0 || plaintext.Length > MaximumPlaintextBytes)
            throw new InvalidOperationException("The bounded live-confirmation document cannot be persisted safely.");

        try
        {
            RequireMacOS();
            var status = SecKeychainAddGenericPassword(
                IntPtr.Zero,
                (uint)_service.Length,
                _service,
                (uint)_account.Length,
                _account,
                (uint)plaintext.Length,
                plaintext,
                out var item);
            if (item != IntPtr.Zero) CFRelease(item);
            if (status == Success)
                return;
            if (status != DuplicateItem)
                throw Failure(status);

            Replace(plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private void Replace(byte[] plaintext)
    {
        var status = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)_service.Length,
            _service,
            (uint)_account.Length,
            _account,
            out _,
            out var data,
            out var item);
        try
        {
            if (status != Success || item == IntPtr.Zero)
                throw Failure(status);

            var modify = SecKeychainItemModifyAttributesAndData(
                item,
                IntPtr.Zero,
                (uint)plaintext.Length,
                plaintext);
            if (modify != Success)
                throw Failure(modify);
        }
        finally
        {
            if (data != IntPtr.Zero) _ = SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != IntPtr.Zero) CFRelease(item);
        }
    }

    private byte[]? Find()
    {
        RequireMacOS();
        var status = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)_service.Length,
            _service,
            (uint)_account.Length,
            _account,
            out var length,
            out var data,
            out var item);
        try
        {
            if (status == ItemNotFound) return null;
            if (status != Success) throw Failure(status);
            if (length <= 0 || length > MaximumPlaintextBytes || data == IntPtr.Zero)
                throw new InvalidDataException("The Keychain live-confirmation document is empty or oversized.");
            var plaintext = new byte[length];
            Marshal.Copy(data, plaintext, 0, checked((int)length));
            return plaintext;
        }
        finally
        {
            if (data != IntPtr.Zero) _ = SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != IntPtr.Zero) CFRelease(item);
        }
    }

    private static void RequireMacOS()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("Live-execution confirmations require the macOS Keychain.");
    }

    private static byte[] EncodeAccount(string accountName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName);
        if (accountName.Length > 128 || !string.Equals(accountName, accountName.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("The Keychain account name must be bounded and trimmed.", nameof(accountName));
        return Encoding.UTF8.GetBytes(accountName);
    }

    private static CryptographicException Failure(int status) =>
        new($"macOS Keychain could not access the live-execution confirmations (status {status}).");

    private sealed record ConfirmationDocument(
        int Version,
        IReadOnlyList<LiveExecutionConfirmation> Confirmations);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainFindGenericPassword(
        IntPtr keychainOrArray,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        out uint passwordLength,
        out IntPtr passwordData,
        out IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainAddGenericPassword(
        IntPtr keychain,
        uint serviceNameLength,
        byte[] serviceName,
        uint accountNameLength,
        byte[] accountName,
        uint passwordLength,
        byte[] passwordData,
        out IntPtr itemRef);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemModifyAttributesAndData(
        IntPtr itemRef,
        IntPtr attrList,
        uint length,
        byte[] data);

    [DllImport("/System/Library/Frameworks/Security.framework/Security")]
    private static extern int SecKeychainItemFreeContent(IntPtr attributeList, IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);
}
