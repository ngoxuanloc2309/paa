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
            ProductVariant = (ushort)SPLC_DtuVariant.VARIANT_DTU_RS485_NET,
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

        // 2. GATEWAY_RS485_ETH (Ethernet only, NO Wi-Fi)
        var ethMeta = new ProjectMetadata
        {
            DeviceClass = (ushort)SPLC_DeviceClass.GATEWAY,
            ProductVariant = (ushort)SPLC_GatewayVariant.VARIANT_RS485_ETH,
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
}
