using Avalonia;
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
        Vm?.Printer.Shutdown();
    }

    // ── Canvas interaction: select, drag-move, resize (§7) ───────────────────────────────────

    private DragMode _dragMode = DragMode.None;
    private Handle _dragHandle;
    private Point _dragStart;
    private GeomMm _dragStartGeom;
    private bool _dragMoved;

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is not { } vm || sender is not Control host) return;
        var ed = vm.Editor;
        var p = e.GetPosition(host);

        // A resize handle of the current selection takes priority.
        if (ed.HasSelection && ed.HitTestHandle(p.X, p.Y) is { } handle)
        {
            StartDrag(ed, DragMode.Resize, handle, p);
            e.Pointer.Capture(host);
            return;
        }

        // Otherwise select the topmost element under the point; if one is hit, start a move.
        ed.HitTestSelect(p.X, p.Y);
        if (ed.HasSelection)
        {
            StartDrag(ed, DragMode.Move, Handle.TopLeft, p);
            e.Pointer.Capture(host);
        }
    }

    private void StartDrag(ViewModels.EditorViewModel ed, DragMode mode, Handle handle, Point p)
    {
        if (ed.SelectedGeometry() is not { } geom) return;
        _dragMode = mode;
        _dragHandle = handle;
        _dragStart = p;
        _dragStartGeom = geom;
        _dragMoved = false;
        ed.BeginInteractive();
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (Vm is not { } vm || sender is not Control host) return;

        if (_dragMode == DragMode.None)
        {
            UpdateCursor(vm.Editor, host, e.GetPosition(host));
            return;
        }

        var p = e.GetPosition(host);
        var dx = p.X - _dragStart.X;
        var dy = p.Y - _dragStart.Y;
        if (!_dragMoved && Math.Abs(dx) < 1 && Math.Abs(dy) < 1) return; // ignore click jitter
        _dragMoved = true;
        vm.Editor.DragApply(_dragMode, _dragHandle, _dragStartGeom, dx, dy);
    }

    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragMode == DragMode.None) return;
        if (_dragMoved && Vm is { } vm) vm.Editor.EndInteractive();
        _dragMode = DragMode.None;
        e.Pointer.Capture(null);
    }

    private StandardCursorType _cursorType = StandardCursorType.Arrow;

    private void UpdateCursor(ViewModels.EditorViewModel ed, Control host, Point p)
    {
        var type = ed.HasSelection
            ? ed.HitTestHandle(p.X, p.Y) switch
            {
                Handle.TopLeft or Handle.BottomRight => StandardCursorType.TopLeftCorner,
                Handle.TopRight or Handle.BottomLeft => StandardCursorType.TopRightCorner,
                Handle.Top or Handle.Bottom => StandardCursorType.SizeNorthSouth,
                Handle.Left or Handle.Right => StandardCursorType.SizeWestEast,
                _ => StandardCursorType.Arrow,
            }
            : StandardCursorType.Arrow;

        if (type == _cursorType) return;
        _cursorType = type;
        host.Cursor = new Cursor(type);
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
