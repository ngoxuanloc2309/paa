using System;
using System.Collections.Generic;
using SimplePLC.Domain.Enums;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Models;

namespace SimplePLC.Domain.Builders;

/// <summary>
/// Trình dựng ProductDefinition động tại runtime theo Wire Profile V1 (Contract V1.9).
/// MCU là nguồn chân lý duy nhất (Source of Truth) cho tài nguyên thiết bị.
/// </summary>
public static class DeviceProfileBuilder
{
    // Bất biến kiến trúc Wire Profile: Trần tài nguyên tối đa
    public const ushort WireProfileV1 = 1;
    public const ushort WireProfileV2 = 2;
    public const ushort ProtocolVersionV1 = 1;
    public const ushort ProtocolVersionV2 = 2;
    public const ushort MaxRulesV1 = 100;
    public const ushort MaxRuntimeTagsV1 = 128;

    public const ushort MaxDigitalInputs = 8;
    public const ushort MaxDigitalOutputs = 8;
    public const ushort MaxAnalogInputs = 4;
    public const ushort MaxVirtualFlags = 32;
    public const ushort MaxVirtualRegisters = 32;
    public const ushort MaxRetentiveRegisters = 32;
    public const ushort MaxCounters = 8;

    /// <summary>
    /// Thử thẩm định và dựng ProductDefinition từ metadata thiết bị khai báo.
    /// Trả về true nếu hợp lệ; trả về false kèm errorMessage nếu vi phạm Wire Profile V1 hoặc V2.
    /// </summary>
    public static bool TryBuild(
        ushort deviceClass,
        ushort productVariant,
        ushort protocolVersion,
        ushort wireProfile,
        ushort maxRules,
        ushort runtimeTagCount,
        ProductResourceProfile resources,
        out ProductDefinition? product,
        out string? errorMessage)
    {
        product = null;
        errorMessage = null;

        if (protocolVersion != ProtocolVersionV1 && protocolVersion != ProtocolVersionV2)
        {
            errorMessage = $"Unsupported protocol version {protocolVersion}. Only Protocol Version {ProtocolVersionV1} and {ProtocolVersionV2} are supported.";
            return false;
        }

        if (wireProfile != WireProfileV1 && wireProfile != WireProfileV2)
        {
            errorMessage = $"Unsupported wire profile {wireProfile}. Only Wire Profile {WireProfileV1} and {WireProfileV2} are supported.";
            return false;
        }

        if (maxRules > MaxRulesV1)
        {
            errorMessage = $"Declared max rules ({maxRules}) exceeds Wire Profile V1 capacity limit of {MaxRulesV1}.";
            return false;
        }

        if (resources.DigitalInputs > MaxDigitalInputs)
        {
            errorMessage = $"Declared DI count ({resources.DigitalInputs}) exceeds Wire Profile V1 limit ({MaxDigitalInputs}).";
            return false;
        }

        if (resources.DigitalOutputs > MaxDigitalOutputs)
        {
            errorMessage = $"Declared DO count ({resources.DigitalOutputs}) exceeds Wire Profile V1 limit ({MaxDigitalOutputs}).";
            return false;
        }

        if (resources.AnalogInputs > MaxAnalogInputs)
        {
            errorMessage = $"Declared AI count ({resources.AnalogInputs}) exceeds Wire Profile V1 limit ({MaxAnalogInputs}).";
            return false;
        }

        if (resources.VirtualFlags > MaxVirtualFlags)
        {
            errorMessage = $"Declared VFLAG count ({resources.VirtualFlags}) exceeds Wire Profile V1 limit ({MaxVirtualFlags}).";
            return false;
        }

        if (resources.VirtualRegisters > MaxVirtualRegisters)
        {
            errorMessage = $"Declared VREG count ({resources.VirtualRegisters}) exceeds Wire Profile V1 limit ({MaxVirtualRegisters}).";
            return false;
        }

        if (resources.RetentiveRegisters > MaxRetentiveRegisters)
        {
            errorMessage = $"Declared VREG_RETAIN count ({resources.RetentiveRegisters}) exceeds Wire Profile V1 limit ({MaxRetentiveRegisters}).";
            return false;
        }

        if (resources.Counters > MaxCounters)
        {
            errorMessage = $"Declared COUNTER count ({resources.Counters}) exceeds Wire Profile V1 limit ({MaxCounters}).";
            return false;
        }

        if (resources.TotalTags > MaxRuntimeTagsV1)
        {
            errorMessage = $"Total declared tags ({resources.TotalTags}) exceeds Wire Profile V1 capacity limit of {MaxRuntimeTagsV1}.";
            return false;
        }

        if (runtimeTagCount != resources.TotalTags)
        {
            errorMessage = $"RuntimeTagCount mismatch: device reported {runtimeTagCount} but sum of declared resource slots is {resources.TotalTags}.";
            return false;
        }

        // Sinh danh sách TagDefinition theo các base index cố định của Wire Profile V1
        var tags = GenerateTags(resources);

        string productName = GetDefaultProductName(deviceClass, productVariant, resources);

        product = new ProductDefinition(
            productName,
            deviceClass,
            productVariant,
            maxRules,
            resources,
            tags,
            wireProfile: wireProfile);

        return true;
    }

    /// <summary>
    /// Dựng ProductDefinition hoặc ném ngoại lệ InvalidOperationException nếu metadata không hợp lệ.
    /// </summary>
    public static ProductDefinition Build(
        ushort deviceClass,
        ushort productVariant,
        ushort protocolVersion,
        ushort wireProfile,
        ushort maxRules,
        ushort runtimeTagCount,
        ProductResourceProfile resources)
    {
        if (!TryBuild(deviceClass, productVariant, protocolVersion, wireProfile, maxRules, runtimeTagCount, resources, out var product, out var error))
        {
            throw new InvalidOperationException($"Failed to build product profile: {error}");
        }
        return product!;
    }

    /// <summary>
    /// Sinh danh sách TagDefinition động từ ProductResourceProfile.
    /// Đảm bảo tuyệt đối các dải base index không bị xê dịch.
    /// </summary>
    public static List<TagDefinition> GenerateTags(ProductResourceProfile resources)
    {
        var layout = new TagLayoutMap(
            resources.DigitalInputs,
            resources.DigitalOutputs,
            resources.AnalogInputs,
            resources.VirtualFlags,
            resources.VirtualRegisters,
            resources.RetentiveRegisters,
            resources.Counters);

        var list = new List<TagDefinition>(resources.TotalTags);

        for (ushort i = 0; i < resources.DigitalInputs; i++)
            list.Add(new TagDefinition((ushort)(layout.DiBase + i), $"DI{i}", TagKind.DiscreteInput, TagDataType.Boolean, isReadOnly: true, $"Digital Input {i}"));

        for (ushort i = 0; i < resources.DigitalOutputs; i++)
            list.Add(new TagDefinition((ushort)(layout.DoBase + i), $"DO{i}", TagKind.DiscreteOutput, TagDataType.Boolean, isReadOnly: false, $"Digital Output {i}"));

        for (ushort i = 0; i < resources.AnalogInputs; i++)
            list.Add(new TagDefinition((ushort)(layout.AiBase + i), $"AI{i}", TagKind.AnalogInput, TagDataType.Int32, isReadOnly: true, $"Analog Input {i}"));

        for (ushort i = 0; i < resources.VirtualFlags; i++)
            list.Add(new TagDefinition((ushort)(layout.VflagBase + i), $"VFLAG{i}", TagKind.VirtualFlag, TagDataType.Boolean, isReadOnly: false, $"Virtual Flag {i}"));

        for (ushort i = 0; i < resources.VirtualRegisters; i++)
            list.Add(new TagDefinition((ushort)(layout.VregBase + i), $"VREG{i}", TagKind.VirtualRegister, TagDataType.Int32, isReadOnly: false, $"Virtual Register {i}"));

        for (ushort i = 0; i < resources.RetentiveRegisters; i++)
            list.Add(new TagDefinition((ushort)(layout.VregRetainBase + i), $"VREG_RETAIN{i}", TagKind.VirtualRegisterRetain, TagDataType.Int32, isReadOnly: false, $"Retain Virtual Register {i}"));

        for (ushort i = 0; i < resources.Counters; i++)
            list.Add(new TagDefinition((ushort)(layout.CounterBase + i), $"COUNTER{i}", TagKind.Counter, TagDataType.Int32, isReadOnly: false, $"Counter {i}"));

        return list;
    }

    private static string GetDefaultProductName(ushort deviceClass, ushort productVariant, ProductResourceProfile resources)
    {
        string className = deviceClass switch
        {
            1 => "Remote I/O",
            2 => "Data Logger",
            3 => "Gateway",
            4 => "Controller",
            _ => "SimplePLC Device"
        };

        return $"{className} (Var:{productVariant} - {resources.TotalTags} Tags)";
    }
}
