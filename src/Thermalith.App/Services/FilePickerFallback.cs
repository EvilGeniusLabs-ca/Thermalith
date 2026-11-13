using Niimbot.Net.Diagnostics;

namespace Thermalith.App.Services;

/// <summary>
/// Linux file-dialog safety net (GitHub #19). On Linux, Avalonia asks the desktop's
/// xdg-desktop-portal for file dialogs over D-Bus. Some setups have a portal that is present but
/// refuses the request (e.g. Gentoo: "AccessDenied: Portal operation not allowed: Unable to open
/// /proc/&lt;pid&gt;/root"). Avalonia only falls back to its GTK or built-in dialog when the portal
/// is missing, not when it fails, so the exception reached the dispatcher and killed the app.
///
/// Fix: when a picker call fails, a marker file is written and the next launch starts with the
/// portal turned off (<see cref="UsePortal"/>), so Avalonia uses its own dialog instead. The
/// provider is chosen once at startup, which is why the switch needs a restart.
/// <c>THERMALITH_NO_PORTAL=1</c> forces the same behaviour without the marker.
/// </summary>
public static class FilePickerFallback
{
    private static string MarkerPath => Path.Combine(PlatformDirectories.AppData(), "portal-file-dialog-disabled");

    /// <summary>Whether Avalonia should try the D-Bus portal for file dialogs this session.</summary>
    public static bool UsePortal =>
        !(Environment.GetEnvironmentVariable("THERMALITH_NO_PORTAL") is "1" or "true") &&
        !SafeExists(MarkerPath);

    /// <summary>Remember that the portal failed so the next launch skips it. Best-effort.</summary>
    public static void DisablePortal(Exception cause)
    {
        NiimbotTrace.Log("dialog", $"file dialog failed: {cause.GetType().Name}: {cause.Message}");
        try
        {
            Directory.CreateDirectory(PlatformDirectories.AppData());
            File.WriteAllText(MarkerPath,
                $"Written {DateTime.Now:yyyy-MM-dd HH:mm:ss} after the desktop file dialog failed:{Environment.NewLine}" +
                $"{cause.GetType().FullName}: {cause.Message}{Environment.NewLine}" +
                "Delete this file to try the desktop (portal) file dialog again.");
        }
        catch
        {
            // Config folder not writable: the user still gets the message; nothing else to do.
        }
    }

    private static bool SafeExists(string path)
    {
        try { return File.Exists(path); }
        catch { return false; }
    }
}
