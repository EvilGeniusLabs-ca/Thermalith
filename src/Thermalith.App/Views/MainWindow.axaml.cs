using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Thermalith.App.Services;
using Thermalith.App.ViewModels;

namespace Thermalith.App.Views;

/// <summary>
/// The editor shell (build spec §7). Code-behind is limited to view concerns the VM can't own:
/// canvas hit-testing, the platform file dialogs (<see cref="IFilePicker"/>), the central keymap
/// wiring (§7.2), the dynamic recent-files submenu, and persisting window/panel geometry.
/// </summary>
public partial class MainWindow : Window, IFilePicker, IDialogService
{
    private bool _allowClose;

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
        vm.Dialogs = this;
        vm.CloseRequested += (_, _) => Close();

        // Restore persisted window geometry.
        var s = vm.Settings;
        Width = s.WindowWidth;
        Height = s.WindowHeight;
        if (s.WindowX is { } wx && s.WindowY is { } wy)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint((int)wx, (int)wy);
        }
        if (s.WindowMaximized) WindowState = WindowState.Maximized;
        BodyGrid.ColumnDefinitions[0].Width = new GridLength(s.LeftPanelWidth);
        BodyGrid.ColumnDefinitions[4].Width = new GridLength(s.RightPanelWidth);

        WireKeymap(vm);
        BuildRecentMenu(vm);
        vm.RecentFiles.CollectionChanged += (_, _) => BuildRecentMenu(vm);

        // Persist geometry as it changes too — a debugger stop kills the process before a clean close.
        _persistDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _persistDebounce.Tick += (_, _) => { _persistDebounce!.Stop(); PersistWindowState(); };
        PositionChanged += (_, _) => DebouncePersist();
        Resized += (_, _) => DebouncePersist();
    }

    private DispatcherTimer? _persistDebounce;

    private void DebouncePersist()
    {
        _persistDebounce?.Stop();
        _persistDebounce?.Start();
    }

    private void PersistWindowState()
    {
        if (Vm is not { } vm) return;
        var left = BodyGrid.ColumnDefinitions[0].ActualWidth;
        var right = BodyGrid.ColumnDefinitions[4].ActualWidth;
        if (WindowState == WindowState.Normal)
            vm.SaveWindowState(Position.X, Position.Y, Width, Height, false, left, right);
        else
            vm.SaveWindowState(null, null, null, null, WindowState == WindowState.Maximized, left, right);
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        if (Vm is not { } vm) return;

        // Guard unsaved work: cancel the close, confirm, then close for real.
        if (!_allowClose && vm.Editor.Dirty)
        {
            e.Cancel = true;
            if (await ConfirmDiscardAsync())
            {
                _allowClose = true;
                Close();
            }
            return;
        }

        PersistWindowState();
        vm.Printer.Shutdown();
    }

    public Task<bool> ConfirmDiscardAsync() => new ConfirmDialog().ShowDialog<bool>(this);

    // ── Canvas interaction: select, drag-move, resize, marquee (§7) ──────────────────────────────

    private bool _dragging;
    private bool _marquee;
    private Point _pressPoint;

    private void OnCanvasPressed(object? sender, PointerPressedEventArgs e)
    {
        if (Vm is not { } vm || sender is not Control host) return;
        var ed = vm.Editor;
        var p = e.GetPosition(host);
        _pressPoint = p;

        // A resize handle of a single selection takes priority.
        if (ed.HasSelection && ed.HitTestHandle(p.X, p.Y) is { } handle)
        {
            ed.BeginDrag(DragMode.Resize, handle);
            _dragging = true;
            e.Pointer.Capture(host);
            return;
        }

        var additive = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (ed.SelectAt(p.X, p.Y, additive))
        {
            ed.BeginDrag(DragMode.Move, Handle.TopLeft); // move the whole selection
            _dragging = true;
            e.Pointer.Capture(host);
        }
        else
        {
            // Empty space → rubber-band marquee select.
            _marquee = true;
            ShowMarquee(p, p);
            e.Pointer.Capture(host);
        }
    }

    private void OnCanvasMoved(object? sender, PointerEventArgs e)
    {
        if (Vm is not { } vm || sender is not Control host) return;
        var p = e.GetPosition(host);

        if (_dragging)
        {
            vm.Editor.DragTo(p.X - _pressPoint.X, p.Y - _pressPoint.Y);
            return;
        }
        if (_marquee)
        {
            ShowMarquee(_pressPoint, p);
            return;
        }
        UpdateCursor(vm.Editor, host, p);
    }

    private void OnCanvasReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (Vm is not { } vm || sender is not Control host) return;

        if (_dragging)
        {
            vm.Editor.EndDrag();
            _dragging = false;
            e.Pointer.Capture(null);
            return;
        }
        if (_marquee)
        {
            _marquee = false;
            Marquee.IsVisible = false;
            vm.Editor.SelectInRect(RectFrom(_pressPoint, e.GetPosition(host)));
            e.Pointer.Capture(null);
        }
    }

    private void OnCanvasWheel(object? sender, PointerWheelEventArgs e)
    {
        if (Vm is not { } vm) return;
        var ed = vm.Editor;
        var zoomOnly = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        if (ed.HasSelection && !zoomOnly)
            ed.ScaleSelection(e.Delta.Y > 0 ? 1.06 : 1 / 1.06);   // resize the selection
        else if (e.Delta.Y > 0)
            ed.ZoomIn();
        else
            ed.ZoomOut();

        e.Handled = true; // don't let the ScrollViewer scroll instead
    }

    private void ShowMarquee(Point a, Point b)
    {
        var r = RectFrom(a, b);
        Canvas.SetLeft(Marquee, r.X);
        Canvas.SetTop(Marquee, r.Y);
        Marquee.Width = r.Width;
        Marquee.Height = r.Height;
        Marquee.IsVisible = true;
    }

    private static Rect RectFrom(Point a, Point b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

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
        Bind(EditorAction.Cut, vm.CutCommand, MiCut);
        Bind(EditorAction.Copy, vm.CopyCommand, MiCopy);
        Bind(EditorAction.Paste, vm.PasteCommand, MiPaste);
        Bind(EditorAction.Duplicate, vm.DuplicateCommand, MiDuplicate);
        Bind(EditorAction.Group, vm.GroupCommand, MiGroup);
        Bind(EditorAction.Ungroup, vm.UngroupCommand, MiUngroup);
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
