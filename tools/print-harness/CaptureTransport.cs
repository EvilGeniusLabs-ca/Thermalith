using Niimbot.Net;

namespace Thermalith.PrintHarness;

/// <summary>
/// An <see cref="INiimbotTransport"/> decorator that tees every raw byte to/from the wrapped
/// transport into a sink, so a live test produces a ground-truth TX/RX capture. This is the
/// spec's "byte-level capture from a real B1 is the tiebreaker" instrument (§5/§10): the bytes it
/// records are exactly what the 4 PENDING-HARDWARE-VERIFICATION tests compare against.
/// </summary>
public sealed class CaptureTransport(INiimbotTransport inner, Action<char, ReadOnlyMemory<byte>> sink) : INiimbotTransport
{
    public bool IsConnected => inner.IsConnected;

    public event EventHandler<TransportState>? StateChanged
    {
        add => inner.StateChanged += value;
        remove => inner.StateChanged -= value;
    }

    public ValueTask ConnectAsync(CancellationToken ct = default) => inner.ConnectAsync(ct);

    public ValueTask DisconnectAsync(CancellationToken ct = default) => inner.DisconnectAsync(ct);

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        sink('T', data);
        await inner.WriteAsync(data, ct).ConfigureAwait(false);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var n = await inner.ReadAsync(buffer, ct).ConfigureAwait(false);
        if (n > 0)
            sink('R', buffer[..n]);
        return n;
    }

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}
