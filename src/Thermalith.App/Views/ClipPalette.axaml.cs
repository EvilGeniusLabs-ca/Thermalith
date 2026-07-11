using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Thermalith.App.ViewModels;

namespace Thermalith.App.Views;

public partial class ClipPalette : UserControl
{
    public ClipPalette() => InitializeComponent();

    private void OnGlyphTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { DataContext: ClipGlyphItemViewModel vm })
            vm.InsertCommand.Execute(null);
    }

    private void OnResizeDragDelta(object? sender, VectorEventArgs e)
    {
        if (DataContext is not ClipPaletteViewModel vm) return;
        vm.Width = Math.Max(ClipPaletteViewModel.MinWidth, vm.Width + e.Vector.X);
        vm.Height = Math.Max(ClipPaletteViewModel.MinHeight, vm.Height + e.Vector.Y);
    }

    private void OnResizeDragCompleted(object? sender, VectorEventArgs e)
        => (DataContext as ClipPaletteViewModel)?.SaveSize();
}
