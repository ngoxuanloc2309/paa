using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;

namespace SimplePLC.Infrastructure.Devices;

/// <summary>
/// Hiện thực đọc bảng Active Rule Table và toàn bộ các phân hệ chức năng từ MCU qua Modbus FC03 theo từng chunk an toàn.
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
        var infoRegs = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.RuleTableInfoAddress,
            ModbusRegisterMap.RuleTableInfoLength,
            cancellationToken
        ).ConfigureAwait(false);

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

        var chunks = ModbusChunkPlanner.PlanReadChunks(
            ModbusRegisterMap.ActiveRuleTableBaseAddress,
            totalRegisters
        );

        int bufferOffset = 0;
        foreach (var (chunkAddress, count) in chunks)
        {
            var chunkData = await _client.ReadHoldingRegistersAsync(slaveId, chunkAddress, count, cancellationToken).ConfigureAwait(false);
            Array.Copy(chunkData, 0, buffer, bufferOffset, count);
            bufferOffset += count;
        }

        return RegisterCodec.DecodeRuleRecords(buffer, ruleCount);
    }

    public async Task<IReadOnlyList<FbTimerRecordDto>> ReadActiveTimersAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        ushort totalRegs = ModbusRegisterMap.FbTimerTableLength;
        var chunks = ModbusChunkPlanner.PlanReadChunks(ModbusRegisterMap.FbTimerTableBaseAddress, totalRegs);
        var buffer = new ushort[totalRegs];
        int offset = 0;
        foreach (var (addr, count) in chunks)
        {
            var data = await _client.ReadHoldingRegistersAsync(slaveId, addr, count, cancellationToken).ConfigureAwait(false);
            Array.Copy(data, 0, buffer, offset, count);
            offset += count;
        }

        return DecodeTimers(buffer);
    }

    private static List<FbTimerRecordDto> DecodeTimers(ushort[] buffer)
    {
        var list = new List<FbTimerRecordDto>();
        for (int i = 0; i < ModbusRegisterMap.FbMaxTimers; i++)
        {
            var span = buffer.AsSpan(i * ModbusRegisterMap.FbRegistersPerBlock, ModbusRegisterMap.FbRegistersPerBlock);
            var timer = FunctionBlockCodec.DecodeTimer(span);
            if (timer.Mode != 0 || timer.PresetMs > 0 || timer.StatusBits != 0)
            {
                list.Add(timer);
            }
        }
        return list;
    }

    public async Task<IReadOnlyList<FbCounterRecordDto>> ReadActiveCountersAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        ushort totalRegs = ModbusRegisterMap.FbCounterTableLength;
        var chunks = ModbusChunkPlanner.PlanReadChunks(ModbusRegisterMap.FbCounterTableBaseAddress, totalRegs);
        var buffer = new ushort[totalRegs];
        int offset = 0;
        foreach (var (addr, count) in chunks)
        {
            var data = await _client.ReadHoldingRegistersAsync(slaveId, addr, count, cancellationToken).ConfigureAwait(false);
            Array.Copy(data, 0, buffer, offset, count);
            offset += count;
        }

        return DecodeCounters(buffer);
    }

    private static List<FbCounterRecordDto> DecodeCounters(ushort[] buffer)
    {
        var list = new List<FbCounterRecordDto>();
        for (int i = 0; i < ModbusRegisterMap.FbMaxCounters; i++)
        {
            var span = buffer.AsSpan(i * ModbusRegisterMap.FbRegistersPerBlock, ModbusRegisterMap.FbRegistersPerBlock);
            var counter = FunctionBlockCodec.DecodeCounter(span);
            if (counter.Mode != 0 || counter.PresetValue > 0 || counter.StatusBits != 0)
            {
                list.Add(counter);
            }
        }
        return list;
    }

    public async Task<IReadOnlyList<ModbusMasterReadConfigDto>> ReadActiveModbusMasterConfigsAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        ushort totalRegs = ModbusRegisterMap.ModbusMasterTableTotalLength;
        var chunks = ModbusChunkPlanner.PlanReadChunks(ModbusRegisterMap.ModbusMasterTableBaseAddress, totalRegs);
        var buffer = new ushort[totalRegs];
        int offset = 0;
        foreach (var (addr, count) in chunks)
        {
            var data = await _client.ReadHoldingRegistersAsync(slaveId, addr, count, cancellationToken).ConfigureAwait(false);
            Array.Copy(data, 0, buffer, offset, count);
            offset += count;
        }

        return DecodeModbusMasterConfigs(buffer);
    }

    private static List<ModbusMasterReadConfigDto> DecodeModbusMasterConfigs(ushort[] buffer)
    {
        var list = new List<ModbusMasterReadConfigDto>();
        for (int i = 0; i < ModbusRegisterMap.ModbusMasterMaxBlocks; i++)
        {
            var span = buffer.AsSpan(i * ModbusRegisterMap.ModbusMasterRegistersPerBlock, ModbusRegisterMap.ModbusMasterRegistersPerBlock);
            var config = ModbusMasterCodec.DecodeBlock(span, i);
            if (config.Enabled && config.SlaveId > 0 && config.RegCount > 0)
            {
                list.Add(config);
            }
        }
        return list;
    }

    public async Task<IReadOnlyList<ModbusMasterWriteConfigDto>> ReadActiveModbusMasterWriteConfigsAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        ushort totalRegs = ModbusRegisterMap.ModbusMasterWriteTableTotalLength;
        var chunks = ModbusChunkPlanner.PlanReadChunks(ModbusRegisterMap.ModbusMasterWriteTableBaseAddress, totalRegs);
        var buffer = new ushort[totalRegs];
        int offset = 0;
        foreach (var (addr, count) in chunks)
        {
            var data = await _client.ReadHoldingRegistersAsync(slaveId, addr, count, cancellationToken).ConfigureAwait(false);
            Array.Copy(data, 0, buffer, offset, count);
            offset += count;
        }

        return DecodeModbusMasterWriteConfigs(buffer);
    }

    private static List<ModbusMasterWriteConfigDto> DecodeModbusMasterWriteConfigs(ushort[] buffer)
    {
        var list = new List<ModbusMasterWriteConfigDto>();
        for (int i = 0; i < ModbusRegisterMap.ModbusMasterWriteMaxBlocks; i++)
        {
            var span = buffer.AsSpan(i * ModbusRegisterMap.ModbusMasterWriteRegistersPerBlock, ModbusRegisterMap.ModbusMasterWriteRegistersPerBlock);
            var config = ModbusMasterCodec.DecodeWriteBlock(span, i);
            if (config.Enabled && config.SlaveId > 0 && config.PointCount > 0)
            {
                list.Add(config);
            }
        }
        return list;
    }

    public async Task<IReadOnlyList<DirectWriteConfigDto>> ReadActiveDirectWriteConfigsAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        ushort totalRegs = ModbusRegisterMap.DirectWriteTableTotalLength;
        var chunks = ModbusChunkPlanner.PlanReadChunks(ModbusRegisterMap.DirectWriteTableBaseAddress, totalRegs);
        var buffer = new ushort[totalRegs];
        int offset = 0;
        foreach (var (addr, count) in chunks)
        {
            var data = await _client.ReadHoldingRegistersAsync(slaveId, addr, count, cancellationToken).ConfigureAwait(false);
            Array.Copy(data, 0, buffer, offset, count);
            offset += count;
        }

        return DecodeDirectWriteConfigs(buffer);
    }

    private static List<DirectWriteConfigDto> DecodeDirectWriteConfigs(ushort[] buffer)
    {
        var list = new List<DirectWriteConfigDto>();
        for (int i = 0; i < ModbusRegisterMap.DirectWriteMaxBlocks; i++)
        {
            var span = buffer.AsSpan(i * ModbusRegisterMap.DirectWriteRegistersPerBlock, ModbusRegisterMap.DirectWriteRegistersPerBlock);
            if ((span[2] & 0x01) != 0) // DirectWriteFlagEnabled
            {
                var config = DirectWriteCodec.DecodeBlock(span);
                list.Add(config);
            }
        }
        return list;
    }

    public async Task<IReadOnlyList<MqttTelemetryConfigDto>> ReadActiveMqttTelemetryConfigsAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        ushort totalRegs = ModbusRegisterMap.MqttTableTotalLength;
        var chunks = ModbusChunkPlanner.PlanReadChunks(ModbusRegisterMap.MqttTelemetryTableBaseAddress, totalRegs);
        var buffer = new ushort[totalRegs];
        int offset = 0;
        foreach (var (addr, count) in chunks)
        {
            var data = await _client.ReadHoldingRegistersAsync(slaveId, addr, count, cancellationToken).ConfigureAwait(false);
            Array.Copy(data, 0, buffer, offset, count);
            offset += count;
        }

        return DecodeMqttTelemetryConfigs(buffer);
    }

    private static List<MqttTelemetryConfigDto> DecodeMqttTelemetryConfigs(ushort[] buffer)
    {
        var list = new List<MqttTelemetryConfigDto>();
        for (int i = 0; i < ModbusRegisterMap.MqttMaxBlocks; i++)
        {
            var span = buffer.AsSpan(i * ModbusRegisterMap.MqttRegistersPerBlock, ModbusRegisterMap.MqttRegistersPerBlock);
            if ((span[0] & ModbusRegisterMap.MqttControlBitEnabled) != 0)
            {
                var config = MqttTelemetryCodec.DecodeBlock(span);
                if (!string.IsNullOrWhiteSpace(config.Topic))
                {
                    list.Add(config);
                }
            }
        }
        return list;
    }

    public async Task<IReadOnlyList<MqttSubscriptionConfigDto>> ReadActiveMqttSubscribeConfigsAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        ushort totalRegs = ModbusRegisterMap.MqttSubscribeTableTotalLength;
        var chunks = ModbusChunkPlanner.PlanReadChunks(ModbusRegisterMap.MqttSubscribeTableBaseAddress, totalRegs);
        var buffer = new ushort[totalRegs];
        int offset = 0;
        foreach (var (addr, count) in chunks)
        {
            var data = await _client.ReadHoldingRegistersAsync(slaveId, addr, count, cancellationToken).ConfigureAwait(false);
            Array.Copy(data, 0, buffer, offset, count);
            offset += count;
        }

        return DecodeMqttSubscribeConfigs(buffer);
    }

    private static List<MqttSubscriptionConfigDto> DecodeMqttSubscribeConfigs(ushort[] buffer)
    {
        var list = new List<MqttSubscriptionConfigDto>();
        for (int i = 0; i < ModbusRegisterMap.MqttSubscribeMaxBlocks; i++)
        {
            var span = buffer.AsSpan(i * ModbusRegisterMap.MqttSubscribeRegistersPerBlock, ModbusRegisterMap.MqttSubscribeRegistersPerBlock);
            if ((span[0] & ModbusRegisterMap.MqttSubControlBitEnabled) != 0)
            {
                var config = MqttSubscriptionCodec.DecodeBlock(span);
                if (!string.IsNullOrWhiteSpace(config.Topic))
                {
                    list.Add(config);
                }
            }
        }
        return list;
    }

    public async Task<MqttBrokerConfigDto?> ReadActiveMqttBrokerConfigAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        ushort totalRegs = ModbusRegisterMap.MqttBrokerConfigRegisters;
        var chunks = ModbusChunkPlanner.PlanReadChunks(ModbusRegisterMap.MqttBrokerConfigBaseAddress, totalRegs);
        var buffer = new ushort[totalRegs];
        int offset = 0;
        foreach (var (addr, count) in chunks)
        {
            var data = await _client.ReadHoldingRegistersAsync(slaveId, addr, count, cancellationToken).ConfigureAwait(false);
            Array.Copy(data, 0, buffer, offset, count);
            offset += count;
        }

        if ((buffer[0] & ModbusRegisterMap.MqttBrokerControlBitEnabled) == 0)
        {
            return null;
        }

        var config = MqttBrokerConfigCodec.Decode(buffer);
        return (!string.IsNullOrWhiteSpace(config.Host)) ? config : null;
    }

    public async Task<IReadOnlyList<ModbusPortConfigDto>> ReadActiveModbusPortsAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        ushort totalRegs = ModbusRegisterMap.ModbusPortConfigTableTotalLength;
        var chunks = ModbusChunkPlanner.PlanReadChunks(ModbusRegisterMap.ModbusPortConfigBaseAddress, totalRegs);
        var buffer = new ushort[totalRegs];
        int offset = 0;
        foreach (var (addr, count) in chunks)
        {
            var data = await _client.ReadHoldingRegistersAsync(slaveId, addr, count, cancellationToken).ConfigureAwait(false);
            Array.Copy(data, 0, buffer, offset, count);
            offset += count;
        }

        return ModbusPortConfigCodec.DecodeAllPorts(buffer);
    }

    public async Task<FullDeviceConfigurationDto> ReadFullDeviceConfigurationAsync(byte slaveId = 1, CancellationToken cancellationToken = default)
    {
        var rules = await ReadActiveRulesAsync(slaveId, cancellationToken).ConfigureAwait(false);
        var timers = await ReadActiveTimersAsync(slaveId, cancellationToken).ConfigureAwait(false);
        var counters = await ReadActiveCountersAsync(slaveId, cancellationToken).ConfigureAwait(false);
        var mbReads = await ReadActiveModbusMasterConfigsAsync(slaveId, cancellationToken).ConfigureAwait(false);
        var mbWrites = await ReadActiveModbusMasterWriteConfigsAsync(slaveId, cancellationToken).ConfigureAwait(false);
        var directWrites = await ReadActiveDirectWriteConfigsAsync(slaveId, cancellationToken).ConfigureAwait(false);
        var mqttTelemetries = await ReadActiveMqttTelemetryConfigsAsync(slaveId, cancellationToken).ConfigureAwait(false);
        var mqttSubs = await ReadActiveMqttSubscribeConfigsAsync(slaveId, cancellationToken).ConfigureAwait(false);
        var brokerConfig = await ReadActiveMqttBrokerConfigAsync(slaveId, cancellationToken).ConfigureAwait(false);
        var modbusPorts = await ReadActiveModbusPortsAsync(slaveId, cancellationToken).ConfigureAwait(false);

        return new FullDeviceConfigurationDto
        {
            Rules = rules,
            Timers = timers,
            Counters = counters,
            ModbusMasterReads = mbReads,
            ModbusMasterWrites = mbWrites,
            DirectWrites = directWrites,
            MqttTelemetries = mqttTelemetries,
            MqttSubscriptions = mqttSubs,
            BrokerConfig = brokerConfig,
            ModbusPorts = modbusPorts
        };
    }
}
