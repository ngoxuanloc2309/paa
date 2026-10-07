using SimplePLC.Application.Abstractions;
using SimplePLC.Application.Models;
using SimplePLC.Domain.Models;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Gateways;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Sessions;

/// <summary>
/// Hiện thực IDeviceSession quản lý tài nguyên phiên làm việc với vi điều khiển.
/// Sở hữu vòng đời của IUsbCdcTransport, IModbusClient và 4 gateway chuẩn hóa.
/// Đảm bảo giải phóng an toàn cả transport và Modbus client khi DisposeAsync().
/// </summary>
public sealed class DeviceSession : IDeviceSession
{
    private readonly IUsbCdcTransport _transport;
    private readonly IModbusClient _modbusClient;
    private bool _isDisposed;

    public DeviceEndpoint Endpoint { get; }
    public DeviceDescriptorDto Descriptor { get; }
    public DeviceResourceInfoDto ResourceInfo { get; }
    public ProductDefinition Product { get; }
    public byte SlaveId { get; }

    public IRuleTableGateway Rules { get; }
    public IRuntimeTagReader RuntimeTags { get; }
    public IDeviceHealthReader Health { get; }
    public ISystemCommandClient Commands { get; }
    public IFunctionBlockGateway FunctionBlocks { get; }
    public IDiagnosticGateway Diagnostics { get; }
    public IRtcClockClient RtcClock { get; }

    public bool IsActive => !_isDisposed && _transport.IsOpen;

    // Internal access for test inspection or composition root if needed
    internal IUsbCdcTransport Transport => _transport;
    internal IModbusClient Client => _modbusClient;

    public DeviceSession(
        DeviceEndpoint endpoint,
        DeviceDescriptorDto descriptor,
        byte slaveId,
        IUsbCdcTransport transport,
        IModbusClient modbusClient,
        IRuleTableGateway rules,
        IRuntimeTagReader runtimeTags,
        IDeviceHealthReader health,
        ISystemCommandClient commands,
        DeviceResourceInfoDto? resourceInfo = null,
        ProductDefinition? product = null,
        IFunctionBlockGateway? functionBlocks = null,
        IDiagnosticGateway? diagnostics = null,
        IRtcClockClient? rtcClock = null)
    {
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        Descriptor = descriptor ?? throw new ArgumentNullException(nameof(descriptor));
        SlaveId = slaveId;
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _modbusClient = modbusClient ?? throw new ArgumentNullException(nameof(modbusClient));
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
        RuntimeTags = runtimeTags ?? throw new ArgumentNullException(nameof(runtimeTags));
        Health = health ?? throw new ArgumentNullException(nameof(health));
        Commands = commands ?? throw new ArgumentNullException(nameof(commands));
        FunctionBlocks = functionBlocks ?? new FunctionBlockGateway(_modbusClient);
        Diagnostics = diagnostics ?? new SimplePLC.Infrastructure.Devices.DiagnosticGateway(_modbusClient);
        RtcClock = rtcClock ?? new SimplePLC.Infrastructure.Devices.RtcClockClient(_modbusClient);
        ResourceInfo = resourceInfo ?? DeviceResourceInfoDto.CreateRemoteIo8Di8Do4Ai();
        Product = product ?? ProductDefinition.CreateRemoteIo8Di8Do4Ai();
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;

        try
        {
            await _modbusClient.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Bỏ qua lỗi trong quá trình dispose client
        }

        try
        {
            await _transport.DisposeAsync().ConfigureAwait(false);
        }
        catch
        {
            // Bỏ qua lỗi trong quá trình dispose transport
        }
    }
}
