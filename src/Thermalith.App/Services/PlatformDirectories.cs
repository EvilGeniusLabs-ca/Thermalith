namespace Thermalith.App.Services;

/// <summary>
/// Resolves the per-platform, user-writable app-data directory for runtime settings + the MRU list
/// (build spec §7). This is the runtime store, distinct from any deploy-time appsettings.json.
/// </summary>
public static class PlatformDirectories
{
    private const string AppFolder = "Thermalith";

    /// <summary>The Thermalith app-data directory, created if missing.</summary>
    public static string AppData()
    {
        var dir = Path.Combine(BaseDir(), AppFolder);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string BaseDir()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData); // %APPDATA% (Roaming)
        if (OperatingSystem.IsMacOS())
            return Path.Combine(home, "Library", "Application Support");
        return Environment.GetEnvironmentVariable("XDG_CONFIG_HOME") is { Length: > 0 } xdg
            ? xdg
            : Path.Combine(home, ".config");
    }
}
