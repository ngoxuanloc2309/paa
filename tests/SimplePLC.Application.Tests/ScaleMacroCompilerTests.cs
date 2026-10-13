using System;
using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Executable Specification Test Suite for Linear Scaler Function Block (y = k * x + b).
/// Đối chiếu với tài liệu đặc tả SimplePLC_Modbus_Register_Map_V1.md:
/// Input: Cảm biến Analog AI (AI0), Output: Thanh ghi số học nội bộ VREG (VREG0).
/// </summary>
public class ScaleMacroCompilerTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void Scale_WithWireConnections_CompilesToScaleTagRule_WithoutActionGrammarError()
    {
        // Tra cứu TagIndex thực tế từ tài liệu đặc tả của sản phẩm (AI0 và VREG0)
        var ai0Sensor = _product.FindTagByName("AI0")!;
        var vreg0Storage = _product.FindTagByName("VREG0")!;

        var graph = new LogicGraph();

        var inputNode = LogicNode.CreateInput("IN_1", ai0Sensor.TagIndex, "Sensor_AI0");
        var scaleNode = LogicNode.CreateScale("SCALE_1", gain: 0.0039, offset: 0, inTagIndex: ai0Sensor.TagIndex, outTagIndex: vreg0Storage.TagIndex, label: "PressureScale");
        var actionNode = LogicNode.CreateAction("ACT_1", vreg0Storage.TagIndex, ActionKind.SetTag, parameter: 0, label: "Reg_Pressure");

        graph.Nodes.Add(inputNode);
        graph.Nodes.Add(scaleNode);
        graph.Nodes.Add(actionNode);

        // Connect Input -> Scale -> Action
        graph.Edges.Add(new LogicEdge(inputNode.Id, scaleNode.Id, "Out", "In"));
        graph.Edges.Add(new LogicEdge(scaleNode.Id, actionNode.Id, "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Compilation must succeed. Diagnostics: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(ai0Sensor.TagIndex, rule.Trigger.Tag.TagIndex); // AI0
        Assert.Equal(TriggerKind.OnChange, rule.Trigger.Type);
        Assert.Equal(vreg0Storage.TagIndex, rule.Action.TargetTag.TagIndex); // VREG0
        Assert.Equal(ActionKind.ScaleTag, rule.Action.Type);
        Assert.Equal((int)Math.Round(0.0039 * 1000.0), rule.Action.Parameter); // 4 (gain * 1000)
    }

    [Fact]
    public void Scale_WithoutOutgoingAction_EmitsDiagnosticError()
    {
        var ai0Sensor = _product.FindTagByName("AI0")!;

        var graph = new LogicGraph();
        var inputNode = LogicNode.CreateInput("IN_1", ai0Sensor.TagIndex, "Sensor_AI0");
        var scaleNode = new LogicNode
        {
            Id = "SCALE_1",
            Kind = LogicNodeKind.Scale,
            Label = "OrphanScale",
            ScaleData = new ScaleNodeData
            {
                InTagIndex = ai0Sensor.TagIndex,
                OutTagIndex = null,
                Gain = 1.0,
                Offset = 0.0
            }
        };

        graph.Nodes.Add(inputNode);
        graph.Nodes.Add(scaleNode);
        graph.Edges.Add(new LogicEdge(inputNode.Id, scaleNode.Id, "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => (d.Code == RuleCompiler.ErrScaleNoOutput || d.Code == RuleCompiler.ErrTagNotFound) && d.Message.Contains("Output Tag"));
    }
}
