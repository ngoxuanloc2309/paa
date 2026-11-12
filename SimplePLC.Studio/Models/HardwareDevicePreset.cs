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
    public string McuTarget { get; init; } = "SynaptiX SYNX-RIO-884";

    public ushort DigitalInputs { get; init; } = 8;
    public ushort DigitalOutputs { get; init; } = 8;
    public ushort AnalogInputs { get; init; } = 4;
    public ushort VirtualFlags { get; init; } = 32;
    public ushort VirtualRegisters { get; init; } = 32;
    public ushort RetentiveRegisters { get; init; } = 32;
    public ushort Counters { get; init; } = 8;
    public ushort MaxRules { get; init; } = 100;
    public ushort PhysicalRs485PortCount { get; init; } = 2;
    public SPLC_HardwareCapabilities HardwareCapabilities { get; init; } = SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE;

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
            protocolVersion: DeviceProfileBuilder.ProtocolVersionV2,
            wireProfile: DeviceProfileBuilder.WireProfileV2,
            maxRules: MaxRules,
            runtimeTagCount: (ushort)resources.TotalTags,
            resources: resources,
            hardwareCapabilities: HardwareCapabilities,
            physicalRs485PortCount: PhysicalRs485PortCount);
    }

    /// <summary>
    /// Danh sách các preset mẫu định nghĩa chuẩn theo các họ thiết bị thương mại SynaptiX.
    /// </summary>
    public static IReadOnlyList<HardwareDevicePreset> BuiltInPresets { get; } = new List<HardwareDevicePreset>
    {
        // 1. REMOTE_IO - Chuẩn 8DI / 8DO / 4AI (Mặc định)
        new()
        {
            Id = "SYNX_RIO_884",
            DisplayName = "SynaptiX SYNX-RIO-884 (Remote I/O · 8DI / 8DO / 4AI)",
            DescriptionKey = "PresetRio884Desc",
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = (ushort)SPLC_RemoteIoVariant.VARIANT_RIO_884,
            McuTarget = "SynaptiX SYNX-RIO-884",
            DigitalInputs = 8,
            DigitalOutputs = 8,
            AnalogInputs = 4,
            VirtualFlags = 32,
            VirtualRegisters = 32,
            RetentiveRegisters = 32,
            Counters = 8,
            MaxRules = 100,
            HardwareCapabilities = SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.MODBUS_MASTER
        },

        // 2. REMOTE_IO - 4DI / 4DO Compact
        new()
        {
            Id = "SYNX_RIO_440",
            DisplayName = "SynaptiX SYNX-RIO-440 (Remote I/O · 4DI / 4DO)",
            DescriptionKey = "PresetRio440Desc",
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = (ushort)SPLC_RemoteIoVariant.VARIANT_RIO_440,
            McuTarget = "SynaptiX SYNX-RIO-440",
            DigitalInputs = 4,
            DigitalOutputs = 4,
            AnalogInputs = 0,
            VirtualFlags = 16,
            VirtualRegisters = 16,
            RetentiveRegisters = 16,
            Counters = 4,
            MaxRules = 50,
            HardwareCapabilities = SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.MODBUS_MASTER
        },

        // 3. REMOTE_IO - 8DI / 8DO
        new()
        {
            Id = "SYNX_RIO_880",
            DisplayName = "SynaptiX SYNX-RIO-880 (Remote I/O · 8DI / 8DO)",
            DescriptionKey = "PresetRio880Desc",
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = (ushort)SPLC_RemoteIoVariant.VARIANT_RIO_880,
            McuTarget = "SynaptiX SYNX-RIO-880",
            DigitalInputs = 8,
            DigitalOutputs = 8,
            AnalogInputs = 0,
            VirtualFlags = 32,
            VirtualRegisters = 32,
            RetentiveRegisters = 32,
            Counters = 8,
            MaxRules = 100,
            HardwareCapabilities = SPLC_HardwareCapabilities.BASE_LOGIC | SPLC_HardwareCapabilities.RETENTIVE | SPLC_HardwareCapabilities.MODBUS_MASTER
        },

        // 4. REMOTE_IO - Wi-Fi Wireless 4DI / 4DO
        new()
        {
            Id = "SYNX_RIO_440_WF",
            DisplayName = "SynaptiX SYNX-RIO-440-WF (Remote I/O · Wi-Fi · 4DI / 4DO)",
            DescriptionKey = "PresetRio440WfDesc",
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = (ushort)SPLC_RemoteIoVariant.VARIANT_RIO_440_WF,
            McuTarget = "SynaptiX SYNX-RIO-440-WF",
            DigitalInputs = 4,
            DigitalOutputs = 4,
            AnalogInputs = 0,
            VirtualFlags = 16,
            VirtualRegisters = 32,
            RetentiveRegisters = 16,
            Counters = 4,
            MaxRules = 50,
            HardwareCapabilities = SPLC_HardwareCapabilities.BASE_LOGIC |
                                   SPLC_HardwareCapabilities.RETENTIVE |
                                   SPLC_HardwareCapabilities.MODBUS_SLAVE |
                                   SPLC_HardwareCapabilities.MODBUS_MASTER |
                                   SPLC_HardwareCapabilities.MODBUS_TCP |
                                   SPLC_HardwareCapabilities.WIFI |
                                   SPLC_HardwareCapabilities.MQTT_PUB |
                                   SPLC_HardwareCapabilities.MQTT_SUB
        },

        // 5. REMOTE_IO - Wireless Zigbee I/O 4DI / 4DO
        new()
        {
            Id = "SYNX_RIO_440_ZB",
            DisplayName = "SynaptiX SYNX-RIO-440-ZB (Remote I/O · Zigbee · 4DI / 4DO)",
            DescriptionKey = "PresetRio440ZbDesc",
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = (ushort)SPLC_RemoteIoVariant.VARIANT_RIO_440_ZB,
            McuTarget = "SynaptiX SYNX-RIO-440-ZB",
            DigitalInputs = 4,
            DigitalOutputs = 4,
            AnalogInputs = 0,
            VirtualFlags = 16,
            VirtualRegisters = 32,
            RetentiveRegisters = 16,
            Counters = 4,
            MaxRules = 50,
            HardwareCapabilities = SPLC_HardwareCapabilities.BASE_LOGIC |
                                   SPLC_HardwareCapabilities.RETENTIVE |
                                   SPLC_HardwareCapabilities.MODBUS_SLAVE |
                                   SPLC_HardwareCapabilities.MODBUS_MASTER |
                                   SPLC_HardwareCapabilities.ZIGBEE
        },

        // 6. DATALOGGER - Sensor Hub 4DI / 2DO / 4AI
        new()
        {
            Id = "SYNX_DL_424",
            DisplayName = "SynaptiX SYNX-DL-424 (Data Logger · 4DI / 2DO / 4AI)",
            DescriptionKey = "PresetDataloggerDesc",
            DeviceClass = SPLC_DeviceClass.DATALOGGER,
            DeviceVariant = (ushort)SPLC_DataloggerVariant.VARIANT_DL_424,
            McuTarget = "SynaptiX SYNX-DL-424",
            DigitalInputs = 4,
            DigitalOutputs = 2,
            AnalogInputs = 4,
            VirtualFlags = 16,
            VirtualRegisters = 32,
            RetentiveRegisters = 32,
            Counters = 4,
            MaxRules = 50,
            PhysicalRs485PortCount = 2,
            HardwareCapabilities = SPLC_HardwareCapabilities.BASE_LOGIC |
                                   SPLC_HardwareCapabilities.RETENTIVE |
                                   SPLC_HardwareCapabilities.RTC |
                                   SPLC_HardwareCapabilities.SD_DATALOG |
                                   SPLC_HardwareCapabilities.MODBUS_MASTER |
                                   SPLC_HardwareCapabilities.WIFI |
                                   SPLC_HardwareCapabilities.MQTT_PUB |
                                   SPLC_HardwareCapabilities.MQTT_SUB
        },

        // 7. GATEWAY - RS485 / Ethernet / Wi-Fi
        new()
        {
            Id = "SYNX_GW_220",
            DisplayName = "SynaptiX SYNX-GW-220 (Gateway · 2DI / 2DO)",
            DescriptionKey = "PresetGatewayDesc",
            DeviceClass = SPLC_DeviceClass.GATEWAY,
            DeviceVariant = (ushort)SPLC_GatewayVariant.VARIANT_GW_220,
            McuTarget = "SynaptiX SYNX-GW-220",
            DigitalInputs = 2,
            DigitalOutputs = 2,
            AnalogInputs = 0,
            VirtualFlags = 32,
            VirtualRegisters = 32,
            RetentiveRegisters = 32,
            Counters = 4,
            MaxRules = 50,
            PhysicalRs485PortCount = 2,
            HardwareCapabilities = SPLC_HardwareCapabilities.BASE_LOGIC |
                                   SPLC_HardwareCapabilities.RETENTIVE |
                                   SPLC_HardwareCapabilities.ETHERNET |
                                   SPLC_HardwareCapabilities.WIFI |
                                   SPLC_HardwareCapabilities.MQTT_PUB |
                                   SPLC_HardwareCapabilities.MQTT_SUB |
                                   SPLC_HardwareCapabilities.MODBUS_MASTER |
                                   SPLC_HardwareCapabilities.MODBUS_TCP |
                                   SPLC_HardwareCapabilities.HTTP_CLIENT |
                                   SPLC_HardwareCapabilities.TRANSPARENT_STREAM
        },

        // 8. DTU - Industrial RS-485 Gateway & Data Transfer Unit (TCP / MQTT / HTTP)
        new()
        {
            Id = "SYNX_DTU_485",
            DisplayName = "SynaptiX SYNX-DTU-485 (DTU · 4x RS-485)",
            DescriptionKey = "PresetDtuDesc",
            DeviceClass = SPLC_DeviceClass.DTU,
            DeviceVariant = (ushort)SPLC_DtuVariant.VARIANT_DTU_485,
            McuTarget = "SynaptiX SYNX-DTU-485",
            DigitalInputs = 0,
            DigitalOutputs = 0,
            AnalogInputs = 0,
            VirtualFlags = 32,
            VirtualRegisters = 32,
            RetentiveRegisters = 32,
            Counters = 4,
            MaxRules = 50,
            PhysicalRs485PortCount = 4,
            HardwareCapabilities = SPLC_HardwareCapabilities.ETHERNET |
                                   SPLC_HardwareCapabilities.WIFI |
                                   SPLC_HardwareCapabilities.MQTT_PUB |
                                   SPLC_HardwareCapabilities.MQTT_SUB |
                                   SPLC_HardwareCapabilities.MODBUS_MASTER |
                                   SPLC_HardwareCapabilities.MODBUS_TCP |
                                   SPLC_HardwareCapabilities.HTTP_CLIENT |
                                   SPLC_HardwareCapabilities.TRANSPARENT_STREAM
        }
    };
}
