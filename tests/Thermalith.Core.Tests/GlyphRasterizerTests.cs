using SkiaSharp;
using Thermalith.Core.Fonts;
using Thermalith.Core.Rendering;
using Xunit;
using Xunit.Abstractions;

namespace Thermalith.Core.Tests;

public class GlyphRasterizerTests
{
    private readonly ITestOutputHelper _out;
    public GlyphRasterizerTests(ITestOutputHelper output) => _out = output;

    // ── Deterministic unit coverage (uses the embedded Roboto — no external files) ──────────────

    [Fact]
    public void Rasterizes_a_glyph_to_a_cropped_monochrome_raster()
    {
        using var fonts = new FontService();
        var roboto = fonts.Resolve(FontService.BundledFamily, bold: false, italic: false);

        var g = GlyphRasterizer.Rasterize(roboto, 'R', renderPx: 64);

        Assert.NotNull(g);
        Assert.True(g!.WidthPx is > 0 and < 96, $"width {g.WidthPx}");
        Assert.True(g.HeightPx is > 0 and < 96, $"height {g.HeightPx}");
        Assert.False(g.HasColor, "an outline font must rasterize monochrome");
        Assert.True(InkPixels(g) > 0, "the glyph must leave ink");
        // A valid, decodable PNG.
        using var decoded = SKBitmap.Decode(g.Png);
        Assert.NotNull(decoded);
    }

    [Fact]
    public void Blank_and_absent_glyphs_return_null()
    {
        using var fonts = new FontService();
        var roboto = fonts.Resolve(FontService.BundledFamily, false, false);

        Assert.Null(GlyphRasterizer.Rasterize(roboto, ' ', 64));        // whitespace → nothing to place
        Assert.Null(GlyphRasterizer.Rasterize(roboto, 0xF0001, 64));    // PUA icon codepoint Roboto lacks
    }

    [Fact]
    public void Larger_render_size_yields_a_larger_raster()
    {
        using var fonts = new FontService();
        var roboto = fonts.Resolve(FontService.BundledFamily, false, false);

        var small = GlyphRasterizer.Rasterize(roboto, 'R', 32)!;
        var big = GlyphRasterizer.Rasterize(roboto, 'R', 128)!;

        Assert.True(big.HeightPx > small.HeightPx, $"{big.HeightPx} !> {small.HeightPx}");
    }

    // ── Visual sample emitter for the bundled clip fonts (Richard's hands-on check) ─────────────
    // Loads each staged clip font, rasterizes a handful of real glyphs, and writes them to a temp
    // folder to eyeball. Doubles as a load-smoke for all 7 fonts and answers the open colour-emoji
    // question by logging whether Noto Color Emoji actually rendered in colour.

    [Fact]
    public void Emits_clipart_font_samples_for_review()
    {
        var dir = FindClipartDir();
        Assert.True(dir is not null, "could not locate src/Thermalith.Core/Fonts/Clipart from the test output dir");

        var outRoot = Path.Combine(Path.GetTempPath(), "thermalith-clipart-samples");
        Directory.CreateDirectory(outRoot);
        _out.WriteLine($"samples → {outRoot}");

        var fonts = Directory.GetFiles(dir!, "*.ttf");
        Assert.NotEmpty(fonts);

        foreach (var path in fonts)
        {
            using var tf = SKTypeface.FromFile(path);
            Assert.True(tf is not null, $"failed to load {Path.GetFileName(path)}");

            var name = Path.GetFileNameWithoutExtension(path);
            var fontOut = Path.Combine(outRoot, name);
            Directory.CreateDirectory(fontOut);

            var codepoints = SampleCodepoints(tf!, max: 8);
            Assert.True(codepoints.Count > 0, $"{name}: no glyphs found to sample");

            var anyColor = false;
            foreach (var cp in codepoints)
            {
                var g = GlyphRasterizer.Rasterize(tf!, cp, renderPx: 128);
                if (g is null) continue;
                anyColor |= g.HasColor;
                File.WriteAllBytes(Path.Combine(fontOut, $"U+{cp:X4}.png"), g.Png);
            }
            _out.WriteLine($"{name,-34} glyphs sampled: {codepoints.Count,-3} colour: {anyColor}");
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────

    private static int InkPixels(GlyphRasterizer.GlyphRaster g)
    {
        using var bmp = SKBitmap.Decode(g.Png);
        var n = 0;
        for (var y = 0; y < bmp.Height; y++)
            for (var x = 0; x < bmp.Width; x++)
            {
                var c = bmp.GetPixel(x, y);
                if ((c.Red * 299 + c.Green * 587 + c.Blue * 114) / 1000 < 128) n++;
            }
        return n;
    }

    // Probe the codepoint ranges icon + emoji fonts actually use (PUA, supplementary PUA, and the
    // emoji blocks) and collect the first present glyphs — no per-font hardcoded codepoints.
    private static List<int> SampleCodepoints(SKTypeface tf, int max)
    {
        (int Lo, int Hi)[] ranges =
        [
            (0x1F300, 0x1FAFF), // emoji pictographs (put first so colour emoji get sampled)
            (0x2600, 0x27BF),   // misc symbols + dingbats
            (0xE000, 0xF8FF),   // Private Use Area (most icon fonts)
            (0xF0000, 0xF2FFF), // supplementary PUA (MDI)
        ];
        var found = new List<int>();
        foreach (var (lo, hi) in ranges)
        {
            for (var cp = lo; cp <= hi && found.Count < max; cp++)
                if (tf.ContainsGlyph(cp))
                    found.Add(cp);
            if (found.Count >= max) break;
        }
        return found;
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
