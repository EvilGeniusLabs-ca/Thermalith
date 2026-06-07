using System.IO.Ports;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

namespace Niimbot.Net.Transport;

/// <summary>
/// The shipped default <see cref="INiimbotTransport"/> over a serial port (USB CDC), using
/// <see cref="SerialPort"/>. A dumb byte duplex — it knows nothing about NIIMBOT packets; all
/// framing lives in <see cref="NiimbotClient"/> above it (spec §5.1). I/O goes through the port's
/// <see cref="SerialPort.BaseStream"/> so reads/writes are genuinely async.
/// </summary>
public sealed class SerialTransport : INiimbotTransport
{
    /// <summary>NIIMBOT USB-serial bridges run at 115200 8N1.</summary>
    public const int DefaultBaudRate = 115200;

    private readonly string _portName;
    private readonly int _baudRate;
    private readonly int _readTimeoutMs;
    private readonly int _writeTimeoutMs;
    private SerialPort? _port;

    public SerialTransport(string portName, int baudRate = DefaultBaudRate, int readTimeoutMs = 500, int writeTimeoutMs = 2000)
    {
        _portName = portName ?? throw new ArgumentNullException(nameof(portName));
        _baudRate = baudRate;
        _readTimeoutMs = readTimeoutMs;
        // Short write timeouts let discovery fail fast on a powered-off printer (the synchronous
        // Write ignores cancellation, so the timeout is the only bound). Printing uses the default.
        _writeTimeoutMs = writeTimeoutMs;
    }

    public bool IsConnected => _port?.IsOpen ?? false;

    public event EventHandler<TransportState>? StateChanged;

    public ValueTask ConnectAsync(CancellationToken ct = default)
    {
        if (IsConnected)
            return ValueTask.CompletedTask;

        StateChanged?.Invoke(this, TransportState.Connecting);
        try
        {
            var port = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = _readTimeoutMs,
                WriteTimeout = _writeTimeoutMs,
                // Niimbot bridges don't use hardware flow control; asserting the lines is harmless
                // and some adapters need DTR/RTS high to enable the data path.
                DtrEnable = true,
                RtsEnable = true,
            };
            port.Open();
            _port = port;
        }
        catch
        {
            StateChanged?.Invoke(this, TransportState.Faulted);
            throw;
        }

        StateChanged?.Invoke(this, TransportState.Connected);
        return ValueTask.CompletedTask;
    }

    public ValueTask DisconnectAsync(CancellationToken ct = default)
    {
        if (_port is { } port)
        {
            try
            {
                if (port.IsOpen)
                    port.Close();
            }
            finally
            {
                port.Dispose();
                _port = null;
                StateChanged?.Invoke(this, TransportState.Disconnected);
            }
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var port = Port;
        // Synchronous Write on a worker. SerialPort.BaseStream's async methods are unreliable on
        // Windows; the synchronous API honors WriteTimeout and is far more predictable.
        // Catch on the worker thread (a WriteTimeout fires when e.g. the printer is powered off) so
        // the exception isn't flagged by the debugger as "unhandled in user code" at the throw site,
        // then rethrow on the awaiting context where callers (probe, client) handle it.
        ExceptionDispatchInfo? failure = null;
        await Task.Run(() =>
        {
            try
            {
                if (MemoryMarshal.TryGetArray(data, out var segment) && segment.Array is not null)
                    port.Write(segment.Array, segment.Offset, segment.Count);
                else
                {
                    var tmp = data.ToArray();
                    port.Write(tmp, 0, tmp.Length);
                }
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        }, ct).ConfigureAwait(false);

        failure?.Throw();
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var port = _port;
        if (port is null || !port.IsOpen)
            return 0;

        try
        {
            // CRITICAL: use the synchronous Read, not BaseStream.ReadAsync. On Windows the async
            // path ignores both the cancellation token AND ReadTimeout, so it blocks forever on an
            // idle port and the client's read pump can never be cancelled (the cause of the hang on
            // disconnect). The synchronous Read honors ReadTimeout, returning every ~500 ms so the
            // pump checks cancellation; a quiet line surfaces as TimeoutException → 0.
            return await Task.Run(() =>
            {
                try
                {
                    if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment) && segment.Array is not null)
                        return port.Read(segment.Array, segment.Offset, segment.Count);

                    var tmp = new byte[buffer.Length];
                    var k = port.Read(tmp, 0, tmp.Length);
                    tmp.AsSpan(0, k).CopyTo(buffer.Span);
                    return k;
                }
                catch (TimeoutException)
                {
                    return 0; // idle line within ReadTimeout
                }
            }, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return 0;
        }
        catch (Exception) when (!IsConnected)
        {
            // Surprise unplug / port closed mid-read.
            StateChanged?.Invoke(this, TransportState.Faulted);
            return 0;
        }
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);

    private SerialPort Port =>
        _port ?? throw new InvalidOperationException("Serial port is not connected.");
}
