using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Thermalith.App.ViewModels.Editors;

/// <summary>One editable table cell (content only; per-cell justification is a later pass, §11.9).</summary>
public sealed partial class TableCellViewModel : ObservableObject
{
    private readonly Action _changed;

    public TableCellViewModel(string content, Action changed)
    {
        _content = content;
        _changed = changed;
    }

    [ObservableProperty] private string _content;

    partial void OnContentChanged(string value) => _changed();
}

/// <summary>A row of editable cells, so the inspector can bind a strongly-typed nested grid.</summary>
public sealed class TableRowViewModel(ObservableCollection<TableCellViewModel> cells)
{
    public ObservableCollection<TableCellViewModel> Cells { get; } = cells;
}
