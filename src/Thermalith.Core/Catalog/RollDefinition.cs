using System.Text.Json;
using System.Text.Json.Serialization;
using Thermalith.Core.Serialization;

namespace Thermalith.Core.Catalog;

/// <summary>
/// A label roll the user has characterised — the learned-catalogue record (worklist §A/§B). The
/// RFID read tells us <i>which</i> roll is loaded but carries no size or part name (verified on real
/// hardware), so the dimensions + paper type come from the user; the RFID fields are opaque match
/// keys. Box fields (printed on the consumable's box) are the human cross-reference. The same shape
/// is what an opt-in community DB would share (build spec §13.1).
/// </summary>
public sealed record RollDefinition
{
    // ── RFID identity (from the printer; opaque keys) ──────────────────────────────────────────
    /// <summary>Per-SKU key candidate (NIIMBOT article/batch code). The catalogue is keyed by this.</summary>
    public string? Barcode { get; init; }
    /// <summary>Per-physical-roll id (changes with every roll).</summary>
    public string? Uuid { get; init; }
    /// <summary>Per-physical-roll serial.</summary>
    public string? Serial { get; init; }
    /// <summary>Paper type the RFID reported (e.g. "WithGaps") — informational cross-check.</summary>
    public string? ConsumablesType { get; init; }

    // ── Box / human fields (what's printed on the box; user-entered) ───────────────────────────
    public string? BoxId { get; init; }      // e.g. "30486"
    public string? PartName { get; init; }   // e.g. "T40*20-320WHITE"

    // ── The roll definition ────────────────────────────────────────────────────────────────────
    public string Name { get; init; } = "";
    public string PaperType { get; init; } = "gap"; // gap | black | continuous | transparent | perforated | …
    public double WidthMm { get; init; }
    public double HeightMm { get; init; }
    public string Shape { get; init; } = "rectangle";
    public int? Density { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>
/// The learned label-roll catalogue (worklist §A). Starts empty and grows as the user characterises
/// rolls. Keyed by RFID <see cref="RollDefinition.Barcode"/>; remembers the last-used roll as the
/// default for new labels and the printer-off case. Persisted as <c>labels.json</c> in the app-data
/// dir by the App (Core stays I/O-agnostic).
/// </summary>
public sealed record LabelRollCatalog
{
    public string SchemaVersion { get; init; } = "1";

    /// <summary>The most recent roll definition used — the default for a new label / when offline. May lack a barcode.</summary>
    public RollDefinition? LastUsed { get; init; }

    /// <summary>Characterised rolls, keyed by <see cref="RollDefinition.Barcode"/> (entries with a barcode only).</summary>
    public List<RollDefinition> Rolls { get; init; } = [];

    public RollDefinition? FindByBarcode(string? barcode) =>
        string.IsNullOrEmpty(barcode) ? null : Rolls.FirstOrDefault(r => string.Equals(r.Barcode, barcode, StringComparison.Ordinal));

    /// <summary>
    /// Record a roll: replace/add by barcode (only entries with a barcode are kept in the keyed list)
    /// and set it as last-used. Returns a new catalogue (records are immutable).
    /// </summary>
    public LabelRollCatalog Upsert(RollDefinition roll)
    {
        var rolls = new List<RollDefinition>(Rolls);
        if (!string.IsNullOrEmpty(roll.Barcode))
        {
            var idx = rolls.FindIndex(r => string.Equals(r.Barcode, roll.Barcode, StringComparison.Ordinal));
            if (idx >= 0) rolls[idx] = roll;
            else rolls.Add(roll);
        }
        return this with { Rolls = rolls, LastUsed = roll };
    }

    public string ToJson() => JsonSerializer.Serialize(this, LabelJson.Options);

    public static LabelRollCatalog FromJson(string json) =>
        JsonSerializer.Deserialize<LabelRollCatalog>(json, LabelJson.Options) ?? new LabelRollCatalog();
}
