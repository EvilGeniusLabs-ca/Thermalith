using System.Globalization;
using Thermalith.Core.Model;
using Thermalith.Core.Tokens;

namespace Thermalith.Core.Rendering;

/// <summary>Inputs to the RESOLVE stage — the data row, the row index (serial advance), a frozen clock, and assets.</summary>
public sealed record ResolveContext
{
    /// <summary>The supplied data row, token-name (or bound column) → value (§6.5).</summary>
    public IReadOnlyDictionary<string, object?>? Data { get; init; }

    /// <summary>Zero-based row index across a batch — drives serial-number advance (§6.5).</summary>
    public int RowIndex { get; init; }

    /// <summary>The frozen clock for <c>datetime</c> elements with <c>source = printNow</c>. Defaults to the Unix epoch for determinism.</summary>
    public DateTimeOffset Now { get; init; } = DateTimeOffset.UnixEpoch;

    /// <summary>Embedded image assets, assetId → encoded bytes (§6.6).</summary>
    public IReadOnlyDictionary<string, byte[]>? Assets { get; init; }
}

/// <summary>
/// Stage 1 of the pipeline (§6.3): bind data, substitute <c>{tokens}</c>, and expand
/// serial / datetime against a frozen clock into a pure, deterministic <see cref="ResolvedLabel"/>.
/// </summary>
public static class LabelResolver
{
    public static ResolvedLabel Resolve(LabelDocument doc, ResolveContext ctx)
    {
        var tokens = new TokenResolver(ctx.Data, doc.Bindings, doc.Tokens);
        var resolved = new List<ResolvedElement>(doc.Elements.Count);

        foreach (var el in doc.Elements)
        {
            if (!el.Visible) continue;
            var r = ResolveElement(doc, el, tokens, ctx);
            if (r is not null) resolved.Add(r);
        }

        return new ResolvedLabel(doc.Canvas, resolved);
    }

    private static ResolvedElement? ResolveElement(LabelDocument doc, LabelElement el, TokenResolver tokens, ResolveContext ctx)
    {
        var justify = el.Justify ?? new Justify();
        return el switch
        {
            TextElement t => Geo(new ResolvedText
            {
                Id = el.Id,
                Text = tokens.Substitute(t.Props.Content),
                Style = BuildStyle(doc, el, t.Props.FontFamily, t.Props.FontSizePt, t.Props.Bold, t.Props.Italic, t.Props.Underline, t.Props.LineSpacing, t.Props.LetterSpacing),
                Wrap = t.Props.Wrap,
                FontSizing = t.Props.FontSizing,
                MinFontSizePt = t.Props.MinFontSizePt,
                MaxFontSizePt = t.Props.MaxFontSizePt,
            }, el, justify),

            BarcodeElement b => Geo(new ResolvedBarcode
            {
                Id = el.Id,
                Symbology = b.Props.Symbology,
                Value = tokens.Substitute(b.Props.Value),
                ShowText = b.Props.ShowText,
                TextPosition = b.Props.TextPosition,
                ModuleWidthMm = b.Props.ModuleWidthMm,
                QuietZoneMm = b.Props.QuietZoneMm,
                TextStyle = BuildStyle(doc, el, null, null, null, null, null, null, 0),
            }, el, justify),

            QrElement q => Geo(new ResolvedQr
            {
                Id = el.Id,
                Value = tokens.Substitute(q.Props.Value),
                Encoding = q.Props.Encoding,
                EcLevel = q.Props.EcLevel,
                ModuleSizeMm = q.Props.ModuleSizeMm,
                QuietZoneMm = q.Props.QuietZoneMm,
            }, el, justify),

            SerialElement s => Geo(new ResolvedText
            {
                Id = el.Id,
                Text = ExpandSerial(s.Props, ctx.RowIndex),
                Style = BuildStyle(doc, el, null, null, null, null, null, null, 0),
            }, el, justify),

            DateTimeElement d => Geo(new ResolvedText
            {
                Id = el.Id,
                Text = ExpandDateTime(d.Props, ctx.Now),
                Style = BuildStyle(doc, el, null, null, null, null, null, null, 0),
            }, el, justify),

            ShapeElement sh => Geo(new ResolvedShape
            {
                Id = el.Id,
                ShapeType = sh.Props.ShapeType,
                StrokeWidthMm = ResolveStroke(doc, el, sh.Props.StrokeWidthMm),
                Fill = ResolveFill(doc, el, sh.Props.Fill),
                CornerRadiusMm = sh.Props.CornerRadiusMm,
            }, el, justify),

            ImageElement im => Geo(new ResolvedImage
            {
                Id = el.Id,
                Data = ctx.Assets is not null && ctx.Assets.TryGetValue(im.Props.AssetId, out var bytes) ? bytes : null,
                Fit = im.Props.Fit,
                Dither = im.Props.Dither,
                Threshold = im.Props.Threshold,
                Invert = im.Props.Invert,
            }, el, justify),

            TableElement tab => Geo(new ResolvedTable
            {
                Id = el.Id,
                Cols = tab.Props.Cols,
                Rows = tab.Props.Rows,
                ColumnWidthsMm = tab.Props.ColumnWidthsMm,
                RowHeightsMm = tab.Props.RowHeightsMm,
                Cells = ResolveCells(tab.Props, justify, tokens),
                BorderWidthMm = tab.Props.BorderWidthMm,
                HeaderRow = tab.Props.HeaderRow,
                Style = BuildStyle(doc, el, null, null, null, null, null, null, 0),
            }, el, justify),

            _ => null,
        };
    }

    private static T Geo<T>(T r, LabelElement el, Justify justify) where T : ResolvedElement =>
        r with { XMm = el.X, YMm = el.Y, WMm = el.W, HMm = el.H, RotationDeg = el.Rotation, Justify = justify };

    private static ResolvedTextStyle BuildStyle(
        LabelDocument doc, LabelElement el,
        string? family, double? size, bool? bold, bool? italic, bool? underline, double? lineSpacing, double letterSpacing)
    {
        var s = el.Style;
        var d = doc.DefaultStyle;
        return new ResolvedTextStyle
        {
            FontFamily = family ?? s?.FontFamily ?? d?.FontFamily ?? "Roboto",
            FontSizePt = size ?? s?.FontSizePt ?? d?.FontSizePt ?? 9,
            Bold = bold ?? s?.Bold ?? d?.Bold ?? false,
            Italic = italic ?? s?.Italic ?? d?.Italic ?? false,
            Underline = underline ?? s?.Underline ?? d?.Underline ?? false,
            LineSpacing = lineSpacing ?? s?.LineSpacing ?? d?.LineSpacing ?? 1.0,
            LetterSpacing = letterSpacing,
        };
    }

    private static double ResolveStroke(LabelDocument doc, LabelElement el, double propStroke) =>
        // props default is 0.3; honour an explicit cascade only when props left it at the schema default.
        propStroke != 0.3 ? propStroke : el.Style?.StrokeWidthMm ?? doc.DefaultStyle?.StrokeWidthMm ?? propStroke;

    private static bool ResolveFill(LabelDocument doc, LabelElement el, string propFill)
    {
        var fill = propFill is not ("none") ? propFill : el.Style?.Fill ?? doc.DefaultStyle?.Fill ?? propFill;
        return string.Equals(fill, "solid", StringComparison.OrdinalIgnoreCase);
    }

    private static ResolvedCell[][] ResolveCells(TableProps p, Justify elJustify, TokenResolver tokens)
    {
        var cells = p.Cells ?? [];
        var rows = new ResolvedCell[p.Rows][];
        for (var r = 0; r < p.Rows; r++)
        {
            var row = new ResolvedCell[p.Cols];
            for (var c = 0; c < p.Cols; c++)
            {
                var cell = r < cells.Count && c < cells[r].Count ? cells[r][c] : null;
                var text = tokens.Substitute(cell?.Content ?? "");
                row[c] = new ResolvedCell(text, cell?.Justify ?? elJustify);
            }
            rows[r] = row;
        }
        return rows;
    }

    private static string ExpandSerial(SerialProps p, int rowIndex)
    {
        var value = p.Start + (long)rowIndex * p.Step;
        var pad = p.PadChar.Length > 0 ? p.PadChar[0] : '0';
        var body = Math.Abs(value).ToString(CultureInfo.InvariantCulture);
        if (p.PadLength > 0) body = body.PadLeft(p.PadLength, pad);
        if (value < 0) body = "-" + body;
        return p.Prefix + body + p.Suffix;
    }

    private static string ExpandDateTime(DateTimeProps p, DateTimeOffset now)
    {
        DateTimeOffset value = now;
        if (string.Equals(p.Source, "fixed", StringComparison.OrdinalIgnoreCase) && p.FixedValueUtc is { } fixedUtc
            && DateTimeOffset.TryParse(fixedUtc, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal, out var parsed))
        {
            value = parsed;
        }
        return value.ToString(p.Format, CultureInfo.InvariantCulture);
    }
}
