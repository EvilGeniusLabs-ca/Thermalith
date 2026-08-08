using Niimbot.Net.Profiles;
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
        Assert.Equal(0, b1.PrintDirectionDeg);        // top-fed (re-mined field)
        Assert.True(b1.Verified);                     // B1 is hardware-verified
    }

    [Fact]
    public void B21_Pro_resolves_at_300_dpi()
    {
        // GitHub #17: shipped as a phantom 229 dpi / 450px, so 50×30mm labels printed at ~76% size.
        var b21Pro = PrinterCatalog.LoadEmbedded().FindByModelId(785);

        Assert.NotNull(b21Pro);
        Assert.Equal("B21_Pro", b21Pro!.Model);
        Assert.Equal(300, b21Pro.Dpi);
        Assert.Equal(591, b21Pro.PrintheadPx);        // ceil(50mm × 11.81px/mm)
    }

    [Fact]
    public void No_printer_has_a_dpi_outside_the_two_real_values()
    {
        // NIIMBOT ships exactly two head resolutions. Anything else means the importer is deriving
        // dpi arithmetically again instead of reading paccuracyName — see GitHub #17.
        var offenders = PrinterCatalog.LoadEmbedded().Printers
            .Where(p => p.Dpi is not (203 or 300))
            .Select(p => $"{p.Model} = {p.Dpi}dpi")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Printhead_pixels_agree_with_dpi_and_printable_width()
    {
        var offenders = PrinterCatalog.LoadEmbedded().Printers
            .Where(p => p.PrintheadPx != (int)Math.Ceiling(p.PrintableWidthMm * (p.Dpi == 300 ? 11.81 : 8.0)))
            .Select(p => $"{p.Model}: {p.PrintheadPx}px for {p.PrintableWidthMm}mm @ {p.Dpi}dpi")
            .ToList();

        Assert.Empty(offenders);
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
