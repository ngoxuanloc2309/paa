using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using Xunit;

namespace SimplePLC.Application.Tests;

public class RuntimeMonitorServiceTests
{
    private readonly ProductDefinition _product = ProductDefinition.CreateRemoteIo8Di8Do4Ai();

    [Fact]
    public async Task Start_FiresTagsUpdatedAndHealthUpdatedEvents()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var tagReader = new RuntimeTagReader(fakeClient);
        var healthReader = new DeviceHealthReader(fakeClient);

        using var monitor = new RuntimeMonitorService(tagReader, healthReader)
        {
            PollingInterval = TimeSpan.FromMilliseconds(30),
            HealthCheckDivisor = 2
        };

        var tagsTcs = new TaskCompletionSource<IReadOnlyList<RuntimeTagValue>>();
        var healthTcs = new TaskCompletionSource<DeviceHealthInfo>();

        monitor.TagsUpdated += tags =>
        {
            tagsTcs.TrySetResult(tags);
        };

        monitor.HealthUpdated += health =>
        {
            healthTcs.TrySetResult(health);
        };

        // Act
        monitor.Start(_product, slaveId: 1, tagCount: 124);
        Assert.True(monitor.IsRunning);

        // Wait for events to fire
        var completedTagTask = await Task.WhenAny(tagsTcs.Task, Task.Delay(2000));
        Assert.Same(tagsTcs.Task, completedTagTask);
        var tags = await tagsTcs.Task;
        Assert.Equal(124, tags.Count);
        Assert.Equal("DI0", tags[0].TagName);

        var completedHealthTask = await Task.WhenAny(healthTcs.Task, Task.Delay(2000));
        Assert.Same(healthTcs.Task, completedHealthTask);
        var health = await healthTcs.Task;
        Assert.NotNull(health);

        // Act 2: Stop
        await monitor.StopAsync();

        // Assert
        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public async Task StopAsync_StopsGracefullyWithoutExceptions()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var tagReader = new RuntimeTagReader(fakeClient);
        var healthReader = new DeviceHealthReader(fakeClient);

        var monitor = new RuntimeMonitorService(tagReader, healthReader)
        {
            PollingInterval = TimeSpan.FromMilliseconds(20)
        };

        monitor.Start(_product);
        Assert.True(monitor.IsRunning);

        // Act
        await monitor.StopAsync();

        // Assert
        Assert.False(monitor.IsRunning);
    }

    [Fact]
    public async Task Start_FiresRtcUpdatedEvent_WhenSupported()
    {
        // Arrange
        var fakeClient = new FakeModbusClient();
        await fakeClient.ConnectAsync("COM1", 115200);
        var tagReader = new RuntimeTagReader(fakeClient);
        var healthReader = new DeviceHealthReader(fakeClient);
        var rtcClient = new RtcClockClient(fakeClient);

        var productV2 = ProductDefinition.CreateRemoteIo8Di8Do4Ai(wireProfile: 2);

        using var monitor = new RuntimeMonitorService(
            tagReader,
            healthReader,
            rtcClient: rtcClient)
        {
            PollingInterval = TimeSpan.FromMilliseconds(20),
            HealthCheckDivisor = 2,
            RtcCheckDivisor = 2
        };

        var rtcTcs = new TaskCompletionSource<SimplePLC.Protocol.Dto.RtcClockDto>();
        monitor.RtcUpdated += rtc => rtcTcs.TrySetResult(rtc);

        // Act
        monitor.Start(productV2, slaveId: 1, tagCount: 124);

        // Wait for RTC event to fire
        var completedTask = await Task.WhenAny(rtcTcs.Task, Task.Delay(2000));
        Assert.Same(rtcTcs.Task, completedTask);
        var rtcDto = await rtcTcs.Task;
        Assert.NotNull(rtcDto);

        await monitor.StopAsync();
    }
}
