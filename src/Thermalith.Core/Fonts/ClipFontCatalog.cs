using System.Reflection;
using SkiaSharp;

namespace Thermalith.Core.Fonts;

/// <summary>
/// The clip-art palette's font source (GitHub #2): resolves a clip-font <b>key</b> (the font file stem,
/// e.g. <c>materialdesignicons-webfont</c> or <c>NotoColorEmoji</c>) to an <see cref="SKTypeface"/>. The
/// fonts ship <b>embedded</b> in the assembly (<see cref="CreateEmbedded"/>); the directory constructor
/// exists for tooling/tests that load from the source tree. Typefaces are loaded lazily and cached.
/// </summary>
public sealed class ClipFontCatalog : IDisposable
{
    private const string EmbeddedMarker = ".Clipart.";

    private readonly Dictionary<string, Func<SKTypeface?>> _loaders;
    private readonly Dictionary<string, SKTypeface?> _cache = new(StringComparer.OrdinalIgnoreCase);

    private ClipFontCatalog(Dictionary<string, Func<SKTypeface?>> loaders) => _loaders = loaders;

    /// <summary>Index every <c>*.ttf</c>/<c>*.otf</c> directly under <paramref name="directory"/> by its file stem.</summary>
    public ClipFontCatalog(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);

        _loaders = Directory.EnumerateFiles(directory, "*.ttf")
            .Concat(Directory.EnumerateFiles(directory, "*.otf"))
            .ToDictionary(
                Path.GetFileNameWithoutExtension!,
                p => (Func<SKTypeface?>)(() => SKTypeface.FromFile(p)),
                StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Build a catalog from the clip fonts embedded in <paramref name="assembly"/>
    /// (default: the assembly that owns this type) — the runtime source.</summary>
    public static ClipFontCatalog CreateEmbedded(Assembly? assembly = null)
    {
        var asm = assembly ?? typeof(ClipFontCatalog).Assembly;
        var loaders = new Dictionary<string, Func<SKTypeface?>>(StringComparer.OrdinalIgnoreCase);
        foreach (var res in asm.GetManifestResourceNames())
        {
            if (!res.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)) continue;
            var at = res.IndexOf(EmbeddedMarker, StringComparison.Ordinal);
            if (at < 0) continue;
            var key = res[(at + EmbeddedMarker.Length)..^4]; // strip prefix and ".ttf"
            var name = res;
            loaders[key] = () =>
            {
                using var s = asm.GetManifestResourceStream(name);
                return s is null ? null : SKTypeface.FromStream(s);
            };
        }
        return new ClipFontCatalog(loaders);
    }

    /// <summary>The available clip-font keys.</summary>
    public IReadOnlyCollection<string> Keys => _loaders.Keys;

    /// <summary>True if <paramref name="key"/> names a bundled clip font.</summary>
    public bool Contains(string key) => _loaders.ContainsKey(key);

    /// <summary>Resolve a clip-font key to its typeface (cached), or <c>null</c> if the key is unknown or the
    /// font fails to load. The catalog owns the returned typeface's lifetime — do not dispose it.</summary>
    public SKTypeface? Resolve(string key)
    {
        if (_cache.TryGetValue(key, out var cached)) return cached;
        var tf = _loaders.TryGetValue(key, out var load) ? load() : null;
        _cache[key] = tf;
        return tf;
    }

    public void Dispose()
    {
        foreach (var tf in _cache.Values) tf?.Dispose();
        _cache.Clear();
    }
}
