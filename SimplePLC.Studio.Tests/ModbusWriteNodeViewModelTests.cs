using System.Linq;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class ModbusWriteNodeViewModelTests
{
    [Fact]
    public void Constructor_DefaultChannelIndexedMode_InitializesStandardConnectors()
    {
        var node = new ModbusWriteNodeViewModel();

        Assert.Equal("MB_WRITE", node.Title);
        Assert.Equal(ModbusWriteMode.ChannelIndexed, node.WriteMode);
        Assert.Contains("MB_WRITE", node.SummaryText);
        Assert.Contains("CH:9", node.NodeInfoSummary);

        // Inputs: Trig, Channel, Command, Param (4 total)
        Assert.Equal(4, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("Channel", node.InputConnectors[1].Title);
        Assert.Equal("Command", node.InputConnectors[2].Title);
        Assert.Equal("Param", node.InputConnectors[3].Title);

        // Outputs: Done, Busy, Error, ErrCode (4 total)
        Assert.Equal(4, node.OutputConnectors.Count);
        Assert.Equal("Done", node.OutputConnectors[0].Title);
        Assert.Equal("Busy", node.OutputConnectors[1].Title);
        Assert.Equal("Error", node.OutputConnectors[2].Title);
        Assert.Equal("ErrCode", node.OutputConnectors[3].Title);
    }

    [Fact]
    public void WriteMode_SwitchToDirectRegisters_UpdatesConnectorsToDynamicPins()
    {
        var node = new ModbusWriteNodeViewModel();
        Assert.Equal(4, node.InputConnectors.Count);

        node.WriteMode = ModbusWriteMode.DirectRegisters;
        // In Direct mode with 0 pins: only Trig input connector
        Assert.Single(node.InputConnectors);
        Assert.Equal("Trig", node.InputConnectors[0].Title);

        // Add 2 pins
        node.Pins.Add(new ModbusPinBindingViewModel { Label = "Setpoint_1", RegOffset = 0 });
        node.Pins.Add(new ModbusPinBindingViewModel { Label = "Setpoint_2", RegOffset = 1 });

        // Trig + 2 pins = 3 inputs
        Assert.Equal(3, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("Setpoint_1", node.InputConnectors[1].Title);
        Assert.Equal("Setpoint_2", node.InputConnectors[2].Title);

        // Switch back to ChannelIndexed
        node.WriteMode = ModbusWriteMode.ChannelIndexed;
        Assert.Equal(4, node.InputConnectors.Count);
        Assert.Equal("Channel", node.InputConnectors[1].Title);
    }

    [Theory]
    [InlineData("MB_WRITE")]
    [InlineData("MB_WR")]
    [InlineData("ModbusWrite")]
    [InlineData("MbWrite")]
    public void CreateNodeInstance_SupportsAllMbWriteAliases(string alias)
    {
        var vm = new LogicEditorViewModel(new TagCatalogViewModel());
        var node = vm.CreateNodeInstance(alias);

        Assert.NotNull(node);
        Assert.IsType<ModbusWriteNodeViewModel>(node);
        Assert.Equal("MB_WRITE", node.Title);
    }

    [Fact]
    public void Serialization_And_Deserialization_Roundtrip_PreservesAllChannelIndexedProperties()
    {
        var editor = new LogicEditorViewModel(new TagCatalogViewModel());
        var originalNode = new ModbusWriteNodeViewModel
        {
            SlaveId = 5,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            BaseAddressCommand = 0x0200,
            BaseAddressParam = 0x0240,
            ChannelCount = 18,
            IsExclusiveChannel = true,
            TriggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge,
            CyclicIntervalMs = 250,
            TimeoutMs = 600,
            CustomLabel = "LED_Wall_Controller"
        };

        var projectNodeData = LogicEditorViewModel.ExportSingleNodeData(originalNode);

        Assert.Equal("ModbusWrite", projectNodeData.Type);
        Assert.Equal((byte)5, projectNodeData.ModbusWriteSlaveId);
        Assert.Equal("ChannelIndexed", projectNodeData.ModbusWriteMode);
        Assert.Equal((ushort)0x0200, projectNodeData.ModbusWriteBaseAddressCommand);
        Assert.Equal((ushort)0x0240, projectNodeData.ModbusWriteBaseAddressParam);
        Assert.Equal((ushort)18, projectNodeData.ModbusWriteChannelCount);
        Assert.True(projectNodeData.ModbusWriteIsExclusiveChannel);
        Assert.Equal("LED_Wall_Controller", projectNodeData.CustomLabel);

        var restoredNode = editor.CreateNodeFromData(projectNodeData) as ModbusWriteNodeViewModel;

        Assert.NotNull(restoredNode);
        Assert.Equal((byte)5, restoredNode.SlaveId);
        Assert.Equal(ModbusWriteMode.ChannelIndexed, restoredNode.WriteMode);
        Assert.Equal((ushort)0x0200, restoredNode.BaseAddressCommand);
        Assert.Equal((ushort)0x0240, restoredNode.BaseAddressParam);
        Assert.Equal((ushort)18, restoredNode.ChannelCount);
        Assert.True(restoredNode.IsExclusiveChannel);
        Assert.Equal("LED_Wall_Controller", restoredNode.CustomLabel);
        Assert.Equal(4, restoredNode.InputConnectors.Count);
    }

    [Fact]
    public void Serialization_And_Deserialization_Roundtrip_PreservesAllDirectRegistersProperties()
    {
        var editor = new LogicEditorViewModel(new TagCatalogViewModel());
        var originalNode = new ModbusWriteNodeViewModel
        {
            SlaveId = 3,
            WriteMode = ModbusWriteMode.DirectRegisters,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters,
            StartAddress = 100,
            PointCount = 2,
            TriggerMode = ModbusWriteTriggerMode.OnDataChange,
            CyclicIntervalMs = 1000,
            TimeoutMs = 400
        };

        originalNode.Pins.Add(new ModbusPinBindingViewModel
        {
            Label = "Flow_SP",
            RegOffset = 0,
            DataType = ModbusDataType.Real,
            Endianness = ModbusEndianness.MidBig_CDAB,
            Multiplier = 0.1f
        });

        var projectNodeData = LogicEditorViewModel.ExportSingleNodeData(originalNode);
        var restoredNode = editor.CreateNodeFromData(projectNodeData) as ModbusWriteNodeViewModel;

        Assert.NotNull(restoredNode);
        Assert.Equal((byte)3, restoredNode.SlaveId);
        Assert.Equal(ModbusWriteMode.DirectRegisters, restoredNode.WriteMode);
        Assert.Equal(ModbusMasterWriteFunctionCode.WriteMultipleRegisters, restoredNode.FunctionCode);
        Assert.Equal((ushort)100, restoredNode.StartAddress);
        Assert.Single(restoredNode.Pins);
        Assert.Equal("Flow_SP", restoredNode.Pins[0].Label);
        Assert.Equal(ModbusDataType.Real, restoredNode.Pins[0].DataType);
        Assert.Equal(ModbusEndianness.MidBig_CDAB, restoredNode.Pins[0].Endianness);
        Assert.Equal(0.1f, restoredNode.Pins[0].Multiplier, 3);
        // Direct mode: Trig + 1 pin = 2 inputs
        Assert.Equal(2, restoredNode.InputConnectors.Count);
    }
}
