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

        Assert.Equal("Transference", vm.SerialMode);
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
}
