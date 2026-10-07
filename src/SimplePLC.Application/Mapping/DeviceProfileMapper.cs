namespace SimplePLC.Application.Mapping;

using SimplePLC.Domain.Builders;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Dto;

/// <summary>
/// Cầu nối thuần túy (Pure Adapter) giữa Protocol DTO (DeviceResourceInfoDto, DeviceDescriptorDto)
/// và Domain Models (ProductResourceProfile, ProductDefinition).
/// <para>
/// <b>NGUYÊN TẮC RÀNH MẠCH KIẾN TRÚC:</b> Mapper này TUYỆT ĐỐI KHÔNG chứa business validation.
/// Toàn bộ việc thẩm định tính hợp lệ của Wire Profile V1, kiểm tra giới hạn tài nguyên và dựng TagCatalog
/// thuộc sở hữu duy nhất của <see cref="DeviceProfileBuilder"/> trong tầng Domain.
/// </para>
/// </summary>
public static class DeviceProfileMapper
{
    /// <summary>
    /// Chuyển đổi DeviceResourceInfoDto sang ProductResourceProfile của tầng Domain.
    /// </summary>
    public static ProductResourceProfile ToResourceProfile(DeviceResourceInfoDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new ProductResourceProfile(
            DigitalInputs: dto.DigitalInputCount,
            DigitalOutputs: dto.DigitalOutputCount,
            AnalogInputs: dto.AnalogInputCount,
            VirtualFlags: dto.VirtualFlagCount,
            VirtualRegisters: dto.VirtualRegisterCount,
            RetentiveRegisters: dto.RetentiveRegisterCount,
            Counters: dto.CounterCount);
    }

    /// <summary>
    /// Thẩm định và dựng ProductDefinition từ DeviceDescriptorDto và DeviceResourceInfoDto.
    /// </summary>
    public static bool TryBuildProductDefinition(
        DeviceDescriptorDto descriptor,
        DeviceResourceInfoDto resourceInfo,
        out ProductDefinition? product,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(resourceInfo);

        var resources = ToResourceProfile(resourceInfo);

        return DeviceProfileBuilder.TryBuild(
            deviceClass: (ushort)descriptor.DeviceClass,
            productVariant: descriptor.DeviceVariant,
            protocolVersion: descriptor.ProtocolVersion,
            wireProfile: resourceInfo.WireProfile,
            maxRules: resourceInfo.MaxRules,
            runtimeTagCount: resourceInfo.RuntimeTagCount,
            resources: resources,
            out product,
            out errorMessage);
    }
}
