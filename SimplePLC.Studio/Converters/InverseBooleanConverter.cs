using System;
using System.Globalization;
using System.Windows.Data;

namespace SimplePLC.Studio.Converters;

/// <summary>
/// Converts a boolean value to its logical inverse (true -> false, false -> true).
/// </summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : true;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool b ? !b : true;
    }
}
