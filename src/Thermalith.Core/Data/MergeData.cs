namespace Thermalith.Core.Data;

/// <summary>
/// One column of a loaded merge data set (GitHub #7). <see cref="Ordinal"/> is 1-based. <see cref="Name"/>
/// is null when the column's header is blank or duplicated — such a column is reachable only by ordinal.
/// </summary>
public sealed record MergeColumn(int Ordinal, string? Name)
{
    /// <summary>The token an author drops on a control to bind this column: a quoted name when the column
    /// has a unique, non-blank header, otherwise the bare 1-based ordinal.</summary>
    public string Token => Name is { Length: > 0 } n ? $"{{\"{n}\"}}" : $"{{{Ordinal}}}";

    /// <summary>Human label for the columns menu, e.g. <c>1  part_no</c> or <c>4  (unnamed)</c>.</summary>
    public string Label => $"{Ordinal}  {(Name is { Length: > 0 } n ? n : "(unnamed)")}";
}

/// <summary>One data row: values addressable by unique header name and by 1-based ordinal.</summary>
public sealed class MergeRow(IReadOnlyDictionary<string, object?> byName, IReadOnlyList<object?> byOrdinal)
{
    /// <summary>Values keyed by unique, non-blank header name (feeds <c>ResolveContext.Data</c>).</summary>
    public IReadOnlyDictionary<string, object?> ByName { get; } = byName;

    /// <summary>Every value in column order (feeds <c>ResolveContext.DataByOrdinal</c>).</summary>
    public IReadOnlyList<object?> ByOrdinal { get; } = byOrdinal;
}

/// <summary>A loaded merge data set — its columns and rows, ready to drive a batch print (GitHub #7).</summary>
public sealed class MergeDataSet(string? path, bool hasHeader, IReadOnlyList<MergeColumn> columns, IReadOnlyList<MergeRow> rows)
{
    public string? Path { get; } = path;
    public bool HasHeader { get; } = hasHeader;
    public IReadOnlyList<MergeColumn> Columns { get; } = columns;
    public IReadOnlyList<MergeRow> Rows { get; } = rows;
    public int RowCount => Rows.Count;
}
