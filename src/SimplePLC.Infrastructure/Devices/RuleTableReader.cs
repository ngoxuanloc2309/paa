using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Devices;

/// <summary>
/// Hiện thực đọc bảng Active Rule Table từ MCU qua Modbus FC03 theo từng chunk an toàn.
/// </summary>
public sealed class RuleTableReader : IRuleTableReader
{
    private readonly IModbusClient _client;

    public RuleTableReader(IModbusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<IReadOnlyList<RuleRecordDto>> ReadActiveRulesAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        // 1. Đọc số rule active từ RULE_TABLE_INFO (0x0010)
        var infoRegs = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.RuleTableInfoAddress,
            ModbusRegisterMap.RuleTableInfoLength,
            cancellationToken
        );

        ushort ruleCount = infoRegs[0];
        if (ruleCount == 0)
        {
            return Array.Empty<RuleRecordDto>();
        }

        if (ruleCount > ModbusRegisterMap.MaxRules)
        {
            ruleCount = ModbusRegisterMap.MaxRules;
        }

        ushort totalRegisters = (ushort)(ruleCount * ModbusRegisterMap.RegistersPerRule);
        var buffer = new ushort[totalRegisters];

        // 2. Đọc theo chunks an toàn từ ACTIVE_RULE_TABLE (0x0100)
        var chunks = ModbusChunkPlanner.PlanReadChunks(
            ModbusRegisterMap.ActiveRuleTableBaseAddress,
            totalRegisters
        );

        int bufferOffset = 0;
        foreach (var (chunkAddress, count) in chunks)
        {
            var chunkData = await _client.ReadHoldingRegistersAsync(slaveId, chunkAddress, count, cancellationToken);
            Array.Copy(chunkData, 0, buffer, bufferOffset, count);
            bufferOffset += count;
        }

        // 3. Giải mã mảng RuleRecordDto
        return RegisterCodec.DecodeRuleRecords(buffer, ruleCount);
    }
}
