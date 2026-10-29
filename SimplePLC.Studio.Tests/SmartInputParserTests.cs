using SimplePLC.Application.Enums;
using SimplePLC.Domain.Enums;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

public class SmartInputParserTests
{
    [Theory]
    [InlineData("1", 1)]
    [InlineData("0", 0)]
    [InlineData("true", 1)]
    [InlineData("false", 0)]
    [InlineData("TRUE", 1)]
    [InlineData("FALSE", 0)]
    [InlineData("on", 1)]
    [InlineData("off", 0)]
    [InlineData("bật", 1)]
    [InlineData("tắt", 0)]
    [InlineData("bat", 1)]
    [InlineData("tat", 0)]
    [InlineData("high", 1)]
    [InlineData("low", 0)]
    [InlineData("yes", 1)]
    [InlineData("no", 0)]
    [InlineData("y", 1)]
    [InlineData("n", 0)]
    [InlineData(" 1 ", 1)]
    [InlineData(" 0 ", 0)]
    public void TryParseBool_ValidInputs_ReturnsExpected(string input, int expected)
    {
        bool success = SmartInputParser.TryParseBool(input, out int result);
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("invalid")]
    public void TryParseBool_InvalidInputs_ReturnsFalse(string? input)
    {
        bool success = SmartInputParser.TryParseBool(input, out _);
        Assert.False(success);
    }

    [Theory]
    [InlineData(1, "1")]
    [InlineData(0, "0")]
    [InlineData(99, "1")]
    [InlineData(-1, "1")]
    public void FormatBool_StrictBinary_ReturnsZeroOrOne(int value, string expected)
    {
        string formatted = SmartInputParser.FormatBool(value);
        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("25.5", 25.5)]
    [InlineData("25,5", 25.5)]
    [InlineData("0.0", 0.0)]
    [InlineData("0,0", 0.0)]
    [InlineData("-12.34", -12.34)]
    [InlineData("-12,34", -12.34)]
    [InlineData("  100  ", 100.0)]
    public void TryParseFlexibleDouble_SupportsCommaAndDot(string input, double expected)
    {
        bool success = SmartInputParser.TryParseFlexibleDouble(input, out double result);
        Assert.True(success);
        Assert.Equal(expected, result, precision: 5);
    }

    [Theory]
    [InlineData(25.0, "25.0")]
    [InlineData(0.0, "0.0")]
    [InlineData(100.0, "100.0")]
    [InlineData(25.5, "25.5")]
    [InlineData(25.1234, "25.1234")]
    [InlineData(-5.0, "-5.0")]
    [InlineData(-5.25, "-5.25")]
    public void FormatReal_IndustrialStandard_AppendsZeroForIntegers(double value, string expected)
    {
        string formatted = SmartInputParser.FormatReal(value);
        Assert.Equal(expected, formatted);
    }

    [Theory]
    [InlineData("100", 100)]
    [InlineData("  -42  ", -42)]
    [InlineData("100.0", 100)]
    [InlineData("50,0", 50)]
    public void TryParseInt_ValidInputs_ReturnsExpected(string input, int expected)
    {
        bool success = SmartInputParser.TryParseInt(input, out int result);
        Assert.True(success);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryParse_BooleanTag_ParsesToZeroOrOne()
    {
        bool ok = SmartInputParser.TryParse("bật", TagDataType.Boolean, isDigital: true, out int rawVal, out string display);
        Assert.True(ok);
        Assert.Equal(1, rawVal);
        Assert.Equal("1", display);

        ok = SmartInputParser.TryParse("0", TagDataType.Boolean, isDigital: true, out rawVal, out display);
        Assert.True(ok);
        Assert.Equal(0, rawVal);
        Assert.Equal("0", display);
    }

    [Fact]
    public void TryParse_Float32Tag_ParsesBitEquivalentAndFormatted()
    {
        bool ok = SmartInputParser.TryParse("25,5", TagDataType.Float32, isDigital: false, out int rawVal, out string display);
        Assert.True(ok);
        float asFloat = BitConverter.Int32BitsToSingle(rawVal);
        Assert.Equal(25.5f, asFloat, precision: 4);
        Assert.Equal("25.5", display);

        ok = SmartInputParser.TryParse("25", TagDataType.Float32, isDigital: false, out rawVal, out display);
        Assert.True(ok);
        Assert.Equal("25.0", display);
    }

    [Fact]
    public void ActionNodeViewModel_WhenTargetIsBoolean_DefaultsActionParamToZeroOrOne()
    {
        var node = new ActionNodeViewModel();
        var boolTag = new TagModel { Name = "DO0", Kind = TagKind.DiscreteOutput, DataType = TagDataType.Boolean };

        node.TargetTag = boolTag;

        Assert.True(node.IsTargetBoolean);
        Assert.False(node.IsTargetReal);

        // Test SetActionParamBoolCommand
        node.SetActionParamBoolCommand.Execute("1");
        Assert.Equal(1, node.ActionParam);
        Assert.Equal("1", node.ActionParamText);

        node.SetActionParamBoolCommand.Execute("0");
        Assert.Equal(0, node.ActionParam);
        Assert.Equal("0", node.ActionParamText);
    }

    [Fact]
    public void ComparatorNodeViewModel_WhenRealTag_FormatsThreshold()
    {
        var node = new ComparatorNodeViewModel();
        var realTag = new TagModel { Name = "AI0", Kind = TagKind.AnalogInput, DataType = TagDataType.Float32 };

        node.InputTag = realTag;
        node.CompareOp = CompareOp.GT;
        node.ThresholdLo = 25.0;

        Assert.Equal("AI0 > 25.0", node.FormulaExpression);
    }

    [Fact]
    public void TagModel_WhenBooleanTag_ClampsValuesToStrictZeroOrOne()
    {
        var tag = new TagModel { Name = "DI0", Kind = TagKind.DiscreteInput, DataType = TagDataType.Boolean };

        // Test assigning 2 or any non-zero value
        tag.Value = 2;
        Assert.Equal(1, tag.Value);
        Assert.Equal("1", tag.FormattedValue);

        tag.Value = 99;
        Assert.Equal(1, tag.Value);
        Assert.Equal("1", tag.FormattedValue);

        tag.Value = -1;
        Assert.Equal(1, tag.Value);
        Assert.Equal("1", tag.FormattedValue);

        tag.Value = 0;
        Assert.Equal(0, tag.Value);
        Assert.Equal("0", tag.FormattedValue);
    }

    [Fact]
    public void InputNodeViewModel_DisplaysNormalizedBinaryZeroOrOne()
    {
        var tag = new TagModel { Name = "DI0", Kind = TagKind.DiscreteInput, DataType = TagDataType.Boolean };
        var inputNode = new InputNodeViewModel(tag);

        tag.Value = 1;
        Assert.Equal("1", inputNode.ValueBadgeText);
        Assert.Equal("● 1", inputNode.SimButtonContent);

        tag.Value = 0;
        Assert.Equal("0", inputNode.ValueBadgeText);
        Assert.Equal("○ 0", inputNode.SimButtonContent);

        // Test SetTagValueBoolCommand
        inputNode.SetTagValueBoolCommand.Execute("1");
        Assert.Equal(1, tag.Value);
        Assert.Equal("1", inputNode.ValueBadgeText);

        inputNode.SetTagValueBoolCommand.Execute("0");
        Assert.Equal(0, tag.Value);
        Assert.Equal("0", inputNode.ValueBadgeText);
    }
}
