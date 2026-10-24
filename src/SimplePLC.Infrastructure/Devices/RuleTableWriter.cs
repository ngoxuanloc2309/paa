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

    public Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs,
        MqttBrokerConfigDto? brokerConfig,
        IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubConfigs,
        CancellationToken cancellationToken = default) =>
        DeployRulesAsync(slaveId, rules, timers, counters, mqttConfigs, brokerConfig, mqttSubConfigs, null, cancellationToken);

    public Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs,
        MqttBrokerConfigDto? brokerConfig,
        IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubConfigs,
        IReadOnlyList<ModbusMasterReadConfigDto>? modbusMasterConfigs,
        CancellationToken cancellationToken = default) =>
        DeployRulesAsync(slaveId, rules, timers, counters, mqttConfigs, brokerConfig, mqttSubConfigs, modbusMasterConfigs, null, null, null, null, cancellationToken, null);

    public async Task<RuleDeployResult> DeployRulesAsync(
        byte slaveId,
        IReadOnlyList<RuleRecordDto> rules,
        IReadOnlyList<FbTimerRecordDto>? timers,
        IReadOnlyList<FbCounterRecordDto>? counters,
        IReadOnlyList<MqttTelemetryConfigDto>? mqttConfigs,
        MqttBrokerConfigDto? brokerConfig,
        IReadOnlyList<MqttSubscriptionConfigDto>? mqttSubConfigs,
        IReadOnlyList<ModbusMasterReadConfigDto>? modbusMasterConfigs = null,
        IReadOnlyList<ModbusMasterWriteConfigDto>? modbusMasterWriteConfigs = null,
        IReadOnlyList<DirectWriteConfigDto>? directWriteConfigs = null,
        IReadOnlyList<ModbusPortConfigDto>? modbusPortConfigs = null,
        IReadOnlyList<byte>? tagDataTypes = null,
        CancellationToken cancellationToken = default,
        Action<string>? progressLogger = null)
    {
        // 0. Nạp bảng kiểu dữ liệu Tag Catalog (0x0080..0x00BF, 64 thanh ghi = 128 bytes)
        if (tagDataTypes != null && tagDataTypes.Count > 0)
        {
            var typeRegs = new ushort[ModbusRegisterMap.TagDataTypesLength];
            byte[] typeBytes = tagDataTypes is byte[] arr ? arr : tagDataTypes.ToArray();
            RegisterCodec.EncodeTagDataTypes(typeBytes, typeRegs);

            progressLogger?.Invoke($"[TAG TYPES] Writing {typeRegs.Length} registers to 0x{ModbusRegisterMap.TagDataTypesBaseAddress:X04} (Tag DataType Registry)...");
            var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                ModbusRegisterMap.TagDataTypesBaseAddress,
                typeRegs.AsMemory());

            foreach (var (chunkAddress, chunkMemory) in chunks)
            {
                await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
            }
        }

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
        bool hasMqttPub = mqttConfigs != null && mqttConfigs.Count > 0;
        bool hasMqttSub = mqttSubConfigs != null && mqttSubConfigs.Count > 0;
        bool hasBroker = brokerConfig != null && brokerConfig.Enabled;
        bool hasMqttWork = hasMqttPub || hasMqttSub || hasBroker;
        bool hasMbRead = modbusMasterConfigs != null && modbusMasterConfigs.Count > 0;
        bool hasMbWrite = modbusMasterWriteConfigs != null && modbusMasterWriteConfigs.Count > 0;
        bool hasMbPorts = modbusPortConfigs != null && modbusPortConfigs.Count > 0;
        bool hasModbusWork = hasMbRead || hasMbWrite || hasMbPorts;
        ushort physicalRs485PortCount = 2;

        bool isMqttPubSupported = false;
        bool isMqttSubSupported = false;
        SPLC_HardwareCapabilities caps = SPLC_HardwareCapabilities.NONE;
        Exception? capsReadException = null;

        if (hasMqttWork || hasModbusWork || mqttConfigs != null || brokerConfig != null || mqttSubConfigs != null)
        {
            try
            {
                progressLogger?.Invoke($"[CAPS] Reading hardware capabilities at 0x{ModbusRegisterMap.HardwareCapabilitiesAddress:X04}...");
                // Kiểm tra năng lực phần cứng thực tế qua thanh ghi HardwareCapabilities tại 0x0A08
                var rawCaps = await _client.ReadHoldingRegistersAsync(
                    slaveId,
                    ModbusRegisterMap.HardwareCapabilitiesAddress,
                    ModbusRegisterMap.HardwareCapabilitiesLength,
                    cancellationToken).ConfigureAwait(false);

                if (rawCaps != null && rawCaps.Length >= ModbusRegisterMap.HardwareCapabilitiesLength)
                {
                    caps = RegisterCodec.DecodeHardwareCapabilities(rawCaps);
                    isMqttPubSupported = (caps & SPLC_HardwareCapabilities.MQTT_PUB) != 0;
                    isMqttSubSupported = (caps & SPLC_HardwareCapabilities.MQTT_SUB) != 0;
                    progressLogger?.Invoke($"[CAPS] MCU capabilities: 0x{(uint)caps:X8} (MQTT_PUB: {isMqttPubSupported}, MQTT_SUB: {isMqttSubSupported})");
                }
                else
                {
                    progressLogger?.Invoke($"[CAPS WARNING] Empty or short response when reading 0x{ModbusRegisterMap.HardwareCapabilitiesAddress:X04}.");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                capsReadException = ex;
                isMqttPubSupported = false;
                isMqttSubSupported = false;
                progressLogger?.Invoke($"[CAPS WARNING] Error reading 0x{ModbusRegisterMap.HardwareCapabilitiesAddress:X04}: {ex.Message}");
            }

            // Cơ chế an toàn Fail-Fast: Chặn triển khai nếu dự án có dùng MQTT nhưng phần cứng không đáp ứng
            if (hasMqttWork)
            {
                if (capsReadException != null)
                {
                    return new RuleDeployResult(
                        false,
                        (ushort)SPLC_CommandStatus.ERROR,
                        SPLC_ErrorCode.UNSUPPORTED,
                        0,
                        $"Communication error reading hardware capabilities at 0x{ModbusRegisterMap.HardwareCapabilitiesAddress:X04}: {capsReadException.Message}. Deployment aborted for safety.");
                }

                if (hasMqttPub && !isMqttPubSupported)
                {
                    return new RuleDeployResult(
                        false,
                        (ushort)SPLC_CommandStatus.ERROR,
                        SPLC_ErrorCode.UNSUPPORTED,
                        0,
                        $"Device does not support MQTT Telemetry PUB (Reported caps: 0x{(uint)caps:X8}, missing SPLC_CAP_MQTT_PUB at 0x0A08). Deployment aborted for safety.");
                }

                if (hasMqttSub && !isMqttSubSupported)
                {
                    return new RuleDeployResult(
                        false,
                        (ushort)SPLC_CommandStatus.ERROR,
                        SPLC_ErrorCode.UNSUPPORTED,
                        0,
                        $"Device does not support MQTT Inbound SUB (Reported caps: 0x{(uint)caps:X8}, missing SPLC_CAP_MQTT_SUB at 0x0A08). Deployment aborted for safety.");
                }

                if (hasBroker && !isMqttPubSupported && !isMqttSubSupported)
                {
                    return new RuleDeployResult(
                        false,
                        (ushort)SPLC_CommandStatus.ERROR,
                        SPLC_ErrorCode.UNSUPPORTED,
                        0,
                        $"Device does not declare any MQTT capability (Reported caps: 0x{(uint)caps:X8}, missing MQTT_PUB/SUB at 0x0A08). Deployment aborted for safety.");
                }
            }

            // Cơ chế an toàn Fail-Fast: Chặn triển khai nếu dự án dùng Modbus Master nhưng phần cứng không đáp ứng
            if (hasModbusWork)
            {
                try
                {
                    var rawPortRegs = await _client.ReadHoldingRegistersAsync(
                        slaveId,
                        ModbusRegisterMap.Rs485PhysicalPortCountAddress,
                        ModbusRegisterMap.Rs485PhysicalPortCountLength,
                        cancellationToken).ConfigureAwait(false);

                    if (rawPortRegs != null && rawPortRegs.Length >= ModbusRegisterMap.Rs485PhysicalPortCountLength)
                    {
                        physicalRs485PortCount = rawPortRegs[0];
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    // Fallback an toàn cho firmware đời cũ
                    bool hasMbCap = (caps & SPLC_HardwareCapabilities.MODBUS_MASTER) != 0;
                    physicalRs485PortCount = hasMbCap ? (ushort)2 : (ushort)0;
                }

                bool isModbusMasterSupported = (caps & SPLC_HardwareCapabilities.MODBUS_MASTER) != 0;
                if (!isModbusMasterSupported && (hasMbRead || hasMbWrite))
                {
                    return new RuleDeployResult(
                        false,
                        (ushort)SPLC_CommandStatus.ERROR,
                        SPLC_ErrorCode.UNSUPPORTED,
                        0,
                        $"Device does not declare Modbus Master capability (Reported caps: 0x{(uint)caps:X8}, missing SPLC_CAP_MODBUS_MASTER at 0x0A08). Deployment aborted for safety.");
                }

                if (physicalRs485PortCount == 0 && (hasMbRead || hasMbWrite))
                {
                    return new RuleDeployResult(
                        false,
                        (ushort)SPLC_CommandStatus.ERROR,
                        SPLC_ErrorCode.UNSUPPORTED,
                        0,
                        "Device declares 0 physical RS-485 ports (0x0A0A). Modbus Master deployment aborted for safety.");
                }

                if (hasMbRead)
                {
                    var invalidRead = modbusMasterConfigs!.FirstOrDefault(c => c.PortIndex >= physicalRs485PortCount);
                    if (invalidRead != null)
                    {
                        return new RuleDeployResult(
                            false,
                            (ushort)SPLC_CommandStatus.ERROR,
                            SPLC_ErrorCode.UNSUPPORTED,
                            0,
                            $"Modbus Master Read block targets Port {invalidRead.PortIndex + 1}, exceeding device's physical RS-485 port count ({physicalRs485PortCount}). Deployment aborted for safety.");
                    }
                }

                if (hasMbWrite)
                {
                    var invalidWrite = modbusMasterWriteConfigs!.FirstOrDefault(c => c.PortIndex >= physicalRs485PortCount);
                    if (invalidWrite != null)
                    {
                        return new RuleDeployResult(
                            false,
                            (ushort)SPLC_CommandStatus.ERROR,
                            SPLC_ErrorCode.UNSUPPORTED,
                            0,
                            $"Modbus Master Write block targets Port {invalidWrite.PortIndex + 1}, exceeding device's physical RS-485 port count ({physicalRs485PortCount}). Deployment aborted for safety.");
                    }
                }
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
                progressLogger?.Invoke($"[MQTT PUB] Writing {blockRegs.Length} registers to 0x{blockStartAddr:X04} (Block {i})...");
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
                progressLogger?.Invoke($"[MQTT SUB] Writing {blockRegs.Length} registers to 0x{blockStartAddr:X04} (Block {i})...");
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

            progressLogger?.Invoke($"[MQTT Broker] Writing {brokerRegs.Length} registers to 0x{ModbusRegisterMap.MqttBrokerConfigBaseAddress:X04} (Host: {brokerConfig.Host}, Port: {brokerConfig.Port})...");
            var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                ModbusRegisterMap.MqttBrokerConfigBaseAddress,
                brokerRegs.AsMemory());

            foreach (var (chunkAddress, chunkMemory) in chunks)
            {
                await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
            }
        }

        // 4.1 Nạp cấu hình Modbus RTU Master blocks (0x1000) nếu có
        if (modbusMasterConfigs != null && modbusMasterConfigs.Count > 0)
        {
            int blockCount = Math.Min(modbusMasterConfigs.Count, ModbusRegisterMap.ModbusMasterMaxBlocks);
            for (int i = 0; i < blockCount; i++)
            {
                var blockRegs = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
                ModbusMasterCodec.EncodeBlock(modbusMasterConfigs[i], blockRegs);

                ushort blockStartAddr = ModbusRegisterMap.GetModbusMasterBlockAddress(i);
                var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                    blockStartAddr,
                    blockRegs.AsMemory());

                foreach (var (chunkAddress, chunkMemory) in chunks)
                {
                    await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
                }
            }

            // Vô hiệu hóa (disable) các khối Modbus Master cũ không còn trong cấu hình
            for (int j = blockCount; j < ModbusRegisterMap.ModbusMasterMaxBlocks; j++)
            {
                ushort staleBlockAddr = ModbusRegisterMap.GetModbusMasterBlockAddress(j);
                await _client.WriteSingleRegisterAsync(slaveId, staleBlockAddr, 0, cancellationToken).ConfigureAwait(false);
            }
        }

        // 4.2 Nạp cấu hình Modbus RTU Master Write blocks (0x1100) nếu có
        if (modbusMasterWriteConfigs != null && modbusMasterWriteConfigs.Count > 0)
        {
            int blockCount = Math.Min(modbusMasterWriteConfigs.Count, ModbusRegisterMap.ModbusMasterWriteMaxBlocks);
            for (int i = 0; i < blockCount; i++)
            {
                var blockRegs = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
                ModbusMasterCodec.EncodeWriteBlock(modbusMasterWriteConfigs[i], blockRegs);

                ushort blockStartAddr = ModbusRegisterMap.GetModbusMasterWriteBlockAddress(i);
                progressLogger?.Invoke($"[MB WRITE] Writing {blockRegs.Length} registers to 0x{blockStartAddr:X04} (Block {i})...");
                var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                    blockStartAddr,
                    blockRegs.AsMemory());

                foreach (var (chunkAddress, chunkMemory) in chunks)
                {
                    await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
                }
            }

            // Vô hiệu hóa các khối Modbus Master Write cũ không còn trong cấu hình (xóa sạch 64 thanh ghi về 0)
            var zeroMmwBlock = new ushort[ModbusRegisterMap.ModbusMasterWriteRegistersPerBlock];
            for (int j = blockCount; j < ModbusRegisterMap.ModbusMasterWriteMaxBlocks; j++)
            {
                ushort staleBlockAddr = ModbusRegisterMap.GetModbusMasterWriteBlockAddress(j);
                var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(staleBlockAddr, zeroMmwBlock.AsMemory());
                foreach (var (chunkAddress, chunkMemory) in chunks)
                {
                    await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        // 4.3 Nạp cấu hình PULSE_OUT / Direct Write blocks (0x1200) nếu có
        if (directWriteConfigs != null && directWriteConfigs.Count > 0)
        {
            int blockCount = Math.Min(directWriteConfigs.Count, ModbusRegisterMap.DirectWriteMaxBlocks);
            for (int i = 0; i < blockCount; i++)
            {
                var blockRegs = new ushort[ModbusRegisterMap.DirectWriteRegistersPerBlock];
                DirectWriteCodec.EncodeBlock(directWriteConfigs[i], blockRegs);

                ushort blockStartAddr = ModbusRegisterMap.GetDirectWriteBlockAddress(i);
                progressLogger?.Invoke($"[PULSE_OUT] Writing {blockRegs.Length} registers to 0x{blockStartAddr:X04} (Block {i})...");
                var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                    blockStartAddr,
                    blockRegs.AsMemory());

                foreach (var (chunkAddress, chunkMemory) in chunks)
                {
                    await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
                }
            }

            // Vô hiệu hóa các khối PULSE_OUT cũ không còn trong cấu hình (xóa sạch 16 thanh ghi về 0)
            var zeroPulseBlock = new ushort[ModbusRegisterMap.DirectWriteRegistersPerBlock];
            for (int j = blockCount; j < ModbusRegisterMap.DirectWriteMaxBlocks; j++)
            {
                ushort staleBlockAddr = ModbusRegisterMap.GetDirectWriteBlockAddress(j);
                await _client.WriteMultipleRegistersAsync(slaveId, staleBlockAddr, zeroPulseBlock, cancellationToken).ConfigureAwait(false);
            }
        }

        // 4.4 Nạp cấu hình Cổng RS-485 Modbus Master (0x1400) nếu có (SPLC-SPEC-MBM-003)
        if (modbusPortConfigs != null && modbusPortConfigs.Count > 0)
        {
            var portRegs = new ushort[ModbusRegisterMap.ModbusPortConfigTableTotalLength];
            ModbusPortConfigCodec.EncodeAllPorts(modbusPortConfigs, portRegs);

            progressLogger?.Invoke($"[RS-485 Ports] Writing {portRegs.Length} registers to 0x{ModbusRegisterMap.ModbusPortConfigBaseAddress:X04} (Multi-Port Config)...");
            var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                ModbusRegisterMap.ModbusPortConfigBaseAddress,
                portRegs.AsMemory());

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
