using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Styling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Niimbot.Net.Commands;
using Thermalith.App.Services;
using Thermalith.App.ViewModels.Editors;
using Thermalith.Core.Catalog;
using Thermalith.Core.Serialization;

namespace Thermalith.App.ViewModels;

/// <summary>
/// The application shell view-model (build spec §7): owns the editor, the file commands, the MRU
/// list and settings. UI-free — file dialogs come through <see cref="IFilePicker"/> and window
/// close through <see cref="CloseRequested"/>, both supplied by the view (§4.1, no DI container).
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly SettingsService _settingsService;
    private readonly LabelRollStore _rollStore = new();
    private AppSettings _settings;

    public MainWindowViewModel() : this(new SettingsService()) { }

    public MainWindowViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settings = settingsService.Load();
        Editor = new EditorViewModel();
        Printer = new PrinterViewModel(Editor);
        RecentFiles = new ObservableCollection<string>(_settings.RecentFiles);

        Editor.StateChanged += (_, _) => OnEditorStateChanged();
        Editor.PropertyChanged += OnEditorPropertyChanged;
        Printer.RollDetected += OnRollDetected;
        Printer.Connected += OnPrinterConnected;

        // Seed the startup canvas to the last applied roll's (printable) size, so designs don't start at
        // the generic 50×30 and then jump when a printer with a narrower printhead is attached.
        if (_settings.LastCanvasWidthMm is > 0 && _settings.LastCanvasHeightMm is > 0)
            Editor.NewDocument(_settings.LastCanvasWidthMm.Value, _settings.LastCanvasHeightMm.Value,
                _settings.LastCanvasDpi ?? 203, _settings.LastCanvasShape ?? "rectangle", _settings.LastPrintheadWidthMm,
                _settings.LastSafeMarginMm);

        UpdateTitle();

        // Apply the saved UI theme at startup (before the window shows, so there's no flash).
        _theme = _settings.Theme;
        ApplyTheme(_theme);

        // Background scan + reconnect to the last printer (fire-and-forget — never blocks startup/editing).
        _ = Printer.AutoConnectAsync(_settings.LastPrinterPort, _settings.LastPrinterModel);
    }

    /// <summary>Persist the connected printer so the next startup can scan + reconnect to it.</summary>
    private void OnPrinterConnected(object? sender, EventArgs e)
    {
        _settings = _settings with { LastPrinterPort = Printer.ConnectedPort, LastPrinterModel = Printer.ConnectedModel };
        _settingsService.Save(_settings);
    }

    /// <summary>Persist the current canvas size + printhead width as the last applied roll (seeds next startup).</summary>
    private void RememberCanvas()
    {
        var (w, h, dpi, shape, head, margin) = Editor.CurrentCanvas();
        _settings = _settings with
        {
            LastCanvasWidthMm = w, LastCanvasHeightMm = h, LastCanvasDpi = dpi, LastCanvasShape = shape,
            LastPrintheadWidthMm = head, LastSafeMarginMm = margin,
        };
        _settingsService.Save(_settings);
    }

    public EditorViewModel Editor { get; }
    public PrinterViewModel Printer { get; }
    public ObservableCollection<string> RecentFiles { get; }

    /// <summary>Set by the view before first use.</summary>
    public IFilePicker? FilePicker { get; set; }

    /// <summary>Set by the view before first use.</summary>
    public IDialogService? Dialogs { get; set; }

    /// <summary>Raised when the user chooses Quit; the view closes the window.</summary>
    public event EventHandler? CloseRequested;

    [ObservableProperty] private string _title = "Thermalith";
    [ObservableProperty] private string _statusMessage = "";
    public bool HasRecentFiles => RecentFiles.Count > 0;

    // ── Theme ─────────────────────────────────────────────────────────────────────────────────────

    /// <summary>UI theme: "Default" (follow OS), "Light", or "Dark". Persisted; applied on change.</summary>
    [ObservableProperty] private string _theme = "Default";

    partial void OnThemeChanged(string value)
    {
        ApplyTheme(value);
        _settings = _settings with { Theme = value };
        _settingsService.Save(_settings);
    }

    /// <summary>Menu-bound: set the theme (parameter is "Default" | "Light" | "Dark").</summary>
    [RelayCommand]
    private void SetTheme(string theme) => Theme = theme;

    private static void ApplyTheme(string theme)
    {
        if (Application.Current is not { } app) return;
        app.RequestedThemeVariant = theme switch
        {
            "Light" => ThemeVariant.Light,
            "Dark" => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    // ── File ────────────────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await EnsureDiscardableAsync()) return;

        // New-label dialog seeded (size/paper) from the last-used roll — barcodeless, so confirming
        // it doesn't redefine a learned roll, only updates the "last used" default.
        var last = _rollStore.LastUsed;
        var seed = new RollDefinition
        {
            Name = "Untitled",
            PaperType = last?.PaperType ?? "gap",
            WidthMm = last?.WidthMm ?? 50,
            HeightMm = last?.HeightMm ?? 30,
            Shape = last?.Shape ?? "rectangle",
            Density = last?.Density,
        };
        var def = Dialogs is null ? seed : await Dialogs.DefineRollAsync(seed, "New label");
        if (def is null) return; // cancelled

        Editor.NewDocument();
        Editor.ApplyRoll(def.WidthMm, def.HeightMm, def.Shape, Printer.ConnectedDpi, Printer.ConnectedPrintableWidthMm);
        Printer.ApplyPaperType(def.PaperType);
        _rollStore.Remember(def);
        RememberCanvas();
    }

    // ── Loaded-roll detection (worklist §B) ───────────────────────────────────────────────────────

    private async void OnRollDetected(object? sender, EventArgs e)
    {
        try { await ResolveRollAsync(); }
        catch (Exception ex) { StatusMessage = "Roll detect: " + ex.Message; }
    }

    private async Task ResolveRollAsync()
    {
        if (Printer.LoadedRfid is not { TagPresent: true } r || string.IsNullOrEmpty(r.Barcode)) return;

        var known = _rollStore.FindByBarcode(r.Barcode);
        if (known is not null)
        {
            var hasMargin = ApplyResolvedRoll(known);
            _rollStore.Remember(known); // refresh last-used
            StatusMessage = $"Loaded roll: {known.Name} ({known.WidthMm:0.#}×{known.HeightMm:0.#} mm)"
                + (hasMargin ? $" — printable width {Printer.ConnectedPrintableWidthMm:0.#} mm; the rest crops at print." : "");
            return;
        }

        if (Dialogs is null) return;
        var seed = new RollDefinition
        {
            Barcode = r.Barcode,
            Uuid = r.Uuid,
            Serial = r.SerialNumber,
            ConsumablesType = r.ConsumablesType.ToString(),
            PaperType = MapPaper(r.ConsumablesType),
            Shape = "rectangle",
        };
        var defined = await Dialogs.DefineRollAsync(seed, "New label roll detected");
        if (defined is null) return;

        _rollStore.Remember(defined);
        ApplyResolvedRoll(defined);
        StatusMessage = $"Saved roll: {defined.Name}";
    }

    /// <summary>Apply a roll to the canvas, clamping width to the connected printer's printable area.
    /// Returns true if the canvas width was clamped below the roll's stock width.</summary>
    private bool ApplyResolvedRoll(RollDefinition roll)
    {
        // Canvas = label model: a roll only sizes the canvas for a *fresh* (empty) doc. If the user has
        // a design, preserve their size and just target the printer (printhead width + dpi) + paper type.
        if (Editor.HasElements)
            Editor.SetPrinterTarget(Printer.ConnectedPrintableWidthMm, Printer.ConnectedDpi);
        else
            Editor.ApplyRoll(roll.WidthMm, roll.HeightMm, roll.Shape, Printer.ConnectedDpi, Printer.ConnectedPrintableWidthMm);
        Printer.ApplyPaperType(roll.PaperType);
        RememberCanvas();
        return Editor.HasPrintableMargin; // a printable margin exists (label wider than the head)
    }

    private static string MapPaper(LabelType t) => t switch
    {
        LabelType.Black => "black",
        LabelType.Continuous => "continuous",
        LabelType.Transparent => "transparent",
        _ => "gap",
    };

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (FilePicker is null) return;
        if (!await EnsureDiscardableAsync()) return;
        var path = await FilePicker.OpenLabelAsync();
        if (path is null) return;
        OpenPath(path);
    }

    [RelayCommand]
    private async Task OpenRecentAsync(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;
        if (!await EnsureDiscardableAsync()) return;
        OpenPath(path);
    }

    /// <summary>True if it's safe to replace the current document (not dirty, or the user confirms discard).</summary>
    public async Task<bool> EnsureDiscardableAsync()
    {
        if (!Editor.Dirty) return true;
        return Dialogs is not null && await Dialogs.ConfirmDiscardAsync();
    }

    private void OpenPath(string path)
    {
        try
        {
            var package = LabelPackageIo.Load(path);
            Editor.LoadPackage(package, path);
            AddRecent(path);
            StatusMessage = $"Opened {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Open failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (Editor.FilePath is { } path) Save(path);
        else await SaveAsAsync();
    }

    [RelayCommand]
    private async Task SaveAsAsync()
    {
        if (FilePicker is null) return;
        var suggested = Editor.DocumentName is { Length: > 0 } n ? n : "label";
        var path = await FilePicker.SaveLabelAsync(suggested);
        if (path is null) return;
        Save(path);
    }

    private void Save(string path)
    {
        try
        {
            Editor.SaveTo(path);
            AddRecent(path);
            RememberCanvas(); // capture this label's canvas (incl. safe margin) as the default for new labels
            StatusMessage = $"Saved {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
    }

    // ── Edit / View ───────────────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(CanUndo))] private void Undo() => Editor.Undo();
    [RelayCommand(CanExecute = nameof(CanRedo))] private void Redo() => Editor.Redo();
    [RelayCommand(CanExecute = nameof(CanDelete))] private void Delete() => Editor.DeleteSelected();
    [RelayCommand(CanExecute = nameof(CanDelete))] private void Copy() => Editor.Copy();
    [RelayCommand(CanExecute = nameof(CanDelete))] private void Cut() => Editor.Cut();
    [RelayCommand(CanExecute = nameof(CanPaste))] private void Paste() => Editor.Paste();
    [RelayCommand(CanExecute = nameof(CanDelete))] private void Duplicate() => Editor.Duplicate();
    [RelayCommand(CanExecute = nameof(CanGroup))] private void Group() => Editor.Group();
    [RelayCommand(CanExecute = nameof(CanUngroup))] private void Ungroup() => Editor.Ungroup();
    [RelayCommand(CanExecute = nameof(CanDelete))] private void ToggleLock() => Editor.ToggleLock();
    [RelayCommand(CanExecute = nameof(CanDelete))] private void ToggleVisible() => Editor.ToggleVisible();

    [RelayCommand]
    private async Task ChooseImageAsync()
    {
        if (FilePicker is null || Editor.SelectedEditor is not ImageEditorViewModel) return;
        var path = await FilePicker.OpenImageAsync();
        if (path is null) return;
        try
        {
            var bytes = File.ReadAllBytes(path);
            if (Editor.SetImageAssetOnSelection(bytes, Path.GetExtension(path)))
                StatusMessage = $"Embedded {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Image load failed: {ex.Message}";
        }
    }

    private bool CanPaste() => Editor.HasClipboard;
    private bool CanUngroup() => Editor.HasGroupInSelection;
    private bool CanGroup() => Editor.SelectionCount >= 2;
    [RelayCommand] private void ZoomIn() => Editor.ZoomIn();
    [RelayCommand] private void ZoomOut() => Editor.ZoomOut();
    [RelayCommand] private void ZoomFit() => Editor.FitToWindow();
    [RelayCommand] private void RotateLeft() => Editor.RotateLeft();
    [RelayCommand] private void RotateRight() => Editor.RotateRight();

    private bool CanUndo() => Editor.CanUndo;
    private bool CanRedo() => Editor.CanRedo;
    private bool CanDelete() => Editor.SelectedEditor is not null;

    // ── Insert / Arrange ────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Insert(string? type)
    {
        if (!string.IsNullOrEmpty(type)) Editor.AddElement(type);
    }

    [RelayCommand(CanExecute = nameof(CanDelete))] private void BringToFront() => Editor.BringToFront();
    [RelayCommand(CanExecute = nameof(CanDelete))] private void SendToBack() => Editor.SendToBack();
    [RelayCommand(CanExecute = nameof(CanDelete))] private void BringForward() => Editor.BringForward();
    [RelayCommand(CanExecute = nameof(CanDelete))] private void SendBackward() => Editor.SendBackward();

    [RelayCommand(CanExecute = nameof(CanAlign))] private void AlignLeft() => Editor.AlignLeft();
    [RelayCommand(CanExecute = nameof(CanAlign))] private void AlignRight() => Editor.AlignRight();
    [RelayCommand(CanExecute = nameof(CanAlign))] private void AlignTop() => Editor.AlignTop();
    [RelayCommand(CanExecute = nameof(CanAlign))] private void AlignBottom() => Editor.AlignBottom();
    [RelayCommand(CanExecute = nameof(CanAlign))] private void AlignCenterH() => Editor.AlignCenterH();
    [RelayCommand(CanExecute = nameof(CanAlign))] private void AlignMiddleV() => Editor.AlignMiddleV();
    [RelayCommand(CanExecute = nameof(CanDistribute))] private void DistributeH() => Editor.DistributeH();
    [RelayCommand(CanExecute = nameof(CanDistribute))] private void DistributeV() => Editor.DistributeV();

    [RelayCommand(CanExecute = nameof(CanDelete))] private void CenterOnLabelH() => Editor.CenterOnLabelH();
    [RelayCommand(CanExecute = nameof(CanDelete))] private void CenterOnLabelV() => Editor.CenterOnLabelV();

    // ── Table cell operations (in cell-edit mode) — bound by both the right-click menu and the props panel ──
    private bool InCell() => Editor.InCellMode;
    [RelayCommand(CanExecute = nameof(InCell))] private void CellMerge() => Editor.MergeCells();
    [RelayCommand(CanExecute = nameof(InCell))] private void CellUnmerge() => Editor.UnmergeCells();
    [RelayCommand(CanExecute = nameof(InCell))] private void CellFill(string? pct) { if (int.TryParse(pct, out var p)) Editor.SetCellFill(p); }
    [RelayCommand(CanExecute = nameof(InCell))] private void CellTextBlack() => Editor.SetCellTextColor(false);
    [RelayCommand(CanExecute = nameof(InCell))] private void CellTextWhite() => Editor.SetCellTextColor(true);
    [RelayCommand(CanExecute = nameof(InCell))] private void CellBold() => Editor.ToggleCellBold();
    [RelayCommand(CanExecute = nameof(InCell))] private void CellItalic() => Editor.ToggleCellItalic();
    [RelayCommand(CanExecute = nameof(InCell))] private void CellAlignH(string? h) { if (h is not null) Editor.SetCellAlignH(h); }
    [RelayCommand(CanExecute = nameof(InCell))] private void CellAlignV(string? v) { if (v is not null) Editor.SetCellAlignV(v); }
    [RelayCommand(CanExecute = nameof(InCell))] private void CellHeaderRow() => Editor.ToggleHeaderRow();
    [RelayCommand(CanExecute = nameof(InCell))] private void CellHeaderColumn() => Editor.ToggleHeaderColumn();

    private bool CanAlign() => Editor.SelectionCount >= 2;
    private bool CanDistribute() => Editor.SelectionCount >= 3;

    [RelayCommand]
    private void Quit() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private async Task About()
    {
        if (Dialogs is not null) await Dialogs.ShowAboutAsync();
    }

    // ── MRU / settings ──────────────────────────────────────────────────────────────────────────

    private void AddRecent(string path)
    {
        _settings = SettingsService.WithRecent(_settings, path);
        _settingsService.Save(_settings);
        RecentFiles.Clear();
        foreach (var p in _settings.RecentFiles) RecentFiles.Add(p);
        OnPropertyChanged(nameof(HasRecentFiles));
    }

    /// <summary>
    /// Persist window + panel geometry. Position/size are only updated when the window is in its
    /// normal state (pass nulls while maximized so the restore size isn't clobbered with the
    /// maximized bounds). Panel widths and the maximized flag are always saved.
    /// </summary>
    public void SaveWindowState(double? x, double? y, double? width, double? height, bool maximized, double leftPanel, double rightPanel)
    {
        _settings = _settings with
        {
            WindowX = x ?? _settings.WindowX,
            WindowY = y ?? _settings.WindowY,
            WindowWidth = width ?? _settings.WindowWidth,
            WindowHeight = height ?? _settings.WindowHeight,
            WindowMaximized = maximized,
            LeftPanelWidth = leftPanel > 0 ? leftPanel : _settings.LeftPanelWidth,
            RightPanelWidth = rightPanel > 0 ? rightPanel : _settings.RightPanelWidth,
        };
        _settingsService.Save(_settings);
    }

    public AppSettings Settings => _settings;

    // ── Reactions ─────────────────────────────────────────────────────────────────────────────

    private void OnEditorStateChanged()
    {
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        UpdateTitle();
    }

    private void OnEditorPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.SelectedEditor))
        {
            DeleteCommand.NotifyCanExecuteChanged();
            CopyCommand.NotifyCanExecuteChanged();
            CutCommand.NotifyCanExecuteChanged();
            DuplicateCommand.NotifyCanExecuteChanged();
            BringToFrontCommand.NotifyCanExecuteChanged();
            SendToBackCommand.NotifyCanExecuteChanged();
            BringForwardCommand.NotifyCanExecuteChanged();
            SendBackwardCommand.NotifyCanExecuteChanged();
        }
        else if (e.PropertyName == nameof(EditorViewModel.SelectionCount))
        {
            AlignLeftCommand.NotifyCanExecuteChanged();
            AlignRightCommand.NotifyCanExecuteChanged();
            AlignTopCommand.NotifyCanExecuteChanged();
            AlignBottomCommand.NotifyCanExecuteChanged();
            AlignCenterHCommand.NotifyCanExecuteChanged();
            AlignMiddleVCommand.NotifyCanExecuteChanged();
            DistributeHCommand.NotifyCanExecuteChanged();
            DistributeVCommand.NotifyCanExecuteChanged();
            CenterOnLabelHCommand.NotifyCanExecuteChanged();
            CenterOnLabelVCommand.NotifyCanExecuteChanged();
            GroupCommand.NotifyCanExecuteChanged();
        }
        else if (e.PropertyName == nameof(EditorViewModel.HasGroupInSelection))
        {
            UngroupCommand.NotifyCanExecuteChanged();
        }
        else if (e.PropertyName == nameof(EditorViewModel.HasClipboard))
        {
            PasteCommand.NotifyCanExecuteChanged();
        }
        else if (e.PropertyName == nameof(EditorViewModel.InCellMode))
        {
            CellMergeCommand.NotifyCanExecuteChanged();
            CellUnmergeCommand.NotifyCanExecuteChanged();
            CellFillCommand.NotifyCanExecuteChanged();
            CellTextBlackCommand.NotifyCanExecuteChanged();
            CellTextWhiteCommand.NotifyCanExecuteChanged();
            CellBoldCommand.NotifyCanExecuteChanged();
            CellItalicCommand.NotifyCanExecuteChanged();
            CellAlignHCommand.NotifyCanExecuteChanged();
            CellAlignVCommand.NotifyCanExecuteChanged();
            CellHeaderRowCommand.NotifyCanExecuteChanged();
            CellHeaderColumnCommand.NotifyCanExecuteChanged();
            OnPropertyChanged(nameof(InspectorShowsCells));
        }
    }

    /// <summary>True when the Properties panel should show table-cell controls (a table is in cell mode).</summary>
    public bool InspectorShowsCells => Editor.InCellMode;

    private void UpdateTitle() =>
        Title = $"Thermalith — {Editor.DocumentName}{(Editor.Dirty ? " *" : "")}";
}
