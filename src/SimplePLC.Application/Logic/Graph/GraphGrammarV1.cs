namespace SimplePLC.Application.Logic.Graph;

using SimplePLC.Application.Logic.Compilation;

public static class GraphGrammarV1
{
    public const string ErrSameNode = "SPLC-GRAPH-001";
    public const string ErrActionSource = "SPLC-GRAPH-002";
    public const string ErrInputTarget = "SPLC-GRAPH-003";
    public const string ErrInputToAction = "SPLC-GRAPH-004";
    public const string ErrInputToGuard = "SPLC-GRAPH-005";
    public const string ErrTriggerToTrigger = "SPLC-GRAPH-006";
    public const string ErrChainedGuards = "SPLC-GRAPH-007";
    public const string ErrGuardToTrigger = "SPLC-GRAPH-008";
    public const string ErrInvalidTransition = "SPLC-GRAPH-009";
    public const string ErrMultipleInputsOnPort = "SPLC-GRAPH-010";
    public const string ErrCycleDetected = "SPLC-GRAPH-011";

    public static bool CanConnect(LogicNodeKind sourceKind, LogicNodeKind targetKind) =>
        CanConnect(sourceKind, targetKind, out _, out _);

    public static bool CanConnect(LogicNodeKind sourceKind, LogicNodeKind targetKind, out string errorCode, out string errorReason)
    {
        if (sourceKind == LogicNodeKind.Action)
        {
            errorCode = ErrActionSource;
            errorReason = "Action node has no output connectors.";
            return false;
        }

        if (sourceKind == LogicNodeKind.Timer)
        {
            if (targetKind == LogicNodeKind.Action)
            {
                errorCode = string.Empty;
                errorReason = string.Empty;
                return true;
            }

            errorCode = "SPLC-GRAPH-TIMER-OUT";
            errorReason = "Timer output Q can only connect to an Action node.";
            return false;
        }

        if (sourceKind == LogicNodeKind.Counter)
        {
            if (targetKind == LogicNodeKind.Action)
            {
                errorCode = string.Empty;
                errorReason = string.Empty;
                return true;
            }

            errorCode = "SPLC-GRAPH-COUNTER-OUT";
            errorReason = "Counter output Q can only connect to an Action node.";
            return false;
        }

        if (sourceKind == LogicNodeKind.Scale)
        {
            if (targetKind == LogicNodeKind.Action || targetKind == LogicNodeKind.Trigger)
            {
                errorCode = string.Empty;
                errorReason = string.Empty;
                return true;
            }

            errorCode = "SPLC-GRAPH-SCALE-OUT";
            errorReason = "Scale output can connect to an Action or Trigger node.";
            return false;
        }

        if (sourceKind == LogicNodeKind.Input && targetKind == LogicNodeKind.Scale)
        {
            errorCode = string.Empty;
            errorReason = string.Empty;
            return true;
        }

        if (targetKind == LogicNodeKind.Scale)
        {
            errorCode = "SPLC-GRAPH-SCALE-IN";
            errorReason = "Scale node can only receive an upstream connection from an Input node.";
            return false;
        }

        if (targetKind == LogicNodeKind.Input)
        {
            errorCode = ErrInputTarget;
            errorReason = "Input node has no input connectors.";
            return false;
        }

        // Allowed transitions
        if (sourceKind == LogicNodeKind.Input && targetKind == LogicNodeKind.Trigger)
        {
            errorCode = string.Empty;
            errorReason = string.Empty;
            return true;
        }

        if (sourceKind == LogicNodeKind.Input && targetKind == LogicNodeKind.Timer)
        {
            errorCode = string.Empty;
            errorReason = string.Empty;
            return true;
        }

        if (targetKind == LogicNodeKind.Timer)
        {
            errorCode = "SPLC-GRAPH-TIMER-IN";
            errorReason = "Timer macro node can only receive an upstream connection from an Input node.";
            return false;
        }

        if (sourceKind == LogicNodeKind.Input && targetKind == LogicNodeKind.Counter)
        {
            errorCode = string.Empty;
            errorReason = string.Empty;
            return true;
        }

        if (targetKind == LogicNodeKind.Counter)
        {
            errorCode = "SPLC-GRAPH-COUNTER-IN";
            errorReason = "Counter macro node can only receive an upstream connection from an Input node.";
            return false;
        }

        if (sourceKind == LogicNodeKind.Trigger && targetKind == LogicNodeKind.Action)
        {
            errorCode = string.Empty;
            errorReason = string.Empty;
            return true;
        }

        if (sourceKind == LogicNodeKind.Trigger && targetKind == LogicNodeKind.Guard)
        {
            errorCode = string.Empty;
            errorReason = string.Empty;
            return true;
        }

        if (sourceKind == LogicNodeKind.Guard && targetKind == LogicNodeKind.Action)
        {
            errorCode = string.Empty;
            errorReason = string.Empty;
            return true;
        }

        // Specific rejected transitions with helpful diagnostics
        if (sourceKind == LogicNodeKind.Input && targetKind == LogicNodeKind.Action)
        {
            errorCode = ErrInputToAction;
            errorReason = "Input cannot connect directly to Action; an intermediate Trigger is required.";
            return false;
        }

        if (sourceKind == LogicNodeKind.Input && targetKind == LogicNodeKind.Guard)
        {
            errorCode = ErrInputToGuard;
            errorReason = "Input cannot connect directly to Guard; Guard must receive signal from a Trigger.";
            return false;
        }

        if (sourceKind == LogicNodeKind.Trigger && targetKind == LogicNodeKind.Trigger)
        {
            errorCode = ErrTriggerToTrigger;
            errorReason = "Trigger cannot connect directly to another Trigger.";
            return false;
        }

        if (sourceKind == LogicNodeKind.Guard && targetKind == LogicNodeKind.Guard)
        {
            errorCode = ErrChainedGuards;
            errorReason = "Chained Guards are not supported in SimplePLC V1. Use a VFLAG intermediate tag to combine multiple conditions.";
            return false;
        }

        if (sourceKind == LogicNodeKind.Guard && targetKind == LogicNodeKind.Trigger)
        {
            errorCode = ErrGuardToTrigger;
            errorReason = "Guard cannot connect back into a Trigger.";
            return false;
        }

        errorCode = ErrInvalidTransition;
        errorReason = $"Connection from {sourceKind} to {targetKind} is not supported.";
        return false;
    }

    public static List<CompileDiagnostic> ValidateStructure(LogicGraph graph)
    {
        var diagnostics = new List<CompileDiagnostic>();
        var nodeMap = graph.Nodes.ToDictionary(n => n.Id);

        // 1. Check duplicate incoming edges to same target port
        var targetPortCounts = new Dictionary<(string NodeId, string Port), int>();
        foreach (var edge in graph.Edges)
        {
            var key = (edge.TargetNodeId, edge.TargetPort);
            targetPortCounts[key] = targetPortCounts.GetValueOrDefault(key) + 1;
            if (targetPortCounts[key] > 1)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrMultipleInputsOnPort,
                    DiagnosticSeverity.Error,
                    $"Port '{edge.TargetPort}' on node '{edge.TargetNodeId}' can only receive 1 incoming connection.",
                    NodeId: edge.TargetNodeId,
                    EdgeId: edge.Id));
            }
        }

        // 2. Validate each edge matches grammar
        foreach (var edge in graph.Edges)
        {
            if (!nodeMap.TryGetValue(edge.SourceNodeId, out var src))
            {
                diagnostics.Add(new CompileDiagnostic(
                    "SPLC-GRAPH-SRC-NOTFOUND",
                    DiagnosticSeverity.Error,
                    $"Source node '{edge.SourceNodeId}' not found.",
                    EdgeId: edge.Id));
                continue;
            }

            if (!nodeMap.TryGetValue(edge.TargetNodeId, out var tgt))
            {
                diagnostics.Add(new CompileDiagnostic(
                    "SPLC-GRAPH-TGT-NOTFOUND",
                    DiagnosticSeverity.Error,
                    $"Target node '{edge.TargetNodeId}' not found.",
                    EdgeId: edge.Id));
                continue;
            }

            if (src.Id == tgt.Id)
            {
                diagnostics.Add(new CompileDiagnostic(
                    ErrSameNode,
                    DiagnosticSeverity.Error,
                    "A node cannot connect to itself.",
                    NodeId: src.Id,
                    EdgeId: edge.Id));
                continue;
            }

            if (!CanConnect(src.Kind, tgt.Kind, out var code, out var reason))
            {
                diagnostics.Add(new CompileDiagnostic(
                    code,
                    DiagnosticSeverity.Error,
                    reason,
                    NodeId: tgt.Id,
                    EdgeId: edge.Id));
            }
        }

        // 3. Cycle Detection using DFS
        var visited = new Dictionary<string, int>(); // 0: unvisited, 1: visiting (in stack), 2: visited
        foreach (var node in graph.Nodes)
        {
            if (!visited.ContainsKey(node.Id))
            {
                if (DetectCycleDfs(node.Id, graph, visited, out var cyclePath))
                {
                    diagnostics.Add(new CompileDiagnostic(
                        ErrCycleDetected,
                        DiagnosticSeverity.Error,
                        $"Graph contains a circular dependency: {string.Join(" -> ", cyclePath)}",
                        NodeId: cyclePath.FirstOrDefault()));
                    break;
                }
            }
        }

        return diagnostics;
    }

    private static bool DetectCycleDfs(
        string currentId,
        LogicGraph graph,
        Dictionary<string, int> visited,
        out List<string> cyclePath)
    {
        visited[currentId] = 1; // visiting
        cyclePath = new List<string> { currentId };

        foreach (var edge in graph.GetOutgoingEdges(currentId))
        {
            var nextId = edge.TargetNodeId;
            if (visited.TryGetValue(nextId, out int state))
            {
                if (state == 1) // Cycle found!
                {
                    cyclePath.Add(nextId);
                    return true;
                }
            }
            else
            {
                if (DetectCycleDfs(nextId, graph, visited, out var subPath))
                {
                    cyclePath.AddRange(subPath);
                    return true;
                }
            }
        }

        visited[currentId] = 2; // done
        return false;
    }
}
