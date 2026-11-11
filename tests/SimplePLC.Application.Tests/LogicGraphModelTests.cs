using SimplePLC.Application.Logic.Graph;
using SimplePLC.Domain.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Unit tests asserting model invariants, node creation factories, and edge queries in LogicGraphModel.
/// </summary>
public class LogicGraphModelTests
{
    [Fact]
    public void LogicGraph_NodeAndEdgeQueries_ReturnCorrectConnections()
    {
        var graph = new LogicGraph();
        var nodeA = LogicNode.CreateInput("NodeA", tagIndex: 0);
        var nodeB = LogicNode.CreateAction("NodeB", targetTagIndex: 8, type: ActionKind.SetTag, parameter: 1);

        graph.Nodes.Add(nodeA);
        graph.Nodes.Add(nodeB);

        var edge = new LogicEdge(nodeA.Id, nodeB.Id, "Out", "In");
        graph.Edges.Add(edge);

        Assert.Equal(nodeA, graph.FindNode("NodeA"));
        Assert.Equal(nodeB, graph.FindNode("NodeB"));
        Assert.Null(graph.FindNode("NonExistent"));

        var incoming = Assert.Single(graph.GetIncomingEdges("NodeB"));
        Assert.Equal("NodeA", incoming.SourceNodeId);

        var outgoing = Assert.Single(graph.GetOutgoingEdges("NodeA"));
        Assert.Equal("NodeB", outgoing.TargetNodeId);
    }
}
