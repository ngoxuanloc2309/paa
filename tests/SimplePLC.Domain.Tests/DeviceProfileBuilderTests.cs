using System;
using System.Linq;
using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using Xunit;

namespace SimplePLC.Domain.Tests;

/// <summary>
/// Bộ kiểm thử hồi quy theo Mục 20 của tài liệu Self-Describing Device Profile V1.9.
/// </summary>
public class DeviceProfileBuilderTests
{
    [Fact]
    public void RemoteIo_Profile_Generates124Tags()
    {
        // Arrange
        var resources = ProductResourceProfile.DefaultRemoteIo;

        // Act
        var product = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 100,
            runtimeTagCount: 124,
            resources: resources);

        // Assert
        Assert.NotNull(product);
        Assert.Equal(124, product.Tags.Count);
        Assert.Equal(100, product.MaxRules);
        Assert.True(product.HasRuleEngine);
        Assert.True(product.HasRetentiveMemory);

        // Kiểm tra số lượng từng nhóm
        Assert.Equal(8, product.Tags.Count(t => t.Kind == TagKind.DiscreteInput));
        Assert.Equal(8, product.Tags.Count(t => t.Kind == TagKind.DiscreteOutput));
        Assert.Equal(4, product.Tags.Count(t => t.Kind == TagKind.AnalogInput));
        Assert.Equal(32, product.Tags.Count(t => t.Kind == TagKind.VirtualFlag));
        Assert.Equal(32, product.Tags.Count(t => t.Kind == TagKind.VirtualRegister));
        Assert.Equal(32, product.Tags.Count(t => t.Kind == TagKind.VirtualRegisterRetain));
        Assert.Equal(8, product.Tags.Count(t => t.Kind == TagKind.Counter));
    }

    [Fact]
    public void SmallProduct_GeneratesOnlyDeclaredTags()
    {
        // Arrange: Small IO: 4 DI, 4 DO, 2 AI, 16 VFLAG, 16 VREG, 8 RETAIN, 4 COUNTER (Tổng: 54 tags)
        var smallResources = new ProductResourceProfile(
            DigitalInputs: 4,
            DigitalOutputs: 4,
            AnalogInputs: 2,
            VirtualFlags: 16,
            VirtualRegisters: 16,
            RetentiveRegisters: 8,
            Counters: 4);

        // Act
        var product = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 2,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 50,
            runtimeTagCount: 54,
            resources: smallResources);

        // Assert: Chỉ sinh chính xác 54 tags khai báo
        Assert.Equal(54, product.Tags.Count);
        Assert.Equal(4, product.Tags.Count(t => t.Kind == TagKind.DiscreteInput));
        Assert.Equal(4, product.Tags.Count(t => t.Kind == TagKind.DiscreteOutput));
        Assert.Equal(2, product.Tags.Count(t => t.Kind == TagKind.AnalogInput));
        Assert.Equal(16, product.Tags.Count(t => t.Kind == TagKind.VirtualFlag));
        Assert.Equal(16, product.Tags.Count(t => t.Kind == TagKind.VirtualRegister));
        Assert.Equal(8, product.Tags.Count(t => t.Kind == TagKind.VirtualRegisterRetain));
        Assert.Equal(4, product.Tags.Count(t => t.Kind == TagKind.Counter));
    }

    [Fact]
    public void SmallProduct_VFlag0_MatchesDynamicLayout()
    {
        var small = new ProductResourceProfile(4, 4, 2, 8, 8, 8, 4);
        var layout = new SimplePLC.Protocol.Models.TagLayoutMap(small.DigitalInputs, small.DigitalOutputs, small.AnalogInputs, small.VirtualFlags, small.VirtualRegisters, small.RetentiveRegisters, small.Counters);
        var tags = DeviceProfileBuilder.GenerateTags(small);

        var vflag0 = tags.FirstOrDefault(t => t.Name == "VFLAG0");
        Assert.NotNull(vflag0);
        Assert.Equal(layout.VflagBase, vflag0.TagIndex);
        Assert.Equal(10, vflag0.TagIndex);
    }

    [Fact]
    public void SmallProduct_VReg0_MatchesDynamicLayout()
    {
        var small = new ProductResourceProfile(4, 4, 2, 8, 8, 8, 4);
        var layout = new SimplePLC.Protocol.Models.TagLayoutMap(small.DigitalInputs, small.DigitalOutputs, small.AnalogInputs, small.VirtualFlags, small.VirtualRegisters, small.RetentiveRegisters, small.Counters);
        var tags = DeviceProfileBuilder.GenerateTags(small);

        var vreg0 = tags.FirstOrDefault(t => t.Name == "VREG0");
        Assert.NotNull(vreg0);
        Assert.Equal(layout.VregBase, vreg0.TagIndex);
        Assert.Equal(18, vreg0.TagIndex);
    }

    [Fact]
    public void SmallProduct_Retain0_MatchesDynamicLayout()
    {
        var small = new ProductResourceProfile(4, 4, 2, 8, 8, 8, 4);
        var layout = new SimplePLC.Protocol.Models.TagLayoutMap(small.DigitalInputs, small.DigitalOutputs, small.AnalogInputs, small.VirtualFlags, small.VirtualRegisters, small.RetentiveRegisters, small.Counters);
        var tags = DeviceProfileBuilder.GenerateTags(small);

        var retain0 = tags.FirstOrDefault(t => t.Name == "VREG_RETAIN0");
        Assert.NotNull(retain0);
        Assert.Equal(layout.VregRetainBase, retain0.TagIndex);
        Assert.Equal(26, retain0.TagIndex);
    }

    [Fact]
    public void SmallProduct_Counter0_MatchesDynamicLayout()
    {
        var small = new ProductResourceProfile(4, 4, 2, 8, 8, 8, 4);
        var layout = new SimplePLC.Protocol.Models.TagLayoutMap(small.DigitalInputs, small.DigitalOutputs, small.AnalogInputs, small.VirtualFlags, small.VirtualRegisters, small.RetentiveRegisters, small.Counters);
        var tags = DeviceProfileBuilder.GenerateTags(small);

        var counter0 = tags.FirstOrDefault(t => t.Name == "COUNTER0");
        Assert.NotNull(counter0);
        Assert.Equal(layout.CounterBase, counter0.TagIndex);
        Assert.Equal(34, counter0.TagIndex);
    }

    [Fact]
    public void UnknownVariant_WithValidProfile_IsAccepted()
    {
        // Variant 9999 chưa từng định nghĩa trong enum nhưng profile hợp lệ
        var profile = new ProductResourceProfile(8, 8, 4, 16, 16, 16, 4); // 72 tags
        bool success = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 9999,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 60,
            runtimeTagCount: 72,
            resources: profile,
            out var product,
            out var error);

        Assert.True(success);
        Assert.Null(error);
        Assert.NotNull(product);
        Assert.Equal(9999, product.ProductVariant);
        Assert.Equal(72, product.Tags.Count);
    }

    [Fact]
    public void UnsupportedWireProfile_IsRejected()
    {
        var profile = ProductResourceProfile.DefaultRemoteIo;
        bool success = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 99, // Wire profile 99 không hỗ trợ
            maxRules: 100,
            runtimeTagCount: 124,
            resources: profile,
            out var product,
            out var error);

        Assert.False(success);
        Assert.Null(product);
        Assert.Contains("Unsupported wire profile 99", error);
    }

    [Fact]
    public void SupportedWireProfileV2_BuildsSuccessfully_AndEnablesV2Capabilities()
    {
        var profile = ProductResourceProfile.DefaultRemoteIo;
        bool success = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 2, // Wire profile 2 hỗ trợ trong V2.0
            maxRules: 100,
            runtimeTagCount: 124,
            resources: profile,
            out var product,
            out var error);

        Assert.True(success);
        Assert.NotNull(product);
        Assert.Equal(2, product.WireProfile);
        Assert.True(product.SupportsDedicatedFunctionBlocks);
        Assert.True(product.SupportsDiagnosticControl);
    }

    [Fact]
    public void SupportedProtocolVersionV2_BuildsSuccessfully()
    {
        var profile = ProductResourceProfile.DefaultRemoteIo;
        bool success = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 2, // Protocol version 2
            wireProfile: 2,     // Wire profile 2
            maxRules: 100,
            runtimeTagCount: 124,
            resources: profile,
            out var product,
            out var error);

        Assert.True(success);
        Assert.NotNull(product);
        Assert.Equal(2, product.WireProfile);
    }

    [Fact]
    public void ResourceCountAboveCapacity_IsRejected()
    {
        // 9 DI vượt quá trần Wire Profile V1 (8)
        var invalidProfile = new ProductResourceProfile(9, 8, 4, 32, 32, 32, 8);
        bool success = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 100,
            runtimeTagCount: 125,
            resources: invalidProfile,
            out var product,
            out var error);

        Assert.False(success);
        Assert.Null(product);
        Assert.Contains("Declared DI count (9) exceeds Wire Profile V1 limit", error);
    }

    [Fact]
    public void RuntimeTagCountMismatch_IsRejected()
    {
        var profile = ProductResourceProfile.DefaultRemoteIo; // 124 tags
        bool success = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 100,
            runtimeTagCount: 120, // Sai lệch: MCU báo 120 nhưng tổng khai báo là 124
            resources: profile,
            out var product,
            out var error);

        Assert.False(success);
        Assert.Null(product);
        Assert.Contains("RuntimeTagCount mismatch", error);
    }

    [Fact]
    public void MaxRulesAbove100_IsRejected()
    {
        var profile = ProductResourceProfile.DefaultRemoteIo;
        bool success = DeviceProfileBuilder.TryBuild(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 101, // Vượt quá 100
            runtimeTagCount: 124,
            resources: profile,
            out var product,
            out var error);

        Assert.False(success);
        Assert.Null(product);
        Assert.Contains("exceeds Wire Profile V1 capacity limit of 100", error);
    }

    [Fact]
    public void MaxRulesZero_DisablesRuleEngineFeatures()
    {
        var noRuleProduct = new ProductResourceProfile(8, 8, 4, 16, 16, 0, 0); // 52 tags
        var product = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 0, // Không có Rule Engine
            runtimeTagCount: 52,
            resources: noRuleProduct);

        Assert.False(product.HasRuleEngine);
    }

    [Fact]
    public void RetainCountZero_DisablesRetentiveResources()
    {
        var noRetainProduct = new ProductResourceProfile(8, 8, 4, 16, 16, 0, 4); // 56 tags
        var product = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 1,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 50,
            runtimeTagCount: 56,
            resources: noRetainProduct);

        Assert.False(product.HasRetentiveMemory);
        Assert.DoesNotContain(product.Tags, t => t.Kind == TagKind.VirtualRegisterRetain);
    }

    [Fact]
    public void ProductDefinition_IsBuiltFromDeviceProfile()
    {
        var profile = new ProductResourceProfile(8, 8, 4, 32, 32, 32, 8);
        var product = DeviceProfileBuilder.Build(
            deviceClass: 1,
            productVariant: 5,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 80,
            runtimeTagCount: 124,
            resources: profile);

        Assert.Equal(1, product.DeviceClass);
        Assert.Equal(5, product.ProductVariant);
        Assert.Equal(80, product.MaxRules);
        Assert.Equal(profile, product.Resources);
        Assert.Equal(124, product.Tags.Count);
    }

    [Fact]
    public void NoProductRegistryEntry_IsRequired()
    {
        // Hoàn toàn không cần một registry hay hard-coded switch/case nào cho variant 42
        var profile = new ProductResourceProfile(2, 2, 1, 4, 4, 0, 0); // 13 tags
        var product = DeviceProfileBuilder.Build(
            deviceClass: 2, // DATALOGGER
            productVariant: 42,
            protocolVersion: 1,
            wireProfile: 1,
            maxRules: 0,
            runtimeTagCount: 13,
            resources: profile);

        Assert.NotNull(product);
        Assert.Equal(13, product.Tags.Count);
        Assert.Equal("Data Logger (Var:42 - 13 Tags)", product.ProductName);
    }
}
