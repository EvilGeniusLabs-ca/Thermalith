namespace Niimbot.Net.Framing;

/// <summary>
/// Command IDs the client sends to the printer (the opcode byte in a request packet).
/// Synthesized from the niimbluelib + niimprint command tables; values are device opcodes,
/// not arbitrary. See build spec §5.
/// </summary>
public enum RequestCommandId : byte
{
    /// <summary>The whole packet is additionally prefixed with a single <c>0x03</c> byte on the wire.</summary>
    Connect = 0xC1,
    CancelPrint = 0xDA,
    CalibrateHeight = 0x59,
    Heartbeat = 0xDC,
    LabelPositioningCalibration = 0x8E,
    PageEnd = 0xE3,
    PrinterLog = 0x05,
    PageStart = 0x03,
    PrintBitmapRow = 0x85,

    /// <summary>Compact row form sent when a row has 6 or fewer black pixels.</summary>
    PrintBitmapRowIndexed = 0x83,
    PrintClear = 0x20,
    PrintEmptyRow = 0x84,
    PrintEnd = 0xF3,
    PrinterInfo = 0x40,
    PrinterConfig = 0xAF,
    PrinterStatusData = 0xA5,
    PrinterReset = 0x28,
    PrintQuantity = 0x15,
    PrintStart = 0x01,
    PrintStatus = 0xA3,
    RfidInfo = 0x1A,
    RfidInfo2 = 0x1C,
    RfidSuccessTimes = 0x54,
    SetAutoShutdownTime = 0x27,
    SetDensity = 0x21,
    SetLabelType = 0x23,

    /// <summary>2, 4, 6, or 13 byte payload depending on model/print-task version.</summary>
    SetPageSize = 0x13,
    SoundSettings = 0x58,
    AntiFake = 0x0B,
    WriteRfid = 0x70,
    PrintTestPage = 0x5A,
    PrinterCheckLine = 0x86,
    GetCurrentTimeFormat = 0x12,
}

/// <summary>
/// Command IDs the printer sends back to the client. Most are simply the request opcode
/// plus a fixed offset, but the device is authoritative, so the full table is enumerated.
/// </summary>
public enum ResponseCommandId : byte
{
    In_NotSupported = 0x00,
    In_Connect = 0xC2,
    In_CalibrateHeight = 0x69,
    In_CancelPrint = 0xD0,
    In_AntiFake = 0x0C,
    In_HeartbeatAdvanced1 = 0xDD,
    In_HeartbeatBasic = 0xDE,
    In_HeartbeatUnknown = 0xDF,
    In_HeartbeatAdvanced2 = 0xD9,
    In_LabelPositioningCalibration = 0x8F,
    In_PageStart = 0x04,
    In_PrintClear = 0x30,
    In_PrinterCheckLine = 0xD3,
    In_PrintEnd = 0xF4,
    In_PrinterConfig = 0xBF,
    In_PrinterLog = 0x06,
    In_PrinterInfoAutoShutDownTime = 0x47,
    In_PrinterInfoBluetoothAddress = 0x4D,
    In_PrinterInfoSpeed = 0x42,
    In_PrinterInfoDensity = 0x41,
    In_PrinterInfoLanguage = 0x46,
    In_PrinterInfoChargeLevel = 0x4A,
    In_PrinterInfoHardWareVersion = 0x4C,
    In_PrinterInfoLabelType = 0x43,
    In_PrinterInfoPrinterCode = 0x48,
    In_PrinterInfoSerialNumber = 0x4B,
    In_PrinterInfoSoftWareVersion = 0x49,
    In_PrinterInfoArea = 0x4F,
    In_PrinterStatusData = 0xB5,
    In_PrinterReset = 0x38,
    In_PrintStatus = 0xB3,

    /// <summary>For example, returned after <see cref="RequestCommandId.SetPageSize"/> when no page print is started.</summary>
    In_PrintError = 0xDB,
    In_PrintQuantity = 0x16,
    In_PrintStart = 0x02,
    In_RfidInfo = 0x1B,
    In_RfidInfo2 = 0x1D,
    In_RfidSuccessTimes = 0x64,
    In_SetAutoShutdownTime = 0x37,
    In_SetDensity = 0x31,
    In_SetLabelType = 0x33,
    In_SetPageSize = 0x14,
    In_SoundSettings = 0x68,
    In_PageEnd = 0xE4,
    In_PrinterPageIndex = 0xE0,
    In_PrintTestPage = 0x6A,
    In_WriteRfid = 0x71,
    In_ResetTimeout = 0xC6,
    In_GetCurrentTimeFormat = 0x11,
}
