using SimplePLC.Application.Models;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Application.Abstractions;

/// <summary>
/// Giao diện thẩm định tính tương thích của thiết bị dựa trên DeviceDescriptor.
/// </summary>
public interface IDeviceCompatibilityValidator
{
    CompatibilityResult Validate(DeviceDescriptorDto descriptor);
}
