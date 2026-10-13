namespace SimplePLC.Application.Tests;

using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

/// <summary>
/// Core Application Rule Compiler Specification Tests.
/// Tuân thủ Clean Code: Tra cứu TagIndex qua Contract của ProductDefinition,
/// không hardcode magic numbers và kiểm tra chặt chẽ các bất biến ngữ nghĩa (Semantic Invariants).
/// </summary>
public class ApplicationRuleCompilerTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void LinearPath_ProducesOneRule_WithSingleAction()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN0", di0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT0", do0.TagIndex, ActionKind.SetTag, 1));

        graph.Edges.Add(new LogicEdge("IN0", "TR0"));
        graph.Edges.Add(new LogicEdge("TR0", "ACT0"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Single(result.Program.Rules);

        var rule = result.Program.Rules[0];
        Assert.Equal(0, rule.RuleIndex);
        Assert.Equal(di0.TagIndex, rule.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnRise, rule.Trigger.Type);
        Assert.Equal(do0.TagIndex, rule.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.SetTag, rule.Action.Type);
        Assert.Equal(1, rule.Action.Parameter);
        Assert.False(rule.Guard.HasGuard);
    }

    [Fact]
    public void FanOut_OneTrigger_TwoActions_ProducesTwoIndependentRules()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        var do1 = _product.FindTagByName("DO1")!;

        // 1 Trigger fanning out to 2 Actions MUST lower into 2 independent single-action Rules!
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN0", di0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT0", do0.TagIndex, ActionKind.SetTag, 1, executionOrder: 0, label: "TurnOnDO0"));
        graph.Nodes.Add(LogicNode.CreateAction("ACT1", do1.TagIndex, ActionKind.SetTag, 1, executionOrder: 1, label: "TurnOnDO1"));

        graph.Edges.Add(new LogicEdge("IN0", "TR0"));
        graph.Edges.Add(new LogicEdge("TR0", "ACT0"));
        graph.Edges.Add(new LogicEdge("TR0", "ACT1"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.Rules.Count);

        var r0 = result.Program.Rules[0];
        var r1 = result.Program.Rules[1];

        // Each rule has exactly 1 action!
        Assert.Equal(0, r0.RuleIndex);
        Assert.Equal(do0.TagIndex, r0.Action.TargetTag.TagIndex);
        Assert.Equal("TurnOnDO0", r0.Name);

        Assert.Equal(1, r1.RuleIndex);
        Assert.Equal(do1.TagIndex, r1.Action.TargetTag.TagIndex);
        Assert.Equal("TurnOnDO1", r1.Name);

        // Both share the identical trigger condition
        Assert.Equal(r0.Trigger.Tag.TagIndex, r1.Trigger.Tag.TagIndex);
        Assert.Equal(r0.Trigger.Type, r1.Trigger.Type);
    }

    [Fact]
    public void GuardBranch_ProducesTwoRules_WithRespectiveGuards()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        var do1 = _product.FindTagByName("DO1")!;
        var vflag0 = _product.FindTagByName("VFLAG0")!;
        var vflag1 = _product.FindTagByName("VFLAG1")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN0", di0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR0", TriggerKind.OnChange));

        graph.Nodes.Add(LogicNode.CreateGuard("GRD0", vflag0.TagIndex, negated: false)); // VFLAG0 (Trong ca)
        graph.Nodes.Add(LogicNode.CreateAction("ACT0", do0.TagIndex, ActionKind.SetTag, 1, executionOrder: 0));

        graph.Nodes.Add(LogicNode.CreateGuard("GRD1", vflag1.TagIndex, negated: true));  // NOT VFLAG1
        graph.Nodes.Add(LogicNode.CreateAction("ACT1", do1.TagIndex, ActionKind.SetTag, 0, executionOrder: 1));

        graph.Edges.Add(new LogicEdge("IN0", "TR0"));
        graph.Edges.Add(new LogicEdge("TR0", "GRD0"));
        graph.Edges.Add(new LogicEdge("GRD0", "ACT0"));
        graph.Edges.Add(new LogicEdge("TR0", "GRD1"));
        graph.Edges.Add(new LogicEdge("GRD1", "ACT1"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.Rules.Count);

        Assert.Equal(vflag0.TagIndex, result.Program.Rules[0].Guard.Tag?.TagIndex);
        Assert.False(result.Program.Rules[0].Guard.Negated);

        Assert.Equal(vflag1.TagIndex, result.Program.Rules[1].Guard.Tag?.TagIndex);
        Assert.True(result.Program.Rules[1].Guard.Negated);
    }

    [Fact]
    public void ConditionThresholds_CorrectlyBelongToTriggerModel()
    {
        var ai0 = _product.FindTagByName("AI0")!;
        var do1 = _product.FindTagByName("DO1")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_AI0", ai0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateTrigger(
            "TR_TEMP",
            type: TriggerKind.OnChange,
            compareOp: CompareOperator.GreaterThan,
            thresholdLo: 85,
            thresholdHi: 0,
            forMs: 3000));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_FAN", do1.TagIndex, ActionKind.SetTag, 1));

        graph.Edges.Add(new LogicEdge("IN_AI0", "TR_TEMP"));
        graph.Edges.Add(new LogicEdge("TR_TEMP", "ACT_FAN"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        var rule = result.Program!.Rules[0];

        // Semantic invariant: CompareOp and Thresholds belong to Trigger
        Assert.Equal(CompareOperator.GreaterThan, rule.Trigger.CompareOp);
        Assert.Equal(85, rule.Trigger.ThresholdLo);
        Assert.Equal(3000u, rule.Trigger.ForMs);
        Assert.False(rule.Guard.HasGuard);
    }

    [Fact]
    public void Guard_WithNonBooleanTag_EmitsDiagnosticError()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var ai0 = _product.FindTagByName("AI0")!; // Analog / Int32, not Boolean!
        var do0 = _product.FindTagByName("DO0")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN0", di0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR0"));
        graph.Nodes.Add(LogicNode.CreateGuard("GRD_ANALOG", ai0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateAction("ACT0", do0.TagIndex));

        graph.Edges.Add(new LogicEdge("IN0", "TR0"));
        graph.Edges.Add(new LogicEdge("TR0", "GRD_ANALOG"));
        graph.Edges.Add(new LogicEdge("GRD_ANALOG", "ACT0"));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrGuardNotBoolean);
    }

    [Fact]
    public void SameGraph_CompiledMultipleTimes_ProducesIdenticalDeterministicOrder()
    {
        var di0 = _product.FindTagByName("DI0")!;
        var do0 = _product.FindTagByName("DO0")!;
        var do1 = _product.FindTagByName("DO1")!;
        var do2 = _product.FindTagByName("DO2")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN0", di0.TagIndex));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR0"));

        // Shuffle node additions with specific ExecutionOrders
        graph.Nodes.Add(LogicNode.CreateAction("ACT_Z", do2.TagIndex, ActionKind.SetTag, 1, executionOrder: 2));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_A", do0.TagIndex, ActionKind.SetTag, 1, executionOrder: 0));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_M", do1.TagIndex, ActionKind.SetTag, 1, executionOrder: 1));

        graph.Edges.Add(new LogicEdge("IN0", "TR0"));
        graph.Edges.Add(new LogicEdge("TR0", "ACT_Z"));
        graph.Edges.Add(new LogicEdge("TR0", "ACT_A"));
        graph.Edges.Add(new LogicEdge("TR0", "ACT_M"));

        var run1 = _compiler.Compile(graph, _product);
        var run2 = _compiler.Compile(graph, _product);

        Assert.True(run1.IsSuccess);
        Assert.True(run2.IsSuccess);

        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(run1.Program!.Rules[i].RuleIndex, run2.Program!.Rules[i].RuleIndex);
            Assert.Equal(run1.Program.Rules[i].Action.TargetTag.TagIndex, run2.Program.Rules[i].Action.TargetTag.TagIndex);
        }

        // Order 0 must be ACT_A (DO0), Order 1 must be ACT_M (DO1), Order 2 must be ACT_Z (DO2)
        Assert.Equal(do0.TagIndex, run1.Program!.Rules[0].Action.TargetTag.TagIndex);
        Assert.Equal(do1.TagIndex, run1.Program.Rules[1].Action.TargetTag.TagIndex);
        Assert.Equal(do2.TagIndex, run1.Program.Rules[2].Action.TargetTag.TagIndex);
    }
}
