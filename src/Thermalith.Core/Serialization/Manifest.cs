using System.Text.Json;
using System.Text.Json.Serialization;

namespace Thermalith.Core.Serialization;

/// <summary>
/// <c>manifest.json</c> — package metadata (build spec §6.6). <see cref="ManifestVersion"/> gates
/// compatibility: a reader refuses a package whose version is newer than it understands.
/// </summary>
public sealed record Manifest
{
    /// <summary>The manifest schema this Core writes and the newest it will open.</summary>
    public const int CurrentVersion = 1;

    public int ManifestVersion { get; init; } = CurrentVersion;
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Version { get; init; } = "1.0.0";
    public string? Created { get; init; }
    public string? Updated { get; init; }
    public string? Author { get; init; }
    public string? Description { get; init; }
    public string? License { get; init; }

    /// <summary>SHA-256 (hex) of the package content — integrity check (§6.6).</summary>
    public string? Fingerprint { get; init; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extensions { get; init; }
}

/// <summary>Raised when a <c>.nlbl</c> package cannot be opened (corrupt, or a newer version than supported).</summary>
public sealed class LabelPackageException : Exception
{
    public LabelPackageException(string message) : base(message) { }
}
