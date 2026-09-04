using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingTerminal.Core.Execution;

/// <summary>Bounded typed framing used only after a local execution peer is connected.</summary>
public interface IExecutionFrameTransport : IAsyncDisposable
{
    ValueTask WriteAsync<TFrame>(TFrame frame, CancellationToken cancellationToken = default);
    ValueTask<TFrame> ReadAsync<TFrame>(CancellationToken cancellationToken = default);
}

/// <summary>
/// Four-byte big-endian length-prefixed JSON over a duplex stream. Read and write directions are
/// serialized independently so concurrent operations cannot interleave bytes.
/// </summary>
public sealed class StreamExecutionFrameTransport : IExecutionFrameTransport
{
    public const int DefaultMaximumFrameBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly Stream _stream;
    private readonly int _maximumFrameBytes;
    private readonly bool _leaveOpen;
    private readonly SemaphoreSlim _readGate = new(1, 1);
    private readonly SemaphoreSlim _writeGate = new(1, 1);
    private bool _disposed;

    public StreamExecutionFrameTransport(
        Stream stream,
        int maximumFrameBytes = DefaultMaximumFrameBytes,
        bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead || !stream.CanWrite)
            throw new ArgumentException("The execution frame stream must be readable and writable.", nameof(stream));
        if (maximumFrameBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumFrameBytes));
        _maximumFrameBytes = maximumFrameBytes;
        _leaveOpen = leaveOpen;
    }

    public async ValueTask WriteAsync<TFrame>(TFrame frame, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(frame);
        byte[] payload;
        try
        {
            payload = JsonSerializer.SerializeToUtf8Bytes(frame, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The execution IPC frame could not be serialized: {exception.Message}", exception);
        }
        if (payload.Length is <= 0 || payload.Length > _maximumFrameBytes)
            throw InvalidLength(payload.Length, _maximumFrameBytes);

        await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var prefix = new byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(prefix, payload.Length);
            await _stream.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    public async ValueTask<TFrame> ReadAsync<TFrame>(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _readGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var prefix = new byte[sizeof(int)];
            await _stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
            var length = BinaryPrimitives.ReadInt32BigEndian(prefix);
            if (length is <= 0 || length > _maximumFrameBytes)
                throw InvalidLength(length, _maximumFrameBytes);
            var payload = new byte[length];
            await _stream.ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false);
            try
            {
                return JsonSerializer.Deserialize<TFrame>(payload, JsonOptions) ??
                       throw new InvalidDataException("The execution IPC frame contained JSON null.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("The execution IPC frame contained invalid JSON.", exception);
            }
        }
        finally
        {
            _readGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        if (!_leaveOpen) await _stream.DisposeAsync().ConfigureAwait(false);
        _readGate.Dispose();
        _writeGate.Dispose();
    }

    private static InvalidDataException InvalidLength(int length, int maximum) =>
        new($"The execution IPC frame length {length} is outside the allowed range 1..{maximum}.");
}
