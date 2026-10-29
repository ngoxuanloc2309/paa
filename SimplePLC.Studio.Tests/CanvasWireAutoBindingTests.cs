using System.Linq;
using SimplePLC.Application.Enums;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class CanvasWireAutoBindingTests
{
    private readonly TagCatalogViewModel _tagCatalog;
    private readonly LogicEditorViewModel _vm;

    public CanvasWireAutoBindingTests()
    {
        _tagCatalog = new TagCatalogViewModel();
        _vm = new LogicEditorViewModel(_tagCatalog);
    }

    [Fact]
    public void Connect_InputNode_To_Comparator_AutoBinds_InputTag_And_Updates_FormulaExpression()
    {
        // Arrange
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var inNode = new InputNodeViewModel(ai0);
        var cmpNode = new ComparatorNodeViewModel(null, CompareOp.GT, 50);

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(cmpNode);

        Assert.Equal("IN > 50", cmpNode.FormulaExpression);
        Assert.False(cmpNode.HasWireConnection);

        // Act
        _vm.Connect(inNode.OutputConnectors[0], cmpNode.InputConnectors[0]);

        // Assert
        Assert.Equal(ai0, cmpNode.InputTag);
        Assert.True(cmpNode.HasWireConnection);
        Assert.Contains("AI0 > 50", cmpNode.FormulaExpression);
        Assert.Equal("🔗 Connected: AI0", cmpNode.InputConnectionBadge);
    }

    [Fact]
    public void Disconnect_Comparator_Restores_Default_Expression()
    {
        // Arrange
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var inNode = new InputNodeViewModel(ai0);
        var cmpNode = new ComparatorNodeViewModel(null, CompareOp.GT, 50);

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(cmpNode);
        _vm.Connect(inNode.OutputConnectors[0], cmpNode.InputConnectors[0]);
        Assert.Equal(ai0, cmpNode.InputTag);

        var conn = _vm.Connections.First(c => c.Source == inNode.OutputConnectors[0] && c.Target == cmpNode.InputConnectors[0]);

        // Act
        _vm.RemoveConnection(conn);

        // Assert
        Assert.Null(cmpNode.InputTag);
        Assert.False(cmpNode.HasWireConnection);
        Assert.Contains("IN > 50", cmpNode.FormulaExpression);
        Assert.Empty(cmpNode.InputConnectionBadge);
    }

    [Fact]
    public void Connect_InputNode_To_Trigger_AutoBinds_InputTag_And_Updates_TriggerActionText()
    {
        // Arrange
        var di0 = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var inNode = new InputNodeViewModel(di0);
        var trigNode = new TriggerNodeViewModel();

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(trigNode);

        Assert.Null(trigNode.InputTag);
        Assert.False(trigNode.HasWireConnection);

        // Act
        _vm.Connect(inNode.OutputConnectors[0], trigNode.InputConnectors[0]);

        // Assert
        Assert.Equal(di0, trigNode.InputTag);
        Assert.True(trigNode.HasWireConnection);
        Assert.StartsWith("DI0:", trigNode.TriggerActionText);
        Assert.Equal("🔗 Connected: DI0", trigNode.InputConnectionBadge);
    }

    [Fact]
    public void Disconnect_Trigger_Restores_Default_ActionText()
    {
        // Arrange
        var di0 = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var inNode = new InputNodeViewModel(di0);
        var trigNode = new TriggerNodeViewModel();

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(trigNode);
        _vm.Connect(inNode.OutputConnectors[0], trigNode.InputConnectors[0]);
        Assert.Equal(di0, trigNode.InputTag);

        var conn = _vm.Connections.First(c => c.Source == inNode.OutputConnectors[0] && c.Target == trigNode.InputConnectors[0]);

        // Act
        _vm.RemoveConnection(conn);

        // Assert
        Assert.Null(trigNode.InputTag);
        Assert.False(trigNode.HasWireConnection);
        Assert.DoesNotContain("DI0:", trigNode.TriggerActionText);
        Assert.Empty(trigNode.InputConnectionBadge);
    }

    [Fact]
    public void Connect_InputNode_To_Scale_Replaces_Abstract_X_With_Real_TagName()
    {
        // Arrange
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var inNode = new InputNodeViewModel(ai0);
        var scaleNode = new ScaleNodeViewModel { Gain = 0.01, Offset = 0, Unit = "bar" };

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(scaleNode);

        Assert.Contains("·x", scaleNode.FormulaExpression);
        Assert.False(scaleNode.HasWireInputConnection);

        // Act
        _vm.Connect(inNode.OutputConnectors[0], scaleNode.InputConnectors[0]);

        // Assert
        Assert.Equal(ai0, scaleNode.InputTag);
        Assert.True(scaleNode.HasWireInputConnection);
        Assert.Contains("·AI0", scaleNode.FormulaExpression);
        Assert.Equal("🔗 Connected: AI0", scaleNode.InputConnectionBadge);
    }

    [Fact]
    public void Connect_Scale_To_Action_AutoBinds_OutputTag()
    {
        // Arrange
        var do0 = _tagCatalog.AllTags.First(t => t.Name == "DO0");
        var scaleNode = new ScaleNodeViewModel();
        var actNode = new ActionNodeViewModel(do0);

        _vm.Nodes.Add(scaleNode);
        _vm.Nodes.Add(actNode);

        Assert.Null(scaleNode.OutputTag);
        Assert.False(scaleNode.HasWireOutputConnection);

        // Act
        _vm.Connect(scaleNode.OutputConnectors[0], actNode.InputConnectors[0]);

        // Assert
        Assert.Equal(do0, scaleNode.OutputTag);
        Assert.True(scaleNode.HasWireOutputConnection);
        Assert.Equal("🔗 Connected: DO0", scaleNode.OutputConnectionBadge);
    }

    [Fact]
    public void Disconnect_Scale_Restores_X_Formula_And_Clears_OutputTag()
    {
        // Arrange
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var do0 = _tagCatalog.AllTags.First(t => t.Name == "DO0");
        var inNode = new InputNodeViewModel(ai0);
        var scaleNode = new ScaleNodeViewModel();
        var actNode = new ActionNodeViewModel(do0);

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(scaleNode);
        _vm.Nodes.Add(actNode);

        _vm.Connect(inNode.OutputConnectors[0], scaleNode.InputConnectors[0]);
        _vm.Connect(scaleNode.OutputConnectors[0], actNode.InputConnectors[0]);

        Assert.Equal(ai0, scaleNode.InputTag);
        Assert.Equal(do0, scaleNode.OutputTag);
        Assert.Contains("·AI0", scaleNode.FormulaExpression);

        var connIn = _vm.Connections.First(c => c.Source == inNode.OutputConnectors[0] && c.Target == scaleNode.InputConnectors[0]);
        var connOut = _vm.Connections.First(c => c.Source == scaleNode.OutputConnectors[0] && c.Target == actNode.InputConnectors[0]);

        // Act
        _vm.RemoveConnection(connIn);
        _vm.RemoveConnection(connOut);

        // Assert
        Assert.Null(scaleNode.InputTag);
        Assert.Null(scaleNode.OutputTag);
        Assert.False(scaleNode.HasWireInputConnection);
        Assert.False(scaleNode.HasWireOutputConnection);
        Assert.Contains("·x", scaleNode.FormulaExpression);
    }

    [Fact]
    public void Connect_InputNode_To_ModbusWrite_DirectPin_AutoBinds_TargetTag()
    {
        // Arrange
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var inNode = new InputNodeViewModel(ai0);
        var mbWrite = new ModbusWriteNodeViewModel(slaveId: 1, mode: ModbusWriteMode.DirectRegisters);
        var pin = new ModbusPinBindingViewModel { Label = "Speed_PV" };
        mbWrite.Pins.Add(pin);
        mbWrite.SyncConnectors();

        var pinConnector = mbWrite.InputConnectors.First(c => c.Title == "Speed_PV");

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(mbWrite);

        Assert.Null(pin.TargetTag);

        // Act
        _vm.Connect(inNode.OutputConnectors[0], pinConnector);

        // Assert
        Assert.Equal(ai0, pin.TargetTag);

        // Disconnect Act
        var conn = _vm.Connections.First(c => c.Source == inNode.OutputConnectors[0] && c.Target == pinConnector);
        _vm.RemoveConnection(conn);

        // Disconnect Assert
        Assert.Null(pin.TargetTag);
    }
    [Fact]
    public void Reactive_InputNode_Tag_Change_Immediately_Propagates_To_Connected_TriggerNode()
    {
        // Arrange: Start with DI1 connected to Trigger
        var di1 = _tagCatalog.AllTags.First(t => t.Name == "DI1");
        var di0 = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var inNode = new InputNodeViewModel(di1);
        var trigNode = new TriggerNodeViewModel();

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(trigNode);
        _vm.Connect(inNode.OutputConnectors[0], trigNode.InputConnectors[0]);

        Assert.Equal(di1, trigNode.InputTag);
        Assert.StartsWith("DI1:", trigNode.TriggerActionText);

        // Act: User changes Tag on InputNode to DI0 while wire remains connected
        inNode.Tag = di0;

        // Assert: TriggerNode immediately receives DI0 without reconnecting wire
        Assert.Equal(di0, trigNode.InputTag);
        Assert.StartsWith("DI0:", trigNode.TriggerActionText);
        Assert.Equal("🔗 Connected: DI0", trigNode.InputConnectionBadge);
    }

    [Fact]
    public void Reactive_InputNode_Tag_Change_Immediately_Propagates_To_Connected_ComparatorNode()
    {
        // Arrange
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var ai1 = _tagCatalog.AllTags.First(t => t.Name == "AI1");
        var inNode = new InputNodeViewModel(ai0);
        var cmpNode = new ComparatorNodeViewModel(null, CompareOp.GT, 50);

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(cmpNode);
        _vm.Connect(inNode.OutputConnectors[0], cmpNode.InputConnectors[0]);

        Assert.Equal(ai0, cmpNode.InputTag);
        Assert.Contains("AI0 > 50", cmpNode.FormulaExpression);

        // Act: Change InputNode tag to AI1
        inNode.Tag = ai1;

        // Assert
        Assert.Equal(ai1, cmpNode.InputTag);
        Assert.Contains("AI1 > 50", cmpNode.FormulaExpression);
        Assert.Equal("🔗 Connected: AI1", cmpNode.InputConnectionBadge);
    }

    [Fact]
    public void Reactive_InputNode_Tag_Change_Immediately_Propagates_To_Connected_ScaleNode()
    {
        // Arrange
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var ai1 = _tagCatalog.AllTags.First(t => t.Name == "AI1");
        var inNode = new InputNodeViewModel(ai0);
        var scaleNode = new ScaleNodeViewModel();

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(scaleNode);
        _vm.Connect(inNode.OutputConnectors[0], scaleNode.InputConnectors[0]);

        Assert.Equal(ai0, scaleNode.InputTag);
        Assert.Contains("AI0", scaleNode.FormulaExpression);

        // Act: Change InputNode tag to AI1
        inNode.Tag = ai1;

        // Assert
        Assert.Equal(ai1, scaleNode.InputTag);
        Assert.Contains("AI1", scaleNode.FormulaExpression);
        Assert.Equal("🔗 Connected: AI1", scaleNode.InputConnectionBadge);
    }

    [Fact]
    public void Reactive_MultiHop_Chain_Propagation_Input_To_Scale_To_Comparator()
    {
        // Arrange: Input (AI0) -> Scale -> Comparator
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var ai1 = _tagCatalog.AllTags.First(t => t.Name == "AI1");
        var inNode = new InputNodeViewModel(ai0);
        var scaleNode = new ScaleNodeViewModel();
        var cmpNode = new ComparatorNodeViewModel(null, CompareOp.GT, 50);

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(scaleNode);
        _vm.Nodes.Add(cmpNode);

        _vm.Connect(inNode.OutputConnectors[0], scaleNode.InputConnectors[0]);
        _vm.Connect(scaleNode.OutputConnectors[0], cmpNode.InputConnectors[0]);

        Assert.Equal(ai0, scaleNode.InputTag);
        Assert.Equal(ai0, cmpNode.InputTag);

        // Act: User changes InputNode tag from AI0 to AI1
        inNode.Tag = ai1;

        // Assert: Both downstream Scale and Comparator update to AI1 seamlessly
        Assert.Equal(ai1, scaleNode.InputTag);
        Assert.Equal(ai1, cmpNode.InputTag);
        Assert.Contains("AI1 > 50", cmpNode.FormulaExpression);
    }

    [Fact]
    public void Reactive_InputNode_Tag_Cleared_Resets_Connected_Trigger_And_Comparator()
    {
        // Arrange
        var di0 = _tagCatalog.AllTags.First(t => t.Name == "DI0");
        var inNode = new InputNodeViewModel(di0);
        var trigNode = new TriggerNodeViewModel();
        var cmpNode = new ComparatorNodeViewModel(null, CompareOp.GT, 50);

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(trigNode);
        _vm.Nodes.Add(cmpNode);

        _vm.Connect(inNode.OutputConnectors[0], trigNode.InputConnectors[0]);
        _vm.Connect(inNode.OutputConnectors[0], cmpNode.InputConnectors[0]);

        Assert.Equal(di0, trigNode.InputTag);
        Assert.Equal(di0, cmpNode.InputTag);

        // Act: Input tag is cleared/set to null
        inNode.Tag = null;

        // Assert: Connected nodes reset cleanly without throwing exception
        Assert.Null(trigNode.InputTag);
        Assert.Null(cmpNode.InputTag);
        Assert.Contains("IN > 50", cmpNode.FormulaExpression);
        Assert.Empty(cmpNode.InputConnectionBadge);
    }

    [Fact]
    public void Reactive_InputNode_Tag_Change_Propagates_To_ModbusWrite_DirectPin()
    {
        // Arrange
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var ai1 = _tagCatalog.AllTags.First(t => t.Name == "AI1");
        var inNode = new InputNodeViewModel(ai0);
        var mbWrite = new ModbusWriteNodeViewModel(slaveId: 1, mode: ModbusWriteMode.DirectRegisters);
        var pin = new ModbusPinBindingViewModel { Label = "Speed_PV" };
        mbWrite.Pins.Add(pin);
        mbWrite.SyncConnectors();

        var pinConnector = mbWrite.InputConnectors.First(c => c.Title == "Speed_PV");

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(mbWrite);
        _vm.Connect(inNode.OutputConnectors[0], pinConnector);

        Assert.Equal(ai0, pin.TargetTag);

        // Act
        inNode.Tag = ai1;

        // Assert: Pin TargetTag is updated to AI1
        Assert.Equal(ai1, pin.TargetTag);
    }

    [Fact]
    public void Reactive_InputNode_Tag_Change_Propagates_To_MqttPublish_DataTag()
    {
        // Arrange
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var ai1 = _tagCatalog.AllTags.First(t => t.Name == "AI1");
        var inNode = new InputNodeViewModel(ai0);
        var mqttNode = new MqttPublishNodeViewModel();

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(mqttNode);
        _vm.Connect(inNode.OutputConnectors[0], mqttNode.DefaultValConnector);

        Assert.Equal(ai0, mqttNode.DataTag);

        // Act
        inNode.Tag = ai1;

        // Assert
        Assert.Equal(ai1, mqttNode.DataTag);
    }

    [Fact]
    public void Reactive_CycleSafety_ClosedLoop_DoesNotHang_Or_StackOverflow()
    {
        // Arrange: Create cyclic feedback connections
        var ai0 = _tagCatalog.AllTags.First(t => t.Name == "AI0");
        var ai1 = _tagCatalog.AllTags.First(t => t.Name == "AI1");
        var inNode = new InputNodeViewModel(ai0);
        var scaleNode = new ScaleNodeViewModel();
        var cmpNode = new ComparatorNodeViewModel();

        _vm.Nodes.Add(inNode);
        _vm.Nodes.Add(scaleNode);
        _vm.Nodes.Add(cmpNode);

        _vm.Connect(inNode.OutputConnectors[0], scaleNode.InputConnectors[0]);
        _vm.Connect(scaleNode.OutputConnectors[0], cmpNode.InputConnectors[0]);
        // Simulate a cycle connection back to scale
        _vm.Connect(cmpNode.OutputConnectors[0], scaleNode.InputConnectors[0]);

        // Act & Assert: Must complete instantaneously without infinite recursion or stack overflow
        var sw = System.Diagnostics.Stopwatch.StartNew();
        inNode.Tag = ai1;
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 500, "BFS propagation must terminate quickly even with cycles.");
        Assert.Equal(ai1, scaleNode.InputTag);
    }
}
