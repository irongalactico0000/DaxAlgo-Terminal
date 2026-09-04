using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace TradingTerminal.Core.Execution;

public static class ExecutionIpcProtocol
{
    public const int Version1 = 1;
    public const int SecretSize = 32;
    public const int NonceSize = 32;
    public const int ProofSize = 32;
}

public interface IExecutionNonceSource
{
    byte[] CreateNonce(int length);
}

public sealed class CryptographicExecutionNonceSource : IExecutionNonceSource
{
    public static CryptographicExecutionNonceSource Instance { get; } = new();
    private CryptographicExecutionNonceSource() { }
    public byte[] CreateNonce(int length) => length > 0
        ? RandomNumberGenerator.GetBytes(length)
        : throw new ArgumentOutOfRangeException(nameof(length));
}

public enum ExecutionHandshakeFailure : byte
{
    None = 0,
    ProtocolVersionMismatch = 1,
    AuthenticationFailed = 2,
    InvalidHandshake = 3,
    TransportFailed = 4,
}

public readonly record struct ExecutionHandshakeResult(
    ExecutionHandshakeFailure Failure,
    int NegotiatedVersion,
    string? Reason)
{
    public bool IsAuthenticated => Failure == ExecutionHandshakeFailure.None;
}

public sealed record ExecutionClientHello(int ProtocolVersion, byte[] ClientNonce);
public sealed record ExecutionServerChallenge(
    int ServerProtocolVersion,
    bool VersionAccepted,
    byte[] ServerNonce,
    byte[] ServerProof,
    string? FailureReason);
public sealed record ExecutionClientProof(byte[] Proof);
public sealed record ExecutionHandshakeCompletion(bool Accepted, int ProtocolVersion, string? FailureReason);

/// <summary>
/// Mutual nonce/HMAC authentication. Direction-specific, version-bound transcripts prevent proof
/// reflection and protocol substitution. Secret material is cloned and zeroed on disposal.
/// </summary>
public sealed class ExecutionIpcAuthenticator : IDisposable
{
    private static readonly byte[] ServerDomain =
        Encoding.UTF8.GetBytes("DaxAlgo.Execution.IPC.Handshake.v1/server-proof");
    private static readonly byte[] ClientDomain =
        Encoding.UTF8.GetBytes("DaxAlgo.Execution.IPC.Handshake.v1/client-proof");

    private readonly byte[] _secret;
    private readonly IExecutionNonceSource _nonceSource;
    private readonly int _version;
    private bool _disposed;

    public ExecutionIpcAuthenticator(
        byte[] secret,
        IExecutionNonceSource? nonceSource = null,
        int protocolVersion = ExecutionIpcProtocol.Version1)
    {
        ArgumentNullException.ThrowIfNull(secret);
        if (secret.Length != ExecutionIpcProtocol.SecretSize)
            throw new ArgumentException("The execution service secret must contain exactly 32 bytes.", nameof(secret));
        if (protocolVersion <= 0) throw new ArgumentOutOfRangeException(nameof(protocolVersion));
        _secret = (byte[])secret.Clone();
        _nonceSource = nonceSource ?? CryptographicExecutionNonceSource.Instance;
        _version = protocolVersion;
    }

    public async ValueTask<ExecutionHandshakeResult> AuthenticateServerAsync(
        IExecutionFrameTransport transport,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(transport);
        try
        {
            var hello = await transport.ReadAsync<ExecutionClientHello>(cancellationToken).ConfigureAwait(false);
            if (!Nonce(hello.ClientNonce) || hello.ProtocolVersion <= 0)
                return Failed(ExecutionHandshakeFailure.InvalidHandshake, "The client hello was invalid.");
            var serverNonce = NewNonce();
            var accepted = hello.ProtocolVersion == _version;
            var mismatch = accepted ? null :
                $"Protocol version mismatch. Client={hello.ProtocolVersion}; service={_version}.";
            var serverProof = Proof(ServerDomain, hello.ProtocolVersion, _version, accepted, hello.ClientNonce, serverNonce);
            await transport.WriteAsync(
                new ExecutionServerChallenge(_version, accepted, serverNonce, serverProof, mismatch),
                cancellationToken).ConfigureAwait(false);

            ExecutionClientProof client;
            try { client = await transport.ReadAsync<ExecutionClientProof>(cancellationToken).ConfigureAwait(false); }
            catch (EndOfStreamException)
            {
                return Failed(ExecutionHandshakeFailure.AuthenticationFailed, "The client proof was absent.");
            }
            var expected = Proof(ClientDomain, hello.ProtocolVersion, _version, accepted, hello.ClientNonce, serverNonce);
            var valid = ValidProof(client.Proof) && CryptographicOperations.FixedTimeEquals(client.Proof, expected);
            CryptographicOperations.ZeroMemory(expected);
            if (!valid)
            {
                await TryCompletion(transport, false, "Authentication failed.", cancellationToken).ConfigureAwait(false);
                return Failed(ExecutionHandshakeFailure.AuthenticationFailed, "The client proof was invalid.");
            }
            if (!accepted)
            {
                await TryCompletion(transport, false, mismatch, cancellationToken).ConfigureAwait(false);
                return Failed(ExecutionHandshakeFailure.ProtocolVersionMismatch, mismatch!);
            }
            await transport.WriteAsync(new ExecutionHandshakeCompletion(true, _version, null), cancellationToken)
                .ConfigureAwait(false);
            return new ExecutionHandshakeResult(ExecutionHandshakeFailure.None, _version, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (ProtocolFailure(exception))
        {
            return Failed(ExecutionHandshakeFailure.TransportFailed, "The server handshake transport failed.");
        }
    }

    public async ValueTask<ExecutionHandshakeResult> AuthenticateClientAsync(
        IExecutionFrameTransport transport,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(transport);
        try
        {
            var clientNonce = NewNonce();
            await transport.WriteAsync(new ExecutionClientHello(_version, clientNonce), cancellationToken)
                .ConfigureAwait(false);
            var challenge = await transport.ReadAsync<ExecutionServerChallenge>(cancellationToken).ConfigureAwait(false);
            if (!Nonce(challenge.ServerNonce) || !ValidProof(challenge.ServerProof) ||
                challenge.ServerProtocolVersion <= 0 ||
                challenge.VersionAccepted != (_version == challenge.ServerProtocolVersion))
                return Failed(ExecutionHandshakeFailure.InvalidHandshake, "The service challenge was invalid.");
            var expected = Proof(
                ServerDomain, _version, challenge.ServerProtocolVersion, challenge.VersionAccepted,
                clientNonce, challenge.ServerNonce);
            var valid = CryptographicOperations.FixedTimeEquals(challenge.ServerProof, expected);
            CryptographicOperations.ZeroMemory(expected);
            if (!valid)
                return Failed(ExecutionHandshakeFailure.AuthenticationFailed, "The service proof was invalid.");
            var clientProof = Proof(
                ClientDomain, _version, challenge.ServerProtocolVersion, challenge.VersionAccepted,
                clientNonce, challenge.ServerNonce);
            await transport.WriteAsync(new ExecutionClientProof(clientProof), cancellationToken).ConfigureAwait(false);
            CryptographicOperations.ZeroMemory(clientProof);
            var completion = await transport.ReadAsync<ExecutionHandshakeCompletion>(cancellationToken).ConfigureAwait(false);
            if (completion.ProtocolVersion != challenge.ServerProtocolVersion)
                return Failed(ExecutionHandshakeFailure.InvalidHandshake, "The completion changed protocol version.");
            if (!challenge.VersionAccepted)
                return completion.Accepted
                    ? Failed(ExecutionHandshakeFailure.InvalidHandshake, "A mismatched protocol was accepted.")
                    : Failed(ExecutionHandshakeFailure.ProtocolVersionMismatch,
                        challenge.FailureReason ?? completion.FailureReason ?? "Protocol version mismatch.");
            return completion.Accepted
                ? new ExecutionHandshakeResult(ExecutionHandshakeFailure.None, challenge.ServerProtocolVersion, null)
                : Failed(ExecutionHandshakeFailure.AuthenticationFailed, "The service rejected authentication.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (ProtocolFailure(exception))
        {
            return Failed(ExecutionHandshakeFailure.TransportFailed, "The client handshake transport failed.");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CryptographicOperations.ZeroMemory(_secret);
    }

    private byte[] NewNonce()
    {
        var value = _nonceSource.CreateNonce(ExecutionIpcProtocol.NonceSize);
        return Nonce(value) ? value : throw new CryptographicException("The nonce source returned an invalid nonce.");
    }

    private byte[] Proof(byte[] domain, int clientVersion, int serverVersion, bool accepted, byte[] clientNonce, byte[] serverNonce)
    {
        var transcript = new byte[sizeof(int) + domain.Length + sizeof(int) * 2 + 1 + clientNonce.Length + serverNonce.Length];
        var offset = 0;
        BinaryPrimitives.WriteInt32BigEndian(transcript.AsSpan(offset, sizeof(int)), domain.Length);
        offset += sizeof(int);
        domain.CopyTo(transcript, offset); offset += domain.Length;
        BinaryPrimitives.WriteInt32BigEndian(transcript.AsSpan(offset, sizeof(int)), clientVersion); offset += sizeof(int);
        BinaryPrimitives.WriteInt32BigEndian(transcript.AsSpan(offset, sizeof(int)), serverVersion); offset += sizeof(int);
        transcript[offset++] = accepted ? (byte)1 : (byte)0;
        clientNonce.CopyTo(transcript, offset); offset += clientNonce.Length;
        serverNonce.CopyTo(transcript, offset);
        try { return HMACSHA256.HashData(_secret, transcript); }
        finally { CryptographicOperations.ZeroMemory(transcript); }
    }

    private async ValueTask TryCompletion(
        IExecutionFrameTransport transport, bool accepted, string? reason, CancellationToken cancellationToken)
    {
        try
        {
            await transport.WriteAsync(new ExecutionHandshakeCompletion(accepted, _version, reason), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (ProtocolFailure(exception)) { }
    }

    private static bool Nonce(byte[]? value) => value is { Length: ExecutionIpcProtocol.NonceSize };
    private static bool ValidProof(byte[]? value) => value is { Length: ExecutionIpcProtocol.ProofSize };
    private static bool ProtocolFailure(Exception value) =>
        value is IOException or InvalidDataException or CryptographicException;
    private static ExecutionHandshakeResult Failed(ExecutionHandshakeFailure failure, string reason) =>
        new(failure, 0, reason);
}
