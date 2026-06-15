using System.Net.Http;
using Niimbot.Net.Profiles;

namespace Thermalith.App.Services;

/// <summary>
/// Resolves and refreshes the printer-capability catalog (worklist §A). The active catalog is the
/// embedded baseline (inside the single-file exe) <b>merged with</b> an optional app-data override —
/// so a dropped-in "additions" file can carry just new models, and the app still runs fully without
/// one. Update fetches the public NIIMBOT device list, imports it to our format, and writes the
/// cache. The fetch is user-initiated (no automatic cloud calls).
/// </summary>
public sealed class PrinterCatalogService
{
    private string CachePath => Path.Combine(PlatformDirectories.AppData(), "printers.json");

    /// <summary>
    /// Load the active catalog: the embedded baseline merged with the app-data override (override
    /// wins per model). Also pushes it into <see cref="PrinterProfiles"/> so profile resolution at the
    /// protocol layer sees the same data. Call once at startup.
    /// </summary>
    public PrinterCatalog Load()
    {
        var catalog = PrinterCatalog.LoadEmbedded();
        try
        {
            if (File.Exists(CachePath))
                catalog = catalog.MergedWith(PrinterCatalog.FromJson(File.ReadAllText(CachePath)));
        }
        catch
        {
            // Corrupt override shouldn't block startup — fall back to the baked-in baseline.
        }
        PrinterProfiles.UseCatalog(catalog);
        return catalog;
    }

    /// <summary>
    /// Fetch the latest device list, import to our format, and write it. Defaults to the app-data
    /// cache; pass <paramref name="outPath"/> to write the committed baseline (the --update-catalog tool).
    /// </summary>
    public async Task<PrinterCatalog> UpdateAsync(string? outPath = null, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        var raw = await http.GetStringAsync(PrinterCatalog.SourceUrl, ct).ConfigureAwait(false);
        var catalog = PrinterCatalogImporter.Import(raw, DateTimeOffset.UtcNow.ToString("o"));

        var path = outPath ?? CachePath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, catalog.ToJson(), ct).ConfigureAwait(false);
        return catalog;
    }
}
