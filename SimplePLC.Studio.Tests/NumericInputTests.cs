using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class NumericInputTests
{
    [Theory]
    [InlineData("75.5", 75.5)]
    [InlineData("75,5", 75.5)]
    [InlineData("0.01", 0.01)]
    [InlineData("0,01", 0.01)]
    [InlineData("100", 100.0)]
    [InlineData("-12.34", -12.34)]
    [InlineData("-12,34", -12.34)]
    [InlineData("  42.5  ", 42.5)]
    public void NumericInputHelper_TryParseFlexibleDouble_SupportsDotAndComma(string input, double expected)
    {
        bool success = NumericInputHelper.TryParseFlexibleDouble(input, out double result);

        Assert.True(success);
        Assert.Equal(expected, result, precision: 6);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-")]
    [InlineData("+")]
    [InlineData(".")]
    [InlineData("-.")]
    [InlineData("abc")]
    public void NumericInputHelper_IncompleteOrInvalidInput_ReturnsFalseSafely(string? input)
    {
        bool success = NumericInputHelper.TryParseFlexibleDouble(input, out double result);

        Assert.False(success);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void ComparatorNodeViewModel_TypingDecimal_PreservesDotAndUpdatesThresholdLo()
    {
        var node = new ComparatorNodeViewModel();

        // Simulate typing keystroke sequence "7" -> "75" -> "75." -> "75.5"
        node.ThresholdLoText = "7";
        Assert.Equal(7.0, node.ThresholdLo);

        node.ThresholdLoText = "75";
        Assert.Equal(75.0, node.ThresholdLo);

        // Typing decimal dot must not be overwritten or wiped out
        node.ThresholdLoText = "75.";
        Assert.Equal("75.", node.ThresholdLoText);
        Assert.Equal(75.0, node.ThresholdLo);

        node.ThresholdLoText = "75.5";
        Assert.Equal("75.5", node.ThresholdLoText);
        Assert.Equal(75.5, node.ThresholdLo);

        // Programmatic update must sync back to ThresholdLoText
        node.ThresholdLo = 82.75;
        Assert.Equal("82.75", node.ThresholdLoText);
    }

    [Fact]
    public void ComparatorNodeViewModel_VietnameseCommaDecimal_UpdatesThresholdLo()
    {
        var node = new ComparatorNodeViewModel();

        node.ThresholdLoText = "75,5";
        Assert.Equal(75.5, node.ThresholdLo);

        node.ThresholdHiText = "99,9";
        Assert.Equal(99.9, node.ThresholdHi);
    }

    [Fact]
    public void TriggerNodeViewModel_DecimalInput_SupportsDotAndComma()
    {
        var node = new TriggerNodeViewModel();

        // Keystroke typing test
        node.ThresholdLoText = "75.";
        Assert.Equal("75.", node.ThresholdLoText);

        node.ThresholdLoText = "75.5";
        Assert.Equal(75.5, node.ThresholdLo);

        node.ThresholdLoText = "85,25";
        Assert.Equal(85.25, node.ThresholdLo);

        // Programmatic change
        node.ThresholdLo = 120.5;
        Assert.Equal("120.5", node.ThresholdLoText);
    }

    [Fact]
    public void ScaleNodeViewModel_DecimalInput_SupportsGainAndOffset()
    {
        var node = new ScaleNodeViewModel();

        node.GainText = "0,05";
        Assert.Equal(0.05, node.Gain, precision: 6);

        node.OffsetText = "-10,5";
        Assert.Equal(-10.5, node.Offset, precision: 6);

        node.ClampMinText = "5.5";
        Assert.Equal(5.5, node.ClampMin, precision: 6);

        node.ClampMaxText = "95.5";
        Assert.Equal(95.5, node.ClampMax, precision: 6);
    }

    [Fact]
    public void ScaleNodeViewModel_CalculateFromCalibPoints_SyncsTextProperties()
    {
        var node = new ScaleNodeViewModel
        {
            CalibX1 = 0,
            CalibY1 = 0,
            CalibX2 = 1000,
            CalibY2 = 50
        };

        node.CalculateFromCalibPoints();

        Assert.Equal(0.05, node.Gain, precision: 6);
        Assert.Equal("0.05", node.GainText);
        Assert.Equal(0.0, node.Offset, precision: 6);
        Assert.Equal("0", node.OffsetText);
        Assert.Equal(0.0, node.ClampMin, precision: 6);
        Assert.Equal("0", node.ClampMinText);
        Assert.Equal(50.0, node.ClampMax, precision: 6);
        Assert.Equal("50", node.ClampMaxText);
    }
}
