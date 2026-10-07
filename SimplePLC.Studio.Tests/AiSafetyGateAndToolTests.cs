using System.Text.Json.Nodes;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services.Ai;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class AiSafetyGateAndToolTests
{
    private List<TagModel> CreateStandardTagCatalog()
    {
        return new List<TagModel>
        {
            new() { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput, Alias = "Start" },
            new() { Index = 1, Name = "DI1", Kind = TagKind.DiscreteInput, Alias = "Stop NC" },
            new() { Index = 2, Name = "DI2", Kind = TagKind.DiscreteInput, Alias = "E-Stop NC" },
            new() { Index = 8, Name = "DO0", Kind = TagKind.DiscreteOutput, Alias = "Motor Pump" },
            new() { Index = 16, Name = "AI0", Kind = TagKind.AnalogInput, Alias = "Pressure" },
            new() { Index = 21, Name = "VFLAG0", Kind = TagKind.VirtualFlag, Alias = "Run Flag" }
        };
    }

    [Fact]
    public void AiSafetyGate_RejectsActionWritingToReadOnlyDI_Tag()
    {
        var tags = CreateStandardTagCatalog();
        var tx = new DraftGraphTransaction();

        // LLM cố gắng thêm action ghi vào DI0 (ngõ vào vật lý)
        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "act_illegal",
            ["node_type"] = "ACTION",
            ["custom_label"] = "Write to DI0",
            ["position_x"] = 100,
            ["position_y"] = 100,
            ["tag_name"] = "DI0",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "SET_TAG",
            ["action_param"] = 1
        }));

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.False(result.IsValid);
        Assert.Equal("SPLC-TAG-ERR-READONLY", result.ErrorCode);
    }

    [Fact]
    public void AiSafetyGate_RejectsActionAsWireSource_SPLC_GRAPH_002()
    {
        var tags = CreateStandardTagCatalog();
        var tx = new DraftGraphTransaction();

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "act1",
            ["node_type"] = "ACTION",
            ["custom_label"] = "Set Flag",
            ["position_x"] = 100,
            ["position_y"] = 100,
            ["tag_name"] = "VFLAG0",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "SET_TAG",
            ["action_param"] = 1
        }));

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "act2",
            ["node_type"] = "ACTION",
            ["custom_label"] = "Set DO0",
            ["position_x"] = 300,
            ["position_y"] = 100,
            ["tag_name"] = "DO0",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "SET_TAG",
            ["action_param"] = 1
        }));

        // Nối từ Action -> Action
        tx.ApplyToolCall(new AiToolCall("connect_wires", new JsonObject
        {
            ["connection_id"] = "illegal_wire",
            ["source_node_id"] = "act1",
            ["source_connector_title"] = "Out",
            ["target_node_id"] = "act2",
            ["target_connector_title"] = "In"
        }));

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.False(result.IsValid);
        Assert.Equal("SPLC-GRAPH-002", result.ErrorCode);
    }

    [Fact]
    public void AiSafetyGate_RejectsChainedGuards_SPLC_GRAPH_007()
    {
        var tags = CreateStandardTagCatalog();
        var tx = new DraftGraphTransaction();

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "grd1",
            ["node_type"] = "GUARD",
            ["custom_label"] = "Guard 1",
            ["position_x"] = 100,
            ["position_y"] = 100,
            ["tag_name"] = "DI1",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "EQ",
            ["threshold_lo"] = 1,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "grd2",
            ["node_type"] = "GUARD",
            ["custom_label"] = "Guard 2",
            ["position_x"] = 300,
            ["position_y"] = 100,
            ["tag_name"] = "DI2",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "EQ",
            ["threshold_lo"] = 1,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        // Nối Guard 1 -> Guard 2
        tx.ApplyToolCall(new AiToolCall("connect_wires", new JsonObject
        {
            ["connection_id"] = "chained_guard_wire",
            ["source_node_id"] = "grd1",
            ["source_connector_title"] = "Out",
            ["target_node_id"] = "grd2",
            ["target_connector_title"] = "In"
        }));

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.False(result.IsValid);
        Assert.Equal("SPLC-GRAPH-007", result.ErrorCode);
    }

    [Fact]
    public void AiSafetyGate_AcceptsValidPipeline_StartLatchWithGuard()
    {
        var tags = CreateStandardTagCatalog();
        var tx = new DraftGraphTransaction();

        // 1. Input DI0
        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "in_start",
            ["node_type"] = "INPUT",
            ["custom_label"] = "Start Button",
            ["position_x"] = 0,
            ["position_y"] = 100,
            ["tag_name"] = "DI0",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        // 2. Trigger ON_RISE
        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "trg_rise",
            ["node_type"] = "TRIGGER",
            ["custom_label"] = "Rise Edge",
            ["position_x"] = 240,
            ["position_y"] = 100,
            ["tag_name"] = "NONE",
            ["trigger_type"] = "ON_RISE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 30,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        // 3. Guard E-Stop DI2 == 1
        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "grd_estop",
            ["node_type"] = "GUARD",
            ["custom_label"] = "E-Stop Safe",
            ["position_x"] = 480,
            ["position_y"] = 100,
            ["tag_name"] = "DI2",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "EQ",
            ["threshold_lo"] = 1,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        // 4. Action VFLAG0 = 1
        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "act_latch",
            ["node_type"] = "ACTION",
            ["custom_label"] = "Latch Run Flag",
            ["position_x"] = 720,
            ["position_y"] = 100,
            ["tag_name"] = "VFLAG0",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "SET_TAG",
            ["action_param"] = 1
        }));

        // Wires
        tx.ApplyToolCall(new AiToolCall("connect_wires", new JsonObject
        {
            ["connection_id"] = "w1",
            ["source_node_id"] = "in_start",
            ["source_connector_title"] = "Out",
            ["target_node_id"] = "trg_rise",
            ["target_connector_title"] = "In"
        }));

        tx.ApplyToolCall(new AiToolCall("connect_wires", new JsonObject
        {
            ["connection_id"] = "w2",
            ["source_node_id"] = "trg_rise",
            ["source_connector_title"] = "Out",
            ["target_node_id"] = "grd_estop",
            ["target_connector_title"] = "In"
        }));

        tx.ApplyToolCall(new AiToolCall("connect_wires", new JsonObject
        {
            ["connection_id"] = "w3",
            ["source_node_id"] = "grd_estop",
            ["source_connector_title"] = "Out",
            ["target_node_id"] = "act_latch",
            ["target_connector_title"] = "In"
        }));

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }
}
