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
    /// Profile mặc định cho biến thể Remote I/O 8DI-8DO-4AI v1.7.
    /// </summary>
    public static SupportedDeviceProfile RemoteIo8Di8Do4AiV1_7 =>
        new(
            DeviceClass: (ushort)SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant: (ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI,
            ProtocolVersion: 1,
            RuleFormatVersion: 1,
            ProfileName: "Remote I/O (8DI-8DO-4AI) V1.7"
        );

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
    /// Danh sách tất cả các profile mặc định được SimplePLC hỗ trợ.
    /// </summary>
    public static IReadOnlyList<SupportedDeviceProfile> DefaultProfiles => new[]
    {
        RemoteIo8Di8Do4AiV1_7,
        RemoteIo8Di8Do4AiV2_0
    };
}
