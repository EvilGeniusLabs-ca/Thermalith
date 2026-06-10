using Avalonia.Controls;
using Avalonia.Interactivity;
using Thermalith.App.Services;
using Thermalith.App.ViewModels;

namespace Thermalith.App.Views;

/// <summary>Modal dialog to define/confirm a label roll (worklist §B). Returns the roll + chosen design
/// target, or null on cancel.</summary>
public partial class RollDialog : Window
{
    public RollDialog() => InitializeComponent();

    private void OnSave(object? sender, RoutedEventArgs e) =>
        Close(DataContext is RollDialogViewModel vm
            ? new NewLabelChoice(vm.ToRoll(), vm.TargetPrintableWidthMm, vm.TargetDpi)
            : (NewLabelChoice?)null);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((NewLabelChoice?)null);
}
