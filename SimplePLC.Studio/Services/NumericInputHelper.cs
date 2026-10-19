using System.Globalization;

namespace SimplePLC.Studio.Services;

/// <summary>
/// Helper for robust culture-tolerant decimal numeric parsing and formatting for WPF text input.
/// Accepts both '.' and ',' as decimal separators, avoiding truncation or caret jumps during typing.
/// </summary>
public static class NumericInputHelper
{
    public static bool TryParseFlexibleDouble(string? text, out double result)
    {
        result = 0.0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string normalized = text.Trim().Replace(',', '.');

        // Do not fail or treat incomplete prefixes as valid numbers
        if (normalized == "-" || normalized == "+" || normalized == "." || normalized == "-." || normalized == "+.")
        {
            return false;
        }

        return double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    public static string FormatDouble(double value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
