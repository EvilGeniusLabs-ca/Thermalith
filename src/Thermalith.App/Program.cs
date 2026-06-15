using Avalonia;
using Thermalith.App.Services;

namespace Thermalith.App;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called.
    [STAThread]
    public static void Main(string[] args)
    {
        // Dev/maintenance: regenerate the bundled printer catalog from NIIMBOT's public device list,
        // using the app's own importer (no separate tool). Runs headless, then exits.
        //   Thermalith.App --update-catalog --out <path-to>/src/Niimbot.Net/Profiles/printers.json
        if (Array.IndexOf(args, "--update-catalog") >= 0)
        {
            UpdateCatalog(args);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by the visual designer.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void UpdateCatalog(string[] args)
    {
        var outPath = OptionValue(args, "--out");
        try
        {
            var catalog = new PrinterCatalogService().UpdateAsync(outPath).GetAwaiter().GetResult();
            Console.WriteLine($"Printer catalog updated: {catalog.Printers.Count} printers → {outPath ?? "(app-data cache)"}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Catalog update failed: " + ex.Message);
            Environment.ExitCode = 1;
        }
    }

    private static string? OptionValue(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }
}
