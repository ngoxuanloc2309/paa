using System.Text.Json.Nodes;

namespace SimplePLC.Studio.Services.Ai;

/// <summary>
/// Đại diện cho một thao tác gọi công cụ (Tool Call) do mô hình AI phát ra.
/// </summary>
public record AiToolCall(string Name, JsonObject Arguments);

/// <summary>
/// Kho lưu trữ các schemas định nghĩa công cụ (Function Declarations) cho Gemini & OpenAI API.
/// Tuân thủ quy chuẩn Strict Mode.
/// </summary>
public static class AiToolDeclarations
{
    public static JsonArray GetGeminiFunctionDeclarations()
    {
        return new JsonArray
        {
            BuildAddNodeSchema(),
            BuildRemoveNodeSchema(),
            BuildConnectWiresSchema(),
            BuildDisconnectWireSchema(),
            BuildConfigureTimerCounterSchema(),
            BuildConfigureScaleSchema(),
            BuildRenameTagSchema(),
            BuildAutoLayoutGraphSchema(),
            BuildDiagnoseLogicBugsSchema()
        };
    }

    private static JsonObject BuildAddNodeSchema()
    {
        return new JsonObject
        {
            ["name"] = "add_node",
            ["description"] = "Tạo một khối logic mới trên FBD Canvas của SimplePLC Studio với tọa độ và cấu hình cụ thể.",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["node_id"] = new JsonObject { ["type"] = "string", ["description"] = "Mã định danh duy nhất của khối (vd: 'node_in_start', 'node_act_pump')." },
                    ["node_type"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Loại khối logic cần tạo.",
                        ["enum"] = new JsonArray { "INPUT", "TRIGGER", "GUARD", "ACTION", "TIMER", "COUNTER", "SCALE" }
                    },
                    ["custom_label"] = new JsonObject { ["type"] = "string", ["description"] = "Nhãn gợi nhớ hiển thị trên đỉnh của khối." },
                    ["position_x"] = new JsonObject { ["type"] = "number", ["description"] = "Tọa độ X trên canvas (bước nhảy lưới tiêu chuẩn 240px)." },
                    ["position_y"] = new JsonObject { ["type"] = "number", ["description"] = "Tọa độ Y trên canvas." },
                    ["tag_name"] = new JsonObject { ["type"] = "string", ["description"] = "Mã Tag liên kết (chỉ áp dụng cho INPUT, GUARD, ACTION). Vd: 'DI0', 'DO1', 'VFLAG0', hoặc 'NONE'." },
                    ["trigger_type"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Kiểu kích hoạt (chỉ áp dụng cho TRIGGER).",
                        ["enum"] = new JsonArray { "NONE", "ON_CHANGE", "ON_RISE", "ON_FALL", "INTERVAL" }
                    },
                    ["compare_op"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Toán tử so sánh ngưỡng.",
                        ["enum"] = new JsonArray { "NONE", "EQ", "NEQ", "GT", "LT", "GTE", "LTE", "BETWEEN" }
                    },
                    ["threshold_lo"] = new JsonObject { ["type"] = "integer", ["description"] = "Ngưỡng so sánh dưới. Mặc định 0." },
                    ["threshold_hi"] = new JsonObject { ["type"] = "integer", ["description"] = "Ngưỡng so sánh trên (chỉ dùng khi BETWEEN). Mặc định 0." },
                    ["debounce_ms"] = new JsonObject { ["type"] = "integer", ["description"] = "Thời gian lọc nhiễu dội phím hoặc for_ms (ms)." },
                    ["action_type"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Hành vi thực thi (chỉ cho ACTION).",
                        ["enum"] = new JsonArray { "NONE", "SET_TAG", "TOGGLE_TAG", "INC_COUNTER", "ADD_TAG", "SCALE_TAG" }
                    },
                    ["action_param"] = new JsonObject { ["type"] = "integer", ["description"] = "Tham số tác vụ (1: bật, 0: tắt, +1: đếm)." }
                },
                ["required"] = new JsonArray
                {
                    "node_id", "node_type", "custom_label", "position_x", "position_y",
                    "tag_name", "trigger_type", "compare_op", "threshold_lo",
                    "threshold_hi", "debounce_ms", "action_type", "action_param"
                }
            }
        };
    }

    private static JsonObject BuildRemoveNodeSchema()
    {
        return new JsonObject
        {
            ["name"] = "remove_node",
            ["description"] = "Xóa một khối logic khỏi Canvas dựa trên Node ID, đồng thời tự động thu hồi tất cả các dây nối liên kết.",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["node_id"] = new JsonObject { ["type"] = "string", ["description"] = "Định danh duy nhất của khối cần xóa." },
                    ["reason"] = new JsonObject { ["type"] = "string", ["description"] = "Lý do kỹ thuật giải thích việc loại bỏ khối này." }
                },
                ["required"] = new JsonArray { "node_id", "reason" }
            }
        };
    }

    private static JsonObject BuildConnectWiresSchema()
    {
        return new JsonObject
        {
            ["name"] = "connect_wires",
            ["description"] = "Tạo đường dây nối truyền tín hiệu giữa cổng xuất (Out) của một khối nguồn tới cổng nhập (In) của một khối đích.",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["connection_id"] = new JsonObject { ["type"] = "string", ["description"] = "Mã định danh duy nhất cho đường dây mới." },
                    ["source_node_id"] = new JsonObject { ["type"] = "string", ["description"] = "Mã định danh của khối nguồn phát." },
                    ["source_connector_title"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Tên cổng xuất của khối nguồn ('Out', 'Q', hoặc 'OUT').",
                        ["enum"] = new JsonArray { "Out", "Q", "OUT" }
                    },
                    ["target_node_id"] = new JsonObject { ["type"] = "string", ["description"] = "Mã định danh của khối đích nhận." },
                    ["target_connector_title"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Tên cổng nhập của khối đích ('In', 'CU', 'CD', 'R', 'IN').",
                        ["enum"] = new JsonArray { "In", "CU", "CD", "R", "IN" }
                    }
                },
                ["required"] = new JsonArray { "connection_id", "source_node_id", "source_connector_title", "target_node_id", "target_connector_title" }
            }
        };
    }

    private static JsonObject BuildDisconnectWireSchema()
    {
        return new JsonObject
        {
            ["name"] = "disconnect_wire",
            ["description"] = "Gỡ bỏ một đường dây kết nối cụ thể giữa hai khối trên Canvas.",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["connection_id"] = new JsonObject { ["type"] = "string", ["description"] = "Mã định danh của dây cần gỡ hoặc 'AUTO'." },
                    ["source_node_id"] = new JsonObject { ["type"] = "string", ["description"] = "ID khối nguồn phát." },
                    ["target_node_id"] = new JsonObject { ["type"] = "string", ["description"] = "ID khối đích nhận." }
                },
                ["required"] = new JsonArray { "connection_id", "source_node_id", "target_node_id" }
            }
        };
    }

    private static JsonObject BuildConfigureTimerCounterSchema()
    {
        return new JsonObject
        {
            ["name"] = "configure_timer_counter",
            ["description"] = "Cấu hình chi tiết tham số vận hành cho khối Macro Timer (TON/TOF/TP) hoặc Counter (CTU/CTD) hiện có trên Canvas.",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["node_id"] = new JsonObject { ["type"] = "string", ["description"] = "ID của khối Timer hoặc Counter cần cấu hình." },
                    ["mode"] = new JsonObject
                    {
                        ["type"] = "string",
                        ["description"] = "Chế độ định thời hoặc đếm.",
                        ["enum"] = new JsonArray { "TON", "TOF", "TP", "CTU", "CTD" }
                    },
                    ["preset_value"] = new JsonObject { ["type"] = "integer", ["description"] = "Thời gian đặt PT (ms cho Timer) hoặc giá trị đếm PV (cho Counter)." },
                    ["input_tag_name"] = new JsonObject { ["type"] = "string", ["description"] = "Mã Tag đầu vào kích hoạt khối (vd: 'VFLAG0', 'DI0', 'NONE')." },
                    ["cv_tag_name"] = new JsonObject { ["type"] = "string", ["description"] = "Tag lưu giá trị đếm hiện thời CV (cho Counter)." },
                    ["reset_tag_name"] = new JsonObject { ["type"] = "string", ["description"] = "Tag tín hiệu xóa bộ đếm Reset." },
                    ["output_tag_name"] = new JsonObject { ["type"] = "string", ["description"] = "Tag ngõ ra chỉ thị đạt ngưỡng Q (vd: 'DO0', 'VFLAG1')." }
                },
                ["required"] = new JsonArray { "node_id", "mode", "preset_value", "input_tag_name", "cv_tag_name", "reset_tag_name", "output_tag_name" }
            }
        };
    }

    private static JsonObject BuildConfigureScaleSchema()
    {
        return new JsonObject
        {
            ["name"] = "configure_scale",
            ["description"] = "Cấu hình tham số biến đổi tuyến tính tín hiệu tương tự (Analog Linear Scaling: y = Gain * x + Offset) và giới hạn kẹp an toàn cho khối SCALE.",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["node_id"] = new JsonObject { ["type"] = "string", ["description"] = "ID của khối SCALE cần cấu hình." },
                    ["gain"] = new JsonObject { ["type"] = "number", ["description"] = "Hệ số Gain." },
                    ["offset"] = new JsonObject { ["type"] = "number", ["description"] = "Hệ số Offset." },
                    ["unit"] = new JsonObject { ["type"] = "string", ["description"] = "Đơn vị vật lý kỹ thuật hiển thị ('bar', '°C', 'RPM')." },
                    ["decimal_places"] = new JsonObject { ["type"] = "integer", ["description"] = "Số chữ số thập phân hiển thị (mặc định 1)." },
                    ["is_clamped"] = new JsonObject { ["type"] = "boolean", ["description"] = "Kích hoạt giới hạn kẹp an toàn." },
                    ["clamp_min"] = new JsonObject { ["type"] = "number", ["description"] = "Giá trị kẹp dưới an toàn." },
                    ["clamp_max"] = new JsonObject { ["type"] = "number", ["description"] = "Giá trị kẹp trên an toàn." }
                },
                ["required"] = new JsonArray { "node_id", "gain", "offset", "unit", "decimal_places", "is_clamped", "clamp_min", "clamp_max" }
            }
        };
    }

    private static JsonObject BuildRenameTagSchema()
    {
        return new JsonObject
        {
            ["name"] = "rename_tag",
            ["description"] = "Cập nhật bí danh gợi nhớ (Alias) hoặc mô tả kỹ thuật cho một Tag trong danh bạ Tag Catalog.",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["tag_name"] = new JsonObject { ["type"] = "string", ["description"] = "Mã Tag phần cứng hệ thống (vd: 'DI0', 'DO1', 'VFLAG0')." },
                    ["new_alias"] = new JsonObject { ["type"] = "string", ["description"] = "Tên bí danh gợi nhớ mới." },
                    ["description"] = new JsonObject { ["type"] = "string", ["description"] = "Mô tả chi tiết vị trí lắp đặt hoặc công năng." }
                },
                ["required"] = new JsonArray { "tag_name", "new_alias", "description" }
            }
        };
    }

    private static JsonObject BuildAutoLayoutGraphSchema()
    {
        return new JsonObject
        {
            ["name"] = "auto_layout_graph",
            ["description"] = "Tự động sắp xếp vị trí các khối trên Canvas theo cấu trúc phân tầng công nghiệp (Sugiyama Layered Layout).",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["diagram_id"] = new JsonObject { ["type"] = "string", ["description"] = "Định danh sơ đồ hoặc 'ALL'." },
                    ["horizontal_spacing"] = new JsonObject { ["type"] = "number", ["description"] = "Khoảng cách giữa các cột (mặc định 260)." },
                    ["vertical_spacing"] = new JsonObject { ["type"] = "number", ["description"] = "Khoảng cách giữa các hàng (mặc định 120)." }
                },
                ["required"] = new JsonArray { "diagram_id", "horizontal_spacing", "vertical_spacing" }
            }
        };
    }

    private static JsonObject BuildDiagnoseLogicBugsSchema()
    {
        return new JsonObject
        {
            ["name"] = "diagnose_logic_bugs",
            ["description"] = "Kích hoạt bộ kiểm tra tĩnh toàn diện của SimplePLC (Graph Grammar, Xung đột ghi Write Conflict, Chu trình lặp Cyclic Dependency) để phát hiện lỗi logic tiềm ẩn.",
            ["parameters"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["check_write_conflicts"] = new JsonObject { ["type"] = "boolean", ["description"] = "Kiểm tra xung đột ghi đè nhiều Action vào 1 Tag." },
                    ["check_cyclic_loops"] = new JsonObject { ["type"] = "boolean", ["description"] = "Kiểm tra chu trình khép kín gây bất định chu kỳ quét." },
                    ["auto_propose_fixes"] = new JsonObject { ["type"] = "boolean", ["description"] = "Tự động đề xuất phương án sửa chữa." }
                },
                ["required"] = new JsonArray { "check_write_conflicts", "check_cyclic_loops", "auto_propose_fixes" }
            }
        };
    }
}
