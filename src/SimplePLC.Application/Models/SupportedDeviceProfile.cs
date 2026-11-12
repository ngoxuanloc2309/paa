using SimplePLC.Domain.Enums;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Application.Models;

/// <summary>
/// Hồ sơ năng lực (Capability Profile) của một biến thể thiết bị được hỗ trợ bởi SimplePLC.
/// Dùng cho Capability Registry trong IDeviceCompatibilityValidator.
/// </summary>
public sealed record SupportedDeviceProfile(
    ushort DeviceClass,
    ushort DeviceVariant,
    ushort ProtocolVersion,
    ushort RuleFormatVersion,
    string ProfileName = "")
{
    public bool Matches(DeviceDescriptorDto descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return (ushort)descriptor.DeviceClass == DeviceClass
            && descriptor.DeviceVariant == DeviceVariant
            && descriptor.ProtocolVersion == ProtocolVersion
            && descriptor.RuleFormatVersion == RuleFormatVersion;
    }

    /// <summary>
    /// Profile chuẩn cho biến thể Remote I/O SYNX-RIO-884 V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile RemoteIoRio884V2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant: (ushort)SPLC_RemoteIoVariant.VARIANT_RIO_884,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "SynaptiX Remote I/O (SYNX-RIO-884) V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho biến thể Remote I/O Wi-Fi SYNX-RIO-440-WF V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile RemoteIoRio440WfV2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant: (ushort)SPLC_RemoteIoVariant.VARIANT_RIO_440_WF,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "SynaptiX Wi-Fi I/O (SYNX-RIO-440-WF) V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho biến thể Remote I/O Compact SYNX-RIO-440 V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile RemoteIoRio440V2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant: (ushort)SPLC_RemoteIoVariant.VARIANT_RIO_440,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "SynaptiX Remote I/O (SYNX-RIO-440) V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho biến thể Remote I/O Relay SYNX-RIO-880 V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile RemoteIoRio880V2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant: (ushort)SPLC_RemoteIoVariant.VARIANT_RIO_880,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "SynaptiX Remote I/O (SYNX-RIO-880) V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho biến thể Remote I/O Zigbee SYNX-RIO-440-ZB V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile RemoteIoRio440ZbV2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant: (ushort)SPLC_RemoteIoVariant.VARIANT_RIO_440_ZB,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "SynaptiX Zigbee I/O (SYNX-RIO-440-ZB) V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho biến thể Gateway SYNX-GW-220 V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile GatewayGw220V2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.GATEWAY,
            DeviceVariant: (ushort)SPLC_GatewayVariant.VARIANT_GW_220,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "SynaptiX Gateway (SYNX-GW-220) V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho biến thể Data Logger SYNX-DL-424 V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile DataloggerDl424V2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.DATALOGGER,
            DeviceVariant: (ushort)SPLC_DataloggerVariant.VARIANT_DL_424,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "SynaptiX Data Logger (SYNX-DL-424) V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho Bộ điều khiển lập trình được V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile ControllerV2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.CONTROLLER,
            DeviceVariant: 1,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "Programmable Controller V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho thiết bị SynaptiX DTU SYNX-DTU-485 (4x RS-485 to Net: MQTT, HTTP, TCP).
    /// </summary>
    public static SupportedDeviceProfile Dtu485V2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.DTU,
            DeviceVariant: (ushort)SPLC_DtuVariant.VARIANT_DTU_485,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "SynaptiX DTU (SYNX-DTU-485) V2.0"
        );

    /// <summary>
    /// Danh sách tất cả các profile mặc định được SimplePLC V2.0 hỗ trợ.
    /// </summary>
    public static IReadOnlyList<SupportedDeviceProfile> DefaultProfiles => new[]
    {
        RemoteIoRio884V2_0,
        RemoteIoRio440WfV2_0,
        RemoteIoRio440V2_0,
        RemoteIoRio880V2_0,
        RemoteIoRio440ZbV2_0,
        GatewayGw220V2_0,
        DataloggerDl424V2_0,
        ControllerV2_0,
        Dtu485V2_0
    };
}
