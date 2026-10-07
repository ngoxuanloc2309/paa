using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using SimplePLC.Application.Enums;

namespace SimplePLC.Studio.Converters;

/// <summary>
/// Chuyển đổi TagQuality thành Brush màu sắc cho UI.
/// Tách biệt hoàn toàn Styling khỏi ViewModel.
/// </summary>
public class TagQualityToBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush GoodBrush = new(Color.FromRgb(0x10, 0x7C, 0x41));   // #107C41 IEC 60073 Industrial Green
    private static readonly SolidColorBrush StaleBrush = new(Color.FromRgb(0xC0, 0x56, 0x21));  // #C05621 IEC 60073 Warning Amber
    private static readonly SolidColorBrush UnknownBrush = new(Color.FromRgb(0x94, 0xA3, 0xB8));// Slate gray

    static TagQualityToBrushConverter()
    {
        GoodBrush.Freeze();
        StaleBrush.Freeze();
        UnknownBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TagQuality quality)
        {
            return quality switch
            {
                TagQuality.Good => GoodBrush,
                TagQuality.Stale => StaleBrush,
                _ => UnknownBrush
            };
        }

        return UnknownBrush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Chuyển đổi boolean IsOn và TagQuality thành Brush màu.
/// Khi Quality là Good: ON xanh lá (#107C41), OFF xám (#64748B).
/// Khi Quality không phải Good (Stale/Unknown): Giữ trạng thái nhưng làm mờ màu xám trung tính (#94A3B8)
/// để phân biệt rõ ràng dữ liệu cũ đã mất kết nối với dữ liệu đang chạy thực tế.
/// </summary>
public class BooleanQualityToStatusBrushConverter : IMultiValueConverter
{
    private static readonly SolidColorBrush ActiveOnBrush = new(Color.FromRgb(0x10, 0x7C, 0x41));  // IEC Green
    private static readonly SolidColorBrush StaleOnBrush = new(Color.FromRgb(0x94, 0xA3, 0xB8));   // Slate Gray
    private static readonly SolidColorBrush OffBrush = new(Color.FromRgb(0x64, 0x74, 0x8B));       // Inactive Slate

    static BooleanQualityToStatusBrushConverter()
    {
        ActiveOnBrush.Freeze();
        StaleOnBrush.Freeze();
        OffBrush.Freeze();
    }

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        bool isOn = values.Length > 0 && values[0] is bool b && b;
        TagQuality quality = values.Length > 1 && values[1] is TagQuality q ? q : TagQuality.Unknown;

        if (isOn)
        {
            return quality == TagQuality.Good ? ActiveOnBrush : StaleOnBrush;
        }

        return OffBrush;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Chuyển đổi TagQuality thành Opacity hiển thị (Good = 1.0, Stale/Unknown = 0.65).
/// </summary>
public class TagQualityToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TagQuality quality && quality == TagQuality.Good)
        {
            return 1.0;
        }

        return 0.65;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

