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
        DeployRulesAsync(slaveId, rules, timers, counters, mqttConfigs, brokerConfig, mqttSubConfigs, modbusMasterConfigs, null, null, null, null, null, null, null, null, cancellationToken, null);

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
        IReadOnlyList<ModbusSlaveMapEntryDto>? slaveMapConfigs = null,
        IReadOnlyList<byte>? tagDataTypes = null,
        DtuConfigDto? dtuConfig = null,
        MqttWillConfigDto? mqttWillConfig = null,
        MqttTlsCertificatesDto? tlsCertificates = null,
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
        bool hasTlsWork = (brokerConfig != null && brokerConfig.UseTls) || (tlsCertificates != null && tlsCertificates.HasAnyCertificates);
        bool hasMqttWork = hasMqttPub || hasMqttSub || hasBroker || hasTlsWork;
        bool hasMbRead = modbusMasterConfigs != null && modbusMasterConfigs.Count > 0;
        bool hasMbWrite = modbusMasterWriteConfigs != null && modbusMasterWriteConfigs.Count > 0;
        bool hasMbPorts = modbusPortConfigs != null && modbusPortConfigs.Count > 0;
        bool hasSlavePort = modbusPortConfigs != null && modbusPortConfigs.Any(p => p.Role == ModbusPortRole.Slave && p.Enabled);
        bool hasModbusWork = hasMbRead || hasMbWrite || hasMbPorts || hasSlavePort;
        ushort physicalRs485PortCount = 2;

        bool isMqttPubSupported = false;
        bool isMqttSubSupported = false;
        bool isMqttTlsSupported = false;
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
                    isMqttTlsSupported = (caps & SPLC_HardwareCapabilities.MQTT_TLS_CERT) != 0;
                    progressLogger?.Invoke($"[CAPS] MCU capabilities: 0x{(uint)caps:X8} (MQTT_PUB: {isMqttPubSupported}, MQTT_SUB: {isMqttSubSupported}, MQTT_TLS: {isMqttTlsSupported})");
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
                isMqttTlsSupported = false;
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

                if (hasTlsWork && !isMqttTlsSupported)
                {
                    return new RuleDeployResult(
                        false,
                        (ushort)SPLC_CommandStatus.ERROR,
                        SPLC_ErrorCode.UNSUPPORTED,
                        0,
                        $"Device does not declare MQTT TLS Certificate capability (Reported caps: 0x{(uint)caps:X8}, missing SPLC_CAP_MQTT_TLS_CERT 0x0080 at 0x0A08). Deployment aborted for safety.");
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
                if (capsReadException != null && (hasMbRead || hasMbWrite || hasSlavePort))
                {
                    return new RuleDeployResult(
                        false,
                        (ushort)SPLC_CommandStatus.ERROR,
                        SPLC_ErrorCode.UNSUPPORTED,
                        0,
                        $"Communication error reading hardware capabilities at 0x{ModbusRegisterMap.HardwareCapabilitiesAddress:X04}: {capsReadException.Message}. Modbus deployment aborted for safety.");
                }

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

                if (hasSlavePort && (caps & SPLC_HardwareCapabilities.MODBUS_SLAVE) == 0)
                {
                    return new RuleDeployResult(
                        false,
                        (ushort)SPLC_CommandStatus.ERROR,
                        SPLC_ErrorCode.UNSUPPORTED,
                        0,
                        $"Device does not support Modbus Slave (Reported caps: 0x{(uint)caps:X8}, missing SPLC_CAP_MODBUS_SLAVE at 0x0A08). Deployment aborted for safety.");
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

        // 3.4 Nạp cấu hình & phân đoạn chứng chỉ MQTT TLS / mTLS (0x0F00 - 0x0F46) nếu có
        if (hasTlsWork && isMqttTlsSupported)
        {
            var tlsResult = await DeployTlsCertificatesAsync(
                slaveId,
                brokerConfig,
                tlsCertificates,
                cancellationToken,
                progressLogger).ConfigureAwait(false);

            if (!tlsResult.IsSuccess)
            {
                return tlsResult;
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

        // 4.4.1 Bảng Ánh Xạ Modbus Slave (0x1500 cũ) đã bị hủy bỏ theo kiến trúc Fixed Memory Map v2.12.0 (Zero Legacy)
        // Firmware sử dụng bản đồ cố định được nạp sẵn từ nhà máy (O(1) direct dispatch), không còn nạp bảng 0x1500.

        // 4.5 Nạp cấu hình MQTT Will Message (0x0E60) nếu có
        var effectiveWill = mqttWillConfig ?? dtuConfig?.MqttWillConfig;
        if (effectiveWill != null && effectiveWill.Enabled)
        {
            var willRegs = new ushort[ModbusRegisterMap.MqttWillConfigRegisters];
            MqttWillConfigCodec.Encode(effectiveWill, willRegs);
            progressLogger?.Invoke($"[MQTT LWT] Writing {willRegs.Length} registers to 0x{ModbusRegisterMap.MqttWillConfigBaseAddress:X04} (Will Topic: {effectiveWill.Topic})...");
            var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                ModbusRegisterMap.MqttWillConfigBaseAddress,
                willRegs.AsMemory());
            foreach (var (chunkAddress, chunkMemory) in chunks)
            {
                await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
            }
        }
        else if (effectiveWill != null && !effectiveWill.Enabled)
        {
            // Vô hiệu hóa (disable) an toàn theo SPLC-SPEC-MQTT-004: ghi 32 registers về 0 (will_control = 0)
            var zeroWillRegs = new ushort[ModbusRegisterMap.MqttWillConfigRegisters];
            progressLogger?.Invoke($"[MQTT LWT] Disabling LWT: clearing {zeroWillRegs.Length} registers at 0x{ModbusRegisterMap.MqttWillConfigBaseAddress:X04}...");
            var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                ModbusRegisterMap.MqttWillConfigBaseAddress,
                zeroWillRegs.AsMemory());
            foreach (var (chunkAddress, chunkMemory) in chunks)
            {
                await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
            }
        }

        // 4.6 Nạp cấu hình DTU Gateway (0x1600 - 0x16BF) nếu có (SPLC-SPEC-DTU-001)
        if (dtuConfig != null && dtuConfig.Enabled)
        {
            // A. HTTP REST Client (0x1600)
            if (dtuConfig.HttpConfig != null && (dtuConfig.UplinkMode == DtuUplinkMode.Http || dtuConfig.HttpConfig.Enabled))
            {
                var httpRegs = new ushort[ModbusRegisterMap.DtuHttpConfigRegisters];
                DtuConfigCodec.EncodeHttp(dtuConfig.HttpConfig, httpRegs);
                progressLogger?.Invoke($"[DTU HTTP] Writing {httpRegs.Length} registers to 0x{ModbusRegisterMap.DtuHttpConfigBaseAddress:X04} (Url: {dtuConfig.HttpConfig.Url})...");
                var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                    ModbusRegisterMap.DtuHttpConfigBaseAddress,
                    httpRegs.AsMemory());
                foreach (var (chunkAddress, chunkMemory) in chunks)
                {
                    await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
                }
            }

            // B. TCP Socket Client/Server (0x1640)
            if (dtuConfig.TcpConfig != null && (dtuConfig.UplinkMode == DtuUplinkMode.TcpClient || dtuConfig.UplinkMode == DtuUplinkMode.TcpServer || dtuConfig.TcpConfig.Enabled))
            {
                var tcpRegs = new ushort[ModbusRegisterMap.DtuTcpConfigRegisters];
                DtuConfigCodec.EncodeTcp(dtuConfig.TcpConfig, tcpRegs);
                progressLogger?.Invoke($"[DTU TCP] Writing {tcpRegs.Length} registers to 0x{ModbusRegisterMap.DtuTcpConfigBaseAddress:X04} ({dtuConfig.TcpConfig.SocketMode} Port: {dtuConfig.TcpConfig.RemotePort})...");
                var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                    ModbusRegisterMap.DtuTcpConfigBaseAddress,
                    tcpRegs.AsMemory());
                foreach (var (chunkAddress, chunkMemory) in chunks)
                {
                    await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
                }
            }

            // C. Routing & Serial Mode (0x1680)
            var routingRegs = new ushort[ModbusRegisterMap.DtuRoutingConfigRegisters];
            DtuConfigCodec.EncodeRouting(dtuConfig, routingRegs);
            progressLogger?.Invoke($"[DTU ROUTING] Writing {routingRegs.Length} registers to 0x{ModbusRegisterMap.DtuRoutingConfigBaseAddress:X04} (Serial: {dtuConfig.SerialMode}, Uplink: {dtuConfig.UplinkMode})...");
            var routingChunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                ModbusRegisterMap.DtuRoutingConfigBaseAddress,
                routingRegs.AsMemory());
            foreach (var (chunkAddress, chunkMemory) in routingChunks)
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
        ).ConfigureAwait(false);

        // 3. Bước 2 (R10): Ghi STAGING_RULE_TABLE (0x9010) theo các chunks an toàn (16 regs = 1 rule)
        if (totalRegisters > 0)
        {
            var chunks = ModbusChunkPlanner.PlanWriteChunks<ushort>(
                ModbusRegisterMap.StagingRuleTableBaseAddress,
                stagingRegisters.AsMemory()
            );

            foreach (var (chunkAddress, chunkMemory) in chunks)
            {
                await _client.WriteMultipleRegistersAsync(slaveId, chunkAddress, chunkMemory, cancellationToken).ConfigureAwait(false);
            }
        }

        // Đọc phiên bản Active Rule Version hiện tại trước khi commit để kiểm chứng sau commit
        ushort initialVersion = 0;
        try
        {
            var initVerRegs = await _client.ReadHoldingRegistersAsync(
                slaveId,
                ModbusRegisterMap.ActiveRuleVersionAddress,
                1,
                cancellationToken
            ).ConfigureAwait(false);
            if (initVerRegs.Length > 0)
            {
                initialVersion = initVerRegs[0];
            }
        }
        catch
        {
            // Bỏ qua lỗi đọc phiên bản ban đầu nếu thiết bị chưa có phiên bản
        }

        // 4. Bước 3 (R10): Ghi COMMIT_COMMAND (0xA000) với Magic 0xA5A5
        await _client.WriteSingleRegisterAsync(
            slaveId,
            ModbusRegisterMap.CommitCommandAddress,
            ModbusRegisterMap.CommitMagic,
            cancellationToken
        ).ConfigureAwait(false);

        // 5. Bước 4 (R10): Chờ MCU xác thực CRC và kiểm tra trạng thái CONFIG_STATUS
        return await WaitForCommitCompletionAsync(slaveId, initialVersion, cancellationToken).ConfigureAwait(false);
    }

    private async Task<RuleDeployResult> WaitForCommitCompletionAsync(byte slaveId, ushort initialVersion, CancellationToken cancellationToken)
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
            ).ConfigureAwait(false);

            ushort status = statusRegs[0];
            var errorCode = (SPLC_ErrorCode)statusRegs[1];

            // 0=IDLE, 1=RECEIVING, 2=VERIFYING, 3=READY, 4=ERROR
            if (status == 4) // ERROR
            {
                return new RuleDeployResult(
                    IsSuccess: false,
                    ConfigStatus: status,
                    ErrorCode: errorCode,
                    ErrorMessage: $"MCU commit failed with error code: {errorCode}"
                );
            }

            var verRegs = await _client.ReadHoldingRegistersAsync(
                slaveId,
                ModbusRegisterMap.ActiveRuleVersionAddress,
                1,
                cancellationToken
            ).ConfigureAwait(false);
            ushort currentVersion = verRegs.Length > 0 ? verRegs[0] : (ushort)0;

            if (status == 3 && (currentVersion != initialVersion || (initialVersion == 0 && currentVersion > 0)))
            {
                return new RuleDeployResult(
                    IsSuccess: true,
                    ConfigStatus: status,
                    ErrorCode: SPLC_ErrorCode.NONE,
                    ActiveVersion: currentVersion
                );
            }

            await Task.Delay(35, cancellationToken).ConfigureAwait(false);
        }

        return new RuleDeployResult(
            IsSuccess: false,
            ConfigStatus: 4,
            ErrorCode: SPLC_ErrorCode.BUSY,
            ErrorMessage: $"Timed out waiting for MCU to verify and commit staged rules (Active version remained at {initialVersion})."
        );
    }

    private async Task<RuleDeployResult> DeployTlsCertificatesAsync(
        byte slaveId,
        MqttBrokerConfigDto? brokerConfig,
        MqttTlsCertificatesDto? tlsCertificates,
        CancellationToken cancellationToken,
        Action<string>? progressLogger)
    {
        progressLogger?.Invoke($"[MQTT TLS] Checking MCU certificate status at 0x{ModbusRegisterMap.MqttCertActiveTypeMaskAddress:X04}...");

        var statusRegs = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.MqttCertActiveTypeMaskAddress,
            4,
            cancellationToken).ConfigureAwait(false);

        ushort activeMask = statusRegs != null && statusRegs.Length >= 1 ? statusRegs[0] : (ushort)0;
        ushort storedCrcRootCa = statusRegs != null && statusRegs.Length >= 2 ? statusRegs[1] : (ushort)0;
        ushort storedCrcClientCert = statusRegs != null && statusRegs.Length >= 3 ? statusRegs[2] : (ushort)0;
        ushort storedCrcClientKey = statusRegs != null && statusRegs.Length >= 4 ? statusRegs[3] : (ushort)0;

        // 1. Đồng bộ Server Root CA
        if (tlsCertificates?.RootCaBytes != null && tlsCertificates.RootCaBytes.Length > 0)
        {
            var caResult = await SyncSingleCertificateAsync(
                slaveId,
                ModbusRegisterMap.MqttCertTypeRootCa,
                tlsCertificates.RootCaBytes,
                activeMaskBit: 0x0001,
                currentActiveMask: activeMask,
                storedCrc: storedCrcRootCa,
                cancellationToken,
                progressLogger).ConfigureAwait(false);

            if (!caResult.IsSuccess) return caResult;
        }

        // 2. Đồng bộ Mutual TLS (mTLS) Client Certificate & Client Key nếu được bật
        if (brokerConfig?.UseMtls == true)
        {
            if (tlsCertificates?.ClientCertBytes != null && tlsCertificates.ClientCertBytes.Length > 0)
            {
                var clientCertResult = await SyncSingleCertificateAsync(
                    slaveId,
                    ModbusRegisterMap.MqttCertTypeClientCert,
                    tlsCertificates.ClientCertBytes,
                    activeMaskBit: 0x0002,
                    currentActiveMask: activeMask,
                    storedCrc: storedCrcClientCert,
                    cancellationToken,
                    progressLogger).ConfigureAwait(false);

                if (!clientCertResult.IsSuccess) return clientCertResult;
            }

            if (tlsCertificates?.ClientKeyBytes != null && tlsCertificates.ClientKeyBytes.Length > 0)
            {
                var clientKeyResult = await SyncSingleCertificateAsync(
                    slaveId,
                    ModbusRegisterMap.MqttCertTypeClientKey,
                    tlsCertificates.ClientKeyBytes,
                    activeMaskBit: 0x0004,
                    currentActiveMask: activeMask,
                    storedCrc: storedCrcClientKey,
                    cancellationToken,
                    progressLogger).ConfigureAwait(false);

                if (!clientKeyResult.IsSuccess) return clientKeyResult;
            }
        }

        return new RuleDeployResult(true, 0, SPLC_ErrorCode.NONE, 0);
    }

    private async Task<RuleDeployResult> SyncSingleCertificateAsync(
        byte slaveId,
        byte certType,
        byte[] certBytes,
        ushort activeMaskBit,
        ushort currentActiveMask,
        ushort storedCrc,
        CancellationToken cancellationToken,
        Action<string>? progressLogger)
    {
        string certName = certType switch
        {
            ModbusRegisterMap.MqttCertTypeRootCa => "Root CA",
            ModbusRegisterMap.MqttCertTypeClientCert => "Client Certificate",
            ModbusRegisterMap.MqttCertTypeClientKey => "Client Private Key",
            _ => $"Certificate 0x{certType:X02}"
        };

        // 1. Tính toán CRC-16 Checksum của file chứng chỉ
        ushort localCrc = Crc16Modbus.Compute(certBytes);

        // 2. Kiểm tra Smart Sync Checksum: Nếu cert đã active và trùng khớp CRC-16 -> Bỏ qua
        bool isAlreadyActive = (currentActiveMask & activeMaskBit) != 0;
        if (isAlreadyActive && storedCrc == localCrc)
        {
            progressLogger?.Invoke($"[MQTT TLS] {certName} is up-to-date on MCU (CRC-16 matched: 0x{localCrc:X04}). Skipping chunk upload.");
            return new RuleDeployResult(true, 0, SPLC_ErrorCode.NONE, 0);
        }

        // 3. Phân mảnh chứng chỉ theo struct 128 bytes (64 registers)
        var chunks = TlsCertificateCodec.SliceCertificate(certType, certBytes);
        progressLogger?.Invoke($"[MQTT TLS] Slicing {certName} ({certBytes.Length} bytes) into {chunks.Count} chunks...");

        var chunkRegs = new ushort[ModbusRegisterMap.MqttCertChunkRegisters];

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            TlsCertificateCodec.EncodeChunk(chunk, chunkRegs);

            const int maxRetries = 3;
            bool chunkAccepted = false;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                // Ghi 64 thanh ghi vào 0x0F00
                await _client.WriteMultipleRegistersAsync(
                    slaveId,
                    ModbusRegisterMap.MqttCertStagingBaseAddress,
                    chunkRegs,
                    cancellationToken).ConfigureAwait(false);

                // Đọc phản hồi ACK (0x0F40) và Trạng thái lỗi (0x0F41)
                var ackRegs = await _client.ReadHoldingRegistersAsync(
                    slaveId,
                    ModbusRegisterMap.MqttCertAckAddress,
                    2,
                    cancellationToken).ConfigureAwait(false);

                if (ackRegs != null && ackRegs.Length >= 2)
                {
                    ushort ackIndex = ackRegs[0];
                    ushort statusCode = ackRegs[1];

                    if (statusCode == 0 && ackIndex == chunk.ChunkIndex)
                    {
                        chunkAccepted = true;
                        break;
                    }

                    progressLogger?.Invoke($"[MQTT TLS WARNING] {certName} Chunk {i} attempt {attempt}/{maxRetries} failed (ACK={ackIndex}, Status={statusCode}). Retrying...");
                }
            }

            if (!chunkAccepted)
            {
                return new RuleDeployResult(
                    false,
                    (ushort)SPLC_CommandStatus.ERROR,
                    SPLC_ErrorCode.FLASH,
                    0,
                    $"Failed to stage {certName} chunk {i}/{chunks.Count} after {maxRetries} attempts.");
            }
        }

        // 4. Phát lệnh Atomic Commit (ghi 0xA5A5 vào 0x0F42)
        progressLogger?.Invoke($"[MQTT TLS] Emitting Atomic Commit magic 0xA5A5 to 0x{ModbusRegisterMap.MqttCertCommitAddress:X04} for {certName}...");
        await _client.WriteSingleRegisterAsync(
            slaveId,
            ModbusRegisterMap.MqttCertCommitAddress,
            ModbusRegisterMap.MqttCertCommitMagic,
            cancellationToken).ConfigureAwait(false);

        // 5. Xác thực sau commit
        var verifyRegs = await _client.ReadHoldingRegistersAsync(
            slaveId,
            ModbusRegisterMap.MqttCertStatusAddress,
            3,
            cancellationToken).ConfigureAwait(false);

        if (verifyRegs != null && verifyRegs.Length >= 3)
        {
            ushort status = verifyRegs[0];
            ushort newMask = verifyRegs[2];

            if (status != 0 || (newMask & activeMaskBit) == 0)
            {
                return new RuleDeployResult(
                    false,
                    (ushort)SPLC_CommandStatus.ERROR,
                    SPLC_ErrorCode.FLASH,
                    0,
                    $"MCU reported error finalizing {certName} commit (Status={status}, ActiveMask=0x{newMask:X04}).");
            }
        }

        progressLogger?.Invoke($"[MQTT TLS OK] {certName} committed successfully to MCU Flash (CRC-16: 0x{localCrc:X04}).");
        return new RuleDeployResult(true, 0, SPLC_ErrorCode.NONE, 0);
    }
}
