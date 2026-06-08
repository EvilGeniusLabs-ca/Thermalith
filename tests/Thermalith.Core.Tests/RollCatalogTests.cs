using Thermalith.Core.Catalog;
using Xunit;

namespace Thermalith.Core.Tests;

public class RollCatalogTests
{
    [Theory]
    [InlineData("T40*20-320WHITE", 40d, 20d)]
    [InlineData("T50*30-230White", 50d, 30d)]
    [InlineData("T28*57-130Mist Cable", 28d, 57d)]
    [InlineData("50 x 30 mm", 50d, 30d)]
    [InlineData("Ø25", null, null)]
    [InlineData("", null, null)]
    public void Parses_size_from_part_name(string partName, double? w, double? h)
    {
        var size = RollNaming.ParseSize(partName);
        if (w is null) { Assert.Null(size); return; }
        Assert.NotNull(size);
        Assert.Equal(w, size!.Value.WidthMm);
        Assert.Equal(h, size.Value.HeightMm);
    }

    [Fact]
    public void Upsert_adds_keyed_roll_and_sets_last_used()
    {
        var roll = new RollDefinition { Barcode = "BARCODE-REDACTED", PartName = "T50*30-230White", WidthMm = 50, HeightMm = 30, PaperType = "gap" };
        var catalog = new LabelRollCatalog().Upsert(roll);

        Assert.Single(catalog.Rolls);
        Assert.Equal(roll, catalog.LastUsed);
        Assert.Equal(roll, catalog.FindByBarcode("BARCODE-REDACTED"));
    }

    [Fact]
    public void Upsert_replaces_same_barcode_rather_than_duplicating()
    {
        var catalog = new LabelRollCatalog()
            .Upsert(new RollDefinition { Barcode = "abc", WidthMm = 50, HeightMm = 30 })
            .Upsert(new RollDefinition { Barcode = "abc", WidthMm = 40, HeightMm = 20 });

        Assert.Single(catalog.Rolls);
        Assert.Equal(40, catalog.FindByBarcode("abc")!.WidthMm);
    }

    [Fact]
    public void Barcodeless_roll_is_remembered_as_last_used_but_not_keyed()
    {
        // Manual/offline roll with no RFID barcode: remembered as default, not added to the keyed list.
        var manual = new RollDefinition { WidthMm = 30, HeightMm = 15, PaperType = "black" };
        var catalog = new LabelRollCatalog().Upsert(manual);

        Assert.Empty(catalog.Rolls);
        Assert.Equal(manual, catalog.LastUsed);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var catalog = new LabelRollCatalog()
            .Upsert(new RollDefinition { Barcode = "x", BoxId = "30486", PartName = "T40*20-320WHITE", Name = "40x20", WidthMm = 40, HeightMm = 20, PaperType = "gap", Density = 3 });

        var reparsed = LabelRollCatalog.FromJson(catalog.ToJson());
        Assert.Equal(catalog.Rolls.Count, reparsed.Rolls.Count);
        var r = reparsed.FindByBarcode("x")!;
        Assert.Equal("30486", r.BoxId);
        Assert.Equal("T40*20-320WHITE", r.PartName);
        Assert.Equal(40, r.WidthMm);
        Assert.Equal(3, r.Density);
    }
}
