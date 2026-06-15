using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Thermalith.App.Views;

/// <summary>The Help → About dialog: identity, version, a thank-you, and links out to EvilGeniusLabs
/// (the donate page and the site — both open in the default browser).</summary>
public partial class AboutDialog : Window
{
    private const string DonateUrl = "https://evilgeniuslabs.ca/donate/?from=thermalith";
    private const string SiteUrl = "https://evilgeniuslabs.ca/?from=thermalith";

    // Releases / "check for updates" page (GitHub hosts the published releases), and the beta-access
    // request mailto. The repo is private until launch, so the releases link 404s for the public until then.
    private const string UpdatesUrl = "https://github.com/EvilGeniusLabs-ca/Thermalith/releases";
    private const string BetaRequestUrl = "mailto:evilgenius@evilgeniuslabs.ca?subject=Thermalith%20beta%20access%20request";

    public AboutDialog()
    {
        InitializeComponent();
        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = "Version " + (v is null ? "0.1.0" : v.ToString(3));
    }

    private void OnDonate(object? sender, RoutedEventArgs e) => Open(DonateUrl);

    // The website link and the banner both lead to the EvilGeniusLabs site.
    private void OnVisitSite(object? sender, PointerPressedEventArgs e) => Open(SiteUrl);

    private void OnCheckUpdates(object? sender, PointerPressedEventArgs e) => Open(UpdatesUrl);

    private void OnRequestBeta(object? sender, PointerPressedEventArgs e) => Open(BetaRequestUrl);

    private async void Open(string url)
    {
        // Open in the user's default browser (cross-platform via Avalonia's launcher); never crash on it.
        try { await Launcher.LaunchUriAsync(new Uri(url)); }
        catch { /* no browser available — ignore */ }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
