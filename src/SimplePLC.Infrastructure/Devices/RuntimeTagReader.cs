using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;

namespace SimplePLC.Infrastructure.Devices;

/// <summary>
/// Hiện thực đọc giá trị Tag runtime sống từ MCU qua Modbus FC03 tại địa chỉ 0x0900.
/// </summary>
public sealed class RuntimeTagReader : IRuntimeTagReader
{
    private readonly IModbusClient _client;

    public RuntimeTagReader(IModbusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<int[]> ReadRuntimeTagValuesAsync(
        byte slaveId = 1,
        ushort count = 124,
        CancellationToken cancellationToken = default)
    {
        if (count == 0)
            return Array.Empty<int>();

        if (count > ModbusRegisterMap.MaxRuntimeTags)
            count = ModbusRegisterMap.MaxRuntimeTags;

        ushort totalRegisters = (ushort)(count * ModbusRegisterMap.RegistersPerRuntimeTag);
        var rawRegisters = new ushort[totalRegisters];

        var chunks = ModbusChunkPlanner.PlanReadChunks(
            ModbusRegisterMap.RuntimeTagValuesBaseAddress,
            totalRegisters
        );

        int bufferOffset = 0;
        foreach (var (chunkAddress, chunkCount) in chunks)
        {
            var chunkData = await _client.ReadHoldingRegistersAsync(slaveId, chunkAddress, chunkCount, cancellationToken);
            Array.Copy(chunkData, 0, rawRegisters, bufferOffset, chunkCount);
            bufferOffset += chunkCount;
        }

        var result = new int[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = RegisterCodec.DecodeRuntimeTagValue(rawRegisters.AsSpan(i * 2, 2));
        }

        return result;
    }
}
