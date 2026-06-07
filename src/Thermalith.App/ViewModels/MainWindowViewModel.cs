using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Thermalith.App.Services;
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
    private AppSettings _settings;

    public MainWindowViewModel() : this(new SettingsService()) { }

    public MainWindowViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        _settings = settingsService.Load();
        Editor = new EditorViewModel();
        RecentFiles = new ObservableCollection<string>(_settings.RecentFiles);

        Editor.StateChanged += (_, _) => OnEditorStateChanged();
        Editor.PropertyChanged += OnEditorPropertyChanged;
        UpdateTitle();
    }

    public EditorViewModel Editor { get; }
    public ObservableCollection<string> RecentFiles { get; }

    /// <summary>Set by the view before first use.</summary>
    public IFilePicker? FilePicker { get; set; }

    /// <summary>Raised when the user chooses Quit; the view closes the window.</summary>
    public event EventHandler? CloseRequested;

    [ObservableProperty] private string _title = "Thermalith";
    [ObservableProperty] private string _statusMessage = "";
    public bool HasRecentFiles => RecentFiles.Count > 0;

    // ── File ────────────────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void New() => Editor.NewDocument();

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (FilePicker is null) return;
        var path = await FilePicker.OpenLabelAsync();
        if (path is null) return;
        OpenPath(path);
    }

    [RelayCommand]
    private void OpenRecent(string? path)
    {
        if (!string.IsNullOrEmpty(path)) OpenPath(path);
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
    [RelayCommand] private void ZoomIn() => Editor.ZoomIn();
    [RelayCommand] private void ZoomOut() => Editor.ZoomOut();
    [RelayCommand] private void ZoomFit() => Editor.FitToWidth();

    private bool CanUndo() => Editor.CanUndo;
    private bool CanRedo() => Editor.CanRedo;
    private bool CanDelete() => Editor.SelectedEditor is not null;

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

    /// <summary>Persist window/panel geometry on close.</summary>
    public void SaveLayout(double width, double height, double leftPanel, double rightPanel)
    {
        _settings = _settings with
        {
            WindowWidth = width,
            WindowHeight = height,
            LeftPanelWidth = leftPanel,
            RightPanelWidth = rightPanel,
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
            DeleteCommand.NotifyCanExecuteChanged();
    }

    private void UpdateTitle() =>
        Title = $"Thermalith — {Editor.DocumentName}{(Editor.Dirty ? " *" : "")}";
}
