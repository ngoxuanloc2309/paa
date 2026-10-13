using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SimplePLC.Domain.Validation;

/// <summary>
/// Bộ kiểm định và chuẩn hóa Chủ đề MQTT (MQTT Topic Sanitizer & Validator)
/// Tuân thủ tiêu chuẩn quốc tế OASIS MQTT v3.1.1 & v5.0 RFC.
/// </summary>
public static class MqttTopicValidator
{
    public const int MaxTopicLength = 64;

    /// <summary>
    /// Tự động làm sạch chuỗi Topic: xóa khoảng trắng thừa, xóa gạch chéo dư ở đầu/cuối,
    /// chuyển tiếng Việt có dấu thành không dấu, thay khoảng trắng giữa các từ thành gạch dưới '_'.
    /// </summary>
    public static string Sanitize(string? rawTopic)
    {
        if (string.IsNullOrWhiteSpace(rawTopic))
        {
            return string.Empty;
        }

        string topic = rawTopic.Trim();

        // 1. Chuyển tiếng Việt có dấu thành không dấu
        topic = RemoveDiacritics(topic);

        // 2. Tách theo dấu gạch chéo '/' để làm sạch từng level
        string[] parts = topic.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        var sb = new StringBuilder();

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i].Trim();
            if (string.IsNullOrEmpty(part))
            {
                continue;
            }

            // Thay thế khoảng trắng hoặc ký tự phân cách lạ trong 1 level bằng dấu gạch dưới '_'
            part = Regex.Replace(part, @"\s+", "_");

            // Chỉ giữ lại chữ cái, chữ số, gạch dưới, gạch ngang và dấu ngoặc nhọn nếu là macro {device}
            part = Regex.Replace(part, @"[^a-zA-Z0-9_\-\{\}]", "");
            part = part.ToLowerInvariant();

            if (part.Length > 0)
            {
                if (sb.Length > 0)
                {
                    sb.Append('/');
                }
                sb.Append(part);
            }
        }

        string result = sb.ToString();

        // Cắt bớt nếu vượt quá độ dài tối đa
        if (result.Length > MaxTopicLength)
        {
            result = result.Substring(0, MaxTopicLength).TrimEnd('/');
        }

        return result;
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của Topic theo chuẩn Publish của MQTT.
    /// </summary>
    public static bool Validate(string? topic, out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(topic))
        {
            errorMessage = "Chủ đề MQTT không được để trống.";
            return false;
        }

        string trimmed = topic.Trim();

        if (trimmed.Length > MaxTopicLength)
        {
            errorMessage = $"Độ dài Chủ đề MQTT ({trimmed.Length}) vượt quá giới hạn tối đa cho phép của MCU ({MaxTopicLength} ký tự).";
            return false;
        }

        // Ký tự Wildcard '+' và '#' bị cấm tuyệt đối khi Publish
        if (trimmed.Contains('+') || trimmed.Contains('#'))
        {
            errorMessage = "Ký tự wildcard '+' và '#' chỉ dùng khi nhận tin (Subscribe), cấm dùng khi phát tin (Publish).";
            return false;
        }

        // Tiền tố '$' dành riêng cho Broker nội bộ
        if (trimmed.StartsWith('$'))
        {
            errorMessage = "Tiền tố '$' chỉ dành riêng cho hệ thống nội bộ của Broker (ví dụ $SYS/).";
            return false;
        }

        // Không được bắt đầu hoặc kết thúc bằng '/'
        if (trimmed.StartsWith('/') || trimmed.EndsWith('/'))
        {
            errorMessage = "Chủ đề không được bắt đầu hoặc kết thúc bằng dấu gạch chéo ('/').";
            return false;
        }

        // Không được chứa khoảng trắng
        if (trimmed.Contains(' '))
        {
            errorMessage = "Chủ đề MQTT không được chứa khoảng trắng.";
            return false;
        }

        // Kiểm tra dấu gạch chéo rỗng liên tiếp '//'
        if (trimmed.Contains("//"))
        {
            errorMessage = "Chủ đề không được chứa dấu gạch chéo liên tiếp ('//').";
            return false;
        }

        // Kiểm tra ký tự điều khiển hoặc ký tự Unicode ngoài chuẩn ASCII
        foreach (char c in trimmed)
        {
            if (char.IsControl(c) || c == '\0')
            {
                errorMessage = "Chủ đề chứa ký tự điều khiển không hợp lệ.";
                return false;
            }

            if (c > 127)
            {
                errorMessage = "Chủ đề không được chứa ký tự tiếng Việt có dấu hoặc ký tự ngoài bảng mã chuẩn ASCII.";
                return false;
            }
        }

        errorMessage = string.Empty;
        return true;
    }

    /// <summary>
    /// Chuyển đổi chuỗi tiếng Việt có dấu thành không dấu (Slugify helper).
    /// </summary>
    private static string RemoveDiacritics(string text)
    {
        string normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();

        foreach (char c in normalized)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
            {
                if (c == 'đ' || c == 'Đ')
                {
                    sb.Append(c == 'đ' ? 'd' : 'D');
                }
                else
                {
                    sb.Append(c);
                }
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
