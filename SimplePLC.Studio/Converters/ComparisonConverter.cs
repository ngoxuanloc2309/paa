using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SimplePLC.Studio.Converters;

/// <summary>
/// Compares a bound value with a converter parameter string for two-way radio button selection.
/// Supports both integer indices and string mode identifiers.
/// </summary>
public class ComparisonConverter : IValueConverter
{
    /// <summary>
    /// Checks whether the bound value matches the converter parameter.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() == parameter?.ToString();
    }

    /// <summary>
    /// Converts boolean check state back to the corresponding integer or string parameter value.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null)
        {
            string paramStr = parameter.ToString() ?? string.Empty;
            if (targetType == typeof(int))
            {
                return int.TryParse(paramStr, out int intVal) ? intVal : 0;
            }

            if (targetType == typeof(string) || targetType == typeof(object))
            {
                if (targetType == typeof(object) && int.TryParse(paramStr, out int objInt))
                {
                    return objInt;
                }
                return paramStr;
            }

            if (targetType.IsEnum)
            {
                try { return Enum.Parse(targetType, paramStr, ignoreCase: true); }
                catch { return Binding.DoNothing; }
            }

            if (int.TryParse(paramStr, out int fallbackVal))
            {
                return fallbackVal;
            }

            return paramStr;
        }

        return Binding.DoNothing;
    }
}

/// <summary>
/// Converts value equality check into a <see cref="Visibility"/> state.
/// </summary>
public class ComparisonToVisibilityConverter : IValueConverter
{
    /// <summary>
    /// Returns Visible if value matches parameter; otherwise Collapsed.
    /// </summary>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool match = value?.ToString() == parameter?.ToString();
        return match ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>
    /// ConvertBack is not supported.
    /// </summary>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return Binding.DoNothing;
    }
}
