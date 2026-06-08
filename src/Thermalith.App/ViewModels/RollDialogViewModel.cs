using CommunityToolkit.Mvvm.ComponentModel;
using Thermalith.Core.Catalog;

namespace Thermalith.App.ViewModels;

/// <summary>
/// View-model for the roll/label definition dialog (worklist §B): the full roll the user fills out
/// when a new roll is detected or when creating a new label. Carries the RFID identity (read-only)
/// through unchanged, and pre-fills W×H by parsing the part name (e.g. T40*20 → 40×20).
/// </summary>
public sealed partial class RollDialogViewModel : ObservableObject
{
    private readonly string? _barcode;
    private readonly string? _uuid;
    private readonly string? _serial;
    private readonly string? _consumablesType;

    public RollDialogViewModel() : this(new RollDefinition(), "New label") { }

    public RollDialogViewModel(RollDefinition seed, string title)
    {
        Title = title;
        _barcode = seed.Barcode;
        _uuid = seed.Uuid;
        _serial = seed.Serial;
        _consumablesType = seed.ConsumablesType;

        _name = seed.Name;
        _partName = seed.PartName ?? "";
        _boxId = seed.BoxId ?? "";
        _paperType = string.IsNullOrEmpty(seed.PaperType) ? "gap" : seed.PaperType;
        _width = seed.WidthMm;
        _height = seed.HeightMm;
        _shape = string.IsNullOrEmpty(seed.Shape) ? "rectangle" : seed.Shape;
        _density = seed.Density;
    }

    public string Title { get; }

    /// <summary>True when this dialog is defining a roll detected over RFID (shows the read-only identity).</summary>
    public bool HasRfid => !string.IsNullOrEmpty(_barcode) || !string.IsNullOrEmpty(_uuid);
    public string RfidSummary => $"barcode {_barcode ?? "—"} · uuid {_uuid ?? "—"} · {_consumablesType ?? "?"}";

    [ObservableProperty] private string _name;
    [ObservableProperty] private string _partName;
    [ObservableProperty] private string _boxId;
    [ObservableProperty] private string _paperType;
    [ObservableProperty] private double _width;
    [ObservableProperty] private double _height;
    [ObservableProperty] private string _shape;
    [ObservableProperty] private int? _density;

    // Typing/pasting the part name pre-fills the size from its W*H pattern.
    partial void OnPartNameChanged(string value)
    {
        if (RollNaming.ParseSize(value) is { } size)
        {
            Width = size.WidthMm;
            Height = size.HeightMm;
        }
    }

    /// <summary>Build the roll definition from the form (RFID identity carried through).</summary>
    public RollDefinition ToRoll() => new()
    {
        Barcode = _barcode,
        Uuid = _uuid,
        Serial = _serial,
        ConsumablesType = _consumablesType,
        BoxId = string.IsNullOrWhiteSpace(BoxId) ? null : BoxId.Trim(),
        PartName = string.IsNullOrWhiteSpace(PartName) ? null : PartName.Trim(),
        Name = string.IsNullOrWhiteSpace(Name) ? (string.IsNullOrWhiteSpace(PartName) ? "Label" : PartName.Trim()) : Name.Trim(),
        PaperType = PaperType,
        WidthMm = Width,
        HeightMm = Height,
        Shape = Shape,
        Density = Density,
    };
}
