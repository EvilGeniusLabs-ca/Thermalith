namespace Niimbot.Net.Framing;

/// <summary>
/// Maps each request opcode to the response opcode(s) the printer may answer with. A
/// <c>null</c> entry marks a one-way command for which no response is expected (bitmap rows,
/// empty rows). The client uses this to correlate a sent packet with its reply. See spec §5.1.
/// </summary>
public static class CommandMap
{
    private static readonly Dictionary<RequestCommandId, ResponseCommandId[]?> Map = new()
    {
        [RequestCommandId.PrintBitmapRow] = null,
        [RequestCommandId.PrintBitmapRowIndexed] = null,
        [RequestCommandId.PrintEmptyRow] = null,
        [RequestCommandId.Connect] = [ResponseCommandId.In_Connect],
        [RequestCommandId.CancelPrint] = [ResponseCommandId.In_CancelPrint],
        [RequestCommandId.CalibrateHeight] = [ResponseCommandId.In_CalibrateHeight],
        [RequestCommandId.Heartbeat] =
        [
            ResponseCommandId.In_HeartbeatBasic,
            ResponseCommandId.In_HeartbeatUnknown,
            ResponseCommandId.In_HeartbeatAdvanced1,
            ResponseCommandId.In_HeartbeatAdvanced2,
        ],
        [RequestCommandId.LabelPositioningCalibration] = [ResponseCommandId.In_LabelPositioningCalibration],
        [RequestCommandId.PageEnd] = [ResponseCommandId.In_PageEnd],
        [RequestCommandId.PrinterLog] = [ResponseCommandId.In_PrinterLog],
        [RequestCommandId.PageStart] = [ResponseCommandId.In_PageStart],
        [RequestCommandId.PrintClear] = [ResponseCommandId.In_PrintClear],
        [RequestCommandId.PrintEnd] = [ResponseCommandId.In_PrintEnd],
        [RequestCommandId.PrinterInfo] =
        [
            ResponseCommandId.In_PrinterInfoArea,
            ResponseCommandId.In_PrinterInfoAutoShutDownTime,
            ResponseCommandId.In_PrinterInfoBluetoothAddress,
            ResponseCommandId.In_PrinterInfoChargeLevel,
            ResponseCommandId.In_PrinterInfoDensity,
            ResponseCommandId.In_PrinterInfoHardWareVersion,
            ResponseCommandId.In_PrinterInfoLabelType,
            ResponseCommandId.In_PrinterInfoLanguage,
            ResponseCommandId.In_PrinterInfoPrinterCode,
            ResponseCommandId.In_PrinterInfoSerialNumber,
            ResponseCommandId.In_PrinterInfoSoftWareVersion,
            ResponseCommandId.In_PrinterInfoSpeed,
        ],
        [RequestCommandId.PrinterConfig] = [ResponseCommandId.In_PrinterConfig],
        [RequestCommandId.PrinterStatusData] = [ResponseCommandId.In_PrinterStatusData],
        [RequestCommandId.PrinterReset] = [ResponseCommandId.In_PrinterReset],
        [RequestCommandId.PrintQuantity] = [ResponseCommandId.In_PrintQuantity],
        [RequestCommandId.PrintStart] = [ResponseCommandId.In_PrintStart],
        [RequestCommandId.PrintStatus] = [ResponseCommandId.In_PrintStatus],
        [RequestCommandId.RfidInfo] = [ResponseCommandId.In_RfidInfo],
        [RequestCommandId.RfidInfo2] = [ResponseCommandId.In_RfidInfo2],
        [RequestCommandId.RfidSuccessTimes] = [ResponseCommandId.In_RfidSuccessTimes],
        [RequestCommandId.SetAutoShutdownTime] = [ResponseCommandId.In_SetAutoShutdownTime],
        [RequestCommandId.SetDensity] = [ResponseCommandId.In_SetDensity],
        [RequestCommandId.SetLabelType] = [ResponseCommandId.In_SetLabelType],
        [RequestCommandId.SetPageSize] = [ResponseCommandId.In_SetPageSize],
        [RequestCommandId.SoundSettings] = [ResponseCommandId.In_SoundSettings],
        [RequestCommandId.AntiFake] = [ResponseCommandId.In_AntiFake],
        [RequestCommandId.WriteRfid] = [ResponseCommandId.In_WriteRfid],
        [RequestCommandId.PrintTestPage] = [ResponseCommandId.In_PrintTestPage],
        [RequestCommandId.PrinterCheckLine] = [ResponseCommandId.In_PrinterCheckLine],
        [RequestCommandId.GetCurrentTimeFormat] = [ResponseCommandId.In_GetCurrentTimeFormat],
    };

    /// <summary>The response opcodes a request may produce, or <c>null</c> if it is one-way.</summary>
    public static ResponseCommandId[]? ResponsesFor(RequestCommandId request) =>
        Map.TryGetValue(request, out var responses) ? responses : null;

    /// <summary>True when the request expects no response.</summary>
    public static bool IsOneWay(RequestCommandId request) =>
        Map.TryGetValue(request, out var responses) && responses is null;
}
