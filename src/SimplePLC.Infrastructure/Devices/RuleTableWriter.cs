using SimplePLC.Application.Abstractions;
using SimplePLC.Infrastructure.Abstractions;
using SimplePLC.Infrastructure.Transport;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Cryptography;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Infrastructure.Devices;

/// <summary>
/// Hiện thực nạp bảng quy tắc xuống MCU qua Staging và Commit theo quy chuẩn 4 bước (R10).
/// </summary>
public sealed class RuleTableWriter : IRuleTableWriter
{
    private readonly IModbusClient _client;

    public RuleTableWriter(IModbusClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        CancellationToken cancellationToken = default) =>
        DeployRulesAsync(slaveId, rules, timers, counters, null, cancellationToken);

    public Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs,
        CancellationToken cancellationToken = default) =>
        DeployRulesAsync(slaveId, rules, timers, counters, mqttConfigs, null, cancellationToken);

    public Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs,
        MqttBrokerConfigDto? brokerConfig,
        CancellationToken cancellationToken = default) =>
        DeployRulesAsync(slaveId, rules, timers, counters, mqttConfigs, brokerConfig, null, cancellationToken);

    public async Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs,
        MqttBrokerConfigDto? brokerConfig,
        IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubConfigs,
        CancellationToken cancellationToken = default)
    {
        // 1. Nạp cấu hình Timer blocks (0x0B00) nếu có
        // Giới hạn tối đa 3 khối (24 thanh ghi = 48 bytes payload, frame 57 bytes <= 64 bytes RX buffer của MCU)
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

        // 2. Nạp cấu hình Counter blocks (0x0B40) nếu có (tối đa 3 khối mỗi frame)
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

        // 3. Nạp cấu hình MQTT Telemetry & Broker (0x0C00, 0x0D80, 0x0E00) nếu thiết bị có năng lực MQTT tương ứng (0x0A08)
        bool isMqttPubSupported = false;
        bool isMqttSubSupported = false;
        if (mqttConfigs != null || brokerConfig != null || mqttSubConfigs != null)
        {
            try
            {
                // Kiểm tra năng lực phần cứng thực tế qua thanh ghi HardwareCapabilities tại 0x0A08
                var rawCaps = await _client.ReadHoldingRegistersAsync(
                    slaveId,
                    ModbusRegisterMap.HardwareCapabilitiesAddress,
                    ModbusRegisterMap.HardwareCapabilitiesLength,
                    cancellationToken).ConfigureAwait(false);

                if (rawCaps != null && rawCaps.Length >= ModbusRegisterMap.HardwareCapabilitiesLength)
                {
                    var caps = RegisterCodec.DecodeHardwareCapabilities(rawCaps);
                    isMqttPubSupported = (caps & SPLC_HardwareCapabilities.MQTT_PUB) != 0;
                    isMqttSubSupported = (caps & SPLC_HardwareCapabilities.MQTT_SUB) != 0;
                }
            }
            catch
            {
                // Thiết bị không hỗ trợ vùng nhớ 0x0A08 hoặc từ chối truy cập
                isMqttPubSupported = false;
                isMqttSubSupported = false;
            }
        }

        // 3.1 Nạp cấu hình MQTT Telemetry blocks (0x0C00) nếu thiết bị hỗ trợ PUB (SPLC-SPEC-MQTT-001)
        if (isMqttPubSupported && mqttConfigs != null)
        {
            int blockCount = Math.Min(mqttConfigs.Count, ModbusRegisterMap.MqttMaxBlocks);
            for (int i = 0; i < blockCount; i++)
            {
                var blockRegs = new ushort[ModbusRegisterMap.MqttRegistersPerBlock];
                MqttTelemetryCodec.EncodeBlock(mqttConfigs[i], blockRegs);

                ushort blockStartAddr = ModbusRegisterMap.GetMqttBlockAddress(i);
                var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                    blockStartAddr,
                    blockRegs.AsMemory());

                foreach (var (chunkAddress, chunkMemory) in chunks)
                {
                    await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
                }
            }

            // Vô hiệu hóa (disable) các khối MQTT PUB cũ không còn nằm trong cấu hình triển khai
            for (int j = blockCount; j < ModbusRegisterMap.MqttMaxBlocks; j++)
            {
                ushort staleBlockAddr = ModbusRegisterMap.GetMqttBlockAddress(j);
                await _client.WriteSingleRegisterAsync(slaveId, staleBlockAddr, 0, cancellationToken).ConfigureAwait(false);
            }
        }

        // 3.2 Nạp cấu hình MQTT Inbound Subscribe blocks (0x0D80) nếu thiết bị hỗ trợ SUB (SPLC-SPEC-MQTT-002)
        if (isMqttSubSupported && mqttSubConfigs != null)
        {
            int blockCount = Math.Min(mqttSubConfigs.Count, ModbusRegisterMap.MqttSubscribeMaxBlocks);
            for (int i = 0; i < blockCount; i++)
            {
                var blockRegs = new ushort[ModbusRegisterMap.MqttSubscribeRegistersPerBlock];
                MqttSubscriptionCodec.EncodeBlock(mqttSubConfigs[i], blockRegs);

                ushort blockStartAddr = ModbusRegisterMap.GetMqttSubscribeBlockAddress(i);
                var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                    blockStartAddr,
                    blockRegs.AsMemory());

                foreach (var (chunkAddress, chunkMemory) in chunks)
                {
                    await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
                }
            }

            // Vô hiệu hóa (disable) các khối MQTT SUB cũ không còn nằm trong cấu hình triển khai
            for (int j = blockCount; j < ModbusRegisterMap.MqttSubscribeMaxBlocks; j++)
            {
                ushort staleBlockAddr = ModbusRegisterMap.GetMqttSubscribeBlockAddress(j);
                await _client.WriteSingleRegisterAsync(slaveId, staleBlockAddr, 0, cancellationToken).ConfigureAwait(false);
            }
        }

        // 3.3 Nạp cấu hình MQTT Broker (0x0E00) nếu thiết bị hỗ trợ ít nhất một phân hệ MQTT
        if ((isMqttPubSupported || isMqttSubSupported) && brokerConfig != null)
        {
            var brokerRegs = new ushort[ModbusRegisterMap.MqttBrokerConfigRegisters];
            MqttBrokerConfigCodec.Encode(brokerConfig, brokerRegs);

            var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                ModbusRegisterMap.MqttBrokerConfigBaseAddress,
                brokerRegs.AsMemory());

            foreach (var (chunkAddress, chunkMemory) in chunks)
            {
                await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
            }
        }

        // 5. Tiến hành Staging và Commit Rule Table
        return await DeployRulesAsync(slaveId, rules, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.Count > ModbusRegisterMap.MaxRules)
        {
            return new RuleDeployResult(
                IsSuccess: false,
                ConfigStatus: 4,
                ErrorCode: SPLC_ErrorCode.INVALID_PARAMETER,
                ErrorMessage: $"Rule count ({rules.Count}) exceeds maximum capacity of {ModbusRegisterMap.MaxRules} rules."
            );
        }

        ushort ruleCount = (ushort)rules.Count;
        int totalRegisters = ruleCount * ModbusRegisterMap.RegistersPerRule;

        // 1. Mã hóa toàn bộ RuleRecordDto thành mảng thanh ghi và tính CRC-16/MODBUS
        var stagingRegisters = new ushort[totalRegisters];
        if (ruleCount > 0)
        {
            var ruleArray = rules is RuleRecordDto[] arr ? arr : rules.ToArray();
            RegisterCodec.EncodeRuleRecords(ruleArray, stagingRegisters);
        }

        ushort expectedCrc = Crc16Modbus.ComputeFromRegisters(stagingRegisters);

        // 2. Bước 1 (R10): Ghi RULE_COUNT_STAGED (0x9002) và EXPECTED_CRC16 (0x9003)
        ushort[] transferInfo = [ruleCount, expectedCrc];
        await _client.WriteMultipleRegistersAsync(
            slaveId,
            ModbusRegisterMap.RuleCountStagedAddress,
            transferInfo,
            cancellationToken
        );

        // 3. Bước 2 (R10): Ghi STAGING_RULE_TABLE (0x9010) theo các chunks an toàn (16 regs = 1 rule)
        if (totalRegisters > 0)
        {
            var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                ModbusRegisterMap.StagingRuleTableBaseAddress,
                stagingRegisters.AsMemory()
            );

            foreach (var (chunkAddress, chunkMemory) in chunks)
            {
                await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken);
            }
        }

        // 4. Bước 3 (R10): Ghi COMMIT_COMMAND (0xA000) với Magic 0xA5A5
        await _client.WriteSingleRegisterAsync(
            slaveId,
            ModbusRegisterMap.CommitCommandAddress,
            ModbusRegisterMap.CommitMagic,
            cancellationToken
        );

        // 5. Bước 4 (R10): Chờ MCU xác thực CRC và kiểm tra trạng thái CONFIG_STATUS
        return await WaitForCommitCompletionAsync(slaveId, cancellationToken);
    }

    private async Task<RuleDeployResult> WaitForCommitCompletionAsync(byte slaveId, CancellationToken cancellationToken)
    {
        var timeoutAt = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < timeoutAt)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var statusRegs = await _client.ReadHoldingRegistersAsync(
                slaveId,
                ModbusRegisterMap.ConfigStatusAddress,
                2, // Đọc cả CONFIG_STATUS (0x9000) và CONFIG_ERROR_CODE (0x9001)
                cancellationToken
            );

            ushort status = statusRegs[0];
            var errorCode = (SPLC_ErrorCode)statusRegs[1];

            // 0=IDLE, 1=RECEIVING, 2=VERIFYING, 3=READY, 4=ERROR
            if (status == 3) // READY (Commit thành công)
            {
                var verRegs = await _client.ReadHoldingRegistersAsync(
                    slaveId,
                    ModbusRegisterMap.ActiveRuleVersionAddress,
                    1,
                    cancellationToken
                );
                return new RuleDeployResult(
                    IsSuccess: true,
                    ConfigStatus: status,
                    ErrorCode: SPLC_ErrorCode.NONE,
                    ActiveVersion: verRegs[0]
                );
            }

            if (status == 4) // ERROR
            {
                return new RuleDeployResult(
                    IsSuccess: false,
                    ConfigStatus: status,
                    ErrorCode: errorCode,
                    ErrorMessage: $"MCU commit failed with error code: {errorCode}"
                );
            }

            await Task.Delay(25, cancellationToken);
        }

        return new RuleDeployResult(
            IsSuccess: false,
            ConfigStatus: 4,
            ErrorCode: SPLC_ErrorCode.BUSY,
            ErrorMessage: "Timed out waiting for MCU to verify and commit staged rules."
        );
    }
}
