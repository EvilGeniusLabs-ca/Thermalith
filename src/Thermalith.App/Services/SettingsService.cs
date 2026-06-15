using System.Text.Json;

namespace Thermalith.App.Services;

/// <summary>User-writable app settings: window geometry, panel sizes, and the recent-files list (§7).</summary>
public sealed record AppSettings
{
    public List<string> RecentFiles { get; init; } = [];
    public double WindowWidth { get; init; } = 1200;
    public double WindowHeight { get; init; } = 780;
    public double? WindowX { get; init; }
    public double? WindowY { get; init; }
    public bool WindowMaximized { get; init; }
    public double LeftPanelWidth { get; init; } = 200;
    public double RightPanelWidth { get; init; } = 300;

    /// <summary>UI theme: "Default" (follow OS), "Light", or "Dark". Applied at startup + on change.</summary>
    public string Theme { get; init; } = "Default";

    // Last applied canvas (from a roll / printer attach) — seeds a new doc at the real printable size
    // (e.g. 48×30 for a B1) so designs don't start 2mm too wide. Null until a roll has been applied.
    public double? LastCanvasWidthMm { get; init; }
    public double? LastCanvasHeightMm { get; init; }
    public int? LastCanvasDpi { get; init; }
    public string? LastCanvasShape { get; init; }
    public double? LastPrintheadWidthMm { get; init; }
    public double? LastSafeMarginMm { get; init; }

    // Last connected printer — startup background-scans and reconnects if found. Port is a hint (ports
    // re-enumerate); model is the real match.
    public string? LastPrinterPort { get; init; }
    public string? LastPrinterModel { get; init; }
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
