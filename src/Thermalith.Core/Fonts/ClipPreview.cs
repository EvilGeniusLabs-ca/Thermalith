using Thermalith.Core.Rendering;

namespace Thermalith.Core.Fonts;

/// <summary>
/// Palette convenience (GitHub #2): rasterize a clip glyph to PNG bytes via the catalog + rasterizer, so
/// UI code deals only in bytes and never references SkiaSharp. Returns <c>null</c> if the font isn't
/// bundled or the glyph can't be rendered.
/// </summary>
public static class ClipPreview
{
    public static byte[]? RenderPng(ClipFontCatalog catalog, string fontKey, int codepoint, int renderPx)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var typeface = catalog.Resolve(fontKey);
        return typeface is null ? null : GlyphRasterizer.Rasterize(typeface, codepoint, renderPx)?.Png;
    }
}
