using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services.Ai;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử Triad Tests theo học thuyết IEC 61508-3 cho các tính năng mới của SynaptiX AI Copilot:
/// Khối Math ALU, Giao thức Edge/IoT, Wire Contract 32-Byte mới (MATH_OP=8, Flags, AuxParam),
/// và Kiểm toán an toàn chống chia cho 0 tĩnh.
/// </summary>
public class AiMathAndEdgeToolTests
{
    private List<TagModel> CreateStandardTagCatalog()
    {
        return new List<TagModel>
        {
            new() { Index = 0, Name = "DI0", Kind = TagKind.DiscreteInput, Alias = "Start" },
            new() { Index = 1, Name = "DI1", Kind = TagKind.DiscreteInput, Alias = "Stop NC" },
            new() { Index = 8, Name = "DO0", Kind = TagKind.DiscreteOutput, Alias = "Motor Pump" },
            new() { Index = 16, Name = "AI0", Kind = TagKind.AnalogInput, Alias = "Sensor A" },
            new() { Index = 17, Name = "AI1", Kind = TagKind.AnalogInput, Alias = "Sensor B" },
            new() { Index = 21, Name = "VFLAG0", Kind = TagKind.VirtualFlag, Alias = "Run Flag" },
            new() { Index = 24, Name = "VREG0", Kind = TagKind.VirtualRegister, Alias = "Calc Result" },
            new() { Index = 25, Name = "VREG1", Kind = TagKind.VirtualRegister, Alias = "Temp Var" },
            new() { Index = 40, Name = "AO0", Kind = TagKind.VirtualRegister, Alias = "Analog Out 1" }
        };
    }

    [Fact]
    public void AiToolDeclarations_ContainsMathAndComparator_StrictSchema()
    {
        var tools = AiToolDeclarations.GetGeminiFunctionDeclarations();

        // 1. Kiểm tra add_node chứa MATH và COMPARATOR trong node_type enum
        var addNodeTool = tools.FirstOrDefault(t => t?["name"]?.GetValue<string>() == "add_node");
        Assert.NotNull(addNodeTool);

        var nodeTypeEnum = addNodeTool["parameters"]?["properties"]?["node_type"]?["enum"]?.AsArray();
        Assert.NotNull(nodeTypeEnum);

        var types = nodeTypeEnum.Select(e => e?.GetValue<string>()).ToList();
        Assert.Contains("MATH", types);
        Assert.Contains("COMPARATOR", types);

        // 2. Kiểm tra configure_math schema
        var configMathTool = tools.FirstOrDefault(t => t?["name"]?.GetValue<string>() == "configure_math");
        Assert.NotNull(configMathTool);

        var mathParams = configMathTool["parameters"]?["properties"]?.AsObject();
        Assert.NotNull(mathParams);
        Assert.True(mathParams.ContainsKey("operator"));
        Assert.True(mathParams.ContainsKey("type_mode"));
        Assert.True(mathParams.ContainsKey("output_tag"));
        Assert.True(mathParams.ContainsKey("pin1_tag_or_value"));
        Assert.True(mathParams.ContainsKey("pin2_tag_or_value"));
    }

    [Fact]
    public void AiSafetyGate_RejectsMathNode_WritingToReadOnlyDI_Tag()
    {
        var tags = CreateStandardTagCatalog();
        var tx = new DraftGraphTransaction();

        // LLM cố gắng thêm khối MATH ghi kết quả vào ngõ vào chỉ đọc DI0
        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "math_illegal",
            ["node_type"] = "MATH",
            ["custom_label"] = "Illegal Math",
            ["position_x"] = 100,
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

        tx.ApplyToolCall(new AiToolCall("configure_math", new JsonObject
        {
            ["node_id"] = "math_illegal",
            ["operator"] = "ADD",
            ["type_mode"] = "Auto",
            ["output_tag"] = "DI0",
            ["pin1_tag_or_value"] = "AI0",
            ["pin1_is_constant"] = false,
            ["pin2_tag_or_value"] = "AI1",
            ["pin2_is_constant"] = false,
            ["clamp_min"] = 0,
            ["clamp_max"] = 100
        }));

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.False(result.IsValid);
        Assert.Equal("SPLC-TAG-ERR-READONLY", result.ErrorCode);
        Assert.Contains("DI0", result.ErrorMessage);
    }

    [Fact]
    public void AiSafetyGate_RejectsMathNode_DivisionByZeroConstant()
    {
        var tags = CreateStandardTagCatalog();
        var tx = new DraftGraphTransaction();

        // Khối Math thực hiện phép DIV với hằng số 0
        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "math_div_zero",
            ["node_type"] = "MATH",
            ["custom_label"] = "Div by Zero",
            ["position_x"] = 100,
            ["position_y"] = 100,
            ["tag_name"] = "VREG0",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        tx.ApplyToolCall(new AiToolCall("configure_math", new JsonObject
        {
            ["node_id"] = "math_div_zero",
            ["operator"] = "DIV",
            ["type_mode"] = "Auto",
            ["output_tag"] = "VREG0",
            ["pin1_tag_or_value"] = "AI0",
            ["pin1_is_constant"] = false,
            ["pin2_tag_or_value"] = "0",
            ["pin2_is_constant"] = true,
            ["clamp_min"] = 0,
            ["clamp_max"] = 100
        }));

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.False(result.IsValid);
        Assert.Equal("SPLC-MATH-ERR-DIVZERO", result.ErrorCode);
        Assert.Contains("hằng số 0", result.ErrorMessage);
    }

    [Fact]
    public void AiSafetyGate_AllowsMathNode_ValidConfiguration()
    {
        var tags = CreateStandardTagCatalog();
        var tx = new DraftGraphTransaction();

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "math_valid",
            ["node_type"] = "MATH",
            ["custom_label"] = "Valid Math ADD",
            ["position_x"] = 100,
            ["position_y"] = 100,
            ["tag_name"] = "VREG0",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        tx.ApplyToolCall(new AiToolCall("configure_math", new JsonObject
        {
            ["node_id"] = "math_valid",
            ["operator"] = "ADD",
            ["type_mode"] = "Auto",
            ["output_tag"] = "VREG0",
            ["pin1_tag_or_value"] = "AI0",
            ["pin1_is_constant"] = false,
            ["pin2_tag_or_value"] = "AI1",
            ["pin2_is_constant"] = false,
            ["clamp_min"] = 0,
            ["clamp_max"] = 100
        }));

        var result = AiSafetyGate.Validate(tx, tags);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void AiContextAggregator_CollectsTagsFromMathAndEdgeNodes()
    {
        var tags = CreateStandardTagCatalog();
        var tagCatalogVM = new TagCatalogViewModel();
        var logicVM = new LogicEditorViewModel(tagCatalogVM, uiDispatcher: a => a());

        // Thêm một khối MathNode trên Canvas
        var mathNode = new MathNodeViewModel(tags.First(t => t.Name == "VREG0"));
        logicVM.Nodes.Add(mathNode);

        var aggregator = new AiContextAggregator(
            () => tags,
            () => logicVM,
            null,
            () => "CONNECTED_ONLINE");

        var payload = aggregator.BuildContextPayload();

        Assert.NotNull(payload);
        var tagsArray = payload["tag_catalog"]?.AsArray();
        Assert.NotNull(tagsArray);

        // VREG0 phải có is_in_canvas = true vì được gắn vào MathNode
        var vreg0Json = tagsArray.FirstOrDefault(t => t?["name"]?.GetValue<string>() == "VREG0");
        Assert.NotNull(vreg0Json);
        Assert.True(vreg0Json["is_in_canvas"]?.GetValue<bool>());

        // Kiểm tra hardware_profile chứa thông tin Modbus Slave Fixed Map mới
        var hwProfile = payload["hardware_profile"]?.AsObject();
        Assert.NotNull(hwProfile);
        Assert.Contains("SPLC-SPEC-SLAVE-001", hwProfile["target"]?.GetValue<string>() ?? string.Empty);
    }

    [Fact]
    public void AiExplanationSynthesizer_GeneratesAccurateMathAluDescription_And32ByteJsonRules()
    {
        var tx = new DraftGraphTransaction();

        tx.ApplyToolCall(new AiToolCall("add_node", new JsonObject
        {
            ["node_id"] = "math_calc",
            ["node_type"] = "MATH",
            ["custom_label"] = "Difference Calculator",
            ["position_x"] = 100,
            ["position_y"] = 100,
            ["tag_name"] = "VREG0",
            ["trigger_type"] = "NONE",
            ["compare_op"] = "NONE",
            ["threshold_lo"] = 0,
            ["threshold_hi"] = 0,
            ["debounce_ms"] = 0,
            ["action_type"] = "NONE",
            ["action_param"] = 0
        }));

        tx.ApplyToolCall(new AiToolCall("configure_math", new JsonObject
        {
            ["node_id"] = "math_calc",
            ["operator"] = "SUB",
            ["type_mode"] = "ForceReal",
            ["output_tag"] = "VREG0",
            ["pin1_tag_or_value"] = "AI0",
            ["pin1_is_constant"] = false,
            ["pin2_tag_or_value"] = "AI1",
            ["pin2_is_constant"] = false,
            ["clamp_min"] = 0,
            ["clamp_max"] = 100
        }));

        string explanation = AiExplanationSynthesizer.Synthesize(tx, "Tính hiệu số giữa AI0 và AI1");

        // 1. Phải có thuyết minh nguyên lý
        Assert.Contains("MATH ALU (SUB)", explanation);
        Assert.Contains("VREG0 = AI0 - AI1", explanation);

        // 2. Phải có phòng vệ số học
        Assert.Contains("Phòng vệ số học", explanation);

        // 3. Phải sinh rule theo chuẩn Wire Contract 32-byte mới với action_type = MATH_OP
        var rules = AiRuleParser.ExtractRules(explanation, out _);
        Assert.Single(rules);
        var rule = rules[0];
        Assert.Equal("MATH_OP", rule.ActionType);
        Assert.Equal("SUB", rule.CompareOp);
        Assert.Equal("AI0", rule.InputTag);
        Assert.Equal("AI1", rule.GuardTag);
        Assert.Equal("VREG0", rule.ActionTag);
        Assert.True(rule.IsFloat);
    }

    [Fact]
    public void AiRuleSpecModel_SerializesAndDeserializes_MathOpAnd32ByteFields()
    {
        string json = """
        [
          {
            "narrative": "Tính lưu lượng từ chênh áp",
            "input_tag": "AI0",
            "trigger": "ON_CHANGE",
            "compare_op": "MUL",
            "threshold_lo": 0,
            "threshold_hi": 0,
            "for_ms": 0,
            "guard_tag": "NONE",
            "action_tag": "VREG0",
            "action_type": "MATH_OP",
            "param": 250,
            "aux_param": 1000,
            "is_float": true
          }
        ]
        """;

        var rules = AiRuleParser.ExtractRules(json, out var clean);

        Assert.Single(rules);
        var r = rules[0];
        Assert.Equal("MATH_OP", r.ActionType);
        Assert.Equal("MUL", r.CompareOp);
        Assert.Equal(250, r.Param);
        Assert.Equal(1000, r.AuxParam);
        Assert.True(r.IsFloat);
    }
}
