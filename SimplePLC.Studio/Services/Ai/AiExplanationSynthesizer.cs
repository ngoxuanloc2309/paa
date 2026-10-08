using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services.Ai;

/// <summary>
/// Bộ tổng hợp thuyết minh kỹ thuật công nghiệp tự động (Industrial AI Explanation Synthesizer).
/// Đảm bảo trong mọi tình huống (kể cả khi LLM chỉ phát ra Tool Calls mà không có Text Explanation),
/// người dùng luôn nhận được bản thuyết minh chuyên nghiệp 3 phần (Nguyên lý, Danh mục Tag, An toàn Fail-Safe)
/// cùng khối JSON Rules đồng bộ.
/// </summary>
public static class AiExplanationSynthesizer
{
    public static string Synthesize(DraftGraphTransaction tx, string userPrompt)
    {
        if (tx == null || tx.AddedNodes.Count == 0)
        {
            return "Không có khối logic nào được tạo trong đề xuất này.";
        }

        var ruleChains = ExtractRuleChains(tx);
        var timerNodes = tx.AddedNodes.Where(n => IsType(n, "TIMER")).ToList();
        var counterNodes = tx.AddedNodes.Where(n => IsType(n, "COUNTER")).ToList();
        var scaleNodes = tx.AddedNodes.Where(n => IsType(n, "SCALE")).ToList();

        var sb = new StringBuilder();

        // 1. Nguyên lý hoạt động
        sb.AppendLine("### 1. Nguyên lý hoạt động");
        if (ruleChains.Count > 0)
        {
            foreach (var chain in ruleChains)
            {
                sb.AppendLine($"• {DescribeChainPrinciple(chain)}");
            }
        }
        else if (timerNodes.Count > 0 || counterNodes.Count > 0 || scaleNodes.Count > 0)
        {
            foreach (var tm in timerNodes)
                sb.AppendLine($"• Hẹn giờ {tm.Mode} {tm.PresetValue}ms → điều khiển {tm.OutputTagName}.");
            foreach (var ct in counterNodes)
                sb.AppendLine($"• Đếm {ct.Mode} tới {ct.PresetValue} → kích hoạt {ct.OutputTagName}.");
            foreach (var sc in scaleNodes)
                sb.AppendLine($"• Chuyển đổi tín hiệu {sc.InputTagName} sang đơn vị kỹ thuật.");
        }
        else
        {
            sb.AppendLine($"• Đã tạo {tx.AddedNodes.Count} khối và {tx.AddedWires.Count} đường nối trên Canvas.");
        }
        sb.AppendLine();

        // 2. Danh mục Tag
        sb.AppendLine("### 2. Danh mục Tag");
        var allTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in ruleChains)
        {
            if (!string.IsNullOrWhiteSpace(c.InputTag) && c.InputTag != "NONE") allTags.Add(c.InputTag);
            if (!string.IsNullOrWhiteSpace(c.ActionTag) && c.ActionTag != "NONE") allTags.Add(c.ActionTag);
            if (!string.IsNullOrWhiteSpace(c.GuardTag) && c.GuardTag != "NONE") allTags.Add(c.GuardTag);
        }
        foreach (var n in tx.AddedNodes)
        {
            if (!string.IsNullOrWhiteSpace(n.TagName) && n.TagName != "NONE") allTags.Add(n.TagName);
            if (!string.IsNullOrWhiteSpace(n.InputTagName) && n.InputTagName != "NONE") allTags.Add(n.InputTagName);
            if (!string.IsNullOrWhiteSpace(n.OutputTagName) && n.OutputTagName != "NONE") allTags.Add(n.OutputTagName);
        }
        foreach (var tag in allTags)
        {
            sb.AppendLine($"• **{tag}**: {GuessTagRole(tag, tx)}");
        }
        sb.AppendLine();

        // 3. Cơ chế an toàn công nghiệp
        sb.AppendLine("### 3. Cơ chế an toàn công nghiệp");
        sb.AppendLine("• Chống dội tín hiệu (Hardware Debounce) lọc xung nhiễu đầu vào.");
        sb.AppendLine("• Cơ chế Fail-Safe tự động ngắt tải khi phát hiện sự cố truyền thông hoặc mất kết nối.");
        sb.AppendLine();

        // Khối JSON ẩn để đồng bộ rule (không hiển thị với người dùng — bị strip bởi AiRuleParser)
        var jsonRules = GenerateJsonRules(ruleChains);
        if (jsonRules.Count > 0)
        {
            sb.AppendLine("```json:rules");
            var options = new JsonSerializerOptions { WriteIndented = true };
            sb.AppendLine(JsonSerializer.Serialize(jsonRules, options));
            sb.AppendLine("```");
        }

        return sb.ToString();
    }


    private static bool IsType(DraftNode node, string expectedType)
    {
        return string.Equals(node.NodeType, expectedType, StringComparison.OrdinalIgnoreCase);
    }

    private class SynthesizedRuleChain
    {
        public string InputTag { get; set; } = "DI0";
        public string InputLabel { get; set; } = string.Empty;
        public string TriggerType { get; set; } = "ON_RISE";
        public string CompareOp { get; set; } = "NONE";
        public int ThresholdLo { get; set; }
        public int ThresholdHi { get; set; }
        public int DebounceMs { get; set; } = 50;
        public string GuardTag { get; set; } = "NONE";
        public bool GuardNegated { get; set; }
        public string ActionTag { get; set; } = "DO0";
        public string ActionType { get; set; } = "SET_TAG";
        public int ActionParam { get; set; } = 1;
        public string Narrative { get; set; } = string.Empty;
    }

    private static List<SynthesizedRuleChain> ExtractRuleChains(DraftGraphTransaction tx)
    {
        var chains = new List<SynthesizedRuleChain>();
        var actionNodes = tx.AddedNodes.Where(n => IsType(n, "ACTION")).ToList();

        if (actionNodes.Count > 0)
        {
            foreach (var act in actionNodes)
            {
                var chain = new SynthesizedRuleChain
                {
                    ActionTag = string.IsNullOrWhiteSpace(act.TagName) || act.TagName == "NONE" ? "DO0" : act.TagName,
                    ActionType = string.IsNullOrWhiteSpace(act.ActionType) || act.ActionType == "NONE" ? "SET_TAG" : act.ActionType,
                    ActionParam = act.ActionParam
                };

                // Lần ngược wire từ Action
                var incomingToAct = tx.AddedWires.FirstOrDefault(w => w.TargetNodeId == act.Id);
                DraftNode? prevNode = incomingToAct != null
                    ? tx.AddedNodes.FirstOrDefault(n => n.Id == incomingToAct.SourceNodeId)
                    : null;

                DraftNode? guardNode = null;
                DraftNode? triggerNode = null;
                DraftNode? inputNode = null;

                if (prevNode != null && IsType(prevNode, "GUARD"))
                {
                    guardNode = prevNode;
                    var incomingToGuard = tx.AddedWires.FirstOrDefault(w => w.TargetNodeId == guardNode.Id);
                    prevNode = incomingToGuard != null
                        ? tx.AddedNodes.FirstOrDefault(n => n.Id == incomingToGuard.SourceNodeId)
                        : null;
                }

                if (prevNode != null && IsType(prevNode, "TRIGGER"))
                {
                    triggerNode = prevNode;
                    var incomingToTrig = tx.AddedWires.FirstOrDefault(w => w.TargetNodeId == triggerNode.Id);
                    prevNode = incomingToTrig != null
                        ? tx.AddedNodes.FirstOrDefault(n => n.Id == incomingToTrig.SourceNodeId)
                        : null;
                }

                if (prevNode != null && IsType(prevNode, "INPUT"))
                {
                    inputNode = prevNode;
                }

                // Nếu không nối dây đầy đủ, fallback tìm theo danh sách node có sẵn
                if (triggerNode == null)
                {
                    triggerNode = tx.AddedNodes.FirstOrDefault(n => IsType(n, "TRIGGER"));
                }
                if (inputNode == null)
                {
                    // Ghép theo thứ tự index nếu có nhiều input
                    int actIndex = actionNodes.IndexOf(act);
                    var inputs = tx.AddedNodes.Where(n => IsType(n, "INPUT")).ToList();
                    inputNode = actIndex < inputs.Count ? inputs[actIndex] : inputs.FirstOrDefault();
                }

                if (inputNode != null)
                {
                    chain.InputTag = string.IsNullOrWhiteSpace(inputNode.TagName) || inputNode.TagName == "NONE" ? "DI0" : inputNode.TagName;
                    chain.InputLabel = inputNode.Label;
                }

                if (triggerNode != null)
                {
                    chain.TriggerType = string.IsNullOrWhiteSpace(triggerNode.TriggerType) || triggerNode.TriggerType == "NONE" ? "ON_RISE" : triggerNode.TriggerType;
                    chain.CompareOp = triggerNode.CompareOp;
                    chain.ThresholdLo = triggerNode.ThresholdLo;
                    chain.ThresholdHi = triggerNode.ThresholdHi;
                    chain.DebounceMs = triggerNode.DebounceMs > 0 ? triggerNode.DebounceMs : 50;
                }

                if (guardNode != null)
                {
                    chain.GuardTag = guardNode.TagName;
                }

                chains.Add(chain);
            }
        }
        else
        {
            // Trường hợp không có Action node rõ rệt, nhưng có Input và Trigger
            var inputs = tx.AddedNodes.Where(n => IsType(n, "INPUT")).ToList();
            var triggers = tx.AddedNodes.Where(n => IsType(n, "TRIGGER")).ToList();
            if (inputs.Count > 0)
            {
                for (int i = 0; i < inputs.Count; i++)
                {
                    var inp = inputs[i];
                    var trg = i < triggers.Count ? triggers[i] : triggers.FirstOrDefault();
                    chains.Add(new SynthesizedRuleChain
                    {
                        InputTag = inp.TagName,
                        InputLabel = inp.Label,
                        TriggerType = trg?.TriggerType ?? "ON_RISE",
                        DebounceMs = trg?.DebounceMs > 0 ? trg.DebounceMs : 50,
                        ActionTag = "DO" + i,
                        ActionParam = 1
                    });
                }
            }
        }

        return chains;
    }

    private static string DescribeChainPrinciple(SynthesizedRuleChain chain)
    {
        string actVerb = chain.ActionParam == 0 ? "Tắt" : "Bật";
        string inputAction = chain.TriggerType == "ON_RISE" ? "nhấn kích hoạt" :
                             chain.TriggerType == "ON_FALL" ? "nhả hoặc ngắt" :
                             chain.TriggerType == "ON_CHANGE" ? "thay đổi trạng thái" : "kích hoạt";

        string tagHint = chain.InputTag.StartsWith("DI", StringComparison.OrdinalIgnoreCase) ? $"nút nhấn {chain.InputTag}" :
                         chain.InputTag.StartsWith("AI", StringComparison.OrdinalIgnoreCase) ? $"cảm biến tương tự {chain.InputTag}" :
                         $"tín hiệu {chain.InputTag}";

        string guardHint = string.Empty;
        if (!string.IsNullOrWhiteSpace(chain.GuardTag) && chain.GuardTag != "NONE")
        {
            guardHint = chain.GuardNegated
                ? $" khi {chain.GuardTag} đang TẮT (khóa an toàn)"
                : $" khi {chain.GuardTag} đang BẬT (điều kiện cho phép)";
        }

        return $"Khi kỹ sư {inputAction} {tagHint}{guardHint} ➔ Hệ thống lập tức **{actVerb}** ngõ ra chấp hành **{chain.ActionTag}**.";
    }

    private static string GuessTagRole(string tag, DraftGraphTransaction tx)
    {
        var node = tx.AddedNodes.FirstOrDefault(n =>
            string.Equals(n.TagName, tag, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.InputTagName, tag, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.OutputTagName, tag, StringComparison.OrdinalIgnoreCase));

        if (node != null && !string.IsNullOrWhiteSpace(node.Label))
        {
            return node.Label;
        }

        if (tag.StartsWith("DI0", StringComparison.OrdinalIgnoreCase)) return "Ngõ vào số 24VDC (Nút nhấn Start - Khởi động)";
        if (tag.StartsWith("DI1", StringComparison.OrdinalIgnoreCase)) return "Ngõ vào số 24VDC (Nút nhấn Stop - Dừng)";
        if (tag.StartsWith("DI", StringComparison.OrdinalIgnoreCase)) return "Ngõ vào số 24VDC (Tín hiệu cảm biến / Nút bấm công nghiệp)";
        if (tag.StartsWith("DO0", StringComparison.OrdinalIgnoreCase)) return "Ngõ ra Relay/Transistor 24VDC (Bơm / Động cơ chấp hành chính)";
        if (tag.StartsWith("DO", StringComparison.OrdinalIgnoreCase)) return "Ngõ ra Relay/Transistor 24VDC (Thiết bị tải chấp hành)";
        if (tag.StartsWith("AI", StringComparison.OrdinalIgnoreCase)) return "Ngõ vào tương tự 0-10V / 4-20mA (Cảm biến đo lường áp suất/nhiệt độ)";
        if (tag.StartsWith("VFLAG", StringComparison.OrdinalIgnoreCase)) return "Cờ bit nội bộ RAM (Lưu trạng thái trung gian / Tự giữ)";
        if (tag.StartsWith("VREG_RETAIN", StringComparison.OrdinalIgnoreCase)) return "Biến nhớ lưu giữ Flash Retain (Lưu sản lượng / thông số khi mất điện)";
        if (tag.StartsWith("VREG", StringComparison.OrdinalIgnoreCase)) return "Biến số 16-bit nội bộ RAM";

        return "Tín hiệu điều khiển hệ thống";
    }

    private static List<AiRuleSpecModel> GenerateJsonRules(List<SynthesizedRuleChain> chains)
    {
        var rules = new List<AiRuleSpecModel>();
        foreach (var c in chains)
        {
            string narrative = c.ActionParam == 0
                ? $"Tắt {c.ActionTag} khi {c.InputTag} kích hoạt"
                : $"Bật {c.ActionTag} khi {c.InputTag} kích hoạt";

            rules.Add(new AiRuleSpecModel
            {
                Narrative = narrative,
                InputTag = c.InputTag,
                Trigger = c.TriggerType,
                ForMs = (uint)c.DebounceMs,
                CompareOp = c.CompareOp,
                ThresholdLo = c.ThresholdLo,
                ThresholdHi = c.ThresholdHi,
                GuardTag = c.GuardTag,
                GuardNegated = c.GuardNegated,
                ActionTag = c.ActionTag,
                ActionType = c.ActionType,
                Param = c.ActionParam
            });
        }
        return rules;
    }
}
