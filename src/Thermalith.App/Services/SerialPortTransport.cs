using System.Runtime.Versioning;
using Microsoft.Win32;

namespace Thermalith.App.Services;

/// <summary>
/// Best-effort transport label ("USB" / "Bluetooth") for a serial port, for the connection dropdown.
/// Returns <c>null</c> when it can't classify — the caller just omits the tag. Purely cosmetic.
///
/// On Windows the COM name is opaque, so it's read from the SERIALCOMM device map (the value name is
/// the device path, e.g. <c>\Device\USBSER000</c> / <c>\Device\BthModem0</c>). On Linux/macOS the
/// device-node name already encodes the bus, so a simple name check suffices.
/// </summary>
public static class SerialPortTransport
{
    public static string? Describe(string portName)
    {
        if (OperatingSystem.IsWindows())
            return DescribeWindows(portName);

        // Linux & macOS: the node name encodes the bus.
        var p = portName.ToLowerInvariant();
        if (p.Contains("rfcomm") || p.Contains("bluetooth")) return "Bluetooth";
        if (p.Contains("ttyusb") || p.Contains("ttyacm")        // Linux USB-serial / USB-CDC
            || p.Contains("usbmodem") || p.Contains("usbserial")) // macOS USB
            return "USB";
        return null;
    }

    [SupportedOSPlatform("windows")]
    private static string? DescribeWindows(string portName)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DEVICEMAP\SERIALCOMM");
            if (key is null) return null;
            foreach (var devicePath in key.GetValueNames())
            {
                if (key.GetValue(devicePath) as string != portName) continue;
                var d = devicePath.ToLowerInvariant();
                if (d.Contains("bthmodem") || d.Contains("bluetooth")) return "Bluetooth";
                if (d.Contains("usbser") || d.Contains("silabser") || d.Contains("prolific")
                    || d.Contains("vcp") || d.Contains("usb")) return "USB";
                return null;
            }
        }
        catch { /* registry unreadable — just skip the tag */ }
        return null;
    }
}
