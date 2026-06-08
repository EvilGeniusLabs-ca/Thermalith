using Thermalith.Core.Catalog;

namespace Thermalith.App.Services;

/// <summary>
/// Persists the learned label-roll catalogue as <c>labels.json</c> in the app-data dir (worklist §A).
/// Starts empty; grows as the user characterises rolls; remembers the last-used roll. Forgiving:
/// missing/corrupt file → empty catalogue. Mirrors <see cref="SettingsService"/>.
/// </summary>
public sealed class LabelRollStore
{
    private readonly string _path = Path.Combine(PlatformDirectories.AppData(), "labels.json");

    public LabelRollStore() => Catalog = Load();

    /// <summary>The in-memory catalogue (reload on construction; mutated via <see cref="Remember"/>).</summary>
    public LabelRollCatalog Catalog { get; private set; }

    public RollDefinition? LastUsed => Catalog.LastUsed;

    public RollDefinition? FindByBarcode(string? barcode) => Catalog.FindByBarcode(barcode);

    /// <summary>Record/replace a roll definition and set it as last-used; persists immediately.</summary>
    public void Remember(RollDefinition roll)
    {
        Catalog = Catalog.Upsert(roll);
        Save();
    }

    private LabelRollCatalog Load()
    {
        try
        {
            if (File.Exists(_path))
                return LabelRollCatalog.FromJson(File.ReadAllText(_path));
        }
        catch
        {
            // Corrupt store shouldn't block startup — start fresh.
        }
        return new LabelRollCatalog();
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_path, Catalog.ToJson());
        }
        catch
        {
            // Best-effort persistence (read-only profile, etc.).
        }
    }
}
