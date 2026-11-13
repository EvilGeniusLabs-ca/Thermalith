namespace Niimbot.Net.Commands;

/// <summary>Sub-field selector for the <c>PrinterInfo</c> (0x40) query.</summary>
public enum PrinterInfoType : byte
{
    Density = 1,
    Speed = 2,
    LabelType = 3,
    Language = 6,
    AutoShutdownTime = 7,
    PrinterModelId = 8,
    SoftwareVersion = 9,
    BatteryChargeLevel = 10,
    SerialNumber = 11,
    HardwareVersion = 12,
    BluetoothAddress = 13,
    PrintMode = 14,
    Area = 15,
}

/// <summary>Label stock type, set via <c>SetLabelType</c> (0x23).</summary>
public enum LabelType : byte
{
    Invalid = 0,

    /// <summary>Default for most label printers — die-cut labels separated by gaps.</summary>
    WithGaps = 1,
    Black = 2,
    Continuous = 3,
    Perforated = 4,
    Transparent = 5,
    PvcTag = 6,
    BlackMarkGap = 10,
    HeatShrinkTube = 11,
}

/// <summary>Heartbeat request variant; the model/protocol version selects which one to use.</summary>
public enum HeartbeatType : byte
{
    Advanced1 = 1,
    Basic = 2,
    Unknown = 3,
    Advanced2 = 4,
}

/// <summary>Coarse battery charge bucket (0–4 → 0–100%).</summary>
public enum BatteryChargeLevel : byte
{
    Charge0 = 0,
    Charge25 = 1,
    Charge50 = 2,
    Charge75 = 3,
    Charge100 = 4,
}

/// <summary>Auto-shutdown interval bucket; exact minutes vary by model.</summary>
public enum AutoShutdownTime : byte
{
    Minutes15 = 1,
    Minutes30 = 2,
    Minutes45Or60 = 3,
    Minutes60OrNever = 4,
}

/// <summary><c>In_Connect</c> (0xC2) status code.</summary>
public enum ConnectResult : byte
{
    Disconnect = 0,
    Connected = 1,
    ConnectedNew = 2,
    ConnectedV3 = 3,
    FirmwareErrors = 90,
}

/// <summary>
/// <c>In_PrintError</c> (0xDB) / print-status error flag codes. Surfaced via
/// <see cref="Niimbot.Net.PrintException"/> so callers can map a failed print to a cause.
/// Mirrors niimbluelib's <c>PrinterErrorCode</c> (src/packets/enumerations.ts, imported 2026-11-09).
/// </summary>
public enum PrinterErrorCode : byte
{
    CoverOpen = 0x01,
    LackPaper = 0x02,
    LowBattery = 0x03,
    BatteryException = 0x04,
    UserCancel = 0x05,
    DataError = 0x06,
    Overheat = 0x07,
    PaperOutException = 0x08,
    PrinterBusy = 0x09,
    NoPrinterHead = 0x0A,
    TemperatureLow = 0x0B,
    PrinterHeadLoose = 0x0C,
    NoRibbon = 0x0D,
    WrongRibbon = 0x0E,
    UsedRibbon = 0x0F,
    WrongPaper = 0x10,
    SetPaperFail = 0x11,
    SetPrintModeFail = 0x12,
    SetPrintDensityFail = 0x13,
    WriteRfidFail = 0x14,
    SetMarginFail = 0x15,
    CommunicationException = 0x16,
    Disconnect = 0x17,
    CanvasParameterError = 0x18,
    RotationParameterException = 0x19,
    JsonParameterException = 0x1A,
    B3sAbnormalPaperOutput = 0x1B,
    ECheckPaper = 0x1C,
    RfidTagNotWritten = 0x1D,
    SetPrintDensityNoSupport = 0x1E,
    SetPrintModeNoSupport = 0x1F,
    SetPrintLabelMaterialError = 0x20,
    SetPrintLabelMaterialNoSupport = 0x21,
    NotSupportWrittenRfid = 0x22,
    IllegalPage = 0x32,
    IllegalRibbonPage = 0x33,
    ReceiveDataTimeout = 0x34,
    NonDedicatedRibbon = 0x35,
}

/// <summary>Plain-language descriptions of <see cref="PrinterErrorCode"/> for user-facing messages.</summary>
public static class PrinterErrorCodeText
{
    /// <summary>A short plain-language description, or null for a code we don't know.</summary>
    public static string? Describe(byte code) => (PrinterErrorCode)code switch
    {
        PrinterErrorCode.CoverOpen => "the cover is open",
        PrinterErrorCode.LackPaper => "no labels loaded",
        PrinterErrorCode.LowBattery => "battery too low",
        PrinterErrorCode.BatteryException => "battery fault",
        PrinterErrorCode.UserCancel => "cancelled on the printer",
        PrinterErrorCode.DataError => "the printer rejected the print data",
        PrinterErrorCode.Overheat => "the print head is too hot - let it cool",
        PrinterErrorCode.PaperOutException => "label feed problem",
        PrinterErrorCode.PrinterBusy => "the printer is busy",
        PrinterErrorCode.NoPrinterHead => "print head not detected",
        PrinterErrorCode.TemperatureLow => "the printer is too cold",
        PrinterErrorCode.PrinterHeadLoose => "the print head is loose",
        PrinterErrorCode.NoRibbon => "no ribbon loaded",
        PrinterErrorCode.WrongRibbon => "wrong ribbon",
        PrinterErrorCode.UsedRibbon => "the ribbon is used up",
        PrinterErrorCode.WrongPaper => "wrong label type for this printer",
        PrinterErrorCode.SetPaperFail => "couldn't set the label type",
        PrinterErrorCode.SetPrintModeFail => "couldn't set the print mode",
        PrinterErrorCode.SetPrintDensityFail => "couldn't set the print density",
        PrinterErrorCode.WriteRfidFail => "couldn't write to the label roll's RFID tag - the roll may be third-party, refilled, or used up",
        PrinterErrorCode.SetMarginFail => "couldn't set the margins",
        PrinterErrorCode.CommunicationException => "communication error",
        PrinterErrorCode.Disconnect => "the printer disconnected",
        PrinterErrorCode.CanvasParameterError => "the label size was rejected",
        PrinterErrorCode.RotationParameterException => "the rotation setting was rejected",
        PrinterErrorCode.JsonParameterException => "the printer rejected a setting",
        PrinterErrorCode.B3sAbnormalPaperOutput => "abnormal label output",
        PrinterErrorCode.ECheckPaper => "check the labels",
        PrinterErrorCode.RfidTagNotWritten => "the label roll's RFID tag was not written",
        PrinterErrorCode.SetPrintDensityNoSupport => "this printer doesn't support that density",
        PrinterErrorCode.SetPrintModeNoSupport => "this printer doesn't support that print mode",
        PrinterErrorCode.SetPrintLabelMaterialError => "couldn't set the label material",
        PrinterErrorCode.SetPrintLabelMaterialNoSupport => "this printer doesn't support that label material",
        PrinterErrorCode.NotSupportWrittenRfid => "this printer can't write RFID tags",
        PrinterErrorCode.IllegalPage => "the page was rejected",
        PrinterErrorCode.IllegalRibbonPage => "the ribbon page was rejected",
        PrinterErrorCode.ReceiveDataTimeout => "the printer timed out waiting for data",
        PrinterErrorCode.NonDedicatedRibbon => "non-genuine ribbon",
        _ => null,
    };

    /// <summary>
    /// RFID-tag codes that don't stop us printing: the printer couldn't write (or can't write) the
    /// label roll's tag, which happens with third-party, refilled or used-up rolls. A genuine roll
    /// never reports these, and stopping the job protects nothing, so the client logs them and
    /// carries on (GitHub discussion #23). If the printer itself stops, the job still fails.
    /// </summary>
    public static bool IsNonFatal(byte code) => (PrinterErrorCode)code is
        PrinterErrorCode.WriteRfidFail or PrinterErrorCode.RfidTagNotWritten or PrinterErrorCode.NotSupportWrittenRfid;

    /// <summary>"error code 0x14 (couldn't write ...)" or just "error code 0x14" when unknown.</summary>
    public static string Format(byte code) =>
        Describe(code) is { } text ? $"error code 0x{code:X2} ({text})" : $"error code 0x{code:X2}";
}
