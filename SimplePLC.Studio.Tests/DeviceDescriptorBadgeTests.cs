using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests for device descriptor badges and device info presentation models.
/// </summary>
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

    [Fact]
    public void ShowDeviceInfo_WhenOffline_SeparatesTargetModelAndHardwareVersion()
    {
        var vm = new MainViewModel();
        vm.IsConnected = false;
        vm.CurrentProjectMetadata = new Models.ProjectMetadata
        {
            TargetDevice = "X.Link Zigbee 44",
            DeviceClass = (ushort)SPLC_DeviceClass.REMOTE_IO,
            DigitalInputs = 4,
            DigitalOutputs = 4
        };

        DeviceInfoViewModel? capturedInfo = null;
        vm.ShowDeviceInfoDialogHandler = info => capturedInfo = info;

        // Act
        vm.ShowDeviceInfoCommand.Execute(null);

        // Assert
        Assert.NotNull(capturedInfo);
        Assert.Equal("X.Link Zigbee 44", capturedInfo.TargetModelText);
        Assert.Equal("X.Link Zigbee 44", capturedInfo.DeviceModel);
        Assert.Equal("Remote I/O", capturedInfo.DeviceClassText);
        Assert.Contains("4DI / 4DO", capturedInfo.DeviceVariantText);
        // HW Version must NOT be the device model name!
        Assert.NotEqual("X.Link Zigbee 44", capturedInfo.HwVersion);
        Assert.Contains("v1.0", capturedInfo.HwVersion);
        Assert.False(capturedInfo.IsOnline);
    }

    [Fact]
    public void ShowDeviceInfo_WhenOnline_HardwareVersionFromDescriptor()
    {
        var vm = new MainViewModel();
        vm.IsConnected = true;
        vm.CurrentProjectMetadata = new Models.ProjectMetadata
        {
            TargetDevice = "SynaptiX SYNX-RIO-884",
            DeviceClass = (ushort)SPLC_DeviceClass.REMOTE_IO
        };

        var descriptor = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = 1,
            HwVersionMajor = 2,
            HwVersionMinor = 1,
            HwVersionPatch = 0,
            FwVersionMajor = 1,
            FwVersionMinor = 7,
            FwVersionPatch = 0,
            ProtocolVersion = 2,
            RuleFormatVersion = 2
        };
        vm.UpdateDeviceProductInfo(descriptor);

        DeviceInfoViewModel? capturedInfo = null;
        vm.ShowDeviceInfoDialogHandler = info => capturedInfo = info;

        // Act
        vm.ShowDeviceInfoCommand.Execute(null);

        // Assert
        Assert.NotNull(capturedInfo);
        Assert.Equal("SynaptiX SYNX-RIO-884", capturedInfo.TargetModelText);
        Assert.Equal("v2.1.0", capturedInfo.HwVersion);
        Assert.Equal("v1.7.0", capturedInfo.FwVersion);
        Assert.True(capturedInfo.IsOnline);
    }
}
