namespace Thermalith.Core.Rendering;

/// <summary>An 8-bit grayscale raster (0 = black, 255 = white), row-major, one byte per pixel. Produced
/// by <see cref="LabelRenderer.RenderGray(ResolvedLabel, RenderOptions?)"/> for the smooth stage-3
/// preview (§6.3.6) — a display aid only; printing uses the 1bpp <c>MonochromeBitmap</c>.</summary>
public sealed record GrayBitmap(int WidthPx, int HeightPx, byte[] Gray);
