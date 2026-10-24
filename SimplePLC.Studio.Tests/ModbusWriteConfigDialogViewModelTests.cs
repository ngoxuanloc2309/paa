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

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel())
        {
            SimDirectValue = 291 // 0x0123
        };
        vm.GenerateFramePreview();

        Assert.Contains("FC06", vm.SimGeneratedFrames);
        Assert.Contains("Addr 0x00C8", vm.SimGeneratedFrames);
        Assert.Contains("01 23", vm.SimTxFrameHex); // 291 = 0x0123
        Assert.Contains(vm.FrameBreakdownItems, item => item.RawHex == "01 23" && item.Interpretation.Contains("291"));

        // Switch to FC16
        vm.FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters;
        vm.PointCount = 2;
        vm.GenerateFramePreview();

        Assert.Contains("FC16", vm.SimGeneratedFrames);
        Assert.Contains("Count=2", vm.SimGeneratedFrames);
        Assert.Contains("01 23", vm.SimTxFrameHex); // Starting with 291
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

    [Fact]
    public void Constructor_LoadsTriggerModeAndDeadband_FromNode()
    {
        var node = new ModbusWriteNodeViewModel
        {
            TriggerMode = ModbusWriteTriggerMode.OnDataChange,
            Deadband = 1.75f,
            CyclicIntervalMs = 600
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel());

        Assert.Equal(ModbusWriteTriggerMode.OnDataChange, vm.TriggerMode);
        Assert.Equal(1.75f, vm.Deadband, 3);
        Assert.True(vm.IsOnDataChangeMode);
        Assert.False(vm.IsCyclicMode);
        Assert.False(vm.IsTriggerEdgeMode);
    }

    [Fact]
    public void TriggerMode_PropertyChange_TogglesHelperFlags()
    {
        var node = new ModbusWriteNodeViewModel();
        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel());

        vm.TriggerMode = ModbusWriteTriggerMode.Cyclic;
        Assert.True(vm.IsCyclicMode);
        Assert.False(vm.IsOnDataChangeMode);
        Assert.False(vm.IsTriggerEdgeMode);
        Assert.True(vm.HasTriggerParameter);

        vm.TriggerMode = ModbusWriteTriggerMode.OnDataChange;
        Assert.False(vm.IsCyclicMode);
        Assert.True(vm.IsOnDataChangeMode);
        Assert.False(vm.IsTriggerEdgeMode);
        Assert.True(vm.HasTriggerParameter);

        vm.TriggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge;
        Assert.False(vm.IsCyclicMode);
        Assert.False(vm.IsOnDataChangeMode);
        Assert.True(vm.IsTriggerEdgeMode);
        Assert.False(vm.HasTriggerParameter);
    }

    [Fact]
    public void Apply_TransfersTriggerModeAndDeadband_ToNode()
    {
        var node = new ModbusWriteNodeViewModel
        {
            TriggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge,
            Deadband = 0.5f
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel())
        {
            TriggerMode = ModbusWriteTriggerMode.OnDataChange,
            Deadband = 2.5f
        };

        vm.Apply();

        Assert.Equal(ModbusWriteTriggerMode.OnDataChange, node.TriggerMode);
        Assert.Equal(2.5f, node.Deadband, 3);
    }

    [Fact]
    public void Node_TriggerModeName_ReflectsDeadbandAndInterval()
    {
        var node = new ModbusWriteNodeViewModel
        {
            TriggerMode = ModbusWriteTriggerMode.Cyclic,
            CyclicIntervalMs = 750
        };
        Assert.Contains("750 ms", node.TriggerModeName);

        node.TriggerMode = ModbusWriteTriggerMode.OnDataChange;
        node.Deadband = 0.5f;
        Assert.DoesNotContain("Δ", node.TriggerModeName); // default deadband does not show Δ

        node.Deadband = 1.25f;
        Assert.Contains("Δ1.25", node.TriggerModeName); // custom deadband shows Δ1.25

        node.TriggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge;
        Assert.True(node.IsTriggerMode);
    }

    [Fact]
    public void GenerateFramePreview_PopulatesNonEmptyTooltips_InBothModes()
    {
        // 1. ChannelIndexed
        var node1 = new ModbusWriteNodeViewModel
        {
            WriteMode = ModbusWriteMode.ChannelIndexed,
            SlaveId = 1,
            ChannelCount = 8
        };
        var vm1 = new ModbusWriteConfigDialogViewModel(node1, new TagCatalogViewModel());
        vm1.GenerateFramePreview();

        Assert.NotEmpty(vm1.FrameBreakdownItems);
        Assert.All(vm1.FrameBreakdownItems, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.FieldName));
            Assert.False(string.IsNullOrWhiteSpace(item.RawHex));
            Assert.False(string.IsNullOrWhiteSpace(item.Interpretation));
            Assert.False(string.IsNullOrWhiteSpace(item.Tooltip));
        });

        // 2. DirectRegisters Single Register
        var node2 = new ModbusWriteNodeViewModel
        {
            WriteMode = ModbusWriteMode.DirectRegisters,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister,
            SlaveId = 1
        };
        var vm2 = new ModbusWriteConfigDialogViewModel(node2, new TagCatalogViewModel());
        vm2.GenerateFramePreview();

        Assert.NotEmpty(vm2.FrameBreakdownItems);
        Assert.All(vm2.FrameBreakdownItems, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Tooltip));
        });

        // 3. DirectRegisters Multiple Registers
        vm2.FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters;
        vm2.PointCount = 4;
        vm2.GenerateFramePreview();

        Assert.NotEmpty(vm2.FrameBreakdownItems);
        Assert.All(vm2.FrameBreakdownItems, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Tooltip));
        });
    }
    [Fact]
    public void LanguageSwitching_UpdatesAvailablePortsAndSummary()
    {
        var node = new ModbusWriteNodeViewModel
        {
            SlaveId = 1,
            PortIndex = 0,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            ChannelCount = 4
        };
        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel());

        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "en";
        Assert.StartsWith("Port 1", vm.AvailablePorts[0].DisplayName);
        Assert.StartsWith("Port 1", vm.SummaryBadgeText);

        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "vi";
        Assert.StartsWith("C\u1ED5ng 1", vm.AvailablePorts[0].DisplayName);
        Assert.StartsWith("C\u1ED5ng 1", vm.SummaryBadgeText);

        // Reset back to English
        SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "en";
    }
    [Fact]
    public void PointCount_And_ChannelCount_AreClampedToLimits()
    {
        var node = new ModbusWriteNodeViewModel();
        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel());

        // Test PointCount limit (1..16)
        vm.PointCount = 256;
        Assert.Equal((ushort)16, vm.PointCount);

        vm.PointCount = 0;
        Assert.Equal((ushort)1, vm.PointCount);

        // Test ChannelCount limit (1..64)
        vm.ChannelCount = 100;
        Assert.Equal((ushort)64, vm.ChannelCount);

        vm.ChannelCount = 0;
        Assert.Equal((ushort)1, vm.ChannelCount);
    }

    [Fact]
    public void RefreshAvailablePorts_PreservesSelectedValues_AndDoesNotClearStaticDropdowns()
    {
        var node = new ModbusWriteNodeViewModel
        {
            PortIndex = 1,
            WriteMode = ModbusWriteMode.DirectRegisters,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister,
            TriggerMode = ModbusWriteTriggerMode.OnDataChange
        };

        var initialPorts = new System.Collections.Generic.List<Models.ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "Port 1", Enabled = true },
            new() { PortIndex = 1, PortName = "Port 2", Enabled = true }
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel(), initialPorts);

        Assert.Equal(1, vm.PortIndex);
        Assert.Equal(ModbusWriteMode.DirectRegisters, vm.WriteMode);
        Assert.Equal(ModbusMasterWriteFunctionCode.WriteSingleRegister, vm.FunctionCode);
        Assert.Equal(ModbusWriteTriggerMode.OnDataChange, vm.TriggerMode);
        Assert.Equal(2, vm.AvailablePorts.Count);
        Assert.Equal(2, vm.AvailableWriteModes.Count);
        Assert.Equal(4, vm.AvailableFunctionCodes.Count);
        Assert.Equal(3, vm.AvailableTriggerModes.Count);

        // Simulate returning from RS-485 port settings dialog with updated baud/settings
        var updatedPorts = new System.Collections.Generic.List<Models.ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "Port 1 (RS485-A)", BaudRate = 9600, Enabled = true },
            new() { PortIndex = 1, PortName = "Port 2 (RS485-B)", BaudRate = 115200, Enabled = true },
            new() { PortIndex = 2, PortName = "Port 3", BaudRate = 19200, Enabled = true }
        };

        vm.RefreshAvailablePorts(updatedPorts);

        // Core properties must remain exactly intact
        Assert.Equal(1, vm.PortIndex);
        Assert.Equal(ModbusWriteMode.DirectRegisters, vm.WriteMode);
        Assert.Equal(ModbusMasterWriteFunctionCode.WriteSingleRegister, vm.FunctionCode);
        Assert.Equal(ModbusWriteTriggerMode.OnDataChange, vm.TriggerMode);

        // Dropdown counts must be updated properly without being wiped
        Assert.Equal(3, vm.AvailablePorts.Count);
        Assert.Equal(2, vm.AvailableWriteModes.Count);
        Assert.Equal(4, vm.AvailableFunctionCodes.Count);
        Assert.Equal(3, vm.AvailableTriggerModes.Count);
        Assert.Contains(vm.AvailablePorts, p => p.Value == 1 && p.DisplayName.Contains("RS485-B"));
    }
}
