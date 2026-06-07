using SkiaSharp;
using Thermalith.Core.Fonts;
using Thermalith.Core.Model;
using Thermalith.Core.Rendering;
using Thermalith.Core.Tokens;

namespace Thermalith.Core.Validation;

/// <summary>
/// The default <see cref="ILabelValidator"/> (build spec §6.7). Rules: duplicate ids; undeclared /
/// required-unresolved tokens; barcode-QR module &lt; 1 px at the canvas DPI (§6.3.3); invalid
/// barcode/QR payloads; text overflow past the min-font floor; content outside the canvas/bleed
/// (error) or the safe area (warning, §6.1.3); missing asset references; substituted fonts (§6.3.4).
/// </summary>
public sealed class LabelValidator : ILabelValidator
{
    public ValidationResult Validate(LabelDocument document, ValidationContext? context = null)
    {
        var ctx = context ?? new ValidationContext();
        var diags = new List<ValidationDiagnostic>();
        var canvas = document.Canvas;
        var pxPerMm = canvas.Dpi / 25.4;

        CheckDuplicateIds(document, diags);
        CheckTokens(document, ctx, diags);

        for (var i = 0; i < document.Elements.Count; i++)
        {
            var el = document.Elements[i];
            var path = $"$.elements[{i}]";

            CheckBounds(canvas, el, path, diags);

            switch (el)
            {
                case BarcodeElement b: CheckBarcode(b, pxPerMm, path, diags); break;
                case QrElement q: CheckQr(q, pxPerMm, path, diags); break;
                case TextElement t: CheckTextOverflow(document, t, pxPerMm, ctx, path, diags); break;
                case ImageElement im: CheckAsset(im, ctx, path, diags); break;
            }

            CheckFont(document, el, ctx, path, diags);
        }

        return new ValidationResult(diags);
    }

    private static void CheckDuplicateIds(LabelDocument doc, List<ValidationDiagnostic> diags)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < doc.Elements.Count; i++)
        {
            var id = doc.Elements[i].Id;
            if (!seen.Add(id))
                diags.Add(new ValidationDiagnostic(ValidationCodes.DuplicateId, ValidationSeverity.Error,
                    $"Duplicate element id '{id}'.", $"$.elements[{i}].id"));
        }
    }

    private static void CheckTokens(LabelDocument doc, ValidationContext ctx, List<ValidationDiagnostic> diags)
    {
        var declared = (doc.Tokens ?? []).ToDictionary(t => t.Name, StringComparer.Ordinal);
        var scanned = TokenScanner.Scan(doc);
        var resolver = new TokenResolver(ctx.Data, doc.Bindings, doc.Tokens);

        foreach (var name in scanned)
        {
            if (!declared.ContainsKey(name))
                diags.Add(new ValidationDiagnostic(ValidationCodes.UndeclaredToken, ValidationSeverity.Warning,
                    $"Token '{{{name}}}' is referenced but not declared in tokens[].", "$.tokens"));
        }

        // Required tokens must resolve from data or a default at print time.
        foreach (var decl in doc.Tokens ?? [])
        {
            if (!decl.Required) continue;
            resolver.ResolveToken(decl.Name);
            if (resolver.Unresolved.ContainsKey(decl.Name) && decl.Default is null)
                diags.Add(new ValidationDiagnostic(ValidationCodes.RequiredTokenUnresolved, ValidationSeverity.Error,
                    $"Required token '{{{decl.Name}}}' has no supplied value or default.", "$.tokens"));
        }
    }

    private static void CheckBounds(Canvas canvas, LabelElement el, string path, List<ValidationDiagnostic> diags)
    {
        var bleed = canvas.BleedMm;
        var outOfCanvas = el.X < -bleed || el.Y < -bleed
            || el.X + el.W > canvas.WidthMm + bleed || el.Y + el.H > canvas.HeightMm + bleed;
        if (outOfCanvas)
        {
            diags.Add(new ValidationDiagnostic(ValidationCodes.ElementOutsideCanvas, ValidationSeverity.Error,
                $"Element '{el.Id}' extends outside the canvas{(bleed > 0 ? " + bleed" : "")}.", path));
            return;
        }

        if (canvas.SafeAreaInsetMm is { } inset && inset > 0)
        {
            var outOfSafe = el.X < inset || el.Y < inset
                || el.X + el.W > canvas.WidthMm - inset || el.Y + el.H > canvas.HeightMm - inset;
            if (outOfSafe)
                diags.Add(new ValidationDiagnostic(ValidationCodes.ElementOutsideSafeArea, ValidationSeverity.Warning,
                    $"Element '{el.Id}' extends past the safe print area — at risk of clipping from skew/registration (§6.1.3).", path));
        }
    }

    private static void CheckBarcode(BarcodeElement b, double pxPerMm, string path, List<ValidationDiagnostic> diags)
    {
        if (b.Props.ModuleWidthMm * pxPerMm < 1.0)
            diags.Add(new ValidationDiagnostic(ValidationCodes.BarcodeModuleTooSmall, ValidationSeverity.Warning,
                $"Barcode module {b.Props.ModuleWidthMm} mm rounds to <1 px at {pxPerMm * 25.4:0} dpi — may not scan (§6.3.3).",
                $"{path}.props.moduleWidthMm"));

        if (b.Props.Value.Length > 0 && !b.Props.Value.Contains('{'))
        {
            try { Barcodes.Encode1D(b.Props.Symbology, b.Props.Value); }
            catch
            {
                diags.Add(new ValidationDiagnostic(ValidationCodes.BarcodeInvalidValue, ValidationSeverity.Error,
                    $"Value '{b.Props.Value}' is not valid for symbology '{b.Props.Symbology}'.", $"{path}.props.value"));
            }
        }
    }

    private static void CheckQr(QrElement q, double pxPerMm, string path, List<ValidationDiagnostic> diags)
    {
        var hasToken = q.Props.Value.Contains('{');
        if (q.Props.ModuleSizeMm is { } ms)
        {
            if (ms * pxPerMm < 1.0)
                diags.Add(new ValidationDiagnostic(ValidationCodes.QrModuleTooSmall, ValidationSeverity.Warning,
                    $"QR module {ms} mm rounds to <1 px at {pxPerMm * 25.4:0} dpi — may not scan (§6.3.3).",
                    $"{path}.props.moduleSizeMm"));
        }
        else if (!hasToken && q.Props.Value.Length > 0)
        {
            try
            {
                var modules = Barcodes.EncodeQr(q.Props.Value, q.Props.Encoding, q.Props.EcLevel);
                var n = modules.GetLength(0);
                var quiet = q.Props.QuietZoneMm * pxPerMm;
                var avail = Math.Min(q.W, q.H) * pxPerMm - 2 * quiet;
                if (avail / n < 1.0)
                    diags.Add(new ValidationDiagnostic(ValidationCodes.QrModuleTooSmall, ValidationSeverity.Warning,
                        $"Auto-fit QR module rounds to <1 px in a {q.W}×{q.H} mm box at {pxPerMm * 25.4:0} dpi — may not scan (§6.3.3).",
                        $"{path}.props.moduleSizeMm"));
            }
            catch
            {
                diags.Add(new ValidationDiagnostic(ValidationCodes.QrInvalidValue, ValidationSeverity.Error,
                    $"QR value could not be encoded.", $"{path}.props.value"));
            }
        }
    }

    private static void CheckTextOverflow(LabelDocument doc, TextElement t, double pxPerMm, ValidationContext ctx, string path, List<ValidationDiagnostic> diags)
    {
        if (ctx.Fonts is null) return;
        var content = t.Props.Content;
        if (content.Length == 0 || content.Contains('{')) return; // unresolved tokens — can't measure

        var dpi = pxPerMm * 25.4;
        var floorPt = t.Props.FontSizing == "fixed"
            ? (t.Props.FontSizePt ?? doc.DefaultStyle?.FontSizePt ?? 9)
            : t.Props.MinFontSizePt ?? 4.0;

        var family = t.Props.FontFamily ?? t.Style?.FontFamily ?? doc.DefaultStyle?.FontFamily ?? FontService.BundledFamily;
        var bold = t.Props.Bold ?? t.Style?.Bold ?? doc.DefaultStyle?.Bold ?? false;
        var italic = t.Props.Italic ?? t.Style?.Italic ?? doc.DefaultStyle?.Italic ?? false;
        var lineSpacing = t.Props.LineSpacing ?? t.Style?.LineSpacing ?? doc.DefaultStyle?.LineSpacing ?? 1.0;

        using var paint = new SKPaint
        {
            Typeface = ctx.Fonts.Resolve(family, bold, italic),
            TextSize = (float)(floorPt * dpi / 72.0),
            IsAntialias = true,
        };

        var boxW = (float)(t.W * pxPerMm);
        var boxH = (float)(t.H * pxPerMm);
        var m = paint.FontMetrics;
        var lineH = (m.Descent - m.Ascent) * (float)lineSpacing;
        var wrap = t.Props.Wrap != "none";

        var lineCount = 0;
        var overflows = false;
        foreach (var paragraph in content.Replace("\r\n", "\n").Split('\n'))
        {
            if (!wrap)
            {
                lineCount++;
                if (paint.MeasureText(paragraph) > boxW + 0.5f) overflows = true;
                continue;
            }
            lineCount += CountWrappedLines(paint, paragraph, boxW);
        }
        if (lineH * lineCount > boxH + 0.5f) overflows = true;

        if (overflows)
            diags.Add(new ValidationDiagnostic(ValidationCodes.TextOverflow, ValidationSeverity.Warning,
                $"Text '{t.Id}' overflows its box even at {floorPt} pt.", $"{path}.props.content"));
    }

    private static int CountWrappedLines(SKPaint paint, string paragraph, float boxW)
    {
        if (paragraph.Length == 0) return 1;
        var count = 1;
        var line = "";
        foreach (var word in paragraph.Split(' '))
        {
            var candidate = line.Length == 0 ? word : line + " " + word;
            if (paint.MeasureText(candidate) <= boxW || line.Length == 0)
                line = candidate;
            else { count++; line = word; }
        }
        return count;
    }

    private static void CheckAsset(ImageElement im, ValidationContext ctx, string path, List<ValidationDiagnostic> diags)
    {
        if (ctx.Assets is null) return; // no asset set supplied — can't verify
        if (!ctx.Assets.ContainsKey(im.Props.AssetId))
            diags.Add(new ValidationDiagnostic(ValidationCodes.MissingAsset, ValidationSeverity.Error,
                $"Image asset '{im.Props.AssetId}' is not present in the package.", $"{path}.props.assetId"));
    }

    private static void CheckFont(LabelDocument doc, LabelElement el, ValidationContext ctx, string path, List<ValidationDiagnostic> diags)
    {
        if (ctx.Fonts is null) return;
        var family = el switch
        {
            TextElement t => t.Props.FontFamily ?? el.Style?.FontFamily ?? doc.DefaultStyle?.FontFamily,
            TableElement => el.Style?.FontFamily ?? doc.DefaultStyle?.FontFamily,
            _ => null,
        };
        if (family is null || string.Equals(family, FontService.BundledFamily, StringComparison.OrdinalIgnoreCase)) return;
        if (!ctx.Fonts.IsAvailable(family))
            diags.Add(new ValidationDiagnostic(ValidationCodes.MissingFont, ValidationSeverity.Warning,
                $"Font '{family}' is not installed; falling back to '{FontService.BundledFamily}' (§6.3.4).", $"{path}.props.fontFamily"));
    }
}
