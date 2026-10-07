using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class DeviceDescriptorBadgeTests
{
    [Fact]
    public void DeviceBadge_WhenDisconnected_ShowsProjectMetadataConfig()
    {
        var vm = new MainViewModel();
        vm.IsConnected = false;
        vm.UpdateDeviceProductInfo(null);

        Assert.Equal("Remote I/O (8DI / 8DO / 4AI)", vm.DeviceProductBadgeText);
        Assert.Contains("SynaptiX Remote I/O (8DI / 8DO / 4AI)", vm.DeviceProductToolTip);
        Assert.True(vm.DeviceProductToolTip.Contains("Offline") || vm.DeviceProductToolTip.Contains("Chưa kết nối"));
    }

    [Fact]
    public void DeviceBadge_WhenConnectedToRemoteIo_ResolvesVariantAndFirmware()
    {
        var vm = new MainViewModel();
        vm.IsConnected = true;

        var descriptor = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = 1,
            HwVersionMajor = 1,
            HwVersionMinor = 0,
            HwVersionPatch = 0,
            FwVersionMajor = 1,
            FwVersionMinor = 6,
            FwVersionPatch = 0,
            ProtocolVersion = 1,
            RuleFormatVersion = 7
        };

        vm.UpdateDeviceProductInfo(descriptor);

        Assert.Equal("Remote I/O (8DI / 8DO / 4AI)", vm.DeviceProductBadgeText);
        Assert.Contains("SynaptiX Remote I/O (8DI / 8DO / 4AI)", vm.DeviceProductToolTip);
        Assert.Contains("HW v1.0.0", vm.DeviceProductToolTip);
        Assert.Contains("FW v1.6.0", vm.DeviceProductToolTip);
    }

    [Fact]
    public void DeviceBadge_WhenConnectedToGateway_ResolvesGatewayAndFirmware()
    {
        var vm = new MainViewModel();
        vm.IsConnected = true;

        var descriptor = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.GATEWAY,
            DeviceVariant = 1,
            HwVersionMajor = 1,
            HwVersionMinor = 2,
            HwVersionPatch = 0,
            FwVersionMajor = 2,
            FwVersionMinor = 1,
            FwVersionPatch = 0,
            ProtocolVersion = 1,
            RuleFormatVersion = 7
        };

        vm.UpdateDeviceProductInfo(descriptor);

        Assert.Equal("Gateway (RS485 / Ethernet)", vm.DeviceProductBadgeText);
        Assert.Contains("SynaptiX Gateway (RS485 / Ethernet)", vm.DeviceProductToolTip);
        Assert.Contains("HW v1.2.0", vm.DeviceProductToolTip);
        Assert.Contains("FW v2.1.0", vm.DeviceProductToolTip);
    }

    [Fact]
    public void DeviceBadge_WhenDisconnectedAgain_RevertsToDefault()
    {
        var vm = new MainViewModel();
        vm.IsConnected = true;

        var descriptor = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.DATALOGGER,
            DeviceVariant = 1,
            HwVersionMajor = 1,
            HwVersionMinor = 0,
            HwVersionPatch = 0,
            FwVersionMajor = 1,
            FwVersionMinor = 0,
            FwVersionPatch = 0,
            ProtocolVersion = 1,
            RuleFormatVersion = 7
        };

        vm.UpdateDeviceProductInfo(descriptor);
        Assert.Contains("Datalogger", vm.DeviceProductBadgeText);

        // Disconnect
        vm.IsConnected = false;
        Assert.Equal("Remote I/O (8DI / 8DO / 4AI)", vm.DeviceProductBadgeText);
        Assert.Contains("SynaptiX Remote I/O (8DI / 8DO / 4AI)", vm.DeviceProductToolTip);
    }

    [Fact]
    public void DeviceBadge_WhenResourceInfoProvided_UsesDynamicIoCounts()
    {
        var vm = new MainViewModel();
        vm.IsConnected = true;

        var descriptor = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = 2,
            HwVersionMajor = 1,
            HwVersionMinor = 0,
            HwVersionPatch = 0,
            FwVersionMajor = 1,
            FwVersionMinor = 7,
            FwVersionPatch = 0,
            ProtocolVersion = 2,
            RuleFormatVersion = 2
        };

        var resource = new DeviceResourceInfoDto
        {
            DigitalInputCount = 16,
            DigitalOutputCount = 16,
            AnalogInputCount = 0,
            VirtualFlagCount = 64,
            VirtualRegisterCount = 64,
            RetentiveRegisterCount = 64,
            CounterCount = 16,
            MaxRules = 200,
            WireProfile = 2
        };

        vm.UpdateDeviceProductInfo(descriptor, resource);

        Assert.Equal("Remote I/O (16DI / 16DO)", vm.DeviceProductBadgeText);
        Assert.Contains("16DI / 16DO", vm.DeviceProductToolTip);
    }
}
