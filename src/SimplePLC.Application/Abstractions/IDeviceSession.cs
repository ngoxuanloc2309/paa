using SimplePLC.Application.Models;
using SimplePLC.Domain.Models;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Application.Abstractions;

/// <summary>
/// Đại diện cho ngữ cảnh phiên kết nối làm việc (Device Context) với một vi điều khiển PLC.
/// Sở hữu vòng đời của transport, Modbus client và cung cấp 4 năng lực (capabilities) cốt lõi:
/// Rules, RuntimeTags, Health, Commands.
/// </summary>
public interface IDeviceSession : IAsyncDisposable
{
    DeviceEndpoint Endpoint { get; }
    DeviceDescriptorDto Descriptor { get; }
    DeviceResourceInfoDto ResourceInfo { get; }
    ProductDefinition Product { get; }
    byte SlaveId { get; }

    IRuleTableGateway Rules { get; }
    IRuntimeTagReader RuntimeTags { get; }
    IDeviceHealthReader Health { get; }
    ISystemCommandClient Commands { get; }
    IFunctionBlockGateway FunctionBlocks { get; }
    IDiagnosticGateway Diagnostics { get; }
    IRtcClockClient RtcClock { get; }

    bool IsActive { get; }
}
