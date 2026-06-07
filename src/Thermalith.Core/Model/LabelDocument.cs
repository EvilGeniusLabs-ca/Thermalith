using System.Text.Json;
using System.Text.Json.Serialization;

namespace Thermalith.Core.Model;

// The authoritative field-level schema, every control type's property table, and a worked
// 50×30 example live in Documentation/label-json-spec.md. This model round-trips that schema.
// Conventions (§2): geometry in mm, typography in pt, origin top-left, rotation deg CW about
// centre, z-order = array order. Every model carries a [JsonExtensionData] bag so unknown
// fields from a newer schemaVersion round-trip instead of being dropped (§6.6).

/// <summary>Root of a label template (serialized to <c>label.json</c> inside a <c>.nlbl</c> package, §6.6).</summary>
public sealed record LabelDocument
{
    public string SchemaVersion { get; init; } = "1.0";
    public required LabelMetadata Metadata { get; init; }
    public required Canvas Canvas { get; init; }

    /// <summary>Optional label-level style defaults inherited by elements (§6, style cascade).</summary>
    public ElementStyle? DefaultStyle { get; init; }

    /// <summary>The declared data contract — tokens this template expects (§8).</summary>
    public List<TokenDecl>? Tokens { get; init; }

    /// <summary>Live data-source binding — connection details only, never credentials (§9).</summary>
    public DataSource? DataSource { get; init; }

    /// <summary>Explicit token→column remap; tokens auto-map by name when omitted (§10).</summary>
    public Dictionary<string, string>? Bindings { get; init; }

    /// <summary>Ordered, back-to-front. <c>elements[0]</c> is backmost (§2). May be empty.</summary>
    public List<LabelElement> Elements { get; init; } = [];

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>Human-facing identity + timestamps (§4).</summary>
public sealed record LabelMetadata
{
    public required string Name { get; init; }
    public string CreatedUtc { get; init; } = "";
    public string ModifiedUtc { get; init; } = "";
    public string? AppVersion { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>Physical label surface. Geometry is in millimetres; <see cref="Dpi"/> is model-dependent (§5).</summary>
public sealed record Canvas
{
    public required double WidthMm { get; init; }
    public required double HeightMm { get; init; }

    /// <summary>Dots per inch, seeded from the printer profile. Renderer reads this — never assume 8 (§6.3.1).</summary>
    public int Dpi { get; init; } = 203;

    public string Shape { get; init; } = "rectangle"; // rectangle | rounded | circle | dieCut
    public double CornerRadiusMm { get; init; }

    /// <summary>Optional bleed margin; the validator warns on content past it (§5).</summary>
    public double BleedMm { get; init; }

    /// <summary>
    /// Inset (mm) of the safe print area on every edge — worst-case skew + registration cannot push
    /// content inside it off the label (§6.1.3). <c>null</c> means "no explicit safe area declared".
    /// </summary>
    public double? SafeAreaInsetMm { get; init; }

    /// <summary>Whole-label rotation relative to the print-head feed: 0 | 90 | 180 | 270 (§5).</summary>
    public int OrientationDeg { get; init; }

    public Tail? Tail { get; init; }
    public string Background { get; init; } = "white";

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>Cable/double-label tail (§5).</summary>
public sealed record Tail
{
    public string Position { get; init; } = "none"; // none | left | right
    public double LengthMm { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>
/// Visual defaults that elements inherit when their own <c>props</c>/<c>style</c> omit a value.
/// Resolution order (last wins): <c>defaultStyle</c> → element <c>style</c> → element <c>props</c> (§6).
/// </summary>
public sealed record ElementStyle
{
    public string? FontFamily { get; init; }
    public double? FontSizePt { get; init; }
    public bool? Bold { get; init; }
    public bool? Italic { get; init; }
    public bool? Underline { get; init; }
    public double? LineSpacing { get; init; }
    public double? StrokeWidthMm { get; init; }
    public string? Fill { get; init; } // none | solid

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>One entry in the template's declared token contract (§8).</summary>
public sealed record TokenDecl
{
    public required string Name { get; init; }
    public string Type { get; init; } = "string"; // string | number | date | bool
    public string? Description { get; init; }
    public string? Sample { get; init; }
    public string? Default { get; init; }
    public bool Required { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>Live data-source binding — connection details only, never credentials (§9).</summary>
public sealed record DataSource
{
    public string Kind { get; init; } = "none"; // none | database | csv | xlsx | json
    public string? CredentialRef { get; init; }
    public string? Provider { get; init; }
    public string? ConnectionString { get; init; }
    public string? Query { get; init; }
    public string? Path { get; init; }
    public bool? HasHeaderRow { get; init; }
    public string? Delimiter { get; init; }
    public string? Sheet { get; init; }
    public string? Range { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>Content alignment <i>within</i> an element (§2, §6.2).</summary>
public sealed record Justify
{
    public string H { get; init; } = "left";   // left | center | right | justify
    public string V { get; init; } = "top";    // top | middle | bottom

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}
