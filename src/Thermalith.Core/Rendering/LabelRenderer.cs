using Niimbot.Net.Encoding;
using SkiaSharp;
using Thermalith.Core.Fonts;
using Thermalith.Core.Model;

namespace Thermalith.Core.Rendering;

/// <summary>
/// The render pipeline (build spec §6.3): turns a <see cref="ResolvedLabel"/> into the exact 1bpp
/// raster <c>Niimbot.Net</c> prints, SkiaSharp-only (no Avalonia) so the headless server renders
/// identically (WYSIWYG). Stages MEASURE → RASTER (8-bit gray) → MONOCHROME, honouring the
/// crisp-vs-dither rule (§6.3.2): images dither to their own algorithm before compositing; text,
/// barcodes, QR and shapes stay crisp (AA threshold for text, AA-off integer-px snapping for codes,
/// §6.3.3). Pure given <c>(ResolvedLabel, renderOptions)</c> (§6.3.7).
/// </summary>
public sealed class LabelRenderer
{
    private readonly FontService _fonts;

    public LabelRenderer(FontService? fonts = null) => _fonts = fonts ?? new FontService();

    /// <summary>Resolve then render in one call.</summary>
    public MonochromeBitmap Render(LabelDocument doc, ResolveContext resolve, RenderOptions? options = null) =>
        Render(LabelResolver.Resolve(doc, resolve), options);

    /// <summary>Render a fully-resolved label to the model-agnostic 1bpp hand-off raster (§6.3.5).</summary>
    public MonochromeBitmap Render(ResolvedLabel label, RenderOptions? options = null)
    {
        options ??= RenderOptions.Default;
        var canvas = label.Canvas;
        var pxPerMm = canvas.Dpi / 25.4;

        var w = Math.Max(1, (int)Math.Ceiling(canvas.WidthMm * pxPerMm));
        var h = Math.Max(1, (int)Math.Ceiling(canvas.HeightMm * pxPerMm));

        using var bmp = new SKBitmap(w, h, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(SKColors.White);
            var ctx = new DrawContext(c, pxPerMm);
            foreach (var el in label.Elements) // already in z-order (back→front)
                DrawElement(ctx, el);
        }

        // Stage 4: threshold to a burn mask, then apply whole-label orientation by exact pixel remap.
        var burn = Threshold(bmp, w, h, options.TextThreshold);
        return Orient(burn, w, h, canvas.OrientationDeg);
    }

    private readonly record struct DrawContext(SKCanvas Canvas, double PxPerMm)
    {
        public float Px(double mm) => (float)(mm * PxPerMm);
    }

    private void DrawElement(DrawContext ctx, ResolvedElement el)
    {
        var rotate = Math.Abs(el.RotationDeg) > 1e-9;
        if (rotate)
        {
            ctx.Canvas.Save();
            ctx.Canvas.RotateDegrees((float)el.RotationDeg, ctx.Px(el.XMm + el.WMm / 2), ctx.Px(el.YMm + el.HMm / 2));
        }

        switch (el)
        {
            case ResolvedText t: DrawText(ctx, t); break;
            case ResolvedShape s: DrawShape(ctx, s); break;
            case ResolvedBarcode b: DrawBarcode(ctx, b); break;
            case ResolvedQr q: DrawQr(ctx, q); break;
            case ResolvedImage im: DrawImage(ctx, im); break;
            case ResolvedTable tab: DrawTable(ctx, tab); break;
        }

        if (rotate) ctx.Canvas.Restore();
    }

    // ── Text ────────────────────────────────────────────────────────────────────────────────

    private void DrawText(DrawContext ctx, ResolvedText t)
    {
        if (string.IsNullOrEmpty(t.Text)) return;
        var boxW = ctx.Px(t.WMm);
        var boxH = ctx.Px(t.HMm);
        var dpi = ctx.PxPerMm * 25.4;

        using var tf = _fonts.Resolve(t.Style.FontFamily, t.Style.Bold, t.Style.Italic);
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,           // text: AA on → threshold (§6.3.3)
            Typeface = tf,
            FakeBoldText = t.Style.Bold && !TypefaceIsBold(tf),
            TextSkewX = t.Style.Italic && !tf.IsItalic ? -0.22f : 0f,
            SubpixelText = false,
        };
        var letterSpacingPx = (float)(t.Style.LetterSpacing * dpi / 72.0);

        var sizePt = FitFontSize(paint, t, boxW, boxH, letterSpacingPx, dpi);
        paint.TextSize = (float)(sizePt * dpi / 72.0);

        var lines = WrapLines(paint, t.Text, boxW, letterSpacingPx, t.Wrap != "none");
        var metrics = paint.FontMetrics;
        var lineH = (metrics.Descent - metrics.Ascent) * (float)t.Style.LineSpacing;
        var totalH = lineH * lines.Count;

        var top = ctx.Px(t.YMm) + t.Justify.V switch
        {
            "middle" => (boxH - totalH) / 2,
            "bottom" => boxH - totalH,
            _ => 0f,
        };

        var x0 = ctx.Px(t.XMm);
        for (var i = 0; i < lines.Count; i++)
        {
            var baseline = top + i * lineH - metrics.Ascent;
            var lineW = MeasureLine(paint, lines[i], letterSpacingPx);
            var x = x0 + t.Justify.H switch
            {
                "center" => (boxW - lineW) / 2,
                "right" => boxW - lineW,
                _ => 0f,
            };
            DrawLineText(ctx.Canvas, lines[i], x, baseline, paint, letterSpacingPx);
            if (t.Style.Underline)
            {
                using var ul = new SKPaint { Color = SKColors.Black, IsAntialias = true, StrokeWidth = Math.Max(1, paint.TextSize / 14f) };
                var uy = baseline - metrics.Descent / 2;
                ctx.Canvas.DrawLine(x, uy, x + lineW, uy, ul);
            }
        }
    }

    private static bool TypefaceIsBold(SKTypeface tf) => tf.FontWeight >= (int)SKFontStyleWeight.SemiBold;

    private static void DrawLineText(SKCanvas canvas, string line, float x, float baseline, SKPaint paint, float letterSpacing)
    {
        if (letterSpacing == 0f)
        {
            canvas.DrawText(line, x, baseline, paint);
            return;
        }
        var cursor = x;
        foreach (var ch in line)
        {
            var s = ch.ToString();
            canvas.DrawText(s, cursor, baseline, paint);
            cursor += paint.MeasureText(s) + letterSpacing;
        }
    }

    private static float MeasureLine(SKPaint paint, string line, float letterSpacing)
    {
        if (letterSpacing == 0f || line.Length == 0) return paint.MeasureText(line);
        var w = 0f;
        foreach (var ch in line) w += paint.MeasureText(ch.ToString()) + letterSpacing;
        return w - letterSpacing;
    }

    // Auto-size (§6.2): binary-search the fitted point size in quarter-point steps for determinism.
    private static double FitFontSize(SKPaint paint, ResolvedText t, float boxW, float boxH, float letterSpacing, double dpi)
    {
        if (t.FontSizing is not ("shrink" or "fill"))
            return t.Style.FontSizePt;

        var minPt = t.MinFontSizePt ?? 4.0;
        var maxPt = t.FontSizing == "shrink"
            ? t.Style.FontSizePt
            : t.MaxFontSizePt ?? 200.0;
        if (maxPt < minPt) (minPt, maxPt) = (maxPt, minPt);

        int lo = (int)Math.Round(minPt * 4), hi = (int)Math.Round(maxPt * 4);
        var best = lo;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            paint.TextSize = (float)(mid / 4.0 * dpi / 72.0);
            if (Fits(paint, t, boxW, boxH, letterSpacing))
            {
                best = mid;
                lo = mid + 1;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return best / 4.0;
    }

    private static bool Fits(SKPaint paint, ResolvedText t, float boxW, float boxH, float letterSpacing)
    {
        var wrap = t.Wrap != "none";
        var lines = WrapLines(paint, t.Text, boxW, letterSpacing, wrap);
        var m = paint.FontMetrics;
        var lineH = (m.Descent - m.Ascent) * (float)t.Style.LineSpacing;
        if (lineH * lines.Count > boxH) return false;
        foreach (var line in lines)
            if (MeasureLine(paint, line, letterSpacing) > boxW + 0.5f) return false;
        return true;
    }

    private static List<string> WrapLines(SKPaint paint, string text, float maxWidth, float letterSpacing, bool wrapWords)
    {
        var result = new List<string>();
        foreach (var paragraph in text.Replace("\r\n", "\n").Split('\n'))
        {
            if (!wrapWords)
            {
                result.Add(paragraph);
                continue;
            }
            WrapParagraph(paint, paragraph, maxWidth, letterSpacing, result);
        }
        return result;
    }

    private static void WrapParagraph(SKPaint paint, string paragraph, float maxWidth, float letterSpacing, List<string> result)
    {
        if (paragraph.Length == 0) { result.Add(""); return; }
        var words = paragraph.Split(' ');
        var line = "";
        foreach (var word in words)
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (MeasureLine(paint, candidate, letterSpacing) <= maxWidth || line.Length == 0)
            {
                // A single word longer than the box gets hard-broken by character.
                if (line.Length == 0 && MeasureLine(paint, word, letterSpacing) > maxWidth)
                {
                    BreakWord(paint, word, maxWidth, letterSpacing, result, ref line);
                    continue;
                }
                line = candidate;
            }
            else
            {
                result.Add(line);
                line = word;
            }
        }
        if (line.Length > 0) result.Add(line);
    }

    private static void BreakWord(SKPaint paint, string word, float maxWidth, float letterSpacing, List<string> result, ref string line)
    {
        var chunk = "";
        foreach (var ch in word)
        {
            var candidate = chunk + ch;
            if (MeasureLine(paint, candidate, letterSpacing) > maxWidth && chunk.Length > 0)
            {
                result.Add(chunk);
                chunk = ch.ToString();
            }
            else
            {
                chunk = candidate;
            }
        }
        line = chunk;
    }

    // ── Shapes ──────────────────────────────────────────────────────────────────────────────

    private static void DrawShape(DrawContext ctx, ResolvedShape s)
    {
        var x = ctx.Px(s.XMm);
        var y = ctx.Px(s.YMm);
        var w = ctx.Px(s.WMm);
        var h = ctx.Px(s.HMm);
        var stroke = Math.Max(1f, (float)Math.Round(s.StrokeWidthMm * ctx.PxPerMm));

        using var fill = new SKPaint { Color = SKColors.Black, IsAntialias = false, Style = SKPaintStyle.Fill };
        using var line = new SKPaint { Color = SKColors.Black, IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = stroke };

        switch (s.ShapeType?.ToLowerInvariant())
        {
            case "line":
                ctx.Canvas.DrawLine(x, y, x + w, y + h, line);
                break;
            case "ellipse":
                var oval = new SKRect(x, y, x + w, y + h);
                if (s.Fill) ctx.Canvas.DrawOval(oval, fill);
                else ctx.Canvas.DrawOval(Inset(oval, stroke / 2), line);
                break;
            case "roundedrect":
                var rr = new SKRect(x, y, x + w, y + h);
                var r = (float)(s.CornerRadiusMm * ctx.PxPerMm);
                if (s.Fill) ctx.Canvas.DrawRoundRect(rr, r, r, fill);
                else ctx.Canvas.DrawRoundRect(Inset(rr, stroke / 2), r, r, line);
                break;
            default: // rect
                var rect = new SKRect(x, y, x + w, y + h);
                if (s.Fill) ctx.Canvas.DrawRect(rect, fill);
                else ctx.Canvas.DrawRect(Inset(rect, stroke / 2), line);
                break;
        }
    }

    private static SKRect Inset(SKRect r, float by) => new(r.Left + by, r.Top + by, r.Right - by, r.Bottom - by);

    // ── Barcode ─────────────────────────────────────────────────────────────────────────────

    private void DrawBarcode(DrawContext ctx, ResolvedBarcode b)
    {
        if (string.IsNullOrEmpty(b.Value)) return;
        bool[] pattern;
        try { pattern = Barcodes.Encode1D(b.Symbology, b.Value); }
        catch { return; } // invalid payload — validator surfaces it; don't crash the render

        var x = ctx.Px(b.XMm);
        var y = ctx.Px(b.YMm);
        var w = ctx.Px(b.WMm);
        var h = ctx.Px(b.HMm);

        var moduleW = Math.Max(1, (int)Math.Round(b.ModuleWidthMm * ctx.PxPerMm));
        var quiet = (int)Math.Round(b.QuietZoneMm * ctx.PxPerMm);
        var codeW = pattern.Length * moduleW + 2 * quiet;

        var showText = b.ShowText && b.TextPosition != "none";
        var dpi = ctx.PxPerMm * 25.4;
        var textSizePt = b.TextStyle.FontSizePt;
        using var textPaint = showText ? new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = _fonts.Resolve(b.TextStyle.FontFamily, false, false),
            TextSize = (float)(textSizePt * dpi / 72.0),
        } : null;
        var textH = showText ? textPaint!.FontMetrics.Descent - textPaint.FontMetrics.Ascent : 0f;

        var barsH = h - textH;
        if (barsH < 1) barsH = h;

        var ox = x + b.Justify.H switch
        {
            "center" => (w - codeW) / 2,
            "right" => w - codeW,
            _ => 0f,
        };
        var above = b.TextPosition == "above";
        var barsTop = above ? y + textH : y;

        using var bar = new SKPaint { Color = SKColors.Black, IsAntialias = false, Style = SKPaintStyle.Fill };
        var cursor = ox + quiet;
        foreach (var on in pattern)
        {
            if (on) ctx.Canvas.DrawRect(cursor, barsTop, moduleW, barsH, bar);
            cursor += moduleW;
        }

        if (showText)
        {
            var tw = textPaint!.MeasureText(b.Value);
            var tx = ox + (codeW - tw) / 2;
            var ty = above ? y - textPaint.FontMetrics.Ascent : barsTop + barsH - textPaint.FontMetrics.Descent;
            ctx.Canvas.DrawText(b.Value, tx, ty, textPaint);
        }
    }

    // ── QR ──────────────────────────────────────────────────────────────────────────────────

    private static void DrawQr(DrawContext ctx, ResolvedQr q)
    {
        if (string.IsNullOrEmpty(q.Value)) return;
        bool[,] modules;
        try { modules = Barcodes.EncodeQr(q.Value, q.Encoding, q.EcLevel); }
        catch { return; }

        var n = modules.GetLength(0);
        var x = ctx.Px(q.XMm);
        var y = ctx.Px(q.YMm);
        var w = ctx.Px(q.WMm);
        var h = ctx.Px(q.HMm);
        var quiet = (int)Math.Round(q.QuietZoneMm * ctx.PxPerMm);

        int moduleSize;
        if (q.ModuleSizeMm is { } ms)
        {
            moduleSize = Math.Max(1, (int)Math.Round(ms * ctx.PxPerMm));
        }
        else
        {
            var avail = (int)Math.Floor(Math.Min(w, h)) - 2 * quiet;
            moduleSize = Math.Max(1, avail / n);
        }

        var total = n * moduleSize + 2 * quiet;
        var ox = x + (w - total) / 2;
        var oy = y + (h - total) / 2;

        using var dot = new SKPaint { Color = SKColors.Black, IsAntialias = false, Style = SKPaintStyle.Fill };
        for (var my = 0; my < n; my++)
            for (var mx = 0; mx < n; mx++)
                if (modules[mx, my])
                    ctx.Canvas.DrawRect(ox + quiet + mx * moduleSize, oy + quiet + my * moduleSize, moduleSize, moduleSize, dot);
    }

    // ── Image ───────────────────────────────────────────────────────────────────────────────

    private static void DrawImage(DrawContext ctx, ResolvedImage im)
    {
        var dx = ctx.Px(im.XMm);
        var dy = ctx.Px(im.YMm);
        var dw = Math.Max(1, (int)Math.Round(im.WMm * ctx.PxPerMm));
        var dh = Math.Max(1, (int)Math.Round(im.HMm * ctx.PxPerMm));

        if (im.Data is null)
        {
            using var outline = new SKPaint { Color = SKColors.Black, IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
            ctx.Canvas.DrawRect(dx, dy, dw, dh, outline);
            return;
        }

        using var src = SKBitmap.Decode(im.Data);
        if (src is null) return;

        // Rasterize the fitted image into an opaque grayscale-able target, then dither to 1-bit.
        using var target = new SKBitmap(dw, dh, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using (var tc = new SKCanvas(target))
        {
            tc.Clear(SKColors.White);
            var dest = FitRect(im.Fit, src.Width, src.Height, dw, dh);
            using var p = new SKPaint { FilterQuality = SKFilterQuality.Medium, IsAntialias = true };
            tc.DrawBitmap(src, dest, p);
        }

        var gray = new byte[dw * dh];
        for (var py = 0; py < dh; py++)
            for (var px = 0; px < dw; px++)
            {
                var c = target.GetPixel(px, py);
                var lum = (byte)((c.Red * 299 + c.Green * 587 + c.Blue * 114) / 1000);
                if (im.Invert) lum = (byte)(255 - lum);
                gray[py * dw + px] = lum;
            }

        var mask = Dithering.Reduce(gray, dw, dh, im.Dither, im.Threshold);

        using var bilevel = new SKBitmap(dw, dh, SKColorType.Bgra8888, SKAlphaType.Opaque);
        for (var py = 0; py < dh; py++)
            for (var px = 0; px < dw; px++)
                bilevel.SetPixel(px, py, mask[py * dw + px] ? SKColors.Black : SKColors.White);

        // Drawn 1:1 at integer offset → stays bi-level (unless the element is rotated, which the
        // stage-4 threshold re-binarizes anyway).
        using var blit = new SKPaint { FilterQuality = SKFilterQuality.None, IsAntialias = false };
        ctx.Canvas.DrawBitmap(bilevel, dx, dy, blit);
    }

    private static SKRect FitRect(string fit, int srcW, int srcH, int dw, int dh)
    {
        switch (fit?.ToLowerInvariant())
        {
            case "stretch":
                return new SKRect(0, 0, dw, dh);
            case "center":
            {
                var x = (dw - srcW) / 2f;
                var y = (dh - srcH) / 2f;
                return new SKRect(x, y, x + srcW, y + srcH);
            }
            case "fill":
            {
                var scale = Math.Max((float)dw / srcW, (float)dh / srcH);
                var sw = srcW * scale;
                var sh = srcH * scale;
                var x = (dw - sw) / 2f;
                var y = (dh - sh) / 2f;
                return new SKRect(x, y, x + sw, y + sh);
            }
            default: // fit (contain)
            {
                var scale = Math.Min((float)dw / srcW, (float)dh / srcH);
                var sw = srcW * scale;
                var sh = srcH * scale;
                var x = (dw - sw) / 2f;
                var y = (dh - sh) / 2f;
                return new SKRect(x, y, x + sw, y + sh);
            }
        }
    }

    // ── Table ───────────────────────────────────────────────────────────────────────────────

    private void DrawTable(DrawContext ctx, ResolvedTable tab)
    {
        if (tab.Cols <= 0 || tab.Rows <= 0) return;
        var x = ctx.Px(tab.XMm);
        var y = ctx.Px(tab.YMm);
        var w = ctx.Px(tab.WMm);
        var h = ctx.Px(tab.HMm);
        var dpi = ctx.PxPerMm * 25.4;

        var colW = AxisSizes(tab.ColumnWidthsMm, tab.Cols, w, ctx.PxPerMm);
        var rowH = AxisSizes(tab.RowHeightsMm, tab.Rows, h, ctx.PxPerMm);

        var border = tab.BorderWidthMm > 0 ? Math.Max(1f, (float)Math.Round(tab.BorderWidthMm * ctx.PxPerMm)) : 0f;
        using var borderPaint = border > 0 ? new SKPaint { Color = SKColors.Black, IsAntialias = false, Style = SKPaintStyle.Stroke, StrokeWidth = border } : null;

        var cellY = y;
        for (var r = 0; r < tab.Rows; r++)
        {
            var cellX = x;
            for (var c = 0; c < tab.Cols; c++)
            {
                var rect = new SKRect(cellX, cellY, cellX + colW[c], cellY + rowH[r]);
                if (borderPaint is not null) ctx.Canvas.DrawRect(rect, borderPaint);

                var cell = r < tab.Cells.Length && c < tab.Cells[r].Length ? tab.Cells[r][c] : null;
                if (cell is not null && cell.Text.Length > 0)
                    DrawCellText(ctx, cell, rect, tab.Style, tab.HeaderRow && r == 0, dpi);
                cellX += colW[c];
            }
            cellY += rowH[r];
        }
    }

    private void DrawCellText(DrawContext ctx, ResolvedCell cell, SKRect rect, ResolvedTextStyle style, bool header, double dpi)
    {
        using var paint = new SKPaint
        {
            Color = SKColors.Black,
            IsAntialias = true,
            Typeface = _fonts.Resolve(style.FontFamily, header || style.Bold, style.Italic),
            FakeBoldText = header,
            TextSize = (float)(style.FontSizePt * dpi / 72.0),
        };
        const float pad = 1.5f;
        var m = paint.FontMetrics;
        var tw = paint.MeasureText(cell.Text);
        var tx = cell.Justify.H switch
        {
            "center" => rect.Left + (rect.Width - tw) / 2,
            "right" => rect.Right - tw - pad,
            _ => rect.Left + pad,
        };
        var lineH = m.Descent - m.Ascent;
        var ty = cell.Justify.V switch
        {
            "top" => rect.Top - m.Ascent + pad,
            "bottom" => rect.Bottom - m.Descent - pad,
            _ => rect.MidY + (lineH / 2 - m.Descent),
        };
        ctx.Canvas.DrawText(cell.Text, tx, ty, paint);
    }

    private static float[] AxisSizes(double[]? explicitMm, int count, float totalPx, double pxPerMm)
    {
        var sizes = new float[count];
        if (explicitMm is not null && explicitMm.Length == count)
        {
            for (var i = 0; i < count; i++) sizes[i] = (float)(explicitMm[i] * pxPerMm);
        }
        else
        {
            var each = totalPx / count;
            for (var i = 0; i < count; i++) sizes[i] = each;
        }
        return sizes;
    }

    // ── Stage 4: monochrome + orientation ─────────────────────────────────────────────────────

    private static bool[] Threshold(SKBitmap bmp, int w, int h, int threshold)
    {
        var burn = new bool[w * h];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var c = bmp.GetPixel(x, y);
                var lum = (c.Red * 299 + c.Green * 587 + c.Blue * 114) / 1000;
                burn[y * w + x] = lum < threshold;
            }
        return burn;
    }

    private static MonochromeBitmap Orient(bool[] burn, int w, int h, int orientationDeg)
    {
        var deg = ((orientationDeg % 360) + 360) % 360;
        int ow, oh;
        Func<int, int, (int X, int Y)> map; // (x,y) in source → (x,y) in oriented
        switch (deg)
        {
            case 90: ow = h; oh = w; map = (x, y) => (h - 1 - y, x); break;
            case 180: ow = w; oh = h; map = (x, y) => (w - 1 - x, h - 1 - y); break;
            case 270: ow = h; oh = w; map = (x, y) => (y, w - 1 - x); break;
            default: ow = w; oh = h; map = (x, y) => (x, y); break;
        }

        var bytesPerRow = (ow + 7) / 8;
        var packed = new byte[bytesPerRow * oh];
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (!burn[y * w + x]) continue;
                var (ox, oy) = map(x, y);
                packed[oy * bytesPerRow + (ox >> 3)] |= (byte)(0x80 >> (ox & 7));
            }

        return new MonochromeBitmap(ow, oh, packed);
    }
}
