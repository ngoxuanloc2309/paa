using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class DialogStylingAndWindowResizeTests
{
    private static readonly string ViewsDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SimplePLC.Studio", "Views"));

    private static readonly string StylesDirectory = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "SimplePLC.Studio", "Styles"));

    [Theory]
    [InlineData("ModbusReadConfigDialogView.xaml")]
    [InlineData("ModbusWriteConfigDialogView.xaml")]
    [InlineData("MqttConfigDialogView.xaml")]
    [InlineData("MqttSubConfigDialogView.xaml")]
    [InlineData("DirectWriteConfigDialogView.xaml")]
    public void DialogWindows_MustHave_ResizeModeNoResize(string xamlFileName)
    {
        string filePath = Path.Combine(ViewsDirectory, xamlFileName);
        Assert.True(File.Exists(filePath), $"XAML file '{xamlFileName}' must exist at '{filePath}'.");

        string content = File.ReadAllText(filePath);
        Assert.Contains("ResizeMode=\"NoResize\"", content);
    }

    [Fact]
    public void TableRowDeleteBtnStyle_MustHave_25pxDimensions_AndCenteredViewboxGeometry()
    {
        string controlsPath = Path.Combine(StylesDirectory, "Controls.xaml");
        Assert.True(File.Exists(controlsPath), $"Controls.xaml must exist at '{controlsPath}'.");

        string content = File.ReadAllText(controlsPath);

        // Verify Style existence
        Assert.Contains("x:Key=\"TableRowDeleteBtnStyle\"", content);

        // Verify dimensions are 25x25 (matching standard 25px row control height)
        Assert.Contains("<Setter Property=\"Width\" Value=\"25\" />", content);
        Assert.Contains("<Setter Property=\"Height\" Value=\"25\" />", content);

        // Verify Viewbox and symmetric Canvas geometry
        Assert.Contains("<Viewbox Width=\"9\" Height=\"9\"", content);
        Assert.Contains("<Canvas Width=\"16\" Height=\"16\">", content);
        Assert.Contains("Data=\"M 3,3 L 13,13 M 13,3 L 3,13\"", content);
        Assert.Contains("StrokeThickness=\"2.2\"", content);
        Assert.Contains("StrokeStartLineCap=\"Round\"", content);
    }

    [Theory]
    [InlineData("ModbusReadConfigDialogView.xaml")]
    [InlineData("ModbusWriteConfigDialogView.xaml")]
    [InlineData("MqttConfigDialogView.xaml")]
    [InlineData("MqttSubConfigDialogView.xaml")]
    public void AllConfigDialogViews_MustUse_TableRowDeleteBtnStyle(string xamlFileName)
    {
        string filePath = Path.Combine(ViewsDirectory, xamlFileName);
        string content = File.ReadAllText(filePath);

        Assert.Contains("Style=\"{StaticResource TableRowDeleteBtnStyle}\"", content);
    }

    [Fact]
    public void ModbusReadConfig_RefreshAvailablePorts_MultipleCalls_PreservesState()
    {
        var node = new ModbusReadNodeViewModel
        {
            PortIndex = 2,
            ReadMode = ModbusReadMode.Cyclic,
            FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters
        };

        var initialPorts = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "Port 1", Enabled = true },
            new() { PortIndex = 1, PortName = "Port 2", Enabled = true },
            new() { PortIndex = 2, PortName = "Port 3", Enabled = true }
        };

        var vm = new ModbusReadConfigDialogViewModel(node, new TagCatalogViewModel(), initialPorts);

        Assert.Equal(2, vm.PortIndex);
        Assert.Equal(ModbusReadMode.Cyclic, vm.ReadMode);
        Assert.Equal(ModbusMasterFunctionCode.ReadHoldingRegisters, vm.FunctionCode);

        // 1st Refresh: Same ports with updated descriptions
        vm.RefreshAvailablePorts(new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "Port 1", BaudRate = 19200, Enabled = true },
            new() { PortIndex = 1, PortName = "Port 2", BaudRate = 9600, Enabled = true },
            new() { PortIndex = 2, PortName = "Port 3", BaudRate = 115200, Enabled = true }
        });

        Assert.Equal(2, vm.PortIndex);
        Assert.Equal(ModbusReadMode.Cyclic, vm.ReadMode);
        Assert.Equal(ModbusMasterFunctionCode.ReadHoldingRegisters, vm.FunctionCode);
        Assert.Equal(3, vm.AvailablePorts.Count);
        Assert.Equal(2, vm.AvailableReadModes.Count);
        Assert.Equal(4, vm.AvailableFunctionCodes.Count);

        // 2nd Refresh: Port 2 disabled in settings -> must preserve Port 2 with disabled indicator
        vm.RefreshAvailablePorts(new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "Port 1", Enabled = true },
            new() { PortIndex = 1, PortName = "Port 2", Enabled = true },
            new() { PortIndex = 2, PortName = "Port 3", Enabled = false }
        });

        Assert.Equal(2, vm.PortIndex);
        Assert.Contains(vm.AvailablePorts, p => p.Value == 2);
    }

    [Fact]
    public void ModbusWriteConfig_RefreshAvailablePorts_MultipleCalls_PreservesState()
    {
        var node = new ModbusWriteNodeViewModel
        {
            PortIndex = 1,
            WriteMode = ModbusWriteMode.ChannelIndexed,
            FunctionCode = ModbusMasterWriteFunctionCode.WriteSingleRegister,
            TriggerMode = ModbusWriteTriggerMode.OnTriggerRisingEdge
        };

        var initialPorts = new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "Port 1", Enabled = true },
            new() { PortIndex = 1, PortName = "Port 2", Enabled = true }
        };

        var vm = new ModbusWriteConfigDialogViewModel(node, new TagCatalogViewModel(), initialPorts);

        Assert.Equal(1, vm.PortIndex);
        Assert.Equal(ModbusWriteMode.ChannelIndexed, vm.WriteMode);
        Assert.Equal(ModbusMasterWriteFunctionCode.WriteSingleRegister, vm.FunctionCode);
        Assert.Equal(ModbusWriteTriggerMode.OnTriggerRisingEdge, vm.TriggerMode);

        // Refresh with updated ports
        vm.RefreshAvailablePorts(new List<ModbusPortConfigModel>
        {
            new() { PortIndex = 0, PortName = "Port 1 (New)", Enabled = true },
            new() { PortIndex = 1, PortName = "Port 2 (New)", Enabled = true },
            new() { PortIndex = 3, PortName = "Port 4", Enabled = true }
        });

        Assert.Equal(1, vm.PortIndex);
        Assert.Equal(ModbusWriteMode.ChannelIndexed, vm.WriteMode);
        Assert.Equal(ModbusMasterWriteFunctionCode.WriteSingleRegister, vm.FunctionCode);
        Assert.Equal(ModbusWriteTriggerMode.OnTriggerRisingEdge, vm.TriggerMode);
        Assert.Equal(3, vm.AvailablePorts.Count);
        Assert.Equal(2, vm.AvailableWriteModes.Count);
        Assert.Equal(4, vm.AvailableFunctionCodes.Count);
        Assert.Equal(3, vm.AvailableTriggerModes.Count);
    }
}
