using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Thermalith.Core.Fonts;

/// <summary>One searchable clip glyph: which bundled font, its codepoint, display name, and extra
/// keyword tags (synonyms/aliases beyond the name). Codepoint + font feed <c>ClipBaker</c>; name + tags
/// feed the palette search.</summary>
public sealed record ClipGlyph(string Font, int Codepoint, string Name, IReadOnlyList<string> Tags);

/// <summary>A bundled clip font as it appears in the palette: catalog key, friendly tab label, glyph count.</summary>
public sealed record ClipFontInfo(string Key, string Label, int Count);

/// <summary>
/// The compiled clip-art search index (GitHub #2): the unified <c>{font, codepoint, name, tags}</c> view
/// the palette browses and searches, loaded from <c>clip-index.json</c> (built by
/// <c>tools/build-clip-index</c> from each font's cmap + its metadata). The raw per-font metadata is
/// build-time source; only this slim index ships. Search is a scored substring match over name + tags,
/// optionally scoped to one font (the per-font tabs) or across all (the Search tab).
/// </summary>
public sealed class ClipIndex
{
    private readonly List<ClipGlyph> _glyphs;
    private readonly Dictionary<string, List<ClipGlyph>> _byFont;

    public IReadOnlyList<ClipFontInfo> Fonts { get; }
    public IReadOnlyList<ClipGlyph> Glyphs => _glyphs;

    private ClipIndex(IReadOnlyList<ClipFontInfo> fonts, List<ClipGlyph> glyphs)
    {
        Fonts = fonts;
        _glyphs = glyphs;
        _byFont = glyphs.GroupBy(g => g.Font).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
    }

    /// <summary>All glyphs of one font, in index order (for a per-font browse tab). Empty for an unknown key.</summary>
    public IReadOnlyList<ClipGlyph> ForFont(string fontKey) =>
        _byFont.TryGetValue(fontKey, out var list) ? list : [];

    /// <summary>
    /// Filter by <paramref name="query"/> over name + tags, scoped to <paramref name="fontKey"/> when given
    /// (else across all fonts). Ranked: exact name &gt; name-prefix &gt; name-substring &gt; tag-prefix &gt;
    /// tag-substring. A blank query returns the browse order (all, or the font's glyphs). Capped at
    /// <paramref name="limit"/>.
    /// </summary>
    public IReadOnlyList<ClipGlyph> Search(string? query, string? fontKey = null, int limit = 500)
    {
        var source = fontKey is null ? (IReadOnlyList<ClipGlyph>)_glyphs : ForFont(fontKey);

        if (string.IsNullOrWhiteSpace(query))
            return source.Count <= limit ? source : source.Take(limit).ToList();

        var q = query.Trim().ToLowerInvariant();
        return source
            .Select(g => (g, score: Score(g, q)))
            .Where(t => t.score > 0)
            .OrderByDescending(t => t.score)
            .ThenBy(t => t.g.Name, StringComparer.Ordinal)
            .Take(limit)
            .Select(t => t.g)
            .ToList();
    }

    private static int Score(ClipGlyph g, string q)
    {
        var name = g.Name.ToLowerInvariant();
        if (name == q) return 100;
        if (name.StartsWith(q, StringComparison.Ordinal)) return 50;
        if (name.Contains(q, StringComparison.Ordinal)) return 25;
        var best = 0;
        foreach (var t in g.Tags)
        {
            if (t.StartsWith(q, StringComparison.Ordinal)) return 10;
            if (best == 0 && t.Contains(q, StringComparison.Ordinal)) best = 5;
        }
        return best;
    }

    // ── Load ──────────────────────────────────────────────────────────────────────────────────

    public static ClipIndex LoadFile(string path)
    {
        using var fs = File.OpenRead(path);
        return Load(fs);
    }

    /// <summary>Load the compiled index embedded in <paramref name="assembly"/> (default: the assembly that
    /// owns this type) — the runtime source. Throws if the resource is missing.</summary>
    public static ClipIndex LoadEmbedded(Assembly? assembly = null)
    {
        var asm = assembly ?? typeof(ClipIndex).Assembly;
        var name = Array.Find(asm.GetManifestResourceNames(),
            r => r.EndsWith(".Clipart.clip-index.json", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Embedded clip-index.json resource not found.");
        using var s = asm.GetManifestResourceStream(name)!;
        return Load(s);
    }

    public static ClipIndex Load(Stream json)
    {
        var dto = JsonSerializer.Deserialize<IndexDto>(json)
            ?? throw new InvalidOperationException("clip-index.json failed to parse.");
        var glyphs = dto.Glyphs.Select(x =>
            new ClipGlyph(x.F, x.C, x.N, (IReadOnlyList<string>?)x.T ?? [])).ToList();
        var fonts = dto.Fonts.Select(x => new ClipFontInfo(x.Key, x.Label, x.Count)).ToList();
        return new ClipIndex(fonts, glyphs);
    }

    private sealed record IndexDto(
        [property: JsonPropertyName("fonts")] List<FontDto> Fonts,
        [property: JsonPropertyName("glyphs")] List<GlyphDto> Glyphs);

    private sealed record FontDto(
        [property: JsonPropertyName("key")] string Key,
        [property: JsonPropertyName("label")] string Label,
        [property: JsonPropertyName("count")] int Count);

    private sealed record GlyphDto(
        [property: JsonPropertyName("f")] string F,
        [property: JsonPropertyName("c")] int C,
        [property: JsonPropertyName("n")] string N,
        [property: JsonPropertyName("t")] List<string>? T);
}
