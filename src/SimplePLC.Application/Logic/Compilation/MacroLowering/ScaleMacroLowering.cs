namespace SimplePLC.Application.Logic.Compilation.MacroLowering;

using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;

/// <summary>
/// Hạ cấp (Macro Lowering) các khối Scale (bộ khuếch đại tuyến tính Gain/Offset) thành tập luật logic cơ sở.
/// </summary>
public static class ScaleMacroLowering
{
    /// <summary>
    /// Thực hiện mở rộng các Scale nodes thành các CandidateRule.
    /// </summary>
    public static void Lower(
        LogicGraph graph,
        ProductDefinition product,
        IReadOnlyDictionary<string, LogicNode> nodeMap,
        IReadOnlyList<LogicNode> scaleNodes,
        List<CandidateRule> candidateRules,
        List<CompileDiagnostic> diagnostics)
    {
        foreach (var scaleNode in scaleNodes)
        {
            var sData = scaleNode.ScaleData ?? new ScaleNodeData();
            ushort resolvedInTagIndex = sData.InTagIndex;
            ushort? resolvedOutTagIndex = sData.OutTagIndex;

            // 1. Resolve upstream Input connection
            foreach (var inEdge in graph.GetIncomingEdges(scaleNode.Id))
            {
                if (nodeMap.TryGetValue(inEdge.SourceNodeId, out var srcNode) && srcNode.Kind == LogicNodeKind.Input && srcNode.InputData != null)
                {
                    resolvedInTagIndex = srcNode.InputData.TagIndex;
                }
            }

            // 2. Resolve downstream Output connection (Scale OUT -> Action)
            foreach (var outEdge in graph.GetOutgoingEdges(scaleNode.Id))
            {
                if (nodeMap.TryGetValue(outEdge.TargetNodeId, out var tgtNode) && tgtNode.Kind == LogicNodeKind.Action && tgtNode.ActionData != null)
                {
                    resolvedOutTagIndex = tgtNode.ActionData.TargetTagIndex;
                }
            }

            var inTag = product.FindTagByIndex(resolvedInTagIndex);
            var outTag = resolvedOutTagIndex.HasValue ? product.FindTagByIndex(resolvedOutTagIndex.Value) : null;

            var scaleLabel = CompilationHelpers.GetNodeLabel(scaleNode, "Scale");

            if (inTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    RuleCompiler.ErrScaleNoInput,
                    DiagnosticSeverity.Error,
                    $"Scale node '{scaleLabel}' references unknown Input Tag {resolvedInTagIndex}.",
                    NodeId: scaleNode.Id));
                continue;
            }

            if (outTag == null)
            {
                diagnostics.Add(new CompileDiagnostic(
                    RuleCompiler.ErrScaleNoOutput,
                    DiagnosticSeverity.Error,
                    $"Scale node '{scaleLabel}' requires an Output Tag (connect OUT to an Action node).",
                    NodeId: scaleNode.Id));
                continue;
            }

            string macroId = scaleNode.Id;
            string labelPrefix = !string.IsNullOrWhiteSpace(scaleNode.Label) ? scaleNode.Label : scaleNode.Id;

            bool isFloatScale = inTag.DataType == TagDataType.Float32 || outTag.DataType == TagDataType.Float32;
            TriggerModel trigScale;
            ActionModel actScale;

            if (isFloatScale)
            {
                trigScale = new TriggerModel(inTag, TriggerKind.OnChange)
                {
                    ThresholdHi = sData.Offset, // Giữ tương thích ngược
                    IsFloat = true,
                    ForMs = 0
                };
                actScale = new ActionModel(outTag, ActionKind.ScaleTag, sData.Gain, sData.Offset)
                {
                    IsFloat = true
                };
            }
            else
            {
                int scaleParam = (int)Math.Round(sData.Gain * 1000.0);
                if (scaleParam == 0 && Math.Abs(sData.Gain) > 1e-6)
                {
                    scaleParam = sData.Gain > 0 ? 1 : -1;
                }

                int offsetParam = (int)Math.Round(sData.Offset);
                trigScale = new TriggerModel(inTag, TriggerKind.OnChange)
                {
                    ThresholdHi = offsetParam, // Giữ tương thích ngược
                    ForMs = 0
                };
                actScale = new ActionModel(outTag, ActionKind.ScaleTag, scaleParam, offsetParam);
            }

            var originScale = new GeneratedRuleOrigin(scaleNode.Id, macroId, "SCALE", 0, scaleNode.Label);
            candidateRules.Add(new CandidateRule(scaleNode, trigScale, actScale, GuardModel.Empty, $"{labelPrefix}_SCALE", originScale));
        }
    }
}
