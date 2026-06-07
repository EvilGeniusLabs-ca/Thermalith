using Niimbot.Net;
using Niimbot.Net.Commands;
using Niimbot.Net.Encoding;
using Xunit;

namespace Niimbot.Net.Tests;

/// <summary>
/// Byte-level expectations for the B1 print path, HARDWARE-VERIFIED against a real B1 (firmware
/// 13.02) byte capture on 2026-06-07. The raw capture is archived at
/// <c>docs/captures/b1-cross-print-2026-06-07.txt</c>; these tests assert our encoder reproduces
/// exactly what the device accepted and printed. See build spec §5/§10.
/// </summary>
public class PrintSequenceTests
{
    [Fact] // capture: TX 03 55 55 C1 01 01 C1 AA AA → RX 55 55 C2 01 03 C0 (In_Connect, ConnectedV3)
    public void Connect_prefix_is_accepted_by_a_real_B1()
    {
        var bytes = PacketGenerator.Connect().ToBytes();
        Assert.Equal("035555C10101C1AAAA", Convert.ToHexString(bytes));
    }

    [Fact] // capture lines: SetDensity(5), SetLabelType(WithGaps), PrintStart 7-byte — byte-identical
    public void B1_print_init_stream_matches_capture()
    {
        // 55 55 21 01 05 25 AA AA
        Assert.Equal("555521010525AAAA", Convert.ToHexString(PacketGenerator.SetDensity(5).ToBytes()));
        // 55 55 23 01 01 23 AA AA
        Assert.Equal("555523010123AAAA", Convert.ToHexString(PacketGenerator.SetLabelType(LabelType.WithGaps).ToBytes()));
        // 55 55 01 07 00 01 00 00 00 00 00 07 AA AA  (totalPages=1, reserved, pageColor=0)
        Assert.Equal("555501070001000000000007AAAA", Convert.ToHexString(PacketGenerator.PrintStart(1, 0).ToBytes()));
    }

    [Fact] // capture: TX 55 55 13 06 00 F0 01 80 00 01 65 AA AA  (rows=240, cols=384, copies=1)
    public void B1_uses_six_byte_set_page_size()
    {
        var bytes = PacketGenerator.SetPageSize(rows: 240, cols: 384, copies: 1).ToBytes();
        Assert.Equal("5555130600F00180000165AAAA", Convert.ToHexString(bytes));
    }

    [Fact]
    // The first bitmap-row packet of the captured 320×240 cross print: a full-width top-border run
    // centered on the 384px head. Locks bit order (MSB-first, 1=burn), the 32px centering pad, the
    // split pixel counts (96/128/96), and RLE repeat — all against the on-wire ground truth.
    public void Centered_full_width_row_matches_hardware_capture()
    {
        // The 6px-thick top border: 6 identical all-black rows, 320px wide.
        var border = new MonochromeBitmap(320, 6, BlackRows(320, 6));

        var page = NiimbotClient.PositionOnHead(border, headPx: 384, new PrintOptions { HorizontalAlign = PrintAlignment.Center });
        var rows = RowEncoder.Encode(page, new RowEncoder.Options { PrintheadPixels = 384 });

        var packet = Assert.Single(rows);

        // 55 55 85 36 | pos=0000 | counts=60 80 60 | repeat=06 | 4×00 + 40×FF + 4×00 | cs=35 | AA AA
        var expected = "55558536000060806006"
                       + "00000000"
                       + string.Concat(System.Linq.Enumerable.Repeat("FF", 40))
                       + "00000000"
                       + "35AAAA";
        Assert.Equal(expected, Convert.ToHexString(packet.ToBytes()));
    }

    private static byte[] BlackRows(int widthPx, int heightPx)
    {
        var bytesPerRow = (widthPx + 7) / 8;
        var packed = new byte[bytesPerRow * heightPx];
        Array.Fill(packed, (byte)0xFF);
        return packed;
    }
}
