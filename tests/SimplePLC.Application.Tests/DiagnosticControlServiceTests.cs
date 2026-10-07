using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Services;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

public class DiagnosticControlServiceTests
{
    [Fact]
    public async Task DiagnosticControlService_EnterAndExit_MaintainsModeAndFiresEvents()
    {
        // Arrange
        var simulator = new McuReferenceSimulator(wireProfile: 2);
        var client = new FakeModbusClient(simulator, isConnected: true);
        var gateway = new DiagnosticGateway(client);
        var coordinator = new DeviceOperationCoordinator();

        await using var service = new DiagnosticControlService(gateway, coordinator);

        bool statusChangedFired = false;
        service.StatusChanged += status =>
        {
            statusChangedFired = true;
        };

        // Act: Enter
        bool enterOk = await service.EnterManualModeAsync();
        Assert.True(enterOk);
        Assert.True(service.IsManualModeActive);
        Assert.True(statusChangedFired);

        // Act: Force DO0
        bool tagForcedFired = false;
        service.TagForced += (idx, val) =>
        {
            if (idx == 8 && val == 1) tagForcedFired = true;
        };
        await service.ForceTagAsync(8, 1);
        Assert.True(tagForcedFired);
        Assert.True(service.ForcedTags.ContainsKey(8));
        Assert.Equal(1, service.ForcedTags[8]);

        // Act: Release DO0
        bool tagReleasedFired = false;
        service.TagReleased += idx =>
        {
            if (idx == 8) tagReleasedFired = true;
        };
        await service.ReleaseTagAsync(8);
        Assert.True(tagReleasedFired);
        Assert.False(service.ForcedTags.ContainsKey(8));

        // Act: Exit
        bool exitOk = await service.ExitManualModeAsync();
        Assert.True(exitOk);
        Assert.False(service.IsManualModeActive);
    }

    [Fact]
    public async Task DiagnosticControlService_ReleaseAll_ClearsAllAndExits()
    {
        // Arrange
        var simulator = new McuReferenceSimulator(wireProfile: 2);
        var client = new FakeModbusClient(simulator, isConnected: true);
        var gateway = new DiagnosticGateway(client);

        await using var service = new DiagnosticControlService(gateway);
        await service.EnterManualModeAsync();

        await service.ForceTagAsync(8, 1);
        await service.ForceTagAsync(9, 1);
        Assert.Equal(2, service.ForcedTags.Count);

        // Act
        await service.ReleaseAllAsync();

        // Assert
        Assert.Empty(service.ForcedTags);
        Assert.False(service.IsManualModeActive);
    }
}
