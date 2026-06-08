using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Input;

namespace Thermalith.App.Converters;

/// <summary>Maps a "busy" bool to a wait cursor (else the default) — global feedback during scan/connect/print.</summary>
public sealed class BoolToCursorConverter : IValueConverter
{
    private static readonly Cursor Wait = new(StandardCursorType.Wait);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? Wait : Cursor.Default;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
