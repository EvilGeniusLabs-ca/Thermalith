using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Thermalith.Core.Model;

namespace Thermalith.Core.Serialization;

/// <summary>
/// An opened <c>.nlbl</c> package (build spec §6.6): the manifest, the template, optional data rows,
/// and image assets. A package with no <see cref="Data"/> is a reusable template; with data it is a
/// self-contained label (the API/MCP can still override the data, §6.5).
/// </summary>
public sealed record LabelPackage
{
    public required Manifest Manifest { get; init; }
    public required LabelDocument Document { get; init; }

    /// <summary>Optional bundled data rows (<c>data.json</c>), each a column→value map.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>>? Data { get; init; }

    /// <summary>Image assets, assetId → encoded bytes (<c>assets/</c>).</summary>
    public IReadOnlyDictionary<string, byte[]> Assets { get; init; } = new Dictionary<string, byte[]>();

    /// <summary>True when the stored fingerprint matches the recomputed content hash (set on load).</summary>
    public bool? FingerprintValid { get; init; }
}

/// <summary>Reads and writes <c>.nlbl</c> zip packages. Save == export — the package <i>is</i> the interchange format (§6.6).</summary>
public static class LabelPackageIo
{
    /// <summary>Largest <c>schemaVersion</c> major this Core understands (label-json-spec §14).</summary>
    public const int SupportedSchemaMajor = 1;

    private const string ManifestEntry = "manifest.json";
    private const string LabelEntry = "label.json";
    private const string DataEntry = "data.json";
    private const string AssetsPrefix = "assets/";

    // ── Save ────────────────────────────────────────────────────────────────────────────────

    public static void Save(LabelPackage package, string path)
    {
        using var fs = File.Create(path);
        Save(package, fs);
    }

    public static void Save(LabelPackage package, Stream destination)
    {
        var labelBytes = JsonSerializer.SerializeToUtf8Bytes(package.Document, LabelJson.Options);
        byte[]? dataBytes = package.Data is { Count: > 0 }
            ? JsonSerializer.SerializeToUtf8Bytes(package.Data, LabelJson.Options)
            : null;

        var fingerprint = ComputeFingerprint(labelBytes, dataBytes, package.Assets);
        var manifest = package.Manifest with
        {
            ManifestVersion = Manifest.CurrentVersion,
            Fingerprint = fingerprint,
        };
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, LabelJson.Options);

        using var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);
        WriteEntry(zip, ManifestEntry, manifestBytes);
        WriteEntry(zip, LabelEntry, labelBytes);
        if (dataBytes is not null) WriteEntry(zip, DataEntry, dataBytes);

        foreach (var (id, bytes) in package.Assets.OrderBy(kv => kv.Key, StringComparer.Ordinal))
            WriteEntry(zip, AssetsPrefix + id + AssetExtension(bytes), bytes);
    }

    private static void WriteEntry(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(bytes);
    }

    // ── Load ────────────────────────────────────────────────────────────────────────────────

    public static LabelPackage Load(string path)
    {
        using var fs = File.OpenRead(path);
        return Load(fs);
    }

    public static LabelPackage Load(Stream source)
    {
        using var zip = new ZipArchive(source, ZipArchiveMode.Read);

        var manifestBytes = ReadEntry(zip, ManifestEntry)
            ?? throw new LabelPackageException("Package is missing manifest.json.");
        var manifest = JsonSerializer.Deserialize<Manifest>(manifestBytes, LabelJson.Options)
            ?? throw new LabelPackageException("manifest.json failed to parse.");
        if (manifest.ManifestVersion > Manifest.CurrentVersion)
            throw new LabelPackageException(
                $"Package manifestVersion {manifest.ManifestVersion} is newer than supported ({Manifest.CurrentVersion}).");

        var labelBytes = ReadEntry(zip, LabelEntry)
            ?? throw new LabelPackageException("Package is missing label.json.");
        var document = JsonSerializer.Deserialize<LabelDocument>(labelBytes, LabelJson.Options)
            ?? throw new LabelPackageException("label.json failed to parse.");
        GuardSchemaVersion(document.SchemaVersion);

        var dataBytes = ReadEntry(zip, DataEntry);
        var data = dataBytes is not null ? ParseData(dataBytes) : null;

        var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            if (!entry.FullName.StartsWith(AssetsPrefix, StringComparison.Ordinal) || entry.FullName.EndsWith('/'))
                continue;
            var fileName = entry.FullName[AssetsPrefix.Length..];
            var id = Path.GetFileNameWithoutExtension(fileName);
            if (id.Length == 0) continue;
            assets[id] = ReadAll(entry);
        }

        var fingerprintValid = manifest.Fingerprint is null
            ? (bool?)null
            : string.Equals(manifest.Fingerprint, ComputeFingerprint(labelBytes, dataBytes, assets), StringComparison.OrdinalIgnoreCase);

        return new LabelPackage
        {
            Manifest = manifest,
            Document = document,
            Data = data,
            Assets = assets,
            FingerprintValid = fingerprintValid,
        };
    }

    private static void GuardSchemaVersion(string schemaVersion)
    {
        var major = schemaVersion.Split('.', 2)[0];
        if (int.TryParse(major, out var m) && m > SupportedSchemaMajor)
            throw new LabelPackageException(
                $"label.json schemaVersion {schemaVersion} is a newer major than supported ({SupportedSchemaMajor}).");
    }

    private static byte[]? ReadEntry(ZipArchive zip, string name)
    {
        var entry = zip.GetEntry(name);
        return entry is null ? null : ReadAll(entry);
    }

    private static byte[] ReadAll(ZipArchiveEntry entry)
    {
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static List<IReadOnlyDictionary<string, object?>> ParseData(byte[] dataBytes)
    {
        var rows = new List<IReadOnlyDictionary<string, object?>>();
        using var doc = JsonDocument.Parse(dataBytes, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return rows;
        foreach (var rowEl in doc.RootElement.EnumerateArray())
        {
            if (rowEl.ValueKind != JsonValueKind.Object) continue;
            var row = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var prop in rowEl.EnumerateObject())
                row[prop.Name] = JsonValueToObject(prop.Value);
            rows.Add(row);
        }
        return rows;
    }

    private static object? JsonValueToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => el.GetRawText(),
    };

    // ── Fingerprint ─────────────────────────────────────────────────────────────────────────

    private static string ComputeFingerprint(byte[] labelBytes, byte[]? dataBytes, IReadOnlyDictionary<string, byte[]> assets)
    {
        using var sha = SHA256.Create();
        using var ms = new MemoryStream();
        ms.Write(labelBytes);
        if (dataBytes is not null) ms.Write(dataBytes);
        foreach (var (id, bytes) in assets.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            ms.Write(Encoding.UTF8.GetBytes(id));
            ms.Write(bytes);
        }
        ms.Position = 0;
        return Convert.ToHexString(sha.ComputeHash(ms)).ToLowerInvariant();
    }

    private static string AssetExtension(byte[] bytes)
    {
        if (bytes.Length >= 8 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47) return ".png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return ".jpg";
        if (bytes.Length >= 6 && bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46) return ".gif";
        if (bytes.Length >= 2 && bytes[0] == 0x42 && bytes[1] == 0x4D) return ".bmp";
        return ".bin";
    }
}
