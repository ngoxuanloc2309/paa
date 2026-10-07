using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Devices;

/// <summary>
/// Hiện thực đọc DeviceDescriptor từ MCU qua Modbus FC03 tại địa chỉ 0x0000.
/// </summary>
public sealed class DeviceDescriptorReader : IDeviceDescriptorReader
{
    private readonly IModbusClient _client;

    public DeviceDescriptorReader(IModbusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<DeviceDescriptorDto> ReadDescriptorAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        var rawRegisters = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.DeviceDescriptorAddress,
            ModbusRegisterMap.DeviceDescriptorLength,
            cancellationToken
        );

        return RegisterCodec.DecodeDeviceDescriptor(rawRegisters);
    }

    public async Task<DeviceResourceInfoDto> ReadResourceInfoAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        var rawRegisters = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.DeviceResourceInfoAddress,
            ModbusRegisterMap.DeviceResourceInfoLength,
            cancellationToken
        );

        return RegisterCodec.DecodeDeviceResourceInfo(rawRegisters);
    }
}
