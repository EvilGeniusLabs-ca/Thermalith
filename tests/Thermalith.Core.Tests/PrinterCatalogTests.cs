using Thermalith.Core.Catalog;
using Xunit;

namespace Thermalith.Core.Tests;

public class PrinterCatalogTests
{
    [Fact]
    public void Embedded_catalog_loads_with_printers()
    {
        var catalog = PrinterCatalog.LoadEmbedded();
        Assert.NotEmpty(catalog.Printers);
        Assert.Equal("1", catalog.SchemaVersion);
    }

    [Fact]
    public void B1_resolves_by_model_id_with_expected_capabilities()
    {
        var catalog = PrinterCatalog.LoadEmbedded();

        var b1 = catalog.FindByModelId(4096);
        Assert.NotNull(b1);
        Assert.Equal("B1", b1!.Model);
        Assert.Equal(203, b1.Dpi);
        Assert.Equal(48, b1.PrintableWidthMm);        // printable (head) width, not the 50mm stock
        Assert.Equal(384, b1.PrintheadPx);            // 48mm × 8px/mm
        Assert.Equal(50, b1.StockWidthMm);
        Assert.Equal(3, b1.DensityDefault);
        Assert.Contains(1, b1.PaperTypes);            // gap
    }

    [Fact]
    public void Importer_round_trips_through_our_json()
    {
        var catalog = PrinterCatalog.LoadEmbedded();
        var reparsed = PrinterCatalog.FromJson(catalog.ToJson());
        Assert.Equal(catalog.Printers.Count, reparsed.Printers.Count);
        Assert.Equal(catalog.FindByModelId(4096)!.PrintheadPx, reparsed.FindByModelId(4096)!.PrintheadPx);
    }
}
