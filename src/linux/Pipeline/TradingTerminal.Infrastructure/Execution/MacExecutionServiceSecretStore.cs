using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using TradingTerminal.Core.Execution;

namespace TradingTerminal.Infrastructure.Execution;

public interface IExecutionServiceSecretStore
{
    byte[] LoadOrCreate();
}

/// <summary>
/// Stores the local execution-service authentication secret directly in the current user's macOS
/// login Keychain. Corrupt or inaccessible state fails closed and is never silently replaced.
/// </summary>
public sealed class MacExecutionServiceSecretStore : IExecutionServiceSecretStore
{
    private const int Success = 0;
    private const int DuplicateItem = -25299;
    private const int ItemNotFound = -25300;
    private static readonly byte[] Service = Encoding.UTF8.GetBytes("com.daxalgo.terminal.execution");
    private static readonly byte[] Account = Encoding.UTF8.GetBytes("paper-service-secret-v1");
    private readonly object _gate = new();

    public byte[] LoadOrCreate()
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("The execution service secret requires macOS Keychain.");
        lock (_gate)
        {
            var existing = Find();
            if (existing is not null) return existing;
            var secret = RandomNumberGenerator.GetBytes(ExecutionIpcProtocol.SecretSize);
            try
            {
                var status = SecKeychainAddGenericPassword(
                    IntPtr.Zero,
                    (uint)Service.Length,
                    Service,
                    (uint)Account.Length,
                    Account,
                    (uint)secret.Length,
                    secret,
                    out var item);
                if (item != IntPtr.Zero) CFRelease(item);
                if (status == Success) return (byte[])secret.Clone();
                if (status == DuplicateItem)
                    return Find() ?? throw Failure(status);
                throw Failure(status);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(secret);
            }
        }
    }

    private static byte[]? Find()
    {
        var status = SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)Service.Length,
            Service,
            (uint)Account.Length,
            Account,
            out var length,
            out var data,
            out var item);
        try
        {
            if (status == ItemNotFound) return null;
            if (status != Success) throw Failure(status);
            if (length != ExecutionIpcProtocol.SecretSize || data == IntPtr.Zero)
                throw new InvalidDataException("The Keychain execution-service secret has an invalid length.");
            var secret = new byte[length];
            Marshal.Copy(data, secret, 0, checked((int)length));
            return secret;
        }
        finally
        {
            if (data != IntPtr.Zero) _ = SecKeychainItemFreeContent(IntPtr.Zero, data);
            if (item != IntPtr.Zero) CFRelease(item);
        }
    }

    private static CryptographicException Failure(int status) =>
        new($"macOS Keychain could not access the execution-service secret (status {status}).");

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
    private static extern int SecKeychainItemFreeContent(IntPtr attributeList, IntPtr data);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(IntPtr cf);
}
