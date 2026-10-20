using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Application.Abstractions;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class WifiConfigViewModelTests
{
    private sealed class FakeWifiClient : IWifiClient
    {
        public WifiConfigDto CurrentConfig { get; set; } = new();
        public WifiScanControlDto ScanControl { get; set; } = new();
        public List<WifiApInfoDto> ScanResults { get; set; } = new();

        public ushort LastTriggeredCommand { get; private set; }
        public bool WriteConfigCalled { get; private set; }

        public Task<WifiConfigDto> ReadWifiConfigAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
            => Task.FromResult(CurrentConfig);

        public Task WriteWifiConfigAsync(byte slaveId, WifiConfigDto config, CancellationToken cancellationToken = default)
        {
            WriteConfigCalled = true;
            CurrentConfig.Enabled = config.Enabled;
            CurrentConfig.Ssid = config.Ssid;
            CurrentConfig.Password = config.Password;
            CurrentConfig.IsDhcp = config.IsDhcp;
            CurrentConfig.AutoReconnect = config.AutoReconnect;
            CurrentConfig.StaticIp = config.StaticIp;
            CurrentConfig.StaticSubnet = config.StaticSubnet;
            CurrentConfig.StaticGateway = config.StaticGateway;
            CurrentConfig.StaticDns = config.StaticDns;
            return Task.CompletedTask;
        }

        public Task TriggerWifiCommandAsync(byte slaveId, ushort command, CancellationToken cancellationToken = default)
        {
            LastTriggeredCommand = command;
            return Task.CompletedTask;
        }

        public Task<WifiScanControlDto> ReadWifiScanStatusAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
            => Task.FromResult(ScanControl);

        public Task<IReadOnlyList<WifiApInfoDto>> ReadWifiScanResultsAsync(byte slaveId = 1, ushort count = 8, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WifiApInfoDto>>(ScanResults);
    }

    [Fact]
    public void Constructor_ShouldPopulateFromProjectMetadata()
    {
        // Arrange
        var meta = new ProjectMetadata
        {
            WifiEnabled = true,
            WifiAutoReconnect = false,
            WifiSsid = "Factory_IoT_Net",
            WifiPassword = "SuperSecretPassword123!",
            WifiDhcpEnabled = false,
            WifiStaticIp = "10.0.0.100",
            WifiSubnetMask = "255.255.0.0",
            WifiGateway = "10.0.0.1",
            WifiDns = "1.1.1.1"
        };

        // Act
        using var vm = new WifiConfigViewModel(meta);

        // Assert
        Assert.True(vm.IsEnabled);
        Assert.False(vm.AutoReconnect);
        Assert.Equal("Factory_IoT_Net", vm.Ssid);
        Assert.Equal("SuperSecretPassword123!", vm.Password);
        Assert.False(vm.IsDhcp);
        Assert.Equal("10.0.0.100", vm.StaticIp);
        Assert.Equal("255.255.0.0", vm.StaticSubnet);
        Assert.Equal("10.0.0.1", vm.StaticGateway);
        Assert.Equal("1.1.1.1", vm.StaticDns);
    }

    [Fact]
    public void Constructor_DefaultMetadata_ShouldHaveSensibleDefaults()
    {
        // Arrange
        var meta = new ProjectMetadata();

        // Act
        using var vm = new WifiConfigViewModel(meta);

        // Assert
        Assert.False(vm.IsEnabled);
        Assert.True(vm.AutoReconnect);
        Assert.Equal(string.Empty, vm.Ssid);
        Assert.Equal(string.Empty, vm.Password);
        Assert.True(vm.IsDhcp);
        Assert.Equal("192.168.1.50", vm.StaticIp);
        Assert.Equal("255.255.255.0", vm.StaticSubnet);
        Assert.Equal("192.168.1.1", vm.StaticGateway);
        Assert.Equal("8.8.8.8", vm.StaticDns);
    }

    [Fact]
    public void TogglePasswordVisibility_ShouldToggleState()
    {
        // Arrange
        var meta = new ProjectMetadata();
        using var vm = new WifiConfigViewModel(meta);

        // Act & Assert
        Assert.False(vm.IsPasswordVisible);
        vm.TogglePasswordVisibilityCommand.Execute(null);
        Assert.True(vm.IsPasswordVisible);
        vm.TogglePasswordVisibilityCommand.Execute(null);
        Assert.False(vm.IsPasswordVisible);
    }

    [Fact]
    public void SelectedAp_Change_ShouldAutoFillSsid()
    {
        // Arrange
        var meta = new ProjectMetadata();
        using var vm = new WifiConfigViewModel(meta);

        var ap = new WifiApInfoDto
        {
            Ssid = "Plant_Floor_5G",
            RssiDbm = -62,
            Channel = 36,
            AuthMode = ModbusRegisterMap.WifiAuthWpa2Psk
        };

        // Act
        vm.SelectedAp = ap;

        // Assert
        Assert.Equal("Plant_Floor_5G", vm.Ssid);
    }

    [Fact]
    public async Task SaveCommand_WhenEnabledWithEmptySsid_ShouldSetErrorAndNotClose()
    {
        // Arrange
        var meta = new ProjectMetadata();
        using var vm = new WifiConfigViewModel(meta)
        {
            IsEnabled = true,
            Ssid = "   "
        };

        bool closed = false;
        vm.RequestClose += () => closed = true;

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.False(closed);
        Assert.False(string.IsNullOrEmpty(vm.ErrorMessage));
    }

    [Fact]
    public async Task SaveCommand_Valid_ShouldCommitToMetadataAndInvokeRequestClose()
    {
        // Arrange
        var meta = new ProjectMetadata();
        using var vm = new WifiConfigViewModel(meta)
        {
            IsEnabled = true,
            AutoReconnect = true,
            Ssid = "HQ_Warehouse",
            Password = "warehouse-key",
            IsDhcp = false,
            StaticIp = "192.168.10.55",
            StaticSubnet = "255.255.255.0",
            StaticGateway = "192.168.10.1",
            StaticDns = "8.8.4.4"
        };

        bool closed = false;
        vm.RequestClose += () => closed = true;

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        Assert.True(closed);
        Assert.True(vm.DialogResult);
        Assert.True(meta.WifiEnabled);
        Assert.True(meta.WifiAutoReconnect);
        Assert.Equal("HQ_Warehouse", meta.WifiSsid);
        Assert.Equal("warehouse-key", meta.WifiPassword);
        Assert.False(meta.WifiDhcpEnabled);
        Assert.Equal("192.168.10.55", meta.WifiStaticIp);
        Assert.Equal("255.255.255.0", meta.WifiSubnetMask);
        Assert.Equal("192.168.10.1", meta.WifiGateway);
        Assert.Equal("8.8.4.4", meta.WifiDns);
    }

    [Fact]
    public void CancelCommand_ShouldSetDialogResultFalseAndClose()
    {
        // Arrange
        var meta = new ProjectMetadata();
        using var vm = new WifiConfigViewModel(meta);

        bool closed = false;
        vm.RequestClose += () => closed = true;

        // Act
        vm.CancelCommand.Execute(null);

        // Assert
        Assert.True(closed);
        Assert.False(vm.DialogResult);
    }

    [Fact]
    public async Task ScanAsync_WithFakeWifiClient_ShouldPopulateAvailableAps()
    {
        // Arrange
        var meta = new ProjectMetadata();
        var fakeClient = new FakeWifiClient
        {
            ScanControl = new WifiScanControlDto
            {
                ScanState = ModbusRegisterMap.WifiScanStateCompleted,
                ApFoundCount = 2
            },
            ScanResults = new List<WifiApInfoDto>
            {
                new() { Ssid = "AP_Line1", RssiDbm = -55, Channel = 1, AuthMode = ModbusRegisterMap.WifiAuthWpa2Psk },
                new() { Ssid = "AP_Line2", RssiDbm = -75, Channel = 6, AuthMode = ModbusRegisterMap.WifiAuthWpa3Psk }
            }
        };

        using var vm = new WifiConfigViewModel(meta, fakeClient)
        {
            IsDeviceConnected = true
        };

        // Act
        await vm.ScanCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(ModbusRegisterMap.WifiCmdStartScan, fakeClient.LastTriggeredCommand);
        Assert.Equal(2, vm.AvailableAps.Count);
        Assert.Equal("AP_Line1", vm.AvailableAps[0].Ssid);
        Assert.Equal("AP_Line2", vm.AvailableAps[1].Ssid);
        Assert.False(vm.IsScanning);
    }

    [Fact]
    public async Task ConnectAsync_WithValidSsid_ShouldCallWriteConfigAndTriggerConnect()
    {
        // Arrange
        var meta = new ProjectMetadata();
        var fakeClient = new FakeWifiClient
        {
            CurrentConfig = new WifiConfigDto
            {
                ConnectionStatus = ModbusRegisterMap.WifiConnConnecting,
                RssiDbm = -60,
                CurrentIp = "192.168.1.150",
                MacAddress = "00:80:E1:12:34:56"
            }
        };

        using var vm = new WifiConfigViewModel(meta, fakeClient)
        {
            IsDeviceConnected = true,
            Ssid = "Plant_Mesh",
            Password = "Password!99"
        };

        // Act
        await vm.ConnectCommand.ExecuteAsync(null);

        // Assert
        Assert.True(fakeClient.WriteConfigCalled);
        Assert.Equal("Plant_Mesh", fakeClient.CurrentConfig.Ssid);
        Assert.Equal(ModbusRegisterMap.WifiCmdConnect, fakeClient.LastTriggeredCommand);
        Assert.Equal(ModbusRegisterMap.WifiConnConnecting, vm.ConnectionStatus);
        Assert.Equal("192.168.1.150", vm.CurrentIp);
        Assert.Equal("00:80:E1:12:34:56", vm.MacAddress);
    }

    [Fact]
    public async Task DisconnectAsync_ShouldTriggerDisconnect()
    {
        // Arrange
        var meta = new ProjectMetadata();
        var fakeClient = new FakeWifiClient
        {
            CurrentConfig = new WifiConfigDto
            {
                ConnectionStatus = ModbusRegisterMap.WifiConnDisconnected,
                RssiDbm = -100,
                CurrentIp = "0.0.0.0",
                MacAddress = "00:80:E1:12:34:56"
            }
        };

        using var vm = new WifiConfigViewModel(meta, fakeClient)
        {
            IsDeviceConnected = true
        };

        // Act
        await vm.DisconnectCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(ModbusRegisterMap.WifiCmdDisconnect, fakeClient.LastTriggeredCommand);
        Assert.Equal(ModbusRegisterMap.WifiConnDisconnected, vm.ConnectionStatus);
    }

    [Theory]
    [InlineData(-50, 100)]
    [InlineData(-40, 100)]
    [InlineData(-75, 50)]
    [InlineData(-100, 0)]
    [InlineData(-110, 0)]
    [InlineData(0, 0)]
    public async Task RefreshTelemetryDirectAsync_ShouldComputeSignalQualityCorrectly(sbyte rssi, int expectedPercent)
    {
        // Arrange
        var meta = new ProjectMetadata();
        var fakeClient = new FakeWifiClient
        {
            CurrentConfig = new WifiConfigDto
            {
                ConnectionStatus = ModbusRegisterMap.WifiConnGotIp,
                RssiDbm = rssi,
                CurrentIp = "192.168.1.200",
                MacAddress = "11:22:33:44:55:66"
            }
        };

        using var vm = new WifiConfigViewModel(meta, fakeClient);

        // Act
        await vm.RefreshTelemetryDirectAsync();

        // Assert
        Assert.Equal(expectedPercent, vm.SignalQualityPercent);
        Assert.Equal(ModbusRegisterMap.WifiConnGotIp, vm.ConnectionStatus);
        Assert.Equal("192.168.1.200", vm.CurrentIp);
        Assert.Equal("11:22:33:44:55:66", vm.MacAddress);
    }

    [Fact]
    public async Task SwitchToScanAndStartScan_ShouldSwitchTabAndTriggerScan()
    {
        // Arrange
        var meta = new ProjectMetadata();
        var fakeClient = new FakeWifiClient
        {
            ScanControl = new WifiScanControlDto
            {
                ScanState = ModbusRegisterMap.WifiScanStateCompleted,
                ApFoundCount = 1
            },
            ScanResults = new List<WifiApInfoDto>
            {
                new() { Ssid = "Robotics_Cell_1", RssiDbm = -48, Channel = 11, AuthMode = ModbusRegisterMap.WifiAuthWpa2Psk }
            }
        };

        using var vm = new WifiConfigViewModel(meta, fakeClient)
        {
            IsDeviceConnected = true,
            SelectedTabIndex = 0
        };

        // Act
        await vm.SwitchToScanAndStartScanCommand.ExecuteAsync(null);

        // Assert
        Assert.Equal(1, vm.SelectedTabIndex);
        Assert.Single(vm.AvailableAps);
        Assert.Equal("Robotics_Cell_1", vm.AvailableAps[0].Ssid);
    }

    [Fact]
    public void ApplySelectedApAndReturn_ShouldSetSsidAndSwitchTabToZero()
    {
        // Arrange
        var meta = new ProjectMetadata();
        using var vm = new WifiConfigViewModel(meta)
        {
            SelectedTabIndex = 1
        };

        var ap = new WifiApInfoDto
        {
            Ssid = "Packaging_Line_WiFi",
            RssiDbm = -60,
            Channel = 6,
            AuthMode = ModbusRegisterMap.WifiAuthWpa2Psk
        };

        vm.SelectedAp = ap;

        // Act
        vm.ApplySelectedApAndReturnCommand.Execute(null);

        // Assert
        Assert.Equal("Packaging_Line_WiFi", vm.Ssid);
        Assert.Equal(0, vm.SelectedTabIndex);
    }
}
