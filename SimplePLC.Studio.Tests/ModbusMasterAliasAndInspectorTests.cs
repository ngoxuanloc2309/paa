using System;
using System.Collections.Generic;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit and integration tests for Modbus Master inline Alias editing and the Collapsible RS-485 Inspector.
/// </summary>
public class ModbusMasterAliasAndInspectorTests
{
    public ModbusMasterAliasAndInspectorTests()
    {
        LocalizationService.Instance.CurrentLanguage = "vi";
    }

    [Fact]
    public void TargetTagAlias_WhenTagIsAssigned_SynchronizesWithTagCatalogAndModel()
    {
        // Arrange
        var tag = new TagModel
        {
            Index = 52,
            Name = "VREG0",
            Kind = TagKind.VirtualRegister,
            DataType = TagDataType.Int32,
            Alias = "OriginalAlias"
        };

        var channelVm = new ModbusMasterChannelItemViewModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 0,
            DataType = ModbusDataType.Int16,
            Endianness = ModbusEndianness.BigEndian_ABCD,
            Scale = 1.0,
            TargetTag = tag
        };

        // Assert initial state
        Assert.True(channelVm.IsTagSelected);
        Assert.Equal("OriginalAlias", channelVm.TargetTagAlias);

        // Act - operator edits the alias inline
        channelVm.TargetTagAlias = "Pressure_Sensor_Inlet";

        // Assert - two-way sync updates both ViewModel and underlying Tag
        Assert.Equal("Pressure_Sensor_Inlet", channelVm.TargetTagAlias);
        Assert.Equal("Pressure_Sensor_Inlet", tag.Alias);
        Assert.Equal("Pressure_Sensor_Inlet", channelVm.Description);
        Assert.Contains("Pressure_Sensor_Inlet", channelVm.TargetTagDisplay);
    }

    [Fact]
    public void TargetTagAlias_WhenTagIsNone_IsTagSelectedIsFalseAndAliasIsEmpty()
    {
        // Arrange
        var noneTag = new TagModel
        {
            Index = 65535,
            Name = "NONE",
            Kind = TagKind.None
        };

        var channelVm = new ModbusMasterChannelItemViewModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 0,
            DataType = ModbusDataType.Int16,
            Scale = 1.0,
            TargetTag = noneTag
        };

        // Assert
        Assert.False(channelVm.IsTagSelected);
        Assert.Equal(string.Empty, channelVm.TargetTagAlias);

        // Setting alias when no tag is selected should not throw
        channelVm.TargetTagAlias = "Ignored";
        Assert.Equal("Ignored", channelVm.Description);
    }

    [Fact]
    public void WireFramePreview_ComputesAccurateModbusRtuCrc16AndByteSequence()
    {
        LocalizationService.Instance.CurrentLanguage = "en";
        // Arrange: FC03 Read Holding Register 0 (1 register, 16-bit) on Slave 1
        var channelVm = new ModbusMasterChannelItemViewModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 0,
            DataType = ModbusDataType.Int16,
            Endianness = ModbusEndianness.BigEndian_ABCD,
            Scale = 1.0
        };

        // Standard Modbus RTU request frame for Read Holding Register (Address 0, Quantity 1):
        // 01 03 00 00 00 01 -> CRC-16 is 84 0A
        Assert.Equal("[ 01 ] [ 03 ] [ 00 00 ] [ 00 01 ] [ 84 0A ]", channelVm.RequestFrameHex);
        Assert.Equal("[ 01 ] [ 03 ] [ 02 ] [ HI LO ] [ CRC ]", channelVm.ExpectedResponseHex);
        Assert.Equal("FC03", channelVm.FunctionCodeBadge);
        Assert.Equal("READ", channelVm.OperationTypeBadge);
        Assert.Equal("1 Register (16-bit Signed, 2 Bytes)", channelVm.RegisterSizeDisplay);
        Assert.Equal("Holding Reg (4x) · 0 (Offset 0x0000)", channelVm.RegisterRangeDisplay);
        Assert.Equal("Tag Value = Raw × 1 (1:1 Trực tiếp)", channelVm.ScalingFormulaDisplay);
    }

    [Fact]
    public void WireFramePreview_32BitReal_CalculatesTwoRegistersAndExpectedResponse()
    {
        // Arrange: FC03 Read Holding Register 100 (2 registers, 32-bit Real) on Slave 2
        var channelVm = new ModbusMasterChannelItemViewModel
        {
            SlaveId = 2,
            FunctionCode = 3,
            Address = 100,
            DataType = ModbusDataType.Real,
            Endianness = ModbusEndianness.BigEndian_ABCD,
            Scale = 0.1
        };

        // Address 100 = 0x0064, Quantity 2 = 0x0002
        Assert.Equal("[ 02 ] [ 03 ] [ 00 63 ] [ 00 02 ] [ 34 26 ]", channelVm.RequestFrameHex);
        Assert.Equal("[ 02 ] [ 03 ] [ 04 ] [ B0 B1 B2 B3 ] [ CRC ]", channelVm.ExpectedResponseHex);
        Assert.Equal("2 Registers (32-bit Float BigEndian_ABCD, 4 Bytes)", channelVm.RegisterSizeDisplay);
        Assert.Equal("Holding Reg (4x) · 100 (Offset 0x0063)", channelVm.RegisterRangeDisplay);
        Assert.Equal("Tag Value = Raw × 0.1 (vd: Raw 100 → 10)", channelVm.ScalingFormulaDisplay);
    }

    [Fact]
    public void Inspector_ToggleDrawerCommand_TogglesExpansionState()
    {
        // Arrange
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Default: Inspector is expanded
        Assert.True(vm.IsInspectorExpanded);

        // Act: Collapse
        vm.ToggleInspectorCommand.Execute(null);
        Assert.False(vm.IsInspectorExpanded);

        // Act: Re-expand
        vm.ToggleInspectorCommand.Execute(null);
        Assert.True(vm.IsInspectorExpanded);
    }

    [Fact]
    public void ActivePolledChannelsText_FormatsBilinguallyWithoutGrammaticalErrors()
    {
        // Arrange
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        try
        {
            // Vietnamese
            LocalizationService.Instance.CurrentLanguage = "vi";
            string viText = vm.ActivePolledChannelsText;
            Assert.Contains("Kênh đọc định kỳ", viText);
            Assert.DoesNotContain("Tags", viText);

            // English
            LocalizationService.Instance.CurrentLanguage = "en";
            string enText = vm.ActivePolledChannelsText;
            Assert.Contains("Polled Channel(s)", enText);
            Assert.DoesNotContain("1 Tags", enText);
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = "en";
        }
    }

    [Fact]
    public void ModbusPortItem_EstimatedScanCycle_CalculatesDeterministicallyBasedOnBaudAndChannels()
    {
        // Arrange
        var meta = new ProjectMetadata();
        var vm = new ModbusPortConfigViewModel(meta);
        var port = vm.SelectedPort!;
        port.BaudRate = 9600;
        port.MasterChannels.Clear();

        // 2 Read channels, 1 Write channel
        port.MasterChannels.Add(new ModbusMasterChannelItemViewModel { SlaveId = 1, FunctionCode = 3, Address = 0, DataType = ModbusDataType.Int16 });
        port.MasterChannels.Add(new ModbusMasterChannelItemViewModel { SlaveId = 1, FunctionCode = 3, Address = 10, DataType = ModbusDataType.Real });
        port.MasterChannels.Add(new ModbusMasterChannelItemViewModel { SlaveId = 2, FunctionCode = 6, Address = 20, DataType = ModbusDataType.Int16 });

        // Assert
        Assert.Equal(2, port.ActivePolledChannelsCount);
        Assert.Equal(1, port.ActiveWriteChannelsCount);
        // 2 polled channels at 9600 baud: ~40ms per channel * 2 = 80ms
        Assert.Equal(98, port.EstimatedScanCycleMs);
    }
    [Fact]
    public void IsEndiannessApplicable_TrueFor32Bit_FalseFor16BitAndBool()
    {
        var channelVm = new ModbusMasterChannelItemViewModel();

        // 16-bit Int16 -> False (only single register, 32-bit word swap not applicable)
        channelVm.DataType = ModbusDataType.Int16;
        Assert.False(channelVm.IsEndiannessApplicable);

        // 1-bit Bool -> False (coils and contacts have no byte ordering)
        channelVm.DataType = ModbusDataType.Bool;
        Assert.False(channelVm.IsEndiannessApplicable);

        // 16-bit UInt16 -> False
        channelVm.DataType = ModbusDataType.UInt16;
        Assert.False(channelVm.IsEndiannessApplicable);

        // 32-bit Real (Float) -> True (spans 2 registers, requires ABCD, CDAB, BADC, DCBA selection)
        channelVm.DataType = ModbusDataType.Real;
        Assert.True(channelVm.IsEndiannessApplicable);

        // 32-bit Int32 -> True
        channelVm.DataType = ModbusDataType.Int32;
        Assert.True(channelVm.IsEndiannessApplicable);

        // 32-bit UInt32 -> True
        channelVm.DataType = ModbusDataType.UInt32;
        Assert.True(channelVm.IsEndiannessApplicable);
    }

    [Fact]
    public void WireFrame_ToggleCommand_TogglesRawHexPreviewVisibility()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Default: Wire frame is collapsed (clean mode)
        Assert.False(vm.IsWireFrameExpanded);

        // Act: Expand
        vm.ToggleWireFrameCommand.Execute(null);
        Assert.True(vm.IsWireFrameExpanded);

        // Act: Re-collapse
        vm.ToggleWireFrameCommand.Execute(null);
        Assert.False(vm.IsWireFrameExpanded);
    }

    [Fact]
    public void FilteredAvailableTags_WhenDataTypeIsBool_ReturnsOnlyDigitalTagsAndNone()
    {
        // Arrange
        var noneTag = new TagModel { Index = 0, Name = "NONE", Kind = TagKind.None };
        var diTag = new TagModel { Index = 1, Name = "DI0", Kind = TagKind.DiscreteInput };
        var doTag = new TagModel { Index = 9, Name = "DO0", Kind = TagKind.DiscreteOutput };
        var vflagTag = new TagModel { Index = 20, Name = "VFLAG0", Kind = TagKind.VirtualFlag };
        var vregInt = new TagModel { Index = 52, Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32 };
        var vregReal = new TagModel { Index = 53, Name = "VREG1", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32 };

        var allTags = new List<TagModel> { noneTag, diTag, doTag, vflagTag, vregInt, vregReal };

        var channelVm = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => allTags,
            FunctionCode = 5, // FC05 Write Single Coil (cho phép cả DI, DO, VFLAG làm nguồn ghi)
            DataType = ModbusDataType.Bool
        };

        // Act
        var filtered = channelVm.FilteredAvailableTags;

        // Assert: Bool only contains None and Digital tags (DI, DO, VFLAG), NEVER VREG
        Assert.Contains(noneTag, filtered);
        Assert.Contains(diTag, filtered);
        Assert.Contains(doTag, filtered);
        Assert.Contains(vflagTag, filtered);
        Assert.DoesNotContain(vregInt, filtered);
        Assert.DoesNotContain(vregReal, filtered);
    }

    [Fact]
    public void FilteredAvailableTags_WhenDataTypeIsReal_ReturnsOnlyRealRegistersAndNone()
    {
        // Arrange
        var noneTag = new TagModel { Index = 0, Name = "NONE", Kind = TagKind.None };
        var diTag = new TagModel { Index = 1, Name = "DI0", Kind = TagKind.DiscreteInput };
        var vflagTag = new TagModel { Index = 20, Name = "VFLAG0", Kind = TagKind.VirtualFlag };
        var vregReal = new TagModel { Index = 52, Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Float32 };
        var vregEditable = new TagModel { Index = 53, Name = "VREG1", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32 }; // IsDataTypeEditable is true for VREG

        var allTags = new List<TagModel> { noneTag, diTag, vflagTag, vregReal, vregEditable };

        var channelVm = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => allTags,
            DataType = ModbusDataType.Real
        };

        // Act
        var filtered = channelVm.FilteredAvailableTags;

        // Assert: Real only contains None and Real/Editable registers, NEVER DI or VFLAG
        Assert.Contains(noneTag, filtered);
        Assert.Contains(vregReal, filtered);
        Assert.Contains(vregEditable, filtered);
        Assert.DoesNotContain(diTag, filtered);
        Assert.DoesNotContain(vflagTag, filtered);
    }

    [Fact]
    public void FilteredAvailableTags_WhenDataTypeIsInt16_ReturnsOnlyIntRegistersAndNone()
    {
        // Arrange
        var noneTag = new TagModel { Index = 0, Name = "NONE", Kind = TagKind.None };
        var diTag = new TagModel { Index = 1, Name = "DI0", Kind = TagKind.DiscreteInput };
        var vflagTag = new TagModel { Index = 20, Name = "VFLAG0", Kind = TagKind.VirtualFlag };
        var vregInt = new TagModel { Index = 52, Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32 };

        var allTags = new List<TagModel> { noneTag, diTag, vflagTag, vregInt };

        var channelVm = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => allTags,
            DataType = ModbusDataType.Int16
        };

        // Act
        var filtered = channelVm.FilteredAvailableTags;

        // Assert: Int16 only contains None and Int registers, NEVER DI or VFLAG
        Assert.Contains(noneTag, filtered);
        Assert.Contains(vregInt, filtered);
        Assert.DoesNotContain(diTag, filtered);
        Assert.DoesNotContain(vflagTag, filtered);
    }

    [Fact]
    public void OnDataTypeChanged_WhenSwitchingBetweenDigitalAndAnalog_AutoClearsMismatchedTag()
    {
        // Arrange
        var noneTag = new TagModel { Index = 0, Name = "NONE", Kind = TagKind.None };
        var vflagTag = new TagModel { Index = 20, Name = "VFLAG0", Kind = TagKind.VirtualFlag };
        var vregInt = new TagModel { Index = 52, Name = "VREG0", Kind = TagKind.VirtualRegister, DataType = TagDataType.Int32 };
        var allTags = new List<TagModel> { noneTag, vflagTag, vregInt };

        var channelVm = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => allTags,
            DataType = ModbusDataType.Bool,
            TargetTag = vflagTag
        };

        Assert.Equal(vflagTag, channelVm.TargetTag);

        // Act: Switch to REAL (analog register)
        channelVm.DataType = ModbusDataType.Real;

        // Assert: VFLAG0 is cleared because 1-bit boolean cannot store a 32-bit float!
        Assert.Equal(noneTag, channelVm.TargetTag);
    }

    [Fact]
    public void AppTabIndices_TabOrderMatchesMainWindowContract()
    {
        Assert.Equal(0, AppTabIndices.Device);
        Assert.Equal(1, AppTabIndices.LogicGraph);
        Assert.Equal(2, AppTabIndices.TagCatalog);
        Assert.Equal(3, AppTabIndices.RuleTable);
        Assert.Equal(4, AppTabIndices.Blueprints);
        Assert.Equal(5, AppTabIndices.Deploy);
        Assert.Equal(6, AppTabIndices.LiveWatch);
        Assert.Equal(7, AppTabIndices.Simulator);
    }
}
