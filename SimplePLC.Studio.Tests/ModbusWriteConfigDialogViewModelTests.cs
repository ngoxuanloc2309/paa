using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="ModbusWriteConfigDialogViewModel"/> verifying write configurations, port options, and safety gates.
/// </summary>
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

    [Fact]
    public void ModbusWriteConfigDialogViewModel_WhenZigbeeEnabled_IncludesZigbeePortInDropdown()
    {
        // Arrange
        var node = new ModbusWriteNodeViewModel { PortIndex = 0, SlaveId = 1 };
        var tagCatalog = new TagCatalogViewModel();
        var ports = new System.Collections.Generic.List<Models.ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "Port 1", Enabled = true }
        };
        var zigbee = new Models.ZigbeeConfigModel
        {
            Enabled = true,
            Channel = 20,
            PanIdHex = "ABCD"
        };

        // Act
        var vm = new ModbusWriteConfigDialogViewModel(node, tagCatalog, ports, zigbee);

        // Assert
        Assert.Contains(vm.AvailablePorts, p => p.Value == 4 && p.DisplayName.Contains("Zigbee") && p.DisplayName.Contains("ABCD"));

        // Select Zigbee port
        vm.PortIndex = 4;
        Assert.Contains("Zigbee", vm.PortSelectorTooltip);
        Assert.Contains("Zigbee", vm.SummaryBadgeText);
    }

    [Theory]
    [InlineData((byte)236)]
    [InlineData((byte)237)]
    public void ModbusWriteConfigDialogViewModel_SafetyGate_WhenZigbeePortAndRestrictedId_SetsErrorFlagAndRevertsOnApply(byte restrictedId)
    {
        // Arrange
        var node = new ModbusWriteNodeViewModel();
        var tagCatalog = new TagCatalogViewModel();
        var zigbee = new Models.ZigbeeConfigModel { Enabled = true };
        var vm = new ModbusWriteConfigDialogViewModel(node, tagCatalog, null, zigbee);

        // Act 1: Normal RS-485 port does not restrict 236/237
        vm.PortIndex = 0;
        vm.SlaveId = restrictedId;
        Assert.False(vm.HasRestrictedIdError);

        // Act 2: Zigbee port activates Safety Gate
        vm.PortIndex = 4;
        Assert.True(vm.HasRestrictedIdError);
        Assert.False(string.IsNullOrWhiteSpace(vm.RestrictedIdErrorMessage));

        // Act 3: Apply automatically prevents saving restricted ID to node
        vm.Apply();
        Assert.NotEqual(restrictedId, node.SlaveId);
        Assert.Equal((byte)1, node.SlaveId);
    }

    [Fact]
    public void ModbusWriteConfigDialogViewModel_FC06_EnforcesSinglePin_DisablesAddPin_AndCropsExtraPins()
    {
        // Arrange
        var node = new ModbusWriteNodeViewModel
        {
            WriteMode = ModbusWriteMode.DirectRegisters,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters
        };
        node.Pins.Add(new ModbusPinBindingViewModel { Label = "In_1", DataType = ModbusDataType.Int16 });
        node.Pins.Add(new ModbusPinBindingViewModel { Label = "In_2", DataType = ModbusDataType.Int16 });

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel());
        Assert.Equal(2, node.Pins.Count);
        Assert.True(vm.CanAddPin);

        // Act: Switch to FC06 (Single Register)
        vm.FunctionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister;

        // Assert
        Assert.False(vm.CanAddPin);
        Assert.Single(node.Pins); // Trims down to 1 pin
        Assert.Equal((ushort)1, vm.PointCount);
        Assert.Contains("FC06", vm.SummaryBadgeText);
        Assert.Contains("1 Regs", vm.SummaryBadgeText);
    }

    [Fact]
    public void ModbusWriteConfigDialogViewModel_FC16_PointCountCalculatedFromPinSpans()
    {
        // Arrange
        var node = new ModbusWriteNodeViewModel
        {
            WriteMode = ModbusWriteMode.DirectRegisters,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteMultipleRegisters
        };
        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel());
        node.Pins.Clear();

        // Act: Add Pin 1 (Int16 - 1 word) and Pin 2 (Real - 2 words) via ViewModel commands
        vm.AddPin(); // Pin 1: Default Int16 (1 word)
        vm.AddPin(); // Pin 2: Default Int16 -> switch to Real (2 words)
        vm.Node.Pins[1].DataType = ModbusDataType.Real;

        // Assert: Total words = 1 + 2 = 3 words
        Assert.Equal((ushort)3, vm.PointCount);
        Assert.Contains("3 Regs", vm.SummaryBadgeText);
    }

    [Fact]
    public void ModbusPinBindingViewModel_EndiannessOptions_FiltersOptionsByDataType()
    {
        var pin = new ModbusPinBindingViewModel();

        // 1. INT16 -> 2 options: Big-Endian (AB), Byte-Swap (BA)
        pin.DataType = ModbusDataType.Int16;
        Assert.True(pin.IsEndiannessEnabled);
        Assert.Equal(2, pin.EndiannessOptions.Count);
        Assert.Equal("Big-Endian (AB)", pin.EndiannessOptions[0].DisplayName);
        Assert.Equal("Byte-Swap (BA)", pin.EndiannessOptions[1].DisplayName);

        // 2. REAL -> 4 options: ABCD, CDAB, BADC, DCBA
        pin.DataType = ModbusDataType.Real;
        Assert.True(pin.IsEndiannessEnabled);
        Assert.Equal(4, pin.EndiannessOptions.Count);
        Assert.Contains("ABCD", pin.EndiannessOptions[0].DisplayName);

        // 3. BOOL -> 1 option: — (N/A), Disabled
        pin.DataType = ModbusDataType.Bool;
        Assert.False(pin.IsEndiannessEnabled);
        Assert.Single(pin.EndiannessOptions);
        Assert.Contains("N/A", pin.EndiannessOptions[0].DisplayName);
    }

    [Fact]
    public void ModbusWriteConfigDialogViewModel_PortRoleWarning_TriggersWhenPortIsSlaveOrDisabled()
    {
        var node = new ModbusWriteNodeViewModel { PortIndex = 0 };
        var ports = new System.Collections.Generic.List<Models.ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Slave" },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = false, Role = "Master" },
            new() { PortIndex = 2, PortName = "RS-485 #3", Enabled = true, Role = "Master" }
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel(), ports);

        // Port 0 is Slave -> Has Warning
        vm.PortIndex = 0;
        Assert.True(vm.HasPortRoleWarning);
        Assert.Contains("Slave", vm.PortRoleWarningMessage, System.StringComparison.OrdinalIgnoreCase);

        // Port 1 is Disabled -> Has Warning
        vm.PortIndex = 1;
        Assert.True(vm.HasPortRoleWarning);
        Assert.True(vm.PortRoleWarningMessage.Contains("vô hiệu hóa", System.StringComparison.OrdinalIgnoreCase)
                    || vm.PortRoleWarningMessage.Contains("disabled", System.StringComparison.OrdinalIgnoreCase));

        // Port 2 is Master & Enabled -> No Warning
        vm.PortIndex = 2;
        Assert.False(vm.HasPortRoleWarning);
        Assert.Empty(vm.PortRoleWarningMessage);
    }

    [Fact]
    public void ModbusWriteConfigDialogViewModel_CanImportSystemChannels_ExposesCountAndFormattedLabel()
    {
        var node = new ModbusWriteNodeViewModel { PortIndex = 0, WriteMode = ModbusWriteMode.DirectRegisters };
        var port0 = new Models.ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true,
            Role = "Master",
            MasterChannels = new System.Collections.Generic.List<Models.ModbusMasterChannelModel>
            {
                new() { FunctionCode = 3, Address = 40001, TargetTagName = "Read1" }, // Read FC03
                new() { FunctionCode = 6, Address = 40002, TargetTagName = "Write1" }, // Write FC06
                new() { FunctionCode = 16, Address = 40003, TargetTagName = "Write2" } // Write FC16
            }
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel(), new[] { port0 });

        Assert.True(vm.CanImportSystemChannels);
        Assert.Equal(2, vm.AvailableSystemWriteChannelsCount); // Only FC06 and FC16
        Assert.Contains("(2)", vm.ImportSystemChannelsText);
    }

    [Fact]
    public void ModbusWriteConfigDialogViewModel_OptionC_HidesDisabledPortsAndExposesEmptyStateProperties()
    {
        // 1. Arrange: Node points to Port 1 (index 0) which is disabled. Port 2 (index 1) is Slave. Port 3 (index 2) is enabled Master.
        var node = new ModbusWriteNodeViewModel { PortIndex = 0 };
        var ports = new System.Collections.Generic.List<Models.ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = false, Role = "Master" },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, Role = "Slave" },
            new() { PortIndex = 2, PortName = "RS-485 #3", Enabled = true, Role = "Master" },
            new() { PortIndex = 3, PortName = "RS-485 #4", Enabled = false, Role = "Master" }
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel(), ports);

        // Assert: Only Port 3 (index 2) is listed. Disabled ports 0, 3 and Slave port 1 are hidden completely.
        Assert.Single(vm.AvailablePorts);
        Assert.Equal((byte)2, vm.AvailablePorts[0].Value);
        Assert.True(vm.HasAvailablePorts);
        Assert.False(vm.HasNoAvailablePorts);
        Assert.Equal((byte)2, vm.PortIndex); // Auto-switched to Port 3

        // 2. Act: All ports disabled
        var allDisabledPorts = new System.Collections.Generic.List<Models.ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = false, Role = "Master" },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = false, Role = "Master" }
        };
        vm.RefreshAvailablePorts(allDisabledPorts);

        // Assert: Under Option C, AvailablePorts is empty and HasNoAvailablePorts triggers
        Assert.Empty(vm.AvailablePorts);
        Assert.False(vm.HasAvailablePorts);
        Assert.True(vm.HasNoAvailablePorts);
        Assert.False(string.IsNullOrWhiteSpace(vm.NoPortsAvailableMessage));
        Assert.True(vm.NoPortsAvailableMessage.Contains("Chưa kích hoạt") || vm.NoPortsAvailableMessage.Contains("No RS-485 Master Ports Enabled"));
    }
}
