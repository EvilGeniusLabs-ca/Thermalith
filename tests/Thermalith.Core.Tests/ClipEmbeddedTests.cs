using Thermalith.Core.Fonts;
using Thermalith.Core.Rendering;
using Xunit;

namespace Thermalith.Core.Tests;

// Exercises the runtime path the palette actually uses: fonts + index loaded from embedded resources
// in Thermalith.Core (no source-tree paths).
public class ClipEmbeddedTests
{
    [Fact]
    public void Embedded_catalog_lists_all_fonts_and_resolves()
    {
        using var cat = ClipFontCatalog.CreateEmbedded();

        Assert.Equal(8, cat.Keys.Count);
        Assert.True(cat.Contains("materialdesignicons-webfont"));
        Assert.True(cat.Contains("NotoColorEmoji"));
        Assert.NotNull(cat.Resolve("materialdesignicons-webfont"));
        Assert.Null(cat.Resolve("not-bundled"));
    }

    [Fact]
    public void Embedded_index_loads_and_matches_font_set()
    {
        var idx = ClipIndex.LoadEmbedded();

        Assert.Equal(8, idx.Fonts.Count);
        Assert.True(idx.Glyphs.Count > 20000, $"only {idx.Glyphs.Count} glyphs");
    }

    [Fact]
    public void Embedded_index_and_catalog_bake_a_searched_glyph_end_to_end()
    {
        using var cat = ClipFontCatalog.CreateEmbedded();
        var idx = ClipIndex.LoadEmbedded();

        var hit = idx.Search("delete", fontKey: "materialdesignicons-webfont")[0];
        var tf = cat.Resolve(hit.Font)!;
        var baked = ClipBaker.Bake(tf, hit.Codepoint, nominalSizeMm: 10, dpi: 203);

        Assert.NotNull(baked);
        Assert.True(baked!.WidthMm > 0 && baked.HeightMm > 0);
    }
}
