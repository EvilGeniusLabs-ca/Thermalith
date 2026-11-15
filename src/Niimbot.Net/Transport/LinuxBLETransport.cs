using Linux.Bluetooth;
using Linux.Bluetooth.Extensions;
using Niimbot.Net.Diagnostics;
using System.Runtime.InteropServices;

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
    private readonly Queue<byte[]> _readBuffer = [];

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

        using var adapter = (await BlueZManager.GetAdaptersAsync()).First();
        _bleDevice = await adapter.GetDeviceAsync(Address);

        if (_bleDevice == null)
        {
            NiimbotTrace.Log("LinuxBLE", $"open FAILED {Address}: No device found for address");
            StateChanged?.Invoke(this, TransportState.Faulted);
            throw new InvalidOperationException("Unable to lookup GATT characterisic (is this a printer?)");
        }

        await _bleDevice.ConnectAsync().ConfigureAwait(false);

        // Retrieve the GATT characteristic, used to send/receive data
        var service = await _bleDevice.GetServiceAsync(GattServiceID).ConfigureAwait(false);
        _gattChar = await service.GetCharacteristicAsync(GattCharID).ConfigureAwait(false);

        // TODO: This can fail; maybe retry & then Disconnect
        if (_gattChar == null)
        {
            NiimbotTrace.Log("LinuxBLE", $"open FAILED {Address}: Unable to lookup GATT characterisic (is this a printer?)");
            StateChanged?.Invoke(this, TransportState.Faulted);
            throw new InvalidOperationException("Unable to lookup GATT characterisic (is this a printer?)");
        }

        // Subscribe to responses
        _gattChar.Value += async (sender, eventArgs) =>
        {
            NiimbotTrace.Bytes("LinuxBLE", "< received", eventArgs.Value);
            _readBuffer.Enqueue(eventArgs.Value);
        };
        await _gattChar.StartNotifyAsync().ConfigureAwait(false);

        IsConnected = true;
        NiimbotTrace.Log("LinuxBLE", $"open OK {Address}");
        StateChanged?.Invoke(this, TransportState.Connected);
    }

    public async ValueTask DisconnectAsync(CancellationToken ct = default)
    {
        if (!IsConnected || _bleDevice == null)
            return;

        NiimbotTrace.Log("LinuxBLE", $"disconnecting {Address}");

        await _bleDevice.DisconnectAsync().ConfigureAwait(false);
        IsConnected = false;
        StateChanged?.Invoke(this, TransportState.Disconnected);
        NiimbotTrace.Log("LinuxBLE", $"disconnected {Address}");
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
        // Do it in a separate task to avoid blocking the notification handler
        return await Task.Run(async () =>
        {
            var waited = 0;
            while (true)
            {
                if (ct.IsCancellationRequested)
                    return 0;
                if (_readBuffer.Count > 0)
                    break;
                if (waited >= _readTimeoutMs)
                    return 0; // idle line
                Thread.Sleep(15);
                waited += 15;
            }

            NiimbotTrace.Log("LinuxBLE", $"Read queue - {_readBuffer.Count}");

            var nextResponse = _readBuffer.Dequeue();
            if (MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)buffer, out var segment) && segment.Array is not null)
                nextResponse.CopyTo(segment.Array);
            else
                nextResponse.CopyTo(buffer.Span);

            return nextResponse.Length;
        });
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);
}
