using System.Text.Json;
using System.Text.Json.Serialization;

namespace Thermalith.Core.Model;

/// <summary>
/// The element base shared by every control (§11.1): identity, geometry (mm), rotation, lock,
/// visibility and in-element justification. The concrete subtype is selected by the <c>type</c>
/// discriminator and carries a strongly-typed <c>props</c> block. The freeform
/// <c>[JsonExtensionData]</c> bag round-trips unknown fields from newer schema versions (§6.6).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextElement), "text")]
[JsonDerivedType(typeof(BarcodeElement), "barcode")]
[JsonDerivedType(typeof(QrElement), "qr")]
[JsonDerivedType(typeof(SerialElement), "serial")]
[JsonDerivedType(typeof(DateTimeElement), "datetime")]
[JsonDerivedType(typeof(ShapeElement), "shape")]
[JsonDerivedType(typeof(ImageElement), "image")]
[JsonDerivedType(typeof(TableElement), "table")]
public abstract record LabelElement
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double W { get; init; }
    public double H { get; init; }
    public double Rotation { get; init; }
    public bool Locked { get; init; }
    public bool Visible { get; init; } = true;
    public Justify? Justify { get; init; }
    public ElementStyle? Style { get; init; }

    /// <summary>The schema <c>type</c> string. Serialized as the polymorphic discriminator, not here.</summary>
    [JsonIgnore] public abstract string Type { get; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

public sealed record TextElement : LabelElement
{
    [JsonIgnore] public override string Type => "text";
    public TextProps Props { get; init; } = new();
}

public sealed record BarcodeElement : LabelElement
{
    [JsonIgnore] public override string Type => "barcode";
    public BarcodeProps Props { get; init; } = new();
}

public sealed record QrElement : LabelElement
{
    [JsonIgnore] public override string Type => "qr";
    public QrProps Props { get; init; } = new();
}

public sealed record SerialElement : LabelElement
{
    [JsonIgnore] public override string Type => "serial";
    public SerialProps Props { get; init; } = new();
}

public sealed record DateTimeElement : LabelElement
{
    [JsonIgnore] public override string Type => "datetime";
    public DateTimeProps Props { get; init; } = new();
}

public sealed record ShapeElement : LabelElement
{
    [JsonIgnore] public override string Type => "shape";
    public ShapeProps Props { get; init; } = new();
}

public sealed record ImageElement : LabelElement
{
    [JsonIgnore] public override string Type => "image";
    public ImageProps Props { get; init; } = new();
}

public sealed record TableElement : LabelElement
{
    [JsonIgnore] public override string Type => "table";
    public TableProps Props { get; init; } = new();
}
