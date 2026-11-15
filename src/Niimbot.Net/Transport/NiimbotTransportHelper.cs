using Niimbot.Net.Diagnostics;

namespace Niimbot.Net.Transport;

/// <summary>
/// var ports = await NiimbotTransportHelper.ListDeviceAddresses();
/// var probe = await NiimbotTransportHelper.ProbeAsync(port, timeout: TimeSpan.FromSeconds(10));
/// await using var printerClient = NiimbotTransportHelper.CreateClient(probe.PortName);
/// </summary>
public class NiimbotTransportHelper
{
    public static async Task<IReadOnlyList<string>> ListDeviceAddresses()
    {
        if (OperatingSystem.IsWindows())
            return SerialPortEnumerator.Enumerate().Select(s => s.PortName).ToList();
        else if (OperatingSystem.IsLinux())
        {
            // USB serial ports first: USB is far faster than BLE, so a printer reachable both ways is
            // found (and used) over USB.
            var addresses = SerialPortEnumerator.Enumerate().Select(s => s.PortName).ToList();
            try
            {
                addresses.AddRange(await LinuxBLETransport.ListDevices());
            }
            catch (Exception ex)
            {
                // No Bluetooth adapter / bluetoothd not running shouldn't stop a USB printer being found
                NiimbotTrace.Log("ports", $"Bluetooth device listing failed: {ex.GetType().Name}: {ex.Message}");
            }
            return addresses;
        }

        throw new NotImplementedException("Support for this OS has not been implemented");
    }

    public static INiimbotTransport CreateTransport(string address)
    {
        if (OperatingSystem.IsWindows())
            return new SerialTransport(address);
        else if (OperatingSystem.IsLinux())
            return IsSerialDevicePath(address) ? new SerialTransport(address) : new LinuxBLETransport(address);

        throw new NotImplementedException("Support for this OS has not been implemented");
    }

    /// <summary>A Linux device node (e.g. <c>/dev/ttyACM0</c>) rather than a Bluetooth MAC address.</summary>
    private static bool IsSerialDevicePath(string address) => address.StartsWith("/dev/", StringComparison.Ordinal);

    public static NiimbotClient CreateClient(string address) => new(CreateTransport(address), ownsTransport: true);
    public static async Task<ProbeResult?> ProbeAsync(string address, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        await using var transport = CreateTransport(address); // Dispose of it, because probe does not
        return await PrinterProbe.ProbeAsync(transport, timeout, ct);
    }
}
