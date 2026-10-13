using System;

namespace SimplePLC.Studio.Services.Ai;

/// <summary>
/// Các phân loại ý định chính của người dùng trong phân hệ AI Copilot.
/// </summary>
public enum AiUserIntent
{
    /// <summary>
    /// Tham vấn, hỏi đáp kỹ thuật, lý thuyết, tiêu chuẩn an toàn (IEC 60204-1, Debounce, Fail-safe).
    /// Không sinh Ghost Nodes, không can thiệp Canvas, không ép khuôn JSON.
    /// </summary>
    ConsultativeQnA,

    /// <summary>
    /// Yêu cầu thiết kế, sinh mạch FBD, thêm/sửa node, nối dây, cấu hình Timer/Counter/Scale.
    /// Kích hoạt Tool Calling, sinh Ghost Preview, kiểm duyệt qua AiSafetyGate.
    /// </summary>
    FbdSynthesis,

    /// <summary>
    /// Rà soát, kiểm tra lỗi cú pháp, xung đột ghi (Write Conflict), khóa liên động, chẩn đoán mạch.
    /// Phân tích hiện trạng Canvas và nhật ký lỗi Linter mà không tự tiện thay đổi sơ đồ.
    /// </summary>
    ProjectDiagnose
}

/// <summary>
/// Bộ phân loại ý định ngữ nghĩa tự động (Intent Classifier Service - Layer 1).
/// </summary>
public static class AiIntentClassifier
{
    private static readonly string[] DiagnoseKeywords =
    {
        "kiểm tra lỗi", "kiểm tra sơ đồ", "kiểm tra mạch", "check lỗi", "tìm lỗi",
        "rà soát lỗi", "rà soát", "soát lỗi", "chẩn đoán", "báo lỗi gì", "có lỗi gì",
        "bị lỗi gì", "lỗi gì", "lỗi cú pháp", "lỗi logic", "xung đột ghi", "write conflict",
        "xung đột dữ liệu", "xung đột", "deadlock", "vòng lặp vô hạn", "kiểm tra xung đột",
        "kiểm tra an toàn", "mạch có an toàn không", "sơ đồ có an toàn không", "sao không chạy",
        "tại sao không chạy", "sao lại không chạy", "không hoạt động", "không chạy được",
        "tại sao không chạy được", "không kích hoạt", "phát hiện lỗi", "kiểm tra giúp", "check sơ đồ"
    };

    private static readonly string[] ConsultativeKeywords =
    {
        "tại sao", "vì sao", "sao lại", "lý do gì",
        "giải thích", "cho biết", "làm rõ", "phân tích",
        "là gì", "nghĩa là gì", "ý nghĩa", "định nghĩa",
        "thế nào là", "như thế nào", "hoạt động như thế nào", "ra sao", "làm sao để",
        "nguyên lý", "nguyên tắc", "cơ chế hoạt động", "cơ chế",
        "khác nhau", "khác biệt", "sự khác biệt", "so sánh", "ưu điểm", "nhược điểm", "ưu nhược điểm",
        "tính toán", "tính giúp", "công thức", "hệ số k", "hệ số b", "gain", "offset",
        "chuẩn iec", "tiêu chuẩn iec", "tiêu chuẩn", "iec 60204", "iec 61131", "isa-101",
        "tiêu chuẩn an toàn", "an toàn điện",
        "chống rung", "chống dội", "debounce", "bouncing",
        "fail-safe", "failsafe", "an toàn hỏng hóc",
        "nhiễu điện", "nhiễu tín hiệu", "cách ly quang", "optocoupler",
        "hardware interlock", "software interlock", "khóa chéo phần cứng", "khóa chéo phần mềm",
        "tiếp điểm thường đóng", "tiếp điểm thường mở", "nc hay no", "no hay nc",
        "giới hạn vật lý", "quá nhiệt", "dòng rò", "sụt áp",
        "tư vấn", "cho tôi hỏi", "có nên", "khi nào nên", "hướng dẫn nguyên lý"
    };

    private static readonly string[] SynthesisKeywords =
    {
        "tạo mạch", "thiết kế", "vẽ sơ đồ", "vẽ mạch", "vẽ cho tôi", "vẽ giúp", "tạo sơ đồ",
        "xây dựng mạch", "thêm timer", "thêm counter", "thêm khối", "thêm node", "thêm input",
        "thêm trigger", "thêm guard", "thêm action", "thêm scale", "cấu hình timer",
        "cấu hình counter", "cấu hình scale", "cấu hình bộ đếm", "cấu hình rơ le",
        "nối dây", "kết nối", "nối chân", "ngắt dây", "xóa node", "xóa khối",
        "đổi tag", "đổi tên tag", "gán alias", "đặt tên tag", "lập trình mạch",
        "tạo rule", "sinh rule", "tạo logic", "tự động sinh", "mạch tự giữ",
        "mạch khởi động", "mạch đảo chiều", "mạch sao tam giác", "hẹn giờ",
        "đếm sản phẩm", "đếm xung", "quy đổi cảm biến", "quy đổi", "scale áp suất", "scale nhiệt độ"
    };

    /// <summary>
    /// Phân loại ý định của người dùng từ prompt nhập vào và trạng thái ngữ cảnh Canvas.
    /// </summary>
    public static AiUserIntent Classify(string prompt, bool hasCanvasElements = false, bool hasLinterErrors = false)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return hasLinterErrors ? AiUserIntent.ProjectDiagnose : AiUserIntent.ConsultativeQnA;
        }

        string text = prompt.Trim().ToLowerInvariant();

        // 1. Phân loại ý định Chẩn đoán lỗi (ProjectDiagnose)
        if (IsDiagnoseIntent(text, hasLinterErrors))
        {
            return AiUserIntent.ProjectDiagnose;
        }

        // 2. Phân biệt Hỏi đáp tư vấn (ConsultativeQnA) vs Sinh mạch FBD (FbdSynthesis)
        bool isConsultative = IsConsultativeIntent(text);
        bool isSynthesis = IsSynthesisIntent(text);

        if (isConsultative && !isSynthesis)
        {
            return AiUserIntent.ConsultativeQnA;
        }

        if (isSynthesis && !isConsultative)
        {
            return AiUserIntent.FbdSynthesis;
        }

        if (isConsultative && isSynthesis)
        {
            // Trọng tài cú pháp: nếu câu hỏi được mở đầu bằng các câu hỏi lý thuyết thay vì mệnh lệnh hành động
            // Ví dụ: "Giải thích nguyên lý tạo mạch tự giữ" -> Consultative
            // Nhưng: "Tạo mạch tự giữ và giải thích nguyên lý" -> FbdSynthesis
            if (StartsOrFramedAsConsultative(text))
            {
                return AiUserIntent.ConsultativeQnA;
            }
            return AiUserIntent.FbdSynthesis;
        }

        // Mặc định: nếu không có bất kỳ động từ chỉ lệnh sinh mạch FBD nào, coi là hỏi đáp tư vấn
        return AiUserIntent.ConsultativeQnA;
    }

    private static bool IsDiagnoseIntent(string text, bool hasLinterErrors)
    {
        if (text.Contains("không chạy", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("không hoạt động", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("không kích hoạt", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("không phản hồi", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if ((text.Contains("tại sao", StringComparison.OrdinalIgnoreCase) || text.Contains("sao", StringComparison.OrdinalIgnoreCase)) &&
            (text.Contains("lỗi", StringComparison.OrdinalIgnoreCase) || text.Contains("không chạy", StringComparison.OrdinalIgnoreCase) || text.Contains("bị dừng", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        foreach (var kw in DiagnoseKeywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (hasLinterErrors && (text.Contains("kiểm tra") || text.Contains("lỗi") ||
                                text.Contains("check") || text.Contains("sao thế") || text.Contains("sao vậy")))
        {
            return true;
        }

        return false;
    }

    private static bool IsConsultativeIntent(string text)
    {
        foreach (var kw in ConsultativeKeywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        if (text.EndsWith("?") && (text.Contains("phải không") || text.Contains("đúng không") || text.Contains("thế nào")))
        {
            return true;
        }

        return false;
    }

    private static bool IsSynthesisIntent(string text)
    {
        foreach (var kw in SynthesisKeywords)
        {
            if (text.Contains(kw, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Dạng thức hành động tự động hóa điển hình: "bật bơm DO0 khi...", "tắt quạt DO1...", "đóng van xả DO0 khi..."
        bool hasActionVerb = text.Contains("bật") || text.Contains("tắt") || text.Contains("mở") ||
                             text.Contains("đóng") || text.Contains("ngắt") || text.Contains("kích") ||
                             text.Contains("chạy") || text.Contains("dừng");

        bool hasTargetDevice = text.Contains("do") || text.Contains("bơm") || text.Contains("đèn") ||
                               text.Contains("van") || text.Contains("tải") || text.Contains("còi") ||
                               text.Contains("quạt") || text.Contains("động cơ");

        bool hasCondition = text.Contains("khi") || text.Contains("nếu") || text.Contains("vượt quá") ||
                            text.Contains("đạt") || text.Contains("duy trì") || text.Contains("nhấn nút") ||
                            text.Contains("cảm biến") || text.Contains("sau");

        if (hasActionVerb && hasTargetDevice && hasCondition)
        {
            return true;
        }

        return false;
    }

    private static bool StartsOrFramedAsConsultative(string text)
    {
        string[] questionStarters =
        {
            "tại sao", "vì sao", "giải thích", "cho biết", "làm rõ", "là gì",
            "thế nào là", "như thế nào", "khác nhau", "khác biệt", "sự khác biệt",
            "tính toán", "tính giúp", "tư vấn", "cho tôi hỏi", "có nên"
        };

        foreach (var qs in questionStarters)
        {
            if (text.StartsWith(qs, StringComparison.OrdinalIgnoreCase))
                return true;

            if (text.Contains(qs + " ", StringComparison.OrdinalIgnoreCase) ||
                text.Contains(" " + qs, StringComparison.OrdinalIgnoreCase))
            {
                if (!text.StartsWith("tạo") && !text.StartsWith("hãy tạo") &&
                    !text.StartsWith("vẽ") && !text.StartsWith("hãy vẽ") &&
                    !text.StartsWith("thiết kế") && !text.StartsWith("thêm"))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
