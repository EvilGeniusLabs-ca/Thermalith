using Thermalith.Core.Model;

namespace Thermalith.App.ViewModels;

/// <summary>Creates a sensible default element of a given type, centred on the canvas (insert palette, §6.2/§7).</summary>
public static class ElementFactory
{
    public static LabelElement Create(string type, Canvas canvas)
    {
        var (w, h) = DefaultSize(type);
        // Whole-mm placement, centred (positioning is integer-mm throughout — see EditorViewModel.Snap).
        w = Math.Round(Math.Min(w, canvas.WidthMm - 2));
        h = Math.Round(Math.Min(h, canvas.HeightMm - 2));
        var x = Math.Max(1, Math.Round((canvas.WidthMm - w) / 2));
        var y = Math.Max(1, Math.Round((canvas.HeightMm - h) / 2));
        var id = DocumentFactory.NewId();

        return type switch
        {
            "text" => new TextElement { Id = id, Name = "Text", X = x, Y = y, W = w, H = h, Justify = Center(), Props = new TextProps { Content = "Text", FontSizePt = 10, AutoSize = true } }, // fixed sizing; box auto-hugs the glyphs
            "barcode" => new BarcodeElement { Id = id, Name = "Barcode", X = x, Y = y, W = w, H = h, Justify = new Justify { H = "center" }, Props = new BarcodeProps { Symbology = "code128", Value = "12345" } },
            "qr" => new QrElement { Id = id, Name = "QR", X = x, Y = y, W = w, H = h, Props = new QrProps { Value = "https://example.com" } },
            "serial" => new SerialElement { Id = id, Name = "Serial", X = x, Y = y, W = w, H = h, Justify = Center(), Props = new SerialProps { Start = 1, PadLength = 4, Prefix = "SN" } },
            "datetime" => new DateTimeElement { Id = id, Name = "Date", X = x, Y = y, W = w, H = h, Justify = Center(), Props = new DateTimeProps { Kind = "date", Format = "yyyy-MM-dd", Source = "printNow" } },
            "shape" => new ShapeElement { Id = id, Name = "Shape", X = x, Y = y, W = w, H = h, Props = new ShapeProps { ShapeType = "rect", StrokeWidthMm = 0.3 } },
            "line" => MakeLine(canvas),
            "image" => new ImageElement { Id = id, Name = "Image", X = x, Y = y, W = w, H = h, Props = new ImageProps() },
            "table" => new TableElement { Id = id, Name = "Table", X = x, Y = y, W = w, H = h, Props = NewTable() },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown element type."),
        };
    }

    private static Justify Center() => new() { H = "center", V = "middle" };

    /// <summary>Default Line: horizontal, on the vertical centerline, spanning the middle half of the
    /// label width (worklist §K). Endpoints are stored relative to the bbox origin (X,Y); for a flat
    /// horizontal line the bbox is zero-height, so both relative Y offsets are 0.</summary>
    private static LineElement MakeLine(Canvas canvas)
    {
        var x1 = Math.Round(canvas.WidthMm / 4, 1);
        var x2 = Math.Round(canvas.WidthMm * 3 / 4, 1);
        var y = Math.Round(canvas.HeightMm / 2, 1);
        return new LineElement
        {
            Id = DocumentFactory.NewId(),
            Name = "Line",
            X = x1, Y = y, W = x2 - x1, H = 0,
            Props = new LineProps { X1Mm = 0, Y1Mm = 0, X2Mm = x2 - x1, Y2Mm = 0, WeightMm = 0.3 },
        };
    }

    private static (double W, double H) DefaultSize(string type) => type switch
    {
        "text" => (30, 8),
        "barcode" => (30, 12),
        "qr" => (14, 14),
        "serial" => (20, 6),
        "datetime" => (20, 6),
        "shape" => (20, 12),
        "image" => (16, 16),
        "table" => (24, 12),
        _ => (20, 10),
    };

    private static TableProps NewTable() => new()
    {
        Cols = 2,
        Rows = 2,
        BorderWidthMm = 0.1,
        Cells =
        [
            [new TableCell { Content = "A1" }, new TableCell { Content = "B1" }],
            [new TableCell { Content = "A2" }, new TableCell { Content = "B2" }],
        ],
    };
}
