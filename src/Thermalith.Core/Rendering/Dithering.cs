namespace Thermalith.Core.Rendering;

/// <summary>
/// 1-bit reduction for images (build spec §6.3.2). Images — and only images — are dithered, to
/// their own <c>props.dither</c>, and converted to pure black/white <b>before</b> compositing so an
/// image's dithering never bleeds into neighbouring text. Input is an 8-bit grayscale buffer
/// (<c>0 = black … 255 = white</c>) sized <c>w×h</c>; output is a burn mask (<c>true = black</c>).
/// </summary>
public static class Dithering
{
    /// <summary>Reduce a grayscale buffer to a 1-bit burn mask using the named algorithm.</summary>
    public static bool[] Reduce(byte[] gray, int w, int h, string algorithm, int threshold)
    {
        return algorithm?.ToLowerInvariant() switch
        {
            "floydsteinberg" => ErrorDiffuse(gray, w, h, FloydSteinberg),
            "atkinson" => ErrorDiffuse(gray, w, h, Atkinson),
            "ordered" => Ordered(gray, w, h),
            "none" => Threshold(gray, 128),
            "threshold" => Threshold(gray, threshold),
            _ => ErrorDiffuse(gray, w, h, FloydSteinberg), // default per §11.8
        };
    }

    private static bool[] Threshold(byte[] gray, int threshold)
    {
        var mask = new bool[gray.Length];
        for (var i = 0; i < gray.Length; i++)
            mask[i] = gray[i] < threshold;
        return mask;
    }

    // 4×4 Bayer ordered dither — deterministic, no error propagation.
    private static readonly int[,] Bayer4 =
    {
        {  0,  8,  2, 10 },
        { 12,  4, 14,  6 },
        {  3, 11,  1,  9 },
        { 15,  7, 13,  5 },
    };

    private static bool[] Ordered(byte[] gray, int w, int h)
    {
        var mask = new bool[gray.Length];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var t = (Bayer4[y & 3, x & 3] + 1) * 256 / 17; // 0..255 thresholds
                mask[y * w + x] = gray[y * w + x] < t;
            }
        return mask;
    }

    // (dx, dy, weight, divisor) diffusion kernels.
    private static readonly (int dx, int dy, int w)[] FloydSteinberg =
        [(1, 0, 7), (-1, 1, 3), (0, 1, 5), (1, 1, 1)];
    private const int FloydSteinbergDiv = 16;

    private static readonly (int dx, int dy, int w)[] Atkinson =
        [(1, 0, 1), (2, 0, 1), (-1, 1, 1), (0, 1, 1), (1, 1, 1), (0, 2, 1)];
    private const int AtkinsonDiv = 8;

    private static bool[] ErrorDiffuse(byte[] gray, int w, int h, (int dx, int dy, int wt)[] kernel)
    {
        var div = kernel == Atkinson ? AtkinsonDiv : FloydSteinbergDiv;
        var buf = new float[gray.Length];
        for (var i = 0; i < gray.Length; i++) buf[i] = gray[i];

        var mask = new bool[gray.Length];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var idx = y * w + x;
                var old = buf[idx];
                var black = old < 128;
                mask[idx] = black;
                var newVal = black ? 0f : 255f;
                var err = old - newVal;
                foreach (var (dx, dy, wt) in kernel)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || nx >= w || ny >= h) continue;
                    buf[ny * w + nx] += err * wt / div;
                }
            }
        return mask;
    }
}
