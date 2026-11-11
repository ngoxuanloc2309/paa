using System;
using System.Linq;
using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử xác thực tính đồng bộ và chức năng của Modbus Slave Register Inspector (Hướng A).
/// </summary>
public class ModbusSlaveInspectorTests
{
    [Fact]
    public void ModbusSlaveFixedRegisterViewModel_Properties_CalculatesCorrectOffsetsAndCounts()
    {
        // 1. Holding Register: VFLAG_DWORD (0x0002 .. 0x0003 -> Offset 2, Count 2)
        var dtoVflag = new ModbusSlaveFixedRegisterDto(
            Category: "Holding Registers",
            AddressHex: "0x0002 .. 0x0003",
            ModbusAddress: "40003 .. 40004",
            DeviceTag: "VFLAG_DWORD",
            DataType: "UINT32",
            AccessRight: "RW",
            FunctionCode: "FC 03, 06, 16",
            DescriptionVi: "Gom 32 cờ logic VFLAG0..VFLAG31",
            DescriptionEn: "Packed Virtual Flags VFLAG0..VFLAG31"
        );
        var vmVflag = new ModbusSlaveFixedRegisterViewModel(dtoVflag);
        Assert.Equal(2, vmVflag.StartOffset);
        Assert.Equal(2, vmVflag.RegisterCount);
        Assert.Equal(0x03, vmVflag.PrimaryFunctionCode);
        Assert.Equal("VFLAG_DWORD", vmVflag.PrimaryTagName);

        // 2. Holding Register: AI0 .. AI3 (0x0010 .. 0x0017 -> Offset 16, Count 8)
        var dtoAi = new ModbusSlaveFixedRegisterDto(
            Category: "Holding Registers",
            AddressHex: "0x0010 .. 0x0017",
            ModbusAddress: "40017 .. 40024",
            DeviceTag: "AI0 .. AI3",
            DataType: "FLOAT32",
            AccessRight: "RO",
            FunctionCode: "FC 03",
            DescriptionVi: "4 ngõ vào tương tự",
            DescriptionEn: "4 Analog Inputs"
        );
        var vmAi = new ModbusSlaveFixedRegisterViewModel(dtoAi);
        Assert.Equal(16, vmAi.StartOffset);
        Assert.Equal(8, vmAi.RegisterCount);
        Assert.Equal(0x03, vmAi.PrimaryFunctionCode);
        Assert.Equal("AI0", vmAi.PrimaryTagName);

        // 3. Coils: DO0 .. DO15 (0x0000 .. 0x000F -> Offset 0, Count 16, FC 01)
        var dtoDo = new ModbusSlaveFixedRegisterDto(
            Category: "Coils",
            AddressHex: "0x0000 .. 0x000F",
            ModbusAddress: "00001 .. 00016",
            DeviceTag: "DO0 .. DO15",
            DataType: "BOOL",
            AccessRight: "RW",
            FunctionCode: "FC 01, 05, 15",
            DescriptionVi: "Ngõ ra Relay",
            DescriptionEn: "Relay Outputs"
        );
        var vmDo = new ModbusSlaveFixedRegisterViewModel(dtoDo);
        Assert.Equal(0, vmDo.StartOffset);
        Assert.Equal(16, vmDo.RegisterCount);
        Assert.Equal(0x01, vmDo.PrimaryFunctionCode);
        Assert.Equal("DO0", vmDo.PrimaryTagName);

        // 4. Discrete Inputs: DI0 .. DI15 (FC 02)
        var dtoDi = new ModbusSlaveFixedRegisterDto(
            Category: "Discrete Inputs",
            AddressHex: "0x0000 .. 0x000F",
            ModbusAddress: "10001 .. 10016",
            DeviceTag: "DI0 .. DI15",
            DataType: "BOOL",
            AccessRight: "RO",
            FunctionCode: "FC 02",
            DescriptionVi: "Ngõ vào số",
            DescriptionEn: "Digital Inputs"
        );
        var vmDi = new ModbusSlaveFixedRegisterViewModel(dtoDi);
        Assert.Equal(0x02, vmDi.PrimaryFunctionCode);
    }

    [Fact]
    public void ModbusPortConfigViewModel_SelectedFixedRegisterItem_DefaultsToFirstAndUpdates()
    {
        var metadata = new ProjectMetadata();
        var vm = new ModbusPortConfigViewModel(metadata);

        Assert.NotNull(vm.SelectedFixedRegisterItem);
        Assert.Equal("DI_WORD", vm.SelectedFixedRegisterItem.DeviceTag);

        // Change selected item
        var second = vm.FixedRegisterMap[1];
        vm.SelectedFixedRegisterItem = second;
        Assert.Equal("DO_WORD", vm.SelectedFixedRegisterItem.DeviceTag);

        // Filter tab to Coils
        vm.SelectMemoryTab("Coils");
        Assert.NotNull(vm.SelectedFixedRegisterItem);
        Assert.Equal("Coils", vm.SelectedFixedRegisterItem.Category);
    }

    [Fact]
    public void ModbusLiveTestInspectorViewModel_WhenInSlaveMode_ComputesScadaWireFramesCorrectly()
    {
        var inspector = new ModbusLiveTestInspectorViewModel();
        inspector.IsSlaveMode = true;
        inspector.SlaveId = 1;

        var dtoVflag = new ModbusSlaveFixedRegisterDto(
            Category: "Holding Registers",
            AddressHex: "0x0002 .. 0x0003",
            ModbusAddress: "40003 .. 40004",
            DeviceTag: "VFLAG_DWORD",
            DataType: "UINT32",
            AccessRight: "RW",
            FunctionCode: "FC 03, 06, 16",
            DescriptionVi: "Gom 32 cờ logic VFLAG0..VFLAG31",
            DescriptionEn: "Packed Virtual Flags VFLAG0..VFLAG31"
        );
        var regVm = new ModbusSlaveFixedRegisterViewModel(dtoVflag);
        inspector.SelectedSlaveRegister = regVm;

        Assert.True(inspector.HasSelectedSlaveRegister);
        Assert.Equal("40003 .. 40004 · VFLAG_DWORD", inspector.SelectedRegisterOrChannelSummary);
        Assert.Equal("40003 .. 40004 (0x0002 .. 0x0003)", inspector.SlaveRegisterAddressDisplay);
        Assert.Equal("40003 .. 40004", inspector.SlaveScadaAddress1Based);
        Assert.Equal("2 (0x0002)", inspector.SlaveScadaOffset0Based);
        Assert.Equal("VFLAG_DWORD", inspector.SlaveDeviceTagDisplay);
        Assert.Equal("UINT32", inspector.SlaveDataTypeDisplay);
        Assert.Equal("Read/Write (RW)", inspector.SlaveAccessRightDisplay);
        Assert.Equal("2 Regs (32-bit ABCD)", inspector.SlaveScadaLengthDisplay);
        Assert.Contains("VFLAG0", inspector.SlaveScadaMappingGuide);
        Assert.Contains("9600 bps", inspector.InspectorHeaderSubtitle);

        // SCADA Query Frame: SlaveId 01, FC 03, Offset 0002, Count 0002
        Assert.Equal("[ 01 ] [ 03 ] [ 00 02 ] [ 00 02 ] [ CRC ]", inspector.SlaveScadaQueryWireHex);

        // PLC Response Frame: SlaveId 01, FC 03, ByteCount 04 (2 regs * 2 bytes)
        Assert.Equal("[ 01 ] [ 03 ] [ 04 ] [ DATA x4B ] [ CRC ]", inspector.SlavePlcResponseWireHex);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_WhenPortRoleToggled_PropagatesToInspector()
    {
        var metadata = new ProjectMetadata();
        var dtuVm = new DtuGatewayStudioViewModel(metadata);

        var port1 = dtuVm.ModbusPortConfigVM.Ports[0];

        // 1. Initial default is Master
        port1.Role = "Master";
        dtuVm.ModbusPortConfigVM.SelectedPort = port1;
        Assert.False(dtuVm.LiveTestInspectorVM.IsSlaveMode);
        Assert.True(dtuVm.LiveTestInspectorVM.IsMasterMode);

        // 2. Switch port 1 to Slave
        port1.Role = "Slave";
        Assert.True(dtuVm.LiveTestInspectorVM.IsSlaveMode);
        Assert.False(dtuVm.LiveTestInspectorVM.IsMasterMode);
        Assert.NotNull(dtuVm.LiveTestInspectorVM.SelectedSlaveRegister);
        Assert.Equal(dtuVm.ModbusPortConfigVM.SelectedFixedRegisterItem, dtuVm.LiveTestInspectorVM.SelectedSlaveRegister);

        // 3. User selects different register in slave table
        var targetReg = dtuVm.ModbusPortConfigVM.FixedRegisterMap.First(r => r.DeviceTag.Contains("AI0"));
        dtuVm.ModbusPortConfigVM.SelectedFixedRegisterItem = targetReg;
        Assert.Equal(targetReg, dtuVm.LiveTestInspectorVM.SelectedSlaveRegister);
        Assert.Equal("AI0", targetReg.PrimaryTagName);
    }

    [Fact]
    public void ModbusLiveTestInspectorViewModel_UpdateSlaveLiveValue_ReflectsLiveTagsFromStore()
    {
        var store = new RuntimeStateStore();
        var inspector = new ModbusLiveTestInspectorViewModel(stateStore: store);
        inspector.IsSlaveMode = true;

        var dtoAi = new ModbusSlaveFixedRegisterDto(
            Category: "Holding Registers",
            AddressHex: "0x0010 .. 0x0017",
            ModbusAddress: "40017 .. 40024",
            DeviceTag: "AI0 .. AI3",
            DataType: "FLOAT32",
            AccessRight: "RO",
            FunctionCode: "FC 03",
            DescriptionVi: "4 ngõ vào tương tự",
            DescriptionEn: "4 Analog Inputs"
        );
        inspector.SelectedSlaveRegister = new ModbusSlaveFixedRegisterViewModel(dtoAi);

        // When store is empty
        Assert.NotNull(inspector.SlaveLiveMemoryValue);

        // Update tag 16 (AI0) with Float32 = 25.5f
        int rawFloatBits = BitConverter.SingleToInt32Bits(25.5f);
        store.UpdateTags(new[]
        {
            new RuntimeTagValue
            {
                TagIndex = 16,
                TagName = "AI0",
                Value = rawFloatBits,
                Quality = 192,
                Timestamp = DateTime.UtcNow
            }
        }, ProductDefinition.CreateRemoteIo8Di8Do4Ai());

        inspector.UpdateSlaveLiveValue();
        Assert.Equal("25.50", inspector.SlaveLiveMemoryValue);
    }

    [Fact]
    public void ModbusLiveTestInspectorViewModel_WhenLanguageChanges_UpdatesMappingGuideAndHeadersImmediately()
    {
        var originalLang = SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage;
        try
        {
            SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "en";
            var inspector = new ModbusLiveTestInspectorViewModel();
            inspector.IsSlaveMode = true;

            var dtoVflag = new ModbusSlaveFixedRegisterDto(
                Category: "Holding Registers",
                AddressHex: "0x0002 .. 0x0003",
                ModbusAddress: "40003 .. 40004",
                DeviceTag: "VFLAG_DWORD",
                DataType: "UINT32",
                AccessRight: "RW",
                FunctionCode: "FC 03, 06, 16",
                DescriptionVi: "Gom 32 cờ logic VFLAG0..VFLAG31",
                DescriptionEn: "Packed Virtual Flags VFLAG0..VFLAG31"
            );
            inspector.SelectedSlaveRegister = new ModbusSlaveFixedRegisterViewModel(dtoVflag);

            // In English
            Assert.Contains("logic flags", inspector.SlaveScadaMappingGuide);
            Assert.Contains("Packed Virtual Flags", inspector.SlaveDescriptionDisplay);

            // Now switch to Vietnamese without changing SelectedSlaveRegister
            SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = "vi";

            // Immediately updates to Vietnamese!
            Assert.Contains("cờ logic", inspector.SlaveScadaMappingGuide);
            Assert.Contains("Gom 32 cờ logic", inspector.SlaveDescriptionDisplay);
        }
        finally
        {
            SimplePLC.Studio.Services.LocalizationService.Instance.CurrentLanguage = originalLang;
        }
    }
}
