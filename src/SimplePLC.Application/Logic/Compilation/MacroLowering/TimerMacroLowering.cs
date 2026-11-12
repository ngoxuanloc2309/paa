namespace SimplePLC.Application.Logic.Compilation.MacroLowering;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

/// <summary>
/// Hạ cấp (Macro Lowering) các khối Timer (TON, TOF, TP, CYCLE) thành tập luật logic cơ sở.
/// </summary>
public static class TimerMacroLowering
{
    /// <summary>
    /// Thực hiện mở rộng các Timer nodes thành các CandidateRule.
    /// </summary>
    public static void Lower(
        LogicGraph graph,
        ProductDefinition product,
        IReadOnlyDictionary<string, LogicNode> nodeMap,
        IReadOnlyList<LogicNode> timerNodes,
        List<CandidateRule> candidateRules,
        List<CompileDiagnostic> diagnostics)
    {
        foreach (var timerNode in timerNodes)
        {
            var tData = timerNode.TimerData ?? new TimerNodeData();
            ushort resolvedInTagIndex = tData.InTagIndex;
            ushort resolvedQTagIndex = tData.QTagIndex;
            TriggerNodeData? customTrigData = null;
            GuardModel interlockGuard = GuardModel.Empty;

            // Phân giải kết nối ngõ vào của Timer:
            // Hỗ trợ nhận từ Input (Boolean), Trigger (Comparator/Edge), hoặc Guard (Interlock)
            var inEdge = graph.GetIncomingEdges(timerNode.Id).FirstOrDefault();
            if (inEdge != null && nodeMap.TryGetValue(inEdge.SourceNodeId, out var srcNode))
            {
                if (srcNode.Kind == LogicNodeKind.Input && srcNode.InputData != null)
                {
                    resolvedInTagIndex = srcNode.InputData.TagIndex;
                }
                else if (srcNode.Kind == LogicNodeKind.Trigger && srcNode.TriggerData != null)
                {
                    customTrigData = srcNode.TriggerData;
                    var trigInEdge = graph.GetIncomingEdges(srcNode.Id).FirstOrDefault();
                    if (trigInEdge != null && nodeMap.TryGetValue(trigInEdge.SourceNodeId, out var inpNode) && inpNode.Kind == LogicNodeKind.Input && inpNode.InputData != null)
                    {
                        resolvedInTagIndex = inpNode.InputData.TagIndex;
                    }
                }
                else if (srcNode.Kind == LogicNodeKind.Guard && srcNode.GuardData != null)
                {
                    var gTag = product.FindTagByIndex(srcNode.GuardData.TagIndex);
                    if (gTag != null)
                    {
                        interlockGuard = new GuardModel(gTag, srcNode.GuardData.Negated);
                    }

                    // Upstream của Guard là Trigger
                    var guardInEdge = graph.GetIncomingEdges(srcNode.Id).FirstOrDefault();
                    if (guardInEdge != null && nodeMap.TryGetValue(guardInEdge.SourceNodeId, out var trigNode) && trigNode.Kind == LogicNodeKind.Trigger && trigNode.TriggerData != null)
                    {
                        customTrigData = trigNode.TriggerData;
                        var trigInEdge = graph.GetIncomingEdges(trigNode.Id).FirstOrDefault();
                        if (trigInEdge != null && nodeMap.TryGetValue(trigInEdge.SourceNodeId, out var inpNode) && inpNode.Kind == LogicNodeKind.Input && inpNode.InputData != null)
                        {
                            resolvedInTagIndex = inpNode.InputData.TagIndex;
                        }
                    }
                }
            }

            // Nếu Timer node được nối dây sang Action node downstream -> lấy QTagIndex từ Action node
            var outEdge = graph.GetOutgoingEdges(timerNode.Id).FirstOrDefault();
            if (outEdge != null && nodeMap.TryGetValue(outEdge.TargetNodeId, out var tgtNode) && tgtNode.Kind == LogicNodeKind.Action && tgtNode.ActionData != null)
            {
                resolvedQTagIndex = tgtNode.ActionData.TargetTagIndex;
            }

            var inTag = product.FindTagByIndex(resolvedInTagIndex);
            var qTag = product.FindTagByIndex(resolvedQTagIndex);

            var timerLabel = CompilationHelpers.GetNodeLabel(timerNode, "Timer");
            if (inTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    RuleCompiler.ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Timer node '{timerLabel}' references unknown Input Tag {resolvedInTagIndex}.",
                    NodeId: timerNode.Id));
                continue;
            }

            if (qTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    RuleCompiler.ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Timer node '{timerLabel}' references unknown Output Tag {tData.QTagIndex}.",
                    NodeId: timerNode.Id));
                continue;
            }

            string macroId = timerNode.Id;
            string labelPrefix = !string.IsNullOrWhiteSpace(timerNode.Label) ? timerNode.Label : timerNode.Id;

            switch (tData.Type)
            {
                case TimerMacroType.Ton:
                {
                    if (customTrigData != null && customTrigData.CompareOp != CompareOperator.None)
                    {
                        bool cIsFloat = CompilationHelpers.IsFloatThreshold(customTrigData.ThresholdLo, customTrigData.ThresholdHi, inTag);
                        // TON với Comparator Trigger (ví dụ AI0 > 80 duy trì liên tục trong PresetMs -> Q = 1)
                        // R0: Điều kiện đúng duy trì đủ PresetMs -> SET Q = 1
                        var trig0 = new TriggerModel(inTag, customTrigData.Type)
                        {
                            CompareOp = customTrigData.CompareOp,
                            ThresholdLo = customTrigData.ThresholdLo,
                            ThresholdHi = customTrigData.ThresholdHi,
                            IsFloat = cIsFloat,
                            ForMs = tData.PresetMs
                        };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TON", 0, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig0, act0, interlockGuard, $"{labelPrefix}_TON_ON", origin0));

                        // R1: Điều kiện đảo ngược rớt ngưỡng -> tức thời Reset Q = 0
                        var invertedOp = CompilationHelpers.InvertCompareOp(customTrigData.CompareOp);
                        var trig1 = new TriggerModel(inTag, TriggerKind.OnChange)
                        {
                            CompareOp = invertedOp,
                            ThresholdLo = customTrigData.ThresholdLo,
                            ThresholdHi = customTrigData.ThresholdHi,
                            IsFloat = cIsFloat,
                            ForMs = 0
                        };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TON", 1, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig1, act1, GuardModel.Empty, $"{labelPrefix}_TON_OFF", origin1));
                    }
                    else
                    {
                        // R0: IN ON_RISE for PT -> SET Q = 1
                        var trigKind0 = customTrigData?.Type ?? TriggerKind.OnRise;
                        var trig0 = new TriggerModel(inTag, trigKind0) { ForMs = tData.PresetMs };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TON", 0, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig0, act0, interlockGuard, $"{labelPrefix}_TON_ON", origin0));

                        // R1: IN ON_FALL for 0 -> SET Q = 0
                        var trig1 = new TriggerModel(inTag, TriggerKind.OnFall) { ForMs = 0 };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TON", 1, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig1, act1, GuardModel.Empty, $"{labelPrefix}_TON_OFF", origin1));
                    }
                    break;
                }

                case TimerMacroType.Tof:
                {
                    if (customTrigData != null && customTrigData.CompareOp != CompareOperator.None)
                    {
                        bool cIsFloat = CompilationHelpers.IsFloatThreshold(customTrigData.ThresholdLo, customTrigData.ThresholdHi, inTag);
                        // R0: Điều kiện đúng -> SET Q = 1
                        var trig0 = new TriggerModel(inTag, customTrigData.Type)
                        {
                            CompareOp = customTrigData.CompareOp,
                            ThresholdLo = customTrigData.ThresholdLo,
                            ThresholdHi = customTrigData.ThresholdHi,
                            IsFloat = cIsFloat,
                            ForMs = 0
                        };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TOF", 0, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig0, act0, interlockGuard, $"{labelPrefix}_TOF_ON", origin0));

                        // R1: Điều kiện đảo ngược duy trì đủ PresetMs, Guard Q == 1 -> SET Q = 0
                        var invertedOp = CompilationHelpers.InvertCompareOp(customTrigData.CompareOp);
                        var trig1 = new TriggerModel(inTag, TriggerKind.OnChange)
                        {
                            CompareOp = invertedOp,
                            ThresholdLo = customTrigData.ThresholdLo,
                            ThresholdHi = customTrigData.ThresholdHi,
                            IsFloat = cIsFloat,
                            ForMs = tData.PresetMs
                        };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var guard1 = new GuardModel(qTag, negated: false);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TOF", 1, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig1, act1, guard1, $"{labelPrefix}_TOF_OFF", origin1));
                    }
                    else
                    {
                        // R0: IN ON_RISE for 0 -> SET Q = 1
                        var trig0 = new TriggerModel(inTag, TriggerKind.OnRise) { ForMs = 0 };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TOF", 0, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig0, act0, interlockGuard, $"{labelPrefix}_TOF_ON", origin0));

                        // R1: IN ON_FALL for PT, Guard Q == 1 -> SET Q = 0
                        var trig1 = new TriggerModel(inTag, TriggerKind.OnFall) { ForMs = tData.PresetMs };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var guard1 = new GuardModel(qTag, negated: false);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TOF", 1, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig1, act1, guard1, $"{labelPrefix}_TOF_OFF", origin1));
                    }
                    break;
                }

                case TimerMacroType.Tp:
                {
                    if (customTrigData != null && customTrigData.CompareOp != CompareOperator.None)
                    {
                        bool cIsFloat = CompilationHelpers.IsFloatThreshold(customTrigData.ThresholdLo, customTrigData.ThresholdHi, inTag);
                        // R0: Điều kiện đúng, Guard Q == 0 -> SET Q = 1
                        var trig0 = new TriggerModel(inTag, customTrigData.Type)
                        {
                            CompareOp = customTrigData.CompareOp,
                            ThresholdLo = customTrigData.ThresholdLo,
                            ThresholdHi = customTrigData.ThresholdHi,
                            IsFloat = cIsFloat,
                            ForMs = 0
                        };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var guard0 = new GuardModel(qTag, negated: true);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TP", 0, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig0, act0, guard0, $"{labelPrefix}_TP_START", origin0));

                        // R1: Q ON_RISE for PT -> SET Q = 0
                        var trig1 = new TriggerModel(qTag, TriggerKind.OnRise) { ForMs = tData.PresetMs };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TP", 1, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig1, act1, GuardModel.Empty, $"{labelPrefix}_TP_EXPIRE", origin1));
                    }
                    else
                    {
                        // R0: IN ON_RISE for 0, Guard Q == 0 -> SET Q = 1
                        var trig0 = new TriggerModel(inTag, TriggerKind.OnRise) { ForMs = 0 };
                        var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                        var guard0 = new GuardModel(qTag, negated: true);
                        var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TP", 0, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig0, act0, guard0, $"{labelPrefix}_TP_START", origin0));

                        // R1: Q ON_RISE for PT -> SET Q = 0
                        var trig1 = new TriggerModel(qTag, TriggerKind.OnRise) { ForMs = tData.PresetMs };
                        var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                        var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "TP", 1, timerNode.Label);
                        candidateRules.Add(new CandidateRule(timerNode, trig1, act1, GuardModel.Empty, $"{labelPrefix}_TP_EXPIRE", origin1));
                    }
                    break;
                }

                case TimerMacroType.Cycle:
                {
                    uint onTime = tData.OnTimeMs > 0 ? tData.OnTimeMs : (tData.PresetMs > 0 ? tData.PresetMs : 1000);
                    uint offTime = tData.OffTimeMs > 0 ? tData.OffTimeMs : 1000;
                    bool startWithOn = tData.StartWithOn;

                    // R0: Bat dau chu ky khi In len 1
                    var trig0 = new TriggerModel(inTag, TriggerKind.OnRise) { ForMs = startWithOn ? 0 : offTime };
                    var act0 = new ActionModel(qTag, ActionKind.SetTag, 1);
                    var guard0 = new GuardModel(qTag, negated: true);
                    var origin0 = new GeneratedRuleOrigin(timerNode.Id, macroId, "CYCLE", 0, timerNode.Label);
                    candidateRules.Add(new CandidateRule(timerNode, trig0, act0, guard0, $"{labelPrefix}_CYCLE_START", origin0));

                    // R1: Pha BAT het han: Q ON_RISE for OnTime -> SET Q = 0
                    var trig1 = new TriggerModel(qTag, TriggerKind.OnRise) { ForMs = onTime };
                    var act1 = new ActionModel(qTag, ActionKind.SetTag, 0);
                    var origin1 = new GeneratedRuleOrigin(timerNode.Id, macroId, "CYCLE", 1, timerNode.Label);
                    candidateRules.Add(new CandidateRule(timerNode, trig1, act1, GuardModel.Empty, $"{labelPrefix}_CYCLE_ON_EXPIRE", origin1));

                    // R2: Pha NGHI het han: Q ON_FALL for OffTime, Guard In == 1 -> SET Q = 1
                    var trig2 = new TriggerModel(qTag, TriggerKind.OnFall) { ForMs = offTime };
                    var act2 = new ActionModel(qTag, ActionKind.SetTag, 1);
                    var guard2 = new GuardModel(inTag, negated: false);
                    var origin2 = new GeneratedRuleOrigin(timerNode.Id, macroId, "CYCLE", 2, timerNode.Label);
                    candidateRules.Add(new CandidateRule(timerNode, trig2, act2, guard2, $"{labelPrefix}_CYCLE_OFF_EXPIRE", origin2));

                    // R3: Ngat khan cap / Dung an toan khi In ve 0: In ON_FALL for 0 -> SET Q = 0
                    var trig3 = new TriggerModel(inTag, TriggerKind.OnFall) { ForMs = 0 };
                    var act3 = new ActionModel(qTag, ActionKind.SetTag, 0);
                    var origin3 = new GeneratedRuleOrigin(timerNode.Id, macroId, "CYCLE", 3, timerNode.Label);
                    candidateRules.Add(new CandidateRule(timerNode, trig3, act3, GuardModel.Empty, $"{labelPrefix}_CYCLE_STOP", origin3));
                    break;
                }
            }
        }
    }
}
