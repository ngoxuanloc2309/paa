namespace SimplePLC.Application.Tests;

using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using Xunit;

/// <summary>
/// Kiểm thử toàn diện dịch vụ CommDiagnosticsService (Tier 2 Forensics) theo chuẩn IEC 61508-3.
/// </summary>
public sealed class CommDiagnosticsServiceTests
{
    private readonly CommDiagnosticsService _service = new();

    // ==========================================
    // 1. HEALTHY & STANDALONE PATHS
    // ==========================================

    [Fact]
    public void Analyze_WhenSysCommOkIsTrue_ReturnsHealthyResult()
    {
        // Act
        var result = _service.Analyze(sysCommOk: true);

        // Assert
        Assert.True(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.Healthy, result.Category);
        Assert.Equal("COMM_OK", result.Code);
        Assert.False(string.IsNullOrWhiteSpace(result.MessageVi));
        Assert.False(string.IsNullOrWhiteSpace(result.RecommendationVi));
    }

    [Fact]
    public void Analyze_WhenDeviceIsStandalone_ReturnsOfflineStandaloneResult()
    {
        // Act: Thiết bị chạy độc lập ở tủ, sysCommOk = false
        var result = _service.Analyze(sysCommOk: false, isStandaloneDevice: true);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.OfflineStandalone, result.Category);
        Assert.Equal("OFFLINE_STANDALONE", result.Code);
        Assert.Contains("độc lập", result.MessageVi);
    }

    // ==========================================
    // 2. WI-FI SUBSYSTEM ROOT CAUSES
    // ==========================================

    [Fact]
    public void Analyze_WhenWifiAuthFailed_ReturnsWifiAuthFailedResult()
    {
        // Arrange
        var wifiConfig = new WifiConfigDto
        {
            Enabled = true,
            Ssid = "Factory_AP",
            ConnectionStatus = ModbusRegisterMap.WifiConnAuthFailed
        };

        // Act
        var result = _service.Analyze(sysCommOk: false, wifiConfig: wifiConfig);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.WifiAuthFailed, result.Category);
        Assert.Equal("WIFI_AUTH_FAILED", result.Code);
        Assert.Contains("mật khẩu", result.MessageVi);
    }

    [Fact]
    public void Analyze_WhenWifiSsidNotFound_ReturnsWifiSsidNotFoundResult()
    {
        // Arrange
        var wifiConfig = new WifiConfigDto
        {
            Enabled = true,
            Ssid = "Line1_Router",
            ConnectionStatus = ModbusRegisterMap.WifiConnSsidNotFound
        };

        // Act
        var result = _service.Analyze(sysCommOk: false, wifiConfig: wifiConfig);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.WifiSsidNotFound, result.Category);
        Assert.Equal("WIFI_SSID_NOT_FOUND", result.Code);
        Assert.Contains("Line1_Router", result.MessageVi);
    }

    [Fact]
    public void Analyze_WhenWifiDisconnected_ReturnsWifiDisconnectedResult()
    {
        // Arrange
        var wifiConfig = new WifiConfigDto
        {
            Enabled = true,
            ConnectionStatus = ModbusRegisterMap.WifiConnDisconnected
        };

        // Act
        var result = _service.Analyze(sysCommOk: false, wifiConfig: wifiConfig);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.WifiDisconnected, result.Category);
        Assert.Equal("WIFI_DISCONNECTED", result.Code);
    }

    [Fact]
    public void Analyze_WhenWifiRssiCriticallyLow_ReturnsWifiLowSignalResult()
    {
        // Arrange: Đã có IP nhưng sóng quá yếu (-88 dBm < -80 dBm)
        var wifiConfig = new WifiConfigDto
        {
            Enabled = true,
            ConnectionStatus = ModbusRegisterMap.WifiConnGotIp,
            RssiDbm = -88
        };

        // Act
        var result = _service.Analyze(sysCommOk: false, wifiConfig: wifiConfig);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.WifiLowSignal, result.Category);
        Assert.Equal("WIFI_LOW_SIGNAL", result.Code);
        Assert.Contains("-88", result.MessageVi);
    }

    // ==========================================
    // 3. MQTT CLOUD SUBSYSTEM ROOT CAUSES
    // ==========================================

    [Fact]
    public void Analyze_WhenMqttDnsFail_ReturnsMqttDnsFailedResult()
    {
        // Arrange
        var mqttConfig = new MqttBrokerConfigDto { Enabled = true, Host = "broker.hivemq.com" };

        // Act: Mã lỗi 1 = SPLC_MQTT_ERR_DNS_FAIL
        var result = _service.Analyze(sysCommOk: false, mqttBrokerConfig: mqttConfig, mqttLastErrorCode: 1);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.MqttDnsFailed, result.Category);
        Assert.Equal("MQTT_DNS_FAILED", result.Code);
        Assert.Contains("Internet", result.MessageVi);
    }

    [Fact]
    public void Analyze_WhenMqttTcpReset_ReturnsMqttTcpConnectFailedResult()
    {
        // Arrange
        var mqttConfig = new MqttBrokerConfigDto { Enabled = true, Host = "192.168.1.100", Port = 1883 };

        // Act: Mã lỗi 2 = SPLC_MQTT_ERR_TCP_RESET
        var result = _service.Analyze(sysCommOk: false, mqttBrokerConfig: mqttConfig, mqttLastErrorCode: 2);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.MqttTcpConnectFailed, result.Category);
        Assert.Equal("MQTT_TCP_CONNECT_FAILED", result.Code);
        Assert.Contains("192.168.1.100:1883", result.MessageVi);
    }

    [Fact]
    public void Analyze_WhenMqttAuthFail_ReturnsMqttAuthRejectedResult()
    {
        // Arrange
        var mqttConfig = new MqttBrokerConfigDto { Enabled = true, Host = "cloud.simpleplc.io" };

        // Act: Mã lỗi 3 = SPLC_MQTT_ERR_AUTH_FAIL
        var result = _service.Analyze(sysCommOk: false, mqttBrokerConfig: mqttConfig, mqttLastErrorCode: 3);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.MqttAuthRejected, result.Category);
        Assert.Equal("MQTT_AUTH_REJECTED", result.Code);
        Assert.Contains("tài khoản", result.MessageVi);
    }

    [Fact]
    public void Analyze_WhenMqttTimeout_ReturnsMqttTimeoutResult()
    {
        // Arrange
        var mqttConfig = new MqttBrokerConfigDto { Enabled = true, Host = "cloud.simpleplc.io" };

        // Act: Mã lỗi 4 = SPLC_MQTT_ERR_TIMEOUT
        var result = _service.Analyze(sysCommOk: false, mqttBrokerConfig: mqttConfig, mqttLastErrorCode: 4);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.MqttTimeout, result.Category);
        Assert.Equal("MQTT_TIMEOUT", result.Code);
        Assert.Contains("Ping", result.MessageVi);
    }

    // ==========================================
    // 4. MODBUS SILENCE WATCHDOG ROOT CAUSES
    // ==========================================

    [Fact]
    public void Analyze_WhenModbusSilenceExceedsTimeout_ReturnsModbusTimeoutResult()
    {
        // Act: 4.5 giây kể từ gói tin Modbus cuối (> 3.0s timeout)
        var result = _service.Analyze(
            sysCommOk: false,
            lastModbusRxElapsed: TimeSpan.FromMilliseconds(4500));

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.ModbusTimeout, result.Category);
        Assert.Equal("MODBUS_TIMEOUT", result.Code);
        Assert.Contains("4.5", result.MessageVi);
    }

    [Fact]
    public void Analyze_WhenNoChannelsActive_ReturnsCommInactiveResult()
    {
        // Act: sysCommOk = false, không cấu hình gì
        var result = _service.Analyze(sysCommOk: false);

        // Assert
        Assert.False(result.IsHealthy);
        Assert.Equal(CommRootCauseCategory.OfflineStandalone, result.Category);
        Assert.Equal("COMM_INACTIVE", result.Code);
    }
}
