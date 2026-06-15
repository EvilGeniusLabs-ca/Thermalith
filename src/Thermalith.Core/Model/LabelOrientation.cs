namespace Thermalith.Core.Model;

/// <summary>
/// Whole-label 90° rotation (label-orientation, worklist §A). The editor presents a rotated <i>view</i>
/// of a fixed-size physical label so the designer can work in whichever orientation suits the content;
/// at print the view is rotated onto the physical feed via <see cref="Canvas.OrientationDeg"/>.
///
/// A rotate swaps the view's width/height and rotates each element's <b>bounding box</b> with it
/// (position + W/H swap) so footprints follow the label — a box filling the canvas keeps filling it.
/// It deliberately does <b>NOT</b> change any element's own <see cref="LabelElement.Rotation"/>: the
/// controls stay upright in the editor (a horizontal text box stays horizontal, just reshaped to the
/// reoriented footprint). The 90° turn shows up <b>only in the print output</b>: the renderer applies
/// <see cref="Canvas.OrientationDeg"/> when rasterizing for the printer. <see cref="Canvas.OrientationDeg"/>
/// is adjusted so the physical feed dimensions stay fixed. Four rotates in one direction return to the
/// original document.
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

        // Rotate each element's bounding box with the label: reposition + swap W/H so footprints
        // reorient (a box filling the canvas keeps filling it). Angle is left untouched — controls stay
        // upright in the editor; the 90° turn shows up only in the print output (via OrientationDeg).
        var elements = doc.Elements.Select(e =>
        {
            double nx, ny;
            if (clockwise) { nx = oldH - (e.Y + e.H); ny = e.X; }
            else { nx = e.Y; ny = oldW - (e.X + e.W); }
            return e with { X = nx, Y = ny, W = e.H, H = e.W };
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
