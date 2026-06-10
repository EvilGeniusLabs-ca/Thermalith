using Thermalith.Core.Model;

namespace Thermalith.Core.Rendering;

// The output of stage 1 (RESOLVE): a label with every token substituted and every serial /
// datetime expanded against a frozen clock. It is pure data — no tokens, no live clock — so the
// render path below it is deterministic (§6.3.7). Geometry stays in mm; the renderer applies the
// device transform.

/// <summary>A fully-resolved label, ready for MEASURE/RASTER (§6.3 stage 1 output).</summary>
public sealed record ResolvedLabel(Canvas Canvas, IReadOnlyList<ResolvedElement> Elements);

/// <summary>Effective typography after the style cascade is flattened (defaultStyle → style → props, §6).</summary>
public sealed record ResolvedTextStyle
{
    public string FontFamily { get; init; } = "Roboto";
    public double FontSizePt { get; init; } = 9;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public bool Underline { get; init; }
    public double LineSpacing { get; init; } = 1.0;
    public double LetterSpacing { get; init; }
}

/// <summary>Common resolved geometry/identity for every element kind.</summary>
public abstract record ResolvedElement
{
    public required string Id { get; init; }
    public double XMm { get; init; }
    public double YMm { get; init; }
    public double WMm { get; init; }
    public double HMm { get; init; }
    public double RotationDeg { get; init; }
    public Justify Justify { get; init; } = new();
}

public sealed record ResolvedText : ResolvedElement
{
    public string Text { get; init; } = "";
    public ResolvedTextStyle Style { get; init; } = new();
    public string Wrap { get; init; } = "word";
    public string FontSizing { get; init; } = "fixed";
    public double? MinFontSizePt { get; init; }
    public double? MaxFontSizePt { get; init; }
}

public sealed record ResolvedBarcode : ResolvedElement
{
    public string Symbology { get; init; } = "code128";
    public string Value { get; init; } = "";
    public bool ShowText { get; init; } = true;
    public string TextPosition { get; init; } = "below";
    public double ModuleWidthMm { get; init; } = 0.33;
    public double QuietZoneMm { get; init; } = 2.0;
    public ResolvedTextStyle TextStyle { get; init; } = new();
}

public sealed record ResolvedQr : ResolvedElement
{
    public string Value { get; init; } = "";
    public string Encoding { get; init; } = "text";
    public string EcLevel { get; init; } = "M";
    public double? ModuleSizeMm { get; init; }
    public double QuietZoneMm { get; init; } = 1.0;
}

public sealed record ResolvedShape : ResolvedElement
{
    public string ShapeType { get; init; } = "rect";
    public double StrokeWidthMm { get; init; } = 0.3;
    public bool Fill { get; init; }
    public double CornerRadiusMm { get; init; }
}

public sealed record ResolvedImage : ResolvedElement
{
    /// <summary>Encoded image bytes (PNG/JPEG/…) from the package assets, or <c>null</c> if missing.</summary>
    public byte[]? Data { get; init; }
    public string Fit { get; init; } = "fit";
    public string Dither { get; init; } = "floydSteinberg";
    public int Threshold { get; init; } = 128;
    public bool Invert { get; init; }
    public int RotateQuarters { get; init; }
    public bool FlipH { get; init; }
    public bool FlipV { get; init; }
}

public sealed record ResolvedCell(string Text, Justify Justify)
{
    public int FillPercent { get; init; }       // 0 = none .. 100 = solid; ordered-dithered to grey
    public bool TextWhite { get; init; }         // crisp text colour (black default)
    public string FontFamily { get; init; } = "Roboto";
    public double FontSizePt { get; init; } = 9;
    public bool Bold { get; init; }
    public bool Italic { get; init; }
    public int ColSpan { get; init; } = 1;
    public int RowSpan { get; init; } = 1;
    public bool Covered { get; init; }           // sits under a merge → skipped in layout/render
}

public sealed record ResolvedTable : ResolvedElement
{
    public int Cols { get; init; }
    public int Rows { get; init; }
    public double[]? ColumnWidthsMm { get; init; }
    public double[]? RowHeightsMm { get; init; }
    public ResolvedCell[][] Cells { get; init; } = [];
    public double BorderWidthMm { get; init; } = 0.2;
    public bool HeaderRow { get; init; }
    public bool HeaderColumn { get; init; }
    public ResolvedTextStyle Style { get; init; } = new();
}
