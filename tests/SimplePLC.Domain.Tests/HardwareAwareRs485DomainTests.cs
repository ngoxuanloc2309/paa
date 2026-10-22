using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Domain.Tests;

/// <summary>
/// Kiểm thử độc lập cho tầng Domain: Nhận diện và gán số lượng cổng RS-485 vật lý vào ProductDefinition.
/// </summary>
public sealed class HardwareAwareRs485DomainTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void DeviceProfileBuilder_WithCustomRs485PortCount_AssignsCorrectly(ushort portCount)
    {
        // TC-HW-MB-002
        var resources = ProductResourceProfile.DefaultRemoteIo;
        bool ok = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 2,
            wireProfile: 2,
            maxRules: 100,
            runtimeTagCount: 124,
            resources: resources,
            out var product,
            out var error,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.MODBUS_MASTER,
            physicalRs485PortCount: portCount);

        Assert.True(ok, error);
        Assert.NotNull(product);
        Assert.Equal(portCount, product.PhysicalRs485PortCount);
    }

    [Fact]
    public void ProductDefinition_FallbackWhenModbusMasterDisabled_ReturnsZero()
    {
        // TC-HW-MB-003: Khi không có cờ MODBUS_MASTER và không truyền portCount, mặc định là 0
        var resources = ProductResourceProfile.DefaultRemoteIo;
        var tags = DeviceProfileBuilder.GenerateTags(resources);

        var product = new ProductDefinition(
            "Compute-Only PLC",
            deviceClass: 1,
            productVariant: 1,
            maxRules: 100,
            resources: resources,
            tags: tags,
            wireProfile: 2,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE);

        Assert.False(product.SupportsModbusMaster);
        Assert.Equal(0, product.PhysicalRs485PortCount);
    }

    [Fact]
    public void ProductDefinition_FallbackWhenModbusMasterEnabled_DefaultsToTwo()
    {
        // Khi có MODBUS_MASTER nhưng firmware cũ không gửi portCount (null), mặc định 2 cổng Standard
        var resources = ProductResourceProfile.DefaultRemoteIo;
        var tags = DeviceProfileBuilder.GenerateTags(resources);

        var product = new ProductDefinition(
            "Legacy Standard PLC",
            deviceClass: 1,
            productVariant: 1,
            maxRules: 100,
            resources: resources,
            tags: tags,
            wireProfile: 2,
            hardwareCapabilities: SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.MODBUS_MASTER);

        Assert.True(product.SupportsModbusMaster);
        Assert.Equal(2, product.PhysicalRs485PortCount);
    }
}
