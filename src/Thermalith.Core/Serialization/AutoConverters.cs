using System.Text.Json;
using System.Text.Json.Serialization;

namespace Thermalith.Core.Serialization;

/// <summary>
/// Reads a field that is either a JSON number or the literal string <c>"auto"</c>, mapping
/// <c>"auto"</c> (and absent) to <see langword="null"/>. Used by <c>moduleSizeMm</c> (§11.4):
/// <c>null</c> means "fit automatically", an explicit number snaps to whole device pixels.
/// </summary>
public sealed class AutoDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String when string.Equals(reader.GetString(), "auto", StringComparison.OrdinalIgnoreCase) => null,
            JsonTokenType.String => throw new JsonException($"Expected a number or \"auto\", got string \"{reader.GetString()}\"."),
            _ => throw new JsonException($"Expected a number or \"auto\", got {reader.TokenType}."),
        };

    // Null is omitted by the serializer (WhenWritingNull); only real values reach here.
    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options) =>
        writer.WriteNumberValue(value!.Value);
}

/// <summary>
/// Reads a field that is either a JSON array of numbers or the literal string <c>"auto"</c>,
/// mapping <c>"auto"</c> (and absent) to <see langword="null"/>. Used by <c>columnWidthsMm</c> /
/// <c>rowHeightsMm</c> (§11.9).
/// </summary>
public sealed class AutoDoubleArrayConverter : JsonConverter<double[]?>
{
    public override double[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String when string.Equals(reader.GetString(), "auto", StringComparison.OrdinalIgnoreCase):
                return null;
            case JsonTokenType.StartArray:
                var list = new List<double>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    list.Add(reader.GetDouble());
                return [.. list];
            default:
                throw new JsonException($"Expected an array of numbers or \"auto\", got {reader.TokenType}.");
        }
    }

    public override void Write(Utf8JsonWriter writer, double[]? value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var v in value!)
            writer.WriteNumberValue(v);
        writer.WriteEndArray();
    }
}
