namespace Niimbot.Net.Encoding;

/// <summary>
/// The model-agnostic 1bpp raster the encoder consumes — the clean hand-off seam from a renderer
/// (e.g. Thermalith.Core's identically-shaped <c>MonochromeBitmap</c>, spec §6.3.5) into the
/// protocol layer. <c>Niimbot.Net</c> owns this copy so the library stands alone with no
/// dependency on any label engine.
///
/// <para>Layout: row-major, each row padded to a whole byte; within a byte the <b>most significant
/// bit is the leftmost pixel</b>; a set bit (<c>1</c>) means <b>burn (black)</b>. Verified against
/// both niimbluelib and niimprint, which agree on MSB-first / 1=burn.</para>
/// </summary>
public sealed class MonochromeBitmap
{
    public MonochromeBitmap(int widthPx, int heightPx, byte[] packed)
    {
        if (widthPx <= 0) throw new ArgumentOutOfRangeException(nameof(widthPx));
        if (heightPx <= 0) throw new ArgumentOutOfRangeException(nameof(heightPx));

        BytesPerRow = (widthPx + 7) / 8;
        var expected = BytesPerRow * heightPx;
        if (packed.Length != expected)
            throw new ArgumentException($"Packed length {packed.Length} != expected {expected} ({BytesPerRow}×{heightPx}).", nameof(packed));

        WidthPx = widthPx;
        HeightPx = heightPx;
        Packed = packed;
    }

    public int WidthPx { get; }

    public int HeightPx { get; }

    /// <summary>Packed bits, <c>BytesPerRow × HeightPx</c> bytes.</summary>
    public byte[] Packed { get; }

    /// <summary>Bytes per row, <c>ceil(WidthPx / 8)</c>.</summary>
    public int BytesPerRow { get; }

    /// <summary>A view over the packed bytes of a single row.</summary>
    public ReadOnlySpan<byte> Row(int y) => Packed.AsSpan(y * BytesPerRow, BytesPerRow);
}
