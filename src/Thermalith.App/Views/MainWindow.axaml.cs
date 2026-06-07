using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Thermalith.App.Services;
using Thermalith.App.ViewModels;

namespace Thermalith.App.Views;

/// <summary>
/// The editor shell (build spec §7). Code-behind is limited to view concerns the VM can't own:
/// canvas hit-testing, the platform file dialogs (<see cref="IFilePicker"/>), the central keymap
/// wiring (§7.2), the dynamic recent-files submenu, and persisting window/panel geometry.
/// </summary>
public partial class MainWindow : Window, IFilePicker
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnWindowLoaded;
        Closing += OnWindowClosing;
    }

    private MainWindowViewModel? Vm => DataContext as MainWindowViewModel;

    private void OnWindowLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Vm is not { } vm) return;

        vm.FilePicker = this;
        vm.CloseRequested += (_, _) => Close();

        // Restore persisted window geometry.
        Width = vm.Settings.WindowWidth;
        Height = vm.Settings.WindowHeight;
        BodyGrid.ColumnDefinitions[0].Width = new GridLength(vm.Settings.LeftPanelWidth);
        BodyGrid.ColumnDefinitions[4].Width = new GridLength(vm.Settings.RightPanelWidth);

        WireKeymap(vm);
        BuildRecentMenu(vm);
        vm.RecentFiles.CollectionChanged += (_, _) => BuildRecentMenu(vm);
    }

    private void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        Vm?.SaveLayout(Width, Height, BodyGrid.ColumnDefinitions[0].ActualWidth, BodyGrid.ColumnDefinitions[4].ActualWidth);
    }

    // ── Canvas hit-testing ───────────────────────────────────────────────────────────────────

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is not { } vm || sender is not Control host) return;
        var p = e.GetPosition(host);
        vm.Editor.HitTestSelect(p.X, p.Y);
    }

    // ── Central keymap (§7.2) ────────────────────────────────────────────────────────────────

    private void WireKeymap(MainWindowViewModel vm)
    {
        var mods = PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        var keymap = new KeymapService(mods);

        Bind(EditorAction.New, vm.NewCommand, MiNew);
        Bind(EditorAction.Open, vm.OpenCommand, MiOpen);
        Bind(EditorAction.Save, vm.SaveCommand, MiSave);
        Bind(EditorAction.SaveAs, vm.SaveAsCommand, MiSaveAs);
        Bind(EditorAction.Undo, vm.UndoCommand, MiUndo);
        Bind(EditorAction.Redo, vm.RedoCommand, MiRedo);
        Bind(EditorAction.Delete, vm.DeleteCommand, MiDelete);
        Bind(EditorAction.ZoomIn, vm.ZoomInCommand, MiZoomIn);
        Bind(EditorAction.ZoomOut, vm.ZoomOutCommand, MiZoomOut);
        Bind(EditorAction.ZoomFit, vm.ZoomFitCommand, MiZoomFit);
        Bind(EditorAction.Quit, vm.QuitCommand, MiQuit);
        return;

        void Bind(EditorAction action, System.Windows.Input.ICommand command, MenuItem item)
        {
            var gesture = keymap.Gesture(action);
            KeyBindings.Add(new KeyBinding { Gesture = gesture, Command = command });
            item.InputGesture = gesture;
        }
    }

    // ── Recent-files submenu (MRU, §7) ───────────────────────────────────────────────────────

    private void BuildRecentMenu(MainWindowViewModel vm)
    {
        var items = new List<MenuItem>();
        foreach (var path in vm.RecentFiles)
            items.Add(new MenuItem { Header = path, Command = vm.OpenRecentCommand, CommandParameter = path });
        MiRecent.ItemsSource = items;
    }

    // ── IFilePicker (platform dialogs) ───────────────────────────────────────────────────────

    private static readonly FilePickerFileType NlblType = new("Thermalith label") { Patterns = ["*.nlbl"] };

    public async Task<string?> OpenLabelAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open label",
            AllowMultiple = false,
            FileTypeFilter = [NlblType],
        });
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> SaveLabelAsync(string? suggestedName)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save label",
            SuggestedFileName = (suggestedName ?? "label") + ".nlbl",
            DefaultExtension = "nlbl",
            FileTypeChoices = [NlblType],
        });
        return file?.TryGetLocalPath();
    }
}
