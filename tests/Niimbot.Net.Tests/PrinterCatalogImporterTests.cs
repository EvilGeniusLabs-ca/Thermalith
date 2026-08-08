using Niimbot.Net.Profiles;
using Xunit;

namespace Niimbot.Net.Tests;

/// <summary>
/// Guards the devices.json → catalog derivation. The bug these exist for (GitHub #17): the importer
/// read <c>paccuracy</c> (an internal 8/9 code) instead of <c>paccuracyName</c> (the real dpi) and
/// scaled it by 25.4, inventing 229 dpi for every 300 dpi printer and undersizing its printhead.
/// </summary>
public class PrinterCatalogImporterTests
{
    private static string Device(string name, int code, int paccuracy, string paccuracyName, int widthSetEnd) =>
        $$"""
        [{
          "name": "{{name}}", "seriesName": "test", "codes": [{{code}}],
          "paccuracy": {{paccuracy}}, "paccuracyName": "{{paccuracyName}}",
          "defaultWidth": 50, "defaultHeigth": 30, "maxPrintWidth": 50, "maxPrintHeight": 200,
          "widthSetStart": 20, "widthSetEnd": {{widthSetEnd}},
          "solubilitySetStart": 1, "solubilitySetEnd": 5, "solubilitySetDefault": 3,
          "paperType": "1,2", "rfidType": 1, "printDirection": 0
        }]
        """;

    [Fact]
    public void Dpi_comes_from_paccuracyName_not_paccuracy()
    {
        // paccuracy 9 is NOT 9 px/mm — the old round(9 × 25.4) gave a phantom 229 dpi.
        var catalog = PrinterCatalogImporter.Import(Device("B21_Pro", 785, paccuracy: 9, "300", widthSetEnd: 50));

        var entry = catalog.FindByModelId(785);
        Assert.NotNull(entry);
        Assert.Equal(300, entry!.Dpi);
        Assert.Equal(591, entry.PrintheadPx);   // ceil(50mm × 11.81px/mm), matches niimbluelib
    }

    [Fact]
    public void Two_hundred_three_dpi_printers_are_unchanged()
    {
        var catalog = PrinterCatalogImporter.Import(Device("B1", 4096, paccuracy: 8, "203", widthSetEnd: 48));

        var entry = catalog.FindByModelId(4096);
        Assert.Equal(203, entry!.Dpi);
        Assert.Equal(384, entry.PrintheadPx);   // 48mm × 8px/mm — the hardware-verified reference
    }

    [Fact]
    public void D11_H_derivation_matches_the_hardware_verified_values()
    {
        // Verified on real hardware 2026-06-17: 300 dpi, 142px head over a 12mm printable width.
        // The corrected rule has to reproduce that without the hand-patched catalog entry.
        var catalog = PrinterCatalogImporter.Import(Device("D11_H", 528, paccuracy: 9, "300", widthSetEnd: 12));

        var entry = catalog.FindByModelId(528);
        Assert.Equal(300, entry!.Dpi);
        Assert.Equal(142, entry.PrintheadPx);
    }

    [Fact]
    public void Missing_paccuracyName_falls_back_to_the_paccuracy_code()
    {
        // Older/partial upstream records: 9 still means 300, never 229.
        const string raw = """
        [{ "name": "Mystery", "codes": [1], "paccuracy": 9, "widthSetEnd": 50,
           "solubilitySetStart": 1, "solubilitySetEnd": 5, "solubilitySetDefault": 3 }]
        """;

        var entry = PrinterCatalogImporter.Import(raw).FindByModelId(1);
        Assert.Equal(300, entry!.Dpi);
        Assert.Equal(591, entry.PrintheadPx);
    }
}
