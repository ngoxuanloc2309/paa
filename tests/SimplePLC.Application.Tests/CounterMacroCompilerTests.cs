using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Application.Tests;

public class CounterMacroCompilerTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void CTU_WithReset_ExpandsToExactlyFourRules()
    {
        var graph = new LogicGraph();
        // CU: DI2 (tag 2), Reset: DI0 (tag 0), CV: VREG_RETAIN0 (tag 52), PV: 10, Q: DO1 (tag 9)
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_1", CounterMacroType.Ctu,
            cuTagIndex: 2,
            resetTagIndex: 0,
            cvTagIndex: 52,
            presetValue: 10,
            qTagIndex: 9,
            label: "BatchPackaging"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(4, result.Program.RuleCount);

        // 1. R0: Count Pulse (DI2 ON_RISE, Guard NOT DI0 -> CV += 1)
        var r0 = result.Program.Rules.FirstOrDefault(r => r.Action.Type == ActionKind.IncrementCounter);
        Assert.NotNull(r0);
        Assert.Equal(2, r0.Trigger.Tag.TagIndex); // DI2
        Assert.Equal(TriggerKind.OnRise, r0.Trigger.Type);
        Assert.True(r0.Guard.HasGuard);
        Assert.Equal(0, r0.Guard.Tag!.TagIndex); // Reset DI0
        Assert.True(r0.Guard.Negated); // Guard NOT Reset
        Assert.Equal(52, r0.Action.TargetTag.TagIndex); // CV
        Assert.Equal(1, r0.Action.Parameter); // +1

        // 2. R1: Reset (DI0 ON_RISE -> SET CV = 0)
        var r1 = result.Program.Rules.FirstOrDefault(r => r.Trigger.Tag.TagIndex == 0 && r.Action.TargetTag.TagIndex == 52);
        Assert.NotNull(r1);
        Assert.Equal(TriggerKind.OnRise, r1.Trigger.Type);
        Assert.Equal(ActionKind.SetTag, r1.Action.Type);
        Assert.Equal(0, r1.Action.Parameter);

        // 3. R2: Trip High (CV >= 10 -> SET Q = 1)
        var r2 = result.Program.Rules.FirstOrDefault(r => r.Action.TargetTag.TagIndex == 9 && r.Action.Parameter == 1);
        Assert.NotNull(r2);
        Assert.Equal(52, r2.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnChange, r2.Trigger.Type);
        Assert.Equal(CompareOperator.GreaterThanOrEqual, r2.Trigger.CompareOp);
        Assert.Equal(10, r2.Trigger.ThresholdLo);

        // 4. R3: Clear Low (CV < 10 -> SET Q = 0)
        var r3 = result.Program.Rules.FirstOrDefault(r => r.Action.TargetTag.TagIndex == 9 && r.Action.Parameter == 0);
        Assert.NotNull(r3);
        Assert.Equal(52, r3.Trigger.Tag.TagIndex);
        Assert.Equal(TriggerKind.OnChange, r3.Trigger.Type);
        Assert.Equal(CompareOperator.LessThan, r3.Trigger.CompareOp);
        Assert.Equal(10, r3.Trigger.ThresholdLo);
    }

    [Fact]
    public void CTU_WithoutReset_ExpandsToExactlyThreeRules()
    {
        var graph = new LogicGraph();
        // CU: DI2 (tag 2), Reset: none (0), CV: VREG_RETAIN0 (tag 52), PV: 5, Q: DO1 (tag 9)
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_NO_RESET", CounterMacroType.Ctu,
            cuTagIndex: 2,
            resetTagIndex: null,
            cvTagIndex: 52,
            presetValue: 5,
            qTagIndex: 9));

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
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_MWG", CounterMacroType.Ctu,
            cuTagIndex: 1, resetTagIndex: 0, cvTagIndex: 52, presetValue: 20, qTagIndex: 8));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        // Multi-writer group allows CV to be written by both IncrementCounter and SetTag(0)
        Assert.Empty(result.Diagnostics.Where(d => d.Code == WriteConflictValidator.ErrConflictingWriters));
    }

    [Fact]
    public void Counter_ExternalWriterOnCv_IsRejectedWithConflictDiagnostic()
    {
        var graph = new LogicGraph();
        // Counter macro writing to CV (tag 52)
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_1", CounterMacroType.Ctu,
            cuTagIndex: 1, resetTagIndex: 0, cvTagIndex: 52, presetValue: 20, qTagIndex: 8));

        // External rule trying to write to tag 52
        graph.Nodes.Add(LogicNode.CreateInput("IN_2", tagIndex: 3));
        graph.Nodes.Add(LogicNode.CreateTrigger("TRIG_2", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_EXT", targetTagIndex: 52, ActionKind.SetTag, parameter: 99));
        graph.Edges.Add(new LogicEdge("IN_2", "TRIG_2"));
        graph.Edges.Add(new LogicEdge("TRIG_2", "ACT_EXT"));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == WriteConflictValidator.ErrConflictingWriters);
    }

    [Fact]
    public void Counter_WiredFromInputNode_ResolvesCuTagCorrectly()
    {
        var graph = new LogicGraph();
        // Input node DI3 (tag 3)
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI3", tagIndex: 3));
        // Counter with placeholder cuTagIndex = 0
        var counter = LogicNode.CreateCounter("CTU_WIRED", CounterMacroType.Ctu,
            cuTagIndex: 0, resetTagIndex: 0, cvTagIndex: 52, presetValue: 10, qTagIndex: 9);
        graph.Nodes.Add(counter);
        graph.Edges.Add(new LogicEdge("IN_DI3", "CTU_WIRED"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        var rCount = result.Program!.Rules.FirstOrDefault(r => r.Action.Type == ActionKind.IncrementCounter);
        Assert.NotNull(rCount);
        // Upstream input tag 3 must override placeholder 0
        Assert.Equal(3, rCount.Trigger.Tag.TagIndex);
    }

    [Fact]
    public void Counter_SourceMap_PreservesAttributionToCounterMacro()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_MAP", CounterMacroType.Ctu,
            cuTagIndex: 1, resetTagIndex: 0, cvTagIndex: 52, presetValue: 15, qTagIndex: 8, label: "BoxCount"));

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
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_ORDER", CounterMacroType.Ctu,
            cuTagIndex: 2, resetTagIndex: 0, cvTagIndex: 52, presetValue: 10, qTagIndex: 9));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        var rules = result.Program!.Rules;

        // Rules that write to CV (IncrementCounter and SetTag 0) must appear before rules that read CV (GTE 10 and LT 10)
        int maxCvWriterIndex = -1;
        int minCvReaderIndex = int.MaxValue;

        for (int i = 0; i < rules.Count; i++)
        {
            if (rules[i].Action.TargetTag.TagIndex == 52)
            {
                if (i > maxCvWriterIndex) maxCvWriterIndex = i;
            }
            if (rules[i].Trigger.Tag.TagIndex == 52)
            {
                if (i < minCvReaderIndex) minCvReaderIndex = i;
            }
        }

        Assert.True(maxCvWriterIndex < minCvReaderIndex,
            $"CV writers (index {maxCvWriterIndex}) must precede CV readers (index {minCvReaderIndex}) in execution order.");
    }

    [Fact]
    public void Counter_FbdWiringResolution_WiresCuResetAndQSuccessfully()
    {
        var graph = new LogicGraph();
        // Node 1: Input DI2 (Tag 2)
        graph.Nodes.Add(LogicNode.CreateInput("INP_COUNT", 2));
        // Node 2: Input DI3 (Tag 3)
        graph.Nodes.Add(LogicNode.CreateInput("INP_RESET", 3));
        // Node 3: Counter CTU (placeholders for CU, Reset, Q)
        graph.Nodes.Add(LogicNode.CreateCounter("CTU_FBD", CounterMacroType.Ctu,
            cuTagIndex: 0,
            resetTagIndex: null,
            cvTagIndex: 52, // VREG_RETAIN0
            presetValue: 10,
            qTagIndex: 0));
        // Node 4: Action DO2 (Tag 10)
        graph.Nodes.Add(LogicNode.CreateAction("ACT_OUT", 10));

        // Connect DI2 -> CTU:CU
        graph.Edges.Add(new LogicEdge("INP_COUNT", "CTU_FBD", "Out", "CU"));
        // Connect DI3 -> CTU:R
        graph.Edges.Add(new LogicEdge("INP_RESET", "CTU_FBD", "Out", "R"));
        // Connect CTU:Q -> ACT_OUT:In
        graph.Edges.Add(new LogicEdge("CTU_FBD", "ACT_OUT", "Q", "In"));

        // Structure check should pass grammar
        var structDiags = GraphGrammarV1.ValidateStructure(graph);
        Assert.Empty(structDiags);

        // Compile
        var result = _compiler.Compile(graph, _product);
        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(4, result.Program.RuleCount);

        // Verify CU was resolved to DI2 (Tag 2)
        var countRule = result.Program.Rules.FirstOrDefault(r => r.Action.Type == ActionKind.IncrementCounter);
        Assert.NotNull(countRule);
        Assert.Equal(2, countRule.Trigger.Tag.TagIndex);

        // Verify Reset was resolved to DI3 (Tag 3)
        var resetRule = result.Program.Rules.FirstOrDefault(r => r.Trigger.Tag.TagIndex == 3 && r.Action.TargetTag.TagIndex == 52);
        Assert.NotNull(resetRule);

        // Verify Q was resolved to DO2 (Tag 10)
        var qTripRule = result.Program.Rules.FirstOrDefault(r => r.Action.TargetTag.TagIndex == 10 && r.Action.Parameter == 1);
        Assert.NotNull(qTripRule);
        var qClearRule = result.Program.Rules.FirstOrDefault(r => r.Action.TargetTag.TagIndex == 10 && r.Action.Parameter == 0);
        Assert.NotNull(qClearRule);
    }
}
