using Niimbot.Net.Framing;

namespace Niimbot.Net.Commands;

/// <summary>
/// Builds request packets for the NIIMBOT command set. Each method returns a fully-framed
/// <see cref="NiimbotPacket"/> (with the correct expected-response opcodes attached). The
/// concrete payload layouts are synthesized from niimbluelib + niimprint and cross-checked
/// where they overlap. See build spec §5.
/// </summary>
public static class PacketGenerator
{
    private static byte[] U16(int n) => [(byte)((n >> 8) & 0xFF), (byte)(n & 0xFF)];

    private static NiimbotPacket Mapped(RequestCommandId cmd, ReadOnlySpan<byte> data) => new(cmd, data);

    private static NiimbotPacket Mapped(RequestCommandId cmd) => new(cmd, stackalloc byte[] { 1 });

    public static NiimbotPacket Connect() => Mapped(RequestCommandId.Connect);

    public static NiimbotPacket GetPrinterStatusData() => Mapped(RequestCommandId.PrinterStatusData);

    public static NiimbotPacket GetPrinterInfo(PrinterInfoType type) =>
        Mapped(RequestCommandId.PrinterInfo, [(byte)type]);

    public static NiimbotPacket Heartbeat(HeartbeatType type) =>
        Mapped(RequestCommandId.Heartbeat, [(byte)type]);

    public static NiimbotPacket RfidInfo() => Mapped(RequestCommandId.RfidInfo);

    public static NiimbotPacket SetDensity(int value) =>
        Mapped(RequestCommandId.SetDensity, [(byte)value]);

    public static NiimbotPacket SetLabelType(LabelType value) =>
        Mapped(RequestCommandId.SetLabelType, [(byte)value]);

    /// <summary>2-byte page size: rows only. The form the D11/D110 print task uses — the D11_H burns
    /// image data with this; the 4-byte rows+cols form prints blank on that firmware.</summary>
    public static NiimbotPacket SetPageSize(int rows) =>
        Mapped(RequestCommandId.SetPageSize, U16(rows));

    /// <summary>
    /// 6-byte page size: rows (height px), cols (width px), copies. This is the form the B1
    /// print task uses — the 4-byte form misbehaves on the B1 (blank/duplicate first page).
    /// </summary>
    public static NiimbotPacket SetPageSize(int rows, int cols, int copies) =>
        Mapped(RequestCommandId.SetPageSize, [.. U16(rows), .. U16(cols), .. U16(copies)]);

    public static NiimbotPacket SetPrintQuantity(int quantity) =>
        Mapped(RequestCommandId.PrintQuantity, U16(quantity));

    /// <summary>13-byte page size: rows, cols, copies, cutHeight (each u16), cutType, reserved, sendAll
    /// (each 1 byte), partHeight (u16). The D110M-v4 form (D11_H) — the copy count rides here rather than
    /// in a separate SetPrintQuantity. Extra fields default to 0. Matches niimbluelib setPageSize13b.</summary>
    public static NiimbotPacket SetPageSize13b(int rows, int cols, int copies,
        int cutHeight = 0, int cutType = 0, int sendAll = 0, int partHeight = 0) =>
        Mapped(RequestCommandId.SetPageSize,
            [.. U16(rows), .. U16(cols), .. U16(copies), .. U16(cutHeight),
             (byte)cutType, 0x00, (byte)sendAll, .. U16(partHeight)]);

    public static NiimbotPacket PrintStatus() => Mapped(RequestCommandId.PrintStatus);

    /// <summary>1-byte print start (generic).</summary>
    public static NiimbotPacket PrintStart() => Mapped(RequestCommandId.PrintStart);

    /// <summary>
    /// 7-byte print start: total pages + reserved + page color. This is the B1's print-task
    /// version — with totalPages &gt; 1 the paper parks at the head between pages.
    /// </summary>
    public static NiimbotPacket PrintStart(int totalPages, int pageColor = 0) =>
        Mapped(RequestCommandId.PrintStart, [.. U16(totalPages), 0x00, 0x00, 0x00, 0x00, (byte)pageColor]);

    /// <summary>9-byte print start: total pages (u16) + 4 reserved + page color + speed + flag. The
    /// D110M-v4 family's start (D11_H, B1_PRO, …) — carries the page count the firmware prints.</summary>
    public static NiimbotPacket PrintStart9b(int totalPages, int pageColor = 0, int speed = 0, bool flag = false) =>
        Mapped(RequestCommandId.PrintStart,
            [.. U16(totalPages), 0x00, 0x00, 0x00, 0x00, (byte)pageColor, (byte)speed, (byte)(flag ? 1 : 0)]);

    public static NiimbotPacket PrintEnd() => Mapped(RequestCommandId.PrintEnd);

    public static NiimbotPacket PageStart() => Mapped(RequestCommandId.PageStart);

    public static NiimbotPacket PageEnd() => Mapped(RequestCommandId.PageEnd);

    public static NiimbotPacket PrintClear() => Mapped(RequestCommandId.PrintClear);

    /// <summary>Skip <paramref name="repeats"/> blank rows starting at <paramref name="position"/>.</summary>
    public static NiimbotPacket PrintEmptyRow(int position, int repeats) =>
        Mapped(RequestCommandId.PrintEmptyRow, [.. U16(position), (byte)repeats]);

    /// <summary>
    /// A run of <paramref name="repeats"/> identical pixel rows at <paramref name="position"/>.
    /// Header is <c>pos(2) | counts(3) | repeats(1)</c> followed by the packed row bytes.
    /// The three count bytes are the per-third black-pixel tally (see
    /// <see cref="Encoding.RowEncoder"/>); the printer tolerates zeros but the real app sends them.
    /// </summary>
    public static NiimbotPacket PrintBitmapRow(int position, int repeats, ReadOnlySpan<byte> rowData, (byte, byte, byte) counts)
    {
        Span<byte> payload = stackalloc byte[2 + 3 + 1 + rowData.Length];
        U16(position).CopyTo(payload);
        payload[2] = counts.Item1;
        payload[3] = counts.Item2;
        payload[4] = counts.Item3;
        payload[5] = (byte)repeats;
        rowData.CopyTo(payload[6..]);
        return Mapped(RequestCommandId.PrintBitmapRow, payload);
    }

    /// <summary>
    /// Compact row form for rows with ≤6 black pixels: instead of the packed bitmap, send the
    /// big-endian pixel index of each black dot. Header matches <see cref="PrintBitmapRow"/>.
    /// </summary>
    public static NiimbotPacket PrintBitmapRowIndexed(int position, int repeats, ReadOnlySpan<int> blackPixelIndices, (byte, byte, byte) counts)
    {
        Span<byte> payload = stackalloc byte[2 + 3 + 1 + blackPixelIndices.Length * 2];
        U16(position).CopyTo(payload);
        payload[2] = counts.Item1;
        payload[3] = counts.Item2;
        payload[4] = counts.Item3;
        payload[5] = (byte)repeats;
        var pos = 6;
        foreach (var idx in blackPixelIndices)
        {
            payload[pos++] = (byte)((idx >> 8) & 0xFF);
            payload[pos++] = (byte)(idx & 0xFF);
        }
        return Mapped(RequestCommandId.PrintBitmapRowIndexed, payload);
    }

    public static NiimbotPacket PrintTestPage() => Mapped(RequestCommandId.PrintTestPage);

    public static NiimbotPacket LabelPositioningCalibration(int value) =>
        Mapped(RequestCommandId.LabelPositioningCalibration, [(byte)value]);
}
