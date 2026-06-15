namespace Thermalith.Core.Model;

/// <summary>
/// Whole-label 90° rotation (label-orientation, worklist §A). The editor presents a rotated <i>view</i>
/// of a fixed-size physical label so the designer can work in whichever orientation suits the content;
/// at print the view is rotated onto the physical feed via <see cref="Canvas.OrientationDeg"/>.
///
/// A rotate swaps the view's width/height and moves each element's <b>centre</b> to the reoriented spot
/// so content stays on the reshaped canvas. It deliberately does <b>NOT</b> change any element's own
/// <see cref="LabelElement.Rotation"/> — the controls stay upright in the editor (a horizontal text box
/// stays horizontal). The 90° turn shows up <b>only in the print output</b>: the renderer applies
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

        // Move each element's centre to the reoriented position so it stays on the reshaped canvas;
        // keep its W/H and angle untouched — controls stay upright in the editor (only the print rotates).
        var elements = doc.Elements.Select(e =>
        {
            double cx = e.X + e.W / 2, cy = e.Y + e.H / 2;
            double ncx, ncy;
            if (clockwise) { ncx = oldH - cy; ncy = cx; }
            else { ncx = cy; ncy = oldW - cx; }
            return e with { X = ncx - e.W / 2, Y = ncy - e.H / 2 };
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
