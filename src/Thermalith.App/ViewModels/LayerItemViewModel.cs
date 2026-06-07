using CommunityToolkit.Mvvm.ComponentModel;
using Thermalith.Core.Model;

namespace Thermalith.App.ViewModels;

/// <summary>A row in the layers list (build spec §7) — back-to-front order mirrors z-order (§2).</summary>
public sealed partial class LayerItemViewModel : ObservableObject
{
    public LayerItemViewModel(LabelElement el)
    {
        Id = el.Id;
        _name = el.Name ?? el.Type;
        TypeLabel = el.Type;
        _visible = el.Visible;
    }

    public string Id { get; }
    public string TypeLabel { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _visible;
}
