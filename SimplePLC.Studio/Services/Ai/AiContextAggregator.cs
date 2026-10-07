using System.Text.Json.Nodes;
using SimplePLC.Application.Abstractions;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;

namespace SimplePLC.Studio.Services.Ai;

/// <summary>
/// Dịch vụ tổng hợp ngữ cảnh ngữ nghĩa chuyên sâu của dự án (Layer 1).
/// Đóng gói trạng thái: Toàn bộ danh mục Tag (124+ Tags), cấu trúc đồ thị Canvas (Geometry & Connections),
/// giá trị đo thực thời Live I/O, trạng thái kết nối phần cứng và mã lỗi Linter.
/// </summary>
public class AiContextAggregator
{
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
    /// </summary>
    public JsonObject BuildContextPayload()
    {
        var root = new JsonObject();

        // 1. Hardware & Connection
        var hwNode = new JsonObject
        {
            ["status"] = _hardwareStatusProvider?.Invoke() ?? "OFFLINE",
            ["target"] = "STM32F401RE / Modbus RTU V1.7",
            ["rule_capacity"] = "100 rules (32 bytes per rule)"
        };
        root["hardware_profile"] = hwNode;

        // 2. Full Tag Catalog
        var tagsArray = new JsonArray();
        var allTags = _tagsProvider() ?? Enumerable.Empty<TagModel>();
        foreach (var t in allTags)
        {
            var tagObj = new JsonObject
            {
                ["index"] = t.Index,
                ["name"] = t.Name,
                ["kind"] = t.Kind.ToString(),
                ["data_type"] = t.DataTypeText,
                ["alias"] = string.IsNullOrWhiteSpace(t.Alias) ? string.Empty : t.Alias,
                ["is_readonly"] = t.IsReadOnly
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

        // 3. FBD Canvas Topology
        var logicVM = _logicEditorProvider();
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
        }

        return root;
    }
}
