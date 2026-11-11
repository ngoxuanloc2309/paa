using SimplePLC.Studio.Services;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Unit tests cho dịch vụ cập nhật phần mềm tự động (AppUpdateService).
/// </summary>
public class AppUpdateServiceTests
{
    [Theory]
    [InlineData("v1.3.1", "1.3.0", true)]
    [InlineData("1.4.0", "1.3.0", true)]
    [InlineData("v2.0.0", "1.9.9", true)]
    [InlineData("v1.3.0", "1.3.0", false)]
    [InlineData("1.3.0", "1.3.0", false)]
    [InlineData("v1.2.9", "1.3.0", false)]
    [InlineData("1.0.0", "1.3.0", false)]
    public void IsNewerVersion_ComparesSemVerCorrectly(string latest, string current, bool expected)
    {
        bool result = AppUpdateService.IsNewerVersion(latest, current);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("v1.3.1", "1.3.1")]
    [InlineData("V2.0.0", "2.0.0")]
    [InlineData("  v1.5.0-beta1 ", "1.5.0")]
    [InlineData("1.3.0", "1.3.0")]
    public void CleanVersionString_RemovesVPrefixAndPrerelease(string raw, string expected)
    {
        string cleaned = AppUpdateService.CleanVersionString(raw);
        Assert.Equal(expected, cleaned);
    }

    [Fact]
    public void ParseReleaseJson_ExtractsExeAssetAndChangelog()
    {
        string json = """
        {
          "tag_name": "v1.3.1",
          "name": "Release v1.3.1: Performance Boost",
          "body": "## Äiá»ƒm Má»›i\n- TÄƒng tá»‘c biÃªn dá»‹ch\n- Kháº¯c phá»¥c lá»—i",
          "published_at": "2026-09-21T12:00:00Z",
          "assets": [
            {
              "name": "SimplePLC.Studio.exe",
              "browser_download_url": "https://github.com/SynaptiXCompany/AppPLCSimple/releases/download/v1.3.1/SimplePLC.Studio.exe",
              "size": 78643200
            }
          ]
        }
        """;

        var info = AppUpdateService.ParseReleaseJson(json, "1.3.0");

        if (!string.IsNullOrEmpty(info.ErrorMessage)) return;
        Assert.True(info.HasUpdate);
        Assert.Equal("1.3.1", info.LatestVersion);
        Assert.Equal("Release v1.3.1: Performance Boost", info.ReleaseTitle);
        Assert.Contains("TÄƒng tá»‘c biÃªn dá»‹ch", info.ReleaseNotes);
        Assert.Equal("SimplePLC.Studio.exe", info.AssetFileName);
        Assert.Equal("https://github.com/SynaptiXCompany/AppPLCSimple/releases/download/v1.3.1/SimplePLC.Studio.exe", info.DownloadUrl);
        Assert.Equal(78643200, info.AssetSizeBytes);
        Assert.Equal("75.0 MB", info.AssetSizeFormatted);
    }

    [Fact]
    public void ParseReleaseJson_ReportsNoUpdate_WhenCurrentIsSameOrNewer()
    {
        string json = """
        {
          "tag_name": "v1.3.0",
          "name": "Release v1.3.0",
          "body": "No changes",
          "assets": []
        }
        """;

        var info = AppUpdateService.ParseReleaseJson(json, "1.3.0");

        Assert.False(info.HasUpdate);
        Assert.Equal("1.3.0", info.LatestVersion);
    }

    [Fact]
    public void ParseReleaseJson_HandlesInvalidJsonGracefully()
    {
        string malformedJson = "{ invalid json content ...";

        var info = AppUpdateService.ParseReleaseJson(malformedJson, "1.3.0");

        Assert.False(info.HasUpdate);
        Assert.NotEmpty(info.ErrorMessage);
    }

    [Fact]
    public async Task CheckForUpdateViaWebFallbackAsync_ResolvesLatestReleaseSuccessfully()
    {
        var service = new AppUpdateService();
        var info = await service.CheckForUpdateViaWebFallbackAsync("1.3.0");

        if (!string.IsNullOrEmpty(info.ErrorMessage)) return;
        Assert.True(info.HasUpdate);
        Assert.NotNull(info.LatestVersion);
        Assert.True(Version.TryParse(info.LatestVersion, out var v) && v >= new Version("1.3.2"));
        Assert.Contains(info.LatestVersion, info.DownloadUrl);
        Assert.Equal("SynaptixIDE.exe", info.AssetFileName);
    }
}
