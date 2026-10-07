namespace SimplePLC.Application.Tests;

using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

public class RuleDependencyAndOrderingTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void ProducerBeforeConsumer_IsAutomaticallyOrdered()
    {
        // R-A: DI0 (Tag 0) ON_RISE -> VFLAG0 (Tag 20) = 1
        // R-B: VFLAG0 (Tag 20) ON_RISE -> DO0 (Tag 8) = 1
        // R-B is added first with executionOrder 0, R-A added second with executionOrder 1.
        // Compiler must automatically place R-A before R-B!
        var graph = new LogicGraph();

        // R-B (Consumer)
        graph.Nodes.Add(LogicNode.CreateInput("INP_V0", 20)); // VFLAG0
        graph.Nodes.Add(LogicNode.CreateTrigger("TRIG_B", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_B", 8, ActionKind.SetTag, 1, executionOrder: 0, label: "Rule_B")); // DO0
        graph.Edges.Add(new LogicEdge("INP_V0", "TRIG_B", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TRIG_B", "ACT_B", "Out", "In"));

        // R-A (Producer)
        graph.Nodes.Add(LogicNode.CreateInput("INP_DI0", 0)); // DI0
        graph.Nodes.Add(LogicNode.CreateTrigger("TRIG_A", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_A", 20, ActionKind.SetTag, 1, executionOrder: 1, label: "Rule_A")); // VFLAG0
        graph.Edges.Add(new LogicEdge("INP_DI0", "TRIG_A", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TRIG_A", "ACT_A", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.RuleCount);

        // Rule[0] must be Rule_A (Producer)
        Assert.Equal("Rule_A", result.Program.Rules[0].Name);
        Assert.Equal((ushort)20, result.Program.Rules[0].Action.TargetTag.TagIndex);

        // Rule[1] must be Rule_B (Consumer)
        Assert.Equal("Rule_B", result.Program.Rules[1].Name);
        Assert.Equal((ushort)8, result.Program.Rules[1].Action.TargetTag.TagIndex);
    }

    [Fact]
    public void ThreeLevelChain_IsTopologicallyOrdered()
    {
        // Chain: DI0 -> VFLAG0 -> VFLAG1 -> DO0
        // Nodes inserted in reverse order: R2, R1, R0
        var graph = new LogicGraph();

        // R2: VFLAG1 (Tag 21) -> DO0 (Tag 8)
        graph.Nodes.Add(LogicNode.CreateInput("IN_V1", 21));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_R2", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_R2", 8, ActionKind.SetTag, 1, executionOrder: 0, label: "R2"));
        graph.Edges.Add(new LogicEdge("IN_V1", "TR_R2", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_R2", "ACT_R2", "Out", "In"));

        // R1: VFLAG0 (Tag 20) -> VFLAG1 (Tag 21)
        graph.Nodes.Add(LogicNode.CreateInput("IN_V0", 20));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_R1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_R1", 21, ActionKind.SetTag, 1, executionOrder: 1, label: "R1"));
        graph.Edges.Add(new LogicEdge("IN_V0", "TR_R1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_R1", "ACT_R1", "Out", "In"));

        // R0: DI0 (Tag 0) -> VFLAG0 (Tag 20)
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_R0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_R0", 20, ActionKind.SetTag, 1, executionOrder: 2, label: "R0"));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_R0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_R0", "ACT_R0", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(3, result.Program.RuleCount);

        Assert.Equal("R0", result.Program.Rules[0].Name);
        Assert.Equal("R1", result.Program.Rules[1].Name);
        Assert.Equal("R2", result.Program.Rules[2].Name);
    }

    [Fact]
    public void GuardDependency_IsIncludedInOrdering()
    {
        // R0: DI0 (Tag 0) -> VFLAG_RUNNING (Tag 20) = 1
        // R1: AI0 (Tag 16) > 80, GUARD VFLAG_RUNNING (Tag 20) -> DO0 (Tag 8) = 1
        var graph = new LogicGraph();

        // R1 defined first
        graph.Nodes.Add(LogicNode.CreateInput("IN_AI0", 16));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_AI0", TriggerKind.OnChange, CompareOperator.GreaterThan, thresholdLo: 80));
        graph.Nodes.Add(LogicNode.CreateGuard("GRD_RUN", 20, negated: false));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_DO0", 8, ActionKind.SetTag, 1, executionOrder: 0, label: "R1_Alarm"));
        graph.Edges.Add(new LogicEdge("IN_AI0", "TR_AI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_AI0", "GRD_RUN", "Out", "In"));
        graph.Edges.Add(new LogicEdge("GRD_RUN", "ACT_DO0", "Out", "In"));

        // R0 defined second
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_RUN", 20, ActionKind.SetTag, 1, executionOrder: 1, label: "R0_Start"));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_RUN", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal("R0_Start", result.Program.Rules[0].Name);
        Assert.Equal("R1_Alarm", result.Program.Rules[1].Name);
    }

    [Fact]
    public void IndependentRules_UseStableCanonicalOrder()
    {
        var graph = new LogicGraph();

        // R-B: ExecutionOrder 2
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_DO1", 9, ActionKind.SetTag, 1, executionOrder: 2, label: "RB"));
        graph.Edges.Add(new LogicEdge("IN_DI1", "TR_DI1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1", "ACT_DO1", "Out", "In"));

        // R-A: ExecutionOrder 1
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_DO0", 8, ActionKind.SetTag, 1, executionOrder: 1, label: "RA"));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_DO0", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        // RA has lower ExecutionOrder (1 < 2), so RA comes before RB
        Assert.Equal("RA", result.Program.Rules[0].Name);
        Assert.Equal("RB", result.Program.Rules[1].Name);
    }

    [Fact]
    public void CircularDependency_IsRejected()
    {
        // R0: VFLAG0 (20) -> VFLAG1 (21)
        // R1: VFLAG1 (21) -> VFLAG0 (20)
        var graph = new LogicGraph();

        graph.Nodes.Add(LogicNode.CreateInput("IN_V0", 20));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_R0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_R0", 21, ActionKind.SetTag, 1, label: "R0"));
        graph.Edges.Add(new LogicEdge("IN_V0", "TR_R0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_R0", "ACT_R0", "Out", "In"));

        graph.Nodes.Add(LogicNode.CreateInput("IN_V1", 21));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_R1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_R1", 20, ActionKind.SetTag, 1, label: "R1"));
        graph.Edges.Add(new LogicEdge("IN_V1", "TR_R1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_R1", "ACT_R1", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Program);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrCircularDependency);
        var diag = result.Diagnostics.First(d => d.Code == RuleCompiler.ErrCircularDependency);
        Assert.NotNull(diag.RelatedNodeIds);
        Assert.Contains("ACT_R0", diag.RelatedNodeIds);
        Assert.Contains("ACT_R1", diag.RelatedNodeIds);
    }

    [Fact]
    public void TwoSetWriters_DistinctValues_SetResetPair_AreAllowed()
    {
        var graph = new LogicGraph();

        // R0: DI0 -> DO0 = 1 (SET)
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_0", 8, ActionKind.SetTag, 1));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_0", "Out", "In"));

        // R1: DI1 -> DO0 = 0 (RESET)
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_1", 8, ActionKind.SetTag, 0));
        graph.Edges.Add(new LogicEdge("IN_DI1", "TR_DI1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1", "ACT_1", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.RuleCount);
    }

    [Fact]
    public void TwoSetWriters_IdenticalValue_AreRejected()
    {
        var graph = new LogicGraph();

        // R0: DI0 -> DO0 = 1
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_0", 8, ActionKind.SetTag, 1));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_0", "Out", "In"));

        // R1: DI1 -> DO0 = 1 (Duplicate write of same value)
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_1", 8, ActionKind.SetTag, 1));
        graph.Edges.Add(new LogicEdge("IN_DI1", "TR_DI1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1", "ACT_1", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrConflictingWriters);
    }

    [Fact]
    public void UserDiagram_ThreeChains_VFlagInterlock_And_SetReset_CompilesSuccessfully()
    {
        var graph = new LogicGraph();

        // Chain 1: DI1 (50ms) -> VFLAG0 = 1
        // DI1 = tag 1, VFLAG0 = tag 20
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1_C1", 1));
        var tr1 = LogicNode.CreateTrigger("TR_DI1_C1", TriggerKind.OnRise, forMs: 50);
        graph.Nodes.Add(tr1);
        graph.Nodes.Add(LogicNode.CreateAction("ACT_VFLAG0", 20, ActionKind.SetTag, 1));
        graph.Edges.Add(new LogicEdge("IN_DI1_C1", "TR_DI1_C1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1_C1", "ACT_VFLAG0", "Out", "In"));

        // Chain 2: VFLAG0 -> DO0 = 1
        // DO0 = tag 8
        graph.Nodes.Add(LogicNode.CreateInput("IN_VFLAG0", 20));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_VFLAG0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_DO0_SET", 8, ActionKind.SetTag, 1));
        graph.Edges.Add(new LogicEdge("IN_VFLAG0", "TR_VFLAG0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_VFLAG0", "ACT_DO0_SET", "Out", "In"));

        // Chain 3: DI1 -> Guard NOT VFLAG0 -> DO0 = 0
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1_C3", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1_C3", TriggerKind.OnRise));
        var guard = LogicNode.CreateGuard("GD_NOT_VFLAG0", 20, negated: true);
        graph.Nodes.Add(guard);
        graph.Nodes.Add(LogicNode.CreateAction("ACT_DO0_RST", 8, ActionKind.SetTag, 0));
        graph.Edges.Add(new LogicEdge("IN_DI1_C3", "TR_DI1_C3", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1_C3", "GD_NOT_VFLAG0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("GD_NOT_VFLAG0", "ACT_DO0_RST", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess, $"Compilation must succeed. Errors: {string.Join(", ", result.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(result.Program);
        Assert.Equal(3, result.Program.RuleCount);
    }

    [Fact]
    public void SetAndToggle_SameTarget_AreRejected()
    {
        var graph = new LogicGraph();

        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_0", 8, ActionKind.SetTag, 1));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_0", "Out", "In"));

        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_1", 8, ActionKind.ToggleTag, 0));
        graph.Edges.Add(new LogicEdge("IN_DI1", "TR_DI1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1", "ACT_1", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrConflictingWriters);
    }

    [Fact]
    public void SetAndIncrement_SameTarget_AreRejected()
    {
        var graph = new LogicGraph();

        // Target Tag 28 (VREG_RETAIN0)
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_0", 28, ActionKind.SetTag, 10));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_0", "Out", "In"));

        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_1", 28, ActionKind.IncrementCounter, 1));
        graph.Edges.Add(new LogicEdge("IN_DI1", "TR_DI1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1", "ACT_1", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrConflictingWriters);
    }

    [Fact]
    public void TwoIncrementWriters_SameTarget_AreAllowed()
    {
        var graph = new LogicGraph();

        // Target Tag 116 (COUNTER0)
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_0", 116, ActionKind.IncrementCounter, 1));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_0", "Out", "In"));

        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_1", 116, ActionKind.IncrementCounter, 1));
        graph.Edges.Add(new LogicEdge("IN_DI1", "TR_DI1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1", "ACT_1", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        // Assert: Must be valid and NOT report false cycle or conflict
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(2, result.Program.RuleCount);
    }

    [Fact]
    public void ThreeProductionLines_IncrementSameTotal_AreAllowed()
    {
        var graph = new LogicGraph();

        // Target Tag 116 (COUNTER0) is TotalProduction
        for (int i = 0; i < 3; i++)
        {
            ushort diTag = (ushort)i;
            graph.Nodes.Add(LogicNode.CreateInput($"IN_LINE{i}", diTag));
            graph.Nodes.Add(LogicNode.CreateTrigger($"TR_LINE{i}", TriggerKind.OnRise));
            graph.Nodes.Add(LogicNode.CreateAction($"ACT_LINE{i}", 116, ActionKind.IncrementCounter, 1, executionOrder: i, label: $"Line_{i + 1}"));
            graph.Edges.Add(new LogicEdge($"IN_LINE{i}", $"TR_LINE{i}", "Out", "In"));
            graph.Edges.Add(new LogicEdge($"TR_LINE{i}", $"ACT_LINE{i}", "Out", "In"));
        }

        var result = _compiler.Compile(graph, _product);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Program);
        Assert.Equal(3, result.Program.RuleCount);
    }

    [Fact]
    public void AddAndAdd_SameTarget_AreRejectedInV1()
    {
        var graph = new LogicGraph();

        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_0", 28, ActionKind.AddTag, 0));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_0", "Out", "In"));

        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_1", 28, ActionKind.AddTag, 0));
        graph.Edges.Add(new LogicEdge("IN_DI1", "TR_DI1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1", "ACT_1", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrUnsupportedWriterCombination);
    }

    [Fact]
    public void ScaleAndAny_SameTarget_AreRejectedInV1()
    {
        var graph = new LogicGraph();

        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_0", 28, ActionKind.ScaleTag, 100));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_0", "Out", "In"));

        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1", 1));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_1", 28, ActionKind.IncrementCounter, 1));
        graph.Edges.Add(new LogicEdge("IN_DI1", "TR_DI1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1", "ACT_1", "Out", "In"));

        var result = _compiler.Compile(graph, _product);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Diagnostics, d => d.Code == RuleCompiler.ErrUnsupportedWriterCombination);
    }

    [Fact]
    public void SameGraph_CompiledRepeatedly_ProducesIdenticalRuleOrder()
    {
        var graph = new LogicGraph();

        // Multiple mixed rules
        graph.Nodes.Add(LogicNode.CreateInput("IN_V0", 20));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_R1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_R1", 8, ActionKind.SetTag, 1, executionOrder: 1, label: "R1"));
        graph.Edges.Add(new LogicEdge("IN_V0", "TR_R1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_R1", "ACT_R1", "Out", "In"));

        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_R0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_R0", 20, ActionKind.SetTag, 1, executionOrder: 0, label: "R0"));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_R0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_R0", "ACT_R0", "Out", "In"));

        var firstResult = _compiler.Compile(graph, _product);
        Assert.True(firstResult.IsSuccess);
        var expectedOrder = firstResult.Program!.Rules.Select(r => r.Name).ToList();

        for (int iter = 0; iter < 10; iter++)
        {
            var nextResult = _compiler.Compile(graph, _product);
            Assert.True(nextResult.IsSuccess);
            var actualOrder = nextResult.Program!.Rules.Select(r => r.Name).ToList();
            Assert.Equal(expectedOrder, actualOrder);
        }
    }
}
