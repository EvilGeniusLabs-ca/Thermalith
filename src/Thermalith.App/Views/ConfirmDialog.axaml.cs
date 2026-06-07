using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Thermalith.App.Views;

/// <summary>Minimal modal confirm for discarding unsaved changes (§7). Returns true to discard.</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog() => InitializeComponent();

    private void OnDiscard(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
