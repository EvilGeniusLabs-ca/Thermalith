using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Thermalith.Core.Model;

namespace Thermalith.App.ViewModels.Editors;

/// <summary>
/// Editable view of one element's properties for the inspector (build spec §7). Mirrors the element
/// base (§11.1) as observable fields; subclasses add the type-specific block. Any change invokes the
/// supplied callback, which the editor uses to rebuild the immutable element record, re-render, and
/// checkpoint history. The per-type subclasses surface in the UI via one DataTemplate each (§7).
/// </summary>
public abstract partial class ElementEditorViewModel : ObservableObject
{
    private readonly Action _onChanged;
    private bool _loaded;

    protected ElementEditorViewModel(LabelElement el, Action onChanged)
    {
        _onChanged = onChanged;
        Id = el.Id;
        _name = el.Name ?? el.Type;
        _x = el.X;
        _y = el.Y;
        _w = el.W;
        _h = el.H;
        _rotation = el.Rotation;
        _locked = el.Locked;
        _visible = el.Visible;
        _justifyH = el.Justify?.H ?? "left";
        _justifyV = el.Justify?.V ?? "top";
    }

    public string Id { get; }

    /// <summary>The schema type string (e.g. "text") — used by the inspector header.</summary>
    public abstract string TypeLabel { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _w;
    [ObservableProperty] private double _h;
    [ObservableProperty] private double _rotation;
    [ObservableProperty] private bool _locked;
    [ObservableProperty] private bool _visible;
    [ObservableProperty] private string _justifyH;
    [ObservableProperty] private string _justifyV;

    /// <summary>Call at the end of a subclass constructor so initial assignments don't fire the change callback.</summary>
    protected void MarkLoaded() => _loaded = true;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_loaded) _onChanged();
    }

    /// <summary>Reconstruct the immutable element record from the current field values.</summary>
    public abstract LabelElement ToElement();

    protected Justify JustifyValue() => new() { H = JustifyH, V = JustifyV };

    /// <summary>Build the right editor for an element.</summary>
    public static ElementEditorViewModel Create(LabelElement el, Action onChanged) => el switch
    {
        TextElement t => new TextEditorViewModel(t, onChanged),
        BarcodeElement b => new BarcodeEditorViewModel(b, onChanged),
        QrElement q => new QrEditorViewModel(q, onChanged),
        SerialElement s => new SerialEditorViewModel(s, onChanged),
        DateTimeElement d => new DateTimeEditorViewModel(d, onChanged),
        ShapeElement sh => new ShapeEditorViewModel(sh, onChanged),
        ImageElement im => new ImageEditorViewModel(im, onChanged),
        TableElement tab => new TableEditorViewModel(tab, onChanged),
        _ => throw new ArgumentOutOfRangeException(nameof(el)),
    };
}
