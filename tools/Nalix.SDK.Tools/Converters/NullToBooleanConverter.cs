using System;
using System.Globalization;
using System.Windows.Data;

namespace Nalix.SDK.Tools.Converters;

/// <summary>
/// Converts null values to boolean flags for simple bindings.
/// </summary>
public sealed class NullToBooleanConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is not null;

    /// <inheritdoc/>
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => Binding.DoNothing;
}
