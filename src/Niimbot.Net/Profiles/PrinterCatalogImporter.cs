using System.Text.Json;

namespace Niimbot.Net.Profiles;

/// <summary>
/// Maps NIIMBOT's raw <c>devices.json</c> into our lean <see cref="PrinterCatalog"/> (worklist §A).
/// Isolating their schema here means a change upstream only breaks at this one boundary, not deep in
/// the app.
///
/// <para><b>dpi comes from <c>paccuracyName</c>, NOT <c>paccuracy</c>.</b> They are different fields:
/// <c>paccuracyName</c> is the real dpi ("203" or "300" — the only two values NIIMBOT ships), while
/// <c>paccuracy</c> is an internal code (8 or 9). Treating <c>paccuracy</c> as px/mm and scaling by
/// 25.4 invents a phantom 229 dpi for every 300 dpi printer (9 × 25.4 = 228.6) and undersizes its
/// printhead, so labels print at ~76% of their design size. That bug shipped in 1.1.0 and was
/// reported against the B21 Pro (GitHub #17); the D11_H hit the same thing on hardware (2026-06-17).
/// Pixels-per-mm is a lookup off the dpi, matching niimbluelib's <c>gen-printer-models.js</c>:
/// printheadPx = ceil(widthSetEnd × ppmm), printable width = widthSetEnd.</para>
/// </summary>
public static class PrinterCatalogImporter
{
    /// <summary>Pixels per mm by dpi. 11.81 (not 203/25.4-style exactness) is the value NIIMBOT's own
    /// tooling and niimbluelib use; it reproduces every published printhead width exactly.</summary>
    private static double PixelsPerMm(int dpi) => dpi == 300 ? 11.81 : 8.0;

    public static PrinterCatalog Import(string rawDevicesJson, string? fetchedUtc = null)
    {
        using var doc = JsonDocument.Parse(rawDevicesJson, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var array = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : (doc.RootElement.TryGetProperty("data", out var data) ? data
               : doc.RootElement.TryGetProperty("list", out var list) ? list
               : doc.RootElement);

        var entries = new List<PrinterEntry>();
        foreach (var d in array.EnumerateArray())
        {
            var name = Str(d, "name");
            if (string.IsNullOrWhiteSpace(name)) continue;

            // paccuracyName is the dpi ("203"/"300"); paccuracy (8/9) is only a fallback signal.
            var dpi = (int?)Num(d, "paccuracyName") ?? (Num(d, "paccuracy") >= 9 ? 300 : 203);
            var ppmm = PixelsPerMm(dpi);
            var widthSetEnd = Num(d, "widthSetEnd") ?? Num(d, "maxPrintWidth") ?? 0;
            var ids = Ints(d, "codes");

            entries.Add(new PrinterEntry
            {
                Model = name,
                Series = Str(d, "seriesName"),
                Ids = ids,
                Dpi = dpi,
                DefaultWidthMm = Num(d, "defaultWidth") ?? 0,
                DefaultHeightMm = Num(d, "defaultHeigth") ?? 0,   // NIIMBOT's spelling
                StockWidthMm = Num(d, "maxPrintWidth") ?? 0,
                MaxHeightMm = Num(d, "maxPrintHeight") ?? 0,
                PrintableWidthMm = widthSetEnd,
                PrintheadPx = (int)Math.Ceiling(widthSetEnd * ppmm),
                WidthMinMm = Num(d, "widthSetStart") ?? 0,
                WidthMaxMm = widthSetEnd,
                DensityMin = (int)(Num(d, "solubilitySetStart") ?? 1),
                DensityMax = (int)(Num(d, "solubilitySetEnd") ?? 1),
                DensityDefault = (int)(Num(d, "solubilitySetDefault") ?? 1),
                PaperTypes = CsvInts(Str(d, "paperType")),
                RfidType = (int)(Num(d, "rfidType") ?? 0),
                PrintDirectionDeg = (int)(Num(d, "printDirection") ?? 0),
                Verified = KnownPrinterFacts.IsVerified(ids),
            });
        }

        entries.Sort((a, b) => string.Compare(a.Model, b.Model, StringComparison.OrdinalIgnoreCase));

        return new PrinterCatalog
        {
            Source = PrinterCatalog.SourceUrl,
            FetchedUtc = fetchedUtc,
            Printers = entries,
        };
    }

    // ── tolerant field readers (NIIMBOT mixes numbers and numeric strings) ──────────────────────

    private static string? Str(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static double? Num(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetDouble(),
            JsonValueKind.String when double.TryParse(v.GetString(), out var d) => d,
            _ => null,
        };
    }

    private static IReadOnlyList<int> Ints(JsonElement obj, string name)
    {
        if (!obj.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Array) return [];
        var result = new List<int>();
        foreach (var e in v.EnumerateArray())
        {
            if (e.ValueKind == JsonValueKind.Number && e.TryGetInt32(out var i)) result.Add(i);
            else if (e.ValueKind == JsonValueKind.String && int.TryParse(e.GetString(), out var j)) result.Add(j);
        }
        return result;
    }

    private static IReadOnlyList<int> CsvInts(string? csv)
    {
        if (string.IsNullOrWhiteSpace(csv)) return [];
        var result = new List<int>();
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (int.TryParse(part, out var i)) result.Add(i);
        return result;
    }
}
