using SkiaSharp;
using Thermalith.Core.Fonts;
using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using VerifyXunit;
using Xunit;

namespace Thermalith.Core.Tests;

public class RenderTests
{
    private static LabelDocument VectorLabel() => new()
    {
        Metadata = new LabelMetadata { Name = "vector" },
        Canvas = new Canvas { WidthMm = 40, HeightMm = 30, Dpi = 203 },
        Elements =
        [
            new ShapeElement { Id = "border", X = 0.5, Y = 0.5, W = 39, H = 29, Props = new ShapeProps { ShapeType = "rect", StrokeWidthMm = 0.4 } },
            new ShapeElement { Id = "rule", X = 2, Y = 15, W = 36, H = 0, Props = new ShapeProps { ShapeType = "line", StrokeWidthMm = 0.3 } },
            new BarcodeElement { Id = "bc", X = 2, Y = 2, W = 36, H = 9, Justify = new Justify { H = "center" },
                Props = new BarcodeProps { Symbology = "code128", Value = "THERMALITH", ShowText = false, ModuleWidthMm = 0.4, QuietZoneMm = 2 } },
            new QrElement { Id = "qr", X = 2, Y = 17, W = 12, H = 12,
                Props = new QrProps { Value = "https://glyphdeck.org", EcLevel = "M", QuietZoneMm = 1 } },
        ],
    };

    private static LabelDocument TextLabel() => new()
    {
        Metadata = new LabelMetadata { Name = "text" },
        Canvas = new Canvas { WidthMm = 50, HeightMm = 30, Dpi = 203 },
        Elements =
        [
            new TextElement { Id = "title", X = 2, Y = 2, W = 46, H = 8, Justify = new Justify { H = "left", V = "middle" },
                Props = new TextProps { Content = "Hello B1", FontFamily = FontService.BundledFamily, FontSizePt = 11, Bold = true, FontSizing = "fill", MinFontSizePt = 6, MaxFontSizePt = 14 } },
            new SerialElement { Id = "sn", X = 2, Y = 12, W = 20, H = 5, Justify = new Justify { H = "left", V = "middle" },
                Props = new SerialProps { Start = 1, Step = 1, PadLength = 5, Prefix = "SN" } },
            new DateTimeElement { Id = "dt", X = 2, Y = 20, W = 46, H = 5, Justify = new Justify { H = "left", V = "middle" },
                Props = new DateTimeProps { Kind = "datetime", Format = "yyyy-MM-dd HH:mm", Source = "fixed", FixedValueUtc = "2026-06-07T10:00:00Z" } },
        ],
    };

    private static ResolveContext FrozenContext => new() { Now = new DateTimeOffset(2026, 6, 7, 10, 0, 0, TimeSpan.Zero), RowIndex = 0 };

    [Fact]
    public Task Vector_label_renders_to_expected_1bpp()
    {
        using var fonts = new FontService();
        var renderer = new LabelRenderer(fonts);
        var bmp = renderer.Render(VectorLabel(), new ResolveContext());
        return Verifier.Verify(TestSupport.Snapshot(bmp));
    }

    [Fact]
    public Task Text_label_renders_to_expected_1bpp()
    {
        using var fonts = new FontService();
        var renderer = new LabelRenderer(fonts);
        var bmp = renderer.Render(TextLabel(), FrozenContext);
        return Verifier.Verify(TestSupport.Snapshot(bmp));
    }

    [Fact]
    public void Render_is_deterministic_for_identical_inputs()
    {
        using var fonts = new FontService();
        var renderer = new LabelRenderer(fonts);

        var a = renderer.Render(TextLabel(), FrozenContext);
        var b = renderer.Render(TextLabel(), FrozenContext);

        Assert.Equal(a.WidthPx, b.WidthPx);
        Assert.Equal(a.HeightPx, b.HeightPx);
        Assert.Equal(a.Packed, b.Packed);
    }

    [Fact]
    public void Serial_advances_with_row_index()
    {
        using var fonts = new FontService();
        var renderer = new LabelRenderer(fonts);

        var row0 = renderer.Render(TextLabel(), FrozenContext with { RowIndex = 0 });
        var row1 = renderer.Render(TextLabel(), FrozenContext with { RowIndex = 1 });

        // A different serial number must change the raster.
        Assert.NotEqual(row0.Packed, row1.Packed);
    }

    [Fact]
    public void Dimensions_follow_canvas_dpi_not_hardcoded_8pxmm()
    {
        using var fonts = new FontService();
        var renderer = new LabelRenderer(fonts);

        var at203 = renderer.Render(VectorLabel(), new ResolveContext());
        var doc300 = VectorLabel() with { Canvas = VectorLabel().Canvas with { Dpi = 300 } };
        var at300 = renderer.Render(doc300, new ResolveContext());

        Assert.Equal((int)Math.Ceiling(40 * 203 / 25.4), at203.WidthPx);
        Assert.Equal((int)Math.Ceiling(40 * 300 / 25.4), at300.WidthPx);
    }

    [Fact]
    public void Orientation_90_swaps_dimensions()
    {
        using var fonts = new FontService();
        var renderer = new LabelRenderer(fonts);

        var portrait = renderer.Render(VectorLabel(), new ResolveContext());
        var rotated = renderer.Render(VectorLabel() with { Canvas = VectorLabel().Canvas with { OrientationDeg = 90 } }, new ResolveContext());

        Assert.Equal(portrait.WidthPx, rotated.HeightPx);
        Assert.Equal(portrait.HeightPx, rotated.WidthPx);
    }

    // An asymmetric source (black top-left quadrant only) so each orthogonal transform is observable.
    private static byte[] AsymPng(int w = 8, int h = 8)
    {
        using var bmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                bmp.SetPixel(x, y, x < w / 2 && y < h / 2 ? SKColors.Black : SKColors.White);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static LabelDocument ImageLabel(int rotateQuarters, bool flipH, bool flipV) => new()
    {
        Metadata = new LabelMetadata { Name = "img" },
        Canvas = new Canvas { WidthMm = 20, HeightMm = 20, Dpi = 203 },
        Elements =
        [
            new ImageElement { Id = "i", X = 2, Y = 2, W = 16, H = 16,
                Props = new ImageProps { AssetId = "img", Fit = "stretch", Dither = "threshold", Threshold = 128,
                    RotateQuarters = rotateQuarters, FlipH = flipH, FlipV = flipV } },
        ],
    };

    [Fact]
    public void Image_transform_rotate_180_equals_mirror_plus_flip()
    {
        using var fonts = new FontService();
        var renderer = new LabelRenderer(fonts);
        var ctx = new ResolveContext { Assets = new Dictionary<string, byte[]> { ["img"] = AsymPng() } };

        var identity = renderer.Render(ImageLabel(0, false, false), ctx);
        var rot180 = renderer.Render(ImageLabel(2, false, false), ctx);
        var flipHV = renderer.Render(ImageLabel(0, true, true), ctx);

        Assert.NotEqual(identity.Packed, rot180.Packed);   // the transform actually changes the raster
        Assert.Equal(rot180.Packed, flipHV.Packed);        // 180° rotation ≡ mirror + flip
    }
}
