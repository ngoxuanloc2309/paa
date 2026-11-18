using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SimplePLC.Studio.Models;

/// <summary>
/// Mô hình đặc tả luật logic 32-byte do AI sinh ra hoặc nạp vào Bảng Rule (Wire Contract V1.7).
/// </summary>
public class AiRuleSpecModel
{
    [JsonPropertyName("narrative")]
    public string Narrative { get; set; } = string.Empty;

    [JsonPropertyName("input_tag")]
    public string InputTag { get; set; } = string.Empty;

    [JsonPropertyName("trigger")]
    public string Trigger { get; set; } = "ON_RISE";

    [JsonPropertyName("for_ms")]
    public uint ForMs { get; set; } = 50;

    [JsonPropertyName("compare_op")]
    public string CompareOp { get; set; } = "NONE";

    [JsonPropertyName("threshold_lo")]
    public int ThresholdLo { get; set; } = 0;

    [JsonPropertyName("threshold_hi")]
    public int ThresholdHi { get; set; } = 0;

    [JsonPropertyName("guard_tag")]
    public string GuardTag { get; set; } = "NONE";

    [JsonPropertyName("guard_op")]
    public string GuardOp { get; set; } = "NONE";

    [JsonPropertyName("guard_val")]
    public int GuardVal { get; set; } = 0;

    [JsonPropertyName("guard_negated")]
    public bool GuardNegated { get; set; } = false;

    [JsonPropertyName("action_tag")]
    public string ActionTag { get; set; } = string.Empty;

    [JsonPropertyName("action_type")]
    public string ActionType { get; set; } = "SET_TAG";

    [JsonPropertyName("param")]
    public int Param { get; set; } = 1;

    /// <summary>
    /// Tham số phụ cho Action (Reg 14..15 của chuẩn 32-byte V1.7):
    /// Lưu hệ số lệch b của SCALE_TAG hoặc toán hạng thứ 2 của MATH_OP.
    /// </summary>
    [JsonPropertyName("aux_param")]
    public int AuxParam { get; set; } = 0;

    /// <summary>
    /// Cờ chỉ định dữ liệu số thực dấu chấm động Float32 IEEE-754 (Flags Bit 0 hoặc Bit 1).
    /// </summary>
    [JsonPropertyName("is_float")]
    public bool IsFloat { get; set; } = false;
}

/// <summary>
/// Bộ phân tích và trích xuất danh sách luật JSON từ phản hồi văn bản của mô hình AI.
/// </summary>
public static class AiRuleParser
{
    private static readonly Regex RuleJsonBlockRegex = new(
        @"```(?:json:rules|json)?\s*(\[\s*\{.*?\}\s*\])(?:\s*```)?",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex FallbackArrayRegex = new(
        @"(\[\s*\{\s*""(?:narrative|input_tag)""[\s\S]*?\}\s*\])",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    private static readonly Regex OrphanedBlockRegex = new(
        @"```(?:json:rules|json)?\s*$",
        RegexOptions.Singleline | RegexOptions.IgnoreCase);

    public static List<AiRuleSpecModel> ExtractRules(string rawContent, out string cleanContent)
    {
        cleanContent = rawContent;
        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return new List<AiRuleSpecModel>();
        }

        var match = RuleJsonBlockRegex.Match(rawContent);
        if (!match.Success)
        {
            match = FallbackArrayRegex.Match(rawContent);
        }

        if (match.Success)
        {
            try
            {
                string jsonText = match.Groups[1].Value.Trim();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true
                };

                var rules = JsonSerializer.Deserialize<List<AiRuleSpecModel>>(jsonText, options);
                if (rules != null && rules.Count > 0)
                {
                    // Remove the raw JSON block from displayed content for clean UX
                    cleanContent = rawContent.Remove(match.Index, match.Length).TrimEnd();
                    // Dọn dẹp cả thẻ mở code block mồ côi nếu còn sót lại
                    cleanContent = OrphanedBlockRegex.Replace(cleanContent, "").TrimEnd();
                    return rules;
                }
            }
            catch
            {
                // If JSON is malformed, fail gracefully without breaking chat
            }
        }

        // Dọn dẹp thẻ mồ côi ngay cả khi không parse được
        cleanContent = OrphanedBlockRegex.Replace(cleanContent, "").TrimEnd();
        return new List<AiRuleSpecModel>();
    }
}
