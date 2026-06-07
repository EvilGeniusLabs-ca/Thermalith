using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Thermalith.Core.Model;

namespace Thermalith.App.ViewModels.Editors;

/// <summary>
/// Editable view of the canvas + document name, shown in the inspector when no element is selected
/// (build spec §6.1/§7). Edits the physical size, DPI, shape, orientation, bleed and safe-area inset
/// (§6.1.3). Any change invokes the supplied callback, which rebuilds the document and re-renders.
/// </summary>
public sealed partial class CanvasEditorViewModel : ObservableObject
{
    private readonly Action _onChanged;
    private bool _loaded;

    public CanvasEditorViewModel(LabelDocument doc, Action onChanged)
    {
        _onChanged = onChanged;
        var c = doc.Canvas;
        _name = doc.Metadata.Name;
        _widthMm = c.WidthMm;
        _heightMm = c.HeightMm;
        _dpi = c.Dpi;
        _shape = c.Shape;
        _cornerRadiusMm = c.CornerRadiusMm;
        _bleedMm = c.BleedMm;
        _safeAreaInsetMm = c.SafeAreaInsetMm;
        _orientationDeg = c.OrientationDeg;
        _loaded = true;
    }

    [ObservableProperty] private string _name;
    [ObservableProperty] private double _widthMm;
    [ObservableProperty] private double _heightMm;
    [ObservableProperty] private int _dpi;
    [ObservableProperty] private string _shape;
    [ObservableProperty] private double _cornerRadiusMm;
    [ObservableProperty] private double _bleedMm;
    [ObservableProperty] private double? _safeAreaInsetMm;
    [ObservableProperty] private int _orientationDeg;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (_loaded) _onChanged();
    }

    public LabelDocument Apply(LabelDocument doc) => doc with
    {
        Metadata = doc.Metadata with { Name = Name, ModifiedUtc = DateTimeOffset.UtcNow.ToString("o") },
        Canvas = doc.Canvas with
        {
            WidthMm = WidthMm <= 0 ? doc.Canvas.WidthMm : WidthMm,
            HeightMm = HeightMm <= 0 ? doc.Canvas.HeightMm : HeightMm,
            Dpi = Dpi <= 0 ? doc.Canvas.Dpi : Dpi,
            Shape = Shape,
            CornerRadiusMm = CornerRadiusMm,
            BleedMm = BleedMm,
            SafeAreaInsetMm = SafeAreaInsetMm,
            OrientationDeg = OrientationDeg,
        },
    };
}
