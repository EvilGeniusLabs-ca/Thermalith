using Niimbot.Net.Commands;
using Niimbot.Net.Profiles;

namespace Niimbot.Net.Capabilities;

/// <summary>
/// The hardware-derived setup info surfaced after connect (spec §5): which model answered, its
/// resolution and max print width, density range, supported label types, and — when the model and
/// loaded roll support it — the RFID-reported loaded label. Core reads this to auto-seed
/// <c>canvas.dpi</c> and, where available, <c>canvas.widthMm/heightMm</c>; the user can override.
/// </summary>
public sealed record PrinterCapabilities
{
    public required PrinterModel Model { get; init; }

    /// <summary>The catalogue display name (real name even for models outside the enum).</summary>
    public string ModelName { get; init; } = "Unknown";

    public required int ModelId { get; init; }

    public required int Dpi { get; init; }

    public required double PixelsPerMm { get; init; }

    public required int PrintheadPixels { get; init; }

    public required double MaxPrintWidthMm { get; init; }

    /// <summary>The feed direction the head expects: <see cref="PrintDirection.Left"/> for the small
    /// side-fed D-series (the design must be oriented across the narrow head), else <see cref="PrintDirection.Top"/>.</summary>
    public required PrintDirection PrintDirection { get; init; }

    public required int DensityMin { get; init; }

    public required int DensityMax { get; init; }

    public required int DensityDefault { get; init; }

    public required IReadOnlyList<LabelType> SupportedLabelTypes { get; init; }

    public required bool SupportsRfid { get; init; }

    public string? FirmwareVersion { get; init; }

    public string? SerialNumber { get; init; }

    /// <summary>The loaded RFID-tagged roll, if the device reported one; otherwise null.</summary>
    public RfidInfo? LoadedLabel { get; init; }

    internal static PrinterCapabilities FromProfile(PrinterProfile profile, int modelId) => new()
    {
        Model = profile.Model,
        ModelName = profile.ModelName,
        ModelId = modelId,
        Dpi = profile.Dpi,
        PixelsPerMm = profile.PixelsPerMm,
        PrintheadPixels = profile.PrintheadPixels,
        MaxPrintWidthMm = profile.MaxPrintWidthMm,
        PrintDirection = profile.PrintDirection,
        DensityMin = profile.DensityMin,
        DensityMax = profile.DensityMax,
        DensityDefault = profile.DensityDefault,
        SupportedLabelTypes = profile.SupportedLabelTypes,
        SupportsRfid = profile.SupportsRfid,
    };
}
