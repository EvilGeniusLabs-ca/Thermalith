using System.IO.Ports;

namespace Niimbot.Net.Transport;

/// <summary>A discovered serial port: its system name plus any friendly description available.</summary>
public sealed record SerialPortInfo(string PortName, string? Description = null);

/// <summary>
/// Enumerates candidate serial ports for discovery (spec §5.1). Returns the OS port names; richer
/// VID/PID + friendly-name enrichment is platform-specific and left as a TODO (Windows SetupAPI /
/// Linux <c>/dev/serial/by-id</c>). Pair with <see cref="PrinterProbe"/> to confirm a real NIIMBOT.
/// </summary>
public static class SerialPortEnumerator
{
    /// <summary>All serial port names the OS reports, sorted for stable presentation.</summary>
    public static IReadOnlyList<SerialPortInfo> Enumerate()
    {
        var names = SerialPort.GetPortNames();
        Array.Sort(names, StringComparer.OrdinalIgnoreCase);
        return Array.ConvertAll(names, n => new SerialPortInfo(n));
    }
}
