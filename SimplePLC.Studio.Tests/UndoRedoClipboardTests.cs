using System.Threading;
using System.Windows;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class UndoRedoClipboardTests
{
    private LogicEditorViewModel CreateEditor()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.Nodes.Clear();
        vm.Connections.Clear();
        vm.SelectedNodes.Clear();
        vm.History.Clear();
        return vm;
    }

    [Fact]
    public void UndoRedo_AddNode_CanUndoAndRedo()
    {
        var vm = CreateEditor();
        Assert.Empty(vm.Nodes);
        Assert.False(vm.CanUndo);
        Assert.False(vm.CanRedo);

        vm.AddNode("Input", new Point(100, 100));

        Assert.Single(vm.Nodes);
        Assert.True(vm.CanUndo);
        Assert.False(vm.CanRedo);

        vm.Undo();

        Assert.Empty(vm.Nodes);
        Assert.False(vm.CanUndo);
        Assert.True(vm.CanRedo);

        vm.Redo();

        Assert.Single(vm.Nodes);
        Assert.True(vm.CanUndo);
        Assert.False(vm.CanRedo);
    }

    [Fact]
    public void UndoRedo_Connection_CanUndoAndRedo()
    {
        var vm = CreateEditor();
        vm.AddNode("Input", new Point(100, 100));
        vm.AddNode("Trigger", new Point(300, 100));

        var inputNode = vm.Nodes[0];
        var trigNode = vm.Nodes[1];

        // Connect Input to Trigger
        vm.CompleteConnection((inputNode.OutputConnectors[0], trigNode.InputConnectors[0]));

        Assert.Single(vm.Connections);
        Assert.True(vm.CanUndo);

        vm.Undo();

        Assert.Empty(vm.Connections);
        Assert.True(vm.CanRedo);

        vm.Redo();

        Assert.Single(vm.Connections);
        Assert.Equal(inputNode.OutputConnectors[0].Title, vm.Connections[0].Source.Title);
        Assert.Equal(trigNode.InputConnectors[0].Title, vm.Connections[0].Target.Title);
    }

    [Fact]
    public void UndoRedo_DeleteNode_RestoresNodeAndConnections()
    {
        var vm = CreateEditor();
        vm.AddNode("Input", new Point(100, 100));
        vm.AddNode("Trigger", new Point(300, 100));
        vm.AddNode("Action", new Point(500, 100));

        var inputNode = vm.Nodes[0];
        var trigNode = vm.Nodes[1];
        var actNode = vm.Nodes[2];

        vm.CompleteConnection((inputNode.OutputConnectors[0], trigNode.InputConnectors[0]));
        vm.CompleteConnection((trigNode.OutputConnectors[0], actNode.InputConnectors[0]));

        Assert.Equal(3, vm.Nodes.Count);
        Assert.Equal(2, vm.Connections.Count);

        // Select the middle trigger node and delete
        vm.SelectExclusive(trigNode);
        vm.DeleteSelectedNode();

        Assert.Equal(2, vm.Nodes.Count);
        Assert.Empty(vm.Connections);

        // Undo
        vm.Undo();

        Assert.Equal(3, vm.Nodes.Count);
        Assert.Equal(2, vm.Connections.Count);
    }

    [Fact]
    public void Clipboard_CopyPaste_ClonesNodesWithNewIdsAndRecreatesInternalConnections()
    {
        // Must run in STA thread if Clipboard API is touched
        var thread = new Thread(() =>
        {
            var vm = CreateEditor();
            vm.AddNode("Input", new Point(100, 100));
            vm.AddNode("Trigger", new Point(300, 100));

            var node1 = vm.Nodes[0];
            var node2 = vm.Nodes[1];

            vm.CompleteConnection((node1.OutputConnectors[0], node2.InputConnectors[0]));

            // Select both nodes
            vm.SelectAll();
            Assert.Equal(2, vm.SelectedNodes.Count);

            vm.Copy();
            Assert.True(vm.CanPaste);

            vm.Paste();

            // After paste: should have 4 nodes (2 original + 2 pasted)
            Assert.Equal(4, vm.Nodes.Count);

            // Should have 2 connections (1 original + 1 between the 2 pasted nodes)
            Assert.Equal(2, vm.Connections.Count);

            // The 2 pasted nodes should be selected
            Assert.Equal(2, vm.SelectedNodes.Count);
            Assert.DoesNotContain(node1, vm.SelectedNodes);
            Assert.DoesNotContain(node2, vm.SelectedNodes);

            // Check location offset (+30, +30)
            var pastedNode1 = vm.SelectedNodes.First(n => n is InputNodeViewModel);
            Assert.Equal(130, pastedNode1.Location.X);
            Assert.Equal(130, pastedNode1.Location.Y);
            Assert.NotEqual(node1.Id, pastedNode1.Id);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void Clipboard_Duplicate_DuplicatesSelection()
    {
        var thread = new Thread(() =>
        {
            var vm = CreateEditor();
            vm.AddNode("Action", new Point(200, 150));
            var actNode = (ActionNodeViewModel)vm.Nodes[0];
            actNode.ActionType = ActionType.TOGGLE_TAG;

            vm.SelectExclusive(actNode);
            vm.Duplicate();

            Assert.Equal(2, vm.Nodes.Count);
            var duplicated = (ActionNodeViewModel)vm.SelectedNode!;
            Assert.NotEqual(actNode.Id, duplicated.Id);
            Assert.Equal(ActionType.TOGGLE_TAG, duplicated.ActionType);
            Assert.Equal(230, duplicated.Location.X);
            Assert.Equal(180, duplicated.Location.Y);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void SelectAll_SelectsAllNodesOnCanvas()
    {
        var vm = CreateEditor();
        vm.AddNode("Input", new Point(100, 100));
        vm.AddNode("Trigger", new Point(300, 100));
        vm.AddNode("Guard", new Point(500, 100));
        vm.AddNode("Action", new Point(700, 100));

        // Deselect all
        vm.DeselectAllNodes();
        Assert.Empty(vm.SelectedNodes);

        // Call SelectAll
        vm.SelectAll();

        Assert.Equal(4, vm.SelectedNodes.Count);
        Assert.All(vm.Nodes, n => Assert.True(n.IsSelected));
        Assert.Equal(vm.Nodes.Last(), vm.SelectedNode);
    }

    [Fact]
    public void Wire_SelectAndExecuteDelete_DeletesWireAndSupportsUndo()
    {
        var vm = CreateEditor();
        vm.AddNode("Input", new Point(100, 100));
        vm.AddNode("Trigger", new Point(300, 100));

        var node1 = vm.Nodes[0];
        var node2 = vm.Nodes[1];

        vm.CompleteConnection((node1.OutputConnectors[0], node2.InputConnectors[0]));
        Assert.Single(vm.Connections);
        var conn = vm.Connections[0];

        // Deselect nodes and select the wire
        vm.DeselectAllNodes();
        conn.IsSelected = true;
        Assert.True(vm.HasSelection);

        // Delete with DeleteSelectedNode (standard Delete key command)
        vm.DeleteSelectedNode();

        // Wire should be gone, nodes should still exist
        Assert.Empty(vm.Connections);
        Assert.Equal(2, vm.Nodes.Count);
        Assert.False(node1.OutputConnectors[0].IsConnected);
        Assert.False(node2.InputConnectors[0].IsConnected);

        // Undo should restore the wire
        vm.Undo();
        Assert.Single(vm.Connections);
        Assert.True(vm.Nodes[0].OutputConnectors[0].IsConnected);
        Assert.True(vm.Nodes[1].InputConnectors[0].IsConnected);
    }

    [Fact]
    public void Wire_DeleteConnectionCommand_DeletesWireDirectly()
    {
        var vm = CreateEditor();
        vm.AddNode("Input", new Point(100, 100));
        vm.AddNode("Trigger", new Point(300, 100));

        var node1 = vm.Nodes[0];
        var node2 = vm.Nodes[1];

        vm.CompleteConnection((node1.OutputConnectors[0], node2.InputConnectors[0]));
        var conn = vm.Connections[0];

        // Delete via DeleteConnectionCommand directly
        vm.DeleteConnection(conn);

        Assert.Empty(vm.Connections);
        Assert.False(node1.OutputConnectors[0].IsConnected);

        // Undo restores connection
        vm.Undo();
        Assert.Single(vm.Connections);
        Assert.True(vm.Nodes[0].OutputConnectors[0].IsConnected);
    }
}
