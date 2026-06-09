using CommunityToolkit.Mvvm.ComponentModel;
using Thermalith.Core.Model;

namespace Thermalith.App.ViewModels;

/// <summary>A row in the layers list (build spec §7) — back-to-front order mirrors z-order (§2).
/// Mirrors the element's name + visible/locked state; toggling the eye/lock here calls back into the
/// editor. The callbacks are suppressed while the editor syncs the row from a model edit
/// (<see cref="Sync"/>) so a programmatic refresh isn't mistaken for a user toggle.</summary>
public sealed partial class LayerItemViewModel : ObservableObject
{
    private readonly Action<string, bool>? _onVisibleToggled;
    private readonly Action<string, bool>? _onLockedToggled;
    private bool _suppress;

    public LayerItemViewModel(
        LabelElement el,
        Action<string, bool>? onVisibleToggled = null,
        Action<string, bool>? onLockedToggled = null)
    {
        Id = el.Id;
        _name = el.Name ?? el.Type;
        TypeLabel = el.Type;
        _visible = el.Visible; // backing-field init: doesn't fire the toggle callback
        _locked = el.Locked;
        IsGrouped = el.GroupId is not null;
        _onVisibleToggled = onVisibleToggled;
        _onLockedToggled = onLockedToggled;
    }

    public string Id { get; }
    public string TypeLabel { get; }
    public bool IsGrouped { get; }

    [ObservableProperty] private string _name;
    [ObservableProperty] private bool _visible;
    [ObservableProperty] private bool _locked;

    partial void OnVisibleChanged(bool value) { if (!_suppress) _onVisibleToggled?.Invoke(Id, value); }
    partial void OnLockedChanged(bool value) { if (!_suppress) _onLockedToggled?.Invoke(Id, value); }

    /// <summary>Refresh display state from the model without firing the toggle callbacks.</summary>
    public void Sync(string name, bool visible, bool locked)
    {
        _suppress = true;
        Name = name;
        Visible = visible;
        Locked = locked;
        _suppress = false;
    }
}
