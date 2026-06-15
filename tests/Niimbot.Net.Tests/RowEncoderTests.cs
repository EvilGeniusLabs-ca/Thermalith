using Niimbot.Net.Commands;
using Niimbot.Net.Encoding;
using Niimbot.Net.Framing;
using Xunit;

namespace Niimbot.Net.Tests;

public class RowEncoderTests
{
    [Fact]
    public void IndexPixels_lists_msb_first_indices()
    {
        // 0x81 = 1000_0001 → leftmost (index 0) and rightmost (index 7) bits set.
        Assert.Equal([0, 7], RowEncoder.IndexPixels([0x81]));
    }

    [Fact]
    public void CountPixels_total_mode_encodes_low_high()
    {
        // Three set bits; total mode → [0, low, high].
        var counts = RowEncoder.CountPixels([0b1110_0000], printheadPixels: 0, PixelCountMode.Total);
        Assert.Equal(3, counts.Total);
        Assert.Equal(((byte)0, (byte)3, (byte)0), counts.Parts);
    }

    [Fact]
    public void CountPixels_split_mode_tallies_each_third()
    {
        // 384px head → chunkSize = 384/8/3 = 16 bytes per third. Put a bit in each third.
        var row = new byte[48];
        row[0] = 0x80;  // third 0
        row[20] = 0x80; // third 1
        row[40] = 0x80; // third 2
        var counts = RowEncoder.CountPixels(row, printheadPixels: 384, PixelCountMode.Split);
        Assert.Equal(((byte)1, (byte)1, (byte)1), counts.Parts);
        Assert.Equal(3, counts.Total);
    }

    [Fact]
    public void Encode_collapses_identical_blank_rows_into_one_empty_packet()
    {
        var bitmap = new MonochromeBitmap(8, 3, new byte[3]); // all blank
        var packets = RowEncoder.Encode(bitmap, new RowEncoder.Options { PrintheadPixels = 384 });

        var packet = Assert.Single(packets);
        Assert.Equal((byte)RequestCommandId.PrintEmptyRow, packet.Command);
        // pos(2)=0, repeat=3
        Assert.Equal([0x00, 0x00, 0x03], packet.Data);
    }

    [Fact]
    public void Encode_uses_indexed_packet_for_sparse_rows()
    {
        var bitmap = new MonochromeBitmap(8, 1, [0x80]); // single black pixel
        var packets = RowEncoder.Encode(bitmap, new RowEncoder.Options { PrintheadPixels = 384 });

        var packet = Assert.Single(packets);
        Assert.Equal((byte)RequestCommandId.PrintBitmapRowIndexed, packet.Command);
    }

    [Fact]
    public void Encode_uses_full_bitmap_packet_for_dense_rows()
    {
        var bitmap = new MonochromeBitmap(8, 1, [0xFF]); // 8 black pixels (> 6)
        var packets = RowEncoder.Encode(bitmap, new RowEncoder.Options { PrintheadPixels = 384 });

        var packet = Assert.Single(packets);
        Assert.Equal((byte)RequestCommandId.PrintBitmapRow, packet.Command);
        // pos(2) | counts(3) | repeat(1) | data(1)
        Assert.Equal([0x00, 0x00, 0x08, 0x00, 0x00, 0x01, 0xFF], packet.Data);
    }

    [Fact]
    public void Encode_splits_blank_runs_longer_than_255_so_repeat_never_overflows_the_byte()
    {
        // A tall, fully-blank label (601 rows) — the B4 "only the top prints" case. The repeat field
        // is one byte, so a 601-row run must split into 255 + 255 + 91 at advancing positions.
        var bitmap = new MonochromeBitmap(8, 601, new byte[601]);
        var packets = RowEncoder.Encode(bitmap, new RowEncoder.Options { PrintheadPixels = 384 });

        Assert.Equal(3, packets.Count);
        // Each packet's repeat byte (3rd payload byte) must be ≤ 255, and positions advance by the chunk.
        Assert.Equal([0x00, 0x00, 0xFF], packets[0].Data);       // pos 0,   repeat 255
        Assert.Equal([0x00, 0xFF, 0xFF], packets[1].Data);       // pos 255, repeat 255
        Assert.Equal([0x01, 0xFE, 0x5B], packets[2].Data);       // pos 510, repeat 91
    }

    [Fact]
    public void Encode_splits_long_identical_content_runs_into_le255_chunks()
    {
        // 600 identical dense rows (a vertical border line) → must not emit a single repeat=600 packet.
        var packed = new byte[600];
        for (var y = 0; y < 600; y++) packed[y] = 0xFF;
        var bitmap = new MonochromeBitmap(8, 600, packed);
        var packets = RowEncoder.Encode(bitmap, new RowEncoder.Options { PrintheadPixels = 384 });

        // 600 → 255 + 255 + 90, three packets whose repeat bytes cover every row exactly once.
        Assert.Equal(3, packets.Count);
        Assert.Equal(255, packets[0].Data[5]);
        Assert.Equal(255, packets[1].Data[5]);
        Assert.Equal(90, packets[2].Data[5]);
        Assert.Equal(600, packets.Sum(p => (int)p.Data[5])); // every row accounted for, none truncated
    }

    [Fact]
    public void PrintBitmapRowIndexed_matches_niimbluelib_reference_vector()
    {
        // Documented niimbluelib packet:
        // 5555 83 0e 007e 000400 01 0027 0028 0029 002a fa aaaa
        var packet = PacketGenerator.PrintBitmapRowIndexed(
            position: 0x7E, repeats: 1, blackPixelIndices: [0x27, 0x28, 0x29, 0x2A], counts: (0, 4, 0));
        Assert.Equal("5555830E007E00040001002700280029002AFAAAAA", Convert.ToHexString(packet.ToBytes()));
    }
}
