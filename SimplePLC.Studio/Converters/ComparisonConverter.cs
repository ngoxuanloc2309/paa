using System.Globalization;
using System.Windows.Data;

namespace SimplePLC.Studio.Converters;

public class ComparisonConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() == parameter?.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool b && b && parameter != null && int.TryParse(parameter.ToString(), out int val))
        {
            return val;
        }
        return Binding.DoNothing;
    }
}
