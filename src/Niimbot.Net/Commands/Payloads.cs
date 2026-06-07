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
    Disconnect = 0x17,
}
