using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;
using Niimbot.Net.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Channels;

namespace Niimbot.Net.Transport;

public sealed class LinuxBLETransport : INiimbotTransport
{
    public string Address { get; private set; } = "Unknown";
    public bool IsConnected { get; private set; }
    public event EventHandler<TransportState>? StateChanged;

    private const string GattServiceID = "e7810a71-73ae-499d-8c15-faa9aef0c3f2";
    private const string GattCharID = "bef8d6c9-9c21-4c9e-b632-bd58c1009f9f";

    // BlueZ write options: "command" = write-without-response. The default ("request") waits for an
    // over-the-air ACK per packet, which caps throughput at ~15 packets/s (~30 s per label).
    private static readonly Dictionary<string, object> WriteCommand = new() { ["type"] = "command" };

    private readonly int _readTimeoutMs;

    // Write pacing. Without the per-packet ACK nothing stops us outrunning the printer's receive
    // buffer, so pause briefly after every few writes. 0 for either disables pacing.
    private readonly int _pauseEveryWrites;
    private readonly int _pauseMs;
    private int _writesSincePause;

    private Device? _bleDevice;
    private GattCharacteristic? _gattChar;
    // Notifications from BlueZ arrive on D-Bus threads while ReadAsync drains them on the read pump,
    // so this must be thread-safe (a plain Queue<T> can lose or corrupt entries under that race).
    private readonly Channel<byte[]> _readBuffer = Channel.CreateUnbounded<byte[]>();

    public static async Task<IReadOnlyList<string>> ListDevices()
    {
        using var adapter = (await BlueZManager.GetAdaptersAsync()).First();

        var devices = await adapter.GetDevicesAsync();
        var addresses = new List<string>();
        foreach (var device in devices)
        {
            addresses.Add(await device.GetAddressAsync());
        }
        return addresses;
    }

    /// <param name="address">BLE MAC address of the printer.</param>
    /// <param name="readTimeoutMs">How long <see cref="ReadAsync"/> waits before reporting an idle line.</param>
    /// <param name="pauseEveryWrites">Pause after this many writes (0 disables pacing).</param>
    /// <param name="pauseMs">Length of each pause in milliseconds (0 disables pacing).</param>
    public LinuxBLETransport(string address, int readTimeoutMs = 5000, int pauseEveryWrites = 4, int pauseMs = 20)
    {
        Address = address;
        _readTimeoutMs = readTimeoutMs;
        _pauseEveryWrites = Math.Max(0, pauseEveryWrites);
        _pauseMs = Math.Max(0, pauseMs);
    }

    public async ValueTask ConnectAsync(CancellationToken ct = default)
    {
        if (IsConnected)
            return;

        NiimbotTrace.Log("LinuxBLE", $"open {Address}");
        StateChanged?.Invoke(this, TransportState.Connecting);

        // Drop anything left over from a previous connection
        while (_readBuffer.Reader.TryRead(out _)) { }

        try
        {
            using var adapter = (await BlueZManager.GetAdaptersAsync()).First();
            _bleDevice = await adapter.GetDeviceAsync(Address);

            if (_bleDevice == null)
            {
                NiimbotTrace.Log("LinuxBLE", $"open FAILED {Address}: No device found for address");
                throw new InvalidOperationException($"No Bluetooth device found for address {Address}");
            }

            await _bleDevice.ConnectAsync().ConfigureAwait(false);

            // Retrieve the GATT characteristic, used to send/receive data
            var service = await _bleDevice.GetServiceAsync(GattServiceID).ConfigureAwait(false);
            _gattChar = service == null ? null : await service.GetCharacteristicAsync(GattCharID).ConfigureAwait(false);

            // TODO: This can fail; maybe retry
            if (_gattChar == null)
            {
                NiimbotTrace.Log("LinuxBLE", $"open FAILED {Address}: Unable to lookup GATT characterisic (is this a printer?)");
                throw new InvalidOperationException("Unable to lookup GATT characterisic (is this a printer?)");
            }

            // Subscribe to responses. Must be undone in ReleaseAsync: the subscription outlives a
            // device disconnect, so a forgotten handler keeps receiving (and tracing) every later reply.
            _gattChar.Value += OnValueAsync;
            await _gattChar.StartNotifyAsync().ConfigureAwait(false);
        }
        catch
        {
            await ReleaseAsync().ConfigureAwait(false);
            StateChanged?.Invoke(this, TransportState.Faulted);
            throw;
        }

        IsConnected = true;
        NiimbotTrace.Log("LinuxBLE", $"open OK {Address}");
        StateChanged?.Invoke(this, TransportState.Connected);
    }

    public async ValueTask DisconnectAsync(CancellationToken ct = default)
    {
        if (_bleDevice == null && _gattChar == null)
            return;

        NiimbotTrace.Log("LinuxBLE", $"disconnecting {Address}");

        await ReleaseAsync().ConfigureAwait(false);
        StateChanged?.Invoke(this, TransportState.Disconnected);
        NiimbotTrace.Log("LinuxBLE", $"disconnected {Address}");
    }

    private Task OnValueAsync(GattCharacteristic sender, GattCharacteristicValueEventArgs eventArgs)
    {
        NiimbotTrace.Bytes("LinuxBLE", "< received", eventArgs.Value);
        _readBuffer.Writer.TryWrite(eventArgs.Value); // Unbounded, so this always succeeds
        return Task.CompletedTask;
    }

    /// <summary>
    /// Unsubscribe from notifications, disconnect and dispose the BlueZ objects. Safe to call on a
    /// partially opened connection; each step is best-effort so one failure doesn't skip the rest.
    /// </summary>
    private async Task ReleaseAsync()
    {
        IsConnected = false;

        if (_gattChar is { } gattChar)
        {
            _gattChar = null;
            gattChar.Value -= OnValueAsync;
            try { await gattChar.StopNotifyAsync().ConfigureAwait(false); }
            catch (Exception ex) { NiimbotTrace.Log("LinuxBLE", $"StopNotify failed: {ex.Message}"); }
            (gattChar as IDisposable)?.Dispose();
        }

        if (_bleDevice is { } device)
        {
            _bleDevice = null;
            try { await device.DisconnectAsync().ConfigureAwait(false); }
            catch (Exception ex) { NiimbotTrace.Log("LinuxBLE", $"Disconnect failed: {ex.Message}"); }
            (device as IDisposable)?.Dispose();
        }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        // Do it in a separate task to avoid blocking the notification handler
        await Task.Run(async () =>
        {
            NiimbotTrace.Bytes("LinuxBLE", "> write", data.Span);

            if (_gattChar == null)
                throw new InvalidOperationException("BLE connection has not been established and GATT is not setup.");

            // Reuse the backing array only when it is exactly the data; a slice must be copied, or
            // the bytes either side of it would be sent too.
            var bytes = MemoryMarshal.TryGetArray(data, out var segment) && segment.Array is not null
                        && segment.Offset == 0 && segment.Count == segment.Array.Length
                ? segment.Array
                : data.ToArray();

            await _gattChar.WriteValueAsync(bytes, WriteCommand).ConfigureAwait(false);
        }).ConfigureAwait(false);

        // Pace the stream so the printer's buffer can keep up.
        if (_pauseEveryWrites > 0 && _pauseMs > 0 && ++_writesSincePause >= _pauseEveryWrites)
        {
            _writesSincePause = 0;
            await Task.Delay(_pauseMs, ct).ConfigureAwait(false);
        }
    }

    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
    {
        var reader = _readBuffer.Reader;

        // Wait (without blocking a thread) for the next notification, up to the read timeout
        if (!reader.TryRead(out var nextResponse))
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(_readTimeoutMs);
            try
            {
                if (!await reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false) || !reader.TryRead(out nextResponse))
                    return 0;
            }
            catch (OperationCanceledException)
            {
                return 0; // idle line (timeout) or caller cancelled, as before
            }
        }

        NiimbotTrace.Log("LinuxBLE", $"Read queue - {reader.Count + 1}");

        nextResponse.CopyTo(buffer.Span);
        return nextResponse.Length;
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);
}
