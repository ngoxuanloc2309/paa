using SimplePLC.Application.Models;
using SimplePLC.Application.Services;
using Xunit;

namespace SimplePLC.Application.Tests;

public class DeviceOperationCoordinatorTests
{
    [Fact]
    public async Task TryAcquirePollingLeaseAsync_WhenNoExclusiveOp_ReturnsValidLease()
    {
        // Arrange
        var coordinator = new DeviceOperationCoordinator();

        // Act
        var lease = await coordinator.TryAcquirePollingLeaseAsync();

        // Assert
        Assert.NotNull(lease);
        Assert.False(coordinator.IsExclusiveOperationActive);

        await lease.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquirePollingLeaseAsync_WhenExclusiveOpActive_ReturnsNullImmediately()
    {
        // Arrange
        var coordinator = new DeviceOperationCoordinator();

        // Act & Assert
        await using (var exclusiveLease = await coordinator.AcquireExclusiveAsync(DeviceOperation.DeployRules))
        {
            Assert.True(coordinator.IsExclusiveOperationActive);

            // Polling lease should return null immediately without blocking
            var pollingLease = await coordinator.TryAcquirePollingLeaseAsync();
            Assert.Null(pollingLease);
        }

        // After exclusive lease disposed, polling should succeed
        Assert.False(coordinator.IsExclusiveOperationActive);

        var restoredLease = await coordinator.TryAcquirePollingLeaseAsync();
        Assert.NotNull(restoredLease);
        await restoredLease.DisposeAsync();
    }

    [Fact]
    public async Task AcquireExclusiveAsync_WhenAnotherExclusiveHeld_QueuesAndExecutesSequentially()
    {
        // Arrange
        var coordinator = new DeviceOperationCoordinator();
        var executionOrder = new List<string>();

        // Act
        var lease1 = await coordinator.AcquireExclusiveAsync(DeviceOperation.DeployRules);
        executionOrder.Add("Op1-Started");

        var op2Task = Task.Run(async () =>
        {
            await using var lease2 = await coordinator.AcquireExclusiveAsync(DeviceOperation.SystemCommand);
            executionOrder.Add("Op2-Executed");
        });

        // Small delay to ensure op2 is waiting on queue
        await Task.Delay(50);
        executionOrder.Add("Op1-Finishing");
        await lease1.DisposeAsync();

        await op2Task;

        // Assert
        Assert.Equal(new[] { "Op1-Started", "Op1-Finishing", "Op2-Executed" }, executionOrder);
        Assert.False(coordinator.IsExclusiveOperationActive);
    }

    [Fact]
    public async Task ExclusiveLease_WhenDisposedTwice_IsIdempotent()
    {
        // Arrange
        var coordinator = new DeviceOperationCoordinator();
        var lease = await coordinator.AcquireExclusiveAsync(DeviceOperation.FactoryReset);
        Assert.True(coordinator.IsExclusiveOperationActive);

        // Act
        await lease.DisposeAsync();
        await lease.DisposeAsync(); // Second dispose should be safe and no-op

        // Assert
        Assert.False(coordinator.IsExclusiveOperationActive);
        var pollingLease = await coordinator.TryAcquirePollingLeaseAsync();
        Assert.NotNull(pollingLease);
        await pollingLease.DisposeAsync();
    }
}
