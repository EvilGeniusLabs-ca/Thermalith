using Avalonia.Controls;
using Avalonia.Interactivity;
using Thermalith.App.Services;

namespace Thermalith.App.Views;

/// <summary>Modeless batch-print progress for a data-merge run (GitHub #7): a live "Printing k / N" count
/// and a Cancel button. Cancel stops the run after the current label finishes (checked between rows).</summary>
public partial class MergeProgressDialog : Window, IMergeProgress
{
    private readonly int _total;

    public MergeProgressDialog() => InitializeComponent();

    public MergeProgressDialog(string title, int total) : this()
    {
        Title = title;
        _total = total;
        Bar.Maximum = Math.Max(1, total);
        StatusText.Text = $"Printing 0 / {total}…";
    }

    public bool IsCancelled { get; private set; }

    public void Report(int done)
    {
        Bar.Value = done;
        StatusText.Text = IsCancelled ? "Finishing current label…" : $"Printing {done} / {_total}…";
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        IsCancelled = true;
        CancelButton.IsEnabled = false;
        StatusText.Text = "Finishing current label…";
    }
}
