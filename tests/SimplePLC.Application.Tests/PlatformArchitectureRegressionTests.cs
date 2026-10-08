using SimplePLC.Application.Mapping;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Application.Tests;

/// <summary>
/// Phase E4: Architecture Regression Protection Tests.
/// Kiểm thử hành vi (Behavioral Tests) đảm bảo các quy tắc kiến trúc Platform V1.9:
/// 1. DeviceProfileMapper là pure adapter, không quăng ngoại lệ nghiệp vụ.
/// 2. DeviceProfileBuilder là cổng kiểm soát nghiêm ngặt (strict gatekeeper).
/// 3. Vi điều khiển mang ProductVariant bất kỳ kết nối thành công không cần hard-code factory.
/// </summary>
public class PlatformArchitectureRegressionTests
{
    [Fact]
    public void DeviceProfileMapper_IsPureAdapter_NeverThrowsBusinessExceptions()
    {
        // Arrange: DTO với các giá trị bất thường (WireProfile lạ, tag vượt ngưỡng)
        var dto = new DeviceResourceInfoDto
        {
            WireProfile = 999,
            MaxRules = 500,
            RuntimeTagCount = 9999,
            DigitalInputCount = 100,
            DigitalOutputCount = 200,
            AnalogInputCount = 300,
            VirtualFlagCount = 400,
            VirtualRegisterCount = 500,
            RetentiveRegisterCount = 600,
            CounterCount = 700
        };

        // Act: Mapper chuyển đổi thuần túy thành ProductResourceProfile mà không throw Exception
        var exception = Record.Exception(() => DeviceProfileMapper.ToResourceProfile(dto));

        // Assert
        Assert.Null(exception);
        var profile = DeviceProfileMapper.ToResourceProfile(dto);
        Assert.Equal(100, profile.DigitalInputs);
        Assert.Equal(200, profile.DigitalOutputs);
        Assert.Equal(300, profile.AnalogInputs);
    }

    [Fact]
    public void DeviceProfileBuilder_IsStrictDomainGatekeeper_RejectsInvalidWireProfiles()
    {
        // 1. Từ chối WireProfile != 1
        var profile = ProductResourceProfile.DefaultRemoteIo;

        bool built = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 99, // WireProfile 99 is unsupported
            maxRules: 100,
            runtimeTagCount: 124,
            resources: profile,
            out var product,
            out var error
        );

        Assert.False(built);
        Assert.Null(product);
        Assert.NotNull(error);
        Assert.Contains("wire profile", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeviceProfileBuilder_IsStrictDomainGatekeeper_RejectsExceedingV1PlatformLimits()
    {
        // 2. Từ chối DigitalInputs > 8
        var profileExcessDi = new ProductResourceProfile(
            DigitalInputs: 9, // Vượt quá 8 DI của Wire Profile V1
            DigitalOutputs: 8,
            AnalogInputs: 4,
            VirtualFlags: 32,
            VirtualRegisters: 32,
            RetentiveRegisters: 32,
            Counters: 8
        );

        bool built = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 100,
            runtimeTagCount: (ushort)profileExcessDi.TotalTags,
            resources: profileExcessDi,
            out var product,
            out var error
        );

        Assert.False(built);
        Assert.Null(product);
        Assert.NotNull(error);
        Assert.Contains("exceeds wire profile v1 limit", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeviceProfileBuilder_IsStrictDomainGatekeeper_RejectsRuntimeTagCountMismatch()
    {
        // 3. Từ chối khi RuntimeTagCount != tổng số tag khai báo
        var profile = ProductResourceProfile.DefaultRemoteIo; // Total = 124

        bool built = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 100,
            runtimeTagCount: 50, // Khai báo 50 nhưng tổng tài nguyên là 124
            resources: profile,
            out var product,
            out var error
        );

        Assert.False(built);
        Assert.Null(product);
        Assert.NotNull(error);
        Assert.Contains("mismatch", error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DeviceProfileMapper_TryBuildProductDefinition_SucceedsForUnknownVariant()
    {
        // Arrange: Descriptor mang Variant hoàn toàn mới (0xABCD) và ResourceInfo hợp lệ
        var descriptor = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = 0xABCD,
            HwVersionMajor = 2,
            HwVersionMinor = 0,
            HwVersionPatch = 0,
            FwVersionMajor = 1,
            FwVersionMinor = 9,
            FwVersionPatch = 0,
            ProtocolVersion = 1,
            RuleFormatVersion = 7
        };

        var resourceInfo = new DeviceResourceInfoDto
        {
            WireProfile = 1,
            MaxRules = 25,
            RuntimeTagCount = 30, // 2 + 2 + 2 + 8 + 8 + 4 + 4 = 30
            DigitalInputCount = 2,
            DigitalOutputCount = 2,
            AnalogInputCount = 2,
            VirtualFlagCount = 8,
            VirtualRegisterCount = 8,
            RetentiveRegisterCount = 4,
            CounterCount = 4
        };

        // Act
        bool success = DeviceProfileMapper.TryBuildProductDefinition(
            descriptor,
            resourceInfo,
            out var product,
            out var error
        );

        // Assert
        Assert.True(success, $"Failed to build: {error}");
        Assert.NotNull(product);
        Assert.Equal(0xABCD, product!.ProductVariant);
        Assert.Equal(25, product.MaxRules);
        Assert.Equal(30, product.Tags.Count);
        Assert.True(product.HasRuleEngine);
        Assert.True(product.HasRetentiveMemory);

        // Base Index Assertions (Dynamic Tag Layout)
        var layout = ModbusRegisterMap.ComputeLayout(
            resourceInfo.DigitalInputCount,
            resourceInfo.DigitalOutputCount,
            resourceInfo.AnalogInputCount,
            resourceInfo.VirtualFlagCount,
            resourceInfo.VirtualRegisterCount,
            resourceInfo.RetentiveRegisterCount,
            resourceInfo.CounterCount
        );

        Assert.Equal("DI0", product.FindTagByIndex(layout.DiBase)?.Name);
        Assert.Equal("DO0", product.FindTagByIndex(layout.DoBase)?.Name);
        Assert.Equal("AI0", product.FindTagByIndex(layout.AiBase)?.Name);
        Assert.Equal("VFLAG0", product.FindTagByIndex(layout.VflagBase)?.Name);
        Assert.Equal("VREG0", product.FindTagByIndex(layout.VregBase)?.Name);
        Assert.Equal("VREG_RETAIN0", product.FindTagByIndex(layout.VregRetainBase)?.Name);
        Assert.Equal("COUNTER0", product.FindTagByIndex(layout.CounterBase)?.Name);
    }
}
