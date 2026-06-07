using Niimbot.Net;
using Niimbot.Net.Encoding;
using Xunit;

namespace Niimbot.Net.Tests;

public class PositioningTests
{
    // A 1×1-content bitmap (8px wide, single pixel at x0) placed on a 16px head.
    private static MonochromeBitmap Pixel() => new(8, 1, [0x80]);

    [Fact]
    public void Center_places_content_in_the_middle_of_the_head()
    {
        var page = NiimbotClient.PositionOnHead(Pixel(), 16, new PrintOptions { HorizontalAlign = PrintAlignment.Center });
        Assert.Equal(16, page.WidthPx);
        // leftPx = (16-8)/2 = 4 → bit at x4 = 0x08 in byte 0.
        Assert.Equal([0x08, 0x00], page.Packed);
    }

    [Fact]
    public void Left_align_keeps_content_at_head_origin()
    {
        var page = NiimbotClient.PositionOnHead(Pixel(), 16, new PrintOptions { HorizontalAlign = PrintAlignment.Left });
        Assert.Equal([0x80, 0x00], page.Packed);
    }

    [Fact]
    public void Right_align_pushes_content_to_the_far_edge()
    {
        var page = NiimbotClient.PositionOnHead(Pixel(), 16, new PrintOptions { HorizontalAlign = PrintAlignment.Right });
        // leftPx = 16-8 = 8 → bit at x8 = 0x80 in byte 1.
        Assert.Equal([0x00, 0x80], page.Packed);
    }

    [Fact]
    public void OffsetX_nudges_from_the_aligned_position_and_clamps()
    {
        // Center is x4; +2 → x6 = 0x02 in byte 0.
        var nudged = NiimbotClient.PositionOnHead(Pixel(), 16, new PrintOptions { OffsetXPx = 2 });
        Assert.Equal([0x02, 0x00], nudged.Packed);

        // Left align with a large negative offset clamps to x0, never past the head edge.
        var clamped = NiimbotClient.PositionOnHead(Pixel(), 16,
            new PrintOptions { HorizontalAlign = PrintAlignment.Left, OffsetXPx = -50 });
        Assert.Equal([0x80, 0x00], clamped.Packed);
    }

    [Fact]
    public void OffsetY_prepends_blank_feed_rows()
    {
        var page = NiimbotClient.PositionOnHead(Pixel(), 16, new PrintOptions { HorizontalAlign = PrintAlignment.Left, OffsetYPx = 2 });
        Assert.Equal(3, page.HeightPx); // 1 content row + 2 blank
        Assert.Equal([0x00, 0x00], page.Packed.AsSpan(0, 2).ToArray()); // row 0 blank
        Assert.Equal([0x00, 0x00], page.Packed.AsSpan(2, 2).ToArray()); // row 1 blank
        Assert.Equal([0x80, 0x00], page.Packed.AsSpan(4, 2).ToArray()); // row 2 content
    }
}
