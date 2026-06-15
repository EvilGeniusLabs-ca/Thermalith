namespace Thermalith.Core.Rendering;

/// <summary>Knobs for the render path (kept tiny so the path stays pure given these + the resolved label).</summary>
public sealed record RenderOptions
{
    /// <summary>Luminance cutoff for the stage-4 threshold safety net: a pixel darker than this burns (§6.3.3).</summary>
    public int TextThreshold { get; init; } = 128;

    /// <summary>
    /// Apply the canvas <c>OrientationDeg</c> (view → physical-feed rotation) to the output raster.
    /// <c>true</c> for printing/export (the printer needs the physical feed orientation); <c>false</c>
    /// for the editor preview, which shows the upright design view the user edits in (worklist §A,
    /// label orientation).
    /// </summary>
    public bool ApplyOrientation { get; init; } = true;

    public static readonly RenderOptions Default = new();
}
