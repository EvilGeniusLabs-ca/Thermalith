using System.Text.Json;
using System.Text.Json.Serialization;

namespace Niimbot.Net.Profiles;

/// <summary>
/// One printer's capabilities, distilled from NIIMBOT's public device list (build spec §5/§6.1.2).
/// This is <i>our</i> lean factual format — not a copy of their JSON. Sizes are mm; the printable
/// width is the head limit (their <c>widthSetEnd</c>), distinct from the stock width.
/// </summary>
public sealed record PrinterEntry
{
    public required string Model { get; init; }
    public string? Series { get; init; }

    /// <summary>Protocol model ids reported by the device (NIIMBOT <c>codes</c>) — used to match a connected printer.</summary>
    public IReadOnlyList<int> Ids { get; init; } = [];

    public int Dpi { get; init; }

    public double DefaultWidthMm { get; init; }
    public double DefaultHeightMm { get; init; }

    /// <summary>Physical stock width (their <c>maxPrintWidth</c>) — may exceed the printable width.</summary>
    public double StockWidthMm { get; init; }
    public double MaxHeightMm { get; init; }

    /// <summary>Printable width = head limit (their <c>widthSetEnd</c>). The print guard / safe area use this.</summary>
    public double PrintableWidthMm { get; init; }

    /// <summary>Printhead pixels = <c>round(PrintableWidthMm × pxPerMm)</c> (B1 → 384).</summary>
    public int PrintheadPx { get; init; }

    public double WidthMinMm { get; init; }
    public double WidthMaxMm { get; init; }

    public int DensityMin { get; init; }
    public int DensityMax { get; init; }
    public int DensityDefault { get; init; }

    /// <summary>Accepted paper-type codes (1=Gap, 2=Black, 3=Continuous, 4=Perforated, 5=Transparent, …).</summary>
    public IReadOnlyList<int> PaperTypes { get; init; } = [];

    public int RfidType { get; init; }

    /// <summary>Feed/raster orientation in degrees, mined from NIIMBOT's <c>printDirection</c>
    /// (0/180 → top-fed; 90/270 → the side-fed small D-series). Drives <see cref="PrintDirection"/>.</summary>
    public int PrintDirectionDeg { get; init; }

    /// <summary>True only for models confirmed on real hardware (B1, B4). Everything else is
    /// catalogue-derived-but-unverified. Stamped by the importer from <see cref="KnownPrinterFacts"/>.</summary>
    public bool Verified { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>
/// The printer-capability catalog (build spec §6.1.2 lookup). Lives at the protocol layer so
/// <see cref="PrinterProfiles"/> can derive profiles from it. Shipped as an embedded baseline inside
/// the assembly (and so inside the single-file exe); an optional app-data override is merged over it
/// at startup (worklist §A). Factual data only — no images or template artwork.
/// </summary>
public sealed record PrinterCatalog
{
    /// <summary>Public NIIMBOT source the baseline/refresh is generated from (factual specs, no auth).</summary>
    public const string SourceUrl = "https://oss-print.niimbot.com/public_resources/static_resources/devices.json";

    /// <summary>The canonical serializer settings for the catalog json (camelCase, forgiving read).</summary>
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
    };

    public string SchemaVersion { get; init; } = "1";
    public string? Source { get; init; }
    public string? FetchedUtc { get; init; }
    public List<PrinterEntry> Printers { get; init; } = [];

    /// <summary>Match a connected printer by its reported model id.</summary>
    public PrinterEntry? FindByModelId(int modelId) =>
        Printers.FirstOrDefault(p => p.Ids.Contains(modelId));

    public PrinterEntry? FindByModel(string model) =>
        Printers.FirstOrDefault(p => string.Equals(p.Model, model, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Returns this catalog with <paramref name="overrides"/> layered on top: any override entry that
    /// shares a model id (or, failing that, a model name) replaces the baseline entry; genuinely-new
    /// entries are appended. So an app-data "additions" file can carry just the new models, not the
    /// whole list. Used by the app at startup (embedded baseline ← app-data override).
    /// </summary>
    public PrinterCatalog MergedWith(PrinterCatalog? overrides)
    {
        if (overrides is null || overrides.Printers.Count == 0) return this;

        var merged = new List<PrinterEntry>(Printers);
        foreach (var ov in overrides.Printers)
        {
            var i = merged.FindIndex(p =>
                (ov.Ids.Count > 0 && p.Ids.Any(ov.Ids.Contains)) ||
                string.Equals(p.Model, ov.Model, StringComparison.OrdinalIgnoreCase));
            if (i >= 0) merged[i] = ov;
            else merged.Add(ov);
        }
        merged.Sort((a, b) => string.Compare(a.Model, b.Model, StringComparison.OrdinalIgnoreCase));

        return this with
        {
            Source = overrides.Source ?? Source,
            FetchedUtc = overrides.FetchedUtc ?? FetchedUtc,
            Printers = merged,
        };
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static PrinterCatalog FromJson(string json) =>
        JsonSerializer.Deserialize<PrinterCatalog>(json, JsonOptions)
        ?? throw new InvalidOperationException("printers.json failed to parse.");

    /// <summary>The baseline baked into the assembly (inside the single-file exe).</summary>
    public static PrinterCatalog LoadEmbedded()
    {
        var asm = typeof(PrinterCatalog).Assembly;
        const string name = "Niimbot.Net.Profiles.printers.json";
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded printer catalog '{name}' not found.");
        using var reader = new StreamReader(stream);
        return FromJson(reader.ReadToEnd());
    }
}
