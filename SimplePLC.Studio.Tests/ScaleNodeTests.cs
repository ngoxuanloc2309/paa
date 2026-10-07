using System.Windows;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class ScaleNodeTests
{
    [Fact]
    public void ScaleNode_DefaultValues_CalculatesLinearOutputCorrectly()
    {
        // Arrange: Default Gain = 0.01, Offset = 0, Unit = "bar"
        var scale = new ScaleNodeViewModel
        {
            Gain = 0.01,
            Offset = 0.0,
            Unit = "bar",
            DecimalPlaces = 1,
            IsClamped = true,
            ClampMin = 0.0,
            ClampMax = 100.0
        };

        // Act: 6500 mV (65% of 10V)
        double result = scale.Calculate(6500);

        // Assert
        Assert.Equal(65.0, result, 3);
        Assert.Equal(65.0, scale.LiveScaledValue, 3);
        Assert.Equal(65.0, scale.LiveProgressPercent, 1);
        Assert.Equal("65.0 bar", scale.LiveFormattedValue);
        Assert.True(scale.IsLiveActive);
    }

    [Fact]
    public void ScaleNode_ClampingEnabled_ClampsOutputWithinMinAndMax()
    {
        // Arrange: Pressure sensor 0..10 bar
        var scale = new ScaleNodeViewModel
        {
            Gain = 0.001, // 0..10,000 mV -> 0..10 bar
            Offset = 0.0,
            Unit = "bar",
            IsClamped = true,
            ClampMin = 0.0,
            ClampMax = 10.0
        };

        // Act: Input exceeds 10,000 mV (overvoltage: 12,500 mV)
        double highResult = scale.Calculate(12500);
        Assert.Equal(10.0, highResult);
        Assert.Equal(100.0, scale.LiveProgressPercent);

        // Act: Input below 0 mV (noise: -500 mV)
        double lowResult = scale.Calculate(-500);
        Assert.Equal(0.0, lowResult);
        Assert.Equal(0.0, scale.LiveProgressPercent);
    }

    [Fact]
    public void ScaleNode_ClampingDisabled_AllowsValuesBeyondLimits()
    {
        var scale = new ScaleNodeViewModel
        {
            Gain = 0.001,
            Offset = 0.0,
            IsClamped = false,
            ClampMin = 0.0,
            ClampMax = 10.0
        };

        double result = scale.Calculate(15000);
        Assert.Equal(15.0, result);
    }

    [Fact]
    public void ScaleNode_TwoPointCalibrationHelper_ComputesCorrectGainAndOffset()
    {
        // Arrange: Sensor 4..20mA over 500 Ohm shunt gives 2000..10000 mV
        // Engineering range: 0.0 .. 16.0 bar
        var scale = new ScaleNodeViewModel
        {
            CalibX1 = 2000,  // 4mA = 2000 mV
            CalibY1 = 0.0,   // 0 bar
            CalibX2 = 10000, // 20mA = 10000 mV
            CalibY2 = 16.0   // 16 bar
        };

        // Act
        scale.CalculateFromCalibPointsCommand.Execute(null);

        // Assert
        // Gain = (16 - 0) / (10000 - 2000) = 16 / 8000 = 0.002
        // Offset = 0 - 0.002 * 2000 = -4.0
        Assert.Equal(0.002, scale.Gain, 5);
        Assert.Equal(-4.0, scale.Offset, 3);
        Assert.Equal(0.0, scale.ClampMin);
        Assert.Equal(16.0, scale.ClampMax);

        // Test at midpoint 12mA = 6000 mV -> should be exactly 8.0 bar
        double midResult = scale.Calculate(6000);
        Assert.Equal(8.0, midResult, 3);
        Assert.Equal(50.0, scale.LiveProgressPercent, 1);
    }

    [Fact]
    public void ScaleNode_TwoPointCalibrationHelper_HandlesEqualX_DoesNotCrash()
    {
        var scale = new ScaleNodeViewModel
        {
            Gain = 0.05,
            Offset = 1.0,
            CalibX1 = 5000,
            CalibX2 = 5000 // Equal!
        };

        // Should return early and not throw DivideByZero
        scale.CalculateFromCalibPointsCommand.Execute(null);

        Assert.Equal(0.05, scale.Gain);
        Assert.Equal(1.0, scale.Offset);
    }

    [Fact]
    public void ScaleNode_BipolarTemperatureRange_CalculatesNegativeValues()
    {
        // Temperature sensor: 0..10,000 mV -> -40.0 .. +60.0 degC
        var scale = new ScaleNodeViewModel
        {
            Gain = 0.01,
            Offset = -40.0,
            Unit = "°C",
            DecimalPlaces = 1,
            IsClamped = true,
            ClampMin = -40.0,
            ClampMax = 60.0
        };

        // At 0 mV -> -40.0 degC
        Assert.Equal(-40.0, scale.Calculate(0));
        Assert.Equal("-40.0 °C", scale.LiveFormattedValue);

        // At 4000 mV -> 0.0 degC
        Assert.Equal(0.0, scale.Calculate(4000));
        Assert.Equal("0.0 °C", scale.LiveFormattedValue);

        // At 10,000 mV -> 60.0 degC
        Assert.Equal(60.0, scale.Calculate(10000));
        Assert.Equal("60.0 °C", scale.LiveFormattedValue);
    }

    [Fact]
    public void ScaleNode_ProjectSerialization_RoundtripPreservesAllConfiguration()
    {
        // Arrange
        var catalog = new TagCatalogViewModel();
        var editor = new LogicEditorViewModel(catalog);
        var aiTag = catalog.AllTags.First(t => t.Kind == TagKind.AnalogInput);
        var vregTag = catalog.RegisterTags.First();

        var scaleNode = new ScaleNodeViewModel(aiTag, vregTag)
        {
            Location = new Point(200, 300),
            CustomLabel = "ApSuatBonDau",
            Gain = 0.00125,
            Offset = -2.5,
            Unit = "psi",
            DecimalPlaces = 2,
            IsClamped = true,
            ClampMin = 0.0,
            ClampMax = 150.0
        };

        // Act: Serialize to ProjectNodeData
        var nodeData = LogicEditorViewModel.ExportSingleNodeData(scaleNode);

        // Assert serialized fields
        Assert.Equal("Scale", nodeData.Type);
        Assert.Equal("ApSuatBonDau", nodeData.CustomLabel);
        Assert.Equal(0.00125, nodeData.Gain);
        Assert.Equal(-2.5, nodeData.Offset);
        Assert.Equal("psi", nodeData.Unit);
        Assert.Equal(2, nodeData.DecimalPlaces);
        Assert.True(nodeData.IsClamped);
        Assert.Equal(0.0, nodeData.ClampMin);
        Assert.Equal(150.0, nodeData.ClampMax);
        Assert.Equal(aiTag.Name, nodeData.TagName);
        Assert.Equal(vregTag.Name, nodeData.OutputTagName);

        // Act: Deserialize back from ProjectNodeData
        var restoredNode = editor.CreateNodeFromData(nodeData) as ScaleNodeViewModel;

        // Assert restored ViewModel
        Assert.NotNull(restoredNode);
        Assert.Equal("ApSuatBonDau", restoredNode.CustomLabel);
        Assert.Equal(0.00125, restoredNode.Gain);
        Assert.Equal(-2.5, restoredNode.Offset);
        Assert.Equal("psi", restoredNode.Unit);
        Assert.Equal(2, restoredNode.DecimalPlaces);
        Assert.True(restoredNode.IsClamped);
        Assert.Equal(0.0, restoredNode.ClampMin);
        Assert.Equal(150.0, restoredNode.ClampMax);
        Assert.Equal(aiTag.Name, restoredNode.InputTag?.Name);
        Assert.Equal(vregTag.Name, restoredNode.OutputTag?.Name);
    }

    [Fact]
    public void ScaleNode_SimulationProgressText_DisplaysFormulaWhenIdleAndInWhenActive()
    {
        var scale = new ScaleNodeViewModel
        {
            Gain = 0.015,
            Offset = -50.0,
            Unit = "°C"
        };

        // When idle (rawInput = 0 and !IsLiveActive)
        Assert.Equal("y = 0.015·x - 50", scale.SimulationProgressText);

        // When calculating live / simulation with input 6557 mV
        scale.Calculate(6557);
        Assert.Equal("IN: 6,557 mV", scale.SimulationProgressText);
        Assert.Equal("48.4 °C (48%)", scale.ScaleStatusPillText);
    }


    [Fact]
    public void ActionNode_ConnectedToScale_DisplaysConvertedValue()
    {
        var catalog = new TagCatalogViewModel();
        var vreg = catalog.RegisterTags.First();
        var actNode = new ActionNodeViewModel(vreg)
        {
            ActionType = ActionType.SCALE_TAG
        };

        // Initially when idle
        Assert.Equal($"{vreg.Name} ← SCALE", actNode.ActionExpression);
        Assert.Equal(actNode.TargetTagShortText, actNode.ActionStatusPillText);

        // When simulation updates live text from Scale
        actNode.SimTargetLiveText = "48.4 °C";
        actNode.IsSimActive = true;
        Assert.Equal($"{vreg.Name} = 48.4 °C", actNode.ActionExpression);
        Assert.Equal("48.4 °C", actNode.ActionStatusPillText);
    }
}
