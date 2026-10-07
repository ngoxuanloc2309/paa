using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

public class ScaleMacroCompilerTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void Scale_WithWireConnections_CompilesToScaleTagRule_WithoutActionGrammarError()
    {
        // Graph structure matching Blueprint:
        // Input Node (AI0: tag 16) -> Scale Node -> Action Node (VREG0: tag 20)
        var graph = new LogicGraph();

        var inputNode = LogicNode.CreateInput("IN_1", 16, "Sensor_AI0");
        var scaleNode = LogicNode.CreateScale("SCALE_1", gain: 0.0039, offset: 0, inTagIndex: 16, outTagIndex: 20, label: "PressureScale");
        var actionNode = LogicNode.CreateAction("ACT_1", 20, ActionKind.SetTag, parameter: 0, label: "Reg_Pressure");

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
        Assert.Equal(16, rule.Trigger.Tag.TagIndex); // AI0
        Assert.Equal(TriggerKind.OnChange, rule.Trigger.Type);
        Assert.Equal(20, rule.Action.TargetTag.TagIndex); // VREG0
        Assert.Equal(ActionKind.ScaleTag, rule.Action.Type);
        Assert.Equal((int)Math.Round(0.0039 * 1000.0), rule.Action.Parameter); // 4
    }

    [Fact]
    public void Scale_WithoutOutgoingAction_EmitsDiagnosticError()
    {
        var graph = new LogicGraph();
        var inputNode = LogicNode.CreateInput("IN_1", 16, "Sensor_AI0");
        var scaleNode = new LogicNode
        {
            Id = "SCALE_1",
            Kind = LogicNodeKind.Scale,
            Label = "OrphanScale",
            ScaleData = new ScaleNodeData
            {
                InTagIndex = 16,
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
