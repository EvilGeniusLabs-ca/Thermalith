using System.IO.Ports;

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
    private SerialPort? _port;

    public SerialTransport(string portName, int baudRate = DefaultBaudRate)
    {
        _portName = portName ?? throw new ArgumentNullException(nameof(portName));
        _baudRate = baudRate;
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
                ReadTimeout = 500,
                WriteTimeout = 2000,
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
        var stream = Stream;
        await stream.WriteAsync(data, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        try
        {
            return await Stream.ReadAsync(buffer, ct).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // No data within ReadTimeout — normal idle; report zero so the read pump loops.
            return 0;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception) when (!IsConnected)
        {
            // Surprise unplug surfaces here.
            StateChanged?.Invoke(this, TransportState.Faulted);
            return 0;
        }
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);

    private Stream Stream =>
        _port?.BaseStream ?? throw new InvalidOperationException("Serial port is not connected.");
}
