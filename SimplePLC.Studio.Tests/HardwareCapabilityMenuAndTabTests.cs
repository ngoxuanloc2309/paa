using System;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests verifying Hardware Capabilities filtering across DTU/System Studio ViewModel,
/// Main Menu Items, and Workspace Tab synchronization.
/// </summary>
public sealed class HardwareCapabilityMenuAndTabTests
{
    [Fact]
    public void Localization_TabDevice_ReturnsSystemInEnglishAndHeThongInVietnamese()
    {
        var originalLang = LocalizationService.Instance.CurrentLanguage;
        try
        {
            LocalizationService.Instance.CurrentLanguage = "en";
            Assert.Equal("System", LocalizationService.Instance["TabDevice"]);
            Assert.Equal("System Workspace", LocalizationService.Instance["MenuViewDevice"]);

            LocalizationService.Instance.CurrentLanguage = "vi";
            Assert.Equal("Hệ thống", LocalizationService.Instance["TabDevice"]);
            Assert.Equal("Hệ thống (System)", LocalizationService.Instance["MenuViewDevice"]);
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = originalLang;
        }
    }

    [Fact]
    public void DtuGatewayStudioViewModel_Capabilities_ReflectTargetHardwareAccurately()
    {
        // 1. SYNX-DTU-485 (All comms supported)
        var dtuMeta = new ProjectMetadata
        {
            DeviceClass = (ushort)SPLC_DeviceClass.DTU,
            ProductVariant = (ushort)SPLC_DtuVariant.VARIANT_DTU_485,
            HardwareCapabilities = (uint)(SPLC_HardwareCapabilities.MODBUS_MASTER |
                                          SPLC_HardwareCapabilities.WIFI |
                                          SPLC_HardwareCapabilities.MQTT_PUB |
                                          SPLC_HardwareCapabilities.HTTP_CLIENT |
                                          SPLC_HardwareCapabilities.TRANSPARENT_STREAM)
        };
        var dtuVm = new DtuGatewayStudioViewModel(dtuMeta);

        Assert.True(dtuVm.HasRs485Capability);
        Assert.True(dtuVm.HasWifiCapability);
        Assert.True(dtuVm.HasMqttCapability);
        Assert.True(dtuVm.HasHttpCapability);
        Assert.True(dtuVm.HasTcpSocketCapability);
        Assert.True(dtuVm.HasSystemCapability);

        // 2. SYNX_GW_220 (Ethernet only, NO Wi-Fi)
        var ethMeta = new ProjectMetadata
        {
            DeviceClass = (ushort)SPLC_DeviceClass.GATEWAY,
            ProductVariant = (ushort)SPLC_GatewayVariant.VARIANT_GW_220,
            HardwareCapabilities = (uint)(SPLC_HardwareCapabilities.MODBUS_MASTER |
                                          SPLC_HardwareCapabilities.ETHERNET |
                                          SPLC_HardwareCapabilities.MQTT_PUB |
                                          SPLC_HardwareCapabilities.HTTP_CLIENT |
                                          SPLC_HardwareCapabilities.TRANSPARENT_STREAM)
        };
        var ethVm = new DtuGatewayStudioViewModel(ethMeta);

        Assert.True(ethVm.HasRs485Capability);
        Assert.False(ethVm.HasWifiCapability);
        Assert.True(ethVm.HasMqttCapability);
        Assert.True(ethVm.HasHttpCapability);
        Assert.True(ethVm.HasTcpSocketCapability);
        Assert.True(ethVm.HasSystemCapability);

        // 3. DATALOGGER (RTC & SD only, Zero Comms)
        var dataloggerMeta = new ProjectMetadata
        {
            DeviceClass = (ushort)SPLC_DeviceClass.DATALOGGER,
            HardwareCapabilities = (uint)(SPLC_HardwareCapabilities.BASE_LOGIC |
                                          SPLC_HardwareCapabilities.RETENTIVE |
                                          SPLC_HardwareCapabilities.RTC |
                                          SPLC_HardwareCapabilities.SD_DATALOG)
        };
        var dataVm = new DtuGatewayStudioViewModel(dataloggerMeta);

        Assert.False(dataVm.HasRs485Capability);
        Assert.False(dataVm.HasWifiCapability);
        Assert.False(dataVm.HasMqttCapability);
        Assert.False(dataVm.HasHttpCapability);
        Assert.False(dataVm.HasTcpSocketCapability);
        Assert.False(dataVm.HasSystemCapability);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_SelectFirstAvailableSection_FallsBackCorrectly()
    {
        // Device with Wi-Fi and MQTT, but NO RS-485
        var meta = new ProjectMetadata
        {
            HardwareCapabilities = (uint)(SPLC_HardwareCapabilities.WIFI | SPLC_HardwareCapabilities.MQTT_PUB)
        };
        var vm = new DtuGatewayStudioViewModel(meta);

        // Start at Section 0 (RS-485)
        vm.SelectSection(0);
        Assert.Equal(0, vm.SelectedSectionIndex);

        // Call auto-fallback: should jump to Section 1 (Wi-Fi)
        vm.SelectFirstAvailableSection();
        Assert.Equal(1, vm.SelectedSectionIndex);
        Assert.True(vm.IsSectionWifi);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_SelectFirstAvailableSection_SkipsWifiWhenDisabled()
    {
        // Device with RS-485 and MQTT, but NO Wi-Fi
        var meta = new ProjectMetadata
        {
            HardwareCapabilities = (uint)(SPLC_HardwareCapabilities.MODBUS_MASTER | SPLC_HardwareCapabilities.MQTT_PUB)
        };
        var vm = new DtuGatewayStudioViewModel(meta);

        // Force at Section 1 (Wi-Fi)
        vm.SelectSection(1);
        Assert.Equal(1, vm.SelectedSectionIndex);

        // Call auto-fallback: should jump to Section 0 (RS-485)
        vm.SelectFirstAvailableSection();
        Assert.Equal(0, vm.SelectedSectionIndex);
        Assert.True(vm.IsSectionRs485);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_WhenConnectedToHardwareProduct_OverridesCapabilitiesAccurately()
    {
        // 1. Project metadata ban đầu có Full Comms (RS-485, Wi-Fi, MQTT, HTTP, TCP)
        var dtuMeta = new ProjectMetadata
        {
            DeviceClass = (ushort)SPLC_DeviceClass.DTU,
            ProductVariant = (ushort)SPLC_DtuVariant.VARIANT_DTU_485,
            HardwareCapabilities = (uint)(SPLC_HardwareCapabilities.MODBUS_MASTER |
                                          SPLC_HardwareCapabilities.WIFI |
                                          SPLC_HardwareCapabilities.MQTT_PUB |
                                          SPLC_HardwareCapabilities.HTTP_CLIENT |
                                          SPLC_HardwareCapabilities.TRANSPARENT_STREAM),
            PhysicalRs485PortCount = 4
        };
        var dtuVm = new DtuGatewayStudioViewModel(dtuMeta);
        Assert.True(dtuVm.HasWifiCapability);
        Assert.True(dtuVm.HasRs485Capability);

        // 2. MCU kết nối thực tế chỉ có RS-485 và Modbus Master (KHÔNG có Wi-Fi, 1 cổng RS-485)
        var resources = new ProductResourceProfile(8, 8, 4, 32, 32, 32, 8);
        var product = SimplePLC.Domain.Builders.DeviceProfileBuilder.Build(
            (ushort)SPLC_DeviceClass.REMOTE_IO,
            1,
            2,
            2,
            100,
            (ushort)resources.TotalTags,
            resources,
            hardwareCapabilities: SPLC_HardwareCapabilities.MODBUS_MASTER | SPLC_HardwareCapabilities.BASE_LOGIC,
            physicalRs485PortCount: 1);

        // Act: Sync with connected product
        dtuVm.SyncWithHardwareProduct(product);

        // Assert: Wi-Fi bị ẩn, RS-485 vẫn hiện
        Assert.False(dtuVm.HasWifiCapability);
        Assert.True(dtuVm.HasRs485Capability);
        Assert.False(dtuVm.HasMqttCapability);
        Assert.Single(dtuVm.ModbusPortConfigVM.Ports);

        // Act: Ngắt kết nối (Disconnect)
        dtuVm.SyncWithHardwareProduct(null);

        // Assert: Khôi phục lại năng lực theo project metadata
        Assert.True(dtuVm.HasWifiCapability);
        Assert.True(dtuVm.HasMqttCapability);
        Assert.Equal(4, dtuVm.ModbusPortConfigVM.Ports.Count);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_WhenConnectedHardwareLacksCurrentSection_AutoFallsBackToAvailableSection()
    {
        var meta = new ProjectMetadata
        {
            HardwareCapabilities = (uint)(SPLC_HardwareCapabilities.MODBUS_MASTER | SPLC_HardwareCapabilities.WIFI)
        };
        var vm = new DtuGatewayStudioViewModel(meta);
        vm.SelectSection(1); // Đang đứng ở Wi-Fi
        Assert.True(vm.IsSectionWifi);

        // Phần cứng kết nối chỉ có RS-485 (không có Wi-Fi)
        var resources = new ProductResourceProfile(8, 8, 4, 32, 32, 32, 8);
        var product = SimplePLC.Domain.Builders.DeviceProfileBuilder.Build(
            (ushort)SPLC_DeviceClass.REMOTE_IO,
            1,
            2,
            2,
            100,
            (ushort)resources.TotalTags,
            resources,
            hardwareCapabilities: SPLC_HardwareCapabilities.MODBUS_MASTER,
            physicalRs485PortCount: 2);

        vm.SyncWithHardwareProduct(product);

        // Tự động nhảy về RS-485
        Assert.True(vm.IsSectionRs485);
        Assert.False(vm.HasWifiCapability);
    }

    [Fact]
    public void ModbusPortConfigViewModel_WhenHardwarePortLimitSynced_ClampsAvailablePorts()
    {
        var meta = new ProjectMetadata
        {
            PhysicalRs485PortCount = 4
        };
        var vm = new ModbusPortConfigViewModel(meta);
        Assert.Equal(4, vm.Ports.Count);

        // Sync MCU hardware with only 1 physical port
        vm.SyncHardwarePortLimit(1);
        Assert.Single(vm.Ports);
        Assert.Equal(0, vm.Ports[0].PortIndex);

        // Disconnect -> restore 4 ports
        vm.SyncHardwarePortLimit(null);
        Assert.Equal(4, vm.Ports.Count);
    }
}
