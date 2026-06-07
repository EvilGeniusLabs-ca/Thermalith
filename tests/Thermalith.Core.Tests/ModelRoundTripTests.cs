using Thermalith.Core.Model;
using Thermalith.Core.Serialization;
using Thermalith.Core.Tokens;
using Xunit;

namespace Thermalith.Core.Tests;

public class ModelRoundTripTests
{
    [Fact]
    public void Parses_the_kitchen_sink_example()
    {
        var doc = LabelDocumentSerializer.FromJson(TestSupport.KitchenSinkJson());

        Assert.Equal("1.0", doc.SchemaVersion);
        Assert.Equal("kitchen-sink-50x30", doc.Metadata.Name);
        Assert.Equal(50, doc.Canvas.WidthMm);
        Assert.Equal(30, doc.Canvas.HeightMm);
        Assert.Equal(203, doc.Canvas.Dpi);
        Assert.Equal(11, doc.Elements.Count);

        // Every one of the 8 control types is exercised by the example.
        Assert.Contains(doc.Elements, e => e is TextElement);
        Assert.Contains(doc.Elements, e => e is ImageElement);
        Assert.Contains(doc.Elements, e => e is ShapeElement);
        Assert.Contains(doc.Elements, e => e is BarcodeElement);
        Assert.Contains(doc.Elements, e => e is QrElement);
        Assert.Contains(doc.Elements, e => e is SerialElement);
        Assert.Contains(doc.Elements, e => e is DateTimeElement);
        Assert.Contains(doc.Elements, e => e is TableElement);
    }

    [Fact]
    public void Auto_and_typed_props_round_trip()
    {
        var doc = LabelDocumentSerializer.FromJson(TestSupport.KitchenSinkJson());

        var qr = Assert.IsType<QrElement>(doc.Elements.Single(e => e.Id == "el_qr"));
        Assert.Null(qr.Props.ModuleSizeMm); // "auto" → null

        var title = Assert.IsType<TextElement>(doc.Elements.Single(e => e.Id == "el_title"));
        Assert.Equal("{name}", title.Props.Content);
        Assert.Equal("fill", title.Props.FontSizing);
        Assert.True(title.Props.Bold);

        var table = Assert.IsType<TableElement>(doc.Elements.Single(e => e.Id == "el_spec"));
        Assert.Equal([7d, 8d], table.Props.ColumnWidthsMm!);
        Assert.Null(table.Props.RowHeightsMm); // "auto"
        Assert.Equal("{lot}", table.Props.Cells![1][0].Content);
    }

    [Fact]
    public void Document_survives_a_serialize_deserialize_cycle()
    {
        var original = LabelDocumentSerializer.FromJson(TestSupport.KitchenSinkJson());
        var roundTripped = LabelDocumentSerializer.FromJson(LabelDocumentSerializer.ToJson(original));

        Assert.Equal(original.Elements.Count, roundTripped.Elements.Count);
        Assert.Equal(original.Canvas, roundTripped.Canvas);
        Assert.Equal(
            original.Elements.Select(e => (e.Id, e.Type)),
            roundTripped.Elements.Select(e => (e.Id, e.Type)));
    }

    [Fact]
    public void Unknown_fields_round_trip_via_extension_data()
    {
        const string json = """
        {
          "schemaVersion": "1.0",
          "metadata": { "name": "fwd-compat" },
          "canvas": { "widthMm": 40, "heightMm": 30, "futureCanvasField": 42 },
          "elements": [
            { "id": "el1", "type": "text", "x": 1, "y": 1, "w": 10, "h": 5,
              "props": { "content": "hi", "futureTextField": "keep me" } }
          ],
          "futureRootField": { "nested": true }
        }
        """;

        var doc = LabelDocumentSerializer.FromJson(json);
        var reserialized = LabelDocumentSerializer.ToJson(doc);

        Assert.Contains("futureRootField", reserialized);
        Assert.Contains("futureCanvasField", reserialized);
        Assert.Contains("futureTextField", reserialized);
        Assert.Contains("keep me", reserialized);
    }

    [Fact]
    public void Token_scanner_finds_all_referenced_tokens()
    {
        var doc = LabelDocumentSerializer.FromJson(TestSupport.KitchenSinkJson());
        var tokens = TokenScanner.Scan(doc);

        Assert.Equal(new[] { "name", "sku", "url", "price", "lot" }.OrderBy(x => x), tokens.OrderBy(x => x));
    }
}
