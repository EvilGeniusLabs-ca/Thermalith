using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Thermalith.Core.Model;
using Thermalith.Core.Serialization;

namespace Thermalith.Core.Catalog;

/// <summary>One catalog entry — a standard label-stock size + shape (build spec §6.1.2).</summary>
public sealed record LabelStock
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required double WidthMm { get; init; }
    public required double HeightMm { get; init; }
    public string Shape { get; init; } = "rectangle"; // rectangle | rounded | circle | dieCut

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }

    /// <summary>Seed a fresh <see cref="Canvas"/> from this stock, taking DPI from the printer profile if known.</summary>
    public Canvas ToCanvas(int dpi = 203) => new()
    {
        WidthMm = WidthMm,
        HeightMm = HeightMm,
        Dpi = dpi,
        Shape = Shape,
    };
}

/// <summary>
/// The label-stock catalog: a static JSON resource of common NIIMBOT sizes (§6.1.2), loadable as
/// a starting point for new labels and user-extensible with custom sizes. Complements RFID
/// auto-detect (§5) for non-RFID rolls.
/// </summary>
public sealed class LabelStockCatalog
{
    private readonly Dictionary<string, LabelStock> _byId;

    public LabelStockCatalog(IEnumerable<LabelStock> stock)
    {
        _byId = new Dictionary<string, LabelStock>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in stock)
            _byId[s.Id] = s;
    }

    public IReadOnlyCollection<LabelStock> All => _byId.Values;

    public LabelStock? Find(string id) => _byId.TryGetValue(id, out var s) ? s : null;

    /// <summary>Add or replace a user-defined stock entry; returns a new catalog (the seed stays immutable).</summary>
    public LabelStockCatalog With(LabelStock custom) => new(_byId.Values.Append(custom));

    /// <summary>The bundled seed catalog (lazily parsed from the embedded JSON resource).</summary>
    public static LabelStockCatalog Default { get; } = LoadBundled();

    private static LabelStockCatalog LoadBundled()
    {
        var asm = typeof(LabelStockCatalog).Assembly;
        const string name = "Thermalith.Core.Catalog.label-stock.json";
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded catalog resource '{name}' not found.");
        var doc = JsonSerializer.Deserialize<CatalogFile>(stream, LabelJson.Options)
            ?? throw new InvalidOperationException("Embedded label-stock catalog failed to parse.");
        return new LabelStockCatalog(doc.Stock ?? []);
    }

    private sealed record CatalogFile
    {
        public List<LabelStock>? Stock { get; init; }
    }
}
