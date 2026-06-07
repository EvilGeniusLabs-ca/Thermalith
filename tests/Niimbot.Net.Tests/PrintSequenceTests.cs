using Niimbot.Net.Commands;
using Niimbot.Net.Framing;
using Xunit;

namespace Niimbot.Net.Tests;

/// <summary>
/// Byte-level expectations for the B1 print path, derived from niimbluelib + niimprint. These are
/// the tiebreaker tests of spec §5/§10: the expected bytes below are our best synthesis, but the
/// authoritative source is a real-device capture. They are <b>Skipped (PENDING-HARDWARE-VERIFICATION)</b>
/// until a B1 byte capture confirms them — flip the <c>Skip</c> off once verified.
/// </summary>
public class PrintSequenceTests
{
    [Fact(Skip = "PENDING-HARDWARE-VERIFICATION: confirm the B1 accepts the 0x03-prefixed Connect " +
                 "(niimbluelib prefixes it; niimprint sends no Connect at all).")]
    public void Connect_prefix_is_accepted_by_a_real_B1()
    {
        var bytes = PacketGenerator.Connect().ToBytes();
        Assert.Equal("035555C10101C1AAAA", Convert.ToHexString(bytes));
    }

    [Fact(Skip = "PENDING-HARDWARE-VERIFICATION: confirm the B1 print-init byte stream " +
                 "(SetDensity 3, SetLabelType WithGaps, PrintStart 7-byte form) against a capture.")]
    public void B1_print_init_stream_matches_capture()
    {
        var density = PacketGenerator.SetDensity(3).ToBytes();
        var labelType = PacketGenerator.SetLabelType(LabelType.WithGaps).ToBytes();
        var printStart = PacketGenerator.PrintStart(totalPages: 1, pageColor: 0).ToBytes();

        // Derived expectations (XOR checksums computed by hand from the references):
        Assert.Equal("555521010323AAAA", Convert.ToHexString(density));
        Assert.Equal("555523010123AAAA", Convert.ToHexString(labelType));
        // PrintStart 7b payload: totalPages(2)=0001, reserved(4)=00000000, pageColor=00, checksum=07
        Assert.Equal("555501070001000000000007AAAA", Convert.ToHexString(printStart));
    }

    [Fact(Skip = "PENDING-HARDWARE-VERIFICATION: confirm MonochromeBitmap bit order (MSB-first, 1=burn) " +
                 "and row-padding produce correctly-oriented output on a physical B1 label.")]
    public void Monochrome_bit_order_prints_correctly()
    {
        // Verified structurally in RowEncoderTests; the physical orientation/burn check needs paper.
        Assert.True(true);
    }

    [Fact(Skip = "PENDING-HARDWARE-VERIFICATION: confirm the B1 needs the 6-byte SetPageSize " +
                 "(rows, cols, copies) and misprints with the 4-byte form.")]
    public void B1_uses_six_byte_set_page_size()
    {
        var pageSize = PacketGenerator.SetPageSize(rows: 240, cols: 384, copies: 1).ToBytes();
        // rows=00F0, cols=0180, copies=0001, checksum=65
        Assert.Equal("5555130600F00180000165AAAA", Convert.ToHexString(pageSize));
    }
}
