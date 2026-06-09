using System.Text.Json;
using System.Text.Json.Serialization;
using Thermalith.Core.Serialization;

namespace Thermalith.Core.Model;

// The type-specific props blocks (§11.2–11.9). Cascade-able typography/visual fields are nullable
// so they can inherit from defaultStyle / element style (§6); non-cascade fields carry their
// schema default inline. "auto"-or-number fields use the converters in Serialization/.

/// <summary>§11.2 — text content + typography. Static captions are text with no <c>{token}</c>.</summary>
public sealed record TextProps
{
    public string Content { get; init; } = "";
    public string? FontFamily { get; init; }
    public double? FontSizePt { get; init; }
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public bool? Underline { get; init; }
    public double? LineSpacing { get; init; }
    public double LetterSpacing { get; init; }
    public string Wrap { get; init; } = "word";          // none | word
    public string FontSizing { get; init; } = "fixed";   // fixed | shrink | fill (editor is fixed-only)
    public double? MinFontSizePt { get; init; }
    public double? MaxFontSizePt { get; init; }
    public bool AutoSize { get; init; }                  // editor keeps W/H = the measured text (box hugs glyphs)

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>§11.3 — 1D barcode via ZXing.Net.</summary>
public sealed record BarcodeProps
{
    public string Symbology { get; init; } = "code128";
    public string Value { get; init; } = "";
    public bool ShowText { get; init; } = true;
    public string TextPosition { get; init; } = "below"; // above | below | none
    public double ModuleWidthMm { get; init; } = 0.33;
    public double QuietZoneMm { get; init; } = 2.0;

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>§11.4 — QR code via ZXing.Net (QRCoder fallback).</summary>
public sealed record QrProps
{
    public string Value { get; init; } = "";
    public string Encoding { get; init; } = "text"; // text | hex
    public string EcLevel { get; init; } = "M";     // L | M | Q | H

    /// <summary><c>null</c> = auto-fit to the box; an explicit value snaps to whole device px.</summary>
    [JsonConverter(typeof(AutoDoubleConverter))]
    public double? ModuleSizeMm { get; init; }

    public double QuietZoneMm { get; init; } = 1.0;

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>§11.5 — serial counter. The live counter is runtime state, not stored here.</summary>
public sealed record SerialProps
{
    public int Start { get; init; } = 1;
    public int Step { get; init; } = 1;
    public int PadLength { get; init; }
    public string PadChar { get; init; } = "0";
    public string Prefix { get; init; } = "";
    public string Suffix { get; init; } = "";

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>§11.6 — date/time, resolved at print or fixed.</summary>
public sealed record DateTimeProps
{
    public string Kind { get; init; } = "date";          // date | time | datetime
    public string Format { get; init; } = "yyyy-MM-dd";  // .NET format string
    public string Source { get; init; } = "printNow";    // printNow | fixed
    public string? FixedValueUtc { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>§11.7 — standard shapes.</summary>
public sealed record ShapeProps
{
    public string ShapeType { get; init; } = "rect"; // rect | roundedRect | ellipse | line
    public double StrokeWidthMm { get; init; } = 0.3;
    public string Fill { get; init; } = "none";       // none | solid
    public double CornerRadiusMm { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>§11.8 — embedded raster, dithered to 1-bit before compositing (§6.3.2).</summary>
public sealed record ImageProps
{
    public string AssetId { get; init; } = "";
    public string Fit { get; init; } = "fit";              // fill | fit | stretch | center
    public string Dither { get; init; } = "floydSteinberg"; // threshold | floydSteinberg | atkinson | ordered | none
    public int Threshold { get; init; } = 128;             // used when dither = threshold
    public bool Invert { get; init; }

    // Orthogonal image transforms, applied to the source before fit/dither (distinct from the
    // element's freeform Rotation). Order: rotate, then mirror (H), then flip (V).
    public int RotateQuarters { get; init; }               // 0..3 clockwise 90° steps
    public bool FlipH { get; init; }                       // mirror left↔right
    public bool FlipV { get; init; }                       // flip top↔bottom

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>§11.9 — grid of token-aware cells.</summary>
public sealed record TableProps
{
    public int Cols { get; init; }
    public int Rows { get; init; }

    [JsonConverter(typeof(AutoDoubleArrayConverter))]
    public double[]? ColumnWidthsMm { get; init; }

    [JsonConverter(typeof(AutoDoubleArrayConverter))]
    public double[]? RowHeightsMm { get; init; }

    public List<List<TableCell>>? Cells { get; init; }
    public double BorderWidthMm { get; init; } = 0.2;
    public bool HeaderRow { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>One table cell — token-aware content + optional per-cell justification (§11.9).</summary>
public sealed record TableCell
{
    public string Content { get; init; } = "";
    public Justify? Justify { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}
