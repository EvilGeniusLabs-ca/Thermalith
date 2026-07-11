using SkiaSharp;

namespace Thermalith.Core.Fonts;

/// <summary>
/// The clip-art palette's font source (GitHub #2): resolves a clip-font <b>key</b> (the font file stem,
/// e.g. <c>materialdesignicons-webfont</c> or <c>NotoColorEmoji</c>) to an <see cref="SKTypeface"/>.
/// Loads TrueType/OpenType files from a directory and caches the typefaces. This abstracts *where* the
/// fonts live — a directory today; whether they end up embedded resources or shipped alongside the exe
/// is a distribution decision the rest of the pipeline doesn't need to know.
/// </summary>
public sealed class ClipFontCatalog : IDisposable
{
    private readonly Dictionary<string, string> _paths;                 // key → file path
    private readonly Dictionary<string, SKTypeface?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Index every <c>*.ttf</c>/<c>*.otf</c> directly under <paramref name="directory"/> by its file stem.</summary>
    public ClipFontCatalog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);

        _paths = Directory.EnumerateFiles(directory, "*.ttf")
            .Concat(Directory.EnumerateFiles(directory, "*.otf"))
            .ToDictionary(Path.GetFileNameWithoutExtension!, p => p, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The available clip-font keys.</summary>
    public IReadOnlyCollection<string> Keys => _paths.Keys;

    /// <summary>True if <paramref name="key"/> names a bundled clip font.</summary>
    public bool Contains(string key) => _paths.ContainsKey(key);

    /// <summary>Resolve a clip-font key to its typeface (cached), or <c>null</c> if the key is unknown or the
    /// file fails to load. The catalog owns the returned typeface's lifetime — do not dispose it.</summary>
    public SKTypeface? Resolve(string key)
    {
        if (_cache.TryGetValue(key, out var cached)) return cached;
        var tf = _paths.TryGetValue(key, out var path) ? SKTypeface.FromFile(path) : null;
        _cache[key] = tf;
        return tf;
    }

    public void Dispose()
    {
        foreach (var tf in _cache.Values) tf?.Dispose();
        _cache.Clear();
    }
}
