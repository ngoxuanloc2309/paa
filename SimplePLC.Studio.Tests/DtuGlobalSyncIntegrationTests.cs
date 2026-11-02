using System;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests kiểm tra cơ chế đồng bộ dữ liệu hai chiều (Bi-directional SSoT Sync)
/// giữa DTU Gateway Studio và các hộp thoại cấu hình hệ thống (MQTT Broker & Modbus Port).
/// </summary>
public sealed class DtuGlobalSyncIntegrationTests
{
    /// <summary>
    /// Kiểm tra khi kỹ sư bấm nút "Chứng chỉ & Nâng cao...", DTU ViewModel phải lưu các giá trị
    /// đang chỉnh sửa trên giao diện vào ProjectMetadata trước khi kích hoạt callback mở Dialog.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioVM_OpenAdvancedMqttConfig_SavesPendingEditsAndInvokesCallback()
    {
        // Arrange
        var metadata = new ProjectMetadata
        {
            MqttBrokerHost = "192.168.1.10",
            MqttBrokerPort = 1883,
            MqttTlsEnabled = false
        };
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Kỹ sư chỉnh sửa trên UI nhưng chưa bấm nút "Lưu"
        vm.MqttHost = "10.0.0.50";
        vm.MqttPort = 8883;
        vm.MqttUseTls = true;

        bool callbackInvoked = false;
        vm.OpenMqttBrokerConfigRequested = () =>
        {
            callbackInvoked = true;
            // Xác nhận tại thời điểm mở dialog, metadata đã được cập nhật giá trị mới nhất
            Assert.Equal("10.0.0.50", metadata.MqttBrokerHost);
            Assert.Equal(8883, metadata.MqttBrokerPort);
            Assert.True(metadata.MqttTlsEnabled);
        };

        // Act
        vm.OpenAdvancedMqttConfigCommand.Execute(null);

        // Assert
        Assert.True(callbackInvoked);
    }

    /// <summary>
    /// Kiểm tra sau khi Dialog cấu hình MQTT đóng (sau khi callback kết thúc),
    /// DTU ViewModel phải tự động nạp lại các thay đổi mới nhất từ ProjectMetadata.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioVM_OpenAdvancedMqttConfig_ReloadsMetadataAfterCallbackCompletes()
    {
        // Arrange
        var metadata = new ProjectMetadata
        {
            MqttBrokerHost = "192.168.1.10",
            MqttClientId = "OLD_CLIENT_ID",
            MqttTlsEnabled = false
        };
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Giả lập callback mở dialog, người dùng sửa ClientId và bật TLS mTLS trong dialog
        vm.OpenMqttBrokerConfigRequested = () =>
        {
            metadata.MqttClientId = "SPLC_ADVANCED_CLIENT_ID";
            metadata.MqttTlsEnabled = true;
            metadata.MqttBrokerPort = 8883;
        };

        // Act
        vm.OpenAdvancedMqttConfigCommand.Execute(null);

        // Assert: VM phải nạp lại thuộc tính mới từ metadata
        Assert.Equal("SPLC_ADVANCED_CLIENT_ID", vm.MqttClientId);
        Assert.True(vm.MqttUseTls);
        Assert.Equal(8883, vm.MqttPort);
    }

    /// <summary>
    /// Kiểm tra khi hộp thoại cấu hình Modbus Port thay đổi các tham số cổng RS-485,
    /// phương thức ReloadFromMetadata() của DTU phản ánh chính xác các giá trị mới.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioVM_ReloadFromMetadata_ReflectsExternalModbusChanges()
    {
        // Arrange
        var metadata = new ProjectMetadata();
        Assert.NotEmpty(metadata.ModbusPorts);
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Giả lập hộp thoại Modbus Port cập nhật cấu hình cổng 0
        metadata.ModbusPorts[0].BaudRate = 115200;
        metadata.ModbusPorts[0].DataBits = 8;
        metadata.ModbusPorts[0].Parity = "Even";
        metadata.ModbusPorts[0].StopBits = 2;
        metadata.ModbusPorts[0].SlaveId = 5;
        metadata.ModbusPorts[0].ResponseTimeoutMs = 500;

        // Act
        vm.ReloadFromMetadata();

        // Assert
        Assert.Equal(115200u, vm.BaudRate);
        Assert.Equal(8, vm.DataBits);
        Assert.Equal("Even", vm.Parity);
        Assert.Equal(2, vm.StopBits);
        Assert.Equal(5, vm.SlaveId);
        Assert.Equal(500, vm.ResponseTimeoutMs);
    }

    /// <summary>
    /// Kiểm tra khi hộp thoại cấu hình MQTT Broker thay đổi các tham số broker,
    /// phương thức ReloadFromMetadata() của DTU phản ánh chính xác các giá trị mới.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioVM_ReloadFromMetadata_ReflectsExternalMqttChanges()
    {
        // Arrange
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Giả lập hộp thoại MQTT Broker thay đổi thông tin
        metadata.MqttBrokerHost = "broker.emqx.io";
        metadata.MqttBrokerPort = 8883;
        metadata.MqttTlsEnabled = true;
        metadata.MqttUsername = "operator01";
        metadata.MqttKeepAliveSec = 120;
        metadata.MqttWillEnabled = true;
        metadata.MqttWillTopic = "factory/alerts/fatal";

        // Act
        vm.ReloadFromMetadata();

        // Assert
        Assert.Equal("broker.emqx.io", vm.MqttHost);
        Assert.Equal(8883, vm.MqttPort);
        Assert.True(vm.MqttUseTls);
        Assert.Equal("operator01", vm.MqttUsername);
        Assert.Equal((ushort)120, vm.MqttKeepAlive);
        Assert.True(vm.MqttWillEnabled);
        Assert.Equal("factory/alerts/fatal", vm.MqttWillTopic);
    }

    /// <summary>
    /// Kiểm tra khi kỹ sư bật/tắt checkbox TLS, cổng MQTT tự động chuyển đổi giữa 1883 và 8883.
    /// Giữ nguyên nếu người dùng sử dụng cổng tùy biến khác 1883/8883.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioVM_OnMqttUseTlsChanged_AutoSwitchesPortBetween1883And8883()
    {
        // Arrange
        var metadata = new ProjectMetadata { MqttBrokerPort = 1883, MqttTlsEnabled = false };
        var vm = new DtuGatewayStudioViewModel(metadata);
        Assert.Equal(1883, vm.MqttPort);

        // Act 1: Bật TLS -> Cổng tự động nhảy sang 8883
        vm.MqttUseTls = true;
        Assert.Equal(8883, vm.MqttPort);

        // Act 2: Tắt TLS -> Cổng tự động quay về 1883
        vm.MqttUseTls = false;
        Assert.Equal(1883, vm.MqttPort);

        // Act 3: Đặt cổng tùy biến (vd: 18884) -> Bật TLS không được ghi đè
        vm.MqttPort = 18884;
        vm.MqttUseTls = true;
        Assert.Equal(18884, vm.MqttPort);
    }

    /// <summary>
    /// Kiểm tra toàn bộ các thuộc tính chứng chỉ TLS, mTLS x509 trên DTU ViewModel
    /// được lưu chính xác và trọn vẹn vào ProjectMetadata khi gọi SaveToMetadata().
    /// </summary>
    [Fact]
    public void DtuGatewayStudioVM_TlsInlineProperties_PersistToMetadata()
    {
        // Arrange
        var metadata = new ProjectMetadata();
        var vm = new DtuGatewayStudioViewModel(metadata);

        // Act: Thiết lập các thông số TLS inline trên giao diện DTU
        vm.MqttUseTls = true;
        vm.MqttTlsCaCertPath = "C:/certs/ca.crt";
        vm.MqttTlsCaCertPem = "-----BEGIN CERTIFICATE-----\nMIIB...-----END CERTIFICATE-----";
        vm.MqttTlsAllowSelfSigned = true;
        vm.MqttTlsIgnoreHostname = true;
        vm.MqttTlsProtocolVersion = 2; // TLS 1.3
        vm.MqttTlsSniHostname = "broker.plant.net";
        vm.MqttTlsMtlsEnabled = true;
        vm.MqttTlsClientCertPath = "C:/certs/client.crt";
        vm.MqttTlsClientCertPem = "-----BEGIN CERTIFICATE-----\nMIIC...-----END CERTIFICATE-----";
        vm.MqttTlsClientKeyPath = "C:/certs/client.key";
        vm.MqttTlsClientKeyPem = "-----BEGIN RSA PRIVATE KEY-----\nMIIE...-----END RSA PRIVATE KEY-----";
        vm.MqttTlsClientKeyPassword = "PrivatePassword123";

        vm.SaveToMetadata();

        // Assert: Metadata nhận chính xác 100%
        Assert.True(metadata.MqttTlsEnabled);
        Assert.Equal("C:/certs/ca.crt", metadata.MqttTlsCaCertPath);
        Assert.Equal("-----BEGIN CERTIFICATE-----\nMIIB...-----END CERTIFICATE-----", metadata.MqttTlsCaCertPem);
        Assert.True(metadata.MqttTlsAllowSelfSigned);
        Assert.True(metadata.MqttTlsIgnoreHostname);
        Assert.Equal(2, metadata.MqttTlsProtocolVersion);
        Assert.Equal("broker.plant.net", metadata.MqttTlsSniHostname);
        Assert.True(metadata.MqttTlsMtlsEnabled);
        Assert.Equal("C:/certs/client.crt", metadata.MqttTlsClientCertPath);
        Assert.Equal("-----BEGIN CERTIFICATE-----\nMIIC...-----END CERTIFICATE-----", metadata.MqttTlsClientCertPem);
        Assert.Equal("C:/certs/client.key", metadata.MqttTlsClientKeyPath);
        Assert.Equal("-----BEGIN RSA PRIVATE KEY-----\nMIIE...-----END RSA PRIVATE KEY-----", metadata.MqttTlsClientKeyPem);
        Assert.Equal("PrivatePassword123", metadata.MqttTlsClientKeyPassword);
    }

    /// <summary>
    /// Kiểm tra khi ProjectMetadata có sẵn các chứng chỉ TLS/mTLS, phương thức
    /// ReloadFromMetadata() nạp đầy đủ vào các thuộc tính hiển thị của DTU ViewModel.
    /// </summary>
    [Fact]
    public void DtuGatewayStudioVM_ReloadFromMetadata_LoadsAllTlsAndMtlsFields()
    {
        // Arrange
        var metadata = new ProjectMetadata
        {
            MqttTlsEnabled = true,
            MqttBrokerPort = 8883,
            MqttTlsCaCertPath = "D:/factory/ca.crt",
            MqttTlsCaCertPem = "PEM_CA_DATA",
            MqttTlsAllowSelfSigned = true,
            MqttTlsIgnoreHostname = true,
            MqttTlsProtocolVersion = 1,
            MqttTlsSniHostname = "iot.factory.local",
            MqttTlsMtlsEnabled = true,
            MqttTlsClientCertPath = "D:/factory/client.crt",
            MqttTlsClientCertPem = "PEM_CLIENT_DATA",
            MqttTlsClientKeyPath = "D:/factory/client.key",
            MqttTlsClientKeyPem = "PEM_KEY_DATA",
            MqttTlsClientKeyPassword = "PfxPassword!"
        };

        var vm = new DtuGatewayStudioViewModel(metadata);

        // Act
        vm.ReloadFromMetadata();

        // Assert
        Assert.True(vm.MqttUseTls);
        Assert.Equal(8883, vm.MqttPort);
        Assert.Equal("D:/factory/ca.crt", vm.MqttTlsCaCertPath);
        Assert.Equal("PEM_CA_DATA", vm.MqttTlsCaCertPem);
        Assert.True(vm.MqttTlsAllowSelfSigned);
        Assert.True(vm.MqttTlsIgnoreHostname);
        Assert.Equal(1, vm.MqttTlsProtocolVersion);
        Assert.Equal("iot.factory.local", vm.MqttTlsSniHostname);
        Assert.True(vm.MqttTlsMtlsEnabled);
        Assert.Equal("D:/factory/client.crt", vm.MqttTlsClientCertPath);
        Assert.Equal("PEM_CLIENT_DATA", vm.MqttTlsClientCertPem);
        Assert.Equal("D:/factory/client.key", vm.MqttTlsClientKeyPath);
        Assert.Equal("PEM_KEY_DATA", vm.MqttTlsClientKeyPem);
        Assert.Equal("PfxPassword!", vm.MqttTlsClientKeyPassword);
    }
}
