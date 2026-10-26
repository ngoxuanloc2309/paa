using System.Collections.Generic;
using System.Linq;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Bộ kiểm thử độc lập cho tính năng Lọc Cổng RS-485 đã kích hoạt trong MB_READ/MB_WRITE,
/// Tooltip và Compiler Safety Gate kiểm tra cổng bị vô hiệu hóa.
/// </summary>
public sealed class ModbusPortFilteringTests
{
    [Fact]
    public void FormatDropdownLabel_FormatsCompactlyWithoutTextClipping()
    {
        var port = new ModbusPortConfigModel
        {
            PortIndex = 0,
            PortName = "VFD_BUS",
            Enabled = true,
            BaudRate = 19200,
            DataBits = 8,
            Parity = "Even",
            StopBits = 1,
            ResponseTimeoutMs = 200,
            RetryCount = 1
        };

        // Compact display for closed combobox
        var labelVi = port.FormatDropdownLabel(isVietnamese: true);
        Assert.Equal("Cổng 1: VFD_BUS", labelVi);

        var labelEn = port.FormatDropdownLabel(isVietnamese: false);
        Assert.Equal("Port 1: VFD_BUS", labelEn);

        // Disabled warning suffix
        var labelDisabledWarningVi = port.FormatDropdownLabel(isVietnamese: true, isForceDisabledWarning: true);
        Assert.Equal("Cổng 1: VFD_BUS [ĐÃ TẮT]", labelDisabledWarningVi);

        var labelDisabledWarningEn = port.FormatDropdownLabel(isVietnamese: false, isForceDisabledWarning: true);
        Assert.Equal("Port 1: VFD_BUS [DISABLED]", labelDisabledWarningEn);

        // Full summary for tooltips & status bars
        var fullSummaryVi = port.FormatFullSummary(isVietnamese: true);
        Assert.Equal("Cổng 1: VFD_BUS · 19200 bps · 8-E-1 · Timeout 200ms · Retries 1", fullSummaryVi);
    }

    [Fact]
    public void ModbusReadConfig_FiltersOnlyEnabledPorts()
    {
        var node = new ModbusReadNodeViewModel { PortIndex = 0 };
        var tagCatalog = new TagCatalogViewModel();

        var ports = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, BaudRate = 9600 },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = false, BaudRate = 19200 },
            new() { PortIndex = 2, PortName = "RS-485 #3", Enabled = true, BaudRate = 38400 },
            new() { PortIndex = 3, PortName = "RS-485 #4", Enabled = false, BaudRate = 115200 },
        };

        var vm = new ModbusReadConfigDialogViewModel(node, tagCatalog, ports);

        // Only Port 1 (index 0) and Port 3 (index 2) should be listed
        Assert.Equal(2, vm.AvailablePorts.Count);
        Assert.Equal((byte)0, vm.AvailablePorts[0].Value);
        Assert.Equal((byte)2, vm.AvailablePorts[1].Value);

        // Tooltip provides detailed parameters
        Assert.Contains("9600 bps", vm.PortSelectorTooltip);
    }

    [Fact]
    public void ModbusReadConfig_RetainsDisabledPortWhenCurrentlyAssignedToNode()
    {
        // Node currently has PortIndex = 1 (Port 2), but Port 2 is disabled
        var node = new ModbusReadNodeViewModel { PortIndex = 1 };
        var tagCatalog = new TagCatalogViewModel();

        var ports = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, BaudRate = 9600 },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = false, BaudRate = 19200 },
            new() { PortIndex = 2, PortName = "RS-485 #3", Enabled = true, BaudRate = 38400 },
            new() { PortIndex = 3, PortName = "RS-485 #4", Enabled = false, BaudRate = 115200 },
        };

        var vm = new ModbusReadConfigDialogViewModel(node, tagCatalog, ports);

        // AvailablePorts contains enabled ports (0 and 2) PLUS the currently assigned disabled port (1) with warning tag
        Assert.Equal(3, vm.AvailablePorts.Count);
        var disabledOption = vm.AvailablePorts.FirstOrDefault(p => p.Value == 1);
        Assert.NotNull(disabledOption);
        Assert.True(disabledOption.DisplayName.Contains("[ĐÃ TẮT]") || disabledOption.DisplayName.Contains("[DISABLED]"));

        // Tooltip indicates port is disabled
        Assert.True(vm.PortSelectorTooltip.Contains("VÔ HIỆU HÓA") || vm.PortSelectorTooltip.Contains("DISABLED"));
    }

    [Fact]
    public void ModbusReadConfig_HandlesAllPortsDisabledGracefullyWithoutPortPrefix()
    {
        var node = new ModbusReadNodeViewModel { PortIndex = 0 };
        var tagCatalog = new TagCatalogViewModel();

        var ports = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = false },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = false },
            new() { PortIndex = 2, PortName = "RS-485 #3", Enabled = false },
            new() { PortIndex = 3, PortName = "RS-485 #4", Enabled = false },
        };

        var vm = new ModbusReadConfigDialogViewModel(node, tagCatalog, ports);

        Assert.Single(vm.AvailablePorts);
        var text = vm.AvailablePorts[0].DisplayName;

        // Must NOT have misleading "Port 1" or "Cổng 1" prefix!
        Assert.False(text.StartsWith("Port 1"));
        Assert.False(text.StartsWith("Cổng 1"));
        Assert.True(text.Contains("Chưa kích hoạt") || text.Contains("No RS-485 Ports Enabled"));

        // Tooltip explains how to enable ports
        Assert.True(vm.PortSelectorTooltip.Contains("Cài đặt") || vm.PortSelectorTooltip.Contains("Settings"));
    }

    [Fact]
    public void ModbusWriteConfig_FiltersOnlyEnabledPorts()
    {
        var node = new ModbusWriteNodeViewModel { PortIndex = 2 };
        var tagCatalog = new TagCatalogViewModel();

        var ports = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = false },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, BaudRate = 19200 },
            new() { PortIndex = 2, PortName = "RS-485 #3", Enabled = true, BaudRate = 38400 },
            new() { PortIndex = 3, PortName = "RS-485 #4", Enabled = false },
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, tagCatalog, ports);

        Assert.Equal(2, vm.AvailablePorts.Count);
        Assert.Equal((byte)1, vm.AvailablePorts[0].Value);
        Assert.Equal((byte)2, vm.AvailablePorts[1].Value);

        Assert.Contains("38400 bps", vm.PortSelectorTooltip);
    }

    [Fact]
    public void RuleCompiler_FlagsDisabledModbusPortAsDiagnosticError()
    {
        var compiler = new RuleCompiler();
        var tagCatalog = new TagCatalogViewModel();

        var mbReadNode = new ModbusReadNodeViewModel
        {
            PortIndex = 2, // Port 3
            SlaveId = 1,
            StartAddress = 0,
            RegCount = 1
        };

        var ports = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true },
            new() { PortIndex = 2, PortName = "RS-485 #3", Enabled = false }, // Port 3 is disabled!
            new() { PortIndex = 3, PortName = "RS-485 #4", Enabled = false }
        };

        var nodes = new List<GraphNodeViewModel> { mbReadNode };
        var connections = new List<ConnectionViewModel>();

        var result = compiler.Compile(nodes, connections, tagCatalog, modbusPorts: ports);

        Assert.NotEmpty(result.Diagnostics);
        var err = result.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error && d.NodeId == mbReadNode.Id);
        Assert.NotNull(err);
        Assert.True(err.Message.Contains("vô hiệu hóa") || err.Message.Contains("disabled"));
    }

    [Fact]
    public void RuleCompiler_PassesWhenModbusPortIsEnabled()
    {
        var compiler = new RuleCompiler();
        var tagCatalog = new TagCatalogViewModel();

        var mbReadNode = new ModbusReadNodeViewModel
        {
            PortIndex = 0, // Port 1 is enabled
            SlaveId = 1,
            StartAddress = 0,
            RegCount = 1
        };

        var ports = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true },
            new() { PortIndex = 2, PortName = "RS-485 #3", Enabled = false },
            new() { PortIndex = 3, PortName = "RS-485 #4", Enabled = false }
        };

        var nodes = new List<GraphNodeViewModel> { mbReadNode };
        var connections = new List<ConnectionViewModel>();

        var result = compiler.Compile(nodes, connections, tagCatalog, modbusPorts: ports);

        // No port-disabled error for Port 1
        var portErr = result.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error && (d.Message.Contains("vô hiệu hóa") || d.Message.Contains("disabled")));
        Assert.Null(portErr);
    }

    [Fact]
    public void ModbusReadConfig_AutoSwitchesToFirstMasterPortAndExcludesSlavePorts()
    {
        // Port 1 (index 0) is configured as Slave! Node is pointing to Port 1 (index 0).
        var node = new ModbusReadNodeViewModel { PortIndex = 0 };
        var tagCatalog = new TagCatalogViewModel();

        var ports = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Slave", SlaveId = 1 },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, Role = "Master", BaudRate = 19200 },
            new() { PortIndex = 2, PortName = "RS-485 #3", Enabled = false, Role = "Master" },
            new() { PortIndex = 3, PortName = "RS-485 #4", Enabled = false, Role = "Master" },
        };

        var vm = new ModbusReadConfigDialogViewModel(node, tagCatalog, ports);

        // 1. AvailablePorts must ONLY contain enabled Master ports (Port 2, index 1)
        Assert.Single(vm.AvailablePorts);
        Assert.Equal((byte)1, vm.AvailablePorts[0].Value);
        Assert.DoesNotContain("SLAVE", vm.AvailablePorts[0].DisplayName);

        // 2. PortIndex must be automatically switched to the valid Master port (index 1)
        Assert.Equal((byte)1, vm.PortIndex);

        // 3. Applying updates node.PortIndex to 1, completely preventing Slave conflict
        vm.Apply();
        Assert.Equal((byte)1, node.PortIndex);
    }

    [Fact]
    public void ModbusWriteConfig_AutoSwitchesToFirstMasterPortAndExcludesSlavePorts()
    {
        // Port 1 (index 0) is configured as Slave! Node is pointing to Port 1 (index 0).
        var node = new ModbusWriteNodeViewModel { PortIndex = 0 };
        var tagCatalog = new TagCatalogViewModel();

        var ports = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = "Slave", SlaveId = 1 },
            new() { PortIndex = 1, PortName = "RS-485 #2", Enabled = true, Role = "Master", BaudRate = 19200 },
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, tagCatalog, ports);

        // 1. AvailablePorts must ONLY contain enabled Master ports (Port 2, index 1)
        Assert.Single(vm.AvailablePorts);
        Assert.Equal((byte)1, vm.AvailablePorts[0].Value);

        // 2. PortIndex must be automatically switched to index 1
        Assert.Equal((byte)1, vm.PortIndex);

        // 3. Applying updates node.PortIndex to 1
        vm.Apply();
        Assert.Equal((byte)1, node.PortIndex);
    }
}
