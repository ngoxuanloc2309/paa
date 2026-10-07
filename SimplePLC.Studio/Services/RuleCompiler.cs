using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Services;

public interface IRuleCompiler
{
    SimplePLC.Studio.Models.CompileResult Compile(
        IEnumerable<GraphNodeViewModel> nodes,
        IEnumerable<ConnectionViewModel> connections,
        TagCatalogViewModel tagCatalog,
        ProductDefinition? product = null);
}

public sealed class RuleCompiler : IRuleCompiler
{
    private readonly SimplePLC.Application.Logic.Compilation.RuleCompiler _coreCompiler = new();

    public SimplePLC.Studio.Models.CompileResult Compile(
        IEnumerable<GraphNodeViewModel> nodes,
        IEnumerable<ConnectionViewModel> connections,
        TagCatalogViewModel tagCatalog,
        ProductDefinition? product = null)
    {
        var targetProduct = product ?? AppServices.Instance.CurrentProduct ?? ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var graph = new LogicGraph();

        var nodeList = nodes.ToList();
        var connectionList = connections.ToList();
        var studioDiagnostics = new List<SimplePLC.Studio.Models.Diagnostic>();

        // 1. Build POCO Graph Nodes
        int actionOrder = 0;
        foreach (var node in nodeList)
        {
            switch (node)
            {
                case InputNodeViewModel inp:
                    ushort inTagIdx = (ushort)(inp.Tag != null && inp.Tag.Kind != TagKind.None ? inp.Tag.Index : 0);
                    graph.Nodes.Add(LogicNode.CreateInput(inp.Id, inTagIdx, inp.CustomLabel));
                    break;

                case TriggerNodeViewModel trig:
                    graph.Nodes.Add(LogicNode.CreateTrigger(
                        trig.Id,
                        (TriggerKind)trig.TriggerType,
                        (CompareOperator)trig.CompareOp,
                        trig.ThresholdLo,
                        trig.ThresholdHi,
                        trig.ForMs,
                        trig.CustomLabel));
                    break;

                case GuardNodeViewModel grd:
                    ushort grdTagIdx = (ushort)(grd.GuardTag != null && grd.GuardTag.Kind != TagKind.None ? grd.GuardTag.Index : 0);
                    graph.Nodes.Add(LogicNode.CreateGuard(grd.Id, grdTagIdx, grd.Negate, grd.CustomLabel));
                    break;

                case ActionNodeViewModel act:
                    ushort actTagIdx = (ushort)(act.TargetTag != null && act.TargetTag.Kind != TagKind.None ? act.TargetTag.Index : 0);
                    graph.Nodes.Add(LogicNode.CreateAction(
                        act.Id,
                        actTagIdx,
                        (ActionKind)act.ActionType,
                        act.ActionParam,
                        executionOrder: actionOrder++,
                        act.CustomLabel));
                    break;

                case TimerNodeViewModel tm:
                    ushort? tmInTag = tm.InputTag != null && tm.InputTag.Kind != TagKind.None ? (ushort)tm.InputTag.Index : null;
                    ushort? tmQTag = tm.OutputTag != null && tm.OutputTag.Kind != TagKind.None ? (ushort)tm.OutputTag.Index : null;

                    // Resolve connections directly if wired on canvas
                    var inConn = connectionList.FirstOrDefault(c => c.Target?.Node == tm &&
                        (string.Equals(c.Target.Title, "In", StringComparison.OrdinalIgnoreCase) ||
                         c.Target == tm.InputConnectors.FirstOrDefault()));
                    if (inConn?.Source?.Node is InputNodeViewModel tmInNode && tmInNode.Tag != null)
                    {
                        tmInTag = (ushort)tmInNode.Tag.Index;
                    }

                    var tmQConn = connectionList.FirstOrDefault(c => c.Source?.Node == tm &&
                        (string.Equals(c.Source.Title, "Q", StringComparison.OrdinalIgnoreCase) ||
                         c.Source == tm.OutputConnectors.FirstOrDefault()));
                    if (tmQConn?.Target?.Node is ActionNodeViewModel tmActNode && tmActNode.TargetTag != null)
                    {
                        tmQTag = (ushort)tmActNode.TargetTag.Index;
                    }

                    // Nếu khối Timer thiếu ngõ vào IN
                    if (!tmInTag.HasValue)
                    {
                        studioDiagnostics.Add(new(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Tr("Diag_SPLC-COMP-TIMER-IN"), tm.DisplayTitle),
                            NodeId: tm.Id,
                            FieldName: "In"));
                    }

                    // Nếu khối Timer thiếu ngõ ra Q
                    if (!tmQTag.HasValue)
                    {
                        studioDiagnostics.Add(new(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Tr("Diag_SPLC-COMP-TIMER-Q"), tm.DisplayTitle),
                            NodeId: tm.Id,
                            FieldName: "Q"));
                    }

                    if (!tmInTag.HasValue || !tmQTag.HasValue)
                    {
                        break;
                    }

                    TimerMacroType tmType = tm.TimerMode switch
                    {
                        "TOF" => TimerMacroType.Tof,
                        "TP" => TimerMacroType.Tp,
                        _ => TimerMacroType.Ton
                    };
                    graph.Nodes.Add(LogicNode.CreateTimer(
                        tm.Id,
                        tmType,
                        tmInTag.Value,
                        tm.PresetMs,
                        tmQTag.Value,
                        executionOrder: actionOrder++,
                        tm.CustomLabel));
                    break;

                case CounterNodeViewModel cnt:
                    ushort? cntInTag = cnt.InputTag != null && cnt.InputTag.Kind != TagKind.None ? (ushort)cnt.InputTag.Index : null;
                    ushort cntCvTag = (ushort)(cnt.CvTag != null && cnt.CvTag.Kind != TagKind.None ? cnt.CvTag.Index : 84);
                    ushort? cntQTag = cnt.OutputTag != null && cnt.OutputTag.Kind != TagKind.None ? (ushort)cnt.OutputTag.Index : null;
                    ushort? cntResetTag = cnt.ResetTag != null && cnt.ResetTag.Kind != TagKind.None ? (ushort)cnt.ResetTag.Index : null;

                    // Resolve connections directly if wired on canvas
                    var cuConn = connectionList.FirstOrDefault(c => c.Target?.Node == cnt &&
                        (string.Equals(c.Target.Title, "CU", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(c.Target.Title, "CD", StringComparison.OrdinalIgnoreCase) ||
                         c.Target == cnt.InputConnectors.FirstOrDefault()));
                    if (cuConn?.Source?.Node is InputNodeViewModel inNode && inNode.Tag != null)
                    {
                        cntInTag = (ushort)inNode.Tag.Index;
                    }

                    var rConn = connectionList.FirstOrDefault(c => c.Target?.Node == cnt &&
                        (string.Equals(c.Target.Title, "R", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(c.Target.Title, "Reset", StringComparison.OrdinalIgnoreCase) ||
                         (cnt.InputConnectors.Count > 1 && c.Target == cnt.InputConnectors[1])));
                    if (rConn?.Source?.Node is InputNodeViewModel rNode && rNode.Tag != null)
                    {
                        cntResetTag = (ushort)rNode.Tag.Index;
                    }

                    var qConn = connectionList.FirstOrDefault(c => c.Source?.Node == cnt &&
                        (string.Equals(c.Source.Title, "Q", StringComparison.OrdinalIgnoreCase) ||
                         c.Source == cnt.OutputConnectors.FirstOrDefault()));
                    if (qConn?.Target?.Node is ActionNodeViewModel actNode && actNode.TargetTag != null)
                    {
                        cntQTag = (ushort)actNode.TargetTag.Index;
                    }

                    // Nếu khối Counter thiếu ngõ vào đếm
                    if (!cntInTag.HasValue)
                    {
                        string portName = cnt.CounterMode == "CTD" ? "CD" : "CU";
                        studioDiagnostics.Add(new(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Tr("Diag_SPLC-COMP-COUNTER-IN"), cnt.DisplayTitle, portName),
                            NodeId: cnt.Id,
                            FieldName: portName));
                    }

                    // Nếu khối Counter thiếu ngõ ra Q
                    if (!cntQTag.HasValue)
                    {
                        studioDiagnostics.Add(new(
                            SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                            string.Format(LocalizationService.Tr("Diag_SPLC-COMP-COUNTER-Q"), cnt.DisplayTitle),
                            NodeId: cnt.Id,
                            FieldName: "Q"));
                    }

                    if (!cntInTag.HasValue || !cntQTag.HasValue)
                    {
                        break;
                    }

                    CounterMacroType cntType = cnt.CounterMode switch
                    {
                        "CTD" => CounterMacroType.Ctd,
                        _ => CounterMacroType.Ctu
                    };
                    graph.Nodes.Add(LogicNode.CreateCounter(
                        cnt.Id,
                        cntType,
                        cntInTag.Value,
                        cntResetTag,
                        cntCvTag,
                        cnt.PresetValue,
                        cntQTag.Value,
                        executionOrder: actionOrder++,
                        cnt.CustomLabel));
                    break;

                case ScaleNodeViewModel scl:
                    ushort sclInTag = (ushort)(scl.InputTag?.Index ?? 0);
                    var sclInConn = connectionList.FirstOrDefault(c => c.Target?.Node == scl &&
                        (string.Equals(c.Target.Title, "IN", StringComparison.OrdinalIgnoreCase) ||
                         c.Target == scl.InputConnectors.FirstOrDefault()));
                    if (sclInConn?.Source?.Node is InputNodeViewModel sclInVm && sclInVm.Tag != null)
                    {
                        sclInTag = (ushort)sclInVm.Tag.Index;
                    }

                    ushort? sclOutTag = scl.OutputTag != null ? (ushort)scl.OutputTag.Index : null;
                    var sclOutConn = connectionList.FirstOrDefault(c => c.Source?.Node == scl);
                    if (sclOutConn?.Target?.Node is ActionNodeViewModel actOut && actOut.TargetTag != null)
                    {
                        sclOutTag = (ushort)actOut.TargetTag.Index;
                    }

                    graph.Nodes.Add(LogicNode.CreateScale(
                        scl.Id,
                        sclInTag,
                        sclOutTag,
                        scl.Gain,
                        scl.Offset,
                        scl.IsClamped,
                        scl.ClampMin,
                        scl.ClampMax,
                        scl.DecimalPlaces,
                        scl.Unit,
                        executionOrder: actionOrder++,
                        scl.CustomLabel));
                    break;

                default:
                    studioDiagnostics.Add(new(SimplePLC.Studio.Models.DiagnosticSeverity.Error, LocalizationService.Tr("DiagUnsupportedNode"), NodeId: node.Id));
                    break;
            }
        }

        // 2. Build POCO Graph Edges
        var validNodeIds = new HashSet<string>(graph.Nodes.Select(n => n.Id));
        foreach (var conn in connectionList)
        {
            if (conn.Source?.Node != null && conn.Target?.Node != null)
            {
                // Chỉ bổ sung cạnh nếu cả Source Node và Target Node đều có mặt trong đồ thị.
                // Tránh lỗi kỹ thuật "Target/Source node '<GUID>' not found" khi một khối macro dở dang chưa được nạp vào đồ thị.
                if (validNodeIds.Contains(conn.Source.Node.Id) && validNodeIds.Contains(conn.Target.Node.Id))
                {
                    graph.Edges.Add(new LogicEdge(
                        conn.Source.Node.Id,
                        conn.Target.Node.Id,
                        conn.Source.Title,
                        conn.Target.Title));
                }
            }
        }

        // 3. Delegate to Core Application Compiler
        var coreResult = _coreCompiler.Compile(graph, targetProduct);

        // 4. Map Diagnostics
        foreach (var diag in coreResult.Diagnostics)
        {
            var severity = diag.Severity switch
            {
                SimplePLC.Application.Logic.Compilation.DiagnosticSeverity.Error => SimplePLC.Studio.Models.DiagnosticSeverity.Error,
                SimplePLC.Application.Logic.Compilation.DiagnosticSeverity.Warning => SimplePLC.Studio.Models.DiagnosticSeverity.Warning,
                _ => SimplePLC.Studio.Models.DiagnosticSeverity.Info
            };

            var msg = LocalizeDiagnostic(diag, nodeList);

            studioDiagnostics.Add(new SimplePLC.Studio.Models.Diagnostic(
                severity,
                msg,
                NodeId: diag.NodeId,
                FieldName: diag.FieldName,
                RelatedNodeIds: diag.RelatedNodeIds));
        }

        // 5. Project Domain Rules to RuleItemModels for UI display
        var rules = new List<RuleItemModel>();
        if (coreResult.Program != null)
        {
            foreach (var domainRule in coreResult.Program.Rules)
            {
                var ruleItem = RuleItemModel.FromDomainRule(domainRule, tagCatalog.AllTags);
                ruleItem.Id = $"R{domainRule.RuleIndex + 1}";

                // Gắn SourceMap attribution từ Macro (Disassembly attribution)
                if (coreResult.Program.SourceMap.TryGetValue(domainRule.RuleIndex, out var origin))
                {
                    ruleItem.DiagramId = origin.SourceNodeId;
                    string labelPart = !string.IsNullOrWhiteSpace(origin.DisplayLabel) ? $" \"{origin.DisplayLabel}\"" : "";
                    int macroRuleCount = coreResult.Program.SourceMap.Values.Count(v => v.SourceNodeId == origin.SourceNodeId);
                    if (macroRuleCount <= 0) macroRuleCount = 2;
                    ruleItem.MacroAttribution = $"{origin.MacroType}{labelPart} ({origin.ExpansionIndex + 1}/{macroRuleCount})";
                }

                ruleItem.UpdateNarrative();
                rules.Add(ruleItem);
            }
        }

        return new SimplePLC.Studio.Models.CompileResult(rules, studioDiagnostics, coreResult.Program);
    }

    private static string LocalizeDiagnostic(SimplePLC.Application.Logic.Compilation.CompileDiagnostic diag, IReadOnlyList<GraphNodeViewModel>? nodes = null)
    {
        if (!LocalizationService.Instance.IsVietnamese)
        {
            return diag.Message;
        }

        var key = "Diag_" + diag.Code;
        var template = LocalizationService.Tr(key);
        if (template != key)
        {
            try
            {
                if (template.Contains("{0}"))
                {
                    if (diag.Code == SimplePLC.Application.Logic.Compilation.RuleDependencyGraph.ErrCircularDependency ||
                        diag.Code == SimplePLC.Application.Logic.Graph.GraphGrammarV1.ErrCycleDetected)
                    {
                        var cyclePath = diag.Message
                            .Replace("Cycle detected in logic graph: ", "")
                            .Replace("Graph contains a circular dependency: ", "");
                        return string.Format(template, cyclePath);
                    }

                    var targetNode = !string.IsNullOrEmpty(diag.NodeId)
                        ? nodes?.FirstOrDefault(n => n.Id == diag.NodeId)
                        : null;
                    var nodeLabel = targetNode?.DisplayTitle ?? diag.NodeId ?? diag.FieldName ?? "";
                    return string.Format(template, nodeLabel);
                }
                return template;
            }
            catch
            {
                return template;
            }
        }

        return diag.Message;
    }
}
