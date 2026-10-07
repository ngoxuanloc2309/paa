using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;
using RuleCompiler = SimplePLC.Application.Logic.Compilation.RuleCompiler;

namespace SimplePLC.Studio.Tests;

public class SameScanChainingAcceptanceTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    private readonly RuleCompiler _compiler = new();

    [Fact]
    public void ThreeLevelDependencyChain_ExecutesInOneScan()
    {
        // 3-Level Chain: DI0 -> VFLAG0 -> VFLAG1 -> DO0
        // We purposefully add the nodes to the graph in reverse order:
        // R2 (VFLAG1 -> DO0) added first
        // R1 (VFLAG0 -> VFLAG1) added second
        // R0 (DI0 -> VFLAG0) added third
        var graph = new LogicGraph();

        // R2 (Consumer): reads VFLAG1 (Tag 21), writes DO0 (Tag 8)
        graph.Nodes.Add(LogicNode.CreateInput("IN_V1", 21));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_V1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_R2", 8, ActionKind.SetTag, 1, executionOrder: 0, label: "R2_DO0"));
        graph.Edges.Add(new LogicEdge("IN_V1", "TR_V1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_V1", "ACT_R2", "Out", "In"));

        // R1 (Middle): reads VFLAG0 (Tag 20), writes VFLAG1 (Tag 21)
        graph.Nodes.Add(LogicNode.CreateInput("IN_V0", 20));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_V0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_R1", 21, ActionKind.SetTag, 1, executionOrder: 1, label: "R1_VFLAG1"));
        graph.Edges.Add(new LogicEdge("IN_V0", "TR_V0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_V0", "ACT_R1", "Out", "In"));

        // R0 (Producer): reads DI0 (Tag 0), writes VFLAG0 (Tag 20)
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_R0", 20, ActionKind.SetTag, 1, executionOrder: 2, label: "R0_VFLAG0"));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_R0", "Out", "In"));

        // Compile graph into CompiledProgram
        var compileResult = _compiler.Compile(graph, _product);
        Assert.True(compileResult.IsSuccess, $"Compile failed: {string.Join(", ", compileResult.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(compileResult.Program);
        Assert.Equal(3, compileResult.Program.RuleCount);

        // Verify compiler topological ordering: R0 -> R1 -> R2
        Assert.Equal("R0_VFLAG0", compileResult.Program.Rules[0].Name);
        Assert.Equal("R1_VFLAG1", compileResult.Program.Rules[1].Name);
        Assert.Equal("R2_DO0", compileResult.Program.Rules[2].Name);

        // Prepare RuntimeEngine with tag catalog
        var tags = new TagCatalogViewModel();
        var di0 = tags.AllTags.First(t => t.Index == 0);
        var vflag0 = tags.AllTags.First(t => t.Index == 20);
        var vflag1 = tags.AllTags.First(t => t.Index == 21);
        var do0 = tags.AllTags.First(t => t.Index == 8);

        di0.Value = 0;
        vflag0.Value = 0;
        vflag1.Value = 0;
        do0.Value = 0;

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        // Scan at tick 10: initial state, DI0 is 0
        engine.Scan(compileResult.Program, 10);
        Assert.Equal(0, vflag0.Value);
        Assert.Equal(0, vflag1.Value);
        Assert.Equal(0, do0.Value);

        // Scan at tick 20: DI0 transitions from 0 -> 1 (Rising Edge)
        di0.Value = 1;
        var snapshot = engine.Scan(compileResult.Program, 20);

        // In the EXACT SAME scan cycle:
        // R0 fires: sets VFLAG0 = 1
        // R1 detects VFLAG0 rise: sets VFLAG1 = 1
        // R2 detects VFLAG1 rise: sets DO0 = 1
        Assert.Equal(1, vflag0.Value);
        Assert.Equal(1, vflag1.Value);
        Assert.Equal(1, do0.Value);
        Assert.Equal(3, snapshot.Events.Count);
    }

    [Fact]
    public void ThreeProductionLines_IncrementSameTotal_AreAllowed_AndAccumulateInOneScan()
    {
        // 3 separate production lines incrementing the same COUNTER0 (Tag 116):
        // Line 1: DI0 ON_RISE -> INC_COUNTER COUNTER0 (+1)
        // Line 2: DI1 ON_RISE -> INC_COUNTER COUNTER0 (+1)
        // Line 3: DI2 ON_RISE -> INC_COUNTER COUNTER0 (+1)
        var graph = new LogicGraph();

        // Line 1
        graph.Nodes.Add(LogicNode.CreateInput("IN_L1", 0)); // DI0
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_L1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_L1", 116, ActionKind.IncrementCounter, 1, executionOrder: 0, label: "L1_Inc"));
        graph.Edges.Add(new LogicEdge("IN_L1", "TR_L1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_L1", "ACT_L1", "Out", "In"));

        // Line 2
        graph.Nodes.Add(LogicNode.CreateInput("IN_L2", 1)); // DI1
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_L2", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_L2", 116, ActionKind.IncrementCounter, 1, executionOrder: 1, label: "L2_Inc"));
        graph.Edges.Add(new LogicEdge("IN_L2", "TR_L2", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_L2", "ACT_L2", "Out", "In"));

        // Line 3
        graph.Nodes.Add(LogicNode.CreateInput("IN_L3", 2)); // DI2
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_L3", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_L3", 116, ActionKind.IncrementCounter, 1, executionOrder: 2, label: "L3_Inc"));
        graph.Edges.Add(new LogicEdge("IN_L3", "TR_L3", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_L3", "ACT_L3", "Out", "In"));

        // Compile graph
        var compileResult = _compiler.Compile(graph, _product);
        Assert.True(compileResult.IsSuccess, $"Multi-writer accumulator should compile successfully: {string.Join(", ", compileResult.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(compileResult.Program);
        Assert.Equal(3, compileResult.Program.RuleCount);

        // Prepare RuntimeEngine
        var tags = new TagCatalogViewModel();
        var di0 = tags.AllTags.First(t => t.Index == 0);
        var di1 = tags.AllTags.First(t => t.Index == 1);
        var di2 = tags.AllTags.First(t => t.Index == 2);
        var counter0 = tags.AllTags.First(t => t.Index == 116);

        di0.Value = 0;
        di1.Value = 0;
        di2.Value = 0;
        counter0.Value = 0;

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        // Scan 10: initial low state
        engine.Scan(compileResult.Program, 10);
        Assert.Equal(0, counter0.Value);

        // Scan 20: All 3 production lines signal simultaneously in the SAME scan tick
        di0.Value = 1;
        di1.Value = 1;
        di2.Value = 1;
        var snap20 = engine.Scan(compileResult.Program, 20);

        // Total counter must accumulate all 3 increments: 0 + 1 + 1 + 1 = 3
        Assert.Equal(3, counter0.Value);
        Assert.Equal(3, snap20.Events.Count);

        // Scan 30: Unchanged high values - edge triggers do not re-fire
        var snap30 = engine.Scan(compileResult.Program, 30);
        Assert.Equal(3, counter0.Value);
        Assert.Empty(snap30.Events);

        // Scan 40: Inputs fall back to 0
        di0.Value = 0;
        di1.Value = 0;
        di2.Value = 0;
        engine.Scan(compileResult.Program, 40);
        Assert.Equal(3, counter0.Value);

        // Scan 50: Lines 1 and 2 pulse again (Line 3 remains 0)
        di0.Value = 1;
        di1.Value = 1;
        var snap50 = engine.Scan(compileResult.Program, 50);

        // Counter must be 3 + 1 + 1 = 5
        Assert.Equal(5, counter0.Value);
        Assert.Equal(2, snap50.Events.Count);
    }

    [Fact]
    public void GuardChaining_WithTopologicalSort_ExecutesInOneScan()
    {
        // R_Consumer: DI1 ON_RISE, Guard VFLAG0 == 1 -> SET DO0 = 1
        // R_Producer: DI0 ON_RISE -> SET VFLAG0 = 1
        // We purposefully add R_Consumer first to test that topological sorting reorders Producer before Consumer.
        var graph = new LogicGraph();

        // Consumer added first
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI1", 1)); // DI1
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI1", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateGuard("GRD_V0", 20, negated: false)); // VFLAG0 as Guard
        graph.Nodes.Add(LogicNode.CreateAction("ACT_CONS", 8, ActionKind.SetTag, 1, executionOrder: 0, label: "R_Consumer"));
        graph.Edges.Add(new LogicEdge("IN_DI1", "TR_DI1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI1", "GRD_V0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("GRD_V0", "ACT_CONS", "Out", "In"));

        // Producer added second
        graph.Nodes.Add(LogicNode.CreateInput("IN_DI0", 0)); // DI0
        graph.Nodes.Add(LogicNode.CreateTrigger("TR_DI0", TriggerKind.OnRise));
        graph.Nodes.Add(LogicNode.CreateAction("ACT_PROD", 20, ActionKind.SetTag, 1, executionOrder: 1, label: "R_Producer"));
        graph.Edges.Add(new LogicEdge("IN_DI0", "TR_DI0", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR_DI0", "ACT_PROD", "Out", "In"));

        var compileResult = _compiler.Compile(graph, _product);
        Assert.True(compileResult.IsSuccess, $"Compile failed: {string.Join(", ", compileResult.Diagnostics.Select(d => d.Message))}");
        Assert.NotNull(compileResult.Program);
        Assert.Equal(2, compileResult.Program.RuleCount);

        // Topologically sorted: Producer must be Rule 0, Consumer must be Rule 1
        Assert.Equal("R_Producer", compileResult.Program.Rules[0].Name);
        Assert.Equal("R_Consumer", compileResult.Program.Rules[1].Name);

        var tags = new TagCatalogViewModel();
        var di0 = tags.AllTags.First(t => t.Index == 0);
        var di1 = tags.AllTags.First(t => t.Index == 1);
        var vflag0 = tags.AllTags.First(t => t.Index == 20);
        var do0 = tags.AllTags.First(t => t.Index == 8);

        di0.Value = 0;
        di1.Value = 0;
        vflag0.Value = 0;
        do0.Value = 0;

        var engine = new RuntimeEngine(tags.AllTags.ToList());

        // Baseline tick
        engine.Scan(compileResult.Program, 10);
        Assert.Equal(0, vflag0.Value);
        Assert.Equal(0, do0.Value);

        // Same scan: DI0 and DI1 both rise simultaneously
        di0.Value = 1;
        di1.Value = 1;
        var snap20 = engine.Scan(compileResult.Program, 20);

        // Because R_Producer ran first, VFLAG0 became 1, so R_Consumer's guard was open!
        Assert.Equal(1, vflag0.Value);
        Assert.Equal(1, do0.Value);
        Assert.Equal(2, snap20.Events.Count);
    }
}
