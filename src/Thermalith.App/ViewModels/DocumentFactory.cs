using Thermalith.Core.Model;

namespace Thermalith.App.ViewModels;

/// <summary>Seeds new documents (build spec §6.1) — a default 50×30 rect with a starter text element.</summary>
public static class DocumentFactory
{
    public static LabelDocument New() => new()
    {
        Metadata = new LabelMetadata
        {
            Name = "Untitled",
            CreatedUtc = DateTimeOffset.UtcNow.ToString("o"),
            ModifiedUtc = DateTimeOffset.UtcNow.ToString("o"),
            AppVersion = "0.1.0",
        },
        Canvas = new Canvas { WidthMm = 50, HeightMm = 30, Dpi = 203, Shape = "rectangle", SafeAreaInsetMm = 1.5 },
        Elements =
        [
            new TextElement
            {
                Id = NewId(),
                Name = "Text",
                X = 4, Y = 10, W = 42, H = 10,
                Justify = new Justify { H = "center", V = "middle" },
                Props = new TextProps { Content = "Label", FontSizePt = 14 }, // fixed sizing (model default)
            },
        ],
    };

    /// <summary>A stable, collision-resistant element id (§6.1: ids survive undo/copy/binding).</summary>
    public static string NewId() => "el_" + Guid.NewGuid().ToString("N")[..8];
}
