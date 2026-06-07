using Niimbot.Net.Commands;

namespace Niimbot.Net.Profiles;

/// <summary>
/// The shipped profile library. B1 is the fully-trusted Phase-1 profile (spec §12); the others are
/// seeded from the niimbluelib model table for resolution work and discovery, and should be
/// confirmed against real captures before being relied on for printing.
/// </summary>
public static class PrinterProfiles
{
    /// <summary>The B1 — 203 dpi, 384-px head, ~48 mm max width, reads RFID rolls. Phase-1 target.</summary>
    public static readonly PrinterProfile B1 = new()
    {
        Model = PrinterModel.B1,
        ModelIds = [4096],
        Dpi = 203,
        PrintheadPixels = 384,
        PrintDirection = PrintDirection.Top,
        PrintTaskVersion = PrintTaskVersion.B1,
        DensityMin = 1,
        DensityMax = 5,
        DensityDefault = 3,
        SupportedLabelTypes = [LabelType.WithGaps, LabelType.Black, LabelType.Transparent],
        SupportsRfid = true,
    };

    public static readonly PrinterProfile B1Pro = B1 with
    {
        Model = PrinterModel.B1_Pro,
        ModelIds = [4097],
        Dpi = 300,
        PrintheadPixels = 567,
    };

    public static readonly PrinterProfile B1Se = B1 with
    {
        Model = PrinterModel.B1_SE,
        ModelIds = [4098],
    };

    public static readonly PrinterProfile B21 = B1 with
    {
        Model = PrinterModel.B21,
        ModelIds = [768],
        SupportedLabelTypes = [LabelType.WithGaps, LabelType.Black, LabelType.Continuous, LabelType.Transparent],
    };

    public static readonly PrinterProfile B21S = B21 with
    {
        Model = PrinterModel.B21S,
        ModelIds = [777],
        PrintTaskVersion = PrintTaskVersion.D110,
    };

    public static readonly PrinterProfile D11 = new()
    {
        Model = PrinterModel.D11,
        ModelIds = [512],
        Dpi = 203,
        PrintheadPixels = 96,
        PrintDirection = PrintDirection.Left,
        PrintTaskVersion = PrintTaskVersion.D110,
        DensityMin = 1,
        DensityMax = 3,
        DensityDefault = 2,
        SupportedLabelTypes = [LabelType.WithGaps, LabelType.Transparent],
        SupportsRfid = false,
    };

    public static readonly PrinterProfile D110 = D11 with
    {
        Model = PrinterModel.D110,
        ModelIds = [2304, 2305],
    };

    /// <summary>Generic fallback for an unidentified device — treated like a B1 head, no RFID.</summary>
    public static readonly PrinterProfile Unknown = B1 with
    {
        Model = PrinterModel.Unknown,
        ModelIds = [],
        SupportsRfid = false,
    };

    private static readonly PrinterProfile[] All = [B1, B1Pro, B1Se, B21, B21S, D11, D110];

    /// <summary>Resolve a profile from a reported device model id, or <see cref="Unknown"/>.</summary>
    public static PrinterProfile FromModelId(int modelId) =>
        Array.Find(All, p => p.ModelIds.Contains(modelId)) ?? Unknown with { ModelIds = [modelId] };

    /// <summary>Resolve a profile from a <see cref="PrinterModel"/>, or <see cref="Unknown"/>.</summary>
    public static PrinterProfile ForModel(PrinterModel model) =>
        Array.Find(All, p => p.Model == model) ?? Unknown;
}
