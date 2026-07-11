using SkiaSharp;
using Thermalith.Core.Model;

namespace Thermalith.Core.Rendering;

/// <summary>
/// Bakes a font glyph into a placed clip (GitHub #2): a clip <b>is</b> an <see cref="ImageElement"/> whose
/// bitmap was rasterized from a glyph. This turns a (typeface, codepoint, size, dpi) into the PNG asset
/// plus the element that references it — carrying the clip-ref fields so the app can re-bake crisp on
/// resize / dpi change. Rasterization goes at the label dpi so the bitmap maps 1:1 to printer dots.
/// </summary>
public static class ClipBaker
{
    /// <summary>A rasterized glyph: the encoded PNG and its true physical footprint (the ink bounds at the
    /// bake dpi), plus whether the glyph drew in colour (a colour-emoji tell for the preview call).</summary>
    public sealed record BakedClip(byte[] Png, double WidthMm, double HeightMm, bool HasColor);

    /// <summary>
    /// Rasterize <paramref name="codepoint"/> from <paramref name="typeface"/> at <paramref name="dpi"/>,
    /// nominally <paramref name="nominalSizeMm"/> tall (the em size the user picked). The returned footprint
    /// is the glyph's actual ink extent, sized so one raster pixel is one printer dot at <paramref name="dpi"/>
    /// (no resampling at print). Returns <c>null</c> when the typeface has no glyph for the codepoint.
    /// </summary>
    public static BakedClip? Bake(SKTypeface typeface, int codepoint, double nominalSizeMm, int dpi)
    {
        ArgumentNullException.ThrowIfNull(typeface);
        if (nominalSizeMm <= 0) throw new ArgumentOutOfRangeException(nameof(nominalSizeMm));
        if (dpi < 1) throw new ArgumentOutOfRangeException(nameof(dpi));

        var renderPx = Math.Max(1, (int)Math.Round(nominalSizeMm * dpi / 25.4));
        var g = GlyphRasterizer.Rasterize(typeface, codepoint, renderPx);
        if (g is null) return null;

        return new BakedClip(g.Png, g.WidthPx * 25.4 / dpi, g.HeightPx * 25.4 / dpi, g.HasColor);
    }

    /// <summary>
    /// Build a placed clip from a bake: an <see cref="ImageElement"/> sized to the ink footprint, its
    /// bitmap stored under an asset id equal to the element id (one asset per clip, overwritten on re-bake,
    /// never orphaned). Returns the element and the <c>(assetId, png)</c> to add to the package assets.
    /// </summary>
    public static (ImageElement Element, string AssetId, byte[] Png) CreateElement(
        string id, string fontKey, int codepoint, double xMm, double yMm, BakedClip baked, int dpi)
    {
        var el = new ImageElement
        {
            Id = id,
            X = xMm,
            Y = yMm,
            W = baked.WidthMm,
            H = baked.HeightMm,
            Props = new ImageProps
            {
                AssetId = id,
                Fit = "stretch",
                Dither = "threshold",   // 1:1 raster of a glyph → crisp threshold, not error-diffusion fuzz
                ClipFont = fontKey,
                ClipCodepoint = codepoint,
                ClipBakeDpi = dpi,
            },
        };
        return (el, id, baked.Png);
    }

    /// <summary>
    /// Re-rasterize an existing clip at a new size / dpi (e.g. after a resize or a printer change), keeping
    /// the same asset id so the stored bitmap is overwritten rather than orphaned. Returns the updated
    /// element (footprint + <c>ClipBakeDpi</c> refreshed) and the new PNG, or <c>null</c> when the element
    /// isn't a clip or the glyph can't be rasterized.
    /// </summary>
    public static (ImageElement Element, byte[] Png)? Rebake(ImageElement clip, SKTypeface typeface, double nominalSizeMm, int dpi)
    {
        if (clip.Props.ClipCodepoint is not int codepoint) return null;
        var baked = Bake(typeface, codepoint, nominalSizeMm, dpi);
        if (baked is null) return null;

        var el = clip with
        {
            W = baked.WidthMm,
            H = baked.HeightMm,
            Props = clip.Props with { ClipBakeDpi = dpi },  // AssetId unchanged → overwrites the same asset
        };
        return (el, baked.Png);
    }
}
