using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SimplePLC.Studio.Converters;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests for <see cref="ComparisonConverter"/> and <see cref="ComparisonToVisibilityConverter"/>
/// asserting two-way conversion for both integer tab indices and string mode parameters.
/// </summary>
public sealed class ComparisonConverterTests
{
    private readonly ComparisonConverter _converter = new();
    private readonly ComparisonToVisibilityConverter _visConverter = new();

    [Fact]
    public void Convert_WhenValueMatchesParameter_ReturnsTrue()
    {
        var result = _converter.Convert("Http", typeof(bool), "Http", CultureInfo.InvariantCulture);
        Assert.Equal(true, result);
    }

    [Fact]
    public void Convert_WhenValueDoesNotMatchParameter_ReturnsFalse()
    {
        var result = _converter.Convert("Mqtt", typeof(bool), "Http", CultureInfo.InvariantCulture);
        Assert.Equal(false, result);
    }

    [Fact]
    public void ConvertBack_WhenParameterIsInteger_ReturnsParsedInt()
    {
        var result = _converter.ConvertBack(true, typeof(int), "2", CultureInfo.InvariantCulture);
        Assert.Equal(2, result);
    }

    [Fact]
    public void ConvertBack_WhenParameterIsStringMode_ReturnsString()
    {
        // Bug reproduction test: Parameter "Http" with targetType string
        var result = _converter.ConvertBack(true, typeof(string), "Http", CultureInfo.InvariantCulture);
        Assert.Equal("Http", result);
    }

    [Fact]
    public void ConvertBack_WhenValueIsFalse_ReturnsBindingDoNothing()
    {
        var result = _converter.ConvertBack(false, typeof(string), "Http", CultureInfo.InvariantCulture);
        Assert.Equal(Binding.DoNothing, result);
    }

    [Fact]
    public void ComparisonToVisibilityConverter_WhenMatches_ReturnsVisible()
    {
        var result = _visConverter.Convert("TcpClient", typeof(Visibility), "TcpClient", CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Visible, result);
    }

    [Fact]
    public void ComparisonToVisibilityConverter_WhenDoesNotMatch_ReturnsCollapsed()
    {
        var result = _visConverter.Convert("Mqtt", typeof(Visibility), "TcpClient", CultureInfo.InvariantCulture);
        Assert.Equal(Visibility.Collapsed, result);
    }
}
