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
}
