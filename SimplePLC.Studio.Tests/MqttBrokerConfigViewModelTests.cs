using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests cho MqttBrokerConfigViewModel kiểm tra cấu hình MQTT Broker,
/// nạp và giải mã chứng chỉ TLS/SSL, Mutual TLS (mTLS) và tham số nâng cao.
/// </summary>
public class MqttBrokerConfigViewModelTests
{
    [Fact]
    public void Constructor_ShouldPopulateFromProjectMetadata()
    {
        // Arrange
        var meta = new ProjectMetadata
        {
            MqttBrokerEnabled = true,
            MqttBrokerHost = "iot.company.com",
            MqttBrokerPort = 8883,
            MqttClientId = "PLC-EDGE-01",
            MqttUsername = "operator",
            MqttPassword = "secret-password",
            MqttTlsEnabled = true,
            MqttCleanSession = false,
            MqttKeepAliveSec = 45
        };

        // Act
        var vm = new MqttBrokerConfigViewModel(meta);

        // Assert
        Assert.True(vm.IsEnabled);
        Assert.Equal("iot.company.com", vm.Host);
        Assert.Equal(8883, vm.Port);
        Assert.Equal("PLC-EDGE-01", vm.ClientId);
        Assert.Equal("operator", vm.Username);
        Assert.Equal("secret-password", vm.Password);
        Assert.True(vm.UseTls);
        Assert.False(vm.CleanSession);
        Assert.Equal((ushort)45, vm.KeepAliveSec);
    }

    [Fact]
    public void Constructor_WithEmptyMetadata_ShouldHaveEmptyHost()
    {
        // Arrange
        var meta = new ProjectMetadata();

        // Act
        var vm = new MqttBrokerConfigViewModel(meta);

        // Assert
        Assert.Equal(string.Empty, vm.Host);
        Assert.Equal(1883, vm.Port);
        Assert.Equal(string.Empty, vm.ClientId);
    }

    [Fact]
    public void TlsToggle_ShouldAutoSwitchDefaultPort()
    {
        // Arrange
        var meta = new ProjectMetadata
        {
            MqttBrokerPort = 1883,
            MqttTlsEnabled = false
        };
        var vm = new MqttBrokerConfigViewModel(meta);

        // Act 1: Bật TLS khi port đang 1883 -> Tự chuyển sang 8883
        vm.UseTls = true;
        Assert.Equal(8883, vm.Port);

        // Act 2: Tắt TLS khi port đang 8883 -> Tự chuyển về 1883
        vm.UseTls = false;
        Assert.Equal(1883, vm.Port);

        // Act 3: Nếu là port tùy chỉnh (ví dụ 18883), không tự đổi
        vm.Port = 18883;
        vm.UseTls = true;
        Assert.Equal(18883, vm.Port);
    }

    [Fact]
    public void SaveCommand_ShouldCommitToMetadataAndInvokeRequestClose()
    {
        // Arrange
        var meta = new ProjectMetadata
        {
            MqttBrokerHost = "old.host.com",
            MqttBrokerPort = 1883
        };
        var vm = new MqttBrokerConfigViewModel(meta)
        {
            Host = "new.broker.net",
            Port = 8883,
            ClientId = "NEW-CLIENT",
            Username = "admin",
            Password = "pass",
            UseTls = true,
            CleanSession = true,
            KeepAliveSec = 120,
            IsEnabled = true
        };

        bool closeInvoked = false;
        vm.RequestClose += () => closeInvoked = true;

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.True(closeInvoked);
        Assert.Equal("new.broker.net", meta.MqttBrokerHost);
        Assert.Equal(8883, meta.MqttBrokerPort);
        Assert.Equal("NEW-CLIENT", meta.MqttClientId);
        Assert.Equal("admin", meta.MqttUsername);
        Assert.Equal("pass", meta.MqttPassword);
        Assert.True(meta.MqttTlsEnabled);
        Assert.True(meta.MqttCleanSession);
        Assert.Equal((ushort)120, meta.MqttKeepAliveSec);
        Assert.True(meta.MqttBrokerEnabled);
    }

    [Fact]
    public void CancelCommand_ShouldInvokeRequestCloseWithoutAlteringMetadata()
    {
        // Arrange
        var meta = new ProjectMetadata
        {
            MqttBrokerHost = "original.host.com",
            MqttBrokerPort = 1883
        };
        var vm = new MqttBrokerConfigViewModel(meta)
        {
            Host = "modified.host.com",
            Port = 9999
        };

        bool closeInvoked = false;
        vm.RequestClose += () => closeInvoked = true;

        // Act
        vm.CancelCommand.Execute(null);

        // Assert
        Assert.True(closeInvoked);
        Assert.Equal("original.host.com", meta.MqttBrokerHost);
        Assert.Equal(1883, meta.MqttBrokerPort);
    }

    [Fact]
    public async Task TestConnectionCommand_EmptyHost_ShouldSetError()
    {
        // Arrange
        var meta = new ProjectMetadata();
        var vm = new MqttBrokerConfigViewModel(meta)
        {
            Host = "   "
        };

        // Act
        await vm.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        Assert.False(vm.IsTestSuccess);
        Assert.Equal(SimplePLC.Studio.Services.LocalizationService.Instance["MqttBrokerErrEmptyHost"], vm.TestStatusMessage);
    }

    [Fact]
    public void TlsCertificate_LoadAndClear_ShouldUpdatePropertiesAndCommit()
    {
        // Arrange
        var meta = new ProjectMetadata
        {
            MqttTlsEnabled = true,
            MqttTlsCaCertPath = "C:\\Certs\\test_ca.crt",
            MqttTlsCaCertPem = "-----BEGIN CERTIFICATE-----\nMIID...\n-----END CERTIFICATE-----",
            MqttTlsAllowSelfSigned = true
        };

        // Act 1: Constructor loads metadata
        var vm = new MqttBrokerConfigViewModel(meta);
        Assert.Equal("C:\\Certs\\test_ca.crt", vm.CaCertPath);
        Assert.Equal("-----BEGIN CERTIFICATE-----\nMIID...\n-----END CERTIFICATE-----", vm.CaCertPem);
        Assert.True(vm.AllowSelfSigned);
        Assert.True(vm.HasCaCert);

        // Act 2: Clear certificate
        vm.ClearCaCertCommand.Execute(null);
        Assert.Empty(vm.CaCertPath);
        Assert.Empty(vm.CaCertPem);
        Assert.False(vm.HasCaCert);

        // Act 3: Save commits cleared state to metadata
        vm.SaveCommand.Execute(null);
        Assert.Empty(meta.MqttTlsCaCertPath);
        Assert.Empty(meta.MqttTlsCaCertPem);
    }

    [Fact]
    public void ParseCertPem_ValidX509Pem_ExtractsIssuerAndSha256Fingerprint()
    {
        // Arrange: Generate an authentic self-signed X509 certificate in memory
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=SPX Test Root CA, O=SPX Automation",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(365));
        string pem = cert.ExportCertificatePem();

        var meta = new ProjectMetadata();
        var vm = new MqttBrokerConfigViewModel(meta);

        // Act
        vm.ParseCertPem(pem, "in-memory.crt");

        // Assert
        Assert.True(vm.HasCaCert);
        Assert.Equal("in-memory.crt", vm.CaCertPath);
        Assert.Equal(pem, vm.CaCertPem);
        Assert.Contains("CN=SPX Test Root CA", vm.CaCertIssuer);
        Assert.Contains("CN=SPX Test Root CA", vm.CaCertSubject);
        Assert.False(string.IsNullOrWhiteSpace(vm.CaCertExpiry));
        Assert.False(string.IsNullOrWhiteSpace(vm.CaCertSha256));
        Assert.Contains(":", vm.CaCertSha256); // Valid colon-delimited hex fingerprint
    }

    [Fact]
    public void ParseCertPem_CorruptPem_FallsBackToCustomCertWithoutThrowing()
    {
        // Arrange
        var meta = new ProjectMetadata();
        var vm = new MqttBrokerConfigViewModel(meta);

        // Act
        vm.ParseCertPem("NOT_A_VALID_PEM_OR_CERTIFICATE", "raw_binary.der");

        // Assert: Handles exception gracefully without throwing and falls back to Custom Certificate
        Assert.True(vm.HasCaCert);
        Assert.Equal("raw_binary.der", vm.CaCertPath);
        Assert.Equal("Custom Certificate", vm.CaCertIssuer);
        Assert.Equal("Loaded", vm.CaCertExpiry);
        Assert.Equal("N/A", vm.CaCertSha256);
    }

    [Fact]
    public void Mtls_LoadClientCertAndKey_ShouldExtractSubjectAndCommitToMetadata()
    {
        // Arrange
        using var rsa = RSA.Create(2048);
        var req = new CertificateRequest(
            "CN=PLC-EDGE-CLIENT-01, O=SPX Automation",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var cert = req.CreateSelfSigned(DateTimeOffset.Now.AddDays(-1), DateTimeOffset.Now.AddDays(365));
        string certPem = cert.ExportCertificatePem();
        string keyPem = rsa.ExportPkcs8PrivateKeyPem();

        var meta = new ProjectMetadata();
        var vm = new MqttBrokerConfigViewModel(meta)
        {
            UseTls = true,
            UseMtls = true,
            ClientKeyPassword = "TestPassword"
        };

        // Act 1: Parse Client Cert
        vm.ParseClientCert(certPem, "client.crt");
        vm.ClientKeyPem = keyPem;
        vm.ClientKeyPath = "client.key";
        vm.HasClientKey = true;

        // Assert 1
        Assert.True(vm.HasClientCert);
        Assert.True(vm.HasClientKey);
        Assert.Contains("CN=PLC-EDGE-CLIENT-01", vm.ClientCertSubject);
        Assert.False(string.IsNullOrWhiteSpace(vm.ClientCertSha256));

        // Act 2: Verify CreateClientCertificate
        using var combinedCert = vm.CreateClientCertificate();
        Assert.NotNull(combinedCert);
        Assert.True(combinedCert.HasPrivateKey);

        // Act 3: Save commits to metadata
        vm.SaveCommand.Execute(null);
        Assert.True(meta.MqttTlsMtlsEnabled);
        Assert.Equal("client.crt", meta.MqttTlsClientCertPath);
        Assert.Equal(certPem, meta.MqttTlsClientCertPem);
        Assert.Equal("client.key", meta.MqttTlsClientKeyPath);
        Assert.Equal(keyPem, meta.MqttTlsClientKeyPem);
        Assert.Equal("TestPassword", meta.MqttTlsClientKeyPassword);
    }

    [Fact]
    public void Mtls_ClearClientCertAndKey_ShouldResetProperties()
    {
        // Arrange
        var meta = new ProjectMetadata
        {
            MqttTlsMtlsEnabled = true,
            MqttTlsClientCertPath = "client.crt",
            MqttTlsClientCertPem = "PEM",
            MqttTlsClientKeyPath = "client.key",
            MqttTlsClientKeyPem = "KEY_PEM"
        };
        var vm = new MqttBrokerConfigViewModel(meta);
        Assert.True(vm.UseMtls);
        Assert.True(vm.HasClientKey);

        // Act
        vm.ClearClientCertCommand.Execute(null);
        vm.ClearClientKeyCommand.Execute(null);

        // Assert
        Assert.False(vm.HasClientCert);
        Assert.Empty(vm.ClientCertPath);
        Assert.Empty(vm.ClientCertPem);
        Assert.False(vm.HasClientKey);
        Assert.Empty(vm.ClientKeyPath);
        Assert.Empty(vm.ClientKeyPem);
    }

    [Fact]
    public void AdvancedTlsSettings_ShouldCommitToMetadata()
    {
        // Arrange
        var meta = new ProjectMetadata();
        var vm = new MqttBrokerConfigViewModel(meta)
        {
            UseTls = true,
            IgnoreHostname = true,
            TlsVersionIndex = 2, // TLS 1.3
            SniHostname = "custom-sni.broker.net"
        };

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.True(meta.MqttTlsIgnoreHostname);
        Assert.Equal(2, meta.MqttTlsProtocolVersion);
        Assert.Equal("custom-sni.broker.net", meta.MqttTlsSniHostname);
    }

    [Fact]
    public void OnIsEnabledChanged_WhenFalse_DisablesTestConnectionCommand()
    {
        // Arrange
        var meta = new ProjectMetadata
        {
            MqttBrokerEnabled = true,
            MqttBrokerHost = "broker.hivemq.com",
            MqttBrokerPort = 1883
        };
        var vm = new MqttBrokerConfigViewModel(meta);
        Assert.True(vm.CanTestConnection);
        Assert.True(vm.TestConnectionCommand.CanExecute(null));

        // Act: Bỏ tích công tắc tổng Master Switch
        vm.IsEnabled = false;

        // Assert: Nút kiểm tra kết nối phải bị vô hiệu hóa
        Assert.False(vm.CanTestConnection);
        Assert.False(vm.TestConnectionCommand.CanExecute(null));

        // Act: Bật lại công tắc tổng
        vm.IsEnabled = true;

        // Assert: Nút kiểm tra kết nối được kích hoạt lại
        Assert.True(vm.CanTestConnection);
        Assert.True(vm.TestConnectionCommand.CanExecute(null));
    }

    [Fact]
    public void Save_WhenDisabled_PersistsDisabledStateToMetadata()
    {
        // Arrange
        var meta = new ProjectMetadata { MqttBrokerEnabled = true };
        var vm = new MqttBrokerConfigViewModel(meta);

        // Act
        vm.IsEnabled = false;
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.False(meta.MqttBrokerEnabled);
    }

    [Fact]
    public void WillPayload_WhenContainsQuotesOrBackslashes_SanitizesAutomatically()
    {
        // Arrange
        var meta = new ProjectMetadata();
        var vm = new MqttBrokerConfigViewModel(meta);

        // Act
        vm.WillPayload = "{\"status\":\"offline\\\"}";

        // Assert - Double quotes and backslashes must be stripped
        Assert.Equal("{status:offline}", vm.WillPayload);
    }

    [Fact]
    public void WillTopic_WhenEmptyAndWillEnabled_SetsHasWillTopicErrorAndDisablesSave()
    {
        // Arrange
        var meta = new ProjectMetadata
        {
            MqttBrokerHost = "10.0.0.1",
            MqttBrokerPort = 1883
        };
        var vm = new MqttBrokerConfigViewModel(meta)
        {
            Host = "10.0.0.1",
            WillEnabled = true,
            WillTopic = "   "
        };

        // Assert
        Assert.True(vm.HasWillTopicError);
        Assert.False(string.IsNullOrWhiteSpace(vm.WillTopicErrorMessage));
        Assert.False(vm.CanSave);
        Assert.False(vm.SaveCommand.CanExecute(null));

        // Act: Operator enters a valid topic
        vm.WillTopic = "devices/node1/lwt";

        // Assert: Error clears, CanSave restored
        Assert.False(vm.HasWillTopicError);
        Assert.True(vm.CanSave);
        Assert.True(vm.SaveCommand.CanExecute(null));
    }

    [Fact]
    public void SaveCommand_WhenWillEnabled_PersistsLwtConfigToMetadata()
    {
        // Arrange
        var meta = new ProjectMetadata
        {
            MqttBrokerHost = "10.0.0.1"
        };
        var vm = new MqttBrokerConfigViewModel(meta)
        {
            Host = "10.0.0.1",
            WillEnabled = true,
            WillTopic = "telemetry/{device}/status",
            WillPayload = "offline",
            WillQos = 1,
            WillRetain = true
        };

        // Act
        vm.SaveCommand.Execute(null);

        // Assert
        Assert.True(meta.MqttWillEnabled);
        Assert.Equal("telemetry/{device}/status", meta.MqttWillTopic);
        Assert.Equal("offline", meta.MqttWillPayload);
        Assert.Equal((byte)1, meta.MqttWillQos);
        Assert.True(meta.MqttWillRetain);
    }
}
