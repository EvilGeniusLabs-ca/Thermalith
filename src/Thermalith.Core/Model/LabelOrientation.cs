namespace Thermalith.Core.Model;

/// <summary>
/// Whole-label 90° rotation (label-orientation, worklist §A). The editor presents a rotated <i>view</i>
/// of a fixed-size physical label so the designer can work in whichever orientation suits the content;
/// at print the view is rotated back onto the physical feed via <see cref="Canvas.OrientationDeg"/>.
///
/// A rotate swaps the view's width/height, turns every element 90° (keeping each element's own
/// unrotated W/H, adding ±90° to its angle, and moving its centre to the rotated spot — the renderer
/// rotates each element about its centre, so this turns text/table/line/image uniformly without
/// touching internal geometry), and adjusts <see cref="Canvas.OrientationDeg"/> by the inverse so the
/// physical feed stays fixed. Four rotates in one direction return to the original document.
/// </summary>
public static class LabelOrientation
{
    /// <summary>Turn the label 90° clockwise (rotate-right).</summary>
    public static LabelDocument RotateRight(LabelDocument doc) => Rotate(doc, clockwise: true);

    /// <summary>Turn the label 90° counter-clockwise (rotate-left).</summary>
    public static LabelDocument RotateLeft(LabelDocument doc) => Rotate(doc, clockwise: false);

    public static LabelDocument Rotate(LabelDocument doc, bool clockwise)
    {
        var c = doc.Canvas;
        double oldW = c.WidthMm, oldH = c.HeightMm;

        var elements = doc.Elements.Select(e =>
        {
            double cx = e.X + e.W / 2, cy = e.Y + e.H / 2;
            double ncx, ncy, nrot;
            if (clockwise) { ncx = oldH - cy; ncy = cx; nrot = e.Rotation + 90; }
            else { ncx = cy; ncy = oldW - cx; nrot = e.Rotation - 90; }
            nrot = ((nrot % 360) + 360) % 360;
            return e with { X = ncx - e.W / 2, Y = ncy - e.H / 2, Rotation = nrot };
        }).ToList();

        // rotate-right turns the view +90° CW, so the view→physical rotation drops 90°; left adds 90°.
        var newOrient = (((c.OrientationDeg + (clockwise ? -90 : 90)) % 360) + 360) % 360;

        return doc with
        {
            Canvas = c with { WidthMm = oldH, HeightMm = oldW, OrientationDeg = newOrient },
            Elements = elements,
        };
    }
}
