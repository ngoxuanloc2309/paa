using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Enums;
using SimplePLC.Studio.Services;

namespace SimplePLC.Studio.Models;

/// <summary>
/// Đại diện cho một mẫu cấu hình thiết bị phần cứng mục tiêu (Device Preset)
/// Được định danh và ánh xạ chuẩn theo các define struct trong tài liệu Wire Protocol (Contract V2.0).
/// </summary>
public sealed class HardwareDevicePreset
{
    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DescriptionKey { get; init; } = string.Empty;
    public SPLC_DeviceClass DeviceClass { get; init; } = SPLC_DeviceClass.REMOTE_IO;
    public ushort DeviceVariant { get; init; } = 1;
    public string McuTarget { get; init; } = "STM32F401CCU6 (ARM Cortex-M4)";

    public ushort DigitalInputs { get; init; } = 8;
    public ushort DigitalOutputs { get; init; } = 8;
    public ushort AnalogInputs { get; init; } = 4;
    public ushort VirtualFlags { get; init; } = 32;
    public ushort VirtualRegisters { get; init; } = 32;
    public ushort RetentiveRegisters { get; init; } = 32;
    public ushort Counters { get; init; } = 8;
    public ushort MaxRules { get; init; } = 100;

    public int TotalTags => DigitalInputs + DigitalOutputs + AnalogInputs + VirtualFlags + VirtualRegisters + RetentiveRegisters + Counters;

    public ProductResourceProfile ToResourceProfile() => new(
        DigitalInputs,
        DigitalOutputs,
        AnalogInputs,
        VirtualFlags,
        VirtualRegisters,
        RetentiveRegisters,
        Counters);

    public ProductDefinition BuildProductDefinition()
    {
        var resources = ToResourceProfile();
        return DeviceProfileBuilder.Build(
            (ushort)DeviceClass,
            DeviceVariant,
            protocolVersion: DeviceProfileBuilder.ProtocolVersionV1,
            wireProfile: DeviceProfileBuilder.WireProfileV2,
            maxRules: MaxRules,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources);
    }

    /// <summary>
    /// Danh sách các preset mẫu định nghĩa chuẩn theo các họ thiết bị trong tài liệu Contract V2.0.
    /// </summary>
    public static IReadOnlyList<HardwareDevicePreset> BuiltInPresets { get; } = new List<HardwareDevicePreset>
    {
        // 1. REMOTE_IO - Chuẩn 8DI / 8DO / 4AI (Mặc định)
        new()
        {
            Id = "REMOTE_IO_8DI_8DO_4AI",
            DisplayName = "REMOTE_IO (8DI - 8DO - 4AI)",
            DescriptionKey = "PresetRemoteIo8Di8Do4AiDesc",
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = (ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI,
            McuTarget = "STM32F401CCU6 (Standard Remote I/O)",
            DigitalInputs = 8,
            DigitalOutputs = 8,
            AnalogInputs = 4,
            VirtualFlags = 32,
            VirtualRegisters = 32,
            RetentiveRegisters = 32,
            Counters = 8,
            MaxRules = 100
        },

        // 2. REMOTE_IO - Compact 4DI / 4DO
        new()
        {
            Id = "REMOTE_IO_4DI_4DO",
            DisplayName = "REMOTE_IO (Compact 4DI - 4DO)",
            DescriptionKey = "PresetRemoteIo4Di4DoDesc",
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = 3,
            McuTarget = "STM32F103 / ESP32 (Wireless Substation)",
            DigitalInputs = 4,
            DigitalOutputs = 4,
            AnalogInputs = 0,
            VirtualFlags = 16,
            VirtualRegisters = 16,
            RetentiveRegisters = 16,
            Counters = 4,
            MaxRules = 50
        },

        // 3. REMOTE_IO - Discrete 8DI / 8DO
        new()
        {
            Id = "REMOTE_IO_8DI_8DO",
            DisplayName = "REMOTE_IO (Discrete 8DI - 8DO)",
            DescriptionKey = "PresetRemoteIo8Di8DoDesc",
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = 4,
            McuTarget = "STM32F401 (Pure Discrete Relay Control)",
            DigitalInputs = 8,
            DigitalOutputs = 8,
            AnalogInputs = 0,
            VirtualFlags = 32,
            VirtualRegisters = 32,
            RetentiveRegisters = 32,
            Counters = 8,
            MaxRules = 100
        },

        // 4. DATALOGGER - Sensor Hub 4DI / 2DO / 4AI
        new()
        {
            Id = "DATALOGGER_4DI_2DO_4AI",
            DisplayName = "DATALOGGER (Sensor Hub 4DI - 2DO - 4AI)",
            DescriptionKey = "PresetDataloggerDesc",
            DeviceClass = SPLC_DeviceClass.DATALOGGER,
            DeviceVariant = (ushort)SPLC_DataloggerVariant.VARIANT_8AI,
            McuTarget = "STM32F401 / RP2040 (Environmental & Analog Logger)",
            DigitalInputs = 4,
            DigitalOutputs = 2,
            AnalogInputs = 4,
            VirtualFlags = 16,
            VirtualRegisters = 32,
            RetentiveRegisters = 32,
            Counters = 4,
            MaxRules = 50
        },

        // 5. GATEWAY - RS485 / Ethernet
        new()
        {
            Id = "GATEWAY_RS485_ETH",
            DisplayName = "GATEWAY (RS485 / Ethernet Bridge)",
            DescriptionKey = "PresetGatewayDesc",
            DeviceClass = SPLC_DeviceClass.GATEWAY,
            DeviceVariant = (ushort)SPLC_GatewayVariant.VARIANT_RS485_ETH,
            McuTarget = "STM32F407 / ESP32 (Industrial Gateway & Protocol Bridge)",
            DigitalInputs = 2,
            DigitalOutputs = 2,
            AnalogInputs = 0,
            VirtualFlags = 32,
            VirtualRegisters = 32,
            RetentiveRegisters = 32,
            Counters = 4,
            MaxRules = 50
        },

        // 6. CONTROLLER - Programmable Controller
        new()
        {
            Id = "CONTROLLER_STANDALONE",
            DisplayName = "CONTROLLER (Standalone PLC 8DI - 8DO - 4AI)",
            DescriptionKey = "PresetControllerDesc",
            DeviceClass = SPLC_DeviceClass.CONTROLLER,
            DeviceVariant = 1,
            McuTarget = "STM32F405 / ARM Cortex-M4 (Standalone Edge Controller)",
            DigitalInputs = 8,
            DigitalOutputs = 8,
            AnalogInputs = 4,
            VirtualFlags = 32,
            VirtualRegisters = 32,
            RetentiveRegisters = 32,
            Counters = 8,
            MaxRules = 100
        }
    };
}
