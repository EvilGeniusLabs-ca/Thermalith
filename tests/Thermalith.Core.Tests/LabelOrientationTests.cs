using Thermalith.Core.Model;
using Thermalith.Core.Serialization;
using Xunit;
// LabelDocumentSerializer + LabelOrientation both live under Thermalith.Core.*

namespace Thermalith.Core.Tests;

public class LabelOrientationTests
{
    private static LabelDocument TallDoc() => new()
    {
        Metadata = new LabelMetadata { Name = "rot" },
        Canvas = new Canvas { WidthMm = 30, HeightMm = 50, Dpi = 203 },
        Elements =
        [
            // A box near the top-left of the tall 30×50 label.
            new ShapeElement { Id = "a", X = 2, Y = 4, W = 10, H = 6, Rotation = 0, Props = new ShapeProps { ShapeType = "rect" } },
        ],
    };

    [Fact]
    public void Rotate_right_swaps_the_view_dimensions()
    {
        var r = LabelOrientation.RotateRight(TallDoc());
        Assert.Equal(50, r.Canvas.WidthMm);
        Assert.Equal(30, r.Canvas.HeightMm);
    }

    [Fact]
    public void Rotate_right_drops_orientation_90_and_left_adds_90()
    {
        Assert.Equal(270, LabelOrientation.RotateRight(TallDoc()).Canvas.OrientationDeg);
        Assert.Equal(90, LabelOrientation.RotateLeft(TallDoc()).Canvas.OrientationDeg);
    }

    [Fact]
    public void Four_rotate_rights_return_to_the_original()
    {
        var doc = TallDoc();
        var spun = doc;
        for (var i = 0; i < 4; i++) spun = LabelOrientation.RotateRight(spun);

        Assert.Equal(doc.Canvas.WidthMm, spun.Canvas.WidthMm);
        Assert.Equal(doc.Canvas.HeightMm, spun.Canvas.HeightMm);
        Assert.Equal(0, spun.Canvas.OrientationDeg);

        var a0 = doc.Elements[0];
        var a4 = spun.Elements[0];
        Assert.Equal(a0.X, a4.X, 6);
        Assert.Equal(a0.Y, a4.Y, 6);
        Assert.Equal(a0.W, a4.W, 6);
        Assert.Equal(a0.H, a4.H, 6);
        Assert.Equal(a0.Rotation % 360, a4.Rotation % 360, 6);
    }

    [Fact]
    public void Rotate_right_moves_the_element_centre_but_keeps_it_upright()
    {
        // oldH = 50; centre (7,7) → CW: (oldH - cy, cx) = (43, 7). W/H unchanged, angle UNCHANGED.
        var r = LabelOrientation.RotateRight(TallDoc());
        var a = r.Elements[0];
        Assert.Equal(43 - a.W / 2, a.X, 6);   // newX = newCentreX - W/2 = 43 - 5
        Assert.Equal(7 - a.H / 2, a.Y, 6);    // newY = newCentreY - H/2 = 7 - 3
        Assert.Equal(10, a.W, 6);             // box size preserved
        Assert.Equal(6, a.H, 6);
        Assert.Equal(0, a.Rotation, 6);       // control stays at its authored angle — only the print rotates
    }

    [Fact]
    public void Rotate_does_not_change_an_elements_own_angle()
    {
        var doc = TallDoc() with { Elements = [TallDoc().Elements[0] with { Rotation = 30 } ] };
        Assert.Equal(30, LabelOrientation.RotateRight(doc).Elements[0].Rotation, 6);
        Assert.Equal(30, LabelOrientation.RotateLeft(doc).Elements[0].Rotation, 6);
    }

    [Fact]
    public void OrientationDeg_round_trips_through_nlbl_json()
    {
        var doc = LabelOrientation.RotateRight(TallDoc());            // OrientationDeg = 270
        var back = LabelDocumentSerializer.FromJson(LabelDocumentSerializer.ToJson(doc));
        Assert.Equal(270, back.Canvas.OrientationDeg);
        Assert.Equal(50, back.Canvas.WidthMm);
    }
}
