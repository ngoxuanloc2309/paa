using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO biểu diễn 10 thanh ghi (20 bytes) của SPLC_DeviceDescriptor đọc tại địa chỉ 0x0000.
/// </summary>
public sealed class DeviceDescriptorDto
{
    public SPLC_DeviceClass DeviceClass { get; set; }
    public ushort DeviceVariant { get; set; }
    public ushort HwVersionMajor { get; set; }
    public ushort HwVersionMinor { get; set; }
    public ushort HwVersionPatch { get; set; }
    public ushort FwVersionMajor { get; set; }
    public ushort FwVersionMinor { get; set; }
    public ushort FwVersionPatch { get; set; }
    public ushort ProtocolVersion { get; set; }
    public ushort RuleFormatVersion { get; set; }

    public string HwVersionString => $"{HwVersionMajor}.{HwVersionMinor}.{HwVersionPatch}";
    public string FwVersionString => $"{FwVersionMajor}.{FwVersionMinor}.{FwVersionPatch}";
}
