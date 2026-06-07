using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Niimbot.Net.Encoding;

namespace Thermalith.App.Rendering;

/// <summary>
/// Converts Core's 1bpp <see cref="MonochromeBitmap"/> (the exact thing the printer burns) into an
/// Avalonia bitmap for the canvas. This is the "exact" preview of §6.3.6 — pure black/white, the
/// honest WYSIWYG default. (Thermal-tone simulation is later-phase polish.)
/// </summary>
public static class PreviewImage
{
    private const uint Burn = 0xFF000000; // opaque black (BGRA little-endian → 0xAARRGGBB packed)
    private const uint Paper = 0xFFFFFFFF; // opaque white

    public static Bitmap FromMonochrome(MonochromeBitmap mono)
    {
        var w = mono.WidthPx;
        var h = mono.HeightPx;
        var wb = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);

        using var fb = wb.Lock();
        var stride = fb.RowBytes;
        var row = new byte[stride];

        for (var y = 0; y < h; y++)
        {
            var src = mono.Row(y);
            for (var x = 0; x < w; x++)
            {
                var burn = (src[x >> 3] & (0x80 >> (x & 7))) != 0;
                var color = burn ? Burn : Paper;
                var o = x * 4;
                row[o + 0] = (byte)color;          // B
                row[o + 1] = (byte)(color >> 8);   // G
                row[o + 2] = (byte)(color >> 16);  // R
                row[o + 3] = (byte)(color >> 24);  // A
            }
            Marshal.Copy(row, 0, fb.Address + y * stride, stride);
        }

        return wb;
    }
}
