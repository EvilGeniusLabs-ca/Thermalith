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
            return await LinuxBLETransport.ListDevices();

        throw new NotImplementedException("Support for this OS has not been implemented");
    }

    public static INiimbotTransport CreateTransport(string address)
    {
        if (OperatingSystem.IsWindows())
            return new SerialTransport(address);
        else if (OperatingSystem.IsLinux())
            return new LinuxBLETransport(address);

        throw new NotImplementedException("Support for this OS has not been implemented");
    }

    public static NiimbotClient CreateClient(string address) => new(CreateTransport(address), ownsTransport: true);
    public static async Task<ProbeResult?> ProbeAsync(string address, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        await using var transport = CreateTransport(address); // Dispose of it, because probe does not
        return await PrinterProbe.ProbeAsync(transport, timeout, ct);
    }
}
