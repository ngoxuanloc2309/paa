using SimplePLC.Application.Models;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Validators;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

public sealed class DtuDeviceClassificationTests
{
    [Fact]
    public void StandardDeviceCompatibilityValidator_WhenDtuGatewayDescriptorProvided_ReturnsCompatible()
    {
        // Arrange
        var validator = new StandardDeviceCompatibilityValidator();
        var dtuDescriptor = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.DTU,
            DeviceVariant = (ushort)SPLC_DtuVariant.VARIANT_DTU_485,
            ProtocolVersion = 2,
            RuleFormatVersion = 7,
            HwVersionMajor = 1,
            HwVersionMinor = 0,
            HwVersionPatch = 0,
            FwVersionMajor = 2,
            FwVersionMinor = 0,
            FwVersionPatch = 0
        };

        // Act
        var result = validator.Validate(dtuDescriptor);

        // Assert
        Assert.True(result.IsCompatible);
        Assert.Equal(CompatibilityStatus.Compatible, result.Status);
    }

    [Fact]
    public void DeviceProfileBuilder_WhenDtuGatewayClassUsed_BuildsProductDefinitionWithDtuCapable()
    {
        // Arrange
        var resources = new ProductResourceProfile(0, 0, 0, 32, 32, 32, 4);

        // Act
        var success = DeviceProfileBuilder.TryBuild(
            deviceClass: (ushort)SPLC_DeviceClass.DTU,
            productVariant: (ushort)SPLC_DtuVariant.VARIANT_DTU_485,
            protocolVersion: 2,
            wireProfile: 2,
            maxRules: 50,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources,
            product: out var product,
            errorMessage: out var error,
            hardwareCapabilities: SPLC_HardwareCapabilities.ETHERNET | SPLC_HardwareCapabilities.MODBUS_MASTER | SPLC_HardwareCapabilities.HTTP_CLIENT,
            physicalRs485PortCount: 2);

        // Assert
        Assert.True(success);
        Assert.NotNull(product);
        Assert.True(product.IsDtuCapable);
        Assert.Equal((ushort)SPLC_DeviceClass.DTU, product.DeviceClass);
        Assert.Contains("DTU Gateway", product.ProductName);
    }
}
