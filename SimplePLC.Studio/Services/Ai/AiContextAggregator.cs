using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SimplePLC.Application.Abstractions;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Services.Ai;

/// <summary>
/// Dịch vụ tổng hợp ngữ cảnh ngữ nghĩa chuyên sâu của dự án (Layer 1).
/// Đóng gói trạng thái: Danh mục Tag ưu tiên (canvas tags, alias tags, core tags),
/// cấu trúc đồ thị Canvas (Geometry & Connections), giá trị đo thực thời Live I/O,
/// trạng thái kết nối phần cứng và mã lỗi Linter/Compiler.
/// </summary>
public class AiContextAggregator
{
    private static readonly HashSet<string> CoreDefaultTagNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "DI0", "DI1", "DI2", "DI3",
        "DO0", "DO1", "DO2", "DO3",
        "AI0", "AI1",
        "VFLAG0", "VFLAG1", "VFLAG2", "VFLAG3",
        "VREG0", "VREG1", "VREG2", "VREG3",
        "VREG_RETAIN0", "VREG_RETAIN1", "VREG_RETAIN2", "VREG_RETAIN3"
    };

    private readonly Func<IEnumerable<TagModel>?> _tagsProvider;
    private readonly Func<LogicEditorViewModel?> _logicEditorProvider;
    private readonly Func<IRuntimeStateStore?>? _runtimeStateStoreProvider;
    private readonly Func<string>? _hardwareStatusProvider;

    public AiContextAggregator(
        Func<IEnumerable<TagModel>?> tagsProvider,
        Func<LogicEditorViewModel?> logicEditorProvider,
        Func<IRuntimeStateStore?>? runtimeStateStoreProvider = null,
        Func<string>? hardwareStatusProvider = null)
    {
        _tagsProvider = tagsProvider;
        _logicEditorProvider = logicEditorProvider;
        _runtimeStateStoreProvider = runtimeStateStoreProvider;
        _hardwareStatusProvider = hardwareStatusProvider;
    }

    /// <summary>
    /// Tạo cấu trúc JsonObject mô tả toàn diện ngữ cảnh dự án gửi kèm prompt cho LLM.
    /// Áp dụng cơ chế lọc và ưu tiên Tag thông minh (Relevance-based Tag Injection) để tiết kiệm token.
    /// </summary>
    public JsonObject BuildContextPayload()
    {
        var root = new JsonObject();

        // 1. Hardware & Connection
        var hwNode = new JsonObject
        {
            ["status"] = _hardwareStatusProvider?.Invoke() ?? "OFFLINE",
            ["target"] = "SynaptiX Industrial Controller / Modbus RTU V1.7",
            ["rule_capacity"] = "100 rules (32 bytes per rule)"
        };
        root["hardware_profile"] = hwNode;

        var logicVM = _logicEditorProvider();

        // 2. Thu thập danh mục Tag đang sử dụng trên Canvas (Priority 1)
        var usedTagNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (logicVM != null)
        {
            foreach (var n in logicVM.Nodes.Where(n => !n.IsGhost))
            {
                if (n is InputNodeViewModel inNode && inNode.Tag != null)
                    usedTagNames.Add(inNode.Tag.Name);
                else if (n is ActionNodeViewModel actNode && actNode.TargetTag != null)
                    usedTagNames.Add(actNode.TargetTag.Name);
                else if (n is GuardNodeViewModel grdNode && grdNode.GuardTag != null)
                    usedTagNames.Add(grdNode.GuardTag.Name);
                else if (n is TimerNodeViewModel tmNode)
                {
                    if (tmNode.InputTag != null) usedTagNames.Add(tmNode.InputTag.Name);
                    if (tmNode.OutputTag != null) usedTagNames.Add(tmNode.OutputTag.Name);
                }
                else if (n is CounterNodeViewModel cntNode)
                {
                    if (cntNode.InputTag != null) usedTagNames.Add(cntNode.InputTag.Name);
                    if (cntNode.ResetTag != null) usedTagNames.Add(cntNode.ResetTag.Name);
                    if (cntNode.CvTag != null) usedTagNames.Add(cntNode.CvTag.Name);
                    if (cntNode.OutputTag != null) usedTagNames.Add(cntNode.OutputTag.Name);
                }
                else if (n is ScaleNodeViewModel sclNode)
                {
                    if (sclNode.InputTag != null) usedTagNames.Add(sclNode.InputTag.Name);
                    if (sclNode.OutputTag != null) usedTagNames.Add(sclNode.OutputTag.Name);
                }
            }
        }

        // 3. Lọc và ưu tiên Tag (Prioritized Tag Selection)
        var allTags = (_tagsProvider() ?? Enumerable.Empty<TagModel>()).ToList();
        var prioritizedTags = allTags
            .Where(t =>
                usedTagNames.Contains(t.Name) ||
                (!string.IsNullOrWhiteSpace(t.Alias) && t.Alias != "— Không chọn —") ||
                CoreDefaultTagNames.Contains(t.Name))
            .OrderBy(t => usedTagNames.Contains(t.Name) ? 0 :
                          (!string.IsNullOrWhiteSpace(t.Alias) && t.Alias != "— Không chọn —") ? 1 : 2)
            .ThenBy(t => t.Index)
            .ToList();

        // Dự phòng nếu không có tag nào khớp bộ lọc nhưng allTags có dữ liệu
        if (prioritizedTags.Count == 0 && allTags.Count > 0)
        {
            prioritizedTags = allTags.Take(20).ToList();
        }

        var tagsArray = new JsonArray();
        foreach (var t in prioritizedTags)
        {
            var tagObj = new JsonObject
            {
                ["index"] = t.Index,
                ["name"] = t.Name,
                ["kind"] = t.Kind.ToString(),
                ["data_type"] = t.DataTypeText,
                ["alias"] = string.IsNullOrWhiteSpace(t.Alias) ? string.Empty : t.Alias,
                ["is_readonly"] = t.IsReadOnly,
                ["is_in_canvas"] = usedTagNames.Contains(t.Name)
            };

            // Trích xuất giá trị Live nếu có
            if (_runtimeStateStoreProvider != null)
            {
                var store = _runtimeStateStoreProvider();
                if (store != null)
                {
                    var snap = store.GetTag((ushort)t.Index);
                    if (snap != null)
                    {
                        tagObj["live_val"] = snap.RawValue;
                    }
                }
            }

            tagsArray.Add(tagObj);
        }
        root["tag_catalog"] = tagsArray;

        // 4. FBD Canvas Topology
        if (logicVM != null)
        {
            var nodesArray = new JsonArray();
            foreach (var n in logicVM.Nodes.Where(n => !n.IsGhost))
            {
                var nObj = new JsonObject
                {
                    ["id"] = n.Id,
                    ["type"] = n switch
                    {
                        InputNodeViewModel => "INPUT",
                        TriggerNodeViewModel => "TRIGGER",
                        GuardNodeViewModel => "GUARD",
                        ActionNodeViewModel => "ACTION",
                        TimerNodeViewModel => "TIMER",
                        CounterNodeViewModel => "COUNTER",
                        ScaleNodeViewModel => "SCALE",
                        _ => "UNKNOWN"
                    },
                    ["label"] = n.DisplayTitle,
                    ["pos_x"] = Math.Round(n.Location.X),
                    ["pos_y"] = Math.Round(n.Location.Y),
                    ["summary"] = n.SummaryText
                };

                // Thuộc tính Tag liên kết
                if (n is InputNodeViewModel inNode && inNode.Tag != null)
                    nObj["tag"] = inNode.Tag.Name;
                else if (n is ActionNodeViewModel actNode && actNode.TargetTag != null)
                    nObj["tag"] = actNode.TargetTag.Name;
                else if (n is GuardNodeViewModel grdNode && grdNode.GuardTag != null)
                    nObj["tag"] = grdNode.GuardTag.Name;

                nodesArray.Add(nObj);
            }
            root["canvas_nodes"] = nodesArray;

            var wiresArray = new JsonArray();
            foreach (var c in logicVM.Connections.Where(c => !c.IsGhost))
            {
                wiresArray.Add(new JsonObject
                {
                    ["source_node_id"] = c.Source.Node.Id,
                    ["source_port"] = c.Source.Title,
                    ["target_node_id"] = c.Target.Node.Id,
                    ["target_port"] = c.Target.Title
                });
            }
            root["canvas_connections"] = wiresArray;

            // Selected Nodes
            var selArray = new JsonArray();
            foreach (var sn in logicVM.SelectedNodes.Where(n => !n.IsGhost))
            {
                selArray.Add(sn.Id);
            }
            root["selected_node_ids"] = selArray;

            // 5. Diagnostics & Linter State
            var diagNode = new JsonObject
            {
                ["compile_state"] = logicVM.CompileState.ToString(),
                ["valid_rule_count"] = logicVM.ValidRuleCount,
                ["has_sim_gate_warning"] = logicVM.HasSimGateWarning
            };

            if (logicVM.HasSimGateWarning)
            {
                diagNode["sim_gate_warning"] = logicVM.SimGateWarning;
            }

            if (logicVM.Diagnostics != null && logicVM.Diagnostics.Count > 0)
            {
                var diagArray = new JsonArray();
                foreach (var d in logicVM.Diagnostics)
                {
                    diagArray.Add(new JsonObject
                    {
                        ["severity"] = d.Severity.ToString(),
                        ["message"] = d.Message,
                        ["rule_index"] = d.RuleIndex,
                        ["node_id"] = d.NodeId ?? string.Empty,
                        ["field"] = d.FieldName ?? string.Empty
                    });
                }
                diagNode["diagnostics"] = diagArray;
            }
            root["diagnostics_summary"] = diagNode;
        }

        return root;
    }
}
