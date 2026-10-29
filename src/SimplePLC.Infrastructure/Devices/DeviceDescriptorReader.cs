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
        ).ConfigureAwait(false);

        return RegisterCodec.DecodeDeviceDescriptor(rawRegisters);
    }

    public async Task<DeviceResourceInfoDto> ReadResourceInfoAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        var rawRegisters = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.DeviceResourceInfoAddress,
            ModbusRegisterMap.DeviceResourceInfoLength,
            cancellationToken
        ).ConfigureAwait(false);

        return RegisterCodec.DecodeDeviceResourceInfo(rawRegisters);
    }

    public async Task<SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities> ReadHardwareCapabilitiesAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        try
        {
            var rawRegisters = await _client.ReadHoldingRegistersAsync(
                slaveId,
                ModbusRegisterMap.HardwareCapabilitiesAddress,
                ModbusRegisterMap.HardwareCapabilitiesLength,
                cancellationToken
            ).ConfigureAwait(false);

            if (rawRegisters != null && rawRegisters.Length >= ModbusRegisterMap.HardwareCapabilitiesLength)
            {
                return RegisterCodec.DecodeHardwareCapabilities(rawRegisters);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Fallback an toàn cho firmware đời cũ chưa mở thanh ghi 0x0A08
        }

        return SimplePLC.Protocol.Enums.SPLC_HardwareCapabilities.NONE;
    }

    public async Task<ushort> ReadPhysicalRs485PortCountAsync(byte slaveId = 1, bool hasModbusMasterCap = true, CancellationToken cancellationToken = default)
    {
        if (!hasModbusMasterCap)
        {
            return 0;
        }

        try
        {
            var rawRegisters = await _client.ReadHoldingRegistersAsync(
                slaveId,
                ModbusRegisterMap.Rs485PhysicalPortCountAddress,
                ModbusRegisterMap.Rs485PhysicalPortCountLength,
                cancellationToken
            ).ConfigureAwait(false);

            if (rawRegisters != null && rawRegisters.Length >= ModbusRegisterMap.Rs485PhysicalPortCountLength)
            {
                return rawRegisters[0];
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Fallback an toàn cho firmware đời cũ chưa hỗ trợ thanh ghi 0x0A0A
        }

        // Mặc định an toàn cho thiết bị SimplePLC có Modbus Master là 2 cổng (Standard)
        return 2;
    }
}
