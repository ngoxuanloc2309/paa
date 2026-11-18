using System.Threading;
using System.Windows;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử đơn vị cho tính năng Undo/Redo và Clipboard (Copy/Paste/Cut) trên Logic Editor canvas.
/// </summary>
public class UndoRedoClipboardTests
{
    private LogicEditorViewModel CreateEditor()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        vm.DebounceDelayMs = 0;
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
    public void Clipboard_Duplicate_TimerNode_PreservesAllParameters()
    {
        var thread = new Thread(() =>
        {
            var vm = CreateEditor();
            vm.AddNode("Timer", new Point(100, 100));
            var timerNode = Assert.IsType<TimerNodeViewModel>(vm.Nodes[0]);
            timerNode.TimerMode = "TOF";
            timerNode.SetDuration(15.5, "s");
            var outTag = vm.TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.DiscreteOutput);
            timerNode.OutputTag = outTag;

            vm.SelectExclusive(timerNode);
            vm.Duplicate();

            Assert.Equal(2, vm.Nodes.Count);
            var duplicated = Assert.IsType<TimerNodeViewModel>(vm.SelectedNode);
            Assert.NotEqual(timerNode.Id, duplicated.Id);
            Assert.Equal("TOF", duplicated.TimerMode);
            Assert.Equal(15.5, duplicated.DurationValue);
            Assert.Equal("s", duplicated.TimeUnit);
            Assert.Equal(outTag?.Name, duplicated.OutputTag?.Name);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void Clipboard_Duplicate_CounterNode_PreservesAllParameters()
    {
        var thread = new Thread(() =>
        {
            var vm = CreateEditor();
            vm.AddNode("Counter", new Point(100, 100));
            var counterNode = Assert.IsType<CounterNodeViewModel>(vm.Nodes[0]);
            counterNode.CounterMode = "CTD";
            counterNode.PresetValue = 75;

            vm.SelectExclusive(counterNode);
            vm.Duplicate();

            Assert.Equal(2, vm.Nodes.Count);
            var duplicated = Assert.IsType<CounterNodeViewModel>(vm.SelectedNode);
            Assert.NotEqual(counterNode.Id, duplicated.Id);
            Assert.Equal("CTD", duplicated.CounterMode);
            Assert.Equal(75, duplicated.PresetValue);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void Clipboard_Duplicate_ScaleNode_PreservesAllParameters()
    {
        var thread = new Thread(() =>
        {
            var vm = CreateEditor();
            vm.AddNode("Scale", new Point(100, 100));
            var scaleNode = Assert.IsType<ScaleNodeViewModel>(vm.Nodes[0]);
            scaleNode.Gain = 2.5;
            scaleNode.Offset = -10.0;
            scaleNode.Unit = "PSI";
            scaleNode.ClampMin = -50.0;
            scaleNode.ClampMax = 200.0;

            vm.SelectExclusive(scaleNode);
            vm.Duplicate();

            Assert.Equal(2, vm.Nodes.Count);
            var duplicated = Assert.IsType<ScaleNodeViewModel>(vm.SelectedNode);
            Assert.NotEqual(scaleNode.Id, duplicated.Id);
            Assert.Equal(2.5, duplicated.Gain);
            Assert.Equal(-10.0, duplicated.Offset);
            Assert.Equal("PSI", duplicated.Unit);
            Assert.Equal(-50.0, duplicated.ClampMin);
            Assert.Equal(200.0, duplicated.ClampMax);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void Clipboard_Duplicate_MathNode_PreservesAllParametersAndPins()
    {
        var thread = new Thread(() =>
        {
            var vm = CreateEditor();
            vm.AddNode("Math", new Point(100, 100));
            var mathNode = Assert.IsType<MathNodeViewModel>(vm.Nodes[0]);
            mathNode.Operator = MathOperator.Multiply;
            mathNode.Pins[0].IsConstant = true;
            mathNode.Pins[0].ConstantValue = 42.5;

            vm.SelectExclusive(mathNode);
            vm.Duplicate();

            Assert.Equal(2, vm.Nodes.Count);
            var duplicated = Assert.IsType<MathNodeViewModel>(vm.SelectedNode);
            Assert.NotEqual(mathNode.Id, duplicated.Id);
            Assert.Equal(MathOperator.Multiply, duplicated.Operator);
            Assert.True(duplicated.Pins[0].IsConstant);
            Assert.Equal(42.5, duplicated.Pins[0].ConstantValue);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void Clipboard_Duplicate_TriggerNode_PreservesInputTagAndThresholds()
    {
        var thread = new Thread(() =>
        {
            var vm = CreateEditor();
            vm.AddNode("Trigger", new Point(100, 100));
            var trigNode = Assert.IsType<TriggerNodeViewModel>(vm.Nodes[0]);
            var tag = vm.TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.AnalogInput)
                      ?? vm.TagCatalog.AllTags.First();
            trigNode.InputTag = tag;
            trigNode.TriggerType = TriggerType.ON_FALL;
            trigNode.CompareOp = CompareOp.GT;
            trigNode.ThresholdLo = 55.0;
            trigNode.ForMs = 1200;

            vm.SelectExclusive(trigNode);
            vm.Duplicate();

            Assert.Equal(2, vm.Nodes.Count);
            var duplicated = Assert.IsType<TriggerNodeViewModel>(vm.SelectedNode);
            Assert.NotEqual(trigNode.Id, duplicated.Id);
            Assert.Equal(tag.Name, duplicated.InputTag?.Name);
            Assert.Equal(TriggerType.ON_FALL, duplicated.TriggerType);
            Assert.Equal(CompareOp.GT, duplicated.CompareOp);
            Assert.Equal(55.0, duplicated.ThresholdLo);
            Assert.Equal(1200u, duplicated.ForMs);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void Clipboard_Duplicate_ComparatorNode_PreservesCompareOpAndThresholds()
    {
        var thread = new Thread(() =>
        {
            var vm = CreateEditor();
            vm.AddNode("Comparator", new Point(100, 100));
            var cmpNode = Assert.IsType<ComparatorNodeViewModel>(vm.Nodes[0]);
            var tag = vm.TagCatalog.AllTags.FirstOrDefault(t => t.Kind == TagKind.AnalogInput)
                      ?? vm.TagCatalog.AllTags.First();
            cmpNode.InputTag = tag;
            cmpNode.CompareOp = CompareOp.BETWEEN;
            cmpNode.ThresholdLo = 10.0;
            cmpNode.ThresholdHi = 90.0;

            vm.SelectExclusive(cmpNode);
            vm.Duplicate();

            Assert.Equal(2, vm.Nodes.Count);
            var duplicated = Assert.IsType<ComparatorNodeViewModel>(vm.SelectedNode);
            Assert.NotEqual(cmpNode.Id, duplicated.Id);
            Assert.Equal(tag.Name, duplicated.InputTag?.Name);
            Assert.Equal(CompareOp.BETWEEN, duplicated.CompareOp);
            Assert.Equal(10.0, duplicated.ThresholdLo);
            Assert.Equal(90.0, duplicated.ThresholdHi);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void Clipboard_Duplicate_ModbusReadNode_PreservesParametersAndPins()
    {
        var thread = new Thread(() =>
        {
            var vm = CreateEditor();
            vm.AddNode("ModbusRead", new Point(100, 100));
            var mbNode = Assert.IsType<ModbusReadNodeViewModel>(vm.Nodes[0]);
            mbNode.PortIndex = 2;
            mbNode.SlaveId = 5;
            mbNode.StartAddress = 100;
            mbNode.RegCount = 4;
            mbNode.PollIntervalMs = 250;

            vm.SelectExclusive(mbNode);
            vm.Duplicate();

            Assert.Equal(2, vm.Nodes.Count);
            var duplicated = Assert.IsType<ModbusReadNodeViewModel>(vm.SelectedNode);
            Assert.NotEqual(mbNode.Id, duplicated.Id);
            Assert.Equal((byte)2, duplicated.PortIndex);
            Assert.Equal((byte)5, duplicated.SlaveId);
            Assert.Equal((ushort)100, duplicated.StartAddress);
            Assert.Equal((ushort)4, duplicated.RegCount);
            Assert.Equal(250u, duplicated.PollIntervalMs);
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
    }

    [Fact]
    public void Clipboard_Duplicate_MultipleConnectedNodes_PreservesInterconnections()
    {
        var thread = new Thread(() =>
        {
            var vm = CreateEditor();
            vm.AddNode("Input", new Point(100, 100));
            vm.AddNode("Trigger", new Point(300, 100));

            var node1 = vm.Nodes[0];
            var node2 = vm.Nodes[1];

            vm.CompleteConnection((node1.OutputConnectors[0], node2.InputConnectors[0]));
            Assert.Single(vm.Connections);

            // Select both nodes
            vm.DeselectAllNodes();
            node1.IsSelected = true;
            node2.IsSelected = true;
            Assert.True(node1.IsSelected);
            Assert.True(node2.IsSelected);

            vm.Duplicate();

            // Should have 4 nodes and 2 connections now
            Assert.Equal(4, vm.Nodes.Count);
            Assert.Equal(2, vm.Connections.Count);

            // The newly created nodes must have a connection between them
            var newNodes = vm.Nodes.Skip(2).ToList();
            var newConn = vm.Connections.Last();
            Assert.Contains(newConn.Source.Node, newNodes);
            Assert.Contains(newConn.Target.Node, newNodes);
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
