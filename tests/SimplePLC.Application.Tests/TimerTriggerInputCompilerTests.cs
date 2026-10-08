using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

public class TimerTriggerInputCompilerTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai(wireProfile: 2);
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void AnalogInput_To_Trigger_To_TimerTon_GeneratesThresholdTimedRules()
    {
        // Sơ đồ: AI0 (tag 16) -> Trigger (GT 80) -> Timer (TON 5000ms, Q -> DO0 tag 8)
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_AI0", 16));
        
        var trig = LogicNode.CreateTrigger(
            "TR1",
            type: TriggerKind.OnChange,
            compareOp: CompareOperator.GreaterThan,
            thresholdLo: 80);
        graph.Nodes.Add(trig);

        var timer = LogicNode.CreateTimer("T1", TimerMacroType.Ton, inTagIndex: 16, presetMs: 5000, qTagIndex: 8);
        graph.Nodes.Add(timer);

        graph.Edges.Add(new LogicEdge("IN_AI0", "TR1"));
        graph.Edges.Add(new LogicEdge("TR1", "T1"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.RuleCount);

        // Rule 0: Trip rule (AI0 > 80, for_ms = 5000 -> DO0 = 1)
        var r0 = result.Program.Rules[0];
        Assert.Equal(16, r0.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnChange, r0.Trigger.Type);
        Assert.Equal(CompareOperator.GreaterThan, r0.Trigger.CompareOp);
        Assert.Equal(80, r0.Trigger.ThresholdLo);
        Assert.Equal(5000u, r0.Trigger.ForMs);
        Assert.Equal(8, r0.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.SetTag, r0.Action.Type);
        Assert.Equal(1, r0.Action.Parameter);

        // Rule 1: Clear rule (AI0 <= 80, for_ms = 0 -> DO0 = 0)
        var r1 = result.Program.Rules[1];
        Assert.Equal(16, r1.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnChange, r1.Trigger.Type);
        Assert.Equal(CompareOperator.LessThanOrEqual, r1.Trigger.CompareOp); // Inverted GT is LE
        Assert.Equal(80, r1.Trigger.ThresholdLo);
        Assert.Equal(0u, r1.Trigger.ForMs);
        Assert.Equal(8, r1.Action.TargetTag.TagIndex);
        Assert.Equal(ActionKind.SetTag, r1.Action.Type);
        Assert.Equal(0, r1.Action.Parameter);
    }

    [Fact]
    public void Trigger_With_Guard_To_TimerTon_CarriesGuardInterlock()
    {
        // Sơ đồ: AI0 -> Trigger(GT 50) -> Guard(VFLAG0 = 1, tag 32) -> Timer(TON 3000ms, Q -> DO1 tag 9)
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_AI0", 16));

        var trig = LogicNode.CreateTrigger(
            "TR1",
            type: TriggerKind.OnChange,
            compareOp: CompareOperator.GreaterThan,
            thresholdLo: 50);
        graph.Nodes.Add(trig);

        var guard = LogicNode.CreateGuard("G1", 32);
        graph.Nodes.Add(guard);

        var timer = LogicNode.CreateTimer("T1", TimerMacroType.Ton, inTagIndex: 16, presetMs: 3000, qTagIndex: 9);
        graph.Nodes.Add(timer);

        graph.Edges.Add(new LogicEdge("IN_AI0", "TR1"));
        graph.Edges.Add(new LogicEdge("TR1", "G1"));
        graph.Edges.Add(new LogicEdge("G1", "T1"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.RuleCount);

        var r0 = result.Program.Rules[0];
        Assert.Equal(16, r0.Trigger.Tag.TagIndex);
        Assert.Equal(CompareOperator.GreaterThan, r0.Trigger.CompareOp);
        Assert.Equal(50, r0.Trigger.ThresholdLo);
        Assert.Equal(3000u, r0.Trigger.ForMs);
        Assert.Equal(9, r0.Action.TargetTag.TagIndex);
        Assert.Equal(1, r0.Action.Parameter);
        // Kiểm tra Guard được mang theo
        Assert.True(r0.Guard.HasGuard);
        Assert.Equal(32, r0.Guard.Tag!.TagIndex);

        var r1 = result.Program.Rules[1];
        Assert.Equal(16, r1.Trigger.Tag.TagIndex);
        Assert.Equal(CompareOperator.LessThanOrEqual, r1.Trigger.CompareOp);
        Assert.Equal(50, r1.Trigger.ThresholdLo);
        Assert.Equal(0u, r1.Trigger.ForMs);
        Assert.Equal(9, r1.Action.TargetTag.TagIndex);
        Assert.Equal(0, r1.Action.Parameter);
    }

    [Fact]
    public void Trigger_To_CounterCU_UsesTriggerConditionForCountPulse()
    {
        // Sơ đồ: AI0 -> Trigger(GT 100) -> Counter(CTU, preset 10, Q -> DO2 tag 10)
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN_AI0", 16));

        var trig = LogicNode.CreateTrigger(
            "TR1",
            type: TriggerKind.OnChange,
            compareOp: CompareOperator.GreaterThan,
            thresholdLo: 100);
        graph.Nodes.Add(trig);

        var counter = LogicNode.CreateCounter("C1", CounterMacroType.Ctu, cuTagIndex: 16, resetTagIndex: null, cvTagIndex: 84, presetValue: 10, qTagIndex: 10);
        graph.Nodes.Add(counter);

        graph.Edges.Add(new LogicEdge("IN_AI0", "TR1"));
        graph.Edges.Add(new LogicEdge("TR1", "C1", "Out", "CU"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, string.Join("; ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotNull(result.Program);

        // Với CTU không có Reset: sinh 3 rules (Rule count up + Trip High CV >= PV + Clear Low CV < PV)
        Assert.Equal(3, result.Program.RuleCount);

        // Rule đếm xung CU phải dựa trên Trigger condition (AI0 > 100) thay vì mức logic bit thuần
        var cuRule = result.Program.Rules.FirstOrDefault(r => r.Action.Type == ActionKind.IncrementCounter);
        Assert.NotNull(cuRule);
        Assert.Equal(16, cuRule.Trigger.Tag.TagIndex);
        Assert.Equal(CompareOperator.GreaterThan, cuRule.Trigger.CompareOp);
        Assert.Equal(100, cuRule.Trigger.ThresholdLo);
    }
}
