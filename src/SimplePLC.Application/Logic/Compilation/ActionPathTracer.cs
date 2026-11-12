namespace SimplePLC.Application.Logic.Compilation;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

/// <summary>
/// Truy vết và ánh xạ đường đi từ các node Action ngược dòng qua Guard, Trigger, Comparator, Input thành các CandidateRule.
/// </summary>
public static class ActionPathTracer
{
    /// <summary>
    /// Duyệt tất cả các Action node trong đồ thị để truy vết các chuỗi điều kiện kích hoạt.
    /// </summary>
    public static void Trace(
        LogicGraph graph,
        ProductDefinition product,
        IReadOnlyDictionary<string, LogicNode> nodeMap,
        IReadOnlyList<LogicNode> actionNodes,
        List<CandidateRule> candidateRules,
        List<CompileDiagnostic> diagnostics)
    {
        foreach (var actionNode in actionNodes)
        {
            var actionIncoming = graph.GetIncomingEdges(actionNode.Id).FirstOrDefault();
            if (actionIncoming == null || !nodeMap.TryGetValue(actionIncoming.SourceNodeId, out var directSource))
            {
                var actionLabel = CompilationHelpers.GetNodeLabel(actionNode, "Action");
                diagnostics.Add(new CompileDiagnostic(
                    RuleCompiler.ErrActionNoIncoming,
                    DiagnosticSeverity.Error,
                    $"Action node '{actionLabel}' is not connected to a Trigger or Guard.",
                    NodeId: actionNode.Id));
                continue;
            }

            if (directSource.Kind is LogicNodeKind.Counter or LogicNodeKind.Timer or LogicNodeKind.Scale or LogicNodeKind.MqttPublish or LogicNodeKind.MqttSubscribe or LogicNodeKind.ModbusRead or LogicNodeKind.ModbusWrite or LogicNodeKind.DirectWrite or LogicNodeKind.HttpClient or LogicNodeKind.TcpClient)
            {
                // Action node connects from Counter, Timer, Scale, MQTT, Modbus, or Direct Write port as visual destination.
                // The rules are fully expanded in Macro Expansion or handled by communication subsystems.
                continue;
            }

            LogicNode? triggerNode = null;
            LogicNode? guardNode = null;
            LogicNode? directComparatorNode = null;
            LogicNode? directInputNode = null;
            ushort? resolvedDirectTagIndex = null;

            if (directSource.Kind == LogicNodeKind.Input)
            {
                directInputNode = directSource;
            }
            else if (directSource.Kind == LogicNodeKind.Trigger)
            {
                triggerNode = directSource;
            }
            else if (directSource.Kind == LogicNodeKind.Comparator)
            {
                directComparatorNode = directSource;
            }
            else if (directSource.Kind == LogicNodeKind.Guard)
            {
                guardNode = directSource;
                var guardIncoming = graph.GetIncomingEdges(guardNode.Id).FirstOrDefault();
                if (guardIncoming == null || !nodeMap.TryGetValue(guardIncoming.SourceNodeId, out var guardSource))
                {
                    var guardLabel = CompilationHelpers.GetNodeLabel(guardNode, "Guard");
                    diagnostics.Add(new CompileDiagnostic(
                        RuleCompiler.ErrGuardNoTrigger,
                        DiagnosticSeverity.Error,
                        $"Guard node '{guardLabel}' is not connected to an upstream Trigger, Comparator, or Input.",
                        NodeId: guardNode.Id));
                    continue;
                }

                if (guardSource.Kind == LogicNodeKind.Trigger)
                {
                    triggerNode = guardSource;
                }
                else if (guardSource.Kind == LogicNodeKind.Comparator)
                {
                    directComparatorNode = guardSource;
                }
                else if (guardSource.Kind == LogicNodeKind.Input)
                {
                    directInputNode = guardSource;
                }
                else
                {
                    var grdSrcTag = CompilationHelpers.ResolveSourcePortTagIndex(guardSource, guardIncoming.SourcePort);
                    if (grdSrcTag.HasValue)
                    {
                        resolvedDirectTagIndex = grdSrcTag.Value;
                    }
                    else
                    {
                        var guardLabel2 = CompilationHelpers.GetNodeLabel(guardNode, "Guard");
                        diagnostics.Add(new CompileDiagnostic(
                            RuleCompiler.ErrGuardNoTrigger,
                            DiagnosticSeverity.Error,
                            $"Guard node '{guardLabel2}' must be connected to an upstream Trigger, Comparator, or Input.",
                            NodeId: guardNode.Id));
                        continue;
                    }
                }
            }
            else
            {
                var actionLabel2 = CompilationHelpers.GetNodeLabel(actionNode, "Action");
                diagnostics.Add(new CompileDiagnostic(
                    RuleCompiler.ErrActionNoIncoming,
                    DiagnosticSeverity.Error,
                    $"Action node '{actionLabel2}' must be fed by Input, Trigger, Comparator, or Guard, but got {directSource.Kind}.",
                    NodeId: actionNode.Id));
                continue;
            }

            // Trace Trigger upstream input (or via Comparator or direct Input)
            LogicNode? inputNode = directInputNode;
            LogicNode? comparatorNode = directComparatorNode;
            ushort? resolvedTriggerTagIndex = resolvedDirectTagIndex ?? directInputNode?.InputData?.TagIndex;

            if (triggerNode != null)
            {
                if (triggerNode.TriggerData?.InputTagIndex != null)
                {
                    resolvedTriggerTagIndex = triggerNode.TriggerData.InputTagIndex;
                }

                var triggerIncoming = graph.GetIncomingEdges(triggerNode.Id).FirstOrDefault();
                if (triggerIncoming != null && nodeMap.TryGetValue(triggerIncoming.SourceNodeId, out var candidateSrc))
                {
                    if (candidateSrc.Kind == LogicNodeKind.Input)
                    {
                        inputNode = candidateSrc;
                        resolvedTriggerTagIndex = candidateSrc.InputData?.TagIndex;
                    }
                    else if (candidateSrc.Kind == LogicNodeKind.Comparator)
                    {
                        comparatorNode = candidateSrc;
                        if (comparatorNode.ComparatorData != null)
                        {
                            resolvedTriggerTagIndex = comparatorNode.ComparatorData.InTagIndex;
                        }
                        var compIncoming = graph.GetIncomingEdges(comparatorNode.Id).FirstOrDefault();
                        if (compIncoming != null && nodeMap.TryGetValue(compIncoming.SourceNodeId, out var compSrc))
                        {
                            var compTag = CompilationHelpers.ResolveSourcePortTagIndex(compSrc, compIncoming.SourcePort);
                            if (compTag.HasValue)
                            {
                                resolvedTriggerTagIndex = compTag.Value;
                            }
                        }
                    }
                    else
                    {
                        var srcTag = CompilationHelpers.ResolveSourcePortTagIndex(candidateSrc, triggerIncoming.SourcePort);
                        if (srcTag.HasValue)
                        {
                            resolvedTriggerTagIndex = srcTag.Value;
                        }
                    }
                }
            }
            else if (directComparatorNode != null)
            {
                if (directComparatorNode.ComparatorData != null)
                {
                    resolvedTriggerTagIndex = directComparatorNode.ComparatorData.InTagIndex;
                }
                var compIncoming = graph.GetIncomingEdges(directComparatorNode.Id).FirstOrDefault();
                if (compIncoming != null && nodeMap.TryGetValue(compIncoming.SourceNodeId, out var compSrc))
                {
                    var compTag = CompilationHelpers.ResolveSourcePortTagIndex(compSrc, compIncoming.SourcePort);
                    if (compTag.HasValue)
                    {
                        resolvedTriggerTagIndex = compTag.Value;
                    }
                }
            }

            var triggerData = triggerNode?.TriggerData ?? (directInputNode != null || resolvedTriggerTagIndex.HasValue
                ? new TriggerNodeData { Type = TriggerKind.OnRise }
                : new TriggerNodeData { Type = TriggerKind.OnChange });
            bool requiresInput = triggerData.Type is not (TriggerKind.Interval or TriggerKind.TimeWindow);

            if (requiresInput && !resolvedTriggerTagIndex.HasValue)
            {
                string missingNodeId = triggerNode?.Id ?? directComparatorNode?.Id ?? actionNode.Id;
                string missingNodeLabel = CompilationHelpers.GetNodeLabel(triggerNode, CompilationHelpers.GetNodeLabel(directComparatorNode, "Trigger"));
                diagnostics.Add(new CompileDiagnostic(
                    RuleCompiler.ErrTriggerNoInput,
                    DiagnosticSeverity.Error,
                    $"Trigger/Comparator node '{missingNodeLabel}' requires an upstream Input Tag.",
                    NodeId: missingNodeId,
                    FieldName: "InputTag"));
                continue;
            }

            // Resolve Trigger Tag
            ushort triggerTagIndex = resolvedTriggerTagIndex ?? (comparatorNode?.ComparatorData?.InTagIndex ?? inputNode?.InputData?.TagIndex ?? 0);
            var triggerTag = product.FindTagByIndex(triggerTagIndex);
            if (triggerTag == null)
            {
                if (requiresInput)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        RuleCompiler.ErrTagNotFound,
                        DiagnosticSeverity.Error,
                        $"Trigger Tag index {triggerTagIndex} not found in device profile.",
                        NodeId: inputNode?.Id ?? comparatorNode?.Id ?? triggerNode?.Id ?? actionNode.Id));
                    continue;
                }
                // Fallback virtual/placeholder for interval/timewindow
                triggerTag = new TagDefinition(0, "NONE", TagKind.None, TagDataType.Boolean, isReadOnly: true);
            }

            // Fuse Comparator parameters into TriggerModel if present
            var compareOp = comparatorNode?.ComparatorData?.Operator ?? triggerData.CompareOp;
            var thresholdLo = comparatorNode?.ComparatorData != null ? comparatorNode.ComparatorData.ThresholdLo : triggerData.ThresholdLo;
            var thresholdHi = comparatorNode?.ComparatorData != null ? comparatorNode.ComparatorData.ThresholdHi : triggerData.ThresholdHi;

            var triggerModel = new TriggerModel(triggerTag, triggerData.Type)
            {
                CompareOp = compareOp,
                ThresholdLo = thresholdLo,
                ThresholdHi = thresholdHi,
                IsFloat = CompilationHelpers.IsFloatThreshold(thresholdLo, thresholdHi, triggerTag),
                ForMs = triggerData.ForMs
            };

            // Resolve Guard Tag (if guard exists)
            GuardModel guardModel = GuardModel.Empty;
            if (guardNode != null)
            {
                var guardData = guardNode.GuardData ?? new GuardNodeData();
                var guardTag = product.FindTagByIndex(guardData.TagIndex);
                if (guardTag == null)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        RuleCompiler.ErrTagNotFound,
                        DiagnosticSeverity.Error,
                        $"Guard Tag index {guardData.TagIndex} not found in device profile.",
                        NodeId: guardNode.Id));
                    continue;
                }

                if (guardTag.DataType != TagDataType.Boolean)
                {
                    diagnostics.Add(new CompileDiagnostic(
                        RuleCompiler.ErrGuardNotBoolean,
                        DiagnosticSeverity.Error,
                        $"Guard Tag '{guardTag.Name}' must be Boolean, but found {guardTag.DataType}.",
                        NodeId: guardNode.Id));
                    continue;
                }

                guardModel = new GuardModel(guardTag, guardData.Negated);
            }

            // Resolve Action Tag
            var actionData = actionNode.ActionData ?? new ActionNodeData();
            var actionTag = product.FindTagByIndex(actionData.TargetTagIndex);
            if (actionTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    RuleCompiler.ErrTagNotFound,
                    DiagnosticSeverity.Error,
                    $"Action Target Tag index {actionData.TargetTagIndex} not found in device profile.",
                    NodeId: actionNode.Id));
                continue;
            }

            if (actionTag.IsReadOnly)
            {
                var actLabel = CompilationHelpers.GetNodeLabel(actionNode, "Action");
                diagnostics.Add(new CompileDiagnostic(
                    RuleCompiler.ErrActionReadOnlyTarget,
                    DiagnosticSeverity.Error,
                    $"Action node '{actLabel}' cannot write to read-only tag '{actionTag.Name}'.",
                    NodeId: actionNode.Id));
                continue;
            }

            var actionModel = new ActionModel(actionTag, actionData.Type, actionData.Parameter);

            string ruleName = !string.IsNullOrWhiteSpace(actionNode.Label) ? actionNode.Label : string.Empty;
            candidateRules.Add(new CandidateRule(actionNode, triggerModel, actionModel, guardModel, ruleName, null));
        }
    }
}
