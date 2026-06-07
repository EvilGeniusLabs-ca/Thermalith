namespace Niimbot.Net.Commands;

/// <summary>Per-page print progress, polled via <c>PrintStatus</c> (0xA3) during a print.</summary>
public readonly record struct PrintStatus(int Page, int PagePrintProgress, int PageFeedProgress);

/// <summary>
/// Raw device status data from <c>PrinterStatusData</c> (0xA5): whether the printer supports
/// multicolor paper and which protocol generation it speaks (drives print-task selection).
/// </summary>
public readonly record struct PrinterStatusData(int SupportColor, int ProtocolVersion);

/// <summary>
/// Heartbeat snapshot — the live "is it ready to print" state. Fields are optional because the
/// set the device reports varies by model and protocol version. This is the source for the
/// paper / cover / battery readout exposed on the client.
/// </summary>
public readonly record struct HeartbeatData
{
    public bool? PaperInserted { get; init; }
    public bool? PaperRfidSuccess { get; init; }
    public bool? LidClosed { get; init; }
    public BatteryChargeLevel? ChargeLevel { get; init; }
    public int? Temperature { get; init; }
    public bool? RibbonInserted { get; init; }
    public bool? RibbonRfidSuccess { get; init; }
}

/// <summary>
/// Decoded contents of an RFID-tagged label roll (<c>RfidInfo</c> 0x1A). The B1 reads these tags,
/// so this is how Core auto-seeds canvas dimensions for a loaded roll (spec §5). When no tag is
/// present, <see cref="TagPresent"/> is false and the remaining fields are unset.
/// </summary>
public readonly record struct RfidInfo
{
    public bool TagPresent { get; init; }
    public string Uuid { get; init; }
    public string Barcode { get; init; }
    public string SerialNumber { get; init; }

    /// <summary>Total labels on the roll, or -1 if unknown.</summary>
    public int TotalLabels { get; init; }

    /// <summary>Labels already consumed, or -1 if unknown.</summary>
    public int UsedLabels { get; init; }

    public LabelType ConsumablesType { get; init; }

    public static RfidInfo Empty => new()
    {
        TagPresent = false,
        Uuid = "",
        Barcode = "",
        SerialNumber = "",
        TotalLabels = -1,
        UsedLabels = -1,
        ConsumablesType = LabelType.Invalid,
    };
}
