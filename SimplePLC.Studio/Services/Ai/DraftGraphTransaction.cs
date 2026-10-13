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

    // Macro props (Timer / Counter)
    public string Mode { get; set; } = string.Empty;
    public int PresetValue { get; set; }
    public string InputTagName { get; set; } = "NONE";
    public string CvTagName { get; set; } = "NONE";
    public string ResetTagName { get; set; } = "NONE";
    public string OutputTagName { get; set; } = "NONE";

    // Macro props (Scale)
    public double Gain { get; set; } = 0.01;
    public double Offset { get; set; } = 0.0;
    public string Unit { get; set; } = "bar";
    public int DecimalPlaces { get; set; } = 1;
    public bool IsClamped { get; set; } = true;
    public double ClampMin { get; set; } = 0.0;
    public double ClampMax { get; set; } = 100.0;
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
/// Bản ghi thao tác đổi bí danh Tag trong giao dịch đồ thị nháp.
/// </summary>
public record TagRenameRecord(string TagName, string NewAlias, string Description);

/// <summary>
/// Yêu cầu tự động căn chỉnh sơ đồ theo giải thuật phân tầng Sugiyama.
/// </summary>
public record AutoLayoutRequest(string DiagramId, double HorizontalSpacing, double VerticalSpacing);

/// <summary>
/// Yêu cầu kiểm tra chẩn đoán lỗi tĩnh hệ thống PLC.
/// </summary>
public record DiagnosticRequest(bool CheckWriteConflicts, bool CheckCyclicLoops, bool AutoProposeFixes);

/// <summary>
/// Phiên giao dịch Unit of Work gom toàn bộ thay đổi do AI sinh ra (Layer 3).
/// </summary>
public class DraftGraphTransaction
{
    public List<DraftNode> AddedNodes { get; } = new();
    public List<string> RemovedNodeIds { get; } = new();
    public List<DraftWire> AddedWires { get; } = new();
    public List<string> DisconnectedWireIds { get; } = new();
    public List<TagRenameRecord> RenamedTags { get; } = new();
    public AutoLayoutRequest? LayoutRequest { get; set; }
    public DiagnosticRequest? DiagnosticCheck { get; set; }

    public bool IsAutoLayoutRequested => LayoutRequest != null;
    public bool IsDiagnosticRequested => DiagnosticCheck != null;

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

                // Nếu là khối macro và có tag_name hợp lệ, đồng bộ input_tag_name
                if (node.TagName != "NONE" && !string.IsNullOrWhiteSpace(node.TagName))
                {
                    node.InputTagName = node.TagName;
                }

                AddedNodes.Add(node);
                break;
            }

            case "remove_node":
            {
                string? nodeId = args["node_id"]?.GetValue<string>();
                if (!string.IsNullOrEmpty(nodeId))
                {
                    RemovedNodeIds.Add(nodeId);
                    AddedNodes.RemoveAll(n => n.Id == nodeId);
                    AddedWires.RemoveAll(w => w.SourceNodeId == nodeId || w.TargetNodeId == nodeId);
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

            case "disconnect_wire":
            {
                string? connId = args["connection_id"]?.GetValue<string>();
                string? srcId = args["source_node_id"]?.GetValue<string>();
                string? tgtId = args["target_node_id"]?.GetValue<string>();

                if (!string.IsNullOrEmpty(connId) && !string.Equals(connId, "AUTO", StringComparison.OrdinalIgnoreCase))
                {
                    DisconnectedWireIds.Add(connId);
                    AddedWires.RemoveAll(w => string.Equals(w.Id, connId, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrEmpty(srcId) && !string.IsNullOrEmpty(tgtId))
                {
                    AddedWires.RemoveAll(w =>
                        string.Equals(w.SourceNodeId, srcId, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(w.TargetNodeId, tgtId, StringComparison.OrdinalIgnoreCase));
                }
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

            case "configure_scale":
            {
                string? nodeId = args["node_id"]?.GetValue<string>();
                var targetNode = AddedNodes.FirstOrDefault(n => n.Id == nodeId);
                if (targetNode != null)
                {
                    targetNode.Gain = GetDouble(args["gain"]);
                    targetNode.Offset = GetDouble(args["offset"]);
                    targetNode.Unit = args["unit"]?.GetValue<string>() ?? "bar";
                    targetNode.DecimalPlaces = GetInt(args["decimal_places"]);
                    targetNode.IsClamped = GetBool(args["is_clamped"]);
                    targetNode.ClampMin = GetDouble(args["clamp_min"]);
                    targetNode.ClampMax = GetDouble(args["clamp_max"]);
                    if (args["input_tag_name"] != null)
                    {
                        targetNode.InputTagName = args["input_tag_name"]?.GetValue<string>() ?? targetNode.InputTagName;
                    }
                    if (args["output_tag_name"] != null)
                    {
                        targetNode.OutputTagName = args["output_tag_name"]?.GetValue<string>() ?? targetNode.OutputTagName;
                    }
                }
                break;
            }

            case "rename_tag":
            {
                string? tagName = args["tag_name"]?.GetValue<string>();
                string? newAlias = args["new_alias"]?.GetValue<string>();
                string? desc = args["description"]?.GetValue<string>() ?? string.Empty;

                if (!string.IsNullOrEmpty(tagName) && !string.IsNullOrEmpty(newAlias))
                {
                    RenamedTags.Add(new TagRenameRecord(tagName, newAlias, desc));
                }
                break;
            }

            case "auto_layout_graph":
            {
                string diagramId = args["diagram_id"]?.GetValue<string>() ?? "ALL";
                double hSpacing = GetDouble(args["horizontal_spacing"]);
                if (hSpacing <= 0) hSpacing = 260;
                double vSpacing = GetDouble(args["vertical_spacing"]);
                if (vSpacing <= 0) vSpacing = 120;

                LayoutRequest = new AutoLayoutRequest(diagramId, hSpacing, vSpacing);

                // Tự động gán lại tọa độ các draft node theo phân tầng công nghiệp
                var layerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    ["INPUT"] = 0,
                    ["TRIGGER"] = 1,
                    ["GUARD"] = 2,
                    ["ACTION"] = 3,
                    ["TIMER"] = 1,
                    ["COUNTER"] = 1,
                    ["SCALE"] = 1
                };

                var colCounters = new Dictionary<int, int>();
                foreach (var node in AddedNodes)
                {
                    int col = layerMap.TryGetValue(node.NodeType, out var c) ? c : 0;
                    colCounters.TryGetValue(col, out int row);
                    node.PositionX = 60 + (col * hSpacing);
                    node.PositionY = 100 + (row * vSpacing);
                    colCounters[col] = row + 1;
                }
                break;
            }

            case "diagnose_logic_bugs":
            {
                bool checkWriteConflicts = GetBool(args["check_write_conflicts"]);
                bool checkCyclicLoops = GetBool(args["check_cyclic_loops"]);
                bool autoProposeFixes = GetBool(args["auto_propose_fixes"]);

                DiagnosticCheck = new DiagnosticRequest(checkWriteConflicts, checkCyclicLoops, autoProposeFixes);
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

    private static bool GetBool(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node == null) return false;
        if (node is System.Text.Json.Nodes.JsonValue val)
        {
            if (val.TryGetValue<bool>(out var b)) return b;
            if (bool.TryParse(val.ToString(), out var parsed)) return parsed;
        }
        return false;
    }
}
