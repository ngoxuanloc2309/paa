using SimplePLC.Infrastructure.Devices;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Infrastructure.Tests;

public class DeviceDescriptorAndHealthReaderTests
{
    [Fact]
    public async Task ReadDescriptorAsync_ReturnsValidDeviceDescriptor()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var reader = new DeviceDescriptorReader(client);

        var descriptor = await reader.ReadDescriptorAsync(1);

        Assert.Equal(SPLC_DeviceClass.REMOTE_IO, descriptor.DeviceClass);
        Assert.Equal((ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI, descriptor.DeviceVariant);
        Assert.Equal("1.0.0", descriptor.HwVersionString);
        Assert.Equal("1.7.0", descriptor.FwVersionString);
        Assert.Equal(1, descriptor.ProtocolVersion);
        Assert.Equal(1, descriptor.RuleFormatVersion);
    }

    [Fact]
    public async Task ReadHealthAsync_ReturnsValidDeviceHealth()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var reader = new DeviceHealthReader(client);

        var health = await reader.ReadHealthAsync(1);

        Assert.Equal(3600u, health.UptimeSeconds);
        Assert.Equal(SPLC_ResetReason.POWER_ON, health.ResetReason);
        Assert.Equal(SPLC_HealthFlags.NONE, health.HealthFlags);
        Assert.Equal(25, health.CpuLoadPercent);
        Assert.Equal(40, health.RamUsagePercent);
        Assert.Equal(10u, health.ScanTimeMs);
        Assert.Equal(10u, health.MaxScanTimeMs);
    }

    [Fact]
    public async Task ReadResourceInfoAsync_ReturnsValidDeviceResourceInfo()
    {
        var client = new FakeModbusClient();
        await client.ConnectAsync("COM1");

        var reader = new DeviceDescriptorReader(client);

        var resourceInfo = await reader.ReadResourceInfoAsync(1);

        Assert.Equal(1, resourceInfo.WireProfile);
        Assert.Equal(100, resourceInfo.MaxRules);
        Assert.Equal(124, resourceInfo.RuntimeTagCount);
        Assert.Equal(8, resourceInfo.DigitalInputCount);
        Assert.Equal(8, resourceInfo.DigitalOutputCount);
        Assert.Equal(4, resourceInfo.AnalogInputCount);
        Assert.Equal(32, resourceInfo.VirtualFlagCount);
        Assert.Equal(32, resourceInfo.VirtualRegisterCount);
        Assert.Equal(32, resourceInfo.RetentiveRegisterCount);
        Assert.Equal(8, resourceInfo.CounterCount);
        Assert.Equal(124, resourceInfo.TotalDeclaredTags);
        Assert.True(resourceInfo.IsValidDeclaredCount);
    }
}
