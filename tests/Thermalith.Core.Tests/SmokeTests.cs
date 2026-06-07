using Thermalith.Core.Model;
using Xunit;

namespace Thermalith.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void LabelDocument_defaults_are_sane()
    {
        var doc = new LabelDocument
        {
            Metadata = new LabelMetadata { Name = "smoke" },
            Canvas = new Canvas { WidthMm = 50, HeightMm = 30 },
        };

        Assert.Equal("1.0", doc.SchemaVersion);
        Assert.Equal(203, doc.Canvas.Dpi);
        Assert.Empty(doc.Elements);
    }
}
