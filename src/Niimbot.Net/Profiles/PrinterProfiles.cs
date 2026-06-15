using Niimbot.Net.Commands;

namespace Niimbot.Net.Profiles;

/// <summary>
/// Resolves <see cref="PrinterProfile"/>s for connected printers. Profiles are <b>derived from the
/// printer catalogue</b> (<see cref="PrinterCatalog"/>) rather than hand-maintained: geometry, density,
/// label types and RFID come straight from the mined data, feed direction from the catalogue's
/// <c>printDirection</c>, and only the print-task version + verified flag come from
/// <see cref="KnownPrinterFacts"/> (the two facts NIIMBOT's data doesn't carry, worklist §A).
///
/// The catalogue defaults to the embedded baseline so the library works standalone (as a NuGet); a
/// host app calls <see cref="UseCatalog"/> at startup to swap in the merged embedded+app-data catalog.
/// </summary>
public static class PrinterProfiles
{
    private static PrinterCatalog _catalog = PrinterCatalog.LoadEmbedded();

    /// <summary>Replace the active catalogue (e.g. the app's merged embedded + app-data override).</summary>
    public static void UseCatalog(PrinterCatalog catalog) => _catalog = catalog;

    /// <summary>The active catalogue profiles are derived from.</summary>
    public static PrinterCatalog Catalog => _catalog;

    /// <summary>The B1 — 203 dpi, 384-px head, ~48 mm, reads RFID. The verified Phase-1 reference (spec §12).</summary>
    public static PrinterProfile B1 => FromModelId(4096);

    /// <summary>The B4 — 4" shipping printer, 203 dpi, 832-px head (~104 mm). Verified 2026-06-14.</summary>
    public static PrinterProfile B4 => FromModelId(6656);

    /// <summary>Generic fallback for an unidentified device — treated like a B1 head, no RFID.</summary>
    public static readonly PrinterProfile Unknown = new()
    {
        Model = PrinterModel.Unknown,
        ModelName = "Unknown",
        ModelIds = [],
        Dpi = 203,
        PrintheadPixels = 384,
        PrintDirection = PrintDirection.Top,
        PrintTaskVersion = PrintTaskVersion.B1,
        DensityMin = 1,
        DensityMax = 5,
        DensityDefault = 3,
        SupportedLabelTypes = [LabelType.WithGaps, LabelType.Black, LabelType.Transparent],
        SupportsRfid = false,
        Verified = false,
    };

    /// <summary>Resolve a profile from a reported device model id, or a generic fallback that keeps the id.</summary>
    public static PrinterProfile FromModelId(int modelId)
    {
        var entry = _catalog.FindByModelId(modelId);
        return entry is null
            ? Unknown with { ModelIds = [modelId] }
            : FromEntry(entry);
    }

    /// <summary>Resolve a profile for a known <see cref="PrinterModel"/>, or <see cref="Unknown"/>.</summary>
    public static PrinterProfile ForModel(PrinterModel model)
    {
        if (model == PrinterModel.Unknown) return Unknown;
        var entry = _catalog.Printers.FirstOrDefault(p => ParseModel(p.Model) == model);
        return entry is null ? Unknown : FromEntry(entry);
    }

    /// <summary>Project a catalogue entry into a print-capable profile.</summary>
    private static PrinterProfile FromEntry(PrinterEntry e) => new()
    {
        Model = ParseModel(e.Model),
        ModelName = e.Model,
        ModelIds = e.Ids.Count > 0 ? e.Ids : [],
        Dpi = e.Dpi,
        PrintheadPixels = e.PrintheadPx,
        PrintDirection = e.PrintDirectionDeg is 90 or 270 ? PrintDirection.Left : PrintDirection.Top,
        PrintTaskVersion = KnownPrinterFacts.UsesD110PrintTask(e.Ids) ? PrintTaskVersion.D110 : PrintTaskVersion.B1,
        DensityMin = e.DensityMin,
        DensityMax = e.DensityMax,
        DensityDefault = e.DensityDefault,
        SupportedLabelTypes = MapLabelTypes(e.PaperTypes),
        SupportsRfid = e.RfidType != 0,
        Verified = e.Verified || KnownPrinterFacts.IsVerified(e.Ids),
    };

    /// <summary>Catalogue paper-type codes → <see cref="LabelType"/>, dropping any code we don't model.</summary>
    private static IReadOnlyList<LabelType> MapLabelTypes(IReadOnlyList<int> paperTypes)
    {
        var types = new List<LabelType>(paperTypes.Count);
        foreach (var code in paperTypes)
            if (Enum.IsDefined(typeof(LabelType), (byte)code))
            {
                var t = (LabelType)(byte)code;
                if (t != LabelType.Invalid && !types.Contains(t)) types.Add(t);
            }
        return types.Count > 0 ? types : [LabelType.WithGaps];
    }

    /// <summary>Map a catalogue model name to the small <see cref="PrinterModel"/> enum (Unknown if unlisted).</summary>
    private static PrinterModel ParseModel(string model)
    {
        var normalized = model.Replace(' ', '_').Replace('-', '_');
        return Enum.TryParse<PrinterModel>(normalized, ignoreCase: true, out var m) ? m : PrinterModel.Unknown;
    }
}
