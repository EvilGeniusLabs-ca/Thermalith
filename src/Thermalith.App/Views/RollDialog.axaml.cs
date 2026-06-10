using Avalonia.Controls;
using Avalonia.Interactivity;
using Thermalith.App.ViewModels;
using Thermalith.Core.Catalog;

namespace Thermalith.App.Views;

/// <summary>Modal dialog to define/confirm a label roll (worklist §B). Returns the roll, or null on cancel.</summary>
public partial class RollDialog : Window
{
    public RollDialog() => InitializeComponent();

    private void OnSave(object? sender, RoutedEventArgs e) =>
        Close(DataContext is RollDialogViewModel vm ? vm.ToRoll() : (RollDefinition?)null);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((RollDefinition?)null);
}
