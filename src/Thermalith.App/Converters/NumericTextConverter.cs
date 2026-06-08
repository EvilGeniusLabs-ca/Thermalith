using System.Globalization;
using Avalonia.Data;
using Avalonia.Data.Converters;

namespace Thermalith.App.Converters;

/// <summary>
/// Two-way string↔number converter for the inspector's numeric fields. The point is robustness on
/// <b>blank/invalid</b> input: clearing a field and tabbing out must not throw a binding conversion
/// error (Avalonia's default string→double/double?/int converter throws on "") and must not lose the
/// value. On <see cref="ConvertBack"/>, empty or unparseable text returns <see cref="BindingOperations.DoNothing"/>
/// — the source is left unchanged (the field reverts to its prior value) and no validation error shows.
/// Handles <c>double</c>, <c>double?</c>, <c>int</c>, <c>int?</c>; culture-aware.
/// </summary>
public sealed class NumericTextConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        null => "",
        double d => d.ToString("0.###", culture),
        int i => i.ToString(culture),
        _ => value.ToString(),
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return BindingOperations.DoNothing; // blank → keep the existing value, no error

        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying == typeof(int))
            return int.TryParse(s, NumberStyles.Integer, culture, out var i) ? i : BindingOperations.DoNothing;

        return double.TryParse(s, NumberStyles.Float | NumberStyles.AllowThousands, culture, out var d)
            ? d
            : BindingOperations.DoNothing; // unparseable → keep existing, no error
    }
}
