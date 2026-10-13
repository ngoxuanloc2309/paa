using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Executable Specification Test Suite.
/// Kiểm thử độc lập dựa trên tài liệu đặc tả kỹ thuật và tiêu chuẩn tự động hóa công nghiệp:
/// 1. Tiêu chuẩn an toàn điện IEC 60204-1: Mạch tự giữ an toàn Start/Stop (Stop NC Fail-Safe).
/// 2. Hợp đồng Platform Wire Contract V1.9 / V2.0: Bất biến bảo vệ chống ghi vào vùng ngõ vào chỉ đọc (Read-Only).
/// 3. Tính độc lập đa phần cứng: Đảm bảo cùng một biểu đồ logic hoạt động chính xác trên cả Standard (8DI) lẫn Compact (4DI).
/// </summary>
public class SpecificationBasedCompilationTests
{
    private readonly RuleCompiler _compiler = new();

    // =========================================================================
    // 1. SPECIFICATION: IEC 60204-1 Safety Start/Stop Circuit on Dynamic Hardware
    // =========================================================================

    [Theory]
    [InlineData("Standard 8DI/8DO", 8, 8, 4, 8)]  // Standard: DI0..DI7 -> DO0 có index 8
    [InlineData("Compact 4DI/4DO",  4, 4, 2, 4)]  // Compact:  DI0..DI3 -> DO0 có index 4
    [Trait("Specification", "IEC-60204-1-START-STOP")]
    public void IEC60204_SafetyStartStopCircuit_CompilesCorrectly_OnAnyDynamicHardware(
        string profileName,
        ushort diCount,
        ushort doCount,
        ushort aiCount,
        ushort expectedDo0Index)
    {
        // GIVEN: Phần cứng định nghĩa theo DeviceResourceInfo của MCU
        var resources = new ProductResourceProfile(
            DigitalInputs: diCount,
            DigitalOutputs: doCount,
            AnalogInputs: aiCount,
            VirtualFlags: 16,
            VirtualRegisters: 16,
            RetentiveRegisters: 8,
            Counters: 4);

        var product = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 100,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources);

        // Lấy đúng chân từ Hardware Definition theo Tên chuẩn
        var di0Start = product.Tags.First(t => t.Name == "DI0"); // Start Button (NO)
        var di1Stop  = product.Tags.First(t => t.Name == "DI1"); // Stop Button (NC Fail-Safe)
        var do0Pump  = product.Tags.First(t => t.Name == "DO0"); // Motor / Pump Contactor

        // Khẳng định chỉ số DO0 độc lập theo đặc tả
        Assert.Equal(expectedDo0Index, do0Pump.TagIndex);

        // WHEN: Xây dựng đồ thị logic FBD theo chuẩn 4-Stage Pipeline của SimplePLC
        // Rule 1: [DI0 Start] ON_RISE + [DI1 Stop NC] == 1 -> DO0 = 1 (Bật Bơm)
        // Rule 2: [DI1 Stop NC] ON_FALL (nhấn stop hoặc đứt dây) -> DO0 = 0 (Ngắt Bơm tức thì)
        var graph = new LogicGraph();

        // Nodes Rule 1
        graph.Nodes.Add(LogicNode.CreateInput("IN_START", di0Start.TagIndex, "Start_NO"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_START", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateGuard("GRD_SAFETY", di1Stop.TagIndex, negated: false, "Stop_Healthy_NC"));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_RUN", do0Pump.TagIndex, ActionKind.SetTag, 1, executionOrder: 0, "Pump_ON"));

        // Edges Rule 1
        graph.Edges.Add(new LogicEdge("IN_START", "TR_START"));
        graph.Edges.Add(new LogicEdge("TR_START", "GRD_SAFETY"));
        graph.Edges.Add(new LogicEdge("GRD_SAFETY", "ACT_RUN"));

        // Nodes Rule 2
        graph.Nodes.Add(LogicNode.CreateInput("IN_STOP", di1Stop.TagIndex, "Stop_NC"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_STOP_FALL", TriggerKind.OnFall));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_TRIP", do0Pump.TagIndex, ActionKind.SetTag, 0, executionOrder: 1, "Pump_OFF_Safe"));

        // Edges Rule 2
        graph.Edges.Add(new LogicEdge("IN_STOP", "TR_STOP_FALL"));
        graph.Edges.Add(new LogicEdge("TR_STOP_FALL", "ACT_TRIP"));

        // THEN: Biên dịch đồ thị
        var result = _compiler.Compile(graph, product);

        Assert.True(result.IsSuccess, $"Compilation failed on {profileName}: {string.Join("; ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.Rules.Count);

        // Xác thực Rule 1: Khởi động bơm
        var r1 = result.Program.Rules[0];
        Assert.Equal(di0Start.TagIndex, r1.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnRise, r1.Trigger.Type);
        Assert.True(r1.Guard.HasGuard);
        Assert.Equal(di1Stop.TagIndex, r1.Guard.Tag!.TagIndex);
        Assert.Equal(do0Pump.TagIndex, r1.Action.TargetTag.TagIndex);
        Assert.Equal(1, r1.Action.Parameter);

        // Xác thực Rule 2: Ngắt an toàn
        var r2 = result.Program.Rules[1];
        Assert.Equal(di1Stop.TagIndex, r2.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnFall, r2.Trigger.Type);
        Assert.False(r2.Guard.HasGuard);
        Assert.Equal(do0Pump.TagIndex, r2.Action.TargetTag.TagIndex);
        Assert.Equal(0, r2.Action.Parameter);
    }

    // =========================================================================
    // 2. SPECIFICATION: Read-Only Protection Invariant
    // Không cho phép bất kỳ hành vi ghi nào vào vùng DiscreteInput hoặc AnalogInput
    // =========================================================================

    [Fact]
    [Trait("Specification", "SPLC-SECURITY-READONLY-INVARIANT")]
    public void ReadOnlyInputTag_CannotBeActionTarget_PerSecuritySpecification()
    {
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var di0 = product.Tags.First(t => t.Kind == TagKind.DiscreteInput);

        // Cố tình tạo một Action ghi đè vào chân DI0 chỉ đọc
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN0", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ILLEGAL_ACT", di0.TagIndex, ActionKind.SetTag, 1));

        graph.Edges.Add(new LogicEdge("IN0", "TR0"));
        graph.Edges.Add(new LogicEdge("TR0", "ILLEGAL_ACT"));

        var result = _compiler.Compile(graph, product);

        // Bộ biên dịch bắt buộc phải từ chối theo nguyên tắc bảo vệ phần cứng
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Severity == DiagnosticSeverity.Error && d.Message.Contains("read-only"));
    }

    // =========================================================================
    // 3. SPECIFICATION: SPLC-DOC-SPEC-V19 MaxRules Boundary Invariant
    // Khi số lượng rules vượt quá giới hạn phần cứng (product.MaxRules), compiler
    // bắt buộc phải từ chối với mã lỗi ErrCapacityExceeded (SPLC-COMP-005).
    // =========================================================================

    [Fact]
    [Trait("Specification", "SPLC-DOC-SPEC-V19-MAX-RULES-CAPACITY")]
    public void ExceedingProductMaxRules_ReturnsErrCapacityExceeded_PerContractSpecification()
    {
        // GIVEN: Cấu hình phần cứng có MaxRules = 10
        var resources = ProductResourceProfile.DefaultRemoteIo;
        var product = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 10,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources);

        var di0 = product.Tags.First(t => t.Kind == TagKind.DiscreteInput);
        var targetTags = product.Tags.Where(t => t.Kind is TagKind.DiscreteOutput or TagKind.VirtualFlag).Take(11).ToList();

        // WHEN: Tạo 11 chains độc lập ghi vào 11 tags khác nhau (vượt quá giới hạn MaxRules = 10)
        var graph = new LogicGraph();
        for (int i = 0; i < 11; i++)
        {
            string inId = $"IN_{i}";
            string trId = $"TR_{i}";
            string actId = $"ACT_{i}";

            graph.Nodes.Add(LogicNode.CreateInput(inId, di0.TagIndex));
            graph.Nodes.Add(LogicNode.CreateTrigger(trId, TriggerKind.OnRise));
            graph.Nodes.Add(LogicNode.CreateAction(actId, targetTags[i].TagIndex, ActionKind.SetTag, 1));

            graph.Edges.Add(new LogicEdge(inId, trId));
            graph.Edges.Add(new LogicEdge(trId, actId));
        }

        // THEN: Compiler phải từ chối và phát hiện ErrCapacityExceeded
        var result = _compiler.Compile(graph, product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrCapacityExceeded);
    }

    // =========================================================================
    // 4. SPECIFICATION: SPLC-DOC-SPEC-V19 Acyclic Graph Grammar Invariant
    // Đồ thị điều khiển không được phép chứa chu trình lặp (Cyclic Dependency)
    // =========================================================================

    [Fact]
    [Trait("Specification", "SPLC-DOC-SPEC-V19-ACYCLIC-GRAMMAR")]
    public void CyclicGraphGrammar_IsRejectedDeterministically_PerGraphSpecification()
    {
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var di0 = product.Tags.First(t => t.Kind == TagKind.DiscreteInput);
        var do0 = product.Tags.First(t => t.Kind == TagKind.DiscreteOutput);

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN0", di0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR2", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT0", do0.TagIndex, ActionKind.SetTag, 1));

        // Tạo vòng lặp TR1 -> TR2 -> TR1
        graph.Edges.Add(new LogicEdge("IN0", "TR1"));
        graph.Edges.Add(new LogicEdge("TR1", "TR2"));
        graph.Edges.Add(new LogicEdge("TR2", "TR1"));
        graph.Edges.Add(new LogicEdge("TR2", "ACT0"));

        var result = _compiler.Compile(graph, product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrCircularDependency || d.Severity == DiagnosticSeverity.Error);
    }

    // =========================================================================
    // 5. SPECIFICATION: IEC 61131-3 Comparator (CMP) Lowering & Decoupling
    // Khối Comparator xử lý toán tử so sánh số và hợp nhất vào Micro-Rule chuẩn
    // =========================================================================

    [Fact]
    [Trait("Specification", "IEC-61131-3-COMPARATOR-LOWERING")]
    public void Comparator_ConnectedToTrigger_LowersToSingleMcuRuleRecord()
    {
        // GIVEN: Cấu hình phần cứng chuẩn và các tag AI0, DO0
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var ai0 = product.FindTagByName("AI0")!;
        var do0 = product.FindTagByName("DO0")!;

        // WHEN: Xây dựng sơ đồ FBD theo chuẩn IEC 61131-3:
        // [AI0] -> [Comparator (> 80)] -> [Trigger (OnRise, ForMs = 2000ms)] -> [Action (DO0 = 1)]
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_AI0", ai0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateComparator("CMP1", ai0.TagIndex, CompareOperator.GreaterThan, thresholdLo: 80));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR1", TriggerKind.OnRise, forMs: 2000));
        graph.Nodes.Add(LogicNode.CreateAction("ACT1", do0.TagIndex, ActionKind.SetTag, 1));

        graph.Edges.Add(new LogicEdge("IN_AI0", "CMP1"));
        graph.Edges.Add(new LogicEdge("CMP1", "TR1"));
        graph.Edges.Add(new LogicEdge("TR1", "ACT1"));

        // THEN: Biên dịch đồ thị
        var result = _compiler.Compile(graph, product);

        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(ai0.TagIndex, rule.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnRise, rule.Trigger.Type);
        Assert.Equal(CompareOperator.GreaterThan, rule.Trigger.CompareOp);
        Assert.Equal(80, rule.Trigger.ThresholdLo);
        Assert.Equal(2000u, rule.Trigger.ForMs);
        Assert.Equal(do0.TagIndex, rule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.SetTag, rule.Action.Type);
        Assert.Equal(1, rule.Action.Parameter);
    }

    [Fact]
    [Trait("Specification", "IEC-61131-3-COMPARATOR-RANGE")]
    public void Comparator_WithBandRange_EncodesLoAndHiThresholds()
    {
        // GIVEN: Bộ so sánh trong dải [20..80]
        var product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
        var ai0 = product.FindTagByName("AI0")!;
        var do0 = product.FindTagByName("DO0")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_AI0", ai0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateComparator("CMP1", ai0.TagIndex, CompareOperator.Between, thresholdLo: 20, thresholdHi: 80));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR1", TriggerKind.OnChange));
        graph.Nodes.Add(LogicNode.CreateAction("ACT1", do0.TagIndex, ActionKind.SetTag, 1));

        graph.Edges.Add(new LogicEdge("IN_AI0", "CMP1"));
        graph.Edges.Add(new LogicEdge("CMP1", "TR1"));
        graph.Edges.Add(new LogicEdge("TR1", "ACT1"));

        var result = _compiler.Compile(graph, product);

        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Program);
        var rule = result.Program.Rules[0];
        Assert.Equal(CompareOperator.Between, rule.Trigger.CompareOp);
        Assert.Equal(20, rule.Trigger.ThresholdLo);
        Assert.Equal(80, rule.Trigger.ThresholdHi);
    }

    [Fact]
    [Trait("Specification", "IEC-61131-3-COMPARATOR-GRAMMAR")]
    public void Grammar_AllowsInputToComparator_AndRejectsInvalidConnections()
    {
        // Allowed: Input -> Comparator
        Assert.True(GraphGrammarV1.CanConnect(LogicNodeKind.Input, LogicNodeKind.Comparator));
        // Allowed: Comparator -> Trigger, Guard, Action
        Assert.True(GraphGrammarV1.CanConnect(LogicNodeKind.Comparator, LogicNodeKind.Trigger));
        Assert.True(GraphGrammarV1.CanConnect(LogicNodeKind.Comparator, LogicNodeKind.Guard));
        Assert.True(GraphGrammarV1.CanConnect(LogicNodeKind.Comparator, LogicNodeKind.Action));

        // Rejected: Action -> Comparator
        Assert.False(GraphGrammarV1.CanConnect(LogicNodeKind.Action, LogicNodeKind.Comparator));
        // Rejected: Comparator -> Input
        Assert.False(GraphGrammarV1.CanConnect(LogicNodeKind.Comparator, LogicNodeKind.Input));
    }
}
