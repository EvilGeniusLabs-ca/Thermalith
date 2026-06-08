using System.Text.Json;

namespace Thermalith.Core.Catalog;

/// <summary>
/// Maps NIIMBOT's raw <c>devices.json</c> into our lean <see cref="PrinterCatalog"/> (worklist §A).
/// Isolating their schema here means a change upstream only breaks at this one boundary, not deep in
/// the app. Derivations match the build spec / observed data: dpi = round(paccuracy × 25.4),
/// printheadPx = round(widthSetEnd × paccuracy), printable width = widthSetEnd.
/// </summary>
public static class PrinterCatalogImporter
{
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

            var paccuracy = Num(d, "paccuracy") ?? 8.0;            // px/mm; 8 → 203 dpi
            var widthSetEnd = Num(d, "widthSetEnd") ?? Num(d, "maxPrintWidth") ?? 0;

            entries.Add(new PrinterEntry
            {
                Model = name,
                Series = Str(d, "seriesName"),
                Ids = Ints(d, "codes"),
                Dpi = (int)Math.Round(paccuracy * 25.4),
                DefaultWidthMm = Num(d, "defaultWidth") ?? 0,
                DefaultHeightMm = Num(d, "defaultHeigth") ?? 0,   // NIIMBOT's spelling
                StockWidthMm = Num(d, "maxPrintWidth") ?? 0,
                MaxHeightMm = Num(d, "maxPrintHeight") ?? 0,
                PrintableWidthMm = widthSetEnd,
                PrintheadPx = (int)Math.Round(widthSetEnd * paccuracy),
                WidthMinMm = Num(d, "widthSetStart") ?? 0,
                WidthMaxMm = widthSetEnd,
                DensityMin = (int)(Num(d, "solubilitySetStart") ?? 1),
                DensityMax = (int)(Num(d, "solubilitySetEnd") ?? 1),
                DensityDefault = (int)(Num(d, "solubilitySetDefault") ?? 1),
                PaperTypes = CsvInts(Str(d, "paperType")),
                RfidType = (int)(Num(d, "rfidType") ?? 0),
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
