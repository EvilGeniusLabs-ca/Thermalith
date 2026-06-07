using Niimbot.Net.Framing;

namespace Niimbot.Net.Tests.Fakes;

/// <summary>
/// A scripted B1 for the <see cref="FakeTransport"/>: answers the request opcodes the client sends
/// during connect + print with plausible B1 responses, so the whole protocol layer is exercisable
/// end-to-end with no hardware. Byte-level fidelity of the <em>print stream itself</em> is asserted
/// separately and flagged for real-device verification (see PrintSequenceTests).
/// </summary>
public sealed class FakeB1Printer
{
    /// <summary>The model id the fake reports (real B1 = 4096 / 0x1000).</summary>
    public int ModelId { get; init; } = 4096;

    /// <summary>Captured row/empty-row packets streamed during a print.</summary>
    public List<NiimbotPacket> ReceivedRows { get; } = [];

    public IEnumerable<NiimbotPacket> Respond(NiimbotPacket request)
    {
        switch ((RequestCommandId)request.Command)
        {
            case RequestCommandId.Connect:
                return [Resp(ResponseCommandId.In_Connect, 1)];
            case RequestCommandId.PrinterStatusData:
                return [Resp(ResponseCommandId.In_PrinterStatusData, 0)];
            case RequestCommandId.PrinterInfo:
                return [RespondInfo(request)];
            case RequestCommandId.RfidInfo:
                return [Resp(ResponseCommandId.In_RfidInfo, 0)]; // no tag present
            case RequestCommandId.Heartbeat:
                // 13-byte Advanced1 (B1): [9]=lid(0=closed) [10]=charge(4=100%) [11]=paper(0=inserted) [12]=rfidOk
                return [Resp(ResponseCommandId.In_HeartbeatAdvanced1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 4, 0, 1)];
            case RequestCommandId.SetDensity:
                return [Resp(ResponseCommandId.In_SetDensity, 1)];
            case RequestCommandId.SetLabelType:
                return [Resp(ResponseCommandId.In_SetLabelType, 1)];
            case RequestCommandId.PrintStart:
                return [Resp(ResponseCommandId.In_PrintStart, 1)];
            case RequestCommandId.PageStart:
                return [Resp(ResponseCommandId.In_PageStart, 1)];
            case RequestCommandId.SetPageSize:
                return [Resp(ResponseCommandId.In_SetPageSize, 1)];
            case RequestCommandId.PageEnd:
                return [Resp(ResponseCommandId.In_PageEnd, 1)];
            case RequestCommandId.PrintStatus:
                // page=1, pagePrint=100, pageFeed=100 → finished.
                return [Resp(ResponseCommandId.In_PrintStatus, 0x00, 0x01, 100, 100)];
            case RequestCommandId.PrintEnd:
                return [Resp(ResponseCommandId.In_PrintEnd, 1)];
            case RequestCommandId.PrintBitmapRow:
            case RequestCommandId.PrintBitmapRowIndexed:
            case RequestCommandId.PrintEmptyRow:
                ReceivedRows.Add(request);
                return []; // one-way
            default:
                return [];
        }
    }

    private NiimbotPacket RespondInfo(NiimbotPacket request)
    {
        var type = (Commands.PrinterInfoType)request.Data[0];
        return type switch
        {
            Commands.PrinterInfoType.PrinterModelId =>
                Resp(ResponseCommandId.In_PrinterInfoPrinterCode, (byte)((ModelId >> 8) & 0xFF), (byte)(ModelId & 0xFF)),
            Commands.PrinterInfoType.SoftwareVersion =>
                Resp(ResponseCommandId.In_PrinterInfoSoftWareVersion, 0x01, 0x05),
            Commands.PrinterInfoType.SerialNumber =>
                Resp(ResponseCommandId.In_PrinterInfoSerialNumber, "B1TEST01"u8.ToArray()),
            _ => Resp(ResponseCommandId.In_PrinterInfoArea, 0),
        };
    }

    private static NiimbotPacket Resp(ResponseCommandId id, params byte[] data) => new((byte)id, data);
}
