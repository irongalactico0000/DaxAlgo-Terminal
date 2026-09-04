using System.Buffers.Binary;
using System.Net.Sockets;
using FluentAssertions;
using TradingTerminal.Core.Execution;
using TradingTerminal.Core.Time;
using TradingTerminal.Infrastructure.Execution;
using Xunit;

namespace TradingTerminal.Tests.Headless.Execution;

public sealed class ExecutionMacIpcTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private static readonly ExecutionResource Resource = new(
        new VenueId("paper-ipc"),
        new TradingAccountId("paper-ipc-account"),
        ExecutionEnvironment.SimulatedPaper);

    [Fact]
    public async Task Bounded_frame_round_trips_and_rejects_oversized_output()
    {
        await using var stream = new MemoryStream();
        await using var transport = new StreamExecutionFrameTransport(stream, maximumFrameBytes: 128, leaveOpen: true);
        var expected = new SampleFrame("paper", 7);

        await transport.WriteAsync(expected);
        stream.Position = 0;
        (await transport.ReadAsync<SampleFrame>()).Should().Be(expected);

        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await transport.WriteAsync(new SampleFrame(new string('x', 256), 8)));
    }

    [Fact]
    public async Task Bounded_frame_rejects_zero_oversized_and_truncated_input_before_deserialization()
    {
        await AssertBadPrefix(0);
        await AssertBadPrefix(StreamExecutionFrameTransport.DefaultMaximumFrameBytes + 1);

        var bytes = new byte[sizeof(int) + 2];
        BinaryPrimitives.WriteInt32BigEndian(bytes, 20);
        await using var truncated = new StreamExecutionFrameTransport(new MemoryStream(bytes));
        await Assert.ThrowsAsync<EndOfStreamException>(async () =>
            await truncated.ReadAsync<SampleFrame>());
    }

    [Fact]
    public async Task Mutual_authentication_succeeds_and_protocol_mismatch_fails_closed()
    {
        var secret = Secret(0x41);
        await using (var pair = await SocketTransportPair.ConnectAsync())
        {
            using var server = new ExecutionIpcAuthenticator(secret, new FixedNonceSource(0x51));
            using var client = new ExecutionIpcAuthenticator(secret, new FixedNonceSource(0x61));
            using var timeout = new CancellationTokenSource(TestTimeout);
            var serverTask = server.AuthenticateServerAsync(pair.Server, timeout.Token).AsTask();
            var clientTask = client.AuthenticateClientAsync(pair.Client, timeout.Token).AsTask();
            var results = await Task.WhenAll(serverTask, clientTask);

            results.Should().OnlyContain(result => result.IsAuthenticated);
            results.Should().OnlyContain(result => result.NegotiatedVersion == ExecutionIpcProtocol.Version1);
        }

        await using (var pair = await SocketTransportPair.ConnectAsync())
        {
            using var server = new ExecutionIpcAuthenticator(
                secret, new FixedNonceSource(0x52), ExecutionIpcProtocol.Version1);
            using var client = new ExecutionIpcAuthenticator(
                secret, new FixedNonceSource(0x62), ExecutionIpcProtocol.Version1 + 1);
            using var timeout = new CancellationTokenSource(TestTimeout);
            var serverTask = server.AuthenticateServerAsync(pair.Server, timeout.Token).AsTask();
            var clientTask = client.AuthenticateClientAsync(pair.Client, timeout.Token).AsTask();
            var results = await Task.WhenAll(serverTask, clientTask);

            results.Should().OnlyContain(result =>
                result.Failure == ExecutionHandshakeFailure.ProtocolVersionMismatch);
        }
    }

    [Fact]
    public async Task Wrong_secret_and_reflected_server_proof_are_rejected()
    {
        await using (var pair = await SocketTransportPair.ConnectAsync())
        {
            using var server = new ExecutionIpcAuthenticator(Secret(0x42), new FixedNonceSource(0x53));
            using var client = new ExecutionIpcAuthenticator(Secret(0x43), new FixedNonceSource(0x63));
            using var timeout = new CancellationTokenSource(TestTimeout);
            var serverTask = server.AuthenticateServerAsync(pair.Server, timeout.Token).AsTask();
            var clientResult = await client.AuthenticateClientAsync(pair.Client, timeout.Token);
            await pair.Client.DisposeAsync();
            var serverResult = await serverTask;

            clientResult.Failure.Should().Be(ExecutionHandshakeFailure.AuthenticationFailed);
            serverResult.Failure.Should().Be(ExecutionHandshakeFailure.AuthenticationFailed);
        }

        await using (var pair = await SocketTransportPair.ConnectAsync())
        await using (var reflectingClient = new ReflectServerProofTransport(pair.Client))
        {
            var secret = Secret(0x44);
            using var server = new ExecutionIpcAuthenticator(secret, new FixedNonceSource(0x54));
            using var client = new ExecutionIpcAuthenticator(secret, new FixedNonceSource(0x64));
            using var timeout = new CancellationTokenSource(TestTimeout);
            var serverTask = server.AuthenticateServerAsync(pair.Server, timeout.Token).AsTask();
            var clientTask = client.AuthenticateClientAsync(reflectingClient, timeout.Token).AsTask();
            var results = await Task.WhenAll(serverTask, clientTask);

            results.Should().OnlyContain(result =>
                result.Failure == ExecutionHandshakeFailure.AuthenticationFailed);
        }
    }

    [Fact]
    public async Task Owner_only_socket_authenticates_then_carries_a_correlated_service_exchange()
    {
        await WithRuntime(async (runtime, socketDirectory, socketPath) =>
        {
            var store = new FixedSecretStore(0x71);
            await using var server = new ExecutionUnixSocketServer(runtime.Service, store, socketPath);
            using var stop = new CancellationTokenSource();
            var serverTask = server.RunAsync(stop.Token);
            await using var endpoint = await ExecutionUnixSocketClientEndpoint.ConnectAsync(
                socketPath, store, Resource, runtime.LeaseGrant);

            if (!OperatingSystem.IsWindows())
            {
                File.GetUnixFileMode(socketDirectory).Should().Be(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                File.GetUnixFileMode(socketPath).Should().Be(
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            var request = ReadRequest(runtime, "ipc-status");
            var exchange = endpoint.Handle(request);

            exchange.Response.IsSuccess.Should().BeTrue(exchange.Response.Reason);
            exchange.Response.RequestId.Should().Be(request.RequestId);
            exchange.Response.Resource.Should().Be(Resource);
            exchange.Response.ExecutionLeaseId.Should().Be(runtime.LeaseGrant.Claim.LeaseId);

            await endpoint.DisposeAsync();
            stop.Cancel();
            await serverTask;
            await server.DisposeAsync();
            File.Exists(socketPath).Should().BeFalse();
        });
    }

    [Fact]
    public async Task Authenticated_socket_carries_reconciliation_evidence_and_durable_resolution()
    {
        await WithRuntime(async (runtime, _, socketPath) =>
        {
            var opened = new ReconciliationCase(
                new ReconciliationCaseId("ipc-reconciliation-case"),
                Resource,
                ReconciliationSubjectKind.Cash,
                "currency:SIM",
                null,
                ReconciliationCaseKind.CashMismatch,
                ReconciliationCaseStatus.Open,
                "ledger total=-200",
                "venue total=-100",
                new DateTimeOffset(2026, 8, 24, 8, 0, 0, TimeSpan.Zero));
            runtime.Ledger.TryAppend(opened).Should().BeTrue();
            var secrets = new FixedSecretStore(0x76);
            await using var server = new ExecutionUnixSocketServer(runtime.Service, secrets, socketPath);
            using var stop = new CancellationTokenSource();
            var serverTask = server.RunAsync(stop.Token);
            await using var endpoint = await ExecutionUnixSocketClientEndpoint.ConnectAsync(
                socketPath, secrets, Resource, runtime.LeaseGrant);

            var listed = endpoint.Handle(new ExecutionServiceRequest(
                ExecutionServiceProtocol.CurrentVersion,
                "ipc-list-reconciliation",
                ExecutionServiceRequestKind.ReconciliationCases,
                Resource,
                runtime.LeaseGrant.Claim.LeaseId,
                runtime.LeaseGrant.Claim.FencingToken));
            var resolved = endpoint.Handle(new ExecutionServiceRequest(
                ExecutionServiceProtocol.CurrentVersion,
                "ipc-resolve-reconciliation",
                ExecutionServiceRequestKind.ResolveReconciliationCase,
                Resource,
                runtime.LeaseGrant.Claim.LeaseId,
                runtime.LeaseGrant.Claim.FencingToken,
                ReconciliationResolution: new ExecutionReconciliationResolutionRequest(
                    opened.CaseId,
                    "ipc-operator",
                    "Authenticated operator compared both immutable evidence records.")));
            var relisted = endpoint.Handle(new ExecutionServiceRequest(
                ExecutionServiceProtocol.CurrentVersion,
                "ipc-relist-reconciliation",
                ExecutionServiceRequestKind.ReconciliationCases,
                Resource,
                runtime.LeaseGrant.Claim.LeaseId,
                runtime.LeaseGrant.Claim.FencingToken));

            listed.Response.IsSuccess.Should().BeTrue(listed.Response.Reason);
            listed.CaseFacts.Should().ContainSingle().Which.Should().BeEquivalentTo(opened);
            listed.Response.ReconciliationAdmissionBlocked.Should().BeTrue();
            listed.Events.Should().BeEmpty();
            resolved.Response.IsSuccess.Should().BeTrue(resolved.Response.Reason);
            relisted.CaseFacts.Should().ContainSingle().Which.Should().Match<ReconciliationCase>(item =>
                item.CaseId == opened.CaseId &&
                item.Status == ReconciliationCaseStatus.Resolved &&
                item.ResolvedBy == "ipc-operator");
            relisted.Response.ReconciliationAdmissionBlocked.Should().BeFalse();
            runtime.Ledger.Read(opened.CaseId).Should().HaveCount(2);

            await endpoint.DisposeAsync();
            stop.Cancel();
            await serverTask;
        });
    }

    [Fact]
    public async Task Socket_rejects_wrong_secret_but_remains_available_to_the_authorized_client()
    {
        await WithRuntime(async (runtime, _, socketPath) =>
        {
            var authorized = new FixedSecretStore(0x72);
            await using var server = new ExecutionUnixSocketServer(runtime.Service, authorized, socketPath);
            using var stop = new CancellationTokenSource();
            var serverTask = server.RunAsync(stop.Token);

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await ExecutionUnixSocketClientEndpoint.ConnectAsync(
                    socketPath, new FixedSecretStore(0x73), Resource, runtime.LeaseGrant));
            failure.Message.Contains("authentication failed", StringComparison.OrdinalIgnoreCase)
                .Should().BeTrue(failure.Message);

            await using var endpoint = await ExecutionUnixSocketClientEndpoint.ConnectAsync(
                socketPath, authorized, Resource, runtime.LeaseGrant);
            endpoint.Handle(ReadRequest(runtime, "authorized-after-rejection"))
                .Response.IsSuccess.Should().BeTrue();

            await endpoint.DisposeAsync();
            stop.Cancel();
            await serverTask;
        });
    }

    [Fact]
    public async Task Socket_path_refuses_preexisting_nodes_and_symbolic_parent_directories()
    {
        await WithRuntime(async (runtime, socketDirectory, socketPath) =>
        {
            File.WriteAllText(socketPath, "do-not-replace");
            var createOverFile = () => new ExecutionUnixSocketServer(
                runtime.Service, new FixedSecretStore(0x74), socketPath);
            createOverFile.Should().Throw<IOException>();
            File.ReadAllText(socketPath).Should().Be("do-not-replace");
            File.Delete(socketPath);

            var realDirectory = Path.Combine(socketDirectory, "real");
            var linkedDirectory = Path.Combine(socketDirectory, "linked");
            Directory.CreateDirectory(realDirectory);
            Directory.CreateSymbolicLink(linkedDirectory, realDirectory);
            var createUnderLink = () => new ExecutionUnixSocketServer(
                runtime.Service,
                new FixedSecretStore(0x75),
                Path.Combine(linkedDirectory, "paper.sock"));
            createUnderLink.Should().Throw<InvalidDataException>();
            await Task.CompletedTask;
        });
    }

    private static async Task AssertBadPrefix(int length)
    {
        var bytes = new byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(bytes, length);
        await using var transport = new StreamExecutionFrameTransport(new MemoryStream(bytes));
        await Assert.ThrowsAsync<InvalidDataException>(async () =>
            await transport.ReadAsync<SampleFrame>());
    }

    private static ExecutionServiceRequest ReadRequest(PaperExecutionServiceRuntime runtime, string requestId) =>
        new(
            ExecutionServiceProtocol.CurrentVersion,
            requestId,
            ExecutionServiceRequestKind.Status,
            Resource,
            runtime.LeaseGrant.Claim.LeaseId,
            runtime.LeaseGrant.Claim.FencingToken);

    private static async Task WithRuntime(
        Func<PaperExecutionServiceRuntime, string, string, Task> test)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var ledgerDirectory = Path.Combine(Path.GetTempPath(), "daxalgo-ipc-ledger-tests", suffix);
        var socketDirectory = Path.Combine("/private/tmp", $"dax-ipc-{suffix}");
        Directory.CreateDirectory(ledgerDirectory);
        Directory.CreateDirectory(socketDirectory);
        try
        {
            using var runtime = PaperExecutionServiceRuntime.Create(
                Path.Combine(ledgerDirectory, "orders.db"),
                Resource,
                new FixedClock(new DateTime(2026, 8, 24, 8, 0, 0, DateTimeKind.Utc)),
                new ExecutionLeaseId($"ipc-lease-{suffix}"),
                new RuntimeInstanceId($"ipc-owner-{suffix}"));
            await test(runtime, socketDirectory, Path.Combine(socketDirectory, "paper.sock"));
        }
        finally
        {
            if (Directory.Exists(socketDirectory)) Directory.Delete(socketDirectory, recursive: true);
            if (Directory.Exists(ledgerDirectory)) Directory.Delete(ledgerDirectory, recursive: true);
        }
    }

    private static byte[] Secret(byte value) =>
        Enumerable.Repeat(value, ExecutionIpcProtocol.SecretSize).ToArray();

    private sealed record SampleFrame(string Value, int Sequence);

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class FixedSecretStore(byte value) : IExecutionServiceSecretStore
    {
        private readonly byte[] _secret = Secret(value);
        public byte[] LoadOrCreate() => (byte[])_secret.Clone();
    }

    private sealed class FixedNonceSource(byte value) : IExecutionNonceSource
    {
        public byte[] CreateNonce(int length) => Enumerable.Repeat(value, length).ToArray();
    }

    private sealed class ReflectServerProofTransport(IExecutionFrameTransport inner) : IExecutionFrameTransport
    {
        private byte[]? _serverProof;

        public ValueTask WriteAsync<TFrame>(TFrame frame, CancellationToken cancellationToken = default)
        {
            if (frame is ExecutionClientProof && _serverProof is not null)
            {
                return inner.WriteAsync(
                    (TFrame)(object)new ExecutionClientProof((byte[])_serverProof.Clone()),
                    cancellationToken);
            }
            return inner.WriteAsync(frame, cancellationToken);
        }

        public async ValueTask<TFrame> ReadAsync<TFrame>(CancellationToken cancellationToken = default)
        {
            var frame = await inner.ReadAsync<TFrame>(cancellationToken);
            if (frame is ExecutionServerChallenge challenge)
                _serverProof = (byte[])challenge.ServerProof.Clone();
            return frame;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SocketTransportPair : IAsyncDisposable
    {
        private readonly string _directory;
        private readonly string _socketPath;

        private SocketTransportPair(
            StreamExecutionFrameTransport server,
            StreamExecutionFrameTransport client,
            string directory,
            string socketPath)
        {
            Server = server;
            Client = client;
            _directory = directory;
            _socketPath = socketPath;
        }

        public StreamExecutionFrameTransport Server { get; }
        public StreamExecutionFrameTransport Client { get; }

        public static async Task<SocketTransportPair> ConnectAsync()
        {
            var suffix = Guid.NewGuid().ToString("N")[..10];
            var directory = Path.Combine("/private/tmp", $"dax-auth-{suffix}");
            var socketPath = Path.Combine(directory, "auth.sock");
            Directory.CreateDirectory(directory);
            using var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            listener.Listen(1);
            var client = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            try
            {
                using var timeout = new CancellationTokenSource(TestTimeout);
                var acceptedTask = listener.AcceptAsync(timeout.Token).AsTask();
                await client.ConnectAsync(new UnixDomainSocketEndPoint(socketPath), timeout.Token);
                var server = await acceptedTask;
                return new SocketTransportPair(
                    new StreamExecutionFrameTransport(new NetworkStream(server, ownsSocket: true)),
                    new StreamExecutionFrameTransport(new NetworkStream(client, ownsSocket: true)),
                    directory,
                    socketPath);
            }
            catch
            {
                client.Dispose();
                File.Delete(socketPath);
                Directory.Delete(directory);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Server.DisposeAsync();
            await Client.DisposeAsync();
            File.Delete(_socketPath);
            Directory.Delete(_directory);
        }
    }
}
