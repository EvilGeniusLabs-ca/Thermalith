using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Niimbot.Net.Encoding;
using SkiaSharp;

namespace Thermalith.Core.Tests;

internal static class TestSupport
{
    /// <summary>The worked 50×30 kitchen-sink example from label-json-spec §12, embedded as a test resource.</summary>
    public static string KitchenSinkJson()
    {
        var asm = Assembly.GetExecutingAssembly();
        const string name = "Thermalith.Core.Tests.Resources.kitchen-sink-50x30.json";
        using var s = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Missing test resource '{name}'.");
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }

    /// <summary>A deterministic tiny PNG asset for package/render tests.</summary>
    public static byte[] SamplePng(int w = 8, int h = 8)
    {
        using var bmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                bmp.SetPixel(x, y, ((x + y) & 1) == 0 ? SKColors.Black : SKColors.White);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>A compact, stable snapshot view of a 1bpp raster: dimensions, content hash, and an ASCII thumbnail.</summary>
    public static object Snapshot(MonochromeBitmap bmp) => new
    {
        bmp.WidthPx,
        bmp.HeightPx,
        bmp.BytesPerRow,
        Sha256 = Convert.ToHexString(SHA256.HashData(bmp.Packed)).ToLowerInvariant(),
        BurnPixels = CountBurn(bmp),
        Ascii = Thumbnail(bmp),
    };

    private static int CountBurn(MonochromeBitmap bmp)
    {
        var count = 0;
        for (var y = 0; y < bmp.HeightPx; y++)
            for (var x = 0; x < bmp.WidthPx; x++)
                if (IsBurn(bmp, x, y)) count++;
        return count;
    }

    private static bool IsBurn(MonochromeBitmap bmp, int x, int y) =>
        (bmp.Row(y)[x >> 3] & (0x80 >> (x & 7))) != 0;

    // Downsample to <=64 columns of block-coverage characters for human-reviewable snapshots.
    private static string Thumbnail(MonochromeBitmap bmp)
    {
        const int maxW = 64;
        var step = Math.Max(1, (int)Math.Ceiling(bmp.WidthPx / (double)maxW));
        var sb = new StringBuilder();
        const string ramp = " .:-=+*#%@";
        for (var y = 0; y < bmp.HeightPx; y += step)
        {
            for (var x = 0; x < bmp.WidthPx; x += step)
            {
                int on = 0, total = 0;
                for (var dy = 0; dy < step && y + dy < bmp.HeightPx; dy++)
                    for (var dx = 0; dx < step && x + dx < bmp.WidthPx; dx++)
                    {
                        total++;
                        if (IsBurn(bmp, x + dx, y + dy)) on++;
                    }
                var level = total == 0 ? 0 : on * (ramp.Length - 1) / total;
                sb.Append(ramp[level]);
            }
            sb.Append('\n');
        }
        return sb.ToString();
    }
}
