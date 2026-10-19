using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

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
        using var rsa = System.Security.Cryptography.RSA.Create(2048);
        var req = new System.Security.Cryptography.X509Certificates.CertificateRequest(
            "CN=SPX Test Root CA, O=SPX Automation",
            rsa,
            System.Security.Cryptography.HashAlgorithmName.SHA256,
            System.Security.Cryptography.RSASignaturePadding.Pkcs1);
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
}

