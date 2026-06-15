namespace Thermalith.Core.Model;

/// <summary>
/// Whole-label 90° rotation (label-orientation, worklist §A). The editor presents a rotated <i>view</i>
/// of a fixed-size physical label so the designer can work in whichever orientation suits the content;
/// at print the view is rotated onto the physical feed via <see cref="Canvas.OrientationDeg"/>.
///
/// A rotate swaps the view's width/height and adjusts <see cref="Canvas.OrientationDeg"/> (which the
/// renderer applies to the <b>print output</b>). <b>Elements are left exactly as authored</b> — rotate
/// reorients the canvas + print only; it does not reposition, resize, or turn any element. The user
/// repositions/resizes content for the new orientation (best-effort by design — orientation is a
/// physical/print property, not a layout transform). Four rotates in one direction return to the
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
        // rotate-right turns the view +90° CW, so the view→physical rotation drops 90°; left adds 90°.
        var newOrient = (((c.OrientationDeg + (clockwise ? -90 : 90)) % 360) + 360) % 360;

        // Elements are intentionally untouched — only the canvas dimensions + print orientation change.
        return doc with
        {
            Canvas = c with { WidthMm = c.HeightMm, HeightMm = c.WidthMm, OrientationDeg = newOrient },
        };
    }
}
