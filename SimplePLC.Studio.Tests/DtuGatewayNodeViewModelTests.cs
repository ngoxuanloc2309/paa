using System.Linq;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class DtuGatewayNodeViewModelTests
{
    [Fact]
    public void Constructor_InitializesInputsAndOutputsCorrectly()
    {
        var node = new DtuGatewayNodeViewModel();

        Assert.True(node.Title == "DTU Gateway" || node.Title == "📡 DTU Gateway");
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Equal("EN", node.InputConnectors[0].Title);
        Assert.False(node.InputConnectors[0].IsEvent);
        Assert.Equal("TRIG", node.InputConnectors[1].Title);
        Assert.True(node.InputConnectors[1].IsEvent);

        Assert.Equal(4, node.OutputConnectors.Count);
        Assert.Equal("DONE", node.OutputConnectors[0].Title);
        Assert.True(node.OutputConnectors[0].IsEvent);
        Assert.Equal("BUSY", node.OutputConnectors[1].Title);
        Assert.False(node.OutputConnectors[1].IsEvent);
        Assert.Equal("ERR", node.OutputConnectors[2].Title);
        Assert.False(node.OutputConnectors[2].IsEvent);
        Assert.Equal("CODE", node.OutputConnectors[3].Title);
        Assert.False(node.OutputConnectors[3].IsEvent);
    }

    [Fact]
    public void Properties_SummaryAndBadge_ReflectModeChanges()
    {
        var node = new DtuGatewayNodeViewModel();

        Assert.Equal("RS-485 #1", node.PortDisplayName);
        Assert.Equal("TRANSFERENCE ➔ MQTT", node.BadgeText);
        Assert.Contains("MQTT", node.SummaryText);

        node.PortIndex = 1;
        Assert.Equal("RS-485 #2", node.PortDisplayName);

        node.SerialMode = "ModbusMaster";
        node.UplinkMode = "Http";
        Assert.Equal("MODBUSMASTER ➔ HTTP", node.BadgeText);
        Assert.Contains("Modbus Master", node.SummaryText);
        Assert.Contains("HTTP REST", node.SummaryText);

        node.UplinkMode = "TcpClient";
        Assert.Equal("MODBUSMASTER ➔ TCPCLIENT", node.BadgeText);
        Assert.Contains("TCP Client", node.SummaryText);
    }

    [Fact]
    public void HttpFields_CanBeAddedAndManipulated()
    {
        var node = new DtuGatewayNodeViewModel();
        Assert.Empty(node.HttpFields);

        node.HttpFields.Add(new DtuHttpFieldModel { Key = "temp", ValueSource = "Payload" });
        node.HttpFields.Add(new DtuHttpFieldModel { Key = "device", ValueSource = "DeviceId" });

        Assert.Equal(2, node.HttpFields.Count);
        Assert.Equal("temp", node.HttpFields[0].Key);
        Assert.Equal("Payload", node.HttpFields[0].ValueSource);
    }

    [Fact]
    public void NewProjectViewModel_IsDtuPresetSelected_EvaluatesAccurately()
    {
        var vm = new NewProjectViewModel();

        // Default preset is remote IO, not DTU
        Assert.False(vm.IsDtuPresetSelected);

        // Find DTU preset
        var dtuPreset = vm.Presets.FirstOrDefault(p => p.DeviceClass == SPLC_DeviceClass.DTU);
        if (dtuPreset != null)
        {
            vm.SelectStandardPreset(dtuPreset);
            Assert.True(vm.IsDtuPresetSelected);

            // Switching to custom mode turns it off
            vm.IsCustomMode = true;
            Assert.False(vm.IsDtuPresetSelected);

            vm.IsCustomMode = false;
            Assert.True(vm.IsDtuPresetSelected);
        }
    }
}
