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
    /// Profile chuẩn cho biến thể Remote I/O 8DI-8DO-4AI V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile RemoteIo8Di8Do4AiV2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant: (ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "Remote I/O (8DI-8DO-4AI) V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho biến thể Remote I/O 16DI-16DO V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile RemoteIo16Di16DoV2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant: (ushort)SPLC_RemoteIoVariant.VARIANT_16DI_16DO,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "Remote I/O (16DI-16DO) V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho biến thể Gateway RS485-ETH V2.0 (Wire Profile V2 / simpleplc_protocol_v2_0.h).
    /// </summary>
    public static SupportedDeviceProfile GatewayRs485EthV2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.GATEWAY,
            DeviceVariant: (ushort)SPLC_GatewayVariant.VARIANT_RS485_ETH,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "Gateway (RS485-ETH) V2.0"
        );

    /// <summary>
    /// Profile chuẩn cho biến thể Data Logger 8AI V2.0 (Wire Profile V2).
    /// </summary>
    public static SupportedDeviceProfile Datalogger8AiV2_0 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.DATALOGGER,
            DeviceVariant: (ushort)SPLC_DataloggerVariant.VARIANT_8AI,
            ProtocolVersion: 2,
            RuleFormatVersion: 7,
            ProfileName: "Data Logger (8AI) V2.0"
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
    /// Danh sách tất cả các profile mặc định được SimplePLC V2.0 hỗ trợ.
    /// </summary>
    public static IReadOnlyList<SupportedDeviceProfile> DefaultProfiles => new[]
    {
        RemoteIo8Di8Do4AiV2_0,
        RemoteIo16Di16DoV2_0,
        GatewayRs485EthV2_0,
        Datalogger8AiV2_0,
        ControllerV2_0
    };
}
