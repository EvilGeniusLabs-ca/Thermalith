using SkiaSharp;

namespace Thermalith.Core.Fonts;

/// <summary>
/// Resolves font families to SkiaSharp typefaces with the fallback chain of §6.3.4:
/// <c>[requested family] → [bundled default] → [OS default]</c>. The bundled default is an
/// embedded OFL/Apache font (Roboto) so rendering never tofus-out and the headless server and the
/// editor agree on the fallback floor. When a requested family is not installed, the substitution
/// is recorded so the validator can warn (§6.7).
/// </summary>
public sealed class FontService : IDisposable
{
    /// <summary>The family name of the bundled fallback font. Authoring with this name is always deterministic.</summary>
    public const string BundledFamily = "Roboto";

    private readonly SKTypeface _bundled;
    private readonly Dictionary<(string Family, bool Bold, bool Italic), SKTypeface> _cache = new();
    private readonly Dictionary<int, SKTypeface?> _glyphFallback = new();

    public FontService()
    {
        var asm = typeof(FontService).Assembly;
        const string name = "Thermalith.Core.Fonts.Roboto-Regular.ttf";
        using var stream = asm.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded font resource '{name}' not found.");
        _bundled = SKTypeface.FromStream(stream)
            ?? throw new InvalidOperationException("Bundled fallback font failed to load.");
    }

    /// <summary>True if a real (non-fallback) typeface for <paramref name="family"/> is available on this host.</summary>
    public bool IsAvailable(string? family)
    {
        if (string.IsNullOrWhiteSpace(family)) return true;
        if (string.Equals(family, BundledFamily, StringComparison.OrdinalIgnoreCase)) return true;
        using var tf = SKFontManager.Default.MatchFamily(family, SKFontStyle.Normal);
        return tf is not null && string.Equals(tf.FamilyName, family, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A typeface that can render <paramref name="codepoint"/> when the primary font can't —
    /// asks the OS font manager (covers CJK and other scripts without bundling a large font), cached
    /// per codepoint. Returns null if nothing on the host has the glyph (it then tofus, as before).</summary>
    public SKTypeface? FallbackForCodepoint(int codepoint)
    {
        if (_glyphFallback.TryGetValue(codepoint, out var hit)) return hit;
        var tf = SKFontManager.Default.MatchCharacter(codepoint);
        _glyphFallback[codepoint] = tf;
        return tf;
    }

    /// <summary>Resolve a typeface for the family + style, falling back to the bundled font when not installed.</summary>
    public SKTypeface Resolve(string? family, bool bold, bool italic)
    {
        var key = (family ?? "", bold, italic);
        if (_cache.TryGetValue(key, out var cached)) return cached;

        var tf = ResolveUncached(family, bold, italic);
        _cache[key] = tf;
        return tf;
    }

    private SKTypeface ResolveUncached(string? family, bool bold, bool italic)
    {
        var style = new SKFontStyle(
            bold ? (int)SKFontStyleWeight.Bold : (int)SKFontStyleWeight.Normal,
            (int)SKFontStyleWidth.Normal,
            italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);

        // Bundled family (or unspecified) → the embedded font directly.
        if (string.IsNullOrWhiteSpace(family) || string.Equals(family, BundledFamily, StringComparison.OrdinalIgnoreCase))
            return _bundled;

        // Requested family installed on this host → use it.
        var matched = SKFontManager.Default.MatchFamily(family, style);
        if (matched is not null && string.Equals(matched.FamilyName, family, StringComparison.OrdinalIgnoreCase))
            return matched;
        matched?.Dispose();

        // Not installed → bundled fallback (validator warns about the substitution).
        return _bundled;
    }

    public void Dispose()
    {
        _bundled.Dispose();
        foreach (var tf in _cache.Values)
            if (!ReferenceEquals(tf, _bundled))
                tf.Dispose();
        _cache.Clear();
        foreach (var tf in _glyphFallback.Values)
            if (tf is not null && !ReferenceEquals(tf, _bundled))
                tf.Dispose();
        _glyphFallback.Clear();
    }
}
