using System.Text.Json;

namespace Thermalith.App.Services;

/// <summary>User-writable app settings: window geometry, panel sizes, and the recent-files list (§7).</summary>
public sealed record AppSettings
{
    public List<string> RecentFiles { get; init; } = [];
    public double WindowWidth { get; init; } = 1200;
    public double WindowHeight { get; init; } = 780;
    public double LeftPanelWidth { get; init; } = 200;
    public double RightPanelWidth { get; init; } = 280;
}

/// <summary>
/// Loads/saves <see cref="AppSettings"/> as JSON in the platform app-data dir (§7). Forgiving: a
/// missing or corrupt file yields defaults rather than throwing. Maintains the MRU list.
/// </summary>
public sealed class SettingsService
{
    private const int MaxRecent = 10;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string _path = Path.Combine(PlatformDirectories.AppData(), "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // Corrupt settings should never block startup — fall back to defaults.
        }
        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch
        {
            // Best-effort persistence; a read-only profile shouldn't crash the editor.
        }
    }

    /// <summary>Promote a path to the front of the MRU list (dedup, capped), returning the updated settings.</summary>
    public static AppSettings WithRecent(AppSettings settings, string path)
    {
        var recent = new List<string> { path };
        recent.AddRange(settings.RecentFiles.Where(p => !string.Equals(p, path, StringComparison.OrdinalIgnoreCase)));
        if (recent.Count > MaxRecent) recent.RemoveRange(MaxRecent, recent.Count - MaxRecent);
        return settings with { RecentFiles = recent };
    }
}
