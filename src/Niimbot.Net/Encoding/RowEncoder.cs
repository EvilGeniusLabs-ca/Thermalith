using Niimbot.Net.Commands;
using Niimbot.Net.Framing;

namespace Niimbot.Net.Encoding;

/// <summary>How the three "black pixel count" header bytes of a bitmap-row packet are filled.</summary>
public enum PixelCountMode
{
    /// <summary>Split across thirds of the printhead when the row fits; otherwise fall back to total.</summary>
    Auto,

    /// <summary>Always split the count across thirds of the printhead width.</summary>
    Split,

    /// <summary>Encode the single total count as <c>[0, low, high]</c>.</summary>
    Total,
}

/// <summary>The 3 header count bytes plus the total black-pixel tally for one row.</summary>
public readonly record struct PixelCounts((byte, byte, byte) Parts, int Total);

/// <summary>
/// Turns a <see cref="MonochromeBitmap"/> into the on-wire row packet stream: vertical run-length
/// encoding (identical adjacent rows collapse into one packet with a repeat count), blank rows
/// emitted as <c>PrintEmptyRow</c>, and short rows (≤6 black pixels) emitted in the compact
/// indexed form. Synthesized from niimbluelib's <c>ImageEncoder</c> / <c>PacketGenerator</c>;
/// niimprint confirms the row header shape <c>pos(2)|counts(3)|repeat(1)|data</c>. See spec §5.
/// </summary>
public static class RowEncoder
{
    /// <summary>Options controlling row-packet generation.</summary>
    public sealed record Options
    {
        /// <summary>Printhead resolution in pixels — drives the split-count chunking.</summary>
        public int PrintheadPixels { get; init; }

        /// <summary>How the 3 count header bytes are computed.</summary>
        public PixelCountMode CountMode { get; init; } = PixelCountMode.Auto;

        /// <summary>Use the compact indexed packet for rows with ≤6 black pixels.</summary>
        public bool UseIndexedRows { get; init; } = true;
    }

    /// <summary>
    /// Count black pixels in a packed row and produce the 3-byte header count field. In split mode
    /// the row is divided into three printhead-thirds and each third's set-bit count occupies one
    /// byte; otherwise the total is encoded as <c>[0, low, high]</c>.
    /// </summary>
    public static PixelCounts CountPixels(ReadOnlySpan<byte> rowData, int printheadPixels, PixelCountMode mode = PixelCountMode.Auto)
    {
        var total = 0;
        Span<int> parts = stackalloc int[3];
        var chunkSize = printheadPixels / 8 / 3; // bytes per third (8 px per byte)
        var canSplit = chunkSize > 0 && rowData.Length <= chunkSize * 3;

        var split = mode switch
        {
            PixelCountMode.Total => false,
            PixelCountMode.Split => canSplit,
            _ => canSplit,
        };

        for (var byteN = 0; byteN < rowData.Length; byteN++)
        {
            var value = rowData[byteN];
            if (value == 0)
                continue;

            var chunkIdx = chunkSize > 0 ? byteN / chunkSize : 0;
            for (var bit = 0; bit < 8; bit++)
            {
                if ((value & (1 << bit)) == 0)
                    continue;
                total++;
                if (split && chunkIdx <= 2)
                    parts[chunkIdx]++;
            }
        }

        if (split)
            return new PixelCounts(((byte)parts[0], (byte)parts[1], (byte)parts[2]), total);

        return new PixelCounts((0, (byte)(total & 0xFF), (byte)((total >> 8) & 0xFF)), total);
    }

    /// <summary>Big-endian pixel indices of every set (black) bit in a packed row, MSB-first.</summary>
    public static int[] IndexPixels(ReadOnlySpan<byte> rowData)
    {
        var result = new List<int>();
        for (var bytePos = 0; bytePos < rowData.Length; bytePos++)
        {
            var b = rowData[bytePos];
            for (var bit = 0; bit < 8; bit++)
            {
                if ((b & (1 << (7 - bit))) != 0)
                    result.Add(bytePos * 8 + bit);
            }
        }
        return result.ToArray();
    }

    /// <summary>
    /// Encode every row of <paramref name="bitmap"/> into the ordered packet stream to send between
    /// <c>PageStart</c>/<c>SetPageSize</c> and <c>PageEnd</c>.
    /// </summary>
    public static IReadOnlyList<NiimbotPacket> Encode(MonochromeBitmap bitmap, Options options)
    {
        var packets = new List<NiimbotPacket>();

        var y = 0;
        while (y < bitmap.HeightPx)
        {
            var row = bitmap.Row(y);
            var isVoid = IsBlank(row);

            // Run-length: how many following rows are byte-identical to this one.
            var repeat = 1;
            while (y + repeat < bitmap.HeightPx && bitmap.Row(y + repeat).SequenceEqual(row))
                repeat++;

            if (isVoid)
            {
                packets.Add(PacketGenerator.PrintEmptyRow(y, repeat));
            }
            else
            {
                var counts = CountPixels(row, options.PrintheadPixels, options.CountMode);
                if (options.UseIndexedRows && counts.Total <= 6)
                {
                    var indices = IndexPixels(row);
                    packets.Add(PacketGenerator.PrintBitmapRowIndexed(y, repeat, indices, counts.Parts));
                }
                else
                {
                    packets.Add(PacketGenerator.PrintBitmapRow(y, repeat, row, counts.Parts));
                }
            }

            y += repeat;
        }

        return packets;
    }

    private static bool IsBlank(ReadOnlySpan<byte> row)
    {
        foreach (var b in row)
            if (b != 0)
                return false;
        return true;
    }
}
