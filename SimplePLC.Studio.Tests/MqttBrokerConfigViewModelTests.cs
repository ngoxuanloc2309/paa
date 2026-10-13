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
}
