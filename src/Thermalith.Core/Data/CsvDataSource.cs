using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Thermalith.Core.Data;

/// <summary>
/// Loads the canonical Thermalith merge CSV (GitHub #7): RFC 4180, UTF-8 (BOM tolerated), one row per
/// label, every value text (no type inference — formatting is the template's job). The first row is a
/// header by default; a blank or duplicated header makes that column ordinal-only. Empty cells stay
/// empty strings so a bound token over an empty cell renders blank rather than falling back to a default.
/// </summary>
public static class CsvDataSource
{
    /// <summary>Load a merge CSV from a file path.</summary>
    public static MergeDataSet Load(string path, bool hasHeader = true, string? delimiter = null)
    {
        using var reader = new StreamReader(path); // UTF-8 by default; detects + strips a BOM
        return Parse(reader, path, hasHeader, delimiter);
    }

    /// <summary>Parse a merge CSV from any reader (used by tests and non-file sources).</summary>
    public static MergeDataSet Parse(TextReader reader, string? path = null, bool hasHeader = true, string? delimiter = null)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,   // we split header/data ourselves so blank/dup headers stay addressable
            Delimiter = delimiter is { Length: > 0 } ? delimiter : ",",
            DetectDelimiter = false,
            BadDataFound = null,       // lenient: don't throw mid-file on odd quoting
            MissingFieldFound = null,
            IgnoreBlankLines = true,   // a trailing newline doesn't become an empty label row
            TrimOptions = TrimOptions.None,
        };

        var records = new List<string[]>();
        using (var parser = new CsvParser(reader, config))
            while (parser.Read())
                records.Add(parser.Record ?? []);

        // Column count = the widest record; ragged rows are padded with empty strings.
        var colCount = records.Count == 0 ? 0 : records.Max(r => r.Length);

        string[]? headerRow = null;
        var dataStart = 0;
        if (hasHeader && records.Count > 0) { headerRow = records[0]; dataStart = 1; }

        var columns = BuildColumns(colCount, headerRow);

        var rows = new List<MergeRow>(Math.Max(0, records.Count - dataStart));
        for (var i = dataStart; i < records.Count; i++)
            rows.Add(BuildRow(records[i], columns, colCount));

        return new MergeDataSet(path, hasHeader, columns, rows);
    }

    private static IReadOnlyList<MergeColumn> BuildColumns(int colCount, string[]? header)
    {
        // A header becomes addressable-by-name only when it's non-blank AND unique; blanks and duplicates
        // fall back to ordinal-only.
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (header is not null)
            foreach (var h in header)
            {
                var name = h?.Trim() ?? "";
                if (name.Length > 0) counts[name] = counts.GetValueOrDefault(name) + 1;
            }

        var cols = new List<MergeColumn>(colCount);
        for (var i = 0; i < colCount; i++)
        {
            var raw = header is not null && i < header.Length ? header[i]?.Trim() ?? "" : "";
            var unique = raw.Length > 0 && counts.GetValueOrDefault(raw) == 1;
            cols.Add(new MergeColumn(i + 1, unique ? raw : null));
        }
        return cols;
    }

    private static MergeRow BuildRow(string[] record, IReadOnlyList<MergeColumn> columns, int colCount)
    {
        var ordinal = new object?[colCount];
        var named = new Dictionary<string, object?>(StringComparer.Ordinal);
        for (var i = 0; i < colCount; i++)
        {
            var value = i < record.Length ? record[i] : "";
            ordinal[i] = value;
            if (columns[i].Name is { Length: > 0 } name) named[name] = value;
        }
        return new MergeRow(named, ordinal);
    }
}
