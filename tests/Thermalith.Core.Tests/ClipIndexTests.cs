using Thermalith.Core.Fonts;
using Xunit;

namespace Thermalith.Core.Tests;

public class ClipIndexTests
{
    private const string Mdi = "materialdesignicons-webfont";

    private static ClipIndex Load()
    {
        var path = FindIndex();
        Assert.True(path is not null, "could not locate Clipart/clip-index.json");
        return ClipIndex.LoadFile(path!);
    }

    [Fact]
    public void Loads_all_fonts_and_glyphs()
    {
        var idx = Load();

        Assert.Equal(8, idx.Fonts.Count);
        Assert.True(idx.Glyphs.Count > 20000, $"only {idx.Glyphs.Count} glyphs");
        // Per-font counts match the enumerated cmap of the committed fonts.
        var mdi = idx.Fonts.Single(f => f.Key == Mdi);
        Assert.Equal(7447, mdi.Count);
        Assert.Equal(mdi.Count, idx.ForFont(Mdi).Count);
        Assert.Empty(idx.ForFont("no-such-font"));
    }

    [Fact]
    public void Every_glyph_has_a_font_name_and_codepoint()
    {
        var idx = Load();
        var g = idx.Glyphs[0];
        Assert.False(string.IsNullOrEmpty(g.Font));
        Assert.False(string.IsNullOrEmpty(g.Name));
        Assert.True(g.Codepoint > 0);
    }

    [Fact]
    public void Search_matches_names_and_synonym_tags()
    {
        var idx = Load();

        // 'trash' is a synonym tag on MDI's delete icons (not in the name) — tag search must find them.
        var trash = idx.Search("trash");
        Assert.NotEmpty(trash);
        Assert.Contains(trash, g => g.Name.Contains("delete") || g.Name.Contains("trash"));

        // Name-substring match.
        Assert.Contains(idx.Search("heart"), g => g.Name.Contains("heart"));
    }

    [Fact]
    public void Search_can_scope_to_one_font()
    {
        var idx = Load();

        var scoped = idx.Search("arrow", fontKey: Mdi);
        Assert.NotEmpty(scoped);
        Assert.All(scoped, g => Assert.Equal(Mdi, g.Font));
    }

    [Fact]
    public void Blank_query_returns_browse_order_capped()
    {
        var idx = Load();

        var browse = idx.Search("", fontKey: Mdi, limit: 50);
        Assert.Equal(50, browse.Count);
        Assert.All(browse, g => Assert.Equal(Mdi, g.Font));
    }

    [Fact]
    public void Exact_name_ranks_above_substring()
    {
        var idx = Load();

        var results = idx.Search("home", fontKey: Mdi);
        Assert.NotEmpty(results);
        // The exact glyph named "home" should sort ahead of "home-account" etc.
        Assert.Equal("home", results[0].Name);
    }

    private static string? FindIndex()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null)
        {
            var candidate = Path.Combine(d.FullName, "src", "Thermalith.Core", "Fonts", "Clipart", "clip-index.json");
            if (File.Exists(candidate)) return candidate;
            d = d.Parent;
        }
        return null;
    }
}
