using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Xunit;

namespace Thermalith.Core.Tests;

/// <summary>MeasureRenderedBounds (GitHub #5): the selection box must track drawn glyphs, not the stored box.</summary>
public class RenderedBoundsTests
{
    private static readonly LabelRenderer Renderer = new();

    private static LabelDocument Doc(params LabelElement[] els) => new()
    {
        Metadata = new LabelMetadata { Name = "b" },
        Canvas = new Canvas { WidthMm = 80, HeightMm = 40, Dpi = 203 },
        Elements = [.. els],
    };

    private static RenderedRect Bounds(LabelDocument doc, string id)
    {
        var resolved = LabelResolver.Resolve(doc, new ResolveContext());
        return Renderer.MeasureRenderedBounds(resolved)[id];
    }

    [Fact]
    public void Text_wider_than_its_box_grows_the_rendered_bounds()
    {
        // A tiny 4 mm box with long text at a big font: the drawn glyphs overflow, so bounds must be wider.
        var doc = Doc(new TextElement
        {
            Id = "t", X = 5, Y = 5, W = 4, H = 6,
            Props = new TextProps { Content = "WWWWWWWWWWWW", FontSizePt = 24, AutoSize = false, Wrap = "none" },
        });

        var b = Bounds(doc, "t");
        Assert.True(b.WMm > 4.0, $"expected rendered width > box 4mm, got {b.WMm:0.##}");
        Assert.Equal(5.0, b.XMm, 3); // left-justified: still anchored at the element's X
    }

    [Fact]
    public void Text_narrower_than_its_box_is_tight_to_the_glyphs()
    {
        // A short centred string in a wide box: bounds hug the glyphs, not the 60 mm box.
        var doc = Doc(new TextElement
        {
            Id = "t", X = 5, Y = 5, W = 60, H = 8,
            Justify = new Justify { H = "center" },
            Props = new TextProps { Content = "Hi", FontSizePt = 10, AutoSize = false, Wrap = "none" },
        });

        var b = Bounds(doc, "t");
        Assert.True(b.WMm < 20.0, $"expected tight width « 60mm box, got {b.WMm:0.##}");
        Assert.True(b.XMm > 5.0, "centred text should start right of the box's left edge");
    }

    [Fact]
    public void Non_text_element_bounds_equal_its_model_box()
    {
        var doc = Doc(new ShapeElement
        {
            Id = "s", X = 10, Y = 8, W = 20, H = 12,
            Props = new ShapeProps { ShapeType = "rect" },
        });

        var b = Bounds(doc, "s");
        Assert.Equal(10.0, b.XMm, 3);
        Assert.Equal(8.0, b.YMm, 3);
        Assert.Equal(20.0, b.WMm, 3);
        Assert.Equal(12.0, b.HMm, 3);
    }

    [Fact]
    public void Rotated_element_bounds_are_the_rotated_aabb()
    {
        // A 20×4 box rotated 90° about its centre → AABB is ~4 wide × ~20 tall.
        var doc = Doc(new ShapeElement
        {
            Id = "r", X = 10, Y = 10, W = 20, H = 4, Rotation = 90,
            Props = new ShapeProps { ShapeType = "rect" },
        });

        var b = Bounds(doc, "r");
        Assert.Equal(4.0, b.WMm, 1);
        Assert.Equal(20.0, b.HMm, 1);
    }
}
