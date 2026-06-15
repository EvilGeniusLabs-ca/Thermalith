using Niimbot.Net.Commands;

namespace Niimbot.Net.Profiles;

/// <summary>The feed direction the printhead expects the raster in.</summary>
public enum PrintDirection
{
    /// <summary>Image consumed top-to-bottom (B1 and most label-roll models).</summary>
    Top,

    /// <summary>Image rotated 90° clockwise before sending (small D-series printers).</summary>
    Left,
}

/// <summary>
/// Selects the model-specific print sequence (the "print-task version" of spec §5/§6.3). Different
/// firmware generations want different <c>PrintStart</c>/<c>SetPageSize</c> payload widths.
/// </summary>
public enum PrintTaskVersion
{
    /// <summary>B1 family: 7-byte PrintStart + 6-byte SetPageSize. The Phase-1 target.</summary>
    B1,

    /// <summary>D110 family: 2-byte PrintStart + 4-byte SetPageSize.</summary>
    D110,
}

/// <summary>
/// Static per-model capabilities the protocol layer needs and that Core reads to seed the canvas
/// (resolution and max print width, spec §5/§6.1). Resolved from the device's reported model id
/// on connect.
/// </summary>
public sealed record PrinterProfile
{
    public required PrinterModel Model { get; init; }

    /// <summary>The catalogue display name (e.g. "B1", "D110", "P1S"). Carries the real name even for
    /// models outside the <see cref="PrinterModel"/> enum, where <see cref="Model"/> is
    /// <see cref="PrinterModel.Unknown"/>.</summary>
    public string ModelName { get; init; } = "Unknown";

    /// <summary>Device model id(s) reported by <c>PrinterInfo(PrinterModelId)</c>.</summary>
    public required IReadOnlyList<int> ModelIds { get; init; }

    /// <summary>Dots per inch (203 or 300 for current models).</summary>
    public required int Dpi { get; init; }

    /// <summary>Printhead width in pixels — also the max raster width.</summary>
    public required int PrintheadPixels { get; init; }

    public required PrintDirection PrintDirection { get; init; }

    public required PrintTaskVersion PrintTaskVersion { get; init; }

    public required int DensityMin { get; init; }

    public required int DensityMax { get; init; }

    public required int DensityDefault { get; init; }

    public required IReadOnlyList<LabelType> SupportedLabelTypes { get; init; }

    /// <summary>Whether this model reads RFID-tagged label rolls (drives auto label setup, spec §5).</summary>
    public required bool SupportsRfid { get; init; }

    /// <summary>True only for models confirmed on real hardware (B1, B4). Everything else is
    /// catalogue-derived-but-unverified — geometry should be right, the print path is best-effort.</summary>
    public bool Verified { get; init; }

    /// <summary>Pixels per millimetre, <c>Dpi / 25.4</c> (e.g. 203 dpi → ~7.992).</summary>
    public double PixelsPerMm => Dpi / 25.4;

    /// <summary>Max printable width in millimetres, derived from <see cref="PrintheadPixels"/>.</summary>
    public double MaxPrintWidthMm => PrintheadPixels / PixelsPerMm;
}
