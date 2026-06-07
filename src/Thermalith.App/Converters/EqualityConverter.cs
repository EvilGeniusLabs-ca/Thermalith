using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Thermalith.App.Converters;

/// <summary>
/// Two-way value↔bool converter for icon toggle groups (e.g. justification): a button is checked
/// when the bound value equals its <c>ConverterParameter</c>; checking it writes that parameter back.
/// Unchecking does nothing (radio semantics — another button becomes the checked one).
/// </summary>
public sealed class EqualityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value?.Equals(parameter) ?? parameter is null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? parameter : BindingOperations.DoNothing;
}
