using System.Net.Http;
using Thermalith.Core.Catalog;

namespace Thermalith.App.Services;

/// <summary>
/// Resolves and refreshes the printer-capability catalog (worklist §A). Load order: per-platform
/// app-data cache → embedded baseline. Update fetches the public NIIMBOT device list, imports it to
/// our format, and writes the cache. The fetch is user-initiated (no automatic cloud calls).
/// </summary>
public sealed class PrinterCatalogService
{
    private string CachePath => Path.Combine(PlatformDirectories.AppData(), "printers.json");

    /// <summary>Load the catalog: app-data cache if present and valid, otherwise the embedded baseline.</summary>
    public PrinterCatalog Load()
    {
        try
        {
            if (File.Exists(CachePath))
                return PrinterCatalog.FromJson(File.ReadAllText(CachePath));
        }
        catch
        {
            // Corrupt cache shouldn't block startup — fall back to the baked-in baseline.
        }
        return PrinterCatalog.LoadEmbedded();
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
