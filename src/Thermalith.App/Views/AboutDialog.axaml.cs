using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Thermalith.App.Views;

/// <summary>The Help → About dialog: identity, version, a thank-you, and the donation link
/// (opens evilgeniuslabs.ca/donate in the default browser).</summary>
public partial class AboutDialog : Window
{
    private const string DonateUrl = "https://evilgeniuslabs.ca/donate/?from=thermalith";

    public AboutDialog()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = "Version " + (v is null ? "0.1.0" : v.ToString(3));
    }

    private async void OnDonate(object? sender, RoutedEventArgs e)
    {
        // Open in the user's default browser (cross-platform via Avalonia's launcher), in a new window.
        try { await Launcher.LaunchUriAsync(new Uri(DonateUrl)); }
        catch { /* no browser available — silently ignore so the dialog never crashes */ }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
