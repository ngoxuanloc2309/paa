using System;
using System.Linq;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Kiểm thử tích hợp phân hệ Zigbee Wireless trong DtuGatewayStudioViewModel và tầng Simulator.
/// </summary>
public class ZigbeeStudioIntegrationTests
{
    [Fact]
    public void DtuGatewayStudioViewModel_WhenDeviceHasZigbeeCapability_EnablesZigbeeSection()
    {
        // Arrange: XLINK_ZIGBEE_44 có SPLC_HardwareCapabilities.ZIGBEE
        var preset = HardwareDevicePreset.BuiltInPresets.First(p => p.Id == "XLINK_ZIGBEE_44");
        var metadata = new ProjectMetadata
        {
            DeviceClass = (ushort)preset.DeviceClass,
            ProductVariant = (ushort)preset.DeviceVariant,
            HardwareCapabilities = (uint)preset.HardwareCapabilities
        };

        // Act
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Assert
        Assert.True(vm.HasZigbeeCapability);
        Assert.NotNull(vm.ZigbeeVM);

        // Chuyển sang Section 5: Zigbee
        vm.SelectSection(5);
        Assert.True(vm.IsSectionZigbee);
        Assert.Equal(5, vm.SelectedSectionIndex);
        Assert.False(string.IsNullOrWhiteSpace(vm.ActiveInspectorTitle));
        Assert.False(string.IsNullOrWhiteSpace(vm.ActiveInspectorSubtitle));
    }

    [Fact]
    public void DtuGatewayStudioViewModel_WhenDeviceLacksZigbeeCapability_HidesZigbeeAndFallsBackSafely()
    {
        // Arrange: REMOTE_IO_8DI_8DO_4AI không có Zigbee
        var preset = HardwareDevicePreset.BuiltInPresets.First(p => p.Id == "REMOTE_IO_8DI_8DO_4AI");
        var metadata = new ProjectMetadata
        {
            DeviceClass = (ushort)preset.DeviceClass,
            ProductVariant = (ushort)preset.DeviceVariant,
            HardwareCapabilities = (uint)preset.HardwareCapabilities
        };

        // Act
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Assert
        Assert.False(vm.HasZigbeeCapability);

        // Thử chọn section 5 rồi kích hoạt kiểm tra tính hợp lệ
        vm.SelectSection(5);
        vm.SelectFirstAvailableSection();

        // Phải tự động fallback về section hợp lệ (RS-485 / Section 0)
        Assert.Equal(0, vm.SelectedSectionIndex);
        Assert.True(vm.IsSectionRs485);
    }

    [Fact]
    public void SimulatorRegisterMemory_ZigbeeRange_IsReadableAndWritable()
    {
        // Assert: 0x1700 .. 0x173F (64 holding registers)
        Assert.True(SimulatorRegisterMemory.IsReadableRange(ModbusRegisterMap.ZigbeeConfigBaseAddress, ModbusRegisterMap.ZigbeeConfigRegisters));
        Assert.True(SimulatorRegisterMemory.IsWritableRange(ModbusRegisterMap.ZigbeeConfigBaseAddress, ModbusRegisterMap.ZigbeeConfigRegisters));

        // Test reading and writing in simulator
        using var mem = new SimulatorRegisterMemory();
        var testData = new ushort[ModbusRegisterMap.ZigbeeConfigRegisters];
        testData[0] = 0x0001; // Enable
        testData[1] = 12;     // Slave ID
        testData[2] = 0x1A2B; // PAN ID
        testData[3] = 15;     // Channel

        mem.WriteRegisters(ModbusRegisterMap.ZigbeeConfigBaseAddress, testData);
        var readBack = mem.ReadRegisters(ModbusRegisterMap.ZigbeeConfigBaseAddress, ModbusRegisterMap.ZigbeeConfigRegisters);

        Assert.Equal(testData[0], readBack[0]);
        Assert.Equal(testData[1], readBack[1]);
        Assert.Equal(testData[2], readBack[2]);
        Assert.Equal(testData[3], readBack[3]);
    }
}
