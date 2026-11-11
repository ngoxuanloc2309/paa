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
        Assert.Equal("ModbusMaster", vm.SerialMode);
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
            PayloadMode = "Custom",
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
            PayloadMode = "Custom",
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
        Assert.Contains("[TX-ASCII RS-485", vm.TerminalLog);
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
            PayloadMode = "Custom",
            TcpRemoteHost = "10.0.0.50",
            TcpRemotePort = 9999,
            TcpReconnectDelaySec = 10,
            TcpHeartbeatIntervalSec = 20,
            TcpHeartbeatPayload = "HEARTBEAT",
            TcpRegistrationType = "OnConnect",
            TcpRegistrationPayload = "REG:TEST"
        };
        var dto = vm.BuildDtuConfigDto();
        Assert.True(dto.Enabled);
        Assert.Equal(DtuSerialMode.Transference, dto.SerialMode);
        Assert.Equal(DtuUplinkMode.TcpClient, dto.UplinkMode);
        Assert.Equal(DtuPayloadMode.Custom, dto.PayloadMode);
        Assert.Equal("10.0.0.50", dto.TcpConfig.RemoteHost);
        Assert.Equal(9999, dto.TcpConfig.RemotePort);
        Assert.Equal(10, dto.TcpConfig.ReconnectDelaySec);
        Assert.Equal(20, dto.TcpConfig.HeartbeatIntervalSec);
        Assert.Equal("HEARTBEAT", dto.TcpConfig.HeartbeatPayload);
        Assert.Equal(DtuTcpRegistrationType.OnConnect, dto.TcpConfig.RegistrationType);
        Assert.Equal("REG:TEST", dto.TcpConfig.RegistrationPayload);
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
    public void DtuHttpFieldModel_WhenDefaultPayload_IsCustomValueEditableIsFalse()
    {
        var field = new DtuHttpFieldModel();
        Assert.Equal("Payload", field.ValueSource);
        Assert.False(field.IsCustomValueEditable);
    }
    [Fact]
    public void DtuHttpFieldModel_WhenSwitchedToCustom_IsCustomValueEditableIsTrueAndRaisesNotification()
    {
        var field = new DtuHttpFieldModel();
        bool propertyChangedRaised = false;
        field.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(field.IsCustomValueEditable))
            {
                propertyChangedRaised = true;
            }
        };
        field.ValueSource = "Custom";
        Assert.True(field.IsCustomValueEditable);
        Assert.True(propertyChangedRaised);
    }
    [Fact]
    public void DtuHttpFieldModel_WhenSwitchedFromCustomToDeviceId_IsCustomValueEditableIsFalse()
    {
        var field = new DtuHttpFieldModel { ValueSource = "Custom" };
        Assert.True(field.IsCustomValueEditable);
        field.ValueSource = "DeviceId";
        Assert.False(field.IsCustomValueEditable);
        field.ValueSource = "Timestamp";
        Assert.False(field.IsCustomValueEditable);
        field.ValueSource = "Payload";
        Assert.False(field.IsCustomValueEditable);
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
            PayloadMode = "Custom",
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
    // =========================================================================
    // IEC 61508-3 TRIAD TESTS: AXIS 1 - HAPPY PATH
    // =========================================================================
    /// <summary>
    /// Verifies that DtuConfigModel defaults adhere to IEC/ISA-95 industrial standard configurations.
    /// </summary>
    [Fact]
    public void DtuConfigModel_WhenInitialized_HasStandardDefaultValues()
    {
        var config = new DtuConfigModel();
        Assert.Equal("Standard", config.PayloadMode);
        Assert.True(config.IncludeDeviceId);
        Assert.True(config.IncludeTimestamp);
        Assert.True(config.IncludePort);
        Assert.True(config.IncludeSequence);
        Assert.False(config.IncludeHexStream);
        Assert.False(config.IncludePayloadLength);
        Assert.Equal("OnDataReceived", config.HttpSettings.TriggerMode);
        Assert.Equal("None", config.TcpSettings.RegistrationType);
        Assert.Equal("{{DEVICE_ID}}", config.TcpSettings.RegistrationPayload);
    }
    /// <summary>
    /// Verifies that modified payload modes, HTTP trigger mode, and TCP registration persist and restore accurately.
    /// </summary>
    [Fact]
    public void SaveAndLoadFromMetadata_WhenPayloadModesAndUplinkModified_PersistsAndRestoresAccurately()
    {
        var metadata = new ProjectMetadata();
        var vm1 = new DtuGatewayStudioViewModel(metadata)
        {
            PayloadMode = "Custom",
            IncludeDeviceId = false,
            IncludePayloadLength = false,
            HttpTriggerMode = "Periodic",
            TcpRegistrationType = "OnConnect",
            TcpRegistrationPayload = "REG:SYNAPTIX_001"
        };
        vm1.SaveToMetadata();
        Assert.Equal("Custom", metadata.DtuConfig.PayloadMode);
        Assert.False(metadata.DtuConfig.IncludeDeviceId);
        Assert.False(metadata.DtuConfig.IncludePayloadLength);
        Assert.Equal("Periodic", metadata.DtuConfig.HttpSettings.TriggerMode);
        Assert.Equal("OnConnect", metadata.DtuConfig.TcpSettings.RegistrationType);
        Assert.Equal("REG:SYNAPTIX_001", metadata.DtuConfig.TcpSettings.RegistrationPayload);
        var vm2 = new DtuGatewayStudioViewModel(metadata);
        Assert.Equal("Custom", vm2.PayloadMode);
        Assert.False(vm2.IncludeDeviceId);
        Assert.False(vm2.IncludePayloadLength);
        Assert.Equal("Periodic", vm2.HttpTriggerMode);
        Assert.Equal("OnConnect", vm2.TcpRegistrationType);
        Assert.Equal("REG:SYNAPTIX_001", vm2.TcpRegistrationPayload);
    }
    /// <summary>
    /// Verifies that Standard mode generates valid IEC/ISA-95 compliant JSON payload with enabled fields.
    /// </summary>
    [Fact]
    public void UpdateJsonPreview_WhenStandardModeSelected_GeneratesValidIecStandardJson()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            PayloadMode = "Standard",
            MqttClientId = "DEVICE_IEC_01",
            SampleSerialInput = "TEMP:28.5;HUM:65",
            IncludeDeviceId = true,
            IncludeTimestamp = true,
            IncludePort = true,
            IncludeSequence = true,
            IncludeHexStream = true,
            IncludePayloadLength = true
        };
        vm.UpdateJsonPreview();
        Assert.True(vm.IsJsonSyntaxValid);
        Assert.Contains("\"device_id\": \"DEVICE_IEC_01\"", vm.JsonPreview);
        Assert.Contains("\"port\": 1", vm.JsonPreview);
        Assert.Contains("\"seq\":", vm.JsonPreview);
        Assert.Contains("\"len\": 16", vm.JsonPreview);
        Assert.Contains("\"payload\": \"TEMP:28.5;HUM:65\"", vm.JsonPreview);
        Assert.Contains("\"hex\": \"54 45 4D 50 3A 32 38 2E 35 3B 48 55 4D 3A 36 35\"", vm.JsonPreview);
    }
    /// <summary>
    /// Verifies that Raw mode outputs transparent serial byte streams without JSON brackets.
    /// </summary>
    [Fact]
    public void UpdateJsonPreview_WhenRawModeSelected_DisplaysPureRawStreamWithoutJsonWrapper()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            PayloadMode = "Raw",
            SampleSerialInput = "RAW_MODBUS_STREAM_BYTE_ARRAY"
        };
        vm.UpdateJsonPreview();
        Assert.True(vm.IsJsonSyntaxValid);
        Assert.Equal("RAW_MODBUS_STREAM_BYTE_ARRAY", vm.JsonPreview);
        Assert.DoesNotContain("{", vm.JsonPreview);
        Assert.DoesNotContain("}", vm.JsonPreview);
    }
    /// <summary>
    /// Verifies that Custom mode macros {{PAYLOAD_LEN}} and {{PACKET_SEQ}} are replaced with computed numerical values.
    /// </summary>
    [Fact]
    public void UpdateJsonPreview_WhenCustomModeWithLenAndSeqMacros_ReplacesWithComputedValues()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            PayloadMode = "Custom",
            SampleSerialInput = "ABCDE",
            JsonTemplate = "{\n  \"len\": {{PAYLOAD_LEN}},\n  \"seq\": {{PACKET_SEQ}},\n  \"data\": \"{{SERIAL_PAYLOAD}}\"\n}"
        };
        vm.UpdateJsonPreview();
        Assert.True(vm.IsJsonSyntaxValid);
        Assert.Contains("\"len\": 5", vm.JsonPreview);
        Assert.Contains("\"seq\": 1", vm.JsonPreview);
        Assert.Contains("\"data\": \"ABCDE\"", vm.JsonPreview);
    }
    /// <summary>
    /// Verifies that HEX mode formatting formats log and increments sequence on valid hex input.
    /// </summary>
    [Fact]
    public void InjectDiagnosticMessage_WhenHexModeWithValidHex_FormatsLogAndIncrementsSequence()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            UplinkMode = "TcpClient",
            TestPacketIsHex = true,
            TestPacketInput = "48 65 6C 6C 6F"
        };
        vm.SendTestPacketCommand.Execute(null);
        Assert.Contains("[TX-HEX RS-485", vm.TerminalLog);
        Assert.Contains("(5B)", vm.TerminalLog);
        Assert.Contains("48 65 6C 6C 6F", vm.TerminalLog);
        Assert.Contains("[UPLINK TCP CLIENT]", vm.TerminalLog);
        Assert.Equal(2, vm.PacketSequenceCounter);
    }
    /// <summary>
    /// Verifies that newly added DTU keys in LocalizationService are properly localized in vi and en.
    /// </summary>
    [Fact]
    public void LocalizationService_WhenNewDtuKeysQueried_ReturnsValidStringsInBothLanguages()
    {
        var loc = LocalizationService.Instance;
        loc.CurrentLanguage = "vi";
        Assert.NotEmpty(loc["DtuPayloadModeStandard"]);
        Assert.NotEmpty(loc["DtuPayloadModeCustom"]);
        Assert.NotEmpty(loc["DtuPayloadModeRaw"]);
        Assert.NotEmpty(loc["DtuChkIncludeDeviceId"]);
        Assert.NotEmpty(loc["DtuHttpTriggerModeLabel"]);
        Assert.NotEmpty(loc["DtuTcpRegistrationTitle"]);
        Assert.NotEmpty(loc["DtuMonitorFormatHex"]);
        loc.CurrentLanguage = "en";
        Assert.NotEmpty(loc["DtuPayloadModeStandard"]);
        Assert.NotEmpty(loc["DtuPayloadModeCustom"]);
        Assert.NotEmpty(loc["DtuPayloadModeRaw"]);
        Assert.NotEmpty(loc["DtuChkIncludeDeviceId"]);
        Assert.NotEmpty(loc["DtuHttpTriggerModeLabel"]);
        Assert.NotEmpty(loc["DtuTcpRegistrationTitle"]);
        Assert.NotEmpty(loc["DtuMonitorFormatHex"]);
    }
    // =========================================================================
    // IEC 61508-3 TRIAD TESTS: AXIS 2 - BOUNDARY & CAPACITY
    // =========================================================================
    /// <summary>
    /// Verifies that when all optional checkboxes are disabled in Standard mode, minimal payload is rendered.
    /// </summary>
    [Fact]
    public void UpdateJsonPreview_WhenStandardModeAllCheckboxesFalse_GeneratesMinimalPayload()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            PayloadMode = "Standard",
            SampleSerialInput = "DATA_ONLY",
            IncludeDeviceId = false,
            IncludeTimestamp = false,
            IncludePort = false,
            IncludeSequence = false,
            IncludeHexStream = false,
            IncludePayloadLength = false
        };
        vm.UpdateJsonPreview();
        Assert.True(vm.IsJsonSyntaxValid);
        Assert.Contains("\"payload\": \"DATA_ONLY\"", vm.JsonPreview);
        Assert.DoesNotContain("\"device_id\"", vm.JsonPreview);
        Assert.DoesNotContain("\"timestamp\"", vm.JsonPreview);
        Assert.DoesNotContain("\"port\"", vm.JsonPreview);
        Assert.DoesNotContain("\"seq\"", vm.JsonPreview);
        Assert.DoesNotContain("\"hex\"", vm.JsonPreview);
        Assert.DoesNotContain("\"len\"", vm.JsonPreview);
    }
    /// <summary>
    /// Verifies that an empty sample serial input is handled safely without crashing.
    /// </summary>
    [Fact]
    public void UpdateJsonPreview_WhenSampleInputIsEmpty_HandlesGracefully()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            PayloadMode = "Standard",
            SampleSerialInput = string.Empty,
            IncludePayloadLength = true
        };
        vm.UpdateJsonPreview();
        Assert.True(vm.IsJsonSyntaxValid);
        Assert.Contains("\"len\": 0", vm.JsonPreview);
        Assert.Contains("\"payload\": \"\"", vm.JsonPreview);
    }
    /// <summary>
    /// Verifies that a large sample serial input correctly calculates UTF-8 payload length.
    /// </summary>
    [Fact]
    public void UpdateJsonPreview_WhenSampleInputIsLarge_ComputesAccurateByteLength()
    {
        var metadata = new ProjectMetadata();
        string largeInput = new string('A', 1024);
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            PayloadMode = "Standard",
            SampleSerialInput = largeInput,
            IncludePayloadLength = true
        };
        vm.UpdateJsonPreview();
        Assert.True(vm.IsJsonSyntaxValid);
        Assert.Contains("\"len\": 1024", vm.JsonPreview);
    }
    /// <summary>
    /// Verifies that single byte hex string (e.g. '0A') is formatted cleanly in terminal.
    /// </summary>
    [Fact]
    public void InjectDiagnosticMessage_WhenSingleByteHexProvided_FormatsCorrectly()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            TestPacketIsHex = true,
            TestPacketInput = "0A"
        };
        vm.SendTestPacketCommand.Execute(null);
        Assert.Contains("[TX-HEX RS-485", vm.TerminalLog);
        Assert.Contains("(1B)", vm.TerminalLog);
        Assert.Contains("0A", vm.TerminalLog);
    }
    /// <summary>
    /// Verifies that packet sequence counter increments monotonically across successive diagnostic transmissions.
    /// </summary>
    [Fact]
    public void PacketSequenceCounter_WhenIncrementedRepeatedly_IncrementsMonotonically()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            TestPacketIsHex = false,
            TestPacketInput = "PACKET"
        };
        Assert.Equal(1, vm.PacketSequenceCounter);
        vm.SendTestPacketCommand.Execute(null);
        Assert.Equal(2, vm.PacketSequenceCounter);
        vm.SendTestPacketCommand.Execute(null);
        Assert.Equal(3, vm.PacketSequenceCounter);
        vm.SendTestPacketCommand.Execute(null);
        Assert.Equal(4, vm.PacketSequenceCounter);
    }
    // =========================================================================
    // IEC 61508-3 TRIAD TESTS: AXIS 3 - FAULT INJECTION & RESILIENCE
    // =========================================================================
    /// <summary>
    /// Verifies that odd-length hex input triggers a validation error message in terminal rather than throwing an exception.
    /// </summary>
    [Fact]
    public void InjectDiagnosticMessage_WhenHexModeWithOddLengthHex_LogsValidationErrorWithoutCrashing()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            TestPacketIsHex = true,
            TestPacketInput = "ABC" // 3 nibbles -> odd length
        };
        vm.SendTestPacketCommand.Execute(null);
        Assert.Contains(LocalizationService.Instance["DtuTerminalInvalidHexError"], vm.TerminalLog);
        Assert.DoesNotContain("[UPLINK", vm.TerminalLog);
    }
    /// <summary>
    /// Verifies that invalid non-hex characters trigger a validation error message in terminal without crashing.
    /// </summary>
    [Fact]
    public void InjectDiagnosticMessage_WhenHexModeWithInvalidNonHexCharacters_LogsValidationErrorWithoutCrashing()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            TestPacketIsHex = true,
            TestPacketInput = "12 ZZ 34"
        };
        vm.SendTestPacketCommand.Execute(null);
        Assert.Contains(LocalizationService.Instance["DtuTerminalInvalidHexError"], vm.TerminalLog);
        Assert.DoesNotContain("[UPLINK", vm.TerminalLog);
    }
    /// <summary>
    /// Verifies that whitespace or empty test input is rejected silently without appending corrupt frames.
    /// </summary>
    [Fact]
    public void InjectDiagnosticMessage_WhenTestInputIsEmptyOrWhitespace_DoesNotAddLogOrIncrementSeq()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            TestPacketInput = "   "
        };
        string initialLog = vm.TerminalLog;
        long initialSeq = vm.PacketSequenceCounter;
        vm.SendTestPacketCommand.Execute(null);
        Assert.Equal(initialLog, vm.TerminalLog);
        Assert.Equal(initialSeq, vm.PacketSequenceCounter);
    }
    /// <summary>
    /// Verifies that corrupt or unknown payload mode strings fall back gracefully to Standard mode.
    /// </summary>
    [Fact]
    public void LoadFromMetadata_WhenMetadataPayloadModeIsNullOrCorrupt_FallsBackSafelyToStandard()
    {
        var metadata = new ProjectMetadata
        {
            DtuConfig = new DtuConfigModel
            {
                Enabled = true,
                PayloadMode = "UNKNOWN_CORRUPT_MODE"
            }
        };
        var vm = new DtuGatewayStudioViewModel(metadata);
        Assert.Equal("UNKNOWN_CORRUPT_MODE", vm.PayloadMode);
        vm.UpdateJsonPreview();
        // Since PayloadMode is unknown, it should fallback to standard JSON formatting safely
        Assert.True(vm.IsJsonSyntaxValid);
    }
    /// <summary>
    /// Verifies that InspectorConfigureBtn is localized and does not return the raw fallback key.
    /// </summary>
    [Fact]
    public void LocalizationService_InspectorConfigureBtn_IsLocalizedInBothLanguages()
    {
        var loc = LocalizationService.Instance;
        loc.CurrentLanguage = "vi";
        Assert.NotEqual("InspectorConfigureBtn", loc["InspectorConfigureBtn"]);
        Assert.Contains("DTU", loc["InspectorConfigureBtn"]);
        loc.CurrentLanguage = "en";
        Assert.NotEqual("InspectorConfigureBtn", loc["InspectorConfigureBtn"]);
        Assert.Contains("DTU", loc["InspectorConfigureBtn"]);
    }
    /// <summary>
    /// Verifies that switching HttpTriggerMode between OnDataReceived and Periodic updates IsHttpPeriodicTrigger and raises property changed.
    /// </summary>
    [Fact]
    public void HttpTriggerMode_WhenSwitchedBetweenOnDataAndPeriodic_UpdatesIsHttpPeriodicTrigger()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        Assert.Equal("OnDataReceived", vm.HttpTriggerMode);
        Assert.False(vm.IsHttpPeriodicTrigger);
        bool propertyChangedRaised = false;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(vm.IsHttpPeriodicTrigger))
                propertyChangedRaised = true;
        };
        vm.HttpTriggerMode = "Periodic";
        Assert.True(vm.IsHttpPeriodicTrigger);
        Assert.True(propertyChangedRaised);
        vm.HttpTriggerMode = "OnDataReceived";
        Assert.False(vm.IsHttpPeriodicTrigger);
    }
    /// <summary>
    /// Verifies that all 15 newly added DTU UI labels in LocalizationService return non-empty strings and not the raw key name in both vi and en.
    /// </summary>
    [Fact]
    public void LocalizationService_WhenDtuLabelsQueried_ReturnsNonEmptyLocalizedStringsAcrossLanguages()
    {
        var loc = LocalizationService.Instance;
        string[] keys =
        {
            "DtuMqttTopicsCardTitle",
            "DtuStandardFieldsTitle",
            "DtuStandardFieldsDesc",
            "DtuChkIncludeDeviceId",
            "DtuChkIncludeTimestamp",
            "DtuChkIncludePort",
            "DtuChkIncludeSequence",
            "DtuChkIncludeHexStream",
            "DtuChkIncludePayloadLength",
            "DtuHttpTriggerModeLabel",
            "DtuHttpIntervalHint",
            "DtuMonitorFormatAscii",
            "DtuMonitorFormatHex",
            "DtuPayloadRawNotice",
            "DtuTcpRegistrationTitle",
            "DtuTcpRegistrationTypeLabel",
            "DtuTcpRegistrationPayloadLabel"
        };
        foreach (var lang in new[] { "vi", "en" })
        {
            loc.CurrentLanguage = lang;
            foreach (var key in keys)
            {
                string val = loc[key];
                Assert.NotEmpty(val);
                Assert.NotEqual(key, val);
            }
        }
    }
    [Fact]
    public void ReloadFromMetadata_ShouldUpdateAllViewModelProperties()
    {
        // Arrange
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        Assert.Equal("ModbusMaster", vm.SerialMode);
        Assert.Equal("Mqtt", vm.UplinkMode);
        // Mutate metadata (mimicking Upload from MCU)
        metadata.DtuConfig.PortIndex = 1;
        metadata.DtuConfig.SerialMode = "ModbusMaster";
        metadata.DtuConfig.UplinkMode = "Http";
        metadata.DtuConfig.FrameTimeoutDelayMs = 45;
        metadata.DtuConfig.MaxPacketSize = 1024;
        metadata.DtuConfig.PayloadMode = "Custom";
        metadata.DtuConfig.IncludeDeviceId = false;
        metadata.DtuConfig.IncludeTimestamp = true;
        metadata.DtuConfig.IncludePort = false;
        metadata.DtuConfig.IncludeSequence = true;
        metadata.DtuConfig.IncludeHexStream = true;
        metadata.DtuConfig.IncludePayloadLength = true;
        metadata.DtuConfig.HttpSettings.Url = "https://mes.corp.net/api/v2/ingest";
        metadata.DtuConfig.HttpSettings.Method = "PUT";
        metadata.DtuConfig.HttpSettings.Headers = "Authorization: Bearer token123";
        metadata.DtuConfig.HttpSettings.TimeoutSec = 15;
        metadata.DtuConfig.HttpSettings.PeriodicIntervalSec = 30;
        metadata.DtuConfig.HttpSettings.TriggerMode = "Periodic";
        metadata.DtuConfig.TcpSettings.SocketMode = "Server";
        metadata.DtuConfig.TcpSettings.RemoteHost = "10.0.0.50";
        metadata.DtuConfig.TcpSettings.RemotePort = 9000;
        metadata.DtuConfig.TcpSettings.LocalPort = 9001;
        metadata.DtuConfig.TcpSettings.HeartbeatIntervalSec = 10;
        metadata.DtuConfig.TcpSettings.HeartbeatPayload = "HEARTBEAT";
        metadata.DtuConfig.TcpSettings.RegistrationType = "OnConnect";
        metadata.DtuConfig.TcpSettings.RegistrationPayload = "REGISTER_001";
        metadata.MqttWillEnabled = true;
        metadata.MqttWillTopic = "factory/emergency";
        metadata.MqttWillPayload = "DEVICE_OFFLINE";
        metadata.MqttWillQos = 2;
        metadata.MqttWillRetain = false;
        // Act
        vm.ReloadFromMetadata();
        // Assert
        Assert.Equal(1, vm.SelectedPortIndex);
        Assert.Equal("ModbusMaster", vm.SerialMode);
        Assert.Equal("Http", vm.UplinkMode);
        Assert.Equal(45, vm.FrameTimeoutDelayMs);
        Assert.Equal(1024, vm.MaxPacketSize);
        Assert.Equal("Custom", vm.PayloadMode);
        Assert.False(vm.IncludeDeviceId);
        Assert.True(vm.IncludeTimestamp);
        Assert.False(vm.IncludePort);
        Assert.True(vm.IncludeSequence);
        Assert.True(vm.IncludeHexStream);
        Assert.True(vm.IncludePayloadLength);
        Assert.Equal("https://mes.corp.net/api/v2/ingest", vm.HttpUrl);
        Assert.Equal("PUT", vm.HttpMethod);
        Assert.Equal("Authorization: Bearer token123", vm.HttpHeaders);
        Assert.Equal(15, vm.HttpTimeoutSec);
        Assert.Equal(30, vm.HttpIntervalSec);
        Assert.Equal("Periodic", vm.HttpTriggerMode);
        Assert.True(vm.IsHttpPeriodicTrigger);
        Assert.Equal("Server", vm.TcpSocketMode);
        Assert.Equal("10.0.0.50", vm.TcpRemoteHost);
        Assert.Equal(9000, vm.TcpRemotePort);
        Assert.Equal(9001, vm.TcpLocalPort);
        Assert.Equal(10, vm.TcpHeartbeatIntervalSec);
        Assert.Equal("HEARTBEAT", vm.TcpHeartbeatPayload);
        Assert.Equal("OnConnect", vm.TcpRegistrationType);
        Assert.Equal("REGISTER_001", vm.TcpRegistrationPayload);
        Assert.True(vm.MqttWillEnabled);
        Assert.Equal("factory/emergency", vm.MqttWillTopic);
        Assert.Equal("DEVICE_OFFLINE", vm.MqttWillPayload);
        Assert.Equal(2, vm.MqttWillQos);
        Assert.False(vm.MqttWillRetain);
    }
    [Fact]
    public void ModbusPortConfigVM_IsIntegratedAsSingleSourceOfTruth()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        Assert.NotNull(vm.ModbusPortConfigVM);
        Assert.True(vm.ModbusPortConfigVM.Ports.Count >= 2);
        Assert.NotNull(vm.ModbusPortConfigVM.SelectedPort);
        Assert.Equal(vm.ModbusPortConfigVM.SelectedPort.PortName, vm.PortName);
        Assert.Equal(vm.ModbusPortConfigVM.SelectedPort.BaudRate, vm.BaudRate);
    }
    [Fact]
    public void ModbusPortConfigVM_RoleSwitch_SynchronizesToDtuViewModel()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        // Ban ??u l? Master
        Assert.True(vm.IsMasterMode);
        Assert.False(vm.IsSlaveMode);
        // Chuy?n sang Slave tr?n ModbusPortConfigVM
        vm.ModbusPortConfigVM.SelectedPort!.Role = "Slave";
        Assert.Equal("ModbusSlave", vm.SerialMode);
        Assert.True(vm.IsSlaveMode);
        Assert.False(vm.IsMasterMode);
        Assert.Equal("Slave", vm.Rs485StatusBadge);
        // Chuy?n ng??c l?i Master b?ng SetSerialMode
        vm.SetSerialMode("ModbusMaster");
        Assert.Equal("Master", vm.ModbusPortConfigVM.SelectedPort.Role);
        Assert.True(vm.IsMasterMode);
        Assert.False(vm.IsSlaveMode);
    }
    [Fact]
    public void ModbusPortConfigVM_PortSwitch_UpdatesBaudRateAndInspector()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        if (vm.ModbusPortConfigVM.Ports.Count > 1)
        {
            var port2 = vm.ModbusPortConfigVM.Ports[1];
            port2.BaudRate = 19200;
            vm.ModbusPortConfigVM.SelectedPort = port2;
            Assert.Equal(19200u, vm.BaudRate);
            Assert.Equal(19200u, vm.SerialBaudRate);
            Assert.Equal(port2.PortName, vm.PortName);
        }
    }
    [Fact]
    public void ModbusPortConfigVM_SlaveFixedMemoryMap_ContainsFactoryMcuAddresses()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        var map = vm.ModbusPortConfigVM.FixedRegisterMap;
        Assert.NotNull(map);
        Assert.NotEmpty(map);
        Assert.Contains(map, r => r.ModbusAddress.Contains("40001") && r.DeviceTag == "DI_WORD");
        Assert.Contains(map, r => r.ModbusAddress.Contains("40002") && r.DeviceTag == "DO_WORD");
        Assert.Contains(map, r => r.DeviceTag.Contains("AI0"));
        Assert.Contains(map, r => r.DeviceTag.Contains("VREG0"));
    }
    [Fact]
    public void SaveConfiguration_AppliesModbusPortConfigToProjectMetadata()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        vm.ModbusPortConfigVM.SelectedPort!.BaudRate = 115200;
        vm.ModbusPortConfigVM.SelectedPort.Role = "Slave";
        vm.ModbusPortConfigVM.SelectedPort.SlaveId = 42;
        vm.SaveConfiguration();
        Assert.Equal(115200u, metadata.ModbusPorts[0].BaudRate);
        Assert.Equal("Slave", metadata.ModbusPorts[0].Role);
        Assert.Equal(42, metadata.ModbusPorts[0].SlaveId);
    }
    [Fact]
    public void DtuGatewayStudioViewModel_WhenSectionSwitchedToWifi_UpdatesActiveInspectorHeader()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        // Section 0: RS-485 Modbus
        vm.SelectSection(0);
        Assert.Equal(vm.LiveTestInspectorVM.InspectorHeaderTitle, vm.ActiveInspectorTitle);
        // Section 1: Wi-Fi Network
        vm.SelectSection(1);
        Assert.Equal(LocalizationService.Instance["InspectorWifiHeaderTitle"], vm.ActiveInspectorTitle);
        // With Wi-Fi disabled
        vm.WifiEnabled = false;
        Assert.Equal(LocalizationService.Instance["InspectorWifiDisabledSubtitle"], vm.ActiveInspectorSubtitle);
        // With Wi-Fi enabled and connected to an SSID
        vm.WifiEnabled = true;
        vm.WifiSsid = "Plant_IoT_AP";
        vm.WifiDhcpEnabled = true;
        Assert.Contains("Plant_IoT_AP", vm.ActiveInspectorSubtitle);
        Assert.Contains("DHCP", vm.ActiveInspectorSubtitle);
    }
    [Fact]
    public void DtuGatewayStudioViewModel_WhenWifiDisabled_FormattedRssiIndicatesRadioOff()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        vm.WifiEnabled = false;
        Assert.Equal(LocalizationService.Instance["WifiRssiRadioOff"], vm.FormattedRssi);
        vm.WifiEnabled = true;
        // When device is offline
        Assert.Equal(LocalizationService.Instance["WifiRssiOffline"], vm.FormattedRssi);

        // When device is connected
        vm.UpdateHardwareConnectionState(true, "COM3", "SYNX-DTU-485");
        Assert.Contains("-58 dBm", vm.FormattedRssi);
        Assert.DoesNotContain("dBm dBm", vm.FormattedRssi);
        Assert.True(vm.SignalQualityPercent > 0);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_SignalQualityPercent_CalculatedCorrectlyBasedOnConnection()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        
        vm.WifiEnabled = false;
        Assert.Equal(0, vm.SignalQualityPercent);

        vm.WifiEnabled = true;
        Assert.Equal(0, vm.SignalQualityPercent);

        vm.UpdateHardwareConnectionState(true, "COM3", "SYNX-DTU-485");
        Assert.True(vm.SignalQualityPercent >= 50);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_WifiDisplayProperties_ReflectEmptyStatesAndAssignmentsCorrectly()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Default empty SSID
        Assert.Equal(LocalizationService.Instance["WifiNoSsidPlaceholder"], vm.WifiSsidDisplay);

        vm.WifiSsid = "SynaptiX_Office";
        Assert.Equal("SynaptiX_Office", vm.WifiSsidDisplay);

        // DHCP Assignment
        vm.WifiDhcpEnabled = true;
        Assert.Equal(LocalizationService.Instance["WifiIpDhcpAuto"], vm.WifiIpAssignmentDisplay);

        vm.WifiDhcpEnabled = false;
        Assert.Equal(LocalizationService.Instance["WifiIpStaticManual"], vm.WifiIpAssignmentDisplay);
    }
    [Fact]
    public void DtuGatewayStudioViewModel_ScanNetworks_PopulatesCleanSsidWithoutAppendedRssi()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        vm.ScanNetworks();
        Assert.NotEmpty(vm.AvailableSsids);
        foreach (var ssid in vm.AvailableSsids)
        {
            Assert.DoesNotContain("dBm", ssid);
            Assert.DoesNotContain("(", ssid);
        }
    }
    [Fact]
    public void DtuGatewayStudioViewModel_WhenLanguageChanges_UpdatesWifiTitlesImmediately()
    {
        var originalLang = LocalizationService.Instance.CurrentLanguage;
        try
        {
            LocalizationService.Instance.CurrentLanguage = "en";
            var metadata = new ProjectMetadata();
            var vm = new DtuGatewayStudioViewModel(metadata);
            vm.SelectSection(1);
            Assert.Equal("WI-FI DIAGNOSTICS & TELEMETRY", vm.ActiveInspectorTitle);
            LocalizationService.Instance.CurrentLanguage = "vi";
            Assert.Equal("THÔNG TIN MẠNG WI-FI", vm.ActiveInspectorTitle);
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = originalLang;
        }
    }

    [Fact]
    public void DtuGatewayStudioViewModel_ToggleWifiPasswordVisibility_TogglesCorrectly()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        Assert.False(vm.IsWifiPasswordVisible);

        vm.ToggleWifiPasswordVisibilityCommand.Execute(null);
        Assert.True(vm.IsWifiPasswordVisible);

        vm.ToggleWifiPasswordVisibilityCommand.Execute(null);
        Assert.False(vm.IsWifiPasswordVisible);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_WifiPassword_CanBeUpdatedAndPersisted()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata)
        {
            WifiSsid = "Plant_Mesh_5G",
            WifiPassword = "IndustrialSecretKey#2026"
        };

        Assert.Equal("IndustrialSecretKey#2026", vm.WifiPassword);

        vm.SaveConfiguration();
        Assert.Equal("IndustrialSecretKey#2026", metadata.WifiPassword);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_WhenWifiDisabled_StatusBadgeAndInspectorReflectDisabled()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        vm.WifiEnabled = false;
        Assert.Equal(LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled", vm.WifiStatusBadge);
        Assert.Equal(LocalizationService.Instance["WifiRssiRadioOff"], vm.FormattedRssi);

        vm.WifiEnabled = true;
        Assert.NotEqual(LocalizationService.Instance.IsVietnamese ? "Đã tắt" : "Disabled", vm.WifiStatusBadge);
    }

    [Fact]
    public void HardwareCapabilities_WhenDataloggerPresetActive_DisablesHttpAndTcpSocket()
    {
        var metadata = new ProjectMetadata
        {
            DeviceClass = (ushort)SPLC_DeviceClass.DATALOGGER
        };
        var vm = new DtuGatewayStudioViewModel(metadata);

        Assert.False(vm.HasHttpCapability);
        Assert.False(vm.HasTcpSocketCapability);
        Assert.True(vm.HasSdDatalogCapability);
    }

    [Fact]
    public void HardwareCapabilities_WhenDtuPresetActive_EnablesMqttHttpAndTcp()
    {
        var metadata = new ProjectMetadata
        {
            DeviceClass = (ushort)SPLC_DeviceClass.DTU
        };
        var vm = new DtuGatewayStudioViewModel(metadata);

        Assert.True(vm.HasHttpCapability);
        Assert.True(vm.HasTcpSocketCapability);
        Assert.True(vm.HasMqttCapability);
    }

    [Fact]
    public void HardwareCapabilities_WhenRemoteIoPresetActive_DisablesWifiAndCloud()
    {
        var metadata = new ProjectMetadata
        {
            DeviceClass = (ushort)SPLC_DeviceClass.REMOTE_IO
        };
        var vm = new DtuGatewayStudioViewModel(metadata);

        Assert.False(vm.HasWifiCapability);
        Assert.False(vm.HasMqttCapability);
    }

    [Fact]
    public void MqttBrokerConfig_WhenSavedAndReloaded_SynchronizesTwoWayWithMetadata()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        vm.MqttBrokerConfigVM.Host = "mqtt.edge.internal";
        vm.MqttBrokerConfigVM.Port = 8883;
        vm.MqttBrokerConfigVM.UseTls = true;
        vm.MqttBrokerConfigVM.WillTopic = "plant/status";
        vm.MqttBrokerConfigVM.WillPayload = "{\"state\":\"dead\"}";

        vm.SaveConfiguration();

        Assert.Equal("mqtt.edge.internal", metadata.MqttBrokerHost);
        Assert.Equal(8883, metadata.MqttBrokerPort);
        Assert.True(metadata.MqttTlsEnabled);
        Assert.Equal("plant/status", metadata.MqttWillTopic);
        Assert.Equal("{\"state\":\"dead\"}", metadata.MqttWillPayload);

        var vm2 = new DtuGatewayStudioViewModel(metadata);
        Assert.Equal("mqtt.edge.internal", vm2.MqttBrokerConfigVM.Host);
        Assert.Equal(8883, vm2.MqttBrokerConfigVM.Port);
        Assert.True(vm2.MqttBrokerConfigVM.UseTls);
        Assert.Equal("plant/status", vm2.MqttBrokerConfigVM.WillTopic);
    }

    [Fact]
    public void MqttTelemetry_WhenFieldAdded_PersistsToProjectMetadata()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        vm.MqttTelemetryNode.Topic = "plant/sensors";
        vm.MqttTelemetryNode.Fields.Clear();
        vm.MqttTelemetryNode.Fields.Add(new MqttPayloadFieldViewModel("temp_c", null));

        vm.SaveConfiguration();

        Assert.Equal("plant/sensors", metadata.MqttTopic);
        Assert.Single(metadata.MqttFields);
        Assert.Equal("temp_c", metadata.MqttFields[0].Key);

        var vm2 = new DtuGatewayStudioViewModel(metadata);
        Assert.Equal("plant/sensors", vm2.MqttTelemetryNode.Topic);
        Assert.Single(vm2.MqttTelemetryNode.Fields);
        Assert.Equal("temp_c", vm2.MqttTelemetryNode.Fields[0].Key);
    }

    [Fact]
    public void MqttCommand_WhenPinAdded_PersistsToProjectMetadata()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        vm.MqttCommandNode.Topic = "plant/commands";
        vm.MqttCommandNode.Pins.Clear();
        vm.MqttCommandNode.Pins.Add(new MqttPinBindingViewModel("SpeedCmd", "speed", MqttDataType.Int16));

        vm.SaveConfiguration();

        Assert.Equal("plant/commands", metadata.MqttSubTopic);
        Assert.Single(metadata.MqttSubPins);
        Assert.Equal("SpeedCmd", metadata.MqttSubPins[0].Label);

        var vm2 = new DtuGatewayStudioViewModel(metadata);
        Assert.Equal("plant/commands", vm2.MqttCommandNode.Topic);
        Assert.Single(vm2.MqttCommandNode.Pins);
        Assert.Equal("SpeedCmd", vm2.MqttCommandNode.Pins[0].Label);
    }

    [Fact]
    public void MqttActiveSubSection_WhenSelected_UpdatesVisibilityProperties()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        vm.SelectMqttSubSectionCommand.Execute("0");
        Assert.Equal(0, vm.MqttActiveSubSection);
        Assert.True(vm.IsMqttSubSectionBroker);
        Assert.False(vm.IsMqttSubSectionTelemetry);
        Assert.False(vm.IsMqttSubSectionCommand);

        vm.SelectMqttSubSectionCommand.Execute("1");
        Assert.Equal(1, vm.MqttActiveSubSection);
        Assert.False(vm.IsMqttSubSectionBroker);
        Assert.True(vm.IsMqttSubSectionTelemetry);
        Assert.False(vm.IsMqttSubSectionCommand);

        vm.SelectMqttSubSectionCommand.Execute("2");
        Assert.Equal(2, vm.MqttActiveSubSection);
        Assert.False(vm.IsMqttSubSectionBroker);
        Assert.False(vm.IsMqttSubSectionTelemetry);
        Assert.True(vm.IsMqttSubSectionCommand);
    }

    [Fact]
    public void MqttCommand_WithTargetTag_PersistsAndRestoresAcrossMetadata()
    {
        var tagCatalog = new TagCatalogViewModel();
        var targetTag = tagCatalog.AllTags.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.VirtualRegister);
        Assert.NotNull(targetTag);

        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata, tagCatalog);

        vm.MqttCommandNode.Topic = "plant/commands";
        vm.MqttCommandNode.Pins.Clear();
        var pin = new MqttPinBindingViewModel("TargetSpeed", "speed", MqttDataType.Int16)
        {
            TargetTag = targetTag
        };
        vm.MqttCommandNode.Pins.Add(pin);

        Assert.Equal(targetTag.Name, pin.TargetTagName);
        Assert.Equal((ushort)targetTag.Index, pin.TargetTagIndex);

        vm.SaveConfiguration();

        Assert.Single(metadata.MqttSubPins);
        Assert.Equal("TargetSpeed", metadata.MqttSubPins[0].Label);
        Assert.Equal(targetTag.Name, metadata.MqttSubPins[0].TargetTagName);
        Assert.Equal((ushort)targetTag.Index, metadata.MqttSubPins[0].TargetTagIndex);

        // Reload in a fresh ViewModel
        var vm2 = new DtuGatewayStudioViewModel(metadata, tagCatalog);
        Assert.Single(vm2.MqttCommandNode.Pins);
        var restoredPin = vm2.MqttCommandNode.Pins[0];
        Assert.Equal("TargetSpeed", restoredPin.Label);
        Assert.NotNull(restoredPin.TargetTag);
        Assert.Equal(targetTag.Name, restoredPin.TargetTag.Name);
        Assert.Equal(targetTag.Index, restoredPin.TargetTag.Index);
        Assert.Equal(targetTag.Name, restoredPin.TargetTagName);
        Assert.Equal((ushort)targetTag.Index, restoredPin.TargetTagIndex);
    }

    [Fact]
    public void MqttCommand_WithNoneTargetTag_YieldsNullIndexAndPersistsCleanly()
    {
        var tagCatalog = new TagCatalogViewModel();
        var noneTag = tagCatalog.AllTags.FirstOrDefault(t => t.Kind == SimplePLC.Domain.Enums.TagKind.None);
        Assert.NotNull(noneTag);

        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata, tagCatalog);

        vm.MqttCommandNode.Pins.Clear();
        var pin = new MqttPinBindingViewModel("RawPayloadPin", "payload", MqttDataType.String)
        {
            TargetTag = noneTag
        };
        vm.MqttCommandNode.Pins.Add(pin);

        Assert.Null(pin.TargetTagIndex);

        vm.SaveConfiguration();

        Assert.Single(metadata.MqttSubPins);
        Assert.Null(metadata.MqttSubPins[0].TargetTagIndex);

        var vm2 = new DtuGatewayStudioViewModel(metadata, tagCatalog);
        Assert.Single(vm2.MqttCommandNode.Pins);
        Assert.Null(vm2.MqttCommandNode.Pins[0].TargetTagIndex);
    }

    /// <summary>
    /// [Axiom 2 / ISA-101]: Inspector Cloud Uplink phản ánh trung thực trạng thái 3 Card:
    /// Card 1 (Topic & Triggers), Card 2 (Broker host & ports), Card 3 (Data & Payload Statistics).
    /// </summary>
    [Fact]
    public void MqttCloudUplinkInspector_SummaryProperties_ReflectNodeStateAndLanguage()
    {
        var tagCatalog = new TagCatalogViewModel();
        var metadata = new ProjectMetadata
        {
            MqttTopic = "factory/area1/telemetry",
            MqttSubTopic = "factory/area1/cmd"
        };
        var vm = new DtuGatewayStudioViewModel(metadata, tagCatalog);

        // 1. Initial State from metadata
        Assert.Equal("factory/area1/telemetry", vm.MqttPublishTopic);
        Assert.Equal("factory/area1/cmd", vm.MqttSubscribeTopic);
        Assert.Contains("1000ms", vm.MqttPublishModeDisplay);
        Assert.Contains("FIFO", vm.MqttQueueModeDisplay);

        // 2. Add telemetry field and check reactive updates
        vm.MqttTelemetryNode.Fields.Add(new MqttPayloadFieldViewModel("temp", tagCatalog.AllTags.First(t => t.Name == "VREG0")));
        vm.MqttTelemetryNode.Fields.Add(new MqttPayloadFieldViewModel("humid", tagCatalog.AllTags.First(t => t.Name == "VREG1")));

        // 3. Add command pin and check reactive updates
        var targetTag = tagCatalog.AllTags.First(t => t.Name == "DO0");
        var pin1 = new MqttPinBindingViewModel("p1", "pump", MqttDataType.Bool) { TargetTag = targetTag };
        var pin2 = new MqttPinBindingViewModel("p2", "valve", MqttDataType.Bool); // Unmapped
        vm.MqttCommandNode.Pins.Add(pin1);
        vm.MqttCommandNode.Pins.Add(pin2);

        // Assert Card 3 statistics (Vietnamese)
        LocalizationService.Instance.CurrentLanguage = "vi";
        Assert.Equal("2 trường", vm.MqttPublishFieldsSummaryText);
        Assert.Equal("2 lệnh", vm.MqttSubscribePinsSummaryText);
        Assert.Equal("1/2 tags", vm.MqttMappedTagsSummaryText);
        Assert.Contains("bytes / msg", vm.MqttEstimatedPayloadSizeText);

        // Assert Card 3 statistics (English)
        LocalizationService.Instance.CurrentLanguage = "en";
        Assert.Equal("2 fields", vm.MqttPublishFieldsSummaryText);
        Assert.Equal("2 commands", vm.MqttSubscribePinsSummaryText);
        Assert.Equal("1/2 tags", vm.MqttMappedTagsSummaryText);

        // Dynamically map pin2 -> Mapped tags count updates reactively
        pin2.TargetTag = tagCatalog.AllTags.First(t => t.Name == "DO1");
        Assert.Equal("2/2 tags", vm.MqttMappedTagsSummaryText);

        // 4. Mode change
        vm.MqttTelemetryNode.PublishMode = "Event";
        Assert.Contains("Trigger", vm.MqttPublishModeDisplay);

        LocalizationService.Instance.CurrentLanguage = "vi";
        Assert.Contains("Theo xung kích hoạt", vm.MqttPublishModeDisplay);
    }

    /// <summary>
    /// [Axiom 1 / Ground Truth]: MqttPublishTopic và MqttSubscribeTopic lưu và nạp lại chuẩn xác từ ProjectMetadata.
    /// </summary>
    [Fact]
    public void MqttCloudUplink_PublishAndSubscribeTopics_PersistAndReloadFromMetadata()
    {
        var tagCatalog = new TagCatalogViewModel();
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata, tagCatalog);

        vm.MqttPublishTopic = "custom/plant/telemetry";
        vm.MqttSubscribeTopic = "custom/plant/control";

        vm.SaveConfiguration();

        Assert.Equal("custom/plant/telemetry", metadata.MqttTopic);
        Assert.Equal("custom/plant/control", metadata.MqttSubTopic);

        var vmReloaded = new DtuGatewayStudioViewModel(metadata, tagCatalog);
        Assert.Equal("custom/plant/telemetry", vmReloaded.MqttPublishTopic);
        Assert.Equal("custom/plant/control", vmReloaded.MqttSubscribeTopic);
    }

    /// <summary>
    /// [Axiom 3 / Input State Machine]: Kiểm tra chuyển đổi section sidebar (0: RS-485, 1: Wi-Fi, 2: MQTT, 3: HTTP, 4: TCP, 5: Timing).
    /// </summary>
    [Fact]
    public void DtuGatewayStudioViewModel_SectionNavigation_ResolvesAllSectionsCorrectly()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Section 0: RS-485
        vm.SelectSection(0);
        Assert.True(vm.IsSectionRs485);
        Assert.False(vm.IsSectionWifi);
        Assert.False(vm.IsSectionMqtt);
        Assert.False(vm.IsSectionHttp);
        Assert.False(vm.IsSectionTcp);
        Assert.False(vm.IsSectionUplink);

        // Section 1: Wi-Fi
        vm.SelectSection(1);
        Assert.True(vm.IsSectionWifi);
        Assert.False(vm.IsSectionRs485);
        Assert.False(vm.IsSectionMqtt);

        // Section 2: MQTT Client
        vm.SelectSection(2);
        Assert.True(vm.IsSectionMqtt);
        Assert.True(vm.IsSectionUplink);
        Assert.False(vm.IsSectionHttp);
        Assert.False(vm.IsSectionTcp);

        // Section 3: HTTP Client
        vm.SelectSection(3);
        Assert.True(vm.IsSectionHttp);
        Assert.True(vm.IsSectionUplink);
        Assert.False(vm.IsSectionMqtt);
        Assert.False(vm.IsSectionTcp);

        // Section 4: TCP Client
        vm.SelectSection(4);
        Assert.True(vm.IsSectionTcp);
        Assert.True(vm.IsSectionUplink);
        Assert.False(vm.IsSectionMqtt);
        Assert.False(vm.IsSectionHttp);


    }

    /// <summary>
    /// [Axiom 2 / Visual Truth]: Tiêu đề và phụ đề Inspector hiển thị chính xác theo từng section.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioViewModel_ActiveInspectorTitlesAndSubtitles_UpdateAccurately()
    {
        var originalLang = LocalizationService.Instance.CurrentLanguage;
        try
        {
            LocalizationService.Instance.CurrentLanguage = "en";
            var metadata = new ProjectMetadata();
            var vm = new DtuGatewayStudioViewModel(metadata);

            // MQTT (Section 2)
            vm.SelectSection(2);
            Assert.Equal(LocalizationService.Instance["InspectorMqttHeaderTitle"], vm.ActiveInspectorTitle);
            Assert.Equal(LocalizationService.Instance["InspectorMqttHeaderSubtitle"], vm.ActiveInspectorSubtitle);

            // HTTP (Section 3)
            vm.SelectSection(3);
            Assert.Equal(LocalizationService.Instance["InspectorHttpHeaderTitle"], vm.ActiveInspectorTitle);
            Assert.Equal(LocalizationService.Instance["InspectorHttpHeaderSubtitle"], vm.ActiveInspectorSubtitle);

            // TCP (Section 4)
            vm.SelectSection(4);
            Assert.Equal(LocalizationService.Instance["InspectorTcpHeaderTitle"], vm.ActiveInspectorTitle);
            Assert.Equal(LocalizationService.Instance["InspectorTcpHeaderSubtitle"], vm.ActiveInspectorSubtitle);


        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = originalLang;
        }
    }

    /// <summary>
    /// [Axiom 2 / Visual Truth]: Huy hiệu điều hướng sidebar phản ánh trạng thái bật/tắt của từng dịch vụ
    /// tuân thủ nghiêm ngặt chuẩn đồng bộ nhị phân thuần túy: "Active" / "Disabled" trên toàn bộ các dịch vụ.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioViewModel_SidebarBadges_ReflectProtocolEnabledStates()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Wi-Fi Badge
        vm.WifiEnabled = false;
        Assert.Equal("Disabled", vm.WifiStatusBadge);
        vm.WifiEnabled = true;
        Assert.Equal("Active", vm.WifiStatusBadge);

        // MQTT Badge
        vm.MqttBrokerConfigVM.IsEnabled = false;
        Assert.Equal("Disabled", vm.MqttSidebarBadgeText);
        vm.MqttBrokerConfigVM.IsEnabled = true;
        Assert.Equal("Active", vm.MqttSidebarBadgeText);

        // HTTP Badge
        vm.HttpEnabled = false;
        Assert.Equal("Disabled", vm.HttpSidebarBadgeText);
        vm.HttpEnabled = true;
        Assert.Equal("Active", vm.HttpSidebarBadgeText);

        // TCP Badge
        vm.TcpEnabled = false;
        Assert.Equal("Disabled", vm.TcpSidebarBadgeText);
        vm.TcpEnabled = true;
        Assert.Equal("Active", vm.TcpSidebarBadgeText);

        // Zigbee Badge
        vm.ZigbeeVM.IsEnabled = false;
        Assert.Equal("Disabled", vm.ZigbeeSidebarBadgeText);
        vm.ZigbeeVM.IsEnabled = true;
        Assert.Equal("Active", vm.ZigbeeSidebarBadgeText);

        // RS-485 Badge
        Assert.NotNull(vm.Rs485StatusBadge);
    }

    /// <summary>
    /// [Axiom 1 / Hardware Ground Truth]: Các thuộc tính hiển thị Inspector của HTTP và TCP định dạng chính xác.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioViewModel_HttpAndTcpDisplayProperties_ProvideCleanFormatting()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        // HTTP Inspector formatting
        vm.HttpUrl = "https://iot.plant.vn/api/telemetry";
        vm.HttpMethod = "POST";
        vm.HttpTimeoutSec = 15;
        vm.HttpHeaders = "Authorization: Bearer test_token\nContent-Type: application/json";

        Assert.Equal("https://iot.plant.vn/api/telemetry", vm.HttpEndpointDisplay);
        Assert.Equal("POST", vm.HttpMethodDisplay);
        Assert.Equal("15s", vm.HttpTimeoutDisplay);
        Assert.Equal("2 headers", vm.HttpHeadersDisplay);

        // TCP Inspector formatting
        vm.TcpSocketMode = "Client";
        vm.TcpRemoteHost = "192.168.1.200";
        vm.TcpRemotePort = 5020;
        vm.TcpHeartbeatIntervalSec = 45;
        vm.TcpHeartbeatPayload = "KEEPALIVE";
        vm.TcpRegistrationType = "OnConnect";

        Assert.Equal("Client", vm.TcpModeDisplay);
        Assert.Equal("192.168.1.200", vm.TcpServerDisplay);
        Assert.Equal("Port 5020", vm.TcpPortDisplay);
        Assert.Equal("45s (KEEPALIVE)", vm.TcpHeartbeatDisplay);
        Assert.Equal("OnConnect", vm.TcpRegDisplay);
    }

    /// <summary>
    /// [Axiom 1 / Ground Truth]: Kích hoạt độc lập các dịch vụ MQTT, HTTP, TCP cùng lúc và lưu xuống metadata.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioViewModel_IndependentProtocols_CanBeActivatedSimultaneously()
    {
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Enable all three independently
        vm.MqttBrokerConfigVM.IsEnabled = true;
        vm.HttpEnabled = true;
        vm.TcpEnabled = true;

        vm.SaveConfiguration();

        Assert.True(metadata.MqttBrokerEnabled);
        Assert.True(metadata.DtuConfig.HttpSettings.Enabled);
        Assert.True(metadata.DtuConfig.TcpSettings.Enabled);

        // Reload to verify independent persistence
        var reloaded = new DtuGatewayStudioViewModel(metadata);
        Assert.True(reloaded.MqttBrokerConfigVM.IsEnabled);
        Assert.True(reloaded.HttpEnabled);
        Assert.True(reloaded.TcpEnabled);

        // Disable HTTP, keep MQTT and TCP enabled
        reloaded.HttpEnabled = false;
        reloaded.SaveConfiguration();

        Assert.True(metadata.MqttBrokerEnabled);
        Assert.False(metadata.DtuConfig.HttpSettings.Enabled);
        Assert.True(metadata.DtuConfig.TcpSettings.Enabled);
    }

    /// <summary>
    /// [Axiom 2 / Visual Truth]: MqttTelemetryVM và MqttCommandVM được khởi tạo và đồng bộ dữ liệu chuẩn xác với ViewModel chính.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioViewModel_MqttTelemetryAndCommandViewModels_AreBoundAndSynchronized()
    {
        var metadata = new ProjectMetadata
        {
            MqttTopic = "factory/telemetry/sensors",
            MqttSubTopic = "factory/commands/valves",
            MqttQos = 1,
            MqttSubQos = 2
        };
        var tagCatalog = new TagCatalogViewModel();
        var vm = new DtuGatewayStudioViewModel(metadata, tagCatalog);

        Assert.NotNull(vm.MqttTelemetryVM);
        Assert.NotNull(vm.MqttCommandVM);

        Assert.Equal("factory/telemetry/sensors", vm.MqttTelemetryVM.Node.Topic);
        Assert.Equal(1, vm.MqttTelemetryVM.Node.Qos);

        Assert.Equal("factory/commands/valves", vm.MqttCommandVM.Node.Topic);
        Assert.Equal(2, vm.MqttCommandVM.Node.Qos);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_HttpViewModel_IsBoundAndSynchronizedWithMetadata()
    {
        var metadata = new ProjectMetadata();
        metadata.DtuConfig.HttpSettings.Url = "https://custom-iot.example.com/api/v1/telemetry";
        metadata.DtuConfig.HttpSettings.Method = "PUT";
        metadata.DtuConfig.HttpSettings.TimeoutSec = 15;
        metadata.DtuConfig.HttpSettings.Headers = "Authorization: Bearer test-token";
        metadata.DtuConfig.HttpSettings.TriggerMode = "Periodic";
        metadata.DtuConfig.HttpSettings.PeriodicIntervalSec = 20;

        var tagCatalog = new TagCatalogViewModel();
        var vm = new DtuGatewayStudioViewModel(metadata, tagCatalog);

        Assert.NotNull(vm.HttpVM);
        Assert.NotNull(vm.HttpNode);
        Assert.Equal("https://custom-iot.example.com/api/v1/telemetry", vm.HttpVM.Url);
        Assert.Equal("PUT", vm.HttpVM.Method);
        Assert.Equal(15, vm.HttpVM.TimeoutSec);
        Assert.Equal("Authorization: Bearer test-token", vm.HttpVM.CustomHeaders);
        Assert.Equal("https://custom-iot.example.com/api/v1/telemetry", vm.HttpEndpointDisplay);

        // Edit via HttpVM
        vm.HttpVM.Url = "https://new-endpoint.io/data";
        Assert.Equal("https://new-endpoint.io/data", vm.HttpEndpointDisplay);

        vm.SaveToMetadata();
        Assert.Equal("https://new-endpoint.io/data", metadata.DtuConfig.HttpSettings.Url);
    }

    [Fact]
    public void DtuGatewayStudioViewModel_TcpViewModel_IsBoundAndSynchronizedWithMetadata()
    {
        var metadata = new ProjectMetadata();
        metadata.DtuConfig.TcpSettings.RemoteHost = "10.0.0.50";
        metadata.DtuConfig.TcpSettings.RemotePort = 9000;
        metadata.DtuConfig.TcpSettings.ReconnectDelaySec = 12;
        metadata.DtuConfig.TcpSettings.HeartbeatIntervalSec = 45;
        metadata.DtuConfig.TcpSettings.HeartbeatPayload = "KEEPALIVE";
        metadata.DtuConfig.TcpSettings.RegistrationType = "OnConnect";
        metadata.DtuConfig.TcpSettings.RegistrationPayload = "GW_01";

        var tagCatalog = new TagCatalogViewModel();
        var vm = new DtuGatewayStudioViewModel(metadata, tagCatalog);

        Assert.NotNull(vm.TcpVM);
        Assert.NotNull(vm.TcpNode);
        Assert.Equal("10.0.0.50", vm.TcpVM.RemoteHost);
        Assert.Equal(9000, vm.TcpVM.RemotePort);
        Assert.Equal(12, vm.TcpVM.ReconnectDelaySec);
        Assert.Equal(45, vm.TcpVM.HeartbeatIntervalSec);
        Assert.Equal("KEEPALIVE", vm.TcpVM.HeartbeatPayload);
        Assert.Equal("10.0.0.50", vm.TcpServerDisplay);
        Assert.Equal("Port 9000", vm.TcpPortDisplay);

        // Edit via TcpVM
        vm.TcpVM.RemoteHost = "10.0.0.99";
        vm.TcpVM.RemotePort = 7777;
        Assert.Equal("10.0.0.99", vm.TcpServerDisplay);
        Assert.Equal("Port 7777", vm.TcpPortDisplay);

        vm.SaveToMetadata();
        Assert.Equal("10.0.0.99", metadata.DtuConfig.TcpSettings.RemoteHost);
        Assert.Equal(7777, metadata.DtuConfig.TcpSettings.RemotePort);
    }

    [Fact]
    public void SaveConfigurationCommand_WhenExecuted_SavesToMetadataAndTriggersNotifyConfigurationCommitted()
    {
        // Arrange
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);
        bool committedInvoked = false;
        vm.NotifyConfigurationCommitted = () => committedInvoked = true;

        vm.PublishIntervalSec = 45;

        // Act - Execute via WPF RelayCommand
        Assert.NotNull(vm.SaveConfigurationCommand);
        Assert.True(vm.SaveConfigurationCommand.CanExecute(null));
        vm.SaveConfigurationCommand.Execute(null);

        // Assert
        Assert.True(committedInvoked);
        Assert.Equal(45, metadata.DtuConfig.PublishIntervalSec);
        Assert.False(string.IsNullOrWhiteSpace(vm.DeployStatusMessage));
        Assert.Contains(DateTime.Now.ToString("HH:mm"), vm.DeployStatusMessage);
        Assert.True(vm.DeployStatusMessage.Contains("Đã áp dụng cấu hình") || vm.DeployStatusMessage.Contains("Configuration committed"));
    }

    [Fact]
    public void SaveConfigurationCommand_BilingualFeedback_DisplaysClearStatusInVietnameseAndEnglish()
    {
        // Arrange
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        // 1. Vietnamese test
        LocalizationService.Instance.CurrentLanguage = "vi-VN";
        vm.SaveConfigurationCommand.Execute(null);
        Assert.Contains("Đã áp dụng cấu hình dự án — sẵn sàng nạp thiết bị", vm.DeployStatusMessage);

        // 2. English test
        LocalizationService.Instance.CurrentLanguage = "en-US";
        vm.SaveConfigurationCommand.Execute(null);
        Assert.Contains("Configuration committed to project — ready to deploy", vm.DeployStatusMessage);

        // Restore default
        LocalizationService.Instance.CurrentLanguage = "vi-VN";
    }

    [Fact]
    public void SaveConfigurationCommand_WithModbusPortConfigVM_PreservesMultiPortConfigurationsWithoutClobbering()
    {
        // Arrange
        var metadata = new ProjectMetadata();
        var tagCatalog = new TagCatalogViewModel();
        var vm = new DtuGatewayStudioViewModel(metadata, tagCatalog);
        bool committedInvoked = false;
        vm.NotifyConfigurationCommitted = () => committedInvoked = true;

        // Configure Port 1 (PortIndex 0)
        vm.ModbusPortConfigVM.SelectedPort = vm.ModbusPortConfigVM.Ports[0];
        vm.ModbusPortConfigVM.SelectedPort.BaudRate = 19200;
        vm.ModbusPortConfigVM.SelectedPort.Parity = "Even";

        // Act
        vm.SaveConfigurationCommand.Execute(null);

        // Assert
        Assert.True(committedInvoked);
        Assert.Equal((uint)19200, metadata.ModbusPorts[0].BaudRate);
        Assert.Equal("Even", metadata.ModbusPorts[0].Parity);
        Assert.Equal((uint)19200, vm.BaudRate);
        Assert.Equal("Even", vm.Parity);
    }
}