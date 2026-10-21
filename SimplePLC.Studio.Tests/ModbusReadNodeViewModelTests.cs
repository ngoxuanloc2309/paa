using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class ModbusReadNodeViewModelTests
{
    [Fact]
    public void Constructor_InitializesDefaultConnectors_AndTitleIsMbRead()
    {
        var node = new ModbusReadNodeViewModel();

        Assert.Equal("MB_READ", node.Title);
        Assert.Contains("MB_READ", node.SummaryText);
        Assert.Equal("ID:1 | FC03", node.NodeInfoSummary);
        Assert.Empty(node.InputConnectors);

        Assert.Equal(3, node.OutputConnectors.Count);
        Assert.Equal("Valid", node.OutputConnectors[0].Title);
        Assert.Equal("PinTooltipMbValid", node.OutputConnectors[0].TooltipKey);
        Assert.Equal("Error", node.OutputConnectors[1].Title);
        Assert.Equal("PinTooltipMbErr", node.OutputConnectors[1].TooltipKey);
        Assert.Equal("ErrCode", node.OutputConnectors[2].Title);
        Assert.Equal("PinTooltipMbErrCode", node.OutputConnectors[2].TooltipKey);
    }

    [Fact]
    public void AddPin_SynchronizesDynamicOutputConnector()
    {
        var node = new ModbusReadNodeViewModel();

        var pin = new ModbusPinBindingViewModel
        {
            Label = "Temp_C",
            RegOffset = 0,
            DataType = ModbusDataType.Real,
            Endianness = ModbusEndianness.MidBig_CDAB,
            Multiplier = 0.1f
        };

        node.Pins.Add(pin);

        Assert.Single(node.Pins);
        // 3 fixed (Valid, Err, Err_Code) + 1 dynamic = 4
        Assert.Equal(4, node.OutputConnectors.Count);
        Assert.Equal("Temp_C", node.OutputConnectors[3].Title);
        Assert.NotNull(pin.Connector);
        Assert.Equal(pin.Connector, node.OutputConnectors[3]);
    }

    [Fact]
    public void RenamePin_UpdatesConnectorTitle()
    {
        var node = new ModbusReadNodeViewModel();
        var pin = new ModbusPinBindingViewModel
        {
            Label = "OldLabel",
            RegOffset = 1,
            DataType = ModbusDataType.UInt16
        };
        node.Pins.Add(pin);

        Assert.Equal("OldLabel", node.OutputConnectors[3].Title);

        pin.Label = "NewPressure";
        Assert.Equal("NewPressure", node.OutputConnectors[3].Title);
    }

    [Fact]
    public void RemovePin_RemovesConnectorFromOutputConnectors()
    {
        var node = new ModbusReadNodeViewModel();
        var pin1 = new ModbusPinBindingViewModel { Label = "Pin1", RegOffset = 0 };
        var pin2 = new ModbusPinBindingViewModel { Label = "Pin2", RegOffset = 1 };

        node.Pins.Add(pin1);
        node.Pins.Add(pin2);
        Assert.Equal(5, node.OutputConnectors.Count);

        node.Pins.Remove(pin1);
        Assert.Equal(4, node.OutputConnectors.Count);
        Assert.DoesNotContain(pin1.Connector, node.OutputConnectors);
        Assert.Contains(pin2.Connector, node.OutputConnectors);
    }

    [Theory]
    [InlineData("MB_READ")]
    [InlineData("MB_RD")]
    [InlineData("ModbusRead")]
    [InlineData("Modbus")]
    [InlineData("MbRead")]
    public void CreateNodeInstance_SupportsAllMbReadAliases(string alias)
    {
        var vm = new LogicEditorViewModel(new TagCatalogViewModel());
        var node = vm.CreateNodeInstance(alias);

        Assert.NotNull(node);
        Assert.IsType<ModbusReadNodeViewModel>(node);
        Assert.Equal("MB_READ", node.Title);
    }

    [Fact]
    public void StatusPillText_ReflectsStateTransitions_AndErrors()
    {
        var node = new ModbusReadNodeViewModel { PollIntervalMs = 500 };
        Assert.Equal("READY", node.StatusPillText);
        Assert.False(node.IsErrorActive);

        node.PollCount = 42;
        Assert.Equal("42 polls", node.StatusPillText);

        node.IsErrorActive = true;
        Assert.Equal("ERR: TIMEOUT", node.StatusPillText);

        node.LastErrorMessage = "CRC";
        Assert.Equal("ERR: CRC", node.StatusPillText);
    }

    [Fact]
    public void PinBinding_ToDto_MapsAllPropertiesAccurately()
    {
        var tag = new SimplePLC.Studio.Models.TagModel { Index = 14, Name = "Motor_Speed" };
        var pin = new ModbusPinBindingViewModel
        {
            Label = "Spd",
            RegOffset = 2,
            DataType = ModbusDataType.UInt32,
            Endianness = ModbusEndianness.MidBig_CDAB,
            Multiplier = 0.5f,
            TargetTag = tag
        };

        var dto = pin.ToDto();

        Assert.Equal("Spd", dto.Label);
        Assert.Equal((ushort)2, dto.RegOffset);
        Assert.Equal(ModbusDataType.UInt32, dto.DataType);
        Assert.Equal(ModbusEndianness.MidBig_CDAB, dto.Endianness);
        Assert.Equal(0.5f, dto.Multiplier, 3);
        Assert.Equal((ushort)14, dto.TargetTagIndex);
    }

    [Fact]
    public void NodeProperties_UpdatesNodeInfoSummary()
    {
        var node = new ModbusReadNodeViewModel
        {
            SlaveId = 3,
            FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters
        };

        Assert.Equal("ID:3 | FC03", node.NodeInfoSummary);
        Assert.Equal("FC 03", node.FunctionCodeBadge);
        Assert.Equal("Read Holding Registers", node.FunctionCodeName);
        Assert.Equal("FC 03 (Read Holding Registers)", node.FunctionCodeDisplay);

        node.SlaveId = 15;
        node.FunctionCode = ModbusMasterFunctionCode.ReadInputRegisters;
        Assert.Equal("ID:15 | FC04", node.NodeInfoSummary);
        Assert.Equal("FC 04", node.FunctionCodeBadge);
        Assert.Equal("Read Input Registers", node.FunctionCodeName);
        Assert.Equal("FC 04 (Read Input Registers)", node.FunctionCodeDisplay);

        node.FunctionCode = ModbusMasterFunctionCode.ReadCoils;
        Assert.Equal("FC 01", node.FunctionCodeBadge);
        Assert.Equal("Read Coils", node.FunctionCodeName);
        Assert.Equal("FC 01 (Read Coils)", node.FunctionCodeDisplay);

        node.FunctionCode = ModbusMasterFunctionCode.ReadDiscreteInputs;
        Assert.Equal("FC 02", node.FunctionCodeBadge);
        Assert.Equal("Read Discrete Inputs", node.FunctionCodeName);
        Assert.Equal("FC 02 (Read Discrete Inputs)", node.FunctionCodeDisplay);

        node.PollIntervalMs = 750;
        Assert.Equal("750 ms", node.PollIntervalDisplay);
    }

    [Fact]
    public void ReadMode_SwitchingBetweenCyclicAndTrigger_ShowsTrigOnlyInTriggerMode()
    {
        var node = new ModbusReadNodeViewModel { PollIntervalMs = 500 };
        Assert.Equal(ModbusReadMode.Cyclic, node.ReadMode);
        Assert.Equal("Cyclic (500ms)", node.ReadModeSummaryText);
        Assert.False(node.IsTriggerMode);
        Assert.Empty(node.InputConnectors);

        // Switch to OnTriggerRisingEdge
        node.ReadMode = ModbusReadMode.OnTriggerRisingEdge;
        Assert.Equal("On Trig", node.ReadModeSummaryText);
        Assert.True(node.IsTriggerMode);
        Assert.Single(node.InputConnectors);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("PinTooltipMbReadTrig", node.InputConnectors[0].TooltipKey);

        // Switch back to Cyclic
        node.ReadMode = ModbusReadMode.Cyclic;
        Assert.Equal("Cyclic (500ms)", node.ReadModeSummaryText);
        Assert.False(node.IsTriggerMode);
        Assert.Empty(node.InputConnectors);
    }

    [Fact]
    public void ModbusRead_Serialization_Roundtrip_PreservesReadMode()
    {
        var editor = new LogicEditorViewModel(new TagCatalogViewModel());
        var originalNode = new ModbusReadNodeViewModel
        {
            SlaveId = 4,
            ReadMode = ModbusReadMode.OnTriggerRisingEdge,
            PollIntervalMs = 200
        };

        var projectNodeData = LogicEditorViewModel.ExportSingleNodeData(originalNode);
        Assert.Equal("OnTriggerRisingEdge", projectNodeData.ModbusReadMode);

        var restoredNode = editor.CreateNodeFromData(projectNodeData) as ModbusReadNodeViewModel;
        Assert.NotNull(restoredNode);
        Assert.Equal(ModbusReadMode.OnTriggerRisingEdge, restoredNode.ReadMode);
        Assert.Single(restoredNode.InputConnectors);
        Assert.Equal("Trig", restoredNode.InputConnectors[0].Title);
    }
}


