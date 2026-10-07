using System.Text.Json.Nodes;
using SimplePLC.Studio.Models;

namespace SimplePLC.Studio.Services.Ai;

/// <summary>
/// Đại diện cho một node trong phiên giao dịch đồ thị nháp (Staging Graph Transaction).
/// </summary>
public class DraftNode
{
    public string Id { get; set; } = string.Empty;
    public string NodeType { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double PositionX { get; set; }
    public double PositionY { get; set; }
    public string TagName { get; set; } = "NONE";
    public string TriggerType { get; set; } = "NONE";
    public string CompareOp { get; set; } = "NONE";
    public int ThresholdLo { get; set; }
    public int ThresholdHi { get; set; }
    public int DebounceMs { get; set; }
    public string ActionType { get; set; } = "NONE";
    public int ActionParam { get; set; }

    // Macro props
    public string Mode { get; set; } = string.Empty;
    public int PresetValue { get; set; }
    public string InputTagName { get; set; } = "NONE";
    public string CvTagName { get; set; } = "NONE";
    public string ResetTagName { get; set; } = "NONE";
    public string OutputTagName { get; set; } = "NONE";
}

/// <summary>
/// Đại diện cho một kết nối trong phiên giao dịch đồ thị nháp.
/// </summary>
public class DraftWire
{
    public string Id { get; set; } = string.Empty;
    public string SourceNodeId { get; set; } = string.Empty;
    public string SourcePort { get; set; } = string.Empty;
    public string TargetNodeId { get; set; } = string.Empty;
    public string TargetPort { get; set; } = string.Empty;
}

/// <summary>
/// Phiên giao dịch Unit of Work gom toàn bộ thay đổi do AI sinh ra (Layer 3).
/// </summary>
public class DraftGraphTransaction
{
    public List<DraftNode> AddedNodes { get; } = new();
    public List<string> RemovedNodeIds { get; } = new();
    public List<DraftWire> AddedWires { get; } = new();
    public List<string> DisconnectedWireIds { get; } = new();

    public void ApplyToolCall(AiToolCall toolCall)
    {
        var args = toolCall.Arguments;
        switch (toolCall.Name)
        {
            case "add_node":
            {
                var node = new DraftNode
                {
                    Id = args["node_id"]?.GetValue<string>() ?? Guid.NewGuid().ToString(),
                    NodeType = args["node_type"]?.GetValue<string>() ?? "INPUT",
                    Label = args["custom_label"]?.GetValue<string>() ?? string.Empty,
                    PositionX = GetDouble(args["position_x"]),
                    PositionY = GetDouble(args["position_y"]),
                    TagName = args["tag_name"]?.GetValue<string>() ?? "NONE",
                    TriggerType = args["trigger_type"]?.GetValue<string>() ?? "NONE",
                    CompareOp = args["compare_op"]?.GetValue<string>() ?? "NONE",
                    ThresholdLo = GetInt(args["threshold_lo"]),
                    ThresholdHi = GetInt(args["threshold_hi"]),
                    DebounceMs = GetInt(args["debounce_ms"]),
                    ActionType = args["action_type"]?.GetValue<string>() ?? "NONE",
                    ActionParam = GetInt(args["action_param"])
                };
                AddedNodes.Add(node);
                break;
            }

            case "remove_node":
            {
                string? nodeId = args["node_id"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(nodeId))
                {
                    RemovedNodeIds.Add(nodeId);
                }
                break;
            }

            case "connect_wires":
            {
                var wire = new DraftWire
                {
                    Id = args["connection_id"]?.GetValue<string>() ?? Guid.NewGuid().ToString(),
                    SourceNodeId = args["source_node_id"]?.GetValue<string>() ?? string.Empty,
                    SourcePort = args["source_connector_title"]?.GetValue<string>() ?? "Out",
                    TargetNodeId = args["target_node_id"]?.GetValue<string>() ?? string.Empty,
                    TargetPort = args["target_connector_title"]?.GetValue<string>() ?? "In"
                };
                AddedWires.Add(wire);
                break;
            }

            case "configure_timer_counter":
            {
                string? nodeId = args["node_id"]?.GetValue<string>();
                var targetNode = AddedNodes.FirstOrDefault(n => n.Id == nodeId);
                if (targetNode != null)
                {
                    targetNode.Mode = args["mode"]?.GetValue<string>() ?? "TON";
                    targetNode.PresetValue = GetInt(args["preset_value"]);
                    targetNode.InputTagName = args["input_tag_name"]?.GetValue<string>() ?? "NONE";
                    targetNode.CvTagName = args["cv_tag_name"]?.GetValue<string>() ?? "NONE";
                    targetNode.ResetTagName = args["reset_tag_name"]?.GetValue<string>() ?? "NONE";
                    targetNode.OutputTagName = args["output_tag_name"]?.GetValue<string>() ?? "NONE";
                }
                break;
            }
        }
    }

    private static double GetDouble(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node == null) return 0;
        if (node is System.Text.Json.Nodes.JsonValue val)
        {
            if (val.TryGetValue<double>(out var d)) return d;
            if (val.TryGetValue<int>(out var i)) return i;
            if (val.TryGetValue<long>(out var l)) return l;
            if (double.TryParse(val.ToString(), out var parsed)) return parsed;
        }
        return 0;
    }

    private static int GetInt(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node == null) return 0;
        if (node is System.Text.Json.Nodes.JsonValue val)
        {
            if (val.TryGetValue<int>(out var i)) return i;
            if (val.TryGetValue<double>(out var d)) return (int)d;
            if (val.TryGetValue<long>(out var l)) return (int)l;
            if (int.TryParse(val.ToString(), out var parsed)) return parsed;
        }
        return 0;
    }
}
