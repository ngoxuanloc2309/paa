using System.Linq;
using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Executable Specification Test Suite for CTU / CTD Counter Function Blocks (IEC 61131-3).
/// Tuân thủ Clean Code: Tra cứu thẻ Tag theo ProductDefinition thay vì hardcode số tĩnh.
/// </summary>
public class CounterMacroCompilerTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void CTU_WithReset_ExpandsToExactlyFourRules()
    {
        var di2Cu = _product.FindTagByName("DI2")!;
        var di0Reset = _product.FindTagByName("DI0")!;
        var cvTag = _product.Tags.First(t => t.Kind == TagKind.VirtualRegisterRetain || t.Kind == TagKind.VirtualRegister || t.Kind == TagKind.Counter);
        var do1Q = _product.FindTagByName("DO1")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_1", CounterMacroType.Ctu,
            cuTagIndex: di2Cu.TagIndex,
            resetTagIndex: di0Reset.TagIndex,
            cvTagIndex: cvTag.TagIndex,
            presetValue: 10,
            qTagIndex: do1Q.TagIndex,
            label: "BatchPackaging"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(4, result.Program.RuleCount);

        // 1. R0: Count Pulse (DI2 ON_RISE, Guard NOT DI0 -> CV += 1)
        var r0 = result.Program.Rules.FirstOrDefault(r => r.Action.Type == ActionKind.IncrementCounter);
        Assert.NotNull(r0);
        Assert.Equal(di2Cu.TagIndex, r0.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnRise, r0.Trigger.Type);
        Assert.True(r0.Guard.HasGuard);
        Assert.Equal(di0Reset.TagIndex, r0.Guard.Tag!.TagIndex);
        Assert.True(r0.Guard.Negated);
        Assert.Equal(cvTag.TagIndex, r0.Action.TargetTag.TagIndex);
        Assert.Equal(1, r0.Action.Parameter);

        // 2. R1: Reset (DI0 ON_RISE -> SET CV = 0)
        var r1 = result.Program.Rules.FirstOrDefault(r => r.Trigger.Tag.TagIndex == di0Reset.TagIndex && r.Action.TargetTag.TagIndex == cvTag.TagIndex);
        Assert.NotNull(r1);
        Assert.Equal(TriggerKind.OnRise, r1.Trigger.Type);
        Assert.Equal(ActionKind.SetTag, r1.Action.Type);
        Assert.Equal(0, r1.Action.Parameter);

        // 3. R2: Trip High (CV >= 10 -> SET Q = 1)
        var r2 = result.Program.Rules.FirstOrDefault(r => r.Action.TargetTag.TagIndex == do1Q.TagIndex && r.Action.Parameter == 1);
        Assert.NotNull(r2);
        Assert.Equal(cvTag.TagIndex, r2.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnChange, r2.Trigger.Type);
        Assert.Equal(CompareOperator.GreaterThanOrEqual, r2.Trigger.CompareOp);
        Assert.Equal(10, r2.Trigger.ThresholdLo);

        // 4. R3: Clear Low (CV < 10 -> SET Q = 0)
        var r3 = result.Program.Rules.FirstOrDefault(r => r.Action.TargetTag.TagIndex == do1Q.TagIndex && r.Action.Parameter == 0);
        Assert.NotNull(r3);
        Assert.Equal(cvTag.TagIndex, r3.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnChange, r3.Trigger.Type);
        Assert.Equal(CompareOperator.LessThan, r3.Trigger.CompareOp);
        Assert.Equal(10, r3.Trigger.ThresholdLo);
    }

    [Fact]
    public void CTU_WithoutReset_ExpandsToExactlyThreeRules()
    {
        var di2Cu = _product.FindTagByName("DI2")!;
        var cvTag = _product.Tags.First(t => t.Kind == TagKind.VirtualRegisterRetain || t.Kind == TagKind.VirtualRegister || t.Kind == TagKind.Counter);
        var do1Q = _product.FindTagByName("DO1")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_NO_RESET", CounterMacroType.Ctu,
            cuTagIndex: di2Cu.TagIndex,
            resetTagIndex: null,
            cvTagIndex: cvTag.TagIndex,
            presetValue: 5,
            qTagIndex: do1Q.TagIndex));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(3, result.Program.RuleCount);

        // Rule count should have no Guard
        var rCount = result.Program.Rules.FirstOrDefault(r => r.Action.Type == ActionKind.IncrementCounter);
        Assert.NotNull(rCount);
        Assert.False(rCount.Guard.HasGuard);
    }

    [Fact]
    public void Counter_ControlledMultiWriterGroup_AllowsIncrementAndSetZeroOnCv()
    {
        var di1Cu = _product.FindTagByName("DI1")!;
        var di0Reset = _product.FindTagByName("DI0")!;
        var cvTag = _product.Tags.First(t => t.Kind == TagKind.VirtualRegisterRetain || t.Kind == TagKind.VirtualRegister || t.Kind == TagKind.Counter);
        var do0Q = _product.FindTagByName("DO0")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_MWG", CounterMacroType.Ctu,
            cuTagIndex: di1Cu.TagIndex, resetTagIndex: di0Reset.TagIndex, cvTagIndex: cvTag.TagIndex, presetValue: 20, qTagIndex: do0Q.TagIndex));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Diagnostics.Where(d => d.Code == WriteConflictValidator.ErrConflictingWriters));
    }

    [Fact]
    public void Counter_ExternalWriterOnCv_IsRejectedWithConflictDiagnostic()
    {
        var di1Cu = _product.FindTagByName("DI1")!;
        var di0Reset = _product.FindTagByName("DI0")!;
        var di3Input = _product.FindTagByName("DI3")!;
        var cvTag = _product.Tags.First(t => t.Kind == TagKind.VirtualRegisterRetain || t.Kind == TagKind.VirtualRegister || t.Kind == TagKind.Counter);
        var do0Q = _product.FindTagByName("DO0")!;

        var graph = new LogicGraph();
        // Counter macro writing to CV
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_1", CounterMacroType.Ctu,
            cuTagIndex: di1Cu.TagIndex, resetTagIndex: di0Reset.TagIndex, cvTagIndex: cvTag.TagIndex, presetValue: 20, qTagIndex: do0Q.TagIndex));

        // External rule trying to write to CV tag
        graph.Nodes.Add(LogicNode.CreateInput("IN_2", tagIndex: di3Input.TagIndex));
        graph.Nodes.Add(LogicNode.CreateTrigger("TRIG_2", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_EXT", targetTagIndex: cvTag.TagIndex, ActionKind.SetTag, parameter: 99));
        graph.Edges.Add(new LogicEdge("IN_2", "TRIG_2"));
        graph.Edges.Add(new LogicEdge("TRIG_2", "ACT_EXT"));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == WriteConflictValidator.ErrConflictingWriters);
    }

    [Fact]
    public void Counter_WiredFromInputNode_ResolvesCuTagCorrectly()
    {
        var di3Input = _product.FindTagByName("DI3")!;
        var cvTag = _product.Tags.First(t => t.Kind == TagKind.VirtualRegisterRetain || t.Kind == TagKind.VirtualRegister || t.Kind == TagKind.Counter);
        var do1Q = _product.FindTagByName("DO1")!;

        var graph = new LogicGraph();
        // Input node DI3
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI3", tagIndex: di3Input.TagIndex));
        // Counter with placeholder cuTagIndex = 0
        var counter = LogicNode.CreateCounter("CTU_WIRED", CounterMacroType.Ctu,
            cuTagIndex: 0, resetTagIndex: 0, cvTagIndex: cvTag.TagIndex, presetValue: 10, qTagIndex: do1Q.TagIndex);
        graph.Nodes.Add(counter);
        graph.Edges.Add(new LogicEdge("IN_DI3", "CTU_WIRED"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        var rCount = result.Program!.Rules.FirstOrDefault(r => r.Action.Type == ActionKind.IncrementCounter);
        Assert.NotNull(rCount);
        // Upstream input tag must override placeholder
        Assert.Equal(di3Input.TagIndex, rCount.Trigger.Tag.TagIndex);
    }

    [Fact]
    public void Counter_SourceMap_PreservesAttributionToCounterMacro()
    {
        var di1Cu = _product.FindTagByName("DI1")!;
        var di0Reset = _product.FindTagByName("DI0")!;
        var cvTag = _product.Tags.First(t => t.Kind == TagKind.VirtualRegisterRetain || t.Kind == TagKind.VirtualRegister || t.Kind == TagKind.Counter);
        var do0Q = _product.FindTagByName("DO0")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_MAP", CounterMacroType.Ctu,
            cuTagIndex: di1Cu.TagIndex, resetTagIndex: di0Reset.TagIndex, cvTagIndex: cvTag.TagIndex, presetValue: 15, qTagIndex: do0Q.TagIndex, label: "BoxCount"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program!.SourceMap);
        Assert.Equal(4, result.Program.SourceMap.Count);

        for (int i = 0; i < result.Program.RuleCount; i++)
        {
            Assert.True(result.Program.SourceMap.TryGetValue(i, out var origin));
            Assert.NotNull(origin);
            Assert.Equal("CTU_MAP", origin.MacroInstanceId);
            Assert.Equal("CTU", origin.MacroType);
            Assert.Equal("BoxCount", origin.DisplayLabel);
        }
    }

    [Fact]
    public void Counter_TopologicalOrdering_PlacesCvWritersBeforeCvReaders()
    {
        var di2Cu = _product.FindTagByName("DI2")!;
        var di0Reset = _product.FindTagByName("DI0")!;
        var cvTag = _product.Tags.First(t => t.Kind == TagKind.VirtualRegisterRetain || t.Kind == TagKind.VirtualRegister || t.Kind == TagKind.Counter);
        var do1Q = _product.FindTagByName("DO1")!;

        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_ORDER", CounterMacroType.Ctu,
            cuTagIndex: di2Cu.TagIndex, resetTagIndex: di0Reset.TagIndex, cvTagIndex: cvTag.TagIndex, presetValue: 10, qTagIndex: do1Q.TagIndex));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        var rules = result.Program!.Rules;

        // Rules that write to CV (IncrementCounter and SetTag 0) must appear before rules that read CV (GTE 10 and LT 10)
        int maxCvWriterIndex = -1;
        int minCvReaderIndex = int.MaxValue;

        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].Action.TargetTag.TagIndex == cvTag.TagIndex)
            {
                if (i > maxCvWriterIndex) maxCvWriterIndex = i;
            }
            if (rules[i].Trigger.Tag.TagIndex == cvTag.TagIndex)
            {
                if (i < minCvReaderIndex) minCvReaderIndex = i;
            }
        }

        Assert.True(maxCvWriterIndex < minCvReaderIndex,
            $"Rules writing to CV (max index {maxCvWriterIndex}) must precede rules reading CV (min index {minCvReaderIndex})");
    }
}
