namespace SimplePLC.Application.Logic.Graph;

using SimplePLC.Domain.Enums;

public enum LogicNodeKind
{
    Input = 0,
    Trigger = 1,
    Guard = 2,
    Action = 3,
    Timer = 4,
    Counter = 5,
    Scale = 6
}

public sealed class ScaleNodeData
{
    public ushort InTagIndex { get; set; }
    public ushort? OutTagIndex { get; set; }
    public double Gain { get; set; } = 0.01;
    public double Offset { get; set; } = 0.0;
    public bool IsClamped { get; set; } = true;
    public double ClampMin { get; set; } = 0.0;
    public double ClampMax { get; set; } = 100.0;
    public int DecimalPlaces { get; set; } = 1;
    public string Unit { get; set; } = "bar";
}

public enum TimerMacroType
{
    Ton = 0,
    Tof = 1,
    Tp = 2
}

public enum CounterMacroType
{
    Ctu = 0, // Count Up
    Ctd = 1  // Count Down
}

public sealed class CounterNodeData
{
    public CounterMacroType Type { get; set; } = CounterMacroType.Ctu;
    public ushort CuTagIndex { get; set; }
    public ushort? ResetTagIndex { get; set; } // null if none
    public ushort CvTagIndex { get; set; }
    public int PresetValue { get; set; } = 10;
    public ushort QTagIndex { get; set; }
}

public sealed class TimerNodeData
{
    public TimerMacroType Type { get; set; } = TimerMacroType.Ton;
    public ushort InTagIndex { get; set; }
    public uint PresetMs { get; set; }
    public ushort QTagIndex { get; set; }
}

public sealed class InputNodeData
{
    public ushort TagIndex { get; set; }
}

public sealed class TriggerNodeData
{
    public TriggerKind Type { get; set; } = TriggerKind.OnRise;
    public CompareOperator CompareOp { get; set; } = CompareOperator.None;
    public int ThresholdLo { get; set; }
    public int ThresholdHi { get; set; }
    public uint ForMs { get; set; }
}

public sealed class GuardNodeData
{
    public ushort TagIndex { get; set; }
    public bool Negated { get; set; }
}

public sealed class ActionNodeData
{
    public ushort TargetTagIndex { get; set; }
    public ActionKind Type { get; set; } = ActionKind.SetTag;
    public int Parameter { get; set; } = 1;
}

public sealed class LogicNode
{
    public string Id { get; set; } = string.Empty;
    public LogicNodeKind Kind { get; set; }
    public int ExecutionOrder { get; set; } = 0;
    public string Label { get; set; } = string.Empty;

    public InputNodeData? InputData { get; set; }
    public TriggerNodeData? TriggerData { get; set; }
    public GuardNodeData? GuardData { get; set; }
    public ActionNodeData? ActionData { get; set; }
    public TimerNodeData? TimerData { get; set; }
    public CounterNodeData? CounterData { get; set; }
    public ScaleNodeData? ScaleData { get; set; }

    public static LogicNode CreateInput(string id, ushort tagIndex, string label = "") =>
        new() { Id = id, Kind = LogicNodeKind.Input, Label = label, InputData = new InputNodeData { TagIndex = tagIndex } };

    public static LogicNode CreateTrigger(
        string id,
        TriggerKind type = TriggerKind.OnRise,
        CompareOperator compareOp = CompareOperator.None,
        int thresholdLo = 0,
        int thresholdHi = 0,
        uint forMs = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Trigger,
            Label = label,
            TriggerData = new TriggerNodeData
            {
                Type = type,
                CompareOp = compareOp,
                ThresholdLo = thresholdLo,
                ThresholdHi = thresholdHi,
                ForMs = forMs
            }
        };

    public static LogicNode CreateGuard(string id, ushort tagIndex, bool negated = false, string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Guard,
            Label = label,
            GuardData = new GuardNodeData { TagIndex = tagIndex, Negated = negated }
        };

    public static LogicNode CreateAction(
        string id,
        ushort targetTagIndex,
        ActionKind type = ActionKind.SetTag,
        int parameter = 1,
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Action,
            Label = label,
            ExecutionOrder = executionOrder,
            ActionData = new ActionNodeData
            {
                TargetTagIndex = targetTagIndex,
                Type = type,
                Parameter = parameter
            }
        };

    public static LogicNode CreateTimer(
        string id,
        TimerMacroType type,
        ushort inTagIndex,
        uint presetMs,
        ushort qTagIndex,
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Timer,
            Label = label,
            ExecutionOrder = executionOrder,
            TimerData = new TimerNodeData
            {
                Type = type,
                InTagIndex = inTagIndex,
                PresetMs = presetMs,
                QTagIndex = qTagIndex
            }
        };

    public static LogicNode CreateCounter(
        string id,
        CounterMacroType type,
        ushort cuTagIndex,
        ushort? resetTagIndex,
        ushort cvTagIndex,
        int presetValue,
        ushort qTagIndex,
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Counter,
            Label = label,
            ExecutionOrder = executionOrder,
            CounterData = new CounterNodeData
            {
                Type = type,
                CuTagIndex = cuTagIndex,
                ResetTagIndex = resetTagIndex,
                CvTagIndex = cvTagIndex,
                PresetValue = presetValue,
                QTagIndex = qTagIndex
            }
        };

    public static LogicNode CreateScale(
        string id,
        ushort inTagIndex,
        ushort? outTagIndex,
        double gain = 0.01,
        double offset = 0.0,
        bool isClamped = true,
        double clampMin = 0.0,
        double clampMax = 100.0,
        int decimalPlaces = 1,
        string unit = "bar",
        int executionOrder = 0,
        string label = "") =>
        new()
        {
            Id = id,
            Kind = LogicNodeKind.Scale,
            Label = label,
            ExecutionOrder = executionOrder,
            ScaleData = new ScaleNodeData
            {
                InTagIndex = inTagIndex,
                OutTagIndex = outTagIndex,
                Gain = gain,
                Offset = offset,
                IsClamped = isClamped,
                ClampMin = clampMin,
                ClampMax = clampMax,
                DecimalPlaces = decimalPlaces,
                Unit = unit
            }
        };
}

public sealed class LogicEdge
{
    public string Id { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePort { get; set; } = "Out";
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPort { get; set; } = "In";

    public LogicEdge() { }

    public LogicEdge(string sourceNodeId, string targetNodeId, string sourcePort = "Out", string targetPort = "In", string id = "")
    {
        Id = string.IsNullOrEmpty(id) ? $"{sourceNodeId}->{targetNodeId}" : id;
        SourceNodeId = sourceNodeId;
        TargetNodeId = targetNodeId;
        SourcePort = sourcePort;
        TargetPort = targetPort;
    }
}

public sealed class LogicGraph
{
    public List<LogicNode> Nodes { get; } = new();
    public List<LogicEdge> Edges { get; } = new();

    public LogicNode? FindNode(string id) => Nodes.FirstOrDefault(n => n.Id == id);
    public IEnumerable<LogicEdge> GetIncomingEdges(string targetNodeId) => Edges.Where(e => e.TargetNodeId == targetNodeId);
    public IEnumerable<LogicEdge> GetOutgoingEdges(string sourceNodeId) => Edges.Where(e => e.SourceNodeId == sourceNodeId);
}
