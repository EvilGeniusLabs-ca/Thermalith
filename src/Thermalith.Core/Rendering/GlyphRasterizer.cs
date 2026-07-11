using SkiaSharp;

namespace Thermalith.Core.Rendering;

/// <summary>
/// The clip-art foundation (GitHub #2): rasterize a single font glyph to a bitmap so a placed clip is
/// just an <c>ImageElement</c>. Pure and transport-agnostic — it takes an already-resolved
/// <see cref="SKTypeface"/> and a codepoint and returns an antialiased, tightly-cropped raster on a
/// white ground. It does <b>not</b> threshold: the output is a normal grayscale-able PNG that feeds the
/// existing image dither/threshold path (LabelRenderer.DrawImage) exactly like an imported picture, so
/// the 1-bit conversion happens once, downstream, and "preview = print" holds.
/// </summary>
public static class GlyphRasterizer
{
    /// <summary>Result of rasterizing one glyph: the encoded PNG, its pixel dimensions, and whether the
    /// typeface drew the glyph in colour (COLR/CBDT emoji) rather than a monochrome outline.</summary>
    public sealed record GlyphRaster(byte[] Png, int WidthPx, int HeightPx, bool HasColor);

    /// <summary>
    /// Rasterize <paramref name="codepoint"/> from <paramref name="typeface"/> at a nominal em size of
    /// <paramref name="renderPx"/> pixels, cropped to the glyph's ink bounds (plus <paramref name="paddingPx"/>
    /// on every edge so antialiased edges aren't clipped). Returns <c>null</c> when the typeface has no glyph
    /// for the codepoint, or the glyph is blank (e.g. whitespace) — nothing to place.
    /// </summary>
    public static GlyphRaster? Rasterize(SKTypeface typeface, int codepoint, int renderPx, int paddingPx = 2)
    {
        ArgumentNullException.ThrowIfNull(typeface);
        if (renderPx < 1) throw new ArgumentOutOfRangeException(nameof(renderPx));
        if (paddingPx < 0) throw new ArgumentOutOfRangeException(nameof(paddingPx));
        if (!typeface.ContainsGlyph(codepoint)) return null;

        var text = char.ConvertFromUtf32(codepoint);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,      // monochrome outlines fill black; colour glyphs override this
            IsAntialias = true,          // AA edges → the downstream threshold binarizes crisply (§6.3.3)
            SubpixelText = false,
            Typeface = typeface,
            TextSize = renderPx,
        };

        // Tight ink bounds of the glyph, relative to the baseline origin (Top is negative — above baseline).
        var bounds = new SKRect();
        paint.MeasureText(text, ref bounds);
        if (bounds.Width < 0.5f || bounds.Height < 0.5f) return null;

        var w = (int)Math.Ceiling(bounds.Width) + 2 * paddingPx;
        var h = (int)Math.Ceiling(bounds.Height) + 2 * paddingPx;

        using var bmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(SKColors.White);
            // Shift so the ink's top-left lands at (padding, padding): baseline at (pad - Left, pad - Top).
            canvas.DrawText(text, paddingPx - bounds.Left, paddingPx - bounds.Top, paint);
        }

        var hasColor = DetectColor(bmp, w, h);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return new GlyphRaster(data.ToArray(), w, h, hasColor);
    }

    // A glyph is "colour" if any pixel carries meaningful chroma — i.e. R/G/B diverge. Monochrome outline
    // fonts render pure grays (R==G==B), so this cleanly distinguishes an emoji whose colour layers were
    // actually rasterized from one that fell back to a flat outline.
    private static bool DetectColor(SKBitmap bmp, int w, int h)
    {
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var c = bmp.GetPixel(x, y);
                var max = Math.Max(c.Red, Math.Max(c.Green, c.Blue));
                var min = Math.Min(c.Red, Math.Min(c.Green, c.Blue));
                if (max - min > 16) return true;
            }
        return false;
    }
}
