using System;
using System.Globalization;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class ModbusReadConfigDialogViewModelTests
{
    [Fact]
    public void Constructor_InitializesValuesFromNode_AndComputesInitialRegCount()
    {
        var node = new ModbusReadNodeViewModel
        {
            SlaveId = 2,
            FunctionCode = ModbusMasterFunctionCode.ReadInputRegisters,
            StartAddress = 50,
            RegCount = 1,
            PollIntervalMs = 250,
            TimeoutMs = 800
        };

        var pin1 = new ModbusPinBindingViewModel
        {
            Label = "Pressure",
            RegOffset = 0,
            DataType = ModbusDataType.Int16
        };
        var pin2 = new ModbusPinBindingViewModel
        {
            Label = "FlowRate",
            RegOffset = 1,
            DataType = ModbusDataType.Real // occupies 2 registers: offset 1 and 2 -> needs 3 registers total
        };

        node.Pins.Add(pin1);
        node.Pins.Add(pin2);

        var vm = new ModbusReadConfigDialogViewModel(node, new TagCatalogViewModel());

        Assert.Equal(2, vm.SlaveId);
        Assert.Equal(ModbusMasterFunctionCode.ReadInputRegisters, vm.FunctionCode);
        Assert.Equal(50, vm.StartAddress);
        // RegCount should auto-bump to 3 because pin2 (Real at offset 1) needs 1 + 2 = 3 registers
        Assert.Equal((ushort)3, vm.RegCount);
        Assert.Equal(250u, vm.PollIntervalMs);
        Assert.Equal(800u, vm.TimeoutMs);
        Assert.True(vm.HasPins);
        Assert.False(vm.HasNoPins);
    }

    [Fact]
    public void ChangingPinOffsetOrDataType_AutoUpdatesRegCount_WhenPinsRequireMore()
    {
        var node = new ModbusReadNodeViewModel { RegCount = 2 };
        var pin = new ModbusPinBindingViewModel
        {
            Label = "Temp",
            RegOffset = 0,
            DataType = ModbusDataType.Int16
        };
        node.Pins.Add(pin);

        var vm = new ModbusReadConfigDialogViewModel(node, new TagCatalogViewModel());
        Assert.Equal((ushort)2, vm.RegCount);

        // Change pin offset to 4 (Int16 needs 1 register, so offset 4 ends at 5)
        pin.RegOffset = 4;
        Assert.Equal((ushort)5, vm.RegCount);

        // Change datatype to Real (needs 2 registers, so offset 4 ends at 6)
        pin.DataType = ModbusDataType.Real;
        Assert.Equal((ushort)6, vm.RegCount);
    }

    [Fact]
    public void AddPin_IncrementsOffsetAndAttachesPropertyListener()
    {
        var node = new ModbusReadNodeViewModel();
        var vm = new ModbusReadConfigDialogViewModel(node, new TagCatalogViewModel());

        Assert.Empty(node.Pins);

        // Add 1st pin -> Offset 0, Int16 (span 1)
        vm.AddPin();
        Assert.Single(node.Pins);
        Assert.Equal((ushort)0, node.Pins[0].RegOffset);
        Assert.Equal("Pin_1", node.Pins[0].Label);

        // Change 1st pin to Real (span 2)
        node.Pins[0].DataType = ModbusDataType.Real;

        // Add 2nd pin -> Next offset should be 0 + 2 = 2
        vm.AddPin();
        Assert.Equal(2, node.Pins.Count);
        Assert.Equal((ushort)2, node.Pins[1].RegOffset);
        Assert.Equal("Pin_2", node.Pins[1].Label);

        // Changing the newly added pin's offset should trigger RegCount update
        node.Pins[1].RegOffset = 8;
        Assert.True(vm.RegCount >= 9);
    }

    [Fact]
    public void RemovePin_DetachesPinAndKeepsRemainingPins()
    {
        var node = new ModbusReadNodeViewModel();
        var vm = new ModbusReadConfigDialogViewModel(node, new TagCatalogViewModel());

        vm.AddPin();
        vm.AddPin();
        Assert.Equal(2, node.Pins.Count);

        var pinToRemove = node.Pins[0];
        vm.RemovePin(pinToRemove);

        Assert.Single(node.Pins);
        Assert.DoesNotContain(pinToRemove, node.Pins);
    }

    [Fact]
    public void RunSimulationTest_WithNoPins_FailsGracefully()
    {
        var node = new ModbusReadNodeViewModel();
        var vm = new ModbusReadConfigDialogViewModel(node, new TagCatalogViewModel());

        vm.SimulationHexInput = "01 03 02 00 64 B9 AF";
        vm.RunSimulationTest();

        Assert.True(vm.HasRunSimulation);
        Assert.False(vm.IsSimulationSuccess);
        Assert.Contains("⚠️", vm.SimulationStatusMessage);
    }

    [Fact]
    public void RunSimulationTest_WithInvalidHex_FailsGracefully()
    {
        var node = new ModbusReadNodeViewModel();
        node.Pins.Add(new ModbusPinBindingViewModel { Label = "Pin1", RegOffset = 0 });

        var vm = new ModbusReadConfigDialogViewModel(node, new TagCatalogViewModel());
        vm.SimulationHexInput = "ZZ Top Invalid";
        vm.RunSimulationTest();

        Assert.True(vm.HasRunSimulation);
        Assert.False(vm.IsSimulationSuccess);
        Assert.Contains("❌", vm.SimulationStatusMessage);
    }

    [Fact]
    public void RunSimulationTest_WithValidModbusResponse_DecodesPinsAndSucceeds()
    {
        var node = new ModbusReadNodeViewModel();
        var pin1 = new ModbusPinBindingViewModel
        {
            Label = "Temperature",
            RegOffset = 0,
            DataType = ModbusDataType.Int16,
            Multiplier = 0.1f
        };
        var pin2 = new ModbusPinBindingViewModel
        {
            Label = "Pressure",
            RegOffset = 1,
            DataType = ModbusDataType.UInt16,
            Multiplier = 1.0f
        };
        node.Pins.Add(pin1);
        node.Pins.Add(pin2);

        var vm = new ModbusReadConfigDialogViewModel(node, new TagCatalogViewModel());

        // Frame: Slave 01, FC 03, ByteCount 04, Reg0=0x00FA (250 -> 25.0), Reg1=0x03E8 (1000), CRC
        // CRC for 01 03 04 00 FA 03 E8:
        // Or we can provide register payload: "00 FA 03 E8"
        vm.SimulationHexInput = "00 FA 03 E8";
        vm.RunSimulationTest();

        Assert.True(vm.HasRunSimulation);
        Assert.True(vm.IsSimulationSuccess);
        Assert.Contains("✔", vm.SimulationStatusMessage);
        Assert.Equal(25.0, pin1.CurrentValue);
        Assert.Equal(1000.0, pin2.CurrentValue);
    }

    [Fact]
    public void ResetSampleHex_SetsStandardSampleFrame()
    {
        var node = new ModbusReadNodeViewModel();
        var vm = new ModbusReadConfigDialogViewModel(node, new TagCatalogViewModel());

        vm.ResetSampleHex();

        Assert.False(string.IsNullOrWhiteSpace(vm.SimulationHexInput));
        Assert.Contains("01 03", vm.SimulationHexInput);
        Assert.False(vm.HasRunSimulation);
    }

    [Fact]
    public void Apply_CommitsPropertiesBackToNode()
    {
        var node = new ModbusReadNodeViewModel
        {
            SlaveId = 1,
            StartAddress = 0,
            RegCount = 1,
            PollIntervalMs = 500,
            TimeoutMs = 1000
        };

        var vm = new ModbusReadConfigDialogViewModel(node, new TagCatalogViewModel())
        {
            SlaveId = 10,
            FunctionCode = ModbusMasterFunctionCode.ReadHoldingRegisters,
            StartAddress = 200,
            RegCount = 8,
            PollIntervalMs = 100,
            TimeoutMs = 500
        };

        vm.Apply();

        Assert.Equal((byte)10, node.SlaveId);
        Assert.Equal(ModbusMasterFunctionCode.ReadHoldingRegisters, node.FunctionCode);
        Assert.Equal((ushort)200, node.StartAddress);
        Assert.Equal((ushort)8, node.RegCount);
        Assert.Equal(100u, node.PollIntervalMs);
        Assert.Equal(500u, node.TimeoutMs);
    }

    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void ModbusRead_BilingualTooltips_AreFullyLocalized(string culture)
    {
        var originalLang = Services.LocalizationService.Instance.CurrentLanguage;
        try
        {
            Services.LocalizationService.Instance.CurrentLanguage = culture;

            var keys = new[]
            {
                "ModbusReadColSlaveTooltip",
                "ModbusReadColFcTooltip",
                "ModbusReadColStartAddrTooltip",
                "ModbusReadColQuantityTooltip",
                "ModbusReadColModeTooltip",
                "ModbusReadColPollIntervalTooltip",
                "ModbusReadColTimeoutTooltip",
                "ModbusReadPinNameTooltip",
                "ModbusReadPinOffsetTooltip",
                "ModbusReadPinTypeTooltip",
                "ModbusReadPinEndianTooltip",
                "ModbusReadPinScaleTooltip",
                "ModbusReadPinDeleteTooltip",
                "ModbusReadBtnAddPinTooltip",
                "ModbusReadBtnSampleFrameTooltip",
                "ModbusReadHexInputTooltip",
                "ModbusReadBtnRunTestTooltip",
                "ModbusReadResultsPreviewTooltip",
                "ModbusReadBtnApplyTooltip",
                "ModbusReadBtnCancelTooltip",
                "InspectorModbusReadConfigureTooltip"
            };

            foreach (var key in keys)
            {
                var text = Services.LocalizationService.Tr(key);
                Assert.False(string.IsNullOrWhiteSpace(text), $"Key '{key}' must have non-empty translation for culture '{culture}'");
                Assert.False(text.StartsWith("!"), $"Key '{key}' must not be missing in culture '{culture}'");
            }
        }
        finally
        {
            Services.LocalizationService.Instance.CurrentLanguage = originalLang;
        }
    }
}
