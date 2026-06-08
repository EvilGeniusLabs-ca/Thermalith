using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Niimbot.Net.Commands;
using Thermalith.App.Services;
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
        UpdateTitle();
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
        Editor.ApplyRoll(def.WidthMm, def.HeightMm, def.Shape, Printer.ConnectedDpi);
        Printer.ApplyPaperType(def.PaperType);
        _rollStore.Remember(def);
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
            ApplyResolvedRoll(known);
            _rollStore.Remember(known); // refresh last-used
            StatusMessage = $"Loaded roll: {known.Name} ({known.WidthMm:0.#}×{known.HeightMm:0.#} mm)";
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

    private void ApplyResolvedRoll(RollDefinition roll)
    {
        Editor.ApplyRoll(roll.WidthMm, roll.HeightMm, roll.Shape, Printer.ConnectedDpi);
        Printer.ApplyPaperType(roll.PaperType);
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

    private bool CanPaste() => Editor.HasClipboard;
    private bool CanUngroup() => Editor.HasGroupInSelection;
    private bool CanGroup() => Editor.SelectionCount >= 2;
    [RelayCommand] private void ZoomIn() => Editor.ZoomIn();
    [RelayCommand] private void ZoomOut() => Editor.ZoomOut();
    [RelayCommand] private void ZoomFit() => Editor.FitToWidth();

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

    private bool CanAlign() => Editor.SelectionCount >= 2;
    private bool CanDistribute() => Editor.SelectionCount >= 3;

    [RelayCommand]
    private void Quit() => CloseRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand]
    private void About() =>
        StatusMessage = "Thermalith — Open-Source NIIMBOT System · GPL-3.0-or-later · EvilGeniusLabs";

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
    }

    private void UpdateTitle() =>
        Title = $"Thermalith — {Editor.DocumentName}{(Editor.Dirty ? " *" : "")}";
}
