using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class ModbusWriteConfigDialogViewModelTests
{
    [Fact]
    public void Constructor_InitializesValuesFromNode()
    {
        var node = new ModbusWriteNodeViewModel
        {
            SlaveId = 2,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            BaseAddressCommand = 0x0100,
            BaseAddressParam = 0x0120,
            ChannelCount = 9,
            IsExclusiveChannel = true,
            TriggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge,
            CyclicIntervalMs = 500,
            TimeoutMs = 300
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel());

        Assert.Equal((byte)2, vm.SlaveId);
        Assert.Equal(ModbusWriteMode.ChannelIndexed, vm.WriteMode);
        Assert.Equal((ushort)0x0100, vm.BaseAddressCommand);
        Assert.Equal((ushort)0x0120, vm.BaseAddressParam);
        Assert.Equal((ushort)9, vm.ChannelCount);
        Assert.True(vm.IsExclusiveChannel);
        Assert.True(vm.IsChannelIndexedMode);
        Assert.False(vm.IsDirectRegistersMode);
        Assert.Contains("Slave 2", vm.SummaryBadgeText);
        Assert.Contains("0x0100", vm.SummaryBadgeText);
    }

    [Fact]
    public void GenerateFramePreview_ChannelIndexed_ProducesCommandAndParamFramesWithCrc()
    {
        var node = new ModbusWriteNodeViewModel
        {
            SlaveId = 1,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            BaseAddressCommand = 0x0100,
            BaseAddressParam = 0x0120,
            ChannelCount = 9,
            IsExclusiveChannel = true
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel())
        {
            SimChannel = 4,
            SimCommand = 1,
            SimParamHex = "00FF00" // Green
        };

        vm.GenerateFramePreview();

        Assert.NotEmpty(vm.SimGeneratedFrames);
        Assert.Contains("FC06", vm.SimGeneratedFrames);
        Assert.Contains("FC16", vm.SimGeneratedFrames);
        // Reg addr for Channel 4 command is 0x0100 + 4 - 1 = 0x0103
        Assert.Contains("0x0103", vm.SimGeneratedFrames);
        // Reg addr for Channel 4 param is 0x0120 + ((4-1)*2) = 0x0126
        Assert.Contains("0x0126", vm.SimGeneratedFrames);

        Assert.NotEmpty(vm.SimExplanation);
        Assert.Contains("Exclusive", vm.SimExplanation);
    }

    [Fact]
    public void GenerateFramePreview_DirectRegisters_ProducesSingleAndMultipleRegisterFrames()
    {
        var node = new ModbusWriteNodeViewModel
        {
            SlaveId = 3,
            WriteMode = ModbusWriteMode.DirectRegisters,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister,
            StartAddress = 200
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel());
        vm.GenerateFramePreview();

        Assert.Contains("FC06", vm.SimGeneratedFrames);
        Assert.Contains("Addr 0x00C8", vm.SimGeneratedFrames);

        // Switch to FC16
        vm.FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters;
        vm.PointCount = 2;
        vm.GenerateFramePreview();

        Assert.Contains("FC16", vm.SimGeneratedFrames);
        Assert.Contains("Count=2", vm.SimGeneratedFrames);
    }

    [Fact]
    public void Apply_CommitsPropertiesBackToNode()
    {
        var node = new ModbusWriteNodeViewModel
        {
            SlaveId = 1,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            ChannelCount = 9
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel())
        {
            SlaveId = 7,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            ChannelCount = 18,
            BaseAddressCommand = 0x0300,
            BaseAddressParam = 0x0350,
            IsExclusiveChannel = false,
            CyclicIntervalMs = 800,
            TimeoutMs = 450
        };

        vm.Apply();

        Assert.Equal((byte)7, node.SlaveId);
        Assert.Equal((ushort)18, node.ChannelCount);
        Assert.Equal((ushort)0x0300, node.BaseAddressCommand);
        Assert.Equal((ushort)0x0350, node.BaseAddressParam);
        Assert.False(node.IsExclusiveChannel);
        Assert.Equal((uint)800, node.CyclicIntervalMs);
        Assert.Equal((uint)450, node.TimeoutMs);
    }

    [Fact]
    public void ChannelMappings_GeneratesCorrectAddressesForActuators()
    {
        var node = new ModbusWriteNodeViewModel
        {
            SlaveId = 1,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            BaseAddressCommand = 0x0100,
            BaseAddressParam = 0x0120,
            ChannelCount = 9
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel());

        Assert.Equal(9, vm.ChannelMappings.Count);
        Assert.Equal("CH 01", vm.ChannelMappings[0].ChannelDisplay);
        Assert.Contains("0x0100", vm.ChannelMappings[0].CommandRegHex);
        Assert.Contains("0x0120", vm.ChannelMappings[0].ParamRegHex);

        Assert.Equal("CH 09", vm.ChannelMappings[8].ChannelDisplay);
        Assert.Contains("0x0108", vm.ChannelMappings[8].CommandRegHex);
        Assert.Contains("0x0130", vm.ChannelMappings[8].ParamRegHex);
    }

    [Fact]
    public void FrameBreakdownItems_PopulatedWithCorrectModbusFields()
    {
        var node = new ModbusWriteNodeViewModel
        {
            SlaveId = 1,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            BaseAddressCommand = 0x0100,
            BaseAddressParam = 0x0120,
            ChannelCount = 9
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel())
        {
            SimChannel = 1,
            SimCommand = 1,
            SimParamHex = "00FF00"
        };

        vm.GenerateFramePreview();

        Assert.NotEmpty(vm.FrameBreakdownItems);
        Assert.Contains(vm.FrameBreakdownItems, f => (f.FieldName.Contains("thiết bị") || f.FieldName.Contains("Device") || f.FieldName.Contains("Slave")) && f.RawHex == "01");
        Assert.Contains(vm.FrameBreakdownItems, f => f.RawHex == "06");
        Assert.Contains(vm.FrameBreakdownItems, f => f.RawHex.Contains("01 00"));
        Assert.NotEmpty(vm.SimTxFrameHex);
        Assert.True(vm.HasParamFrame);
        Assert.NotEmpty(vm.SimParamTxFrameHex);
    }

    [Fact]
    public void AvailableOptions_ArePurelyLocalized_WithoutBilingualParentheses()
    {
        var node = new ModbusWriteNodeViewModel();
        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel());

        foreach (var mode in vm.AvailableWriteModes)
        {
            Assert.False(mode.DisplayName.Contains("(Sort-to-Light"), "Write mode options must not contain informal AI text");
            Assert.False(mode.DisplayName.Contains("(Ghi trực tiếp"), "Write mode options must not contain bilingual parentheses");
        }

        foreach (var trig in vm.AvailableTriggerModes)
        {
            Assert.False(trig.DisplayName.Contains("(On Rising Edge)"), "Trigger mode options must not contain bilingual parentheses");
            Assert.False(trig.DisplayName.Contains("(On Data Change)"), "Trigger mode options must not contain bilingual parentheses");
        }
    }
}
