using System;
using System.Linq;
using System.Windows;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests cho Trình Hướng Dẫn Cấu Hình Nhanh DTU Gateway (Multi-Step Quick Setup Wizard).
/// Đảm bảo tính toàn vẹn của chuỗi chuyển đổi các bước (Step 1 -> 2 -> 3),
/// cơ chế xác thực từng bước, và khả năng tự động sinh đồ thị FBD nối dây trên Canvas.
/// </summary>
public sealed class DtuQuickSetupWizardViewModelTests
{
    private readonly ProjectMetadata _metadata;
    private readonly TagCatalogViewModel _tagCatalog;
    private readonly LogicEditorViewModel _logicEditor;
    private readonly DtuGatewayStudioViewModel _wizardVm;

    public DtuQuickSetupWizardViewModelTests()
    {
        _metadata = new ProjectMetadata();
        _tagCatalog = new TagCatalogViewModel();
        _logicEditor = new LogicEditorViewModel(_tagCatalog);
        _wizardVm = new DtuGatewayStudioViewModel(_metadata, _tagCatalog, _logicEditor);
    }

    [Fact]
    public void Wizard_InitializesAtStep1_DefaultToModbusMaster()
    {
        // Assert
        Assert.Equal(1, _wizardVm.CurrentStepIndex);
        Assert.True(_wizardVm.IsStep1);
        Assert.False(_wizardVm.IsStep2);
        Assert.False(_wizardVm.IsStep3);
        Assert.False(_wizardVm.CanGoBack);
        Assert.Equal("ModbusMaster", _wizardVm.SerialMode);
        Assert.NotEmpty(_wizardVm.FastRegisters);
        Assert.True(_wizardVm.CanGoNext);
    }

    [Fact]
    public void Wizard_Step1_RequiresFastRegisters_WhenModbusMaster()
    {
        // Act: Xóa toàn bộ danh sách thanh ghi
        _wizardVm.FastRegisters.Clear();

        // Assert: Không được phép bấm Next khi chưa có thanh ghi đọc nào
        Assert.False(_wizardVm.CanGoNext);

        // Act: Thêm lại 1 thanh ghi
        _wizardVm.AddFastRegister();
        Assert.True(_wizardVm.CanGoNext);

        // Act 2: Chuyển sang Modbus Slave (không bắt buộc bảng đọc ngoài)
        _wizardVm.FastRegisters.Clear();
        _wizardVm.SerialMode = "ModbusSlave";
        Assert.True(_wizardVm.CanGoNext);
    }

    [Fact]
    public void Wizard_StepNavigation_BackAndNext_TransitionsCorrectly()
    {
        // Arrange
        Assert.Equal(1, _wizardVm.CurrentStepIndex);

        // Act: Bấm Next sang bước 2
        _wizardVm.NextStep();
        Assert.Equal(2, _wizardVm.CurrentStepIndex);
        Assert.True(_wizardVm.IsStep2);
        Assert.True(_wizardVm.CanGoBack);
        Assert.False(_wizardVm.IsLastStep);

        // Act: Bấm Next sang bước 3
        _wizardVm.NextStep();
        Assert.Equal(3, _wizardVm.CurrentStepIndex);
        Assert.True(_wizardVm.IsStep3);
        Assert.True(_wizardVm.IsLastStep);
        Assert.False(_wizardVm.CanGoNext); // Bước cuối không còn Next

        // Act: Bấm Quay lại
        _wizardVm.PreviousStep();
        Assert.Equal(2, _wizardVm.CurrentStepIndex);
        Assert.True(_wizardVm.IsStep2);

        _wizardVm.PreviousStep();
        Assert.Equal(1, _wizardVm.CurrentStepIndex);
        Assert.False(_wizardVm.CanGoBack);
    }

    [Fact]
    public void Wizard_Step2_ProtocolSwitching_UpdatesUplinkState()
    {
        // Act: MQTT
        _wizardVm.UplinkMode = "Mqtt";
        _wizardVm.MqttHost = "broker.test.io";
        _wizardVm.MqttPort = 1883;
        Assert.Equal("broker.test.io", _wizardVm.UplinkTargetHost);

        // Act: HTTP
        _wizardVm.UplinkMode = "Http";
        _wizardVm.HttpUrl = "https://api.test.io/v1/telemetry";
        Assert.Equal("https://api.test.io/v1/telemetry", _wizardVm.UplinkTargetHost);

        // Act: TCP Client
        _wizardVm.UplinkMode = "TcpClient";
        _wizardVm.TcpRemoteHost = "10.0.0.50";
        Assert.Equal("10.0.0.50", _wizardVm.UplinkTargetHost);
    }

    [Fact]
    public void Wizard_Step3_SummaryTopology_ReflectsConfiguration()
    {
        // Arrange
        _wizardVm.SerialMode = "ModbusMaster";
        _wizardVm.FastRegisters.Clear();
        _wizardVm.FastRegisters.Add(new DtuFastRegisterItem(1, 40001, ModbusDataType.Real, "TEMP"));
        _wizardVm.FastRegisters.Add(new DtuFastRegisterItem(1, 40003, ModbusDataType.Real, "HUMID"));
        _wizardVm.UplinkMode = "Mqtt";
        _wizardVm.MqttHost = "mqtt.factory.net";
        _wizardVm.MqttPort = 8883;
        _wizardVm.MqttPublishTopic = "factory/env";

        // Assert
        Assert.Contains("Modbus Master", _wizardVm.SummaryTopologyText);
        Assert.Contains("2 regs", _wizardVm.SummaryTopologyText);
        Assert.Contains("mqtt.factory.net:8883", _wizardVm.SummaryTopologyText);
        Assert.Contains("factory/env", _wizardVm.SummaryTopologyText);
    }

    [Fact]
    public void Wizard_Finish_GeneratesFbdNodes_AndWiresSuccessfully()
    {
        // Arrange
        _logicEditor.Nodes.Clear();
        _wizardVm.FastRegisters.Clear();
        _wizardVm.FastRegisters.Add(new DtuFastRegisterItem(1, 40001, ModbusDataType.Real, "PRESSURE"));
        _wizardVm.UplinkMode = "Mqtt";
        _wizardVm.MqttPublishTopic = "factory/boiler/pressure";
        _wizardVm.AutoGenerateFbd = true;

        bool closeInvoked = false;
        _wizardVm.RequestCloseDialog = (success) => closeInvoked = success;

        // Act
        _wizardVm.FinishWizard();

        // Assert: Cửa sổ thông báo đóng thành công
        Assert.True(closeInvoked);

        // Assert: FBD Canvas xuất hiện 2 khối (MB_READ và MQTT_PUB)
        Assert.Equal(2, _logicEditor.Nodes.Count);
        var mbNode = _logicEditor.Nodes.OfType<ModbusReadNodeViewModel>().FirstOrDefault();
        var mqttNode = _logicEditor.Nodes.OfType<MqttPublishNodeViewModel>().FirstOrDefault();

        Assert.NotNull(mbNode);
        Assert.NotNull(mqttNode);
        Assert.Equal(1, mbNode.SlaveId);
        Assert.Equal(40001, mbNode.StartAddress);
        Assert.Single(mbNode.Pins);
        Assert.Equal("PRESSURE", mbNode.Pins[0].Label);

        Assert.Equal("factory/boiler/pressure", mqttNode.Topic);
        Assert.Single(mqttNode.Fields);
        Assert.Equal("PRESSURE", mqttNode.Fields[0].Key);

        // Assert: Nối dây tự động thành công (MB_READ Output nối sang MQTT Input)
        Assert.NotEmpty(_logicEditor.Connections);
    }

    [Fact]
    public void Sidebar_SelectSection_SwitchesPanelsCorrectly()
    {
        // Act & Assert Section 0: RS-485
        _wizardVm.SelectSection(0);
        Assert.Equal(0, _wizardVm.SelectedSectionIndex);
        Assert.True(_wizardVm.IsSectionRs485);
        Assert.False(_wizardVm.IsSectionWifi);
        Assert.False(_wizardVm.IsSectionUplink);

        // Act & Assert Section 1: Wi-Fi
        _wizardVm.SelectSection(1);
        Assert.Equal(1, _wizardVm.SelectedSectionIndex);
        Assert.False(_wizardVm.IsSectionRs485);
        Assert.True(_wizardVm.IsSectionWifi);

        // Act & Assert Section 2: MQTT Client
        _wizardVm.SelectSection(2);
        Assert.Equal(2, _wizardVm.SelectedSectionIndex);
        Assert.True(_wizardVm.IsSectionMqtt);
        Assert.True(_wizardVm.IsSectionUplink);

        // Act & Assert Section 3: HTTP Client
        _wizardVm.SelectSection(3);
        Assert.Equal(3, _wizardVm.SelectedSectionIndex);
        Assert.True(_wizardVm.IsSectionHttp);
        Assert.True(_wizardVm.IsSectionUplink);

        // Act & Assert Section 4: TCP Client
        _wizardVm.SelectSection(4);
        Assert.Equal(4, _wizardVm.SelectedSectionIndex);
        Assert.True(_wizardVm.IsSectionTcp);
        Assert.True(_wizardVm.IsSectionUplink);


    }

    [Fact]
    public void WifiConfig_SyncsTwoWay_WithProjectMetadata()
    {
        // Arrange
        _wizardVm.WifiEnabled = true;
        _wizardVm.WifiSsid = "SmartFactory_AP";
        _wizardVm.WifiPassword = "P@ssw0rdIndustrial";
        _wizardVm.WifiDhcpEnabled = false;
        _wizardVm.WifiStaticIp = "192.168.10.150";

        // Act
        _wizardVm.SaveConfiguration();

        // Assert
        Assert.True(_metadata.WifiEnabled);
        Assert.Equal("SmartFactory_AP", _metadata.WifiSsid);
        Assert.Equal("P@ssw0rdIndustrial", _metadata.WifiPassword);
        Assert.False(_metadata.WifiDhcpEnabled);
        Assert.Equal("192.168.10.150", _metadata.WifiStaticIp);
        Assert.Equal("Active", _wizardVm.WifiStatusBadge);
        Assert.Equal("SmartFactory_AP", _wizardVm.WifiSsidDisplay);
    }

    [Fact]
    public void UplinkMode_SetUplinkMode_UpdatesProtocolFlagsAndBadges()
    {
        // Act 1: Set to HTTP
        _wizardVm.SetUplinkMode("Http");
        Assert.True(_wizardVm.IsUplinkHttp);
        Assert.False(_wizardVm.IsUplinkMqtt);
        Assert.False(_wizardVm.IsUplinkTcp);
        Assert.Equal("HTTP REST", _wizardVm.UplinkStatusBadge);

        // Act 2: Set to TCP
        _wizardVm.SetUplinkMode("TcpClient");
        Assert.True(_wizardVm.IsUplinkTcp);
        Assert.Equal("TCP Socket", _wizardVm.UplinkStatusBadge);

        // Act 3: Set to MQTT
        _wizardVm.SetUplinkMode("Mqtt");
        Assert.True(_wizardVm.IsUplinkMqtt);
        Assert.Equal("MQTT", _wizardVm.UplinkStatusBadge);
    }

    [Fact]
    public void FastRegisters_SupportFunctionCode_AndAutoRegisterTags()
    {
        // Arrange
        _wizardVm.FastRegisters.Clear();
        _wizardVm.FastRegisters.Add(new DtuFastRegisterItem(1, 30001, ModbusDataType.Real, "TEMP_INPUT", "Analog Temp", ModbusMasterFunctionCode.ReadInputRegisters));

        // Act
        _wizardVm.SaveConfiguration();

        // Assert: FunctionCode được giữ nguyên
        Assert.Equal(ModbusMasterFunctionCode.ReadInputRegisters, _wizardVm.FastRegisters[0].FunctionCode);

        // Assert: TagCatalog đã được tự động đăng ký
        Assert.Contains(_tagCatalog.RegisterTags, t => t.Name == "TEMP_INPUT");
    }

    [Fact]
    public void DtuGatewayStudioVM_SyncsFullModbusUartParams_ToMetadata()
    {
        // Arrange
        _wizardVm.PortName = "RS-485 #2";
        _wizardVm.BaudRate = 115200;
        _wizardVm.DataBits = 8;
        _wizardVm.Parity = "Even";
        _wizardVm.StopBits = 2;
        _wizardVm.ResponseTimeoutMs = 500;
        _wizardVm.RetryCount = 3;
        _wizardVm.InterFrameDelayMs = 35;
        _wizardVm.SlaveId = 5;

        // Act
        _wizardVm.SaveConfiguration();

        // Assert
        Assert.NotNull(_metadata.ModbusPorts);
        Assert.NotEmpty(_metadata.ModbusPorts);
        var p0 = _metadata.ModbusPorts[0];
        Assert.Equal("RS-485 #2", p0.PortName);
        Assert.Equal(115200u, p0.BaudRate);
        Assert.Equal(8, p0.DataBits);
        Assert.Equal("Even", p0.Parity);
        Assert.Equal(2, p0.StopBits);
        Assert.Equal(500, p0.ResponseTimeoutMs);
        Assert.Equal(3, p0.RetryCount);
        Assert.Equal(35, p0.InterFrameDelayMs);
        Assert.Equal(5, p0.SlaveId);
    }

    [Fact]
    public void DtuGatewayStudioVM_SyncsMqttTlsAndLwt_ToMetadata()
    {
        // Arrange
        _wizardVm.MqttUseTls = true;
        _wizardVm.MqttTlsCaCertPath = @"C:\Certs\ca.crt";
        _wizardVm.MqttTlsAllowSelfSigned = true;
        _wizardVm.MqttWillEnabled = true;
        _wizardVm.MqttWillTopic = "factory/emergency";
        _wizardVm.MqttWillPayload = "{\"alert\":\"offline\"}";
        _wizardVm.MqttWillQos = 2;
        _wizardVm.MqttWillRetain = true;

        // Act
        _wizardVm.SaveConfiguration();

        // Assert
        Assert.True(_metadata.MqttTlsEnabled);
        Assert.Equal(@"C:\Certs\ca.crt", _metadata.MqttTlsCaCertPath);
        Assert.True(_metadata.MqttTlsAllowSelfSigned);
        Assert.True(_metadata.MqttWillEnabled);
        Assert.Equal("factory/emergency", _metadata.MqttWillTopic);
        Assert.Equal("{\"alert\":\"offline\"}", _metadata.MqttWillPayload);
        Assert.Equal(2, _metadata.MqttWillQos);
        Assert.True(_metadata.MqttWillRetain);
    }

    [Fact]
    public void DtuGatewayStudioVM_SlaveMapEntries_AddAndRemove_WorksCorrectly()
    {
        // Arrange
        _wizardVm.SlaveMapEntries.Clear();
        Assert.Empty(_wizardVm.SlaveMapEntries);

        // Act 1: Add entry
        _wizardVm.AddSlaveMapEntry();
        Assert.Single(_wizardVm.SlaveMapEntries);
        var entry = _wizardVm.SlaveMapEntries[0];
        Assert.Equal(40001, entry.ModbusAddress);

        // Act 2: Save configuration
        _wizardVm.SaveConfiguration();
        Assert.Single(_metadata.ModbusSlaveMapEntries);
        Assert.Equal(40001, _metadata.ModbusSlaveMapEntries[0].ModbusAddress);

        // Act 3: Remove entry
        _wizardVm.RemoveSlaveMapEntry(entry);
        Assert.Empty(_wizardVm.SlaveMapEntries);
    }

    [Fact]
    public void DtuGatewayStudioVM_ScanNetworks_PopulatesAvailableSsids()
    {
        // Act
        _wizardVm.ScanNetworks();

        // Assert
        Assert.NotEmpty(_wizardVm.AvailableSsids);
        Assert.Contains(_wizardVm.AvailableSsids, s => s.Contains("SynaptiX_Lab"));
        Assert.True(_wizardVm.ConnectionStateText.Contains("Đã tìm thấy") || _wizardVm.ConnectionStateText.Contains("Found"));
    }

    [Fact]
    public async Task DtuGatewayStudioVM_TopHardwareActionBar_ConnectionBadgeAndUpload_WorksCorrectly()
    {
        // Arrange
        bool uploadCalled = false;
        _wizardVm.UploadFromDeviceRequested = () =>
        {
            uploadCalled = true;
            return Task.CompletedTask;
        };

        // Act 1: Disconnected state
        _wizardVm.UpdateHardwareConnectionState(false, string.Empty, "SYNX-DTU-485");
        Assert.False(_wizardVm.IsHardwareConnected);
        Assert.True(_wizardVm.HardwareConnectionStatusBadge.Contains("Chưa kết nối") || _wizardVm.HardwareConnectionStatusBadge.Contains("Disconnected"));

        // Act 2: Connected state
        _wizardVm.UpdateHardwareConnectionState(true, "COM3", "SYNX-DTU-485");
        Assert.True(_wizardVm.IsHardwareConnected);
        Assert.Equal("COM3", _wizardVm.HardwarePortName);
        Assert.Contains("COM3", _wizardVm.HardwareConnectionStatusBadge);
        Assert.True(_wizardVm.HardwareConnectionStatusBadge.Contains("ĐÃ KẾT NỐI") || _wizardVm.HardwareConnectionStatusBadge.Contains("CONNECTED"));
        Assert.Contains("SYNX-DTU-485", _wizardVm.HardwareConnectionStatusBadge);

        // Act 3: Upload command
        await _wizardVm.UploadFromDeviceAsync();
        Assert.True(uploadCalled);
    }

    [Fact]
    public void DtuGatewayStudioVM_TopHardwareActionBar_OpenQuickWizard_TriggersCallback()
    {
        // Arrange
        bool wizardOpened = false;
        _wizardVm.OpenQuickWizardRequested = () => { wizardOpened = true; };

        // Act
        _wizardVm.OpenQuickWizard();

        // Assert
        Assert.True(wizardOpened);
    }

    [Fact]
    public void DtuGatewayStudioVM_LiveIntelligence_CalculatesScanCycleAndBusLoad()
    {
        // Arrange
        _wizardVm.FastRegisters.Clear();
        Assert.Equal(0, _wizardVm.EstimatedScanCycleMs);
        Assert.True(_wizardVm.EstimatedBusLoadText.Contains("0%") || _wizardVm.EstimatedBusLoadText.Contains("Idle"));

        // Act: Add 2 registers
        _wizardVm.InterFrameDelayMs = 20;
        _wizardVm.FastRegisters.Add(new DtuFastRegisterItem(1, 40001, ModbusDataType.Real, "TEMP", "Temp"));
        _wizardVm.FastRegisters.Add(new DtuFastRegisterItem(1, 40003, ModbusDataType.Real, "PRESS", "Pressure"));

        // Assert
        Assert.True(_wizardVm.EstimatedScanCycleMs > 0);
        Assert.NotEmpty(_wizardVm.EstimatedBusLoadText);
    }

    [Fact]
    public void DtuGatewayStudioVM_LiveIntelligence_CalculatesMonthlyDataQuota()
    {
        // Arrange
        _wizardVm.PublishIntervalSec = 10;
        _wizardVm.UpdateSummaryAndPreview();

        // Act & Assert
        Assert.True(_wizardVm.EstimatedMonthlyDataMb > 0);
    }

    [Fact]
    public void DtuGatewayStudioVM_CopyMacAddress_ExecutesWithoutError()
    {
        // Act & Assert
        _wizardVm.HardwareMacAddress = "24:6F:28:8A:4C:12";
        _wizardVm.CopyMacAddress();
        Assert.NotEmpty(_wizardVm.HardwareMacAddress);
    }

    [Fact]
    public void DtuGatewayStudioVM_OperatingRole_SwitchesBetweenMasterAndSlaveContext()
    {
        // Default is ModbusMaster
        _wizardVm.SetSerialMode("ModbusMaster");
        Assert.True(_wizardVm.IsMasterMode);
        Assert.False(_wizardVm.IsSlaveMode);

        // Switch to ModbusSlave
        _wizardVm.SetSerialMode("ModbusSlave");
        Assert.False(_wizardVm.IsMasterMode);
        Assert.True(_wizardVm.IsSlaveMode);
        Assert.Equal("Slave", _wizardVm.Rs485StatusBadge);

        // Switch back to ModbusMaster
        _wizardVm.SetSerialMode("ModbusMaster");
        Assert.True(_wizardVm.IsMasterMode);
        Assert.False(_wizardVm.IsSlaveMode);
    }

    [Fact]
    public void LocalizationService_PureBilingualKeys_HaveZeroHybridStrings()
    {
        var loc = LocalizationService.Instance;
        string originalLang = loc.CurrentLanguage;

        try
        {
            // Verify keys in Vietnamese
            loc.CurrentLanguage = "vi";
            Assert.Equal("Tên cổng", loc["LblPortName"]);
            Assert.Equal("Tốc độ truyền (Baud)", loc["LblBaudRate"]);
            Assert.Equal("Kiểm tra chẵn lẻ", loc["LblParity"]);
            Assert.Equal("Bit dữ liệu", loc["LblDataBits"]);
            Assert.Equal("Bit dừng", loc["LblStopBits"]);
            Assert.Equal("Thời gian chờ (ms)", loc["LblTimeout"]);
            Assert.Equal("Số lần thử lại", loc["LblRetries"]);
            Assert.Equal("Khoảng nghỉ khung (ms)", loc["LblInterFrameDelay"]);
            Assert.DoesNotContain("Port Name", loc["LblPortName"]);
            Assert.DoesNotContain("Baudrate", loc["LblBaudRate"]);

            // Verify keys in English
            loc.CurrentLanguage = "en";
            Assert.Equal("Port Name", loc["LblPortName"]);
            Assert.Equal("Baud Rate", loc["LblBaudRate"]);
            Assert.Equal("Parity", loc["LblParity"]);
            Assert.Equal("Data Bits", loc["LblDataBits"]);
            Assert.Equal("Stop Bits", loc["LblStopBits"]);
            Assert.Equal("Response Timeout (ms)", loc["LblTimeout"]);
            Assert.Equal("Retry Count", loc["LblRetries"]);
            Assert.Equal("Inter-frame Delay (ms)", loc["LblInterFrameDelay"]);
            Assert.DoesNotContain("Tên cổng", loc["LblPortName"]);
        }
        finally
        {
            loc.CurrentLanguage = originalLang;
        }
    }
}
