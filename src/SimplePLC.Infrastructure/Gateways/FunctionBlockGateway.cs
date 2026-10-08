using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Gateways;

/// <summary>
/// Cổng giao tiếp cho phân vùng Function Block chuyên dụng (0x0B00..0x0B7F).
/// Hỗ trợ đọc/ghi phi cấp phát (Zero Heap Allocation) qua Span và Modbus RTU.
/// </summary>
public sealed class FunctionBlockGateway : IFunctionBlockGateway
{
    private readonly IModbusClient _client;

    public FunctionBlockGateway(IModbusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task WriteFunctionBlocksAsync(
        byte slaveId,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        CancellationToken cancellationToken = default)
    {
        const int maxBlocksPerChunk = 3;

        if (timers != null && timers.Count > 0)
        {
            for (int offset = 0; offset < timers.Count; offset += maxBlocksPerChunk)
            {
                int count = Math.Min(maxBlocksPerChunk, timers.Count - offset);
                ushort startAddr = (ushort)(ModbusRegisterMap.FbTimerTableBaseAddress + (offset * ModbusRegisterMap.FbRegistersPerBlock));
                var chunkRegs = new ushort[count * ModbusRegisterMap.FbRegistersPerBlock];
                for (int i = 0; i < count; i++)
                {
                    FunctionBlockCodec.EncodeTimer(
                        timers[offset + i],
                        chunkRegs.AsSpan(i * ModbusRegisterMap.FbRegistersPerBlock, ModbusRegisterMap.FbRegistersPerBlock));
                }
                await _client.WriteMultipleRegistersAsync(slaveId, startAddr, chunkRegs, cancellationToken).ConfigureAwait(false);
            }
        }

        if (counters != null && counters.Count > 0)
        {
            for (int offset = 0; offset < counters.Count; offset += maxBlocksPerChunk)
            {
                int count = Math.Min(maxBlocksPerChunk, counters.Count - offset);
                ushort startAddr = (ushort)(ModbusRegisterMap.FbCounterTableBaseAddress + (offset * ModbusRegisterMap.FbRegistersPerBlock));
                var chunkRegs = new ushort[count * ModbusRegisterMap.FbRegistersPerBlock];
                for (int i = 0; i < count; i++)
                {
                    FunctionBlockCodec.EncodeCounter(
                        counters[offset + i],
                        chunkRegs.AsSpan(i * ModbusRegisterMap.FbRegistersPerBlock, ModbusRegisterMap.FbRegistersPerBlock));
                }
                await _client.WriteMultipleRegistersAsync(slaveId, startAddr, chunkRegs, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<(IReadOnlyList<FbTimerRecordDto> Timers, IReadOnlyList<FbCounterRecordDto> Counters)> ReadFunctionBlocksAsync(
        byte slaveId = 1,
        CancellationToken cancellationToken = default)
    {
        // Đọc trọn vẹn 128 thanh ghi phân vùng Function Block (0x0B00..0x0B7F) trong 1 request duy nhất
        var rawRegisters = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.FbTimerTableBaseAddress,
            ModbusRegisterMap.FbTableTotalLength,
            cancellationToken).ConfigureAwait(false);

        if (rawRegisters.Length < ModbusRegisterMap.FbTableTotalLength)
        {
            throw new InvalidOperationException(
                $"Expected {ModbusRegisterMap.FbTableTotalLength} registers from 0x0B00, but received {rawRegisters.Length}.");
        }

        return DecodeBlocks(rawRegisters);
    }

    private static (IReadOnlyList<FbTimerRecordDto> Timers, IReadOnlyList<FbCounterRecordDto> Counters) DecodeBlocks(ushort[] rawRegisters)
    {
        var timers = new List<FbTimerRecordDto>(ModbusRegisterMap.FbMaxTimers);
        for (int i = 0; i < ModbusRegisterMap.FbMaxTimers; i++)
        {
            var slice = rawRegisters.AsSpan(i * ModbusRegisterMap.FbRegistersPerBlock, ModbusRegisterMap.FbRegistersPerBlock);
            timers.Add(FunctionBlockCodec.DecodeTimer(slice));
        }

        var counters = new List<FbCounterRecordDto>(ModbusRegisterMap.FbMaxCounters);
        int counterOffset = ModbusRegisterMap.FbTimerTableLength; // 64
        for (int i = 0; i < ModbusRegisterMap.FbMaxCounters; i++)
        {
            var slice = rawRegisters.AsSpan(counterOffset + (i * ModbusRegisterMap.FbRegistersPerBlock), ModbusRegisterMap.FbRegistersPerBlock);
            counters.Add(FunctionBlockCodec.DecodeCounter(slice));
        }

        return (timers, counters);
    }
}
