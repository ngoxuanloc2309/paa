using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SimplePLC.Application.Enums;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Converters;

/// <summary>
/// Converter tính toán màu nền cho Badge trạng thái Live của Node trên Canvas.
/// </summary>
public class NodeLiveBadgeBackgroundConverter : IValueConverter, IMultiValueConverter
{
    private static readonly SolidColorBrush ActiveBrush = new(Color.FromRgb(0xE6, 0xF4, 0xEA)); // Industrial green tint
    private static readonly SolidColorBrush InactiveBrush = new(Color.FromRgb(0xF3, 0xF4, 0xF6)); // Industrial gray
    private static readonly SolidColorBrush StaleBrush = new(Color.FromRgb(0xFE, 0xF3, 0xC7)); // Industrial amber tint
    private static readonly SolidColorBrush OfflineBrush = new(Color.FromRgb(0xE5, 0xE7, 0xEB)); // Steel gray

    static NodeLiveBadgeBackgroundConverter()
    {
        ActiveBrush.Freeze();
        InactiveBrush.Freeze();
        StaleBrush.Freeze();
        OfflineBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = value switch
        {
            NodeLiveVisualState s => s,
            ILiveTagBoundNode liveNode => liveNode.LiveVisualState,
            _ => NodeLiveVisualState.Inactive
        };

        return state switch
        {
            NodeLiveVisualState.Active => ActiveBrush,
            NodeLiveVisualState.Stale => StaleBrush,
            NodeLiveVisualState.Offline => OfflineBrush,
            _ => InactiveBrush
        };
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 3 &&
            values[0] is bool isOnline &&
            values[1] is TagQuality quality &&
            values[2] is bool isActive)
        {
            if (!isOnline) return OfflineBrush;
            if (quality == TagQuality.Stale) return StaleBrush;
            if (isActive) return ActiveBrush;
            return InactiveBrush;
        }

        if (values.Length > 0 && values[0] != null)
        {
            return Convert(values[0], targetType, parameter, culture);
        }

        return InactiveBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converter tính toán màu viền cho Badge trạng thái Live của Node.
/// </summary>
public class NodeLiveBadgeBorderConverter : IValueConverter, IMultiValueConverter
{
    private static readonly SolidColorBrush ActiveBorder = new(Color.FromRgb(0x10, 0x7C, 0x41)); // Industrial Forest Green
    private static readonly SolidColorBrush InactiveBorder = new(Color.FromRgb(0xD1, 0xD5, 0xDB)); // Slate border
    private static readonly SolidColorBrush StaleBorder = new(Color.FromRgb(0xC0, 0x56, 0x21)); // Safety Amber
    private static readonly SolidColorBrush OfflineBorder = new(Color.FromRgb(0x9C, 0xA3, 0xAF)); // Steel gray
 
    static NodeLiveBadgeBorderConverter()
    {
        ActiveBorder.Freeze();
        InactiveBorder.Freeze();
        StaleBorder.Freeze();
        OfflineBorder.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = value switch
        {
            NodeLiveVisualState s => s,
            ILiveTagBoundNode liveNode => liveNode.LiveVisualState,
            _ => NodeLiveVisualState.Inactive
        };

        return state switch
        {
            NodeLiveVisualState.Active => ActiveBorder,
            NodeLiveVisualState.Stale => StaleBorder,
            NodeLiveVisualState.Offline => OfflineBorder,
            _ => InactiveBorder
        };
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 3 &&
            values[0] is bool isOnline &&
            values[1] is TagQuality quality &&
            values[2] is bool isActive)
        {
            if (!isOnline) return OfflineBorder;
            if (quality == TagQuality.Stale) return StaleBorder;
            if (isActive) return ActiveBorder;
            return InactiveBorder;
        }

        if (values.Length > 0 && values[0] != null)
        {
            return Convert(values[0], targetType, parameter, culture);
        }

        return InactiveBorder;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Converter tính toán màu chữ cho Badge trạng thái Live của Node.
/// </summary>
public class NodeLiveBadgeForegroundConverter : IValueConverter, IMultiValueConverter
{
    private static readonly SolidColorBrush ActiveText = new(Color.FromRgb(0x10, 0x7C, 0x41)); // Industrial Forest Green
    private static readonly SolidColorBrush InactiveText = new(Color.FromRgb(0x37, 0x41, 0x51)); // Industrial Slate
    private static readonly SolidColorBrush StaleText = new(Color.FromRgb(0xC0, 0x56, 0x21)); // Safety Amber
    private static readonly SolidColorBrush OfflineText = new(Color.FromRgb(0x6B, 0x72, 0x80)); // Muted text

    static NodeLiveBadgeForegroundConverter()
    {
        ActiveText.Freeze();
        InactiveText.Freeze();
        StaleText.Freeze();
        OfflineText.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var state = value switch
        {
            NodeLiveVisualState s => s,
            ILiveTagBoundNode liveNode => liveNode.LiveVisualState,
            _ => NodeLiveVisualState.Inactive
        };

        return state switch
        {
            NodeLiveVisualState.Active => ActiveText,
            NodeLiveVisualState.Stale => StaleText,
            NodeLiveVisualState.Offline => OfflineText,
            _ => InactiveText
        };
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 3 &&
            values[0] is bool isOnline &&
            values[1] is TagQuality quality &&
            values[2] is bool isActive)
        {
            if (!isOnline) return OfflineText;
            if (quality == TagQuality.Stale) return StaleText;
            if (isActive) return ActiveText;
            return InactiveText;
        }

        if (values.Length > 0 && values[0] != null)
        {
            return Convert(values[0], targetType, parameter, culture);
        }

        return InactiveText;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
