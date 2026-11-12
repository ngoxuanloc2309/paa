using SimplePLC.Infrastructure.Transport;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

/// <summary>
/// Kiểm thử tích hợp MainViewModel: kết nối simulator và cập nhật thông tin thiết bị.
/// </summary>
[Collection("AppServicesDeployTests")]
public class MainViewModelIntegrationTests
{
    [Fact]
    public async Task ToggleConnect_WhenConnectingToSimulator_ConnectsAndUpdatesMetadata()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        AppServices.Instance.SetModbusClient(fakeClient);

        var mainVM = new MainViewModel();
        mainVM.SelectedPort = "SIMULATOR (VIRTUAL)";

        // Act 1: Connect
        await mainVM.ToggleConnect();

        // Assert 1
        Assert.True(mainVM.IsConnected);
        Assert.Equal(ConnectionState.Connected, mainVM.ConnectionState);
        Assert.Contains("HW 1.0.0", mainVM.FirmwareInfo);
        Assert.Contains("FW 2.0.0", mainVM.FirmwareInfo);
        Assert.NotNull(mainVM.DtuGatewayStudioVM);
        Assert.True(mainVM.DtuGatewayStudioVM.IsHardwareConnected);

        // Act 2: Disconnect
        await mainVM.ToggleConnect();

        // Assert 2
        Assert.False(mainVM.IsConnected);
        Assert.Equal(ConnectionState.Disconnected, mainVM.ConnectionState);
        Assert.False(mainVM.DtuGatewayStudioVM.IsHardwareConnected);
    }

    [Fact]
    public void GetDocumentationUrl_WhenLanguageIsEnglish_ReturnsRootDocumentationPortalUrl()
    {
        // Arrange
        var mainVM = new MainViewModel();
        string origLang = LocalizationService.Instance.CurrentLanguage;

        try
        {
            // Act
            LocalizationService.Instance.CurrentLanguage = "en";
            string url = mainVM.GetDocumentationUrl();

            // Assert
            Assert.Equal("https://hoanv-synaptix.github.io/SynaptiX-IDE-Releases/", url);
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = origLang;
        }
    }

    [Fact]
    public void GetDocumentationUrl_WhenLanguageIsVietnamese_ReturnsVietnameseDocumentationPortalUrl()
    {
        // Arrange
        var mainVM = new MainViewModel();
        string origLang = LocalizationService.Instance.CurrentLanguage;

        try
        {
            // Act
            LocalizationService.Instance.CurrentLanguage = "vi";
            string url = mainVM.GetDocumentationUrl();

            // Assert
            Assert.Equal("https://hoanv-synaptix.github.io/SynaptiX-IDE-Releases/vi/", url);
        }
        finally
        {
            LocalizationService.Instance.CurrentLanguage = origLang;
        }
    }
}
