using System.Text.Json;
using System.Text.Json.Serialization;

namespace Thermalith.Core.Serialization;

/// <summary>
/// The canonical <c>System.Text.Json</c> configuration for label documents and package manifests
/// (build spec §6.6 conventions): camelCase, forgiving read (comments + trailing commas),
/// null-omit on write, and <c>[JsonExtensionData]</c> on the models for forward-compat.
/// </summary>
public static class LabelJson
{
    /// <summary>Shared, immutable options used for every read and write in Core.</summary>
    public static readonly JsonSerializerOptions Options = Build();

    private static JsonSerializerOptions Build()
    {
        var o = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            // The element `type` discriminator may sit after `id` in the schema (label-json-spec §11.1),
            // so allow the polymorphic discriminator to appear out of order.
            AllowOutOfOrderMetadataProperties = true,
            WriteIndented = true,
        };
        return o;
    }
}
