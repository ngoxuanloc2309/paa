using System;
using System.Linq;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class DirectWriteNodeViewModelTests
{
    [Fact]
    public void Constructor_DefaultValues_InitializesCorrectly()
    {
        var node = new DirectWriteNodeViewModel();

        Assert.Equal(DirectWriteTargetPin.Do0, node.TargetPin);
        Assert.Equal(DirectWriteMode.DigitalOutput, node.Mode);
        Assert.Equal((ushort)18, node.ChannelCount);
        Assert.True(node.IsExclusiveChannel);
        Assert.Equal(50u, node.PulseWidthUs);
        Assert.Equal(1000u, node.PwmFrequencyHz);
        Assert.Equal(DirectWriteTriggerMode.OnTriggerRisingEdge, node.TriggerMode);
        Assert.Equal(300u, node.TimeoutMs);
        Assert.Equal(0x00FF00u, node.DefaultColorRgb);

        // Connectors (DigitalOutput mode uses Trig, In, En)
        Assert.Equal(3, node.InputConnectors.Count);
        Assert.Contains(node.InputConnectors, c => c.Title == "Trig");
        Assert.Contains(node.InputConnectors, c => c.Title == "In");
        Assert.Contains(node.InputConnectors, c => c.Title == "En");

        Assert.Equal(2, node.OutputConnectors.Count);
        Assert.Contains(node.OutputConnectors, c => c.Title == "Done");
        Assert.Contains(node.OutputConnectors, c => c.Title == "Err");
    }

    [Fact]
    public void TargetPinDisplay_ReturnsExpectedStrings()
    {
        var node = new DirectWriteNodeViewModel();

        node.TargetPin = DirectWriteTargetPin.Do0;
        Assert.Equal("PIN: DO0", node.TargetPinDisplay);

        node.TargetPin = DirectWriteTargetPin.Do1;
        Assert.Equal("PIN: DO1", node.TargetPinDisplay);

        node.TargetPin = DirectWriteTargetPin.Do3;
        Assert.Equal("PIN: DO3", node.TargetPinDisplay);

        node.TargetPin = DirectWriteTargetPin.Do7;
        Assert.Equal("PIN: DO7", node.TargetPinDisplay);
    }

    [Fact]
    public void ModeSummaryDisplay_ReflectsModeChanges()
    {
        var node = new DirectWriteNodeViewModel();

        node.Mode = DirectWriteMode.AddressableBus;
        node.ChannelCount = 18;
        node.IsExclusiveChannel = true;
        Assert.Equal("CH [1..18] (Excl)", node.ModeSummaryDisplay);

        node.Mode = DirectWriteMode.PwmDuty;
        node.PwmFrequencyHz = 2000;
        Assert.Equal("PWM: 2000Hz", node.ModeSummaryDisplay);

        node.Mode = DirectWriteMode.PulseTrain;
        node.PulseWidthUs = 100;
        Assert.Equal("PULSE: 100µs", node.ModeSummaryDisplay);
    }

    [Fact]
    public void DialogViewModel_ReflectsAndSavesChanges()
    {
        var node = new DirectWriteNodeViewModel();
        var dlgVm = new DirectWriteConfigDialogViewModel(node);

        dlgVm.TargetPin = DirectWriteTargetPin.Do1;
        dlgVm.Mode = DirectWriteMode.PwmDuty;
        dlgVm.PwmFrequencyHz = 5000;
        dlgVm.DefaultColorHex = "#FF0000";

        dlgVm.SaveCommand.Execute(null);

        Assert.Equal(DirectWriteTargetPin.Do1, node.TargetPin);
        Assert.Equal(DirectWriteMode.PwmDuty, node.Mode);
        Assert.Equal(5000u, node.PwmFrequencyHz);
        Assert.Equal(0xFF0000u, node.DefaultColorRgb);
    }

    [Fact]
    public void LogicEditorViewModel_SelectedDirectWriteNode_WorksCorrectly()
    {
        var tagCatalog = new TagCatalogViewModel();
        var vm = new LogicEditorViewModel(tagCatalog);
        var node = new DirectWriteNodeViewModel();

        vm.Nodes.Add(node);
        vm.SelectedNode = node;

        Assert.NotNull(vm.SelectedDirectWriteNode);
        Assert.Same(node, vm.SelectedDirectWriteNode);
    }

    [Fact]
    public void PwmDuty_DynamicallyHidesEnConnector_OnlyKeepsDuty()
    {
        var node = new DirectWriteNodeViewModel();

        // Initially in DO mode with OnTriggerRisingEdge: Trig, In, En
        Assert.Equal(3, node.InputConnectors.Count);
        Assert.Contains(node.InputConnectors, c => c.Title == "En");

        // Switch to PwmDuty mode with Continuous trigger: En and Trig must be removed
        node.Mode = DirectWriteMode.PwmDuty;
        node.TriggerMode = DirectWriteTriggerMode.Continuous;

        Assert.Single(node.InputConnectors);
        Assert.Equal("Duty", node.InputConnectors[0].Title);
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "En");
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "Trig");

        // Switch to OnTriggerRisingEdge: Trig added, Duty kept, En remains hidden
        node.TriggerMode = DirectWriteTriggerMode.OnTriggerRisingEdge;
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("Duty", node.InputConnectors[1].Title);
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "En");

        // Switch to COV (OnChangeOfValue): Trig removed, only Duty kept, En remains hidden
        node.TriggerMode = DirectWriteTriggerMode.OnChangeOfValue;
        Assert.Single(node.InputConnectors);
        Assert.Equal("Duty", node.InputConnectors[0].Title);
        Assert.DoesNotContain(node.InputConnectors, c => c.Title == "En");
        Assert.True(node.IsCovMode);
        Assert.False(node.IsTriggerMode);
    }

    [Fact]
    public void TriggerMode_CovMode_ReflectsDeadbandInSummaryText()
    {
        var node = new DirectWriteNodeViewModel
        {
            Mode = DirectWriteMode.PwmDuty,
            TriggerMode = DirectWriteTriggerMode.OnChangeOfValue,
            Deadband = 0.75f
        };

        Assert.Equal("COV (Δ0.75)", node.TriggerModeSummaryText);
        Assert.True(node.IsCovMode);

        node.Deadband = 1.25f;
        Assert.Equal("COV (Δ1.25)", node.TriggerModeSummaryText);
    }

    [Fact]
    public void PulseTrain_IncludesPulsesAndFreqConnectors()
    {
        var node = new DirectWriteNodeViewModel
        {
            Mode = DirectWriteMode.PulseTrain,
            TriggerMode = DirectWriteTriggerMode.Continuous
        };

        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Equal("Pulses", node.InputConnectors[0].Title);
        Assert.Equal("Freq", node.InputConnectors[1].Title);

        node.TriggerMode = DirectWriteTriggerMode.OnTriggerRisingEdge;
        Assert.Equal(3, node.InputConnectors.Count);
        Assert.Equal("Trig", node.InputConnectors[0].Title);
        Assert.Equal("Pulses", node.InputConnectors[1].Title);
        Assert.Equal("Freq", node.InputConnectors[2].Title);
    }
}
