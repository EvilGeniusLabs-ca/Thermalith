using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Thermalith.App.Converters;

/// <summary>
/// Null-safe converter for <c>NumericUpDown.Value</c> (a <c>decimal?</c>) bound to a non-nullable numeric
/// source (<c>double</c> / <c>int</c>). Clearing the box sets <c>Value</c> to null; the default converter
/// would then throw an <see cref="InvalidCastException"/> writing null into a non-nullable double/int.
/// Here <see cref="ConvertBack"/> returns <see cref="BindingOperations.DoNothing"/> on null — the source
/// keeps its prior value, so an empty field simply waits for a valid entry instead of crashing or
/// resetting to zero. Mirrors <see cref="NumericTextConverter"/> (the TextBox equivalent), for the
/// inspector's <c>NumericUpDown</c> fields. Handles <c>double</c>/<c>double?</c>/<c>int</c>/<c>int?</c>.
/// </summary>
public sealed class NumericValueConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return null;
        try { return System.Convert.ToDecimal(value, culture); }
        catch { return null; }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return BindingOperations.DoNothing; // empty box → leave the source unchanged
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        try { return System.Convert.ChangeType(value, underlying, culture); }
        catch { return BindingOperations.DoNothing; }
    }
}
