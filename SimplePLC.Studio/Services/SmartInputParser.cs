using System.Globalization;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Bộ công cụ phân tích và chuẩn hóa dữ liệu nhập thông minh (Smart Type-Safe Input & Auto-normalization).
/// Đạt chuẩn công nghiệp IEC 61131-3:
/// - Kiểu BOOL: Chỉ chấp nhận và chuẩn hóa về 0 hoặc 1, hỗ trợ dung thứ (true/false/on/off/bật/tắt).
/// - Kiểu REAL (Float32): Luôn tự động format có phần thập phân (25 -> 25.0), chấp nhận cả dấu phẩy (25,5 -> 25.5).
/// - Kiểu INT32: Tự động làm sạch khoảng trắng, dung thứ nếu lỡ gõ đuôi .0 (100.0 -> 100).
/// </summary>
public static class SmartInputParser
{
    /// <summary>
    /// Phân tích chuỗi đầu vào thành giá trị Boolean (0 hoặc 1).
    /// Hỗ trợ cả 0/1 và các biến thể ngôn ngữ thường dùng.
    /// </summary>
    public static bool TryParseBool(string? text, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string clean = text.Trim().ToLowerInvariant();

        if (clean == "1" || clean == "true" || clean == "on" || clean == "bật" || clean == "bat" || clean == "high" || clean == "t" || clean == "y" || clean == "yes")
        {
            result = 1;
            return true;
        }

        if (clean == "0" || clean == "false" || clean == "off" || clean == "tắt" || clean == "tat" || clean == "low" || clean == "f" || clean == "n" || clean == "no")
        {
            result = 0;
            return true;
        }

        // Nếu người dùng nhập số nguyên khác (ví dụ > 0)
        if (int.TryParse(clean, out int intVal))
        {
            result = intVal != 0 ? 1 : 0;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Định dạng giá trị Boolean thành chuỗi nhị phân chuẩn (chỉ "0" hoặc "1").
    /// </summary>
    public static string FormatBool(int value)
    {
        return value != 0 ? "1" : "0";
    }

    /// <summary>
    /// Phân tích chuỗi số thực linh hoạt (hỗ trợ cả dấu chấm . và dấu phẩy ,).
    /// </summary>
    public static bool TryParseFlexibleDouble(string? text, out double result)
    {
        result = 0.0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string normalized = text.Trim().Replace(',', '.');

        if (normalized == "-" || normalized == "+" || normalized == "." || normalized == "-." || normalized == "+.")
        {
            return false;
        }

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Định dạng số thực chuẩn công nghiệp IEC:
    /// Nếu là số nguyên như 25 -> tự thêm phần thập phân thành "25.0".
    /// </summary>
    public static string FormatReal(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value)) return "0.0";

        // Kiểm tra nếu giá trị là số nguyên (hoặc gần như nguyên)
        if (Math.Abs(value - Math.Round(value)) < 1e-9)
        {
            return value.ToString("0.0", CultureInfo.InvariantCulture);
        }

        // Định dạng tối đa 4 chữ số thập phân, tự loại bỏ số 0 thừa ở cuối nhưng giữ tối thiểu 1 chữ số thập phân
        return value.ToString("0.0###", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Phân tích số nguyên có dung thứ (làm sạch khoảng trắng, parse "100.0" -> 100).
    /// </summary>
    public static bool TryParseInt(string? text, out int result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string clean = text.Trim();

        if (int.TryParse(clean, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        // Thử parse dạng double nếu người dùng lỡ gõ đuôi .0
        if (TryParseFlexibleDouble(clean, out double dVal))
        {
            if (Math.Abs(dVal - Math.Round(dVal)) < 1e-9 && dVal >= int.MinValue && dVal <= int.MaxValue)
            {
                result = (int)Math.Round(dVal);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Phân tích và chuẩn hóa dựa trên TagDataType và cờ isDigital.
    /// </summary>
    public static bool TryParse(string? text, TagDataType dataType, bool isDigital, out int rawValue, out string formattedDisplay)
    {
        rawValue = 0;
        formattedDisplay = "0";

        if (isDigital || dataType == TagDataType.Boolean)
        {
            if (TryParseBool(text, out int boolVal))
            {
                rawValue = boolVal;
                formattedDisplay = FormatBool(boolVal);
                return true;
            }
            return false;
        }

        if (dataType == TagDataType.Float32)
        {
            if (TryParseFlexibleDouble(text, out double realVal))
            {
                rawValue = BitConverter.SingleToInt32Bits((float)realVal);
                formattedDisplay = FormatReal(realVal);
                return true;
            }
            return false;
        }

        // Mặc định là INT32
        if (TryParseInt(text, out int intVal))
        {
            rawValue = intVal;
            formattedDisplay = intVal.ToString();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Phân tích và chuẩn hóa thông minh dựa trên kiểu dữ liệu của TagModel.
    /// </summary>
    public static bool TryParseForTag(string? text, TagModel? tag, out int rawValue, out string formattedDisplay)
    {
        if (tag == null)
        {
            return TryParse(text, TagDataType.Int32, false, out rawValue, out formattedDisplay);
        }

        return TryParse(text, tag.DataType, tag.IsDigital, out rawValue, out formattedDisplay);
    }
}
