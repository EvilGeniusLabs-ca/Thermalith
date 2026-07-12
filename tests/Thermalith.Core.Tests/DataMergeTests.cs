using System.Globalization;
using Thermalith.Core.Data;
using Thermalith.Core.Tokens;
using Xunit;

namespace Thermalith.Core.Tests;

/// <summary>Data-merge (GitHub #7): token forms `{"name"}` / `{n}` / `{{escape}}` and the canonical CSV shape.</summary>
public class DataMergeTests
{
    // ── TokenResolver: the three token forms + escaping ──────────────────────────────────────────

    private static TokenResolver Resolver(
        IReadOnlyDictionary<string, object?>? data = null,
        IReadOnlyList<object?>? ordinals = null) => new(data, null, null, ordinals);

    [Fact]
    public void Bare_identifier_binds_by_name()
    {
        var r = Resolver(new Dictionary<string, object?> { ["qty"] = 42 });
        Assert.Equal("on hand 42", r.Substitute("on hand {qty}"));
    }

    [Fact]
    public void Quoted_token_binds_by_name_allowing_spaces()
    {
        var r = Resolver(new Dictionary<string, object?> { ["Column A"] = "Widget" });
        Assert.Equal("Widget", r.Substitute("{\"Column A\"}"));
    }

    [Fact]
    public void Ordinal_token_binds_positionally_one_based()
    {
        var r = Resolver(ordinals: new object?[] { "first", "second", "third" });
        Assert.Equal("second", r.Substitute("{2}"));
    }

    [Fact]
    public void Ordinal_reaches_a_blank_header_column_that_name_cannot()
    {
        // Column 4 has no name (blank header) — only {4} can address it.
        var r = Resolver(
            data: new Dictionary<string, object?> { ["A"] = "a" },
            ordinals: new object?[] { "a", "b", "c", "2026-07-11 09:15" });
        Assert.Equal("printed 2026-07-11 09:15", r.Substitute("printed {4}"));
    }

    [Fact]
    public void Empty_cell_renders_empty_not_a_placeholder()
    {
        var r = Resolver(
            data: new Dictionary<string, object?> { ["mid"] = "" },
            ordinals: new object?[] { "x", "", "z" });
        Assert.Equal("[]", r.Substitute("[{\"mid\"}]"));
        Assert.Equal("[]", r.Substitute("[{2}]"));
    }

    [Fact]
    public void Doubled_braces_escape_to_literals()
    {
        var r = Resolver(new Dictionary<string, object?> { ["quantity"] = 5 });
        // {{quantity}} is an escaped literal, NOT a binding.
        Assert.Equal("{quantity}", r.Substitute("{{quantity}}"));
    }

    [Fact]
    public void Unknown_column_and_ordinal_stay_visible()
    {
        var r = Resolver(new Dictionary<string, object?> { ["a"] = "1" }, new object?[] { "1" });
        Assert.Equal("{\"missing\"}", r.Substitute("{\"missing\"}"));
        Assert.Equal("{9}", r.Substitute("{9}"));
    }

    [Fact]
    public void Mixed_name_and_ordinal_inline()
    {
        var r = Resolver(
            data: new Dictionary<string, object?> { ["part_no"] = "AB-1" },
            ordinals: new object?[] { "AB-1", "7" });
        Assert.Equal("Part AB-1 — qty 7", r.Substitute("Part {\"part_no\"} — qty {2}"));
    }

    // ── CsvDataSource: the canonical shape ───────────────────────────────────────────────────────

    private const string Sample =
        "Count,Column A,Column B,,Column D,quantity\n" +
        "1,Widget Alpha,012345678905,2026-07-11 09:15,\"Bracket, anodized\",42\n" +
        "2,Gizmo Bravo,078912345670,2026-07-11 13:42,\"Gizmo, sealed\",7\n";

    private static MergeDataSet Load(string csv, bool hasHeader = true) =>
        CsvDataSource.Parse(new StringReader(csv), null, hasHeader);

    [Fact]
    public void Parses_columns_rows_and_blank_header_as_ordinal_only()
    {
        var ds = Load(Sample);
        Assert.Equal(6, ds.Columns.Count);
        Assert.Equal(2, ds.RowCount);

        Assert.Equal("Count", ds.Columns[0].Name);
        Assert.Null(ds.Columns[3].Name);                     // blank header → ordinal-only
        Assert.Equal("{4}", ds.Columns[3].Token);
        Assert.Equal("{\"Column A\"}", ds.Columns[1].Token);
    }

    [Fact]
    public void Row_addressable_by_name_and_ordinal_with_quoted_field()
    {
        var ds = Load(Sample);
        var row0 = ds.Rows[0];
        Assert.Equal("Widget Alpha", row0.ByName["Column A"]);
        Assert.Equal("2026-07-11 09:15", row0.ByOrdinal[3]);  // the blank-header column, positional
        Assert.Equal("Bracket, anodized", row0.ByOrdinal[4]); // RFC 4180 quoted comma preserved
        Assert.Equal("42", row0.ByName["quantity"]);
    }

    [Fact]
    public void Duplicate_headers_become_ordinal_only()
    {
        var ds = Load("dup,dup,keep\n1,2,3\n");
        Assert.Null(ds.Columns[0].Name);
        Assert.Null(ds.Columns[1].Name);
        Assert.Equal("keep", ds.Columns[2].Name);
    }

    [Fact]
    public void Headerless_csv_is_all_ordinal()
    {
        var ds = Load("a,b,c\nd,e,f\n", hasHeader: false);
        Assert.Equal(3, ds.Columns.Count);
        Assert.Equal(2, ds.RowCount);
        Assert.All(ds.Columns, c => Assert.Null(c.Name));
        Assert.Equal("a", ds.Rows[0].ByOrdinal[0]);
    }

    // ── Merge auto-size (GitHub #7.1): auto-size text grows to the resolved row value ────────────

    private static Thermalith.Core.Model.LabelDocument MergeDoc(bool autoSize) => new()
    {
        Metadata = new Thermalith.Core.Model.LabelMetadata { Name = "m" },
        Canvas = new Thermalith.Core.Model.Canvas { WidthMm = 80, HeightMm = 30, Dpi = 203 },
        Elements =
        [
            new Thermalith.Core.Model.TextElement
            {
                Id = "t", X = 2, Y = 2, W = 6, H = 6,
                Props = new Thermalith.Core.Model.TextProps { Content = "{\"v\"}", FontSizePt = 16, AutoSize = autoSize, Wrap = "none" },
            },
        ],
    };

    private static double RenderedWidth(Thermalith.Core.Model.LabelDocument doc, IReadOnlyDictionary<string, object?> data)
    {
        var renderer = new Thermalith.Core.Rendering.LabelRenderer();
        var resolved = renderer.Resolve(doc, new Thermalith.Core.Rendering.ResolveContext { Data = data });
        return ((Thermalith.Core.Rendering.ResolvedText)resolved.Elements[0]).WMm;
    }

    [Fact]
    public void Auto_size_box_grows_to_the_resolved_merge_value()
    {
        var data = new Dictionary<string, object?> { ["v"] = "A very long merged value" };
        var w = RenderedWidth(MergeDoc(autoSize: true), data);
        Assert.True(w > 20.0, $"auto-size box should grow to the value, got {w:0.#}mm");
    }

    [Fact]
    public void Fixed_box_keeps_its_authored_size_on_merge()
    {
        var data = new Dictionary<string, object?> { ["v"] = "A very long merged value" };
        var w = RenderedWidth(MergeDoc(autoSize: false), data);
        Assert.Equal(6.0, w, 3); // unchanged — a fixed box is the user's box
    }

    [Fact]
    public void End_to_end_row_resolves_through_the_token_resolver()
    {
        var ds = Load(Sample);
        var row = ds.Rows[1];
        var r = new TokenResolver(row.ByName, null, null, row.ByOrdinal);
        Assert.Equal("Gizmo Bravo #2 @ 2026-07-11 13:42 qty 7",
            r.Substitute("{\"Column A\"} #{\"Count\"} @ {4} qty {\"quantity\"}"));
    }
}
