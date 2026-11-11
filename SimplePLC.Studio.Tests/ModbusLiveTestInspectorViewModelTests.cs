using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.Services.Modbus;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Fake IComPortDiscoveryService phục vụ kiểm thử ViewModel.
/// </summary>
public sealed class FakePortDiscoveryService : IComPortDiscoveryService
{
    public IReadOnlyList<string> Ports { get; set; } = new List<string> { "COM3", "COM4" };
    public IReadOnlyList<string> GetAvailablePorts() => Ports;
}

/// <summary>
/// Fake IModbusLiveProbeService phục vụ kiểm thử ModbusLiveTestInspectorViewModel.
/// </summary>
public sealed class FakeLiveProbeService : IModbusLiveProbeService
{
    public bool IsPortOpen { get; set; }
    public string? CurrentPortName { get; set; }
    public ModbusProbeResult NextResult { get; set; } = ModbusProbeResult.Success(25.4, "254 (0x00FE)", 28);

    public Task OpenPortAsync(string portName, int baudRate = 9600, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One, CancellationToken cancellationToken = default)
    {
        IsPortOpen = true;
        CurrentPortName = portName;
        return Task.CompletedTask;
    }

    public Task ClosePortAsync(CancellationToken cancellationToken = default)
    {
        IsPortOpen = false;
        CurrentPortName = null;
        return Task.CompletedTask;
    }

    public Task<ModbusProbeResult> ProbeAsync(byte slaveId, byte functionCode, ushort address, ModbusDataType dataType, ModbusEndianness endianness, double scale, int timeoutMs = 1000, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(NextResult);
    }

    public ValueTask DisposeAsync()
    {
        IsPortOpen = false;
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Unit tests cho <see cref="ModbusLiveTestInspectorViewModel"/>.
/// </summary>
public class ModbusLiveTestInspectorViewModelTests
{
    [Fact]
    public void RefreshPorts_PopulatesAvailablePortsAndSelectsFirst()
    {
        var discovery = new FakePortDiscoveryService { Ports = new[] { "COM1", "COM2" } };
        var probeService = new FakeLiveProbeService();

        var vm = new ModbusLiveTestInspectorViewModel(probeService, discovery);

        Assert.Equal(2, vm.AvailablePorts.Count);
        Assert.Equal("COM1", vm.SelectedPort);
    }

    [Fact]
    public async Task ConnectAndDisconnect_TogglesPortStateProperly()
    {
        var discovery = new FakePortDiscoveryService();
        var probeService = new FakeLiveProbeService();
        var vm = new ModbusLiveTestInspectorViewModel(probeService, discovery);

        // Act: Connect
        await vm.ConnectPortAsync();
        Assert.True(vm.IsPortConnected);
        Assert.True(probeService.IsPortOpen);

        // Act: Disconnect
        await vm.DisconnectPortAsync();
        Assert.False(vm.IsPortConnected);
        Assert.False(probeService.IsPortOpen);
    }

    [Fact]
    public async Task ReadOnce_WhenChannelConfigured_UpdatesTelemetryReadout()
    {
        var discovery = new FakePortDiscoveryService();
        var probeService = new FakeLiveProbeService
        {
            IsPortOpen = true,
            NextResult = ModbusProbeResult.Success(50.5, "505 (0x01F9)", 35)
        };

        var vm = new ModbusLiveTestInspectorViewModel(probeService, discovery);
        vm.IsPortConnected = true;

        var channel = new ModbusMasterChannelItemViewModel
        {
            SlaveId = 2,
            FunctionCode = 3,
            Address = 40001,
            DataType = ModbusDataType.Real,
            Scale = 0.1
        };
        vm.TargetChannel = channel;

        // Act: Read Once
        await vm.ReadOnceAsync();

        // Assert
        Assert.Equal("50.5", vm.LiveScaledValue);
        Assert.Equal("505 (0x01F9)", vm.LiveRawValue);
        Assert.Contains("35 ms", vm.ResponseLatency);
        Assert.Equal(LiveProbeStatusState.Success, vm.StatusState);
    }

    [Fact]
    public async Task ReadOnce_WhenTimeoutFault_UpdatesDangerStatusAndHint()
    {
        var discovery = new FakePortDiscoveryService();
        var probeService = new FakeLiveProbeService
        {
            IsPortOpen = true,
            NextResult = ModbusProbeResult.Failure(ModbusProbeFaultKind.Timeout, "Timeout", "Kiểm tra dây A/B", latencyMs: 1000)
        };

        var vm = new ModbusLiveTestInspectorViewModel(probeService, discovery);
        vm.IsPortConnected = true;

        var channel = new ModbusMasterChannelItemViewModel { SlaveId = 1, FunctionCode = 3, Address = 40001 };
        vm.TargetChannel = channel;

        // Act
        await vm.ReadOnceAsync();

        // Assert
        Assert.Equal("—", vm.LiveRawValue);
        Assert.Equal(LiveProbeStatusState.Danger, vm.StatusState);
        Assert.Contains("A+/B-", vm.TroubleshootingHint);
    }

    [Fact]
    public void UpdateDeviceConnection_UpdatesConnectionBadgeAndState()
    {
        var discovery = new FakePortDiscoveryService();
        var probeService = new FakeLiveProbeService();
        var vm = new ModbusLiveTestInspectorViewModel(probeService, discovery);

        // Act: Update device connected
        vm.UpdateDeviceConnection(true, "COM3", "SYNX-DTU-485", isDeployed: true);

        Assert.True(vm.IsDeviceConnected);
        Assert.Equal("COM3", vm.DevicePortName);
        Assert.Equal("SYNX-DTU-485", vm.DeviceModelName);
        Assert.Contains("COM3", vm.DeviceConnectionBadge);
        Assert.Contains("SYNX-DTU-485", vm.DeviceConnectionBadge);
        Assert.Equal(LiveProbeStatusState.Idle, vm.StatusState);

        // Act: Update device offline
        vm.UpdateDeviceConnection(false, null, null);
        Assert.False(vm.IsDeviceConnected);
        Assert.Equal(LiveProbeStatusState.Idle, vm.StatusState);
    }

    [Fact]
    public async Task ReadOnce_WhenDeviceOffline_SetsWarningAndOfflineHint()
    {
        var discovery = new FakePortDiscoveryService();
        var probeService = new FakeLiveProbeService();
        var vm = new ModbusLiveTestInspectorViewModel(probeService, discovery);

        vm.UpdateDeviceConnection(false, null, null);
        var channel = new ModbusMasterChannelItemViewModel { SlaveId = 1, FunctionCode = 3, Address = 40001 };
        vm.TargetChannel = channel;

        // Act
        await vm.ReadOnceAsync();

        // Assert
        Assert.Equal(LiveProbeStatusState.Warning, vm.StatusState);
        Assert.False(string.IsNullOrWhiteSpace(vm.TroubleshootingHint));
    }

    [Fact]
    public void TargetSummary_FormatsChannelPropertiesCorrectly()
    {
        var discovery = new FakePortDiscoveryService();
        var probeService = new FakeLiveProbeService();
        var vm = new ModbusLiveTestInspectorViewModel(probeService, discovery);

        // When null
        vm.TargetChannel = null;
        Assert.False(vm.HasTargetChannel);

        // When assigned
        vm.TargetChannel = new ModbusMasterChannelItemViewModel
        {
            SlaveId = 3,
            FunctionCode = 4,
            Address = 30005
        };

        Assert.True(vm.HasTargetChannel);
        Assert.Contains("Slave 3", vm.TargetSummary);
        Assert.Contains("FC04", vm.TargetSummary);
        Assert.Contains("30005", vm.TargetSummary);
    }
}
