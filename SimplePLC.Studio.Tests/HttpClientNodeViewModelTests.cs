using System.Linq;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests cho HttpClientNodeViewModel theo học thuyết IEC 61508-3 và AGENTS.md.
/// </summary>
public class HttpClientNodeViewModelTests
{
    [Fact]
    public void Constructor_DefaultValues_InitializesCorrectly()
    {
        var node = new HttpClientNodeViewModel();

        Assert.Equal("HTTP_CLIENT", node.Title);
        Assert.Equal("http://api.myserver.com/telemetry", node.Url);
        Assert.Equal("POST", node.Method);
        Assert.Equal("POST", node.MethodBadge);
        Assert.Equal("Content-Type: application/json", node.CustomHeaders);
        Assert.Equal((ushort)10, node.TimeoutSec);
        Assert.Equal("Event", node.TriggerMode);
        Assert.True(node.IsEventMode);
        Assert.False(node.IsPeriodicMode);
        Assert.True(node.IsFieldsMode);
        Assert.False(node.IsRawMode);
        Assert.NotNull(node.Fields);

        // Connectors verification
        Assert.Equal(2, node.InputConnectors.Count);
        Assert.Contains(node.InputConnectors, c => c.Title == "Trig" && c.IsInput && c.IsEvent);
        Assert.Contains(node.InputConnectors, c => c.Title == "Data" && c.IsInput);

        Assert.Equal(4, node.OutputConnectors.Count);
        Assert.Contains(node.OutputConnectors, c => c.Title == "Busy" && !c.IsInput);
        Assert.Contains(node.OutputConnectors, c => c.Title == "Done" && !c.IsInput && c.IsEvent);
        Assert.Contains(node.OutputConnectors, c => c.Title == "Err" && !c.IsInput && c.IsEvent);
        Assert.Contains(node.OutputConnectors, c => c.Title == "Status" && !c.IsInput);
    }

    [Fact]
    public void Url_ExceedingLimit_AutoTruncatesTo64Chars()
    {
        var node = new HttpClientNodeViewModel();
        string longUrl = "http://very-long-domain-name-that-definitely-exceeds-the-maximum-limit-of-sixty-four-characters.com/api";

        node.Url = longUrl;

        Assert.True(node.Url.Length <= 64);
        Assert.Equal(longUrl[..64], node.Url);
    }

    [Fact]
    public void UrlValidationError_InvalidFormats_ReportsErrors()
    {
        var node = new HttpClientNodeViewModel();

        node.Url = "";
        Assert.True(node.HasUrlError);
        Assert.NotEmpty(node.UrlValidationError);

        node.Url = "ftp://invalid-protocol.com/data";
        Assert.True(node.HasUrlError);
        Assert.NotEmpty(node.UrlValidationError);

        node.Url = "https://valid.endpoint.io/telemetry";
        Assert.False(node.HasUrlError);
        Assert.Empty(node.UrlValidationError);
    }

    [Fact]
    public void CustomHeaders_ExceedingLimit_AutoTruncatesTo56Chars()
    {
        var node = new HttpClientNodeViewModel();
        string longHeaders = "Authorization: Bearer my-extremely-long-jwt-token-string-that-is-way-too-long-for-mcu";

        node.CustomHeaders = longHeaders;

        Assert.True(node.CustomHeaders.Length <= 56);
        Assert.Equal(longHeaders[..56], node.CustomHeaders);
    }

    [Fact]
    public void TriggerMode_TogglePeriodic_UpdatesProperties()
    {
        var node = new HttpClientNodeViewModel();

        node.TriggerMode = "Periodic";
        node.PeriodicIntervalSec = 15;

        Assert.True(node.IsPeriodicMode);
        Assert.Contains("15", node.TriggerModeName);

        node.TriggerMode = "OnDataReceived";
        Assert.False(node.IsPeriodicMode);
    }

    [Fact]
    public void PayloadMode_ToggleRawAndCustom_UpdatesVisibility()
    {
        var node = new HttpClientNodeViewModel();

        node.PayloadMode = "Raw";
        Assert.True(node.IsRawMode);
        Assert.False(node.IsCustomTemplateVisible);

        node.PayloadMode = "Custom";
        Assert.False(node.IsRawMode);
        Assert.True(node.IsCustomTemplateVisible);
    }

    [Fact]
    public void StatusPillText_ReflectsRuntimeStates()
    {
        var node = new HttpClientNodeViewModel();

        Assert.True(node.StatusPillText.Equals("READY", StringComparison.OrdinalIgnoreCase) || node.StatusPillText.Equals("SẴN SÀNG", StringComparison.OrdinalIgnoreCase));

        node.IsBusyActive = true;
        Assert.True(node.StatusPillText.Contains("TX") || node.StatusPillText.Contains("ĐANG GỬI"));
        node.IsBusyActive = false;

        node.LastStatusCode = 200;
        node.RequestCount = 3;
        Assert.Contains("200", node.StatusPillText);

        node.IsErrorActive = true;
        node.LastErrorMessage = "TIMEOUT";
        Assert.True(node.StatusPillText.Contains("ERR") || node.StatusPillText.Contains("LỖI"));
    }
}
