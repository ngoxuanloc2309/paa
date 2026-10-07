using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Devices;

/// <summary>
/// Hiện thực đọc DeviceHealth từ MCU qua Modbus FC03 tại địa chỉ 0x0800.
/// </summary>
public sealed class DeviceHealthReader : IDeviceHealthReader
{
    private readonly IModbusClient _client;

    public DeviceHealthReader(IModbusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<DeviceHealthDto> ReadHealthAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        var rawRegisters = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.DeviceHealthAddress,
            ModbusRegisterMap.DeviceHealthLength,
            cancellationToken
        );

        return RegisterCodec.DecodeDeviceHealth(rawRegisters);
    }
}
