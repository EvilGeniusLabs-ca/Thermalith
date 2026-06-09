using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Thermalith.Core.Model;

namespace Thermalith.App.ViewModels.Editors;

// One editor per control type (§11.2–11.9). Each mirrors the type's props and rebuilds the record
// in ToElement(); the base handles geometry/identity/justification.

public sealed partial class TextEditorViewModel : ElementEditorViewModel
{
    public override string TypeLabel => "Text";

    [ObservableProperty] private string _content;
    [ObservableProperty] private string? _fontFamily;
    [ObservableProperty] private double? _fontSizePt;
    [ObservableProperty] private bool _bold;
    [ObservableProperty] private bool _italic;
    [ObservableProperty] private bool _underline;
    [ObservableProperty] private double? _lineSpacing;
    [ObservableProperty] private double _letterSpacing;
    [ObservableProperty] private string _wrap;
    [ObservableProperty] private bool _autoSize;

    public TextEditorViewModel(TextElement el, Action<string?> onChanged) : base(el, onChanged)
    {
        _content = el.Props.Content;
        _fontFamily = el.Props.FontFamily;
        _fontSizePt = el.Props.FontSizePt;
        _bold = el.Props.Bold ?? false;
        _italic = el.Props.Italic ?? false;
        _underline = el.Props.Underline ?? false;
        _lineSpacing = el.Props.LineSpacing;
        _letterSpacing = el.Props.LetterSpacing;
        _wrap = el.Props.Wrap;
        _autoSize = el.Props.AutoSize;
        MarkLoaded();
    }

    /// <summary>Font-family dropdown value: the bundled sentinel when no family override is set, else the family.</summary>
    public string SelectedFont
    {
        get => string.IsNullOrWhiteSpace(FontFamily) ? EditorOptions.BundledFont : FontFamily!;
        set => FontFamily = value == EditorOptions.BundledFont ? null : value;
    }

    partial void OnFontFamilyChanged(string? value) => OnPropertyChanged(nameof(SelectedFont));

    public override LabelElement ToElement() => new TextElement
    {
        Id = Id, Name = Name, X = X, Y = Y, W = W, H = H, Rotation = Rotation, Locked = Locked, Visible = Visible, Justify = JustifyValue(),
        Props = new TextProps
        {
            Content = Content, FontFamily = NullIfEmpty(FontFamily), FontSizePt = FontSizePt,
            Bold = Bold, Italic = Italic, Underline = Underline,
            LineSpacing = LineSpacing, LetterSpacing = LetterSpacing,
            Wrap = Wrap, AutoSize = AutoSize,
            // FontSizing left at the model default ("fixed"); Min/Max dropped — the editor is fixed-size
            // only, so the pt size is honoured literally (text grows + bleeds past its box / the label).
        },
    };

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;
}

public sealed partial class BarcodeEditorViewModel : ElementEditorViewModel
{
    public override string TypeLabel => "Barcode";

    [ObservableProperty] private string _symbology;
    [ObservableProperty] private string _value;
    [ObservableProperty] private bool _showText;
    [ObservableProperty] private string _textPosition;
    [ObservableProperty] private double _moduleWidthMm;
    [ObservableProperty] private double _quietZoneMm;

    public BarcodeEditorViewModel(BarcodeElement el, Action<string?> onChanged) : base(el, onChanged)
    {
        _symbology = el.Props.Symbology;
        _value = el.Props.Value;
        _showText = el.Props.ShowText;
        _textPosition = el.Props.TextPosition;
        _moduleWidthMm = el.Props.ModuleWidthMm;
        _quietZoneMm = el.Props.QuietZoneMm;
        MarkLoaded();
    }

    public override LabelElement ToElement() => new BarcodeElement
    {
        Id = Id, Name = Name, X = X, Y = Y, W = W, H = H, Rotation = Rotation, Locked = Locked, Visible = Visible, Justify = JustifyValue(),
        Props = new BarcodeProps
        {
            Symbology = Symbology, Value = Value, ShowText = ShowText,
            TextPosition = TextPosition, ModuleWidthMm = ModuleWidthMm, QuietZoneMm = QuietZoneMm,
        },
    };
}

public sealed partial class QrEditorViewModel : ElementEditorViewModel
{
    public override string TypeLabel => "QR Code";

    [ObservableProperty] private string _value;
    [ObservableProperty] private string _encoding;
    [ObservableProperty] private string _ecLevel;
    [ObservableProperty] private double? _moduleSizeMm;
    [ObservableProperty] private double _quietZoneMm;

    public QrEditorViewModel(QrElement el, Action<string?> onChanged) : base(el, onChanged)
    {
        _value = el.Props.Value;
        _encoding = el.Props.Encoding;
        _ecLevel = el.Props.EcLevel;
        _moduleSizeMm = el.Props.ModuleSizeMm;
        _quietZoneMm = el.Props.QuietZoneMm;
        MarkLoaded();
    }

    public override LabelElement ToElement() => new QrElement
    {
        Id = Id, Name = Name, X = X, Y = Y, W = W, H = H, Rotation = Rotation, Locked = Locked, Visible = Visible, Justify = JustifyValue(),
        Props = new QrProps { Value = Value, Encoding = Encoding, EcLevel = EcLevel, ModuleSizeMm = ModuleSizeMm, QuietZoneMm = QuietZoneMm },
    };
}

public sealed partial class SerialEditorViewModel : ElementEditorViewModel
{
    public override string TypeLabel => "Serial";

    [ObservableProperty] private int _start;
    [ObservableProperty] private int _step;
    [ObservableProperty] private int _padLength;
    [ObservableProperty] private string _padChar;
    [ObservableProperty] private string _prefix;
    [ObservableProperty] private string _suffix;

    public SerialEditorViewModel(SerialElement el, Action<string?> onChanged) : base(el, onChanged)
    {
        _start = el.Props.Start;
        _step = el.Props.Step;
        _padLength = el.Props.PadLength;
        _padChar = el.Props.PadChar;
        _prefix = el.Props.Prefix;
        _suffix = el.Props.Suffix;
        MarkLoaded();
    }

    public override LabelElement ToElement() => new SerialElement
    {
        Id = Id, Name = Name, X = X, Y = Y, W = W, H = H, Rotation = Rotation, Locked = Locked, Visible = Visible, Justify = JustifyValue(),
        Props = new SerialProps { Start = Start, Step = Step, PadLength = PadLength, PadChar = PadChar, Prefix = Prefix, Suffix = Suffix },
    };
}

public sealed partial class DateTimeEditorViewModel : ElementEditorViewModel
{
    public override string TypeLabel => "Date / Time";

    [ObservableProperty] private string _kind;
    [ObservableProperty] private string _format;
    [ObservableProperty] private string _source;
    [ObservableProperty] private string? _fixedValueUtc;

    public DateTimeEditorViewModel(DateTimeElement el, Action<string?> onChanged) : base(el, onChanged)
    {
        _kind = el.Props.Kind;
        _format = el.Props.Format;
        _source = el.Props.Source;
        _fixedValueUtc = el.Props.FixedValueUtc;
        MarkLoaded();
    }

    public override LabelElement ToElement() => new DateTimeElement
    {
        Id = Id, Name = Name, X = X, Y = Y, W = W, H = H, Rotation = Rotation, Locked = Locked, Visible = Visible, Justify = JustifyValue(),
        Props = new DateTimeProps { Kind = Kind, Format = Format, Source = Source, FixedValueUtc = FixedValueUtc },
    };
}

public sealed partial class ShapeEditorViewModel : ElementEditorViewModel
{
    public override string TypeLabel => "Shape";

    [ObservableProperty] private string _shapeType;
    [ObservableProperty] private double _strokeWidthMm;
    [ObservableProperty] private string _fill;
    [ObservableProperty] private double _cornerRadiusMm;

    public ShapeEditorViewModel(ShapeElement el, Action<string?> onChanged) : base(el, onChanged)
    {
        _shapeType = el.Props.ShapeType;
        _strokeWidthMm = el.Props.StrokeWidthMm;
        _fill = el.Props.Fill;
        _cornerRadiusMm = el.Props.CornerRadiusMm;
        MarkLoaded();
    }

    // Switching to roundedRect with a flat 0 radius hides the change; seed a visible 1mm so the
    // rounding is obvious. (Backing field is set directly in the ctor, so this only fires on user edits.)
    partial void OnShapeTypeChanged(string value)
    {
        if (value == "roundedRect" && CornerRadiusMm == 0) CornerRadiusMm = 1;
    }

    public override LabelElement ToElement() => new ShapeElement
    {
        Id = Id, Name = Name, X = X, Y = Y, W = W, H = H, Rotation = Rotation, Locked = Locked, Visible = Visible, Justify = JustifyValue(),
        Props = new ShapeProps { ShapeType = ShapeType, StrokeWidthMm = StrokeWidthMm, Fill = Fill, CornerRadiusMm = CornerRadiusMm },
    };
}

public sealed partial class ImageEditorViewModel : ElementEditorViewModel
{
    public override string TypeLabel => "Image";

    [ObservableProperty] private string _assetId;
    [ObservableProperty] private string _fit;
    [ObservableProperty] private string _dither;
    [ObservableProperty] private int _threshold;
    [ObservableProperty] private bool _invert;
    [ObservableProperty] private int _rotateQuarters;
    [ObservableProperty] private bool _flipH;
    [ObservableProperty] private bool _flipV;

    public ImageEditorViewModel(ImageElement el, Action<string?> onChanged) : base(el, onChanged)
    {
        _assetId = el.Props.AssetId;
        _fit = el.Props.Fit;
        _dither = el.Props.Dither;
        _threshold = el.Props.Threshold;
        _invert = el.Props.Invert;
        _rotateQuarters = el.Props.RotateQuarters;
        _flipH = el.Props.FlipH;
        _flipV = el.Props.FlipV;
        MarkLoaded();
    }

    // Rotate in 90° steps; the property setter fires the change callback → re-render + history.
    [RelayCommand] private void RotateCw() => RotateQuarters = (RotateQuarters + 1) % 4;
    [RelayCommand] private void RotateCcw() => RotateQuarters = (RotateQuarters + 3) % 4;

    public override LabelElement ToElement() => new ImageElement
    {
        Id = Id, Name = Name, X = X, Y = Y, W = W, H = H, Rotation = Rotation, Locked = Locked, Visible = Visible, Justify = JustifyValue(),
        Props = new ImageProps
        {
            AssetId = AssetId, Fit = Fit, Dither = Dither, Threshold = Threshold, Invert = Invert,
            RotateQuarters = RotateQuarters, FlipH = FlipH, FlipV = FlipV,
        },
    };
}

public sealed partial class TableEditorViewModel : ElementEditorViewModel
{
    public override string TypeLabel => "Table";

    private readonly double[]? _columnWidthsMm;
    private readonly double[]? _rowHeightsMm;

    [ObservableProperty] private int _cols;
    [ObservableProperty] private int _rows;
    [ObservableProperty] private double _borderWidthMm;
    [ObservableProperty] private bool _headerRow;

    /// <summary>Editable cell grid bound by the inspector; rebuilt when cols/rows change, preserving content.</summary>
    public ObservableCollection<TableRowViewModel> CellRows { get; } = [];

    public TableEditorViewModel(TableElement el, Action<string?> onChanged) : base(el, onChanged)
    {
        _cols = el.Props.Cols;
        _rows = el.Props.Rows;
        _borderWidthMm = el.Props.BorderWidthMm;
        _headerRow = el.Props.HeaderRow;
        _columnWidthsMm = el.Props.ColumnWidthsMm;
        _rowHeightsMm = el.Props.RowHeightsMm;
        BuildCells(el.Props.Cells);
        MarkLoaded();
    }

    partial void OnColsChanged(int value) => BuildCells(SnapshotCells());
    partial void OnRowsChanged(int value) => BuildCells(SnapshotCells());

    private void BuildCells(IReadOnlyList<IReadOnlyList<string>>? existing)
    {
        CellRows.Clear();
        for (var r = 0; r < Math.Max(0, Rows); r++)
        {
            var row = new ObservableCollection<TableCellViewModel>();
            for (var c = 0; c < Math.Max(0, Cols); c++)
            {
                var content = existing is not null && r < existing.Count && c < existing[r].Count ? existing[r][c] : "";
                row.Add(new TableCellViewModel(content, RaiseEdited));
            }
            CellRows.Add(new TableRowViewModel(row));
        }
    }

    private void BuildCells(List<List<TableCell>>? cells) =>
        BuildCells(cells?.Select(r => (IReadOnlyList<string>)r.Select(c => c.Content).ToList()).ToList());

    private List<IReadOnlyList<string>> SnapshotCells() =>
        CellRows.Select(r => (IReadOnlyList<string>)r.Cells.Select(c => c.Content).ToList()).ToList();

    public override LabelElement ToElement() => new TableElement
    {
        Id = Id, Name = Name, X = X, Y = Y, W = W, H = H, Rotation = Rotation, Locked = Locked, Visible = Visible, Justify = JustifyValue(),
        Props = new TableProps
        {
            Cols = Cols, Rows = Rows, BorderWidthMm = BorderWidthMm, HeaderRow = HeaderRow,
            ColumnWidthsMm = _columnWidthsMm, RowHeightsMm = _rowHeightsMm,
            Cells = CellRows.Select(r => r.Cells.Select(c => new TableCell { Content = c.Content }).ToList()).ToList(),
        },
    };
}
