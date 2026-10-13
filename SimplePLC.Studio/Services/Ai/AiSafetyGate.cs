using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services.Ai;

public record SafetyValidationResult(bool IsValid, string? ErrorCode, string? ErrorMessage, string? Guidance);

/// <summary>
/// Cổng Thẩm Định An Toàn & Logic Công Nghiệp (Layer 3).
/// Kiểm tra:
/// 1. GraphGrammarV1 (Không chained guards SPLC-GRAPH-007, không lấy Action làm nguồn phát SPLC-GRAPH-002, 1 dây/cổng SPLC-GRAPH-010).
/// 2. WriteConflictValidator (Không ghi nhiều lần vào cùng một cuộn dây DO / Tag ngoài Macro SPLC-COMP-DEP-002).
/// 3. ReadOnlyTag Invariant (Không ghi vào DI, AI SPLC-TAG-ERR-READONLY).
/// </summary>
public static class AiSafetyGate
{
    public static SafetyValidationResult Validate(DraftGraphTransaction tx, IEnumerable<TagModel> availableTags)
    {
        var tagList = availableTags.ToList();
        var nodeMap = new Dictionary<string, DraftNode>();
        foreach (var n in tx.AddedNodes)
        {
            nodeMap[n.Id] = n;
        }

        // 1. Kiểm tra Read-Only Invariant: Action hoặc Macro output/accumulator không được ghi vào ngõ vào vật lý DI / AI
        foreach (var node in tx.AddedNodes)
        {
            var targetTagsToCheck = new List<string>();

            if (string.Equals(node.NodeType, "ACTION", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(node.TagName) &&
                !string.Equals(node.TagName, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                targetTagsToCheck.Add(node.TagName);
            }
            else if ((string.Equals(node.NodeType, "TIMER", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(node.NodeType, "COUNTER", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(node.NodeType, "SCALE", StringComparison.OrdinalIgnoreCase)) &&
                     !string.IsNullOrWhiteSpace(node.OutputTagName) &&
                     !string.Equals(node.OutputTagName, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                targetTagsToCheck.Add(node.OutputTagName);
            }

            // Counter còn có thanh ghi tích lũy giá trị đếm hiện tại CvTagName (được biến đổi mỗi xung đếm)
            if (string.Equals(node.NodeType, "COUNTER", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(node.CvTagName) &&
                !string.Equals(node.CvTagName, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                targetTagsToCheck.Add(node.CvTagName);
            }

            foreach (var outTag in targetTagsToCheck)
            {
                var targetTag = tagList.FirstOrDefault(t => string.Equals(t.Name, outTag, StringComparison.OrdinalIgnoreCase));
                if (targetTag != null)
                {
                    if (targetTag.Kind == TagKind.DiscreteInput || targetTag.Kind == TagKind.AnalogInput)
                    {
                        return new SafetyValidationResult(
                            false,
                            "SPLC-TAG-ERR-READONLY",
                            $"Vi phạm an toàn phần cứng: Khối '{node.Label ?? node.Id}' không được phép ghi vào ngõ vào vật lý '{outTag}'.",
                            "Các ngõ vào DI/AI chỉ đọc tín hiệu cảm biến từ bên ngoài. Hãy dùng biến cờ trung gian VFLAG, thanh ghi VREG hoặc cuộn ngõ ra DO.");
                    }
                }
            }
        }

        // 2. Kiểm tra GraphGrammarV1: Action không làm nguồn phát dây nối (SPLC-GRAPH-002)
        foreach (var wire in tx.AddedWires)
        {
            if (nodeMap.TryGetValue(wire.SourceNodeId, out var srcNode))
            {
                if (string.Equals(srcNode.NodeType, "ACTION", StringComparison.OrdinalIgnoreCase))
                {
                    return new SafetyValidationResult(
                        false,
                        "SPLC-GRAPH-002",
                        $"Vi phạm cú pháp ECA: Khối Action '{srcNode.Label}' là điểm kết thúc của pipeline, không thể phát dây nối.",
                        "Nếu muốn dùng kết quả của Action để kích hoạt logic tiếp theo, hãy đặt Action ghi vào cờ VFLAG, sau đó tạo một khối Input đọc cờ VFLAG đó.");
                }
            }
        }

        // 3. Kiểm tra Chained Guards (SPLC-GRAPH-007): Không nối 2 Guard liên tiếp
        foreach (var wire in tx.AddedWires)
        {
            if (nodeMap.TryGetValue(wire.SourceNodeId, out var srcNode) &&
                nodeMap.TryGetValue(wire.TargetNodeId, out var tgtNode))
            {
                if (string.Equals(srcNode.NodeType, "GUARD", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(tgtNode.NodeType, "GUARD", StringComparison.OrdinalIgnoreCase))
                {
                    return new SafetyValidationResult(
                        false,
                        "SPLC-GRAPH-007",
                        "Vi phạm quy tắc ECA: Không được nối 2 khối Guard liên tiếp.",
                        "Hãy gom các điều kiện bằng cách sử dụng cờ nhớ trung gian VFLAG trước khi kiểm tra điều kiện tiếp theo.");
                }
            }
        }

        // 4. Kiểm tra Cổng nhận tối đa 1 dây (SPLC-GRAPH-010)
        var inboundCounts = tx.AddedWires.GroupBy(w => $"{w.TargetNodeId}:{w.TargetPort}");
        foreach (var group in inboundCounts)
        {
            if (group.Count() > 1)
            {
                return new SafetyValidationResult(
                    false,
                    "SPLC-GRAPH-010",
                    $"Vi phạm đa cổng kết nối: Cổng nhận '{group.Key}' nhận nhiều hơn 1 dây nối.",
                    "Mỗi cổng nhận tín hiệu chỉ được phép kết nối với duy nhất 1 nguồn phát.");
            }
        }

        // 5. Kiểm tra Write Conflict: Không tạo nhiều Action hoặc Macro cùng ghi vào 1 Tag ngõ ra (SPLC-COMP-DEP-002)
        var writers = new List<(DraftNode Node, string TagName, bool IsMacro)>();
        foreach (var node in tx.AddedNodes)
        {
            if (string.Equals(node.NodeType, "ACTION", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(node.TagName) &&
                !string.Equals(node.TagName, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                writers.Add((node, node.TagName, false));
            }
            else if ((string.Equals(node.NodeType, "TIMER", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(node.NodeType, "COUNTER", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(node.NodeType, "SCALE", StringComparison.OrdinalIgnoreCase)) &&
                     !string.IsNullOrWhiteSpace(node.OutputTagName) &&
                     !string.Equals(node.OutputTagName, "NONE", StringComparison.OrdinalIgnoreCase))
            {
                writers.Add((node, node.OutputTagName, true));
            }
        }

        var tagGroups = writers.GroupBy(w => w.TagName, StringComparer.OrdinalIgnoreCase);
        foreach (var tagGroup in tagGroups)
        {
            bool hasMacro = tagGroup.Any(w => w.IsMacro);
            int count = tagGroup.Count();

            // Nếu có Macro tham gia điều khiển Tag này, Macro yêu cầu độc quyền ngõ ra (không chấp nhận > 1 writer)
            // Nếu thuần túy là Action thông thường, cho phép tối đa 2 khối (mô hình đối xứng SET / RESET)
            if ((hasMacro && count > 1) || count > 2)
            {
                return new SafetyValidationResult(
                    false,
                    "SPLC-COMP-DEP-002",
                    $"Phát hiện xung đột cuộn hút (Write Conflict / Double Coiling): Có {count} khối (Action/Macro) cùng ghi vào Tag '{tagGroup.Key}'.",
                    "Trong PLC, việc nhiều logic cùng ghi vào 1 ngõ ra trong một chu kỳ quét gây hiện tượng bất định. Khối Macro yêu cầu quyền điều khiển độc quyền ngõ ra. Hãy tách thành các cờ trung gian hoặc dùng logic liên động.");
            }
        }

        return new SafetyValidationResult(true, null, null, null);
    }

    /// <summary>
    /// Thẩm định danh sách luật JSON từ LLM trước khi nạp vào hệ thống.
    /// </summary>
    public static SafetyValidationResult Validate(IEnumerable<AiRuleSpecModel> rules, IEnumerable<TagModel> availableTags)
    {
        var tagList = availableTags.ToList();
        var ruleList = rules?.ToList() ?? new List<AiRuleSpecModel>();

        foreach (var rule in ruleList)
        {
            if (string.IsNullOrWhiteSpace(rule.ActionTag) || string.Equals(rule.ActionTag, "NONE", StringComparison.OrdinalIgnoreCase))
                continue;

            var targetTag = tagList.FirstOrDefault(t => string.Equals(t.Name, rule.ActionTag, StringComparison.OrdinalIgnoreCase));
            if (targetTag != null && (targetTag.Kind == TagKind.DiscreteInput || targetTag.Kind == TagKind.AnalogInput))
            {
                return new SafetyValidationResult(
                    false,
                    "SPLC-TAG-ERR-READONLY",
                    $"Vi phạm an toàn phần cứng: Luật '{rule.Narrative}' không được phép ghi vào ngõ vào vật lý '{rule.ActionTag}'.",
                    "Các ngõ vào DI/AI chỉ đọc tín hiệu cảm biến từ bên ngoài. Hãy dùng biến cờ trung gian VFLAG hoặc cuộn ngõ ra DO.");
            }
        }

        var actionTags = ruleList
            .Where(r => !string.IsNullOrWhiteSpace(r.ActionTag) && !string.Equals(r.ActionTag, "NONE", StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => r.ActionTag, StringComparer.OrdinalIgnoreCase);

        foreach (var tagGroup in actionTags)
        {
            if (tagGroup.Count() > 2)
            {
                return new SafetyValidationResult(
                    false,
                    "SPLC-COMP-DEP-002",
                    $"Phát hiện xung đột cuộn hút (Write Conflict / Double Coiling): Có {tagGroup.Count()} luật cùng ghi vào Tag '{tagGroup.Key}'.",
                    "Trong PLC, việc nhiều logic cùng ghi vào 1 ngõ ra trong một chu kỳ quét gây hiện tượng bất định. Hãy tách thành các cờ trung gian hoặc dùng logic liên động.");
            }
        }

        return new SafetyValidationResult(true, null, null, null);
    }
}
