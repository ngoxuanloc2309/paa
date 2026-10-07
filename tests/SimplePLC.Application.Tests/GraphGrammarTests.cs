namespace SimplePLC.Application.Tests;

using SimplePLC.Application.Logic.Compilation;
using SimplePLC.Application.Logic.Graph;
using Xunit;

public class GraphGrammarTests
{
    [Fact]
    public void Input_To_Trigger_To_Action_IsAllowed()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN1", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR1"));
        graph.Nodes.Add(LogicNode.CreateAction("ACT1", 8));

        graph.Edges.Add(new LogicEdge("IN1", "TR1"));
        graph.Edges.Add(new LogicEdge("TR1", "ACT1"));

        var diagnostics = GraphGrammarV1.ValidateStructure(graph);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Input_To_Trigger_To_Guard_To_Action_IsAllowed()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN1", 0));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR1"));
        graph.Nodes.Add(LogicNode.CreateGuard("GRD1", 32)); // VFLAG0
        graph.Nodes.Add(LogicNode.CreateAction("ACT1", 8));

        graph.Edges.Add(new LogicEdge("IN1", "TR1"));
        graph.Edges.Add(new LogicEdge("TR1", "GRD1"));
        graph.Edges.Add(new LogicEdge("GRD1", "ACT1"));

        var diagnostics = GraphGrammarV1.ValidateStructure(graph);
        Assert.Empty(diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
    }

    [Fact]
    public void Input_Direct_To_Action_IsRejected()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN1", 0));
        graph.Nodes.Add(LogicNode.CreateAction("ACT1", 8));
        graph.Edges.Add(new LogicEdge("IN1", "ACT1"));

        var diagnostics = GraphGrammarV1.ValidateStructure(graph);
        Assert.Contains(diagnostics, d => d.Code == GraphGrammarV1.ErrInputToAction);
    }

    [Fact]
    public void Input_Direct_To_Guard_IsRejected()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateInput("IN1", 0));
        graph.Nodes.Add(LogicNode.CreateGuard("GRD1", 32));
        graph.Edges.Add(new LogicEdge("IN1", "GRD1"));

        var diagnostics = GraphGrammarV1.ValidateStructure(graph);
        Assert.Contains(diagnostics, d => d.Code == GraphGrammarV1.ErrInputToGuard);
    }

    [Fact]
    public void Trigger_To_Trigger_IsRejected()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTrigger("TR1"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR2"));
        graph.Edges.Add(new LogicEdge("TR1", "TR2"));

        var diagnostics = GraphGrammarV1.ValidateStructure(graph);
        Assert.Contains(diagnostics, d => d.Code == GraphGrammarV1.ErrTriggerToTrigger);
    }

    [Fact]
    public void Guard_To_Guard_IsRejected_AsUnsupportedChainedGuard()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateGuard("GRD1", 32));
        graph.Nodes.Add(LogicNode.CreateGuard("GRD2", 33));
        graph.Edges.Add(new LogicEdge("GRD1", "GRD2"));

        var diagnostics = GraphGrammarV1.ValidateStructure(graph);
        Assert.Contains(diagnostics, d => d.Code == GraphGrammarV1.ErrChainedGuards);
    }

    [Fact]
    public void Action_AsSource_IsRejected()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateAction("ACT1", 8));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR1"));
        graph.Edges.Add(new LogicEdge("ACT1", "TR1"));

        var diagnostics = GraphGrammarV1.ValidateStructure(graph);
        Assert.Contains(diagnostics, d => d.Code == GraphGrammarV1.ErrActionSource);
    }

    [Fact]
    public void MultipleIncomingEdges_OnSameInputPort_IsRejected()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTrigger("TR1"));
        graph.Nodes.Add(LogicNode.CreateTrigger("TR2"));
        graph.Nodes.Add(LogicNode.CreateAction("ACT1", 8));

        graph.Edges.Add(new LogicEdge("TR1", "ACT1", "Out", "In"));
        graph.Edges.Add(new LogicEdge("TR2", "ACT1", "Out", "In"));

        var diagnostics = GraphGrammarV1.ValidateStructure(graph);
        Assert.Contains(diagnostics, d => d.Code == GraphGrammarV1.ErrMultipleInputsOnPort);
    }

    [Fact]
    public void Cycle_InGraph_IsDetectedAndRejected()
    {
        var graph = new LogicGraph();
        graph.Nodes.Add(LogicNode.CreateTrigger("TR1"));
        graph.Nodes.Add(LogicNode.CreateGuard("GRD1", 32));

        // Connect TR1 -> GRD1 and GRD1 -> TR1
        graph.Edges.Add(new LogicEdge("TR1", "GRD1"));
        graph.Edges.Add(new LogicEdge("GRD1", "TR1"));

        var diagnostics = GraphGrammarV1.ValidateStructure(graph);
        Assert.Contains(diagnostics, d => d.Code == GraphGrammarV1.ErrCycleDetected);
    }
}
