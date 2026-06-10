using Thermalith.Core.Model;

namespace Thermalith.App.ViewModels;

/// <summary>Seeds new documents (build spec §6.1) — a default 50×30 rect, no starter elements.</summary>
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
        Canvas = new Canvas { WidthMm = 50, HeightMm = 30, Dpi = 203, Shape = "rectangle" },
        Elements = [], // start empty — add elements from the Insert palette
    };

    /// <summary>A new empty document at a specific canvas size + target printhead width (last applied roll).</summary>
    public static LabelDocument New(double widthMm, double heightMm, int dpi, string shape, double? printheadWidthMm) => New() with
    {
        Canvas = new Canvas { WidthMm = widthMm, HeightMm = heightMm, Dpi = dpi, Shape = shape, PrintheadWidthMm = printheadWidthMm },
    };

    /// <summary>A stable, collision-resistant element id (§6.1: ids survive undo/copy/binding).</summary>
    public static string NewId() => "el_" + Guid.NewGuid().ToString("N")[..8];
}
