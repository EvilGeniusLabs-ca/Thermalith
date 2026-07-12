using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Thermalith.App.Services;
using Thermalith.Core.Data;

namespace Thermalith.App.ViewModels;

/// <summary>
/// Data merge / variable data (GitHub #7, V1): load a CSV, expose its columns as insertable tokens, and
/// batch-print one label per row. Binding is authoring — the columns menu just inserts a <c>{"name"}</c>
/// or <c>{n}</c> token into the focused field. UI-free: file dialog + prompts come through
/// <see cref="IFilePicker"/> / <see cref="IDialogService"/>, set by the view.
/// </summary>
public sealed partial class DataMergeViewModel : ObservableObject
{
    private readonly EditorViewModel _editor;
    private readonly PrinterViewModel _printer;
    private readonly Action<string> _setStatus;

    public DataMergeViewModel(EditorViewModel editor, PrinterViewModel printer, Action<string> setStatus)
    {
        _editor = editor;
        _printer = printer;
        _setStatus = setStatus;
    }

    /// <summary>Set by the view before first use.</summary>
    public IFilePicker? FilePicker { get; set; }

    /// <summary>Set by the view before first use.</summary>
    public IDialogService? Dialogs { get; set; }

    private MergeDataSet? _data;

    /// <summary>The loaded CSV's columns, for the Data Merge ▸ Columns token palette. Empty until a file loads.</summary>
    public ObservableCollection<MergeColumn> Columns { get; } = [];

    /// <summary>One-line summary of the loaded source for the menu / status, e.g. "sample.csv — 4 rows, 6 columns".</summary>
    [ObservableProperty] private string _sourceInfo = "No data source loaded";

    /// <summary>True once a CSV is loaded (gates the print + clear commands and the columns submenu).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PrintMergeCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    private bool _hasData;

    // ── Load / clear ─────────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SelectFileAsync()
    {
        if (FilePicker is null) return;
        var path = await FilePicker.OpenCsvAsync();
        if (path is null) return;
        try
        {
            StopPreview(); // a fresh source resets any active preview
            var data = CsvDataSource.Load(path);
            _data = data;
            Columns.Clear();
            foreach (var c in data.Columns) Columns.Add(c);
            HasData = true;
            SourceInfo = $"{System.IO.Path.GetFileName(path)} — {data.RowCount} row{(data.RowCount == 1 ? "" : "s")}, {data.Columns.Count} column{(data.Columns.Count == 1 ? "" : "s")}";
            _setStatus(SourceInfo);
        }
        catch (Exception ex)
        {
            _setStatus($"CSV load failed: {ex.Message}");
        }
    }

    [RelayCommand(CanExecute = nameof(HasData))]
    private void Clear()
    {
        StopPreview();
        _data = null;
        Columns.Clear();
        HasData = false;
        SourceInfo = "No data source loaded";
        _setStatus("Data source cleared.");
    }

    // ── Preview (GitHub #7.2): step through rows on the canvas ────────────────────────────────────

    /// <summary>Whether the canvas is showing merge rows (bound to the Data Merge ▸ Preview toggle).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextRowCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrevRowCommand))]
    private bool _isPreviewing;

    /// <summary>1-based index of the previewed row.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextRowCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrevRowCommand))]
    private int _previewNumber = 1;

    /// <summary>"Row 3 of 12" caption for the preview bar.</summary>
    [ObservableProperty] private string _previewRowLabel = "";

    partial void OnIsPreviewingChanged(bool value)
    {
        if (value)
        {
            if (_data is null || _data.RowCount == 0) { _isPreviewing = false; OnPropertyChanged(nameof(IsPreviewing)); _setStatus("Load a CSV before previewing."); return; }
            PreviewNumber = 1;
            ShowRow();
        }
        else
        {
            _editor.SetPreviewRow(null, 0);
            PreviewRowLabel = "";
        }
    }

    [RelayCommand(CanExecute = nameof(CanStepNext))]
    private void NextRow() { PreviewNumber++; ShowRow(); }

    [RelayCommand(CanExecute = nameof(CanStepPrev))]
    private void PrevRow() { PreviewNumber--; ShowRow(); }

    private bool CanStepNext() => IsPreviewing && _data is not null && PreviewNumber < _data.RowCount;
    private bool CanStepPrev() => IsPreviewing && PreviewNumber > 1;

    private void ShowRow()
    {
        if (_data is null || _data.RowCount == 0) return;
        var i = Math.Clamp(PreviewNumber - 1, 0, _data.RowCount - 1);
        _editor.SetPreviewRow(_data.Rows[i], i);
        PreviewRowLabel = $"Row {i + 1} of {_data.RowCount}";
        NextRowCommand.NotifyCanExecuteChanged();
        PrevRowCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ExitPreview() => StopPreview();

    private void StopPreview()
    {
        if (!IsPreviewing) return;
        IsPreviewing = false; // OnIsPreviewingChanged clears the canvas
    }

    // ── Print ──────────────────────────────────────────────────────────────────────────────────

    private bool CanPrintMerge() => HasData && _printer.IsConnected && !_printer.IsBusy;

    [RelayCommand(CanExecute = nameof(CanPrintMerge))]
    private async Task PrintMergeAsync()
    {
        if (_data is null || Dialogs is null) return;
        if (!_printer.IsConnected) { _setStatus("Connect a printer first."); return; }

        var copies = Math.Max(1, _printer.Copies);
        var rows = _data.RowCount;
        if (rows == 0) { _setStatus("The data source has no rows."); return; }

        var totalLabels = rows * copies;

        // Roll-capacity guard: never start more than the loaded roll can finish. On overflow, print the
        // whole rows that fit and let the user reprint the rest after a roll change (design: no auto-resume).
        var printRows = rows;
        var truncated = false;
        var remaining = _printer.RemainingLabels;
        if (remaining is { } rem && totalLabels > rem)
        {
            printRows = rem / copies;
            truncated = true;
            if (printRows <= 0)
            {
                await Dialogs.MessageAsync("Roll almost empty",
                    $"The roll has {rem} label(s) left, but each row prints {copies}. Change the roll and try again.");
                return;
            }
        }

        var willPrint = printRows * copies;
        var copyText = copies == 1 ? "1 copy" : $"{copies} copies";
        var message = truncated
            ? $"This run needs {totalLabels} labels but the roll holds {remaining}.\n\n" +
              $"Printing the first {printRows} of {rows} rows ({willPrint} labels, {copyText} each). " +
              "Reprint the rest after changing rolls.\n\nContinue?"
            : $"This will print {willPrint} label{(willPrint == 1 ? "" : "s")} " +
              $"({rows} row{(rows == 1 ? "" : "s")} × {copyText}).\n\nContinue?";

        if (!await Dialogs.ConfirmAsync("Print merge", message, "Print")) return;

        var progress = Dialogs.BeginMergeProgress("Printing merge", printRows);
        int printed;
        try
        {
            printed = await _printer.PrintMergeAsync(
                printRows,
                i => _editor.RenderForPrint(_data.Rows[i], i),
                new Progress<int>(done => progress.Report(done)),
                () => progress.IsCancelled);
        }
        finally
        {
            progress.Close();
        }

        _setStatus(printed < printRows
            ? $"Merge cancelled — printed {printed} of {printRows} labels."
            : $"Merge complete — printed {printed} label{(printed == 1 ? "" : "s")}.");
    }

    /// <summary>Re-evaluate the print command's enablement when the printer connects / goes busy.</summary>
    public void RefreshPrintable() => PrintMergeCommand.NotifyCanExecuteChanged();
}
