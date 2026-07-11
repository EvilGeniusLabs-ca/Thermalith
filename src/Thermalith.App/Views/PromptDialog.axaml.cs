using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Thermalith.App.Views;

/// <summary>Reusable modal prompt: a message plus a primary button and an optional Cancel. Returns true
/// when the primary button is clicked. Used for the data-merge confirm-count gate and info messages (#7).</summary>
public partial class PromptDialog : Window
{
    public PromptDialog() => InitializeComponent();

    public PromptDialog(string title, string message, string okText, bool showCancel) : this()
    {
        Title = title;
        MessageText.Text = message;
        OkButton.Content = okText;
        CancelButton.IsVisible = showCancel;
    }

    private void OnOk(object? sender, RoutedEventArgs e) => Close(true);
    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}
