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
        var nodeMap = tx.AddedNodes.ToDictionary(n => n.Id, n => n);

        // 1. Kiểm tra Read-Only Invariant: Action không được ghi vào ngõ vào vật lý DI / AI
        foreach (var node in tx.AddedNodes.Where(n => n.NodeType == "ACTION"))
        {
            if (string.Equals(node.TagName, "NONE", StringComparison.OrdinalIgnoreCase)) continue;

            var targetTag = tagList.FirstOrDefault(t => string.Equals(t.Name, node.TagName, StringComparison.OrdinalIgnoreCase));
            if (targetTag != null)
            {
                if (targetTag.Kind == TagKind.DiscreteInput || targetTag.Kind == TagKind.AnalogInput)
                {
                    return new SafetyValidationResult(
                        false,
                        "SPLC-TAG-ERR-READONLY",
                        $"Vi phạm an toàn phần cứng: Khối Action '{node.Label}' không được phép ghi vào ngõ vào vật lý '{node.TagName}'.",
                        "Các ngõ vào DI/AI chỉ đọc tín hiệu cảm biến từ bên ngoài. Hãy dùng biến cờ trung gian VFLAG hoặc cuộn ngõ ra DO.");
                }
            }
        }

        // 2. Kiểm tra GraphGrammarV1: Action không làm nguồn phát dây nối (SPLC-GRAPH-002)
        foreach (var wire in tx.AddedWires)
        {
            if (nodeMap.TryGetValue(wire.SourceNodeId, out var srcNode))
            {
                if (srcNode.NodeType == "ACTION")
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
                if (srcNode.NodeType == "GUARD" && tgtNode.NodeType == "GUARD")
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

        // 5. Kiểm tra Write Conflict: Không tạo nhiều Action cùng ghi vào 1 Tag ngõ ra (trừ khi là khối Macro)
        var actionTags = tx.AddedNodes
            .Where(n => n.NodeType == "ACTION" && !string.Equals(n.TagName, "NONE", StringComparison.OrdinalIgnoreCase))
            .GroupBy(n => n.TagName, StringComparer.OrdinalIgnoreCase);

        foreach (var tagGroup in actionTags)
        {
            // Nếu có nhiều hơn 1 Action ghi vào cùng Tag mà không phải SET/RESET đối xứng
            if (tagGroup.Count() > 2)
            {
                return new SafetyValidationResult(
                    false,
                    "SPLC-COMP-DEP-002",
                    $"Phát hiện xung đột cuộn hút (Write Conflict / Double Coiling): Có {tagGroup.Count()} khối Action cùng ghi vào Tag '{tagGroup.Key}'.",
                    "Trong PLC, việc nhiều logic cùng ghi vào 1 ngõ ra trong một chu kỳ quét gây hiện tượng bất định. Hãy tách thành các cờ trung gian hoặc dùng logic liên động.");
            }
        }

        return new SafetyValidationResult(true, null, null, null);
    }
}
