namespace Thermalith.Core.Model;

// Minimal seed of the label document model. The full, authoritative schema —
// every field, every control type, conventions, and a worked example — lives in
// Documentation/label-json-spec.md. This stub exists so the engine compiles and
// the shape is visible; flesh it out as the engine is implemented.

/// <summary>Root of a label template (serialized to <c>label.json</c> inside a <c>.nlbl</c> package).</summary>
public sealed record LabelDocument
{
    public string SchemaVersion { get; init; } = "1.0";
    public required Canvas Canvas { get; init; }
    public List<LabelElement> Elements { get; init; } = [];
}

/// <summary>Physical label surface. Geometry is in millimetres; <see cref="Dpi"/> is model-dependent.</summary>
public sealed record Canvas
{
    public required double WidthMm { get; init; }
    public required double HeightMm { get; init; }
    public int Dpi { get; init; } = 203;
    public string Shape { get; init; } = "rectangle"; // rectangle | rounded | circle | dieCut
}

/// <summary>Common base every control shares. Type-specific data lives in a per-type props block (TODO).</summary>
public sealed record LabelElement
{
    public required string Id { get; init; }
    public required string Type { get; init; } // text | barcode | qr | serial | datetime | shape | image | table
    public string? Name { get; init; }
    public double X { get; init; }             // upper-left, mm
    public double Y { get; init; }
    public double W { get; init; }
    public double H { get; init; }
    public double Rotation { get; init; }      // degrees clockwise about centre
    public bool Locked { get; init; }
    public bool Visible { get; init; } = true;
}
