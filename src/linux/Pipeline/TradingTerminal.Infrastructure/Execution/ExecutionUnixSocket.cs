using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using TradingTerminal.Core.Execution;

namespace TradingTerminal.Infrastructure.Execution;

/// <summary>Authenticated, same-user, Unix-domain-socket server over one Paper service engine.</summary>
public sealed class ExecutionUnixSocketServer : IAsyncDisposable
{
    private static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(5);
    private readonly ExecutionServiceEngine _engine;
    private readonly byte[] _secret;
    private readonly Socket _listener;
    private readonly string _socketPath;
    private readonly object _connectionsGate = new();
    private readonly HashSet<Task> _connections = [];
    private bool _disposed;

    public ExecutionUnixSocketServer(
        ExecutionServiceEngine engine,
        IExecutionServiceSecretStore secretStore,
        string socketPath)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        ArgumentNullException.ThrowIfNull(secretStore);
        _secret = secretStore.LoadOrCreate();
        if (_secret.Length != ExecutionIpcProtocol.SecretSize)
            throw new InvalidDataException("The execution-service secret has an invalid length.");
        _socketPath = PreparePath(socketPath);
        _listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        try
        {
            _listener.Bind(new UnixDomainSocketEndPoint(_socketPath));
            if (OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("Execution Unix sockets require a Unix host.");
            File.SetUnixFileMode(_socketPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            _listener.Listen(4);
        }
        catch
        {
            _listener.Dispose();
            CryptographicOperations.ZeroMemory(_secret);
            DeleteOwnedSocket();
            throw;
        }
    }

    public string SocketPath => _socketPath;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        while (!cancellationToken.IsCancellationRequested)
        {
            Socket accepted;
            try { accepted = await _listener.AcceptAsync(cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
            Track(ServeAsync(accepted, cancellationToken));
        }

        Task[] active;
        lock (_connectionsGate) active = _connections.ToArray();
        if (active.Length > 0) await Task.WhenAll(active).ConfigureAwait(false);
    }

    private async Task ServeAsync(Socket socket, CancellationToken cancellationToken)
    {
        using (socket)
        {
            try
            {
                VerifySameUser(socket);
                await using var stream = new NetworkStream(socket, ownsSocket: false);
                await using var transport = new StreamExecutionFrameTransport(stream, leaveOpen: true);
                using var authenticator = new ExecutionIpcAuthenticator(_secret);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                deadline.CancelAfter(HandshakeTimeout);
                var handshake = await authenticator.AuthenticateServerAsync(transport, deadline.Token)
                    .ConfigureAwait(false);
                if (!handshake.IsAuthenticated) return;

                while (!cancellationToken.IsCancellationRequested)
                {
                    ExecutionServiceRequest request;
                    try { request = await transport.ReadAsync<ExecutionServiceRequest>(cancellationToken).ConfigureAwait(false); }
                    catch (Exception exception) when (exception is EndOfStreamException or IOException or InvalidDataException)
                    {
                        break;
                    }
                    var exchange = _engine.Handle(request);
                    await transport.WriteAsync(exchange.Response, cancellationToken).ConfigureAwait(false);
                    foreach (var item in exchange.Events)
                        await transport.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                    foreach (var reconciliationCase in exchange.CaseFacts)
                        await transport.WriteAsync(reconciliationCase, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception) when (exception is IOException or SocketException or UnauthorizedAccessException) { }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed) return ValueTask.CompletedTask;
        _disposed = true;
        _listener.Dispose();
        CryptographicOperations.ZeroMemory(_secret);
        DeleteOwnedSocket();
        return ValueTask.CompletedTask;
    }

    private static string PreparePath(string socketPath)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Execution Unix sockets require a Unix host.");
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        var fullPath = Path.GetFullPath(socketPath);
        var directory = Path.GetDirectoryName(fullPath) ??
                        throw new InvalidOperationException("The execution socket path has no parent.");
        Directory.CreateDirectory(directory);
        var directoryInfo = new DirectoryInfo(directory);
        if (directoryInfo.LinkTarget is not null)
            throw new InvalidDataException("The execution socket directory cannot be a symbolic link.");
        File.SetUnixFileMode(directory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        if (File.Exists(fullPath) || Directory.Exists(fullPath) || new FileInfo(fullPath).LinkTarget is not null)
            throw new IOException("The execution socket path already exists; it will not be replaced.");
        return fullPath;
    }

    private void DeleteOwnedSocket()
    {
        try { File.Delete(_socketPath); }
        catch { }
    }

    private void Track(Task connection)
    {
        lock (_connectionsGate) _connections.Add(connection);
        _ = connection.ContinueWith(
            completed =>
            {
                lock (_connectionsGate) _connections.Remove(completed);
            },
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    internal static void VerifySameUser(Socket socket)
    {
        if (!OperatingSystem.IsMacOS()) return;
        if (getpeereid(socket.Handle.ToInt32(), out var effectiveUser, out _) != 0 || effectiveUser != geteuid())
            throw new UnauthorizedAccessException("The execution socket peer is not the current macOS user.");
    }

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern int getpeereid(int socket, out uint effectiveUserId, out uint effectiveGroupId);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern uint geteuid();
}

/// <summary>Blocking endpoint facade over one authenticated local socket connection.</summary>
public sealed class ExecutionUnixSocketClientEndpoint : IExecutionServiceEndpoint, IAsyncDisposable
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(10);
    private readonly object _gate = new();
    private readonly Socket _socket;
    private readonly StreamExecutionFrameTransport _transport;
    private bool _disposed;

    private ExecutionUnixSocketClientEndpoint(
        Socket socket,
        StreamExecutionFrameTransport transport,
        ExecutionResource resource,
        ExecutionLeaseGrant leaseGrant)
    {
        _socket = socket;
        _transport = transport;
        Resource = resource;
        LeaseGrant = leaseGrant;
    }

    public ExecutionResource Resource { get; }
    public ExecutionLeaseGrant LeaseGrant { get; }

    public static async Task<ExecutionUnixSocketClientEndpoint> ConnectAsync(
        string socketPath,
        IExecutionServiceSecretStore secretStore,
        ExecutionResource resource,
        ExecutionLeaseGrant leaseGrant,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(socketPath);
        ArgumentNullException.ThrowIfNull(secretStore);
        if (!resource.IsValid || !leaseGrant.IsValid || leaseGrant.Claim.Resource != resource)
            throw new ArgumentException("The expected execution resource or lease is invalid.");
        var secret = secretStore.LoadOrCreate();
        Socket? socket = null;
        StreamExecutionFrameTransport? transport = null;
        try
        {
            socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            await socket.ConnectAsync(new UnixDomainSocketEndPoint(Path.GetFullPath(socketPath)), deadline.Token)
                .ConfigureAwait(false);
            ExecutionUnixSocketServer.VerifySameUser(socket);
            var stream = new NetworkStream(socket, ownsSocket: false);
            transport = new StreamExecutionFrameTransport(stream);
            using var authenticator = new ExecutionIpcAuthenticator(secret);
            var handshake = await authenticator.AuthenticateClientAsync(transport, deadline.Token).ConfigureAwait(false);
            if (!handshake.IsAuthenticated)
                throw new InvalidOperationException($"Execution socket authentication failed: {handshake.Failure}: {handshake.Reason}");
            return new ExecutionUnixSocketClientEndpoint(socket, transport, resource, leaseGrant);
        }
        catch
        {
            if (transport is not null) await transport.DisposeAsync().ConfigureAwait(false);
            socket?.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    public ExecutionServiceExchange Handle(ExecutionServiceRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            using var deadline = new CancellationTokenSource(OperationTimeout);
            return ExchangeAsync(request, deadline.Token).GetAwaiter().GetResult();
        }
    }

    private async Task<ExecutionServiceExchange> ExchangeAsync(
        ExecutionServiceRequest request,
        CancellationToken cancellationToken)
    {
        await _transport.WriteAsync(request, cancellationToken).ConfigureAwait(false);
        var response = await _transport.ReadAsync<ExecutionServiceResponse>(cancellationToken).ConfigureAwait(false);
        if (!string.Equals(response.RequestId, request.RequestId, StringComparison.Ordinal) ||
            response.ProtocolVersion != ExecutionServiceProtocol.CurrentVersion ||
            response.EventCount < 0 || response.EventCount > ExecutionServiceProtocol.MaximumEventsPerExchange ||
            response.ReconciliationCaseCount < 0 ||
            response.ReconciliationCaseCount > ExecutionServiceProtocol.MaximumReconciliationCasesPerExchange ||
            (response.HasMoreReconciliationCases &&
             response.ReconciliationCaseCount != ExecutionServiceProtocol.MaximumReconciliationCasesPerExchange))
            throw new InvalidDataException("The execution service returned an invalid response envelope.");
        var events = new ExecutionServiceEvent[response.EventCount];
        var previous = request.AfterOutboxSequence;
        for (var index = 0; index < events.Length; index++)
        {
            var item = await _transport.ReadAsync<ExecutionServiceEvent>(cancellationToken).ConfigureAwait(false);
            if (item.Event is null || item.OutboxSequence <= previous)
                throw new InvalidDataException("The execution service event stream is absent or out of order.");
            events[index] = item;
            previous = item.OutboxSequence;
        }
        if (response.LastOutboxSequence != previous)
            throw new InvalidDataException("The response cursor does not match its event stream.");

        var cases = new ReconciliationCase[response.ReconciliationCaseCount];
        string? previousCaseId = request.AfterReconciliationCaseId?.Value;
        for (var index = 0; index < cases.Length; index++)
        {
            var item = await _transport.ReadAsync<ReconciliationCase>(cancellationToken).ConfigureAwait(false);
            if (item is null || !item.IsValid || item.Resource != Resource ||
                previousCaseId is not null && string.CompareOrdinal(item.CaseId.Value, previousCaseId) <= 0)
                throw new InvalidDataException("The reconciliation-case stream is absent, invalid, or out of order.");
            cases[index] = item;
            previousCaseId = item.CaseId.Value;
        }
        if ((cases.Length == 0 && response.LastReconciliationCaseId.HasValue) ||
            (cases.Length != 0 && response.LastReconciliationCaseId != cases[^1].CaseId))
            throw new InvalidDataException("The reconciliation-case cursor does not match its stream.");
        return new ExecutionServiceExchange(response, Array.AsReadOnly(events), Array.AsReadOnly(cases));
    }

    public async ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
        }
        await _transport.DisposeAsync().ConfigureAwait(false);
        _socket.Dispose();
    }
}
