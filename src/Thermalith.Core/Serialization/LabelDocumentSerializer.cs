using System.Text.Json;
using Thermalith.Core.Model;

namespace Thermalith.Core.Serialization;

/// <summary>Convenience read/write for a standalone <c>label.json</c> template, using the Core JSON conventions (§6.6).</summary>
public static class LabelDocumentSerializer
{
    public static string ToJson(LabelDocument doc) => JsonSerializer.Serialize(doc, LabelJson.Options);

    public static LabelDocument FromJson(string json) =>
        JsonSerializer.Deserialize<LabelDocument>(json, LabelJson.Options)
        ?? throw new LabelPackageException("label.json failed to parse.");
}
