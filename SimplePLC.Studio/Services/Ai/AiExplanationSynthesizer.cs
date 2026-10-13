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
/// cùng khối JSON Rules đồng bộ hỗ trợ đầy đủ các khối Macro IEC 61131-3 (TON, TOF, TP, CTU, CTD, SCALE).
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

        if (timerNodes.Count > 0 || counterNodes.Count > 0 || scaleNodes.Count > 0)
        {
            foreach (var tm in timerNodes)
            {
                string inTag = ResolveInTag(tm, tx, "DI0");
                string qTag = ResolveOutTag(tm, tx, "DO0");
                int pt = tm.PresetValue > 0 ? tm.PresetValue : 1000;
                sb.AppendLine($"• Khối hẹn giờ IEC {tm.Mode} ({pt}ms): Nhận tín hiệu từ {inTag} → điều khiển ngõ ra {qTag}.");
            }
            foreach (var ct in counterNodes)
            {
                string cuTag = ResolveInTag(ct, tx, "DI0");
                string qTag = ResolveOutTag(ct, tx, "DO0");
                int pv = ct.PresetValue > 0 ? ct.PresetValue : 10;
                sb.AppendLine($"• Khối bộ đếm IEC {ct.Mode} (ngưỡng {pv}): Đếm xung từ {cuTag} → kích hoạt ngõ ra {qTag}.");
            }
            foreach (var sc in scaleNodes)
            {
                string inTag = ResolveInTag(sc, tx, "AI0");
                string outTag = ResolveOutTag(sc, tx, "VREG0");
                sb.AppendLine($"• Khối biến đổi tuyến tính SCALE (Gain={sc.Gain}, Offset={sc.Offset}): Chuyển đổi tín hiệu {inTag} sang {outTag} ({sc.Unit}).");
            }
        }
        else if (ruleChains.Count == 0)
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
            if (!string.IsNullOrWhiteSpace(n.CvTagName) && n.CvTagName != "NONE") allTags.Add(n.CvTagName);
            if (!string.IsNullOrWhiteSpace(n.ResetTagName) && n.ResetTagName != "NONE") allTags.Add(n.ResetTagName);
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
        var jsonRules = GenerateJsonRules(tx, ruleChains);
        if (jsonRules.Count > 0)
        {
            sb.AppendLine("```json:rules");
            var options = new JsonSerializerOptions { WriteIndented = true };
            sb.AppendLine(JsonSerializer.Serialize(jsonRules, options));
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    public static List<AiRuleSpecModel> GenerateJsonRules(DraftGraphTransaction tx)
    {
        if (tx == null) return new List<AiRuleSpecModel>();
        var chains = ExtractRuleChains(tx);
        return GenerateJsonRules(tx, chains);
    }

    public static List<AiRuleSpecModel> GenerateJsonRules(List<SynthesizedRuleChain> chains)
    {
        return GenerateJsonRules(null, chains);
    }

    public static List<AiRuleSpecModel> GenerateJsonRules(DraftGraphTransaction? tx, List<SynthesizedRuleChain> chains)
    {
        var rules = new List<AiRuleSpecModel>();

        // 1. Sinh rule cho các chuỗi ECA thông thường
        if (chains != null)
        {
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
        }

        if (tx == null) return rules;

        // 2. Sinh rule IEC 61131-3 cho Macro Timer (TON, TOF, TP)
        foreach (var tm in tx.AddedNodes.Where(n => IsType(n, "TIMER")))
        {
            string inTag = ResolveInTag(tm, tx, "DI0");
            string qTag = ResolveOutTag(tm, tx, "DO0");
            uint pt = tm.PresetValue > 0 ? (uint)tm.PresetValue : 1000;
            string mode = string.IsNullOrWhiteSpace(tm.Mode) ? "TON" : tm.Mode.ToUpperInvariant();

            switch (mode)
            {
                case "TON":
                {
                    // Rule 0: OnRise / for_ms=PT -> SET Q 1
                    rules.Add(new AiRuleSpecModel
                    {
                        Narrative = $"TON {tm.Id}: Kích hoạt {qTag} sau {pt}ms khi {inTag} ON",
                        InputTag = inTag,
                        Trigger = "ON_RISE",
                        ForMs = pt,
                        CompareOp = "NONE",
                        ThresholdLo = 0,
                        ThresholdHi = 0,
                        GuardTag = "NONE",
                        GuardNegated = false,
                        ActionTag = qTag,
                        ActionType = "SET_TAG",
                        Param = 1
                    });

                    // Rule 1: OnFall / for_ms=0 -> SET Q 0
                    rules.Add(new AiRuleSpecModel
                    {
                        Narrative = $"TON {tm.Id}: Tắt {qTag} tức thời khi {inTag} OFF",
                        InputTag = inTag,
                        Trigger = "ON_FALL",
                        ForMs = 0,
                        CompareOp = "NONE",
                        ThresholdLo = 0,
                        ThresholdHi = 0,
                        GuardTag = "NONE",
                        GuardNegated = false,
                        ActionTag = qTag,
                        ActionType = "SET_TAG",
                        Param = 0
                    });
                    break;
                }

                case "TOF":
                {
                    // Rule 0: OnRise / for_ms=0 -> SET Q 1
                    rules.Add(new AiRuleSpecModel
                    {
                        Narrative = $"TOF {tm.Id}: Bật {qTag} tức thời khi {inTag} ON",
                        InputTag = inTag,
                        Trigger = "ON_RISE",
                        ForMs = 0,
                        CompareOp = "NONE",
                        ThresholdLo = 0,
                        ThresholdHi = 0,
                        GuardTag = "NONE",
                        GuardNegated = false,
                        ActionTag = qTag,
                        ActionType = "SET_TAG",
                        Param = 1
                    });

                    // Rule 1: OnFall / for_ms=PT / Guard Q=1 -> SET Q 0
                    rules.Add(new AiRuleSpecModel
                    {
                        Narrative = $"TOF {tm.Id}: Trì hoãn tắt {qTag} sau {pt}ms khi {inTag} OFF (Khóa Q=1)",
                        InputTag = inTag,
                        Trigger = "ON_FALL",
                        ForMs = pt,
                        CompareOp = "NONE",
                        ThresholdLo = 0,
                        ThresholdHi = 0,
                        GuardTag = qTag,
                        GuardNegated = false,
                        ActionTag = qTag,
                        ActionType = "SET_TAG",
                        Param = 0
                    });
                    break;
                }

                case "TP":
                {
                    // Rule 0: OnRise / Guard NOT Q -> SET Q 1
                    rules.Add(new AiRuleSpecModel
                    {
                        Narrative = $"TP {tm.Id}: Phát xung bật {qTag} khi {inTag} ON (Khóa NOT Q)",
                        InputTag = inTag,
                        Trigger = "ON_RISE",
                        ForMs = 0,
                        CompareOp = "NONE",
                        ThresholdLo = 0,
                        ThresholdHi = 0,
                        GuardTag = qTag,
                        GuardNegated = true,
                        ActionTag = qTag,
                        ActionType = "SET_TAG",
                        Param = 1
                    });

                    // Rule 1: Q OnRise / for_ms=PT -> SET Q 0
                    rules.Add(new AiRuleSpecModel
                    {
                        Narrative = $"TP {tm.Id}: Tự ngắt xung {qTag} sau {pt}ms từ cạnh lên của {qTag}",
                        InputTag = qTag,
                        Trigger = "ON_RISE",
                        ForMs = pt,
                        CompareOp = "NONE",
                        ThresholdLo = 0,
                        ThresholdHi = 0,
                        GuardTag = "NONE",
                        GuardNegated = false,
                        ActionTag = qTag,
                        ActionType = "SET_TAG",
                        Param = 0
                    });
                    break;
                }
            }
        }

        // 3. Sinh rule IEC 61131-3 cho Macro Counter (CTU, CTD)
        foreach (var ct in tx.AddedNodes.Where(n => IsType(n, "COUNTER")))
        {
            string countTag = ResolveInTag(ct, tx, "DI0");
            string cvTag = !string.IsNullOrWhiteSpace(ct.CvTagName) && ct.CvTagName != "NONE" ? ct.CvTagName : "VREG0";
            string? resetTag = ResolveResetTag(ct, tx);
            string qTag = ResolveOutTag(ct, tx, "DO0");
            int pv = ct.PresetValue > 0 ? ct.PresetValue : 10;
            string mode = string.IsNullOrWhiteSpace(ct.Mode) ? "CTU" : ct.Mode.ToUpperInvariant();

            bool hasReset = !string.IsNullOrWhiteSpace(resetTag) && resetTag != "NONE";

            if (mode == "CTD")
            {
                // Rule 0: CD OnRise / Guard NOT Reset -> CV -= 1
                rules.Add(new AiRuleSpecModel
                {
                    Narrative = $"CTD {ct.Id}: Giảm biến đếm {cvTag} khi có xung {countTag}",
                    InputTag = countTag,
                    Trigger = "ON_RISE",
                    ForMs = 0,
                    CompareOp = "NONE",
                    ThresholdLo = 0,
                    ThresholdHi = 0,
                    GuardTag = hasReset ? resetTag! : "NONE",
                    GuardNegated = hasReset,
                    ActionTag = cvTag,
                    ActionType = "INC_COUNTER",
                    Param = -1
                });

                // Rule 1: Nạp lại PV khi Reset ON
                if (hasReset)
                {
                    rules.Add(new AiRuleSpecModel
                    {
                        Narrative = $"CTD {ct.Id}: Nạp lại giá trị đặt {pv} vào {cvTag} khi {resetTag} ON",
                        InputTag = resetTag!,
                        Trigger = "ON_RISE",
                        ForMs = 0,
                        CompareOp = "NONE",
                        ThresholdLo = 0,
                        ThresholdHi = 0,
                        GuardTag = "NONE",
                        GuardNegated = false,
                        ActionTag = cvTag,
                        ActionType = "SET_TAG",
                        Param = pv
                    });
                }

                // Rule 2: CV <= 0 -> SET Q 1
                rules.Add(new AiRuleSpecModel
                {
                    Narrative = $"CTD {ct.Id}: Kích hoạt {qTag} khi {cvTag} <= 0",
                    InputTag = cvTag,
                    Trigger = "ON_CHANGE",
                    CompareOp = "LTE",
                    ThresholdLo = 0,
                    ThresholdHi = 0,
                    ForMs = 0,
                    GuardTag = "NONE",
                    GuardNegated = false,
                    ActionTag = qTag,
                    ActionType = "SET_TAG",
                    Param = 1
                });

                // Rule 3: CV > 0 -> SET Q 0
                rules.Add(new AiRuleSpecModel
                {
                    Narrative = $"CTD {ct.Id}: Ngắt {qTag} khi {cvTag} > 0",
                    InputTag = cvTag,
                    Trigger = "ON_CHANGE",
                    CompareOp = "GT",
                    ThresholdLo = 0,
                    ThresholdHi = 0,
                    ForMs = 0,
                    GuardTag = "NONE",
                    GuardNegated = false,
                    ActionTag = qTag,
                    ActionType = "SET_TAG",
                    Param = 0
                });
            }
            else
            {
                // CTU
                // Rule 0: CU OnRise / Guard NOT Reset -> CV += 1
                rules.Add(new AiRuleSpecModel
                {
                    Narrative = $"CTU {ct.Id}: Tăng biến đếm {cvTag} khi có xung {countTag}",
                    InputTag = countTag,
                    Trigger = "ON_RISE",
                    ForMs = 0,
                    CompareOp = "NONE",
                    ThresholdLo = 0,
                    ThresholdHi = 0,
                    GuardTag = hasReset ? resetTag! : "NONE",
                    GuardNegated = hasReset,
                    ActionTag = cvTag,
                    ActionType = "INC_COUNTER",
                    Param = 1
                });

                // Rule 1: Reset về 0 khi Reset ON
                if (hasReset)
                {
                    rules.Add(new AiRuleSpecModel
                    {
                        Narrative = $"CTU {ct.Id}: Xóa giá trị đếm {cvTag} về 0 khi {resetTag} ON",
                        InputTag = resetTag!,
                        Trigger = "ON_RISE",
                        ForMs = 0,
                        CompareOp = "NONE",
                        ThresholdLo = 0,
                        ThresholdHi = 0,
                        GuardTag = "NONE",
                        GuardNegated = false,
                        ActionTag = cvTag,
                        ActionType = "SET_TAG",
                        Param = 0
                    });
                }

                // Rule 2: CV >= PV -> SET Q 1
                rules.Add(new AiRuleSpecModel
                {
                    Narrative = $"CTU {ct.Id}: Kích hoạt {qTag} khi {cvTag} >= {pv}",
                    InputTag = cvTag,
                    Trigger = "ON_CHANGE",
                    CompareOp = "GTE",
                    ThresholdLo = pv,
                    ThresholdHi = 0,
                    ForMs = 0,
                    GuardTag = "NONE",
                    GuardNegated = false,
                    ActionTag = qTag,
                    ActionType = "SET_TAG",
                    Param = 1
                });

                // Rule 3: CV < PV -> SET Q 0
                rules.Add(new AiRuleSpecModel
                {
                    Narrative = $"CTU {ct.Id}: Ngắt {qTag} khi {cvTag} < {pv}",
                    InputTag = cvTag,
                    Trigger = "ON_CHANGE",
                    CompareOp = "LT",
                    ThresholdLo = pv,
                    ThresholdHi = 0,
                    ForMs = 0,
                    GuardTag = "NONE",
                    GuardNegated = false,
                    ActionTag = qTag,
                    ActionType = "SET_TAG",
                    Param = 0
                });
            }
        }

        // 4. Sinh rule cho Macro Linear Scaler (SCALE)
        foreach (var sc in tx.AddedNodes.Where(n => IsType(n, "SCALE")))
        {
            string inTag = ResolveInTag(sc, tx, "AI0");
            string outTag = ResolveOutTag(sc, tx, "VREG0");
            int scaleParam = (int)Math.Round(sc.Gain * 1000.0);
            if (scaleParam == 0 && Math.Abs(sc.Gain) > 1e-6)
            {
                scaleParam = sc.Gain > 0 ? 1 : -1;
            }

            rules.Add(new AiRuleSpecModel
            {
                Narrative = $"SCALE {sc.Id}: Chuyển đổi tuyến tính {inTag} sang {outTag} (Gain={sc.Gain}, Offset={sc.Offset})",
                InputTag = inTag,
                Trigger = "ON_CHANGE",
                CompareOp = "NONE",
                ThresholdLo = 0,
                ThresholdHi = (int)Math.Round(sc.Offset),
                ForMs = 0,
                GuardTag = "NONE",
                GuardNegated = false,
                ActionTag = outTag,
                ActionType = "SCALE_TAG",
                Param = scaleParam
            });
        }

        return rules;
    }

    private static string ResolveInTag(DraftNode node, DraftGraphTransaction tx, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(node.InputTagName) && node.InputTagName != "NONE")
            return node.InputTagName;
        if (!string.IsNullOrWhiteSpace(node.TagName) && node.TagName != "NONE")
            return node.TagName;

        var inWire = tx.AddedWires.FirstOrDefault(w => w.TargetNodeId == node.Id &&
            (string.Equals(w.TargetPort, "In", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(w.TargetPort, "IN", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(w.TargetPort, "CU", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(w.TargetPort, "CD", StringComparison.OrdinalIgnoreCase)));

        if (inWire != null)
        {
            var src = tx.AddedNodes.FirstOrDefault(n => n.Id == inWire.SourceNodeId);
            if (src != null && !string.IsNullOrWhiteSpace(src.TagName) && src.TagName != "NONE")
                return src.TagName;
        }

        return fallback;
    }

    private static string ResolveOutTag(DraftNode node, DraftGraphTransaction tx, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(node.OutputTagName) && node.OutputTagName != "NONE")
            return node.OutputTagName;

        var outWire = tx.AddedWires.FirstOrDefault(w => w.SourceNodeId == node.Id &&
            (string.Equals(w.SourcePort, "Out", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(w.SourcePort, "OUT", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(w.SourcePort, "Q", StringComparison.OrdinalIgnoreCase)));

        if (outWire != null)
        {
            var tgt = tx.AddedNodes.FirstOrDefault(n => n.Id == outWire.TargetNodeId);
            if (tgt != null && !string.IsNullOrWhiteSpace(tgt.TagName) && tgt.TagName != "NONE")
                return tgt.TagName;
        }

        return fallback;
    }

    private static string? ResolveResetTag(DraftNode ct, DraftGraphTransaction tx)
    {
        if (!string.IsNullOrWhiteSpace(ct.ResetTagName) && ct.ResetTagName != "NONE")
            return ct.ResetTagName;

        var rWire = tx.AddedWires.FirstOrDefault(w => w.TargetNodeId == ct.Id &&
            (string.Equals(w.TargetPort, "R", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(w.TargetPort, "Reset", StringComparison.OrdinalIgnoreCase)));

        if (rWire != null)
        {
            var src = tx.AddedNodes.FirstOrDefault(n => n.Id == rWire.SourceNodeId);
            if (src != null && !string.IsNullOrWhiteSpace(src.TagName) && src.TagName != "NONE")
                return src.TagName;
        }

        return null;
    }

    private static bool IsType(DraftNode node, string expectedType)
    {
        return string.Equals(node.NodeType, expectedType, StringComparison.OrdinalIgnoreCase);
    }

    public class SynthesizedRuleChain
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
                // Lần ngược wire từ Action
                var incomingToAct = tx.AddedWires.FirstOrDefault(w => w.TargetNodeId == act.Id);
                DraftNode? prevNode = incomingToAct != null
                    ? tx.AddedNodes.FirstOrDefault(n => n.Id == incomingToAct.SourceNodeId)
                    : null;

                // Nếu Action được cấp tín hiệu từ một khối Macro (TIMER, COUNTER, SCALE),
                // các rule của Macro đã tự quản lý ngõ ra -> không tạo thêm ECA chain dư thừa
                if (prevNode != null && (IsType(prevNode, "TIMER") || IsType(prevNode, "COUNTER") || IsType(prevNode, "SCALE")))
                {
                    continue;
                }

                var chain = new SynthesizedRuleChain
                {
                    ActionTag = string.IsNullOrWhiteSpace(act.TagName) || act.TagName == "NONE" ? "DO0" : act.TagName,
                    ActionType = string.IsNullOrWhiteSpace(act.ActionType) || act.ActionType == "NONE" ? "SET_TAG" : act.ActionType,
                    ActionParam = act.ActionParam
                };

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
            string.Equals(n.OutputTagName, tag, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.CvTagName, tag, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(n.ResetTagName, tag, StringComparison.OrdinalIgnoreCase));

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
}
