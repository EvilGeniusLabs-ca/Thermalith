namespace Niimbot.Net;

/// <summary>Connection state for an <see cref="INiimbotTransport"/>.</summary>
public enum TransportState
{
    Disconnected,
    Connecting,
    Connected,
    Faulted,
}

/// <summary>
/// A dumb async byte duplex to a NIIMBOT printer. The transport knows nothing about
/// NIIMBOT packets: framing (header/opcode/length/checksum/footer), request/response
/// correlation, and RLE all live in the client layer above it. This minimal contract is
/// what lets serial, a future BLE transport, and a test/replay transport all be trivial.
/// See build spec §5.1.
/// </summary>
public interface INiimbotTransport : IAsyncDisposable
{
    bool IsConnected { get; }

    ValueTask ConnectAsync(CancellationToken ct = default);

    ValueTask DisconnectAsync(CancellationToken ct = default);

    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);

    ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default);

    /// <summary>Raised on connect, disconnect, surprise unplug, and fault transitions.</summary>
    event EventHandler<TransportState>? StateChanged;
}
