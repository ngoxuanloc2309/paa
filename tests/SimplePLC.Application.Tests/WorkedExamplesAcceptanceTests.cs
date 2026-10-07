namespace SimplePLC.Application.Tests;

using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

public class WorkedExamplesAcceptanceTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void Example1_StartLamp_ProducesExactlyOneRule()
    {
        // IF Start (DI0) ON_RISE THEN GreenLight (DO0) = ON (1)
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_START", 0, "StartButton"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_START", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_GREEN", 8, ActionKind.SetTag, 1, executionOrder: 0, label: "TurnOnGreen"));

        graph.Edges.Add(new LogicEdge("IN_START", "TR_START"));
        graph.Edges.Add(new LogicEdge("TR_START", "ACT_GREEN"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(0, rule.RuleIndex);
        Assert.Equal(0, rule.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnRise, rule.Trigger.Type);
        Assert.Equal(8, rule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.SetTag, rule.Action.Type);
        Assert.Equal(1, rule.Action.Parameter);
        Assert.False(rule.Guard.HasGuard);
    }

    [Fact]
    public void Example2_ProductCounter_ProducesExactlyOneRule()
    {
        // Tag 92 is COUNTER0 in 8DI-8DO-4AI profile
        var counterTag = _product.Tags.First(t => t.Kind == TagKind.Counter);

        // IF ProductSensor (DI1) ON_RISE THEN Counter0 += 1
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_SENSOR", 1, "ProductSensor"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_PULSE", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_INC", counterTag.TagIndex, ActionKind.IncrementCounter, 1, executionOrder: 0, label: "IncCounter"));

        graph.Edges.Add(new LogicEdge("IN_SENSOR", "TR_PULSE"));
        graph.Edges.Add(new LogicEdge("TR_PULSE", "ACT_INC"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(0, rule.RuleIndex);
        Assert.Equal(1, rule.Trigger.Tag.TagIndex);
        Assert.Equal(counterTag.TagIndex, rule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.IncrementCounter, rule.Action.Type);
        Assert.Equal(1, rule.Action.Parameter);
    }

    [Fact]
    public void Example3_PersistentProductionTotal_ProducesExactlyOneRule()
    {
        // Tag 60 is VREG_RETAIN0 in 8DI-8DO-4AI profile
        var retainTag = _product.Tags.First(t => t.Kind == TagKind.VirtualRegisterRetain);

        // IF ProductSensor (DI1) ON_RISE THEN VREG_RETAIN0 += 1
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_SENSOR", 1, "ProductSensor"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_PULSE", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_RETAIN", retainTag.TagIndex, ActionKind.IncrementCounter, 1, executionOrder: 0, label: "IncRetainTotal"));

        graph.Edges.Add(new LogicEdge("IN_SENSOR", "TR_PULSE"));
        graph.Edges.Add(new LogicEdge("TR_PULSE", "ACT_RETAIN"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(0, rule.RuleIndex);
        Assert.Equal(retainTag.TagIndex, rule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.IncrementCounter, rule.Action.Type);
    }

    [Fact]
    public void Example4_MultiStepViaVirtualState_ProducesTwoRules()
    {
        // R1: IF Start (DI0) ON_RISE THEN VFLAG0 = ON (1)
        // R2: IF VFLAG0 ON_RISE THEN DO_GREEN (DO0) = ON (1)
        var vflag0 = _product.Tags.First(t => t.Kind == TagKind.VirtualFlag);

        var graph = new LogicGraph();
        // Chain 1
        graph.Nodes.Add(LogicNode.CreateInput("IN_START", 0, "StartButton"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_START", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_VFLAG", vflag0.TagIndex, ActionKind.SetTag, 1, executionOrder: 0, label: "SetStep1Active"));

        graph.Edges.Add(new LogicEdge("IN_START", "TR_START"));
        graph.Edges.Add(new LogicEdge("TR_START", "ACT_VFLAG"));

        // Chain 2
        graph.Nodes.Add(LogicNode.CreateInput("IN_VFLAG", vflag0.TagIndex, "Step1State"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_VFLAG", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_GREEN", 8, ActionKind.SetTag, 1, executionOrder: 1, label: "TurnOnGreenLamp"));

        graph.Edges.Add(new LogicEdge("IN_VFLAG", "TR_VFLAG"));
        graph.Edges.Add(new LogicEdge("TR_VFLAG", "ACT_GREEN"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.Rules.Count);

        var r0 = result.Program.Rules[0];
        Assert.Equal(0, r0.RuleIndex);
        Assert.Equal(0, r0.Trigger.Tag.TagIndex);
        Assert.Equal(vflag0.TagIndex, r0.Action.TargetTag.TagIndex);

        var r1 = result.Program.Rules[1];
        Assert.Equal(1, r1.RuleIndex);
        Assert.Equal(vflag0.TagIndex, r1.Trigger.Tag.TagIndex);
        Assert.Equal(8, r1.Action.TargetTag.TagIndex);
    }
}
