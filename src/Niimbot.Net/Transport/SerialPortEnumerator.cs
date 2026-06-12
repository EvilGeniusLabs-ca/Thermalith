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
    /// <summary>Candidate serial ports the OS reports, sorted for stable presentation. On macOS,
    /// non-printer pseudo-devices are filtered out (see <see cref="FilterMacPorts"/>).</summary>
    public static IReadOnlyList<SerialPortInfo> Enumerate()
    {
        var names = SerialPort.GetPortNames();
        if (OperatingSystem.IsMacOS())
            names = FilterMacPorts(names);
        Array.Sort(names, StringComparer.OrdinalIgnoreCase);
        return Array.ConvertAll(names, n => new SerialPortInfo(n));
    }

    /// <summary>
    /// macOS exposes every serial device twice — <c>/dev/tty.*</c> (dial-in; <c>Open()</c> can block
    /// waiting for carrier) and <c>/dev/cu.*</c> (call-out; the correct node for talking to a device).
    /// Keep only the <c>cu.*</c> nodes, and drop the built-in pseudo-devices that are never NIIMBOT
    /// printers (the kernel debug console and the inbound-Bluetooth SPP port). The result is the set of
    /// real call-out ports — typically just the printer's <c>cu.usbmodem*</c> when one is attached.
    /// </summary>
    private static string[] FilterMacPorts(string[] names)
    {
        var kept = new List<string>(names.Length);
        foreach (var n in names)
        {
            if (!n.StartsWith("/dev/cu.", StringComparison.Ordinal)) continue;        // drop tty.* dial-in duplicates
            if (n.Contains("debug-console", StringComparison.OrdinalIgnoreCase)) continue;
            if (n.Contains("Bluetooth-Incoming-Port", StringComparison.OrdinalIgnoreCase)) continue;
            kept.Add(n);
        }
        return kept.ToArray();
    }
}
