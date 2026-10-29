using System;
using System.IO;
using System.Linq;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="DtuGatewayStudioViewModel"/> covering state initialization,
/// tab navigation, uplink modes, JSON macro formatting, and diagnostic terminal actions.
/// </summary>
public sealed class DtuGatewayStudioViewModelTests
{
    [Fact]
    public void BuiltInPresets_ShouldContainDtuPreset()
    {
        var preset = HardwareDevicePreset.BuiltInPresets.FirstOrDefault(p => p.Id == "DTU_RS485_NET");
        Assert.NotNull(preset);
        Assert.Equal(SPLC_DeviceClass.DTU, preset.DeviceClass);
        Assert.Equal((ushort)SPLC_DtuVariant.VARIANT_DTU_RS485_NET, preset.DeviceVariant);
        Assert.True((preset.HardwareCapabilities & SPLC_HardwareCapabilities.HTTP_CLIENT) != 0);
        Assert.True((preset.HardwareCapabilities & SPLC_HardwareCapabilities.TRANSPARENT_STREAM) != 0);
        var product = preset.BuildProductDefinition();
        Assert.NotNull(product);
        Assert.Equal(32, product.Resources.VirtualRegisters);
    }

    [Fact]
    public void ViewModel_Initialization_ShouldLoadDefaultSettings()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        Assert.Equal(0, vm.SelectedTabIndex);
        Assert.Equal("Transference", vm.SerialMode);
        Assert.Equal(9600u, vm.BaudRate);
        Assert.Equal(8, vm.DataBits);
        Assert.Equal("None", vm.Parity);
        Assert.Equal(1, vm.StopBits);
        Assert.Equal(20, vm.FrameTimeoutDelayMs);
        Assert.Equal(512, vm.MaxPacketSize);

        Assert.Equal("Mqtt", vm.UplinkMode);
        Assert.Equal("https://api.factory-cloud.io/v1/telemetry", vm.HttpUrl);
        Assert.Equal("POST", vm.HttpMethod);
        Assert.Equal("Client", vm.TcpSocketMode);
        Assert.Equal("192.168.1.100", vm.TcpRemoteHost);
        Assert.True(vm.IsJsonSyntaxValid);
    }

    [Fact]
    public void JsonPreview_ShouldReplaceMacrosCorrectly()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            MqttClientId = "DTU_TEST_01",
            SelectedPortIndex = 0,
            BaudRate = 115200,
            SampleSerialInput = "DATA_ABC_123",
            JsonTemplate = "{\n  \"dev\": \"{{DEVICE_ID}}\",\n  \"val\": \"{{SERIAL_PAYLOAD}}\",\n  \"baud\": {{BAUDRATE}}\n}"
        };

        vm.UpdateJsonPreview();

        Assert.True(vm.IsJsonSyntaxValid);
        Assert.Contains("\"dev\": \"DTU_TEST_01\"", vm.JsonPreview);
        Assert.Contains("\"val\": \"DATA_ABC_123\"", vm.JsonPreview);
        Assert.Contains("\"baud\": 115200", vm.JsonPreview);
    }

    [Fact]
    public void JsonPreview_InvalidSyntax_ShouldSetIsJsonSyntaxValidToFalse()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            JsonTemplate = "{ invalid json without quotes: {{SERIAL_PAYLOAD}} "
        };

        vm.UpdateJsonPreview();

        Assert.False(vm.IsJsonSyntaxValid);
    }

    [Fact]
    public void SendTestPacket_ShouldAppendToTerminalLog()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            UplinkMode = "Http",
            TestPacketInput = "SENSOR:99.9"
        };

        vm.SendTestPacketCommand.Execute(null);

        Assert.Contains("[TX RS-485", vm.TerminalLog);
        Assert.Contains("[UPLINK HTTP]", vm.TerminalLog);
        Assert.Contains("SENSOR:99.9", vm.TerminalLog);
    }

    [Fact]
    public void SaveToMetadata_ShouldPersistAllDtuConfigs()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            SelectedPortIndex = 1,
            SerialMode = "ModbusMaster",
            BaudRate = 19200,
            DataBits = 8,
            Parity = "Even",
            StopBits = 1,
            FrameTimeoutDelayMs = 45,
            MaxPacketSize = 1024,

            UplinkMode = "Http",
            HttpUrl = "https://my-scada.vn/api/push",
            HttpMethod = "PUT",
            HttpTimeoutSec = 15,
            HttpIntervalSec = 30,

            MqttWillEnabled = true,
            MqttWillTopic = "plant/alert",
            MqttWillPayload = "DEVICE_OFFLINE",
            MqttWillQos = 2,
            MqttWillRetain = true,
            MqttSubscribeTopic = "factory/dtu/cmd",
            ResponseTimeoutMs = 350
        };

        vm.SaveToMetadata();

        var dtu = metadata.DtuConfig;
        Assert.True(dtu.Enabled);
        Assert.Equal(1, dtu.PortIndex);
        Assert.Equal("ModbusMaster", dtu.SerialMode);
        Assert.Equal("Http", dtu.UplinkMode);
        Assert.Equal(45, dtu.FrameTimeoutDelayMs);
        Assert.Equal(1024, dtu.MaxPacketSize);

        Assert.True(dtu.HttpSettings.Enabled);
        Assert.Equal("https://my-scada.vn/api/push", dtu.HttpSettings.Url);
        Assert.Equal("PUT", dtu.HttpSettings.Method);
        Assert.Equal(15, dtu.HttpSettings.TimeoutSec);

        Assert.True(metadata.MqttWillEnabled);
        Assert.Equal("plant/alert", metadata.MqttWillTopic);
        Assert.Equal("DEVICE_OFFLINE", metadata.MqttWillPayload);
        Assert.Equal(2, metadata.MqttWillQos);
        Assert.True(metadata.MqttWillRetain);
        Assert.Equal("factory/dtu/cmd", metadata.MqttSubTopic);
    }

    [Fact]
    public void BuildDtuConfigDto_ShouldConstructValidDto()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            SerialMode = "Transference",
            UplinkMode = "TcpClient",
            TcpRemoteHost = "10.0.0.50",
            TcpRemotePort = 9999,
            TcpReconnectDelaySec = 10,
            TcpHeartbeatIntervalSec = 20,
            TcpHeartbeatPayload = "HEARTBEAT"
        };

        var dto = vm.BuildDtuConfigDto();

        Assert.True(dto.Enabled);
        Assert.Equal(DtuSerialMode.Transference, dto.SerialMode);
        Assert.Equal(DtuUplinkMode.TcpClient, dto.UplinkMode);
        Assert.Equal("10.0.0.50", dto.TcpConfig.RemoteHost);
        Assert.Equal(9999, dto.TcpConfig.RemotePort);
        Assert.Equal(10, dto.TcpConfig.ReconnectDelaySec);
        Assert.Equal(20, dto.TcpConfig.HeartbeatIntervalSec);
        Assert.Equal("HEARTBEAT", dto.TcpConfig.HeartbeatPayload);
    }

    [Fact]
    public void ProjectFileService_ShouldSaveAndLoadDtuMetadata()
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"dtu_test_{Guid.NewGuid():N}.splcproj");
        try
        {
            var project = new ProjectModel
            {
                Metadata = new ProjectMetadata
                {
                    ProjectName = "DTU Factory Project",
                    DeviceClass = (ushort)SPLC_DeviceClass.DTU,
                    ProductVariant = (ushort)SPLC_DtuVariant.VARIANT_DTU_RS485_NET,
                    MqttWillEnabled = true,
                    MqttWillTopic = "station/will",
                    DtuConfig = new DtuConfigModel
                    {
                        Enabled = true,
                        SerialMode = "Transference",
                        UplinkMode = "Mqtt",
                        FrameTimeoutDelayMs = 30,
                        CustomJsonTemplate = "{\"custom\": 123}"
                    }
                }
            };

            ProjectFileService.SaveProject(tempFile, project, allowEmptyOverwrite: true);
            var loaded = ProjectFileService.LoadProject(tempFile);

            Assert.NotNull(loaded?.Metadata);
            Assert.Equal((ushort)SPLC_DeviceClass.DTU, loaded.Metadata.DeviceClass);
            Assert.True(loaded.Metadata.MqttWillEnabled);
            Assert.Equal("station/will", loaded.Metadata.MqttWillTopic);
            Assert.True(loaded.Metadata.DtuConfig.Enabled);
            Assert.Equal("Transference", loaded.Metadata.DtuConfig.SerialMode);
            Assert.Equal("Mqtt", loaded.Metadata.DtuConfig.UplinkMode);
            Assert.Equal(30, loaded.Metadata.DtuConfig.FrameTimeoutDelayMs);
            Assert.Equal("{\"custom\": 123}", loaded.Metadata.DtuConfig.CustomJsonTemplate);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public void SelectedTabIndex_WhenSwitchedAcrossAllFourTabs_MaintainsValidRange()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        Assert.Equal(0, vm.SelectedTabIndex);

        vm.SelectedTabIndex = 1;
        Assert.Equal(1, vm.SelectedTabIndex);

        vm.SelectedTabIndex = 2;
        Assert.Equal(2, vm.SelectedTabIndex);

        vm.SelectedTabIndex = 3;
        Assert.Equal(3, vm.SelectedTabIndex);
    }

    [Fact]
    public void UplinkMode_WhenSwitchedBetweenSupportedModes_UpdatesStateCorrectly()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        vm.UplinkMode = "Mqtt";
        Assert.Equal("Mqtt", vm.UplinkMode);

        vm.UplinkMode = "Http";
        Assert.Equal("Http", vm.UplinkMode);

        vm.UplinkMode = "TcpClient";
        Assert.Equal("TcpClient", vm.UplinkMode);

        vm.UplinkMode = "TcpServer";
        Assert.Equal("TcpServer", vm.UplinkMode);
    }

    [Fact]
    public void HttpFields_WhenAddedAndRemoved_UpdatesCollectionAccurately()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        int initialCount = vm.HttpFields.Count;
        Assert.True(initialCount > 0);

        vm.AddHttpFieldCommand.Execute(null);
        Assert.Equal(initialCount + 1, vm.HttpFields.Count);

        var lastField = vm.HttpFields.Last();
        vm.RemoveHttpFieldCommand.Execute(lastField);
        Assert.Equal(initialCount, vm.HttpFields.Count);
        Assert.DoesNotContain(lastField, vm.HttpFields);
    }

    [Fact]
    public void ClearTerminalCommand_WhenExecuted_ResetsLogToDefault()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            TerminalLog = "Dirty log output from transmission"
        };

        vm.ClearTerminalCommand.Execute(null);

        Assert.Contains("SYNAPTIX DTU GATEWAY DIAGNOSTIC TERMINAL", vm.TerminalLog);
        Assert.DoesNotContain("Dirty log output", vm.TerminalLog);
    }

    [Fact]
    public void UpdateJsonPreview_WhenTemplateHasSparseOrSpecialCharacters_HandlesSafelyWithoutException()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            JsonTemplate = "{\"message\": \"Hello World\", \"status\": 200}"
        };

        vm.UpdateJsonPreview();

        Assert.True(vm.IsJsonSyntaxValid);
        Assert.Contains("\"message\": \"Hello World\"", vm.JsonPreview);
        Assert.Contains("\"status\": 200", vm.JsonPreview);
    }

    [Fact]
    public void LocalizationService_WhenDtuKeysQueried_ReturnsValidStringsInBothLanguages()
    {
        var loc = LocalizationService.Instance;

        loc.CurrentLanguage = "vi";
        Assert.NotEmpty(loc["DtuMqttBrokerCardTitle"]);
        Assert.NotEmpty(loc["DtuResponseTimeoutLabel"]);
        Assert.NotEmpty(loc["DtuMqttSubTopicLabel"]);
        Assert.NotEmpty(loc["DtuTechNotesTitle"]);

        loc.CurrentLanguage = "en";
        Assert.NotEmpty(loc["DtuMqttBrokerCardTitle"]);
        Assert.NotEmpty(loc["DtuResponseTimeoutLabel"]);
        Assert.NotEmpty(loc["DtuMqttSubTopicLabel"]);
        Assert.NotEmpty(loc["DtuTechNotesTitle"]);
        Assert.NotEqual("Ghi chú kỹ thuật cổng RS-485:", loc["DtuTechNotesTitle"]);
    }
}
