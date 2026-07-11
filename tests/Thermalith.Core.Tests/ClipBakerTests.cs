using System.Numerics;
using Niimbot.Net.Encoding;
using SkiaSharp;
using Thermalith.Core.Fonts;
using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Thermalith.Core.Serialization;
using Xunit;

namespace Thermalith.Core.Tests;

public class ClipBakerTests
{
    private const string Mdi = "materialdesignicons-webfont";

    [Fact]
    public void Catalog_lists_and_resolves_bundled_fonts()
    {
        using var cat = OpenCatalog();

        Assert.NotEmpty(cat.Keys);
        Assert.Contains(cat.Keys, k => k.Equals(Mdi, StringComparison.OrdinalIgnoreCase));
        Assert.True(cat.Contains("NotoColorEmoji"));
        Assert.NotNull(cat.Resolve(Mdi));
        Assert.Null(cat.Resolve("does-not-exist"));
    }

    [Fact]
    public void Bakes_a_glyph_to_a_sized_clip()
    {
        using var cat = OpenCatalog();
        var tf = cat.Resolve(Mdi)!;
        var cp = FirstGlyph(tf);

        var baked = ClipBaker.Bake(tf, cp, nominalSizeMm: 10, dpi: 203);

        Assert.NotNull(baked);
        Assert.True(baked!.WidthMm > 0 && baked.HeightMm > 0, $"{baked.WidthMm}×{baked.HeightMm}");
        Assert.True(baked.HeightMm < 15, $"height {baked.HeightMm} unreasonable for a 10mm nominal glyph");
        using var decoded = SKBitmap.Decode(baked.Png);
        Assert.NotNull(decoded);
    }

    [Fact]
    public void Builds_a_clip_image_element_with_ref_fields()
    {
        using var cat = OpenCatalog();
        var tf = cat.Resolve(Mdi)!;
        var cp = FirstGlyph(tf);
        var baked = ClipBaker.Bake(tf, cp, 10, 203)!;

        var (el, assetId, _) = ClipBaker.CreateElement("clip1", Mdi, cp, xMm: 3, yMm: 4, baked, 203);

        Assert.Equal("image", el.Type);
        Assert.Equal("clip1", assetId);
        Assert.Equal(assetId, el.Props.AssetId);
        Assert.Equal(Mdi, el.Props.ClipFont);
        Assert.Equal(cp, el.Props.ClipCodepoint);
        Assert.Equal(203, el.Props.ClipBakeDpi);
        Assert.Equal(baked.WidthMm, el.W);
        Assert.Equal(baked.HeightMm, el.H);
    }

    [Fact]
    public void Clip_element_renders_to_ink()
    {
        using var cat = OpenCatalog();
        var tf = cat.Resolve(Mdi)!;
        var cp = FirstGlyph(tf);
        var baked = ClipBaker.Bake(tf, cp, 10, 203)!;
        var (el, assetId, png) = ClipBaker.CreateElement("clip1", Mdi, cp, 3, 4, baked, 203);

        var doc = new LabelDocument
        {
            Metadata = new LabelMetadata { Name = "clip" },
            Canvas = new Canvas { WidthMm = 20, HeightMm = 20, Dpi = 203 },
            Elements = [el],
        };

        using var fonts = new FontService();
        var bmp = new LabelRenderer(fonts).Render(doc,
            new ResolveContext { Assets = new Dictionary<string, byte[]> { [assetId] = png } });

        Assert.True(InkBits(bmp) > 0, "the baked clip must leave ink on the raster");
    }

    [Fact]
    public void Clip_survives_the_nlbl_round_trip()
    {
        using var cat = OpenCatalog();
        var tf = cat.Resolve(Mdi)!;
        var cp = FirstGlyph(tf);
        var baked = ClipBaker.Bake(tf, cp, 10, 203)!;
        var (el, assetId, png) = ClipBaker.CreateElement("clip1", Mdi, cp, 3, 4, baked, 203);

        var package = new LabelPackage
        {
            Manifest = new Manifest { Id = "id", Name = "clip" },
            Document = new LabelDocument
            {
                Metadata = new LabelMetadata { Name = "clip" },
                Canvas = new Canvas { WidthMm = 20, HeightMm = 20, Dpi = 203 },
                Elements = [el],
            },
            Assets = new Dictionary<string, byte[]> { [assetId] = png },
        };

        using var ms = new MemoryStream();
        LabelPackageIo.Save(package, ms);
        ms.Position = 0;
        var loaded = LabelPackageIo.Load(ms);

        var loadedEl = Assert.IsType<ImageElement>(loaded.Document.Elements[0]);
        Assert.Equal(Mdi, loadedEl.Props.ClipFont);
        Assert.Equal(cp, loadedEl.Props.ClipCodepoint);
        Assert.Equal(203, loadedEl.Props.ClipBakeDpi);
        Assert.True(loaded.Assets.ContainsKey(assetId));
        Assert.Equal(png, loaded.Assets[assetId]);
    }

    [Fact]
    public void Rebake_keeps_the_asset_id_and_updates_dpi_and_size()
    {
        using var cat = OpenCatalog();
        var tf = cat.Resolve(Mdi)!;
        var cp = FirstGlyph(tf);
        var baked = ClipBaker.Bake(tf, cp, 10, 203)!;
        var (el, _, _) = ClipBaker.CreateElement("clip1", Mdi, cp, 3, 4, baked, 203);

        var rebaked = ClipBaker.Rebake(el, tf, nominalSizeMm: 20, dpi: 300);

        Assert.NotNull(rebaked);
        Assert.Equal(el.Props.AssetId, rebaked!.Value.Element.Props.AssetId);   // same asset → overwrite, no orphan
        Assert.Equal(300, rebaked.Value.Element.Props.ClipBakeDpi);
        Assert.True(rebaked.Value.Element.H > el.H, "a larger nominal size must grow the footprint");

        // A plain (non-clip) image has no codepoint → nothing to re-bake.
        var plain = new ImageElement { Id = "x", Props = new ImageProps { AssetId = "a" } };
        Assert.Null(ClipBaker.Rebake(plain, tf, 10, 203));
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────

    private static ClipFontCatalog OpenCatalog()
    {
        var dir = FindClipartDir();
        Assert.True(dir is not null, "could not locate src/Thermalith.Core/Fonts/Clipart");
        return new ClipFontCatalog(dir!);
    }

    private static int InkBits(MonochromeBitmap bmp)
    {
        var n = 0;
        foreach (var b in bmp.Packed) n += BitOperations.PopCount(b);
        return n;
    }

    private static int FirstGlyph(SKTypeface tf)
    {
        foreach (var (lo, hi) in new[] { (0xF0000, 0xF2FFF), (0xE000, 0xF8FF) })
            for (var cp = lo; cp <= hi; cp++)
                if (tf.ContainsGlyph(cp))
                    return cp;
        throw new InvalidOperationException($"no glyphs found in {tf.FamilyName}");
    }

    private static string? FindClipartDir()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var candidate = Path.Combine(d.FullName, "src", "Thermalith.Core", "Fonts", "Clipart");
            if (Directory.Exists(candidate)) return candidate;
            d = d.Parent;
        }
        return null;
    }
}
