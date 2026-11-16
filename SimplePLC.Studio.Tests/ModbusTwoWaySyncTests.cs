using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Modbus;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests for two-way synchronization between System Tab (RS-485 Master Channels)
/// and Logic Graph FBD blocks (MB_READ &amp; MB_WRITE) as well as compile-time coalescing &amp; de-duplication.
/// </summary>
public class ModbusTwoWaySyncTests : IDisposable
{
    public ModbusTwoWaySyncTests()
    {
        TagWriteOwnershipService.Instance.Reset();
    }

    public void Dispose()
    {
        TagWriteOwnershipService.Instance.Reset();
    }
    [Fact]
    public void AddressConversion_ToWireOffsetAndToModicon_BijectiveMapping()
    {
        // FC03 / FC04 Holding/Input registers
        Assert.Equal(0, ModbusChannelSyncService.ToWireOffset(40001, 3));
        Assert.Equal(99, ModbusChannelSyncService.ToWireOffset(40100, 3));
        Assert.Equal(40001, ModbusChannelSyncService.ToModiconAddress(0, 3));
        Assert.Equal(40100, ModbusChannelSyncService.ToModiconAddress(99, 3));

        // FC01 / FC02 Coils / Discrete Inputs
        Assert.Equal(0, ModbusChannelSyncService.ToWireOffset(1, 1));
        Assert.Equal(15, ModbusChannelSyncService.ToWireOffset(16, 1));
        Assert.Equal(1, ModbusChannelSyncService.ToModiconAddress(0, 1));
        Assert.Equal(16, ModbusChannelSyncService.ToModiconAddress(15, 1));

        // When raw wire offset is supplied (e.g., 0)
        Assert.Equal(0, ModbusChannelSyncService.ToWireOffset(0, 3));
        Assert.Equal(5, ModbusChannelSyncService.ToWireOffset(5, 3));
    }

    [Fact]
    public void ReadNodeApply_SynchronizesPinsToSystemPortChannels_CreatesOrUpdatesChannels()
    {
        var portModel = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "Port 1: RS-485 #1",
            Enabled = true,
            ResponseTimeoutMs = 250
        };
        var port = new ModbusPortItemViewModel(portModel);

        var tag0 = new TagModel { Name = "VREG0", Index = 0, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var tag1 = new TagModel { Name = "VREG1", Index = 1, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };

        var node = new ModbusReadNodeViewModel();
        node.PortIndex = 0;
        node.SlaveId = 1;
        node.FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters;
        node.StartAddress = 0; // Wire offset 0 -> 40001
        node.TimeoutMs = 250;
        node.Pins.Clear();
        node.Pins.Add(new ModbusPinBindingViewModel
        {
            Label = "Out_1",
            RegOffset = 0,
            DataType = ModbusDataType.Int16,
            Endianness = ModbusEndianness.BigEndian_ABCD,
            Multiplier = 1.0f,
            TargetTag = tag0
        });
        node.Pins.Add(new ModbusPinBindingViewModel
        {
            Label = "Out_2",
            RegOffset = 1,
            DataType = ModbusDataType.Int16,
            Endianness = ModbusEndianness.BigEndian_ABCD,
            Multiplier = 1.0f,
            TargetTag = tag1
        });

        // Act: Sync Read Node to System Port
        ModbusChannelSyncService.SyncReadNodeToSystemPort(node, port);

        // Assert: System port channels are populated
        Assert.Equal(2, port.MasterChannels.Count);
        var ch0 = port.MasterChannels[0];
        Assert.Equal(1, ch0.SlaveId);
        Assert.Equal(3, ch0.FunctionCode);
        Assert.Equal(40001, ch0.Address);
        Assert.Equal("VREG0", ch0.TargetTagName);

        var ch1 = port.MasterChannels[1];
        Assert.Equal(1, ch1.SlaveId);
        Assert.Equal(3, ch1.FunctionCode);
        Assert.Equal(40002, ch1.Address);
        Assert.Equal("VREG1", ch1.TargetTagName);
    }

    [Fact]
    public void WriteNodeApply_SynchronizesPinsToSystemPortChannels_CreatesOrUpdatesChannels()
    {
        var portModel = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "Port 1: RS-485 #1",
            Enabled = true
        };
        var port = new ModbusPortItemViewModel(portModel);

        var tag10 = new TagModel { Name = "VREG10", Index = 10, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };

        var node = new ModbusWriteNodeViewModel();
        node.PortIndex = 0;
        node.SlaveId = 2;
        node.FunctionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister;
        node.StartAddress = 10; // Wire offset 10 -> 40011
        node.Pins.Clear();
        node.Pins.Add(new ModbusPinBindingViewModel
        {
            Label = "In_1",
            RegOffset = 0,
            DataType = ModbusDataType.Int16,
            Endianness = ModbusEndianness.BigEndian_ABCD,
            Multiplier = 1.0f,
            TargetTag = tag10
        });

        // Act
        ModbusChannelSyncService.SyncWriteNodeToSystemPort(node, port);

        // Assert
        Assert.Single(port.MasterChannels);
        var ch = port.MasterChannels[0];
        Assert.Equal(2, ch.SlaveId);
        Assert.Equal(6, ch.FunctionCode);
        Assert.Equal(40011, ch.Address);
        Assert.Equal("VREG10", ch.TargetTagName);
    }

    [Fact]
    public void SyncSystemPortsToCanvasNodes_UpdatesTimeoutAndBaudProperties()
    {
        var portModel = new ModbusPortConfigModel { PortIndex = 0, ResponseTimeoutMs = 450, Enabled = true };
        var ports = new List<ModbusPortItemViewModel>
        {
            new(portModel)
        };

        var readNode = new ModbusReadNodeViewModel { PortIndex = 0, TimeoutMs = 200 };
        var writeNode = new ModbusWriteNodeViewModel { PortIndex = 0, TimeoutMs = 200 };

        ModbusChannelSyncService.SyncSystemPortsToCanvasNodes(ports, new[] { readNode }, new[] { writeNode });

        Assert.Equal(450u, readNode.TimeoutMs);
        Assert.Equal(450u, writeNode.TimeoutMs);
    }

    [Fact]
    public void CoalescingAndDeduplication_ExcludesLinkedCanvasChannels_AndPacksContiguousSystemChannels()
    {
        var portModel = new ModbusPortConfigModel
        {
            PortIndex = 0,
            Enabled = true,
            ResponseTimeoutMs = 300
        };
        var port = new ModbusPortItemViewModel(portModel);

        var tag0 = new TagModel { Name = "VREG0", Index = 0, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var tag1 = new TagModel { Name = "VREG1", Index = 1, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var tag2 = new TagModel { Name = "VREG2", Index = 2, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var tag3 = new TagModel { Name = "VREG3", Index = 3, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var tag4 = new TagModel { Name = "VREG4", Index = 4, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };

        // System has 5 channels: 40001 (linked to canvas), 40002..40005 (standalone)
        port.MasterChannels.Add(new ModbusMasterChannelItemViewModel { SlaveId = 1, FunctionCode = 3, Address = 40001, DataType = ModbusDataType.Int16, TargetTagName = "VREG0", TargetTag = tag0 });
        port.MasterChannels.Add(new ModbusMasterChannelItemViewModel { SlaveId = 1, FunctionCode = 3, Address = 40002, DataType = ModbusDataType.Int16, TargetTagName = "VREG1", TargetTag = tag1 });
        port.MasterChannels.Add(new ModbusMasterChannelItemViewModel { SlaveId = 1, FunctionCode = 3, Address = 40003, DataType = ModbusDataType.Int16, TargetTagName = "VREG2", TargetTag = tag2 });
        port.MasterChannels.Add(new ModbusMasterChannelItemViewModel { SlaveId = 1, FunctionCode = 3, Address = 40004, DataType = ModbusDataType.Int16, TargetTagName = "VREG3", TargetTag = tag3 });
        port.MasterChannels.Add(new ModbusMasterChannelItemViewModel { SlaveId = 1, FunctionCode = 3, Address = 40005, DataType = ModbusDataType.Int16, TargetTagName = "VREG4", TargetTag = tag4 });

        var canvasReadNode = new ModbusReadNodeViewModel
        {
            PortIndex = 0,
            SlaveId = 1,
            FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
            StartAddress = 0, // 40001
            TimeoutMs = 300
        };
        canvasReadNode.Pins.Add(new ModbusPinBindingViewModel { Label = "Out_1", RegOffset = 0, TargetTag = tag0 });

        var schedule = ModbusChannelSyncService.BuildUnifiedModbusSchedule(
            new[] { port },
            new[] { canvasReadNode },
            Array.Empty<ModbusWriteNodeViewModel>());

        // Canvas node is 1 read command (for VREG0)
        Assert.Single(schedule.CanvasReads);

        // System unlinked channels (VREG1..VREG4) contiguous 40002..40005 should be coalesced into 1 block of 4 pins!
        Assert.Single(schedule.SystemCoalescedReads);
        var coalesced = schedule.SystemCoalescedReads[0];
        Assert.Equal(1, coalesced.SlaveId);
        Assert.Equal(ModbusMasterFunctionCode.ReadHoldingRegisters, coalesced.FunctionCode);
        Assert.Equal(1, coalesced.StartAddress); // wire offset for 40002 is 1
        Assert.Equal(4, coalesced.Pins.Count);
        Assert.Equal((ushort)1, coalesced.Pins[0].TargetTagIndex);
        Assert.Equal((ushort)4, coalesced.Pins[3].TargetTagIndex);
    }

    [Fact]
    public void ReadDialog_InitializesTimeout_FromSelectedPortResponseTimeout()
    {
        var portModel = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "Port 1: RS-485 #1",
            Enabled = true,
            ResponseTimeoutMs = 200
        };

        var node = new ModbusReadNodeViewModel { PortIndex = 0, TimeoutMs = 300 };
        var tagCatalog = new TagCatalogViewModel();

        var dialogVm = new ModbusReadConfigDialogViewModel(node, tagCatalog, new[] { portModel });

        // Assert: TimeoutMs in dialog must inherit 200ms from Port 1, not retain 300ms!
        Assert.Equal(200u, dialogVm.TimeoutMs);
    }

    [Fact]
    public void ReadDialog_ImportSystemChannels_PopulatesAllDeclaredPinsAndConfig()
    {
        var portModel = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "Port 1: RS-485 #1",
            Enabled = true,
            ResponseTimeoutMs = 200
        };
        portModel.MasterChannels.Add(new ModbusMasterChannelModel { SlaveId = 1, FunctionCode = 3, Address = 40001, DataType = ModbusDataType.Int16, TargetTagName = "VREG0" });
        portModel.MasterChannels.Add(new ModbusMasterChannelModel { SlaveId = 1, FunctionCode = 3, Address = 40002, DataType = ModbusDataType.Int16, TargetTagName = "VREG1" });
        portModel.MasterChannels.Add(new ModbusMasterChannelModel { SlaveId = 1, FunctionCode = 3, Address = 40003, DataType = ModbusDataType.Int16, TargetTagName = "VREG2" });

        var tagCatalog = new TagCatalogViewModel();
        tagCatalog.AllTags.Add(new TagModel { Name = "VREG0", Index = 0, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister });
        tagCatalog.AllTags.Add(new TagModel { Name = "VREG1", Index = 1, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister });
        tagCatalog.AllTags.Add(new TagModel { Name = "VREG2", Index = 2, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister });

        var node = new ModbusReadNodeViewModel { PortIndex = 0, SlaveId = 1 };
        node.Pins.Clear();

        var dialogVm = new ModbusReadConfigDialogViewModel(node, tagCatalog, new[] { portModel });
        Assert.True(dialogVm.CanImportSystemChannels);

        // Act: Click Import
        dialogVm.ImportSystemChannels();

        // Assert
        Assert.Equal(3, node.Pins.Count);
        Assert.Equal("VREG0", node.Pins[0].Label);
        Assert.Equal(0, node.Pins[0].RegOffset);
        Assert.Equal("VREG0", node.Pins[0].TargetTag?.Name);

        Assert.Equal("VREG1", node.Pins[1].Label);
        Assert.Equal(1, node.Pins[1].RegOffset);

        Assert.Equal("VREG2", node.Pins[2].Label);
        Assert.Equal(2, node.Pins[2].RegOffset);

        Assert.Equal(3, dialogVm.RegCount);
        Assert.Equal(0, dialogVm.StartAddress);
    }

    [Fact]
    public void WriteDialog_ImportSystemChannels_PopulatesDirectRegisterPins()
    {
        var portModel = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "Port 1: RS-485 #1",
            Enabled = true,
            ResponseTimeoutMs = 200
        };
        portModel.MasterChannels.Add(new ModbusMasterChannelModel { SlaveId = 2, FunctionCode = 6, Address = 40010, DataType = ModbusDataType.Int16, TargetTagName = "VREG10" });
        portModel.MasterChannels.Add(new ModbusMasterChannelModel { SlaveId = 2, FunctionCode = 6, Address = 40011, DataType = ModbusDataType.Int16, TargetTagName = "VREG11" });

        var tagCatalog = new TagCatalogViewModel();
        tagCatalog.AllTags.Add(new TagModel { Name = "VREG10", Index = 10, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister });
        tagCatalog.AllTags.Add(new TagModel { Name = "VREG11", Index = 11, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister });

        var node = new ModbusWriteNodeViewModel { PortIndex = 0, SlaveId = 2, WriteMode = ModbusWriteMode.DirectRegisters };
        node.Pins.Clear();

        var dialogVm = new ModbusWriteConfigDialogViewModel(node, tagCatalog, new[] { portModel });
        Assert.True(dialogVm.CanImportSystemChannels);

        // Act
        dialogVm.ImportSystemChannels();

        // Assert
        Assert.Equal(2, node.Pins.Count);
        Assert.Equal("VREG10", node.Pins[0].Label);
        Assert.Equal(0, node.Pins[0].RegOffset);
        Assert.Equal("VREG10", node.Pins[0].TargetTag?.Name);

        Assert.Equal("VREG11", node.Pins[1].Label);
        Assert.Equal(1, node.Pins[1].RegOffset);
        Assert.Equal(2, dialogVm.PointCount);
        Assert.Equal(9, dialogVm.StartAddress); // wire offset for 40010 is 9
    }

    [Fact]
    public void ModbusPin_TagOptions_CategorizesLinkedTagsOnSamePort_ShowsLinkBadgeAndSelectable()
    {
        var tag0 = new TagModel { Name = "VREG0", Index = 0, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var tag1 = new TagModel { Name = "VREG1", Index = 1, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var allTags = new List<TagModel> { tag0, tag1 };

        var channel = new ModbusMasterChannelModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            TargetTagName = "VREG0",
            DataType = ModbusDataType.Int16
        };

        var pin = new ModbusPinBindingViewModel
        {
            TagProvider = _ => allTags,
            SystemChannelsProvider = () => new List<ModbusMasterChannelModel> { channel }
        };

        pin.RefreshTagOptions();

        Assert.Equal(2, pin.TagOptions.Count);
        var opt0 = pin.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.True(opt0.IsSelectable);
        Assert.True(opt0.HasLinkBadge);
        Assert.Equal("[🔗 Addr 40001]", opt0.LinkedBadge);
        Assert.False(opt0.HasConflict);

        var opt1 = pin.TagOptions.First(o => o.Tag.Name == "VREG1");
        Assert.True(opt1.IsSelectable);
        Assert.False(opt1.HasLinkBadge);
        Assert.Empty(opt1.LinkedBadge);
    }

    [Fact]
    public void ModbusPin_SelectLinkedTag_AutoFillsDataTypeEndiannessAndMultiplier()
    {
        var tag0 = new TagModel { Name = "VREG0", Index = 0, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var channel = new ModbusMasterChannelModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            TargetTagName = "VREG0",
            DataType = ModbusDataType.Real,
            Endianness = ModbusEndianness.MidBig_CDAB,
            Scale = 0.25
        };

        var pin = new ModbusPinBindingViewModel
        {
            TagProvider = _ => new[] { tag0 },
            SystemChannelsProvider = () => new List<ModbusMasterChannelModel> { channel }
        };

        // Act: Assign TargetTag to linked tag VREG0
        pin.TargetTag = tag0;

        // Assert: Pin properties are autofilled from system channel
        Assert.Equal(ModbusDataType.Real, pin.DataType);
        Assert.Equal(ModbusEndianness.MidBig_CDAB, pin.Endianness);
        Assert.Equal(0.25f, pin.Multiplier);
    }

    [Fact]
    public void ReadDialog_ConfigurePinTagProvider_WiresSystemChannelsAndRefreshesOptions()
    {
        var portModel = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "Port 1: RS-485 #1",
            Enabled = true,
            ResponseTimeoutMs = 200
        };
        portModel.MasterChannels.Add(new ModbusMasterChannelModel { SlaveId = 1, FunctionCode = 3, Address = 40001, TargetTagName = "VREG0", DataType = ModbusDataType.Int16 });
        portModel.MasterChannels.Add(new ModbusMasterChannelModel { SlaveId = 1, FunctionCode = 3, Address = 40002, TargetTagName = "VREG1", DataType = ModbusDataType.Int16 });

        var tagCatalog = new TagCatalogViewModel();
        tagCatalog.AllTags.Add(new TagModel { Name = "VREG0", Index = 0, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister });
        tagCatalog.AllTags.Add(new TagModel { Name = "VREG1", Index = 1, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister });
        tagCatalog.AllTags.Add(new TagModel { Name = "VREG2", Index = 2, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister });

        var node = new ModbusReadNodeViewModel { PortIndex = 0, SlaveId = 1 };
        node.Pins.Clear();
        node.Pins.Add(new ModbusPinBindingViewModel { Label = "Pin_1", RegOffset = 0 });

        var dialogVm = new ModbusReadConfigDialogViewModel(node, tagCatalog, new[] { portModel });

        // Assert: Pin_1 TagOptions has VREG0 with linked badge
        var pin = node.Pins[0];
        Assert.NotEmpty(pin.TagOptions);
        var optVreg0 = pin.TagOptions.FirstOrDefault(o => o.Tag.Name == "VREG0");
        Assert.NotNull(optVreg0);
        Assert.True(optVreg0.HasLinkBadge);
        Assert.Equal("[🔗 Addr 40001]", optVreg0.LinkedBadge);

        var optVreg2 = pin.TagOptions.FirstOrDefault(o => o.Tag.Name == "VREG2");
        Assert.NotNull(optVreg2);
        Assert.False(optVreg2.HasLinkBadge);
    }

    [Fact]
    public void ModbusReadNode_PinsCollectionReset_RemovesDynamicConnectors_DoesNotAccumulateOnMultipleImports()
    {
        var node = new ModbusReadNodeViewModel();
        Assert.Equal(3, node.OutputConnectors.Count); // Valid, Error, ErrCode

        var pin1 = new ModbusPinBindingViewModel { Label = "Pin_1", RegOffset = 0 };
        var pin2 = new ModbusPinBindingViewModel { Label = "Pin_2", RegOffset = 1 };
        node.Pins.Add(pin1);
        node.Pins.Add(pin2);
        Assert.Equal(5, node.OutputConnectors.Count); // 3 system + 2 dynamic pins

        // Act 1: Call Pins.Clear() (triggers Action == Reset)
        node.Pins.Clear();

        // Assert 1: Only 3 system connectors remain
        Assert.Equal(3, node.OutputConnectors.Count);
        Assert.Contains(node.ValidConnector, node.OutputConnectors);
        Assert.Contains(node.ErrorConnector, node.OutputConnectors);
        Assert.Contains(node.ErrCodeConnector, node.OutputConnectors);

        // Act 2: Simulate re-importing 2 pins
        node.Pins.Add(new ModbusPinBindingViewModel { Label = "Pin_A", RegOffset = 0 });
        node.Pins.Add(new ModbusPinBindingViewModel { Label = "Pin_B", RegOffset = 1 });
        Assert.Equal(5, node.OutputConnectors.Count);

        // Act 3: Clear and re-import again
        node.Pins.Clear();
        node.Pins.Add(new ModbusPinBindingViewModel { Label = "Pin_X", RegOffset = 0 });
        Assert.Equal(4, node.OutputConnectors.Count); // 3 system + 1 dynamic pin (no accumulation!)
    }

    [Fact]
    public void ModbusPin_IntraBlockExclusion_DisablesTagIfSelectedByPeerPinInSameBlock()
    {
        var tag0 = new TagModel { Name = "VREG0", Index = 0, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var tag1 = new TagModel { Name = "VREG1", Index = 1, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var allTags = new List<TagModel> { tag0, tag1 };

        var pin1 = new ModbusPinBindingViewModel { Label = "Pin_1", RegOffset = 0, TagProvider = _ => allTags };
        var pin2 = new ModbusPinBindingViewModel { Label = "Pin_2", RegOffset = 1, TagProvider = _ => allTags };

        var pinsList = new List<ModbusPinBindingViewModel> { pin1, pin2 };
        pin1.PeerPinsProvider = () => pinsList;
        pin2.PeerPinsProvider = () => pinsList;

        pin1.RefreshTagOptions();
        pin2.RefreshTagOptions();

        // Ban đầu cả 2 pin đều có thể chọn VREG0
        Assert.True(pin1.TagOptions.First(o => o.Tag.Name == "VREG0").IsSelectable);
        Assert.True(pin2.TagOptions.First(o => o.Tag.Name == "VREG0").IsSelectable);

        // Act 1: Pin 1 chọn VREG0
        pin1.TargetTag = tag0;

        // Assert 1: Pin 2 thấy VREG0 bị khóa với badge báo chân Pin_1 đang chiếm giữ
        var optPin2Vreg0 = pin2.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.False(optPin2Vreg0.IsSelectable);
        Assert.Equal("[⛔ Pin_1]", optPin2Vreg0.ConflictBadge);

        // Act 2: Pin 1 giải phóng VREG0 (đổi sang Tag khác hoặc null)
        pin1.TargetTag = null;

        // Assert 2: Pin 2 thấy VREG0 tự do trở lại
        optPin2Vreg0 = pin2.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.True(optPin2Vreg0.IsSelectable);
        Assert.Empty(optPin2Vreg0.ConflictBadge);
    }

    [Fact]
    public void ModbusPin_AccurateAddressMatching_OnlyShowsLinkedBadgeWhenModbusAddressMatches()
    {
        var tag0 = new TagModel { Name = "VREG0", Index = 0, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister };
        var allTags = new List<TagModel> { tag0 };

        var channel = new ModbusMasterChannelModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            TargetTagName = "VREG0",
            DataType = ModbusDataType.Int16
        };

        // Pin 1 ở StartAddress = 0 (Wire 0 -> Modicon 40001)
        var pin1 = new ModbusPinBindingViewModel
        {
            Label = "Pin_1",
            RegOffset = 0,
            TagProvider = _ => allTags,
            SystemChannelsProvider = () => new[] { channel },
            BaseAddressProvider = () => 0,
            FunctionCodeProvider = () => 3
        };

        // Pin 4 ở RegOffset = 3 (Wire 3 -> Modicon 40004)
        var pin4 = new ModbusPinBindingViewModel
        {
            Label = "Pin_4",
            RegOffset = 3,
            TagProvider = _ => allTags,
            SystemChannelsProvider = () => new[] { channel },
            BaseAddressProvider = () => 0,
            FunctionCodeProvider = () => 3
        };

        pin1.RefreshTagOptions();
        pin4.RefreshTagOptions();

        // Pin 1 khớp đúng địa chỉ 40001 nên hiện huy hiệu
        var optPin1 = pin1.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.True(optPin1.HasLinkBadge);
        Assert.Equal("[🔗 Addr 40001]", optPin1.LinkedBadge);

        // Pin 4 ở địa chỉ 40004 KHÔNG được hiển thị [🔗 Addr 40001]
        var optPin4 = pin4.TagOptions.First(o => o.Tag.Name == "VREG0");
        Assert.False(optPin4.HasLinkBadge);
        Assert.Empty(optPin4.LinkedBadge);
    }

    [Fact]
    public void ModbusMasterChannel_DataTypeChange_SyncsWithVirtualRegisterTag()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vreg0 = tagCatalog.AllTags.First(t => t.Name == "VREG0");
        Assert.Equal(TagDataType.Int32, vreg0.DataType); // Mặc định là Int32

        var channelVm = new ModbusMasterChannelItemViewModel
        {
            AvailableTagsProvider = () => tagCatalog.AllTags,
            DataType = ModbusDataType.Int16
        };
        channelVm.TargetTag = vreg0;

        // Act 1: Chuyển kiểu dữ liệu kênh sang REAL (Float32)
        channelVm.DataType = ModbusDataType.Real;

        // Assert 1: vreg0.DataType trong Tag Catalog tự động đồng bộ sang Float32
        Assert.Equal(TagDataType.Float32, vreg0.DataType);

        // Act 2: Chuyển lại kiểu dữ liệu kênh sang INT16
        channelVm.DataType = ModbusDataType.Int16;

        // Assert 2: vreg0.DataType tự động đồng bộ về Int32
        Assert.Equal(TagDataType.Int32, vreg0.DataType);
    }

    [Fact]
    public void ImportSystemChannels_PrioritizesTagAliasOverTagName()
    {
        var portModel = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "Port 1: RS-485 #1",
            Enabled = true,
            ResponseTimeoutMs = 200
        };
        portModel.MasterChannels.Add(new ModbusMasterChannelModel
        {
            SlaveId = 1,
            FunctionCode = 3,
            Address = 40001,
            TargetTagName = "VREG0",
            TargetTagAlias = "Nhiệt độ lò #1",
            DataType = ModbusDataType.Int16
        });

        var tagCatalog = new TagCatalogViewModel();
        tagCatalog.AllTags.Add(new TagModel { Name = "VREG0", Index = 0, DataType = TagDataType.Int32, Kind = TagKind.VirtualRegister, Alias = "Nhiệt độ lò #1" });

        var node = new ModbusReadNodeViewModel { PortIndex = 0, SlaveId = 1 };
        node.Pins.Clear();

        var dialogVm = new ModbusReadConfigDialogViewModel(node, tagCatalog, new[] { portModel });

        // Act: Import from System Tab
        dialogVm.ImportSystemChannels();

        // Assert: Pin Name lấy theo Alias "Nhiệt độ lò #1" thay vì mã "VREG0"
        Assert.Single(node.Pins);
        Assert.Equal("Nhiệt độ lò #1", node.Pins[0].Label);
        Assert.Equal("VREG0", node.Pins[0].TargetTag?.Name);
    }
}
