using SimplePLC.Infrastructure.Transport;
using SimplePLC.Studio.Models;
using SimplePLC.Studio.Services;
using SimplePLC.Studio.ViewModels;
using Xunit;

namespace SimplePLC.Studio.Tests;

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
        Assert.Contains("FW 1.7.0", mainVM.FirmwareInfo);

        // Act 2: Disconnect
        await mainVM.ToggleConnect();

        // Assert 2
        Assert.False(mainVM.IsConnected);
        Assert.Equal(ConnectionState.Disconnected, mainVM.ConnectionState);
    }
}
