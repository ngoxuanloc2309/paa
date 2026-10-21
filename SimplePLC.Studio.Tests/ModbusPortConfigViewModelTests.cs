using System.Collections.Generic;
using System.Linq;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Kiểm thử độc lập cho ViewModel quản lý cấu hình Đa cổng RS-485 Modbus Master (Component 3).
/// </summary>
public sealed class ModbusPortConfigViewModelTests
{
    [Fact]
    public void Constructor_InitializesFourPortsFromMetadata()
    {
        var meta = new ProjectMetadata();
        var vm = new ModbusPortConfigViewModel(meta);

        Assert.Equal(4, vm.Ports.Count);
        Assert.NotNull(vm.SelectedPort);
        Assert.Equal((byte)0, vm.SelectedPort.PortIndex);

        // Port 1 default: 9600-8-N-1
        Assert.Equal(9600u, vm.Ports[0].BaudRate);
        Assert.Equal("None", vm.Ports[0].Parity);
        Assert.True(vm.Ports[0].Enabled);

        // Port 2 default: 19200-8-E-1
        Assert.Equal(19200u, vm.Ports[1].BaudRate);
        Assert.Equal("Even", vm.Ports[1].Parity);
        Assert.True(vm.Ports[1].Enabled);
    }

    [Fact]
    public void ApplyCommand_PersistsModificationsToMetadata()
    {
        var meta = new ProjectMetadata();
        var vm = new ModbusPortConfigViewModel(meta);

        // Select Port 1 and change Baud to 115200, Parity to Even, StopBits to 2
        vm.SelectedPort = vm.Ports[0];
        vm.SelectedPort.BaudRate = 115200;
        vm.SelectedPort.Parity = "Even";
        vm.SelectedPort.StopBits = 2;
        vm.SelectedPort.ResponseTimeoutMs = 350;
        vm.SelectedPort.RetryCount = 3;
        vm.SelectedPort.PortName = "SENSOR_BUS";

        // Select Port 3 and enable it
        vm.Ports[2].Enabled = true;
        vm.Ports[2].BaudRate = 38400;

        vm.ApplyCommand.Execute(null);

        Assert.Equal(115200u, meta.ModbusPorts[0].BaudRate);
        Assert.Equal("Even", meta.ModbusPorts[0].Parity);
        Assert.Equal((byte)2, meta.ModbusPorts[0].StopBits);
        Assert.Equal((ushort)350, meta.ModbusPorts[0].ResponseTimeoutMs);
        Assert.Equal((ushort)3, meta.ModbusPorts[0].RetryCount);
        Assert.Equal("SENSOR_BUS", meta.ModbusPorts[0].PortName);

        Assert.True(meta.ModbusPorts[2].Enabled);
        Assert.Equal(38400u, meta.ModbusPorts[2].BaudRate);
    }

    [Fact]
    public void ResetDefaultsCommand_RestoresPortDefaults()
    {
        var meta = new ProjectMetadata();
        var vm = new ModbusPortConfigViewModel(meta);

        vm.SelectedPort = vm.Ports[0];
        vm.SelectedPort.BaudRate = 115200;
        vm.SelectedPort.Parity = "Odd";

        vm.ResetDefaultsCommand.Execute(null);

        Assert.Equal(9600u, vm.SelectedPort.BaudRate);
        Assert.Equal("None", vm.SelectedPort.Parity);
        Assert.Equal((byte)1, vm.SelectedPort.StopBits);
        Assert.Equal((ushort)200, vm.SelectedPort.ResponseTimeoutMs);
    }

    [Fact]
    public void PortItemViewModel_PillAndSummaryReflectState()
    {
        var model = new ModbusPortConfigModel
        {
            PortIndex = 1,
            PortName = "VFD_BUS",
            Enabled = true,
            BaudRate = 19200,
            Parity = "Even",
            StopBits = 1,
            ResponseTimeoutMs = 200,
            RetryCount = 2
        };

        var item = new ModbusPortItemViewModel(model);

        Assert.Equal("ACTIVE", item.StatusPill);
        Assert.Equal("#107C41", item.StatusPillColor);
        Assert.Contains("19200 bps", item.SummaryBadge);
        Assert.Contains("Timeout 200ms", item.SummaryBadge);
        Assert.Contains("Retries 2", item.SummaryBadge);

        item.Enabled = false;
        Assert.Equal("DISABLED", item.StatusPill);
        Assert.Equal("#64748B", item.StatusPillColor);
    }
}
