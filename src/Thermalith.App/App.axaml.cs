using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Thermalith.App.Services;
using Thermalith.App.ViewModels;
using Thermalith.App.Views;

namespace Thermalith.App;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        // Activate the printer catalogue (embedded baseline merged with any app-data override) so
        // profile resolution at the protocol layer sees the user's additions, not just the baseline.
        new PrinterCatalogService().Load();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
