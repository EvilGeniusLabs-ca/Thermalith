using Niimbot.Net.Commands;
using Niimbot.Net.Profiles;
using Xunit;

namespace Niimbot.Net.Tests;

public class ProfileTests
{
    [Fact]
    public void B1_resolves_from_its_model_id()
    {
        var profile = PrinterProfiles.FromModelId(4096);
        Assert.Equal(PrinterModel.B1, profile.Model);
        Assert.Equal(203, profile.Dpi);
        Assert.Equal(384, profile.PrintheadPixels);
        Assert.True(profile.SupportsRfid);
        Assert.Equal(PrintTaskVersion.B1, profile.PrintTaskVersion);
    }

    [Fact]
    public void B1_max_print_width_is_about_48mm()
    {
        var profile = PrinterProfiles.B1;
        // 384 px / (203/25.4 px-per-mm) ≈ 48.05 mm
        Assert.InRange(profile.MaxPrintWidthMm, 47.5, 48.5);
        Assert.InRange(profile.PixelsPerMm, 7.9, 8.1);
    }

    [Fact]
    public void Unknown_model_falls_back_but_keeps_the_reported_id()
    {
        var profile = PrinterProfiles.FromModelId(0xDEAD);
        Assert.Equal(PrinterModel.Unknown, profile.Model);
        Assert.Contains(0xDEAD, profile.ModelIds);
    }

    [Fact]
    public void B1_supports_the_expected_label_types()
    {
        Assert.Equal(
            [LabelType.WithGaps, LabelType.Black, LabelType.Transparent],
            PrinterProfiles.B1.SupportedLabelTypes);
    }

    [Fact]
    public void B4_derives_the_wide_head_and_stays_a_b1_print_task()
    {
        var b4 = PrinterProfiles.FromModelId(6656);
        Assert.Equal("B4", b4.ModelName);
        Assert.Equal(832, b4.PrintheadPixels);
        Assert.InRange(b4.MaxPrintWidthMm, 103, 105);          // ~104 mm, not the B1 48 mm
        Assert.Equal(PrintDirection.Top, b4.PrintDirection);
        Assert.Equal(PrintTaskVersion.B1, b4.PrintTaskVersion);
    }

    [Fact]
    public void D_series_derives_left_feed_and_the_d110_print_task()
    {
        // D11 (512) and D110 (2304/2305) are the side-fed small label makers.
        foreach (var id in new[] { 512, 2304 })
        {
            var p = PrinterProfiles.FromModelId(id);
            Assert.Equal(PrintDirection.Left, p.PrintDirection);
            Assert.Equal(PrintTaskVersion.D110, p.PrintTaskVersion);
        }
    }

    // Print task follows niimbluelib's model-to-task table (GitHub #21).

    [Theory]
    [InlineData(4608, 4, PrintTaskVersion.B1)]       // M2_H: listed on B1 even at protocol 4
    [InlineData(4864, 5, PrintTaskVersion.D110MV4)]  // K3: unlisted, protocol 5 -> fallback
    [InlineData(4097, 4, PrintTaskVersion.D110MV4)]  // B1_PRO
    [InlineData(785, null, PrintTaskVersion.D110MV4)] // B21_PRO
    [InlineData(2320, 4, PrintTaskVersion.D110MV4)]  // D110_M v4
    [InlineData(2320, 3, PrintTaskVersion.B1)]       // D110_M older
    [InlineData(776, null, PrintTaskVersion.D110)]   // B21S_C2B
    [InlineData(2560, null, PrintTaskVersion.B1)]    // D101
    [InlineData(0xDEAD, 4, PrintTaskVersion.D110MV4)] // unknown id, protocol 4+
    [InlineData(0xDEAD, 3, PrintTaskVersion.B1)]     // unknown id, older protocol
    public void Print_task_follows_niimbluelib(int modelId, int? protocol, PrintTaskVersion expected) =>
        Assert.Equal(expected, PrinterProfiles.FromModelId(modelId, protocol).PrintTaskVersion);

    [Theory]
    [InlineData(4096, PrintTaskVersion.B1)]      // B1
    [InlineData(6656, PrintTaskVersion.B1)]      // B4
    [InlineData(272, PrintTaskVersion.B1)]       // B3S_P
    [InlineData(528, PrintTaskVersion.D110MV4)]  // D11_H
    public void Hardware_verified_printers_keep_their_task_at_any_protocol(int modelId, PrintTaskVersion expected)
    {
        foreach (var protocol in new int?[] { null, 3, 4, 5 })
            Assert.Equal(expected, PrinterProfiles.FromModelId(modelId, protocol).PrintTaskVersion);
    }

    [Fact]
    public void Only_b1_and_b4_are_marked_verified()
    {
        Assert.True(PrinterProfiles.FromModelId(4096).Verified);
        Assert.True(PrinterProfiles.FromModelId(6656).Verified);
        Assert.False(PrinterProfiles.FromModelId(512).Verified);    // D11 — catalogue-derived, unverified
        Assert.False(PrinterProfiles.FromModelId(0xDEAD).Verified); // unknown
    }

    [Fact]
    public void App_data_override_can_add_a_new_model_via_merge()
    {
        var baseline = PrinterCatalog.LoadEmbedded();
        var overrides = PrinterCatalog.FromJson("""
            { "printers": [ { "model": "ZZ-Test", "ids": [99999], "dpi": 203,
              "printheadPx": 200, "printableWidthMm": 25, "densityMin": 1, "densityMax": 5,
              "densityDefault": 3, "paperTypes": [1], "rfidType": 0, "printDirectionDeg": 0 } ] }
            """);

        var merged = baseline.MergedWith(overrides);
        Assert.Equal(baseline.Printers.Count + 1, merged.Printers.Count);
        Assert.Equal("ZZ-Test", merged.FindByModelId(99999)!.Model);
    }
}
