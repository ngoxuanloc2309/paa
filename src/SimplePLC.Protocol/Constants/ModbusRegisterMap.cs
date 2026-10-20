namespace SimplePLC.Protocol.Constants;

/// <summary>
/// Nguồn chân lý duy nhất (R3) cho bản đồ địa chỉ thanh ghi Modbus V1 theo Data Contract V1.7.
/// Quy ước: 1 register = 16 bit. FC03 = Read. FC16 = Write. RuleRecord = 32 byte = 16 register.
/// </summary>
public static class ModbusRegisterMap
{
    // ==========================================
    // 1. KÍCH THƯỚC & GIỚI HẠN CHUẨN
    // ==========================================
    public const int RegistersPerRule = 16;
    public const int BytesPerRule = 32;
    public const int MaxRules = 100;
    public const int MaxRuntimeTags = 128;
    public const int RegistersPerRuntimeTag = 2;

    /// <summary>
    /// Định danh phiên bản Wire Profile V1 theo Contract V1.9.
    /// </summary>
    public const ushort WireProfileV1 = 1;

    /// <summary>
    /// Định danh phiên bản Wire Profile V2 theo Contract V2.0 (Hỗ trợ Function Block subsystem 0x0B00 và Diag Control).
    /// </summary>
    public const ushort WireProfileV2 = 2;

    // Giới hạn tài nguyên tối đa của Wire Profile V1
    public const ushort MaxDigitalInputs = 8;
    public const ushort MaxDigitalOutputs = 8;
    public const ushort MaxAnalogInputs = 4;
    public const ushort MaxVirtualFlags = 32;
    public const ushort MaxVirtualRegisters = 32;
    public const ushort MaxRetentiveRegisters = 32;
    public const ushort MaxCounters = 8;

    // ==========================================
    // 1b. TAG LAYOUT — tính động theo số lượng thực tế
    // ==========================================

    /// <summary>
    /// Tính TagLayoutMap động từ số lượng tài nguyên thực của thiết bị.
    /// DiBase = 0, DoBase = diCount, AiBase = diCount+doCount, ...
    /// </summary>
    public static SimplePLC.Protocol.Models.TagLayoutMap ComputeLayout(
        ushort diCount, ushort doCount, ushort aiCount,
        ushort vflagCount, ushort vregCount, ushort vregRetainCount, ushort counterCount)
        => new(diCount, doCount, aiCount, vflagCount, vregCount, vregRetainCount, counterCount);

    /// <summary>
    /// Layout mặc định đầy đủ (8 DI, 8 DO, 4 AI, 32 VFLAG, 32 VREG, 32 VREG_RETAIN, 8 COUNTER).
    /// Dùng khi chưa có DeviceResourceInfo từ MCU.
    /// </summary>
    public static SimplePLC.Protocol.Models.TagLayoutMap DefaultLayout
        => SimplePLC.Protocol.Models.TagLayoutMap.Default;

    /// <summary>
    /// Magic number ghi vào COMMIT_COMMAND (0xA000) để MCU xác minh CRC và commit Rule Table.
    /// </summary>
    public const ushort CommitMagic = 0xA5A5;

    /// <summary>
    /// Bitmask cho cờ phủ định (NEGATE) ở bit 15 của guard_tag.
    /// </summary>
    public const ushort GuardTagNegateMask = 0x8000;

    /// <summary>
    /// Bitmask cho tag index ở bit 0..14 của guard_tag.
    /// </summary>
    public const ushort GuardTagIndexMask = 0x7FFF;

    /// <summary>
    /// Giá trị Sentinel quy định Rule KHÔNG CÓ Guard (0x7FFF = 32767).
    /// </summary>
    public const ushort GuardTagNone = 0x7FFF;

    // ==========================================
    // 2. PHÂN VÙNG CORE & RUNTIME (0x0000 - 0x0A02)
    // ==========================================
    public const ushort DeviceDescriptorAddress = 0x0000;
    public const ushort DeviceDescriptorLength = 10;

    public const ushort RuleTableInfoAddress = 0x0010;
    public const ushort RuleTableInfoLength = 1;

    /// <summary>
    /// Vùng Self-Describing Device Resource Info (Contract V1.9: 0x0020..0x0029, 10 registers = 20 bytes, RO).
    /// </summary>
    public const ushort DeviceResourceInfoAddress = 0x0020;
    public const ushort DeviceResourceInfoLength = 10;

    public const ushort ActiveRuleTableBaseAddress = 0x0100;
    public const ushort ActiveRuleTableMaxLength = MaxRules * RegistersPerRule; // 1600 registers

    public const ushort DeviceHealthAddress = 0x0800;
    public const ushort DeviceHealthLength = 10;

    /// <summary>
    /// Vùng Real-Time Clock (RTC) đồng bộ Unix Epoch UTC và Timezone Offset (0x0810..0x0813, 4 registers = 8 bytes, RW).
    /// </summary>
    public const ushort RtcClockAddress = 0x0810;
    public const ushort RtcClockLength = 4;

    public const ushort RtcFlagSynced = 0x0001;
    public const ushort RtcFlagHwPresent = 0x0002;
    public const ushort RtcFlagBatteryLow = 0x0004;

    public const ushort RuntimeTagValuesBaseAddress = 0x0900;
    public const ushort RuntimeTagValuesMaxLength = MaxRuntimeTags * RegistersPerRuntimeTag; // 256 registers

    public const ushort SystemCommandAddress = 0x0A00;
    public const ushort SystemCommandLength = 1;

    public const ushort SystemCommandResultAddress = 0x0A01;
    public const ushort SystemCommandResultLength = 2;

    /// <summary>
    /// Vùng khai báo năng lực phần cứng HardwareCapabilities (0x0A08..0x0A09, 2 registers = 4 bytes, uint32 Big-Endian, RO).
    /// </summary>
    public const ushort HardwareCapabilitiesAddress = 0x0A08;
    public const ushort HardwareCapabilitiesLength = 2;

    // ==========================================
    // 2.0 PHÂN VÙNG DIAGNOSTIC & COMMISSIONING V2 (0x0A20 - 0x0A24)
    // ==========================================
    public const ushort DiagBlockBaseAddress = 0x0A20;
    public const ushort DiagBlockLength = 5;

    public const ushort DiagCommandAddress = 0x0A20;
    public const ushort DiagCommandLength = 1;

    public const ushort DiagStateAddress = 0x0A21;
    public const ushort DiagStateLength = 1;

    public const ushort DiagFlagsAddress = 0x0A22;
    public const ushort DiagFlagsLength = 1;

    public const ushort DiagLeaseRemainingAddress = 0x0A23;
    public const ushort DiagLeaseRemainingLength = 1;

    public const ushort DiagErrorCodeAddress = 0x0A24;
    public const ushort DiagErrorCodeLength = 1;

    public const ushort DiagDefaultLeaseMs = 3000;
    public const ushort DiagHeartbeatIntervalMs = 1000;

    // ==========================================
    // 2.1 PHÂN VÙNG FUNCTION BLOCK V2 (0x0B00 - 0x0B7F)
    // ==========================================
    public const ushort FbTimerTableBaseAddress = 0x0B00;
    public const ushort FbCounterTableBaseAddress = 0x0B40;

    public const int FbRegistersPerBlock = 8;
    public const int FbBytesPerBlock = 16;
    public const int FbMaxTimers = 8;
    public const int FbMaxCounters = 8;

    public const ushort FbTimerTableLength = FbMaxTimers * FbRegistersPerBlock; // 64 registers
    public const ushort FbCounterTableLength = FbMaxCounters * FbRegistersPerBlock; // 64 registers
    public const ushort FbTableTotalLength = FbTimerTableLength + FbCounterTableLength; // 128 registers

    // Bitmasks cho STATUS_BITS của Timer (Offset +0)
    public const ushort FbTimerStatusBitIn = 0x0001;
    public const ushort FbTimerStatusBitQ = 0x0002;
    public const ushort FbTimerStatusBitReset = 0x0004;
    public const ushort FbTimerStatusBitRunning = 0x0008;

    // Bitmasks cho STATUS_BITS của Counter (Offset +0)
    public const ushort FbCounterStatusBitCu = 0x0001;
    public const ushort FbCounterStatusBitCd = 0x0002;
    public const ushort FbCounterStatusBitReset = 0x0004;
    public const ushort FbCounterStatusBitQ = 0x0008;

    // Sentinel cho Counter Retain Tag Index không lưu Flash (0xFFFF)
    public const ushort FbCounterRetainNone = 0xFFFF;

    // ==========================================
    // 2.2 PHÂN VÙNG MQTT TELEMETRY V2 (0x0C00 - 0x0D7F)
    // SPLC-SPEC-MQTT-001: 4 Blocks x 96 Registers = 384 Registers (768 Bytes)
    // ==========================================
    public const ushort MqttTelemetryTableBaseAddress = 0x0C00;
    public const int MqttRegistersPerBlock = 96;
    public const int MqttBytesPerBlock = 192;
    public const int MqttMaxBlocks = 4;
    public const int MqttHeaderRegisters = 32;
    public const int MqttTemplateRegisters = 64;
    public const int MqttTopicMaxChars = 32;
    public const int MqttTemplateMaxChars = 128;
    public const int MqttMaxTagsPerBlock = 8;
    public const ushort MqttTableTotalLength = MqttMaxBlocks * MqttRegistersPerBlock; // 384 registers

    // Bitmasks cho CONTROL_FLAGS của MQTT Block (Offset +0)
    public const ushort MqttControlBitEnabled = 0x0001;        // Bit 0: Enable
    public const ushort MqttControlModeMask = 0x0006;          // Bit 1..2: Publish Mode (0=Event, 1=COV, 2=Periodic)
    public const ushort MqttControlModeShift = 1;
    public const ushort MqttControlQosMask = 0x0018;           // Bit 3..4: QoS (0, 1, 2)
    public const ushort MqttControlQosShift = 3;
    public const ushort MqttControlBitRetain = 0x0020;         // Bit 5: Retain
    public const ushort MqttControlPayloadModeMask = 0x00C0;   // Bit 6..7: Payload Mode (0=JSON, 1=Raw, 2=Custom)
    public const ushort MqttControlPayloadModeShift = 6;

    // ==========================================
    // 2.3 PHÂN VÙNG MQTT SUBSCRIBE INBOUND (0x0D80 - 0x0DFF)
    // SPLC-SPEC-MQTT-002: 2 Blocks x 64 Registers = 128 Registers (256 Bytes)
    // ==========================================
    public const ushort MqttSubscribeTableBaseAddress = 0x0D80;
    public const int MqttSubscribeRegistersPerBlock = 64;
    public const int MqttSubscribeBytesPerBlock = 128;
    public const int MqttSubscribeMaxBlocks = 2;
    public const int MqttSubscribeMaxPinsPerBlock = 4;
    public const int MqttSubscribeJsonKeyMaxChars = 12;
    public const ushort MqttSubscribeTableTotalLength = MqttSubscribeMaxBlocks * MqttSubscribeRegistersPerBlock; // 128 registers

    public static ushort GetMqttSubscribeBlockAddress(int blockIndex) =>
        (ushort)(MqttSubscribeTableBaseAddress + (blockIndex * MqttSubscribeRegistersPerBlock));

    // Bitmasks cho CONTROL_FLAGS của Subscribe Block (Offset +0)
    public const ushort MqttSubControlBitEnabled = 0x0001;        // Bit 0: Enable
    public const ushort MqttSubControlQosMask = 0x0006;           // Bit 1..2: QoS (0, 1, 2)
    public const ushort MqttSubControlQosShift = 1;
    public const ushort MqttSubControlBitQueueFifo = 0x0008;      // Bit 3: 0=LatestOnly, 1=FIFO
    public const ushort MqttSubControlBitIgnoreRetain = 0x0010;   // Bit 4: Ignore Retained
    public const ushort MqttSubControlFailSafeMask = 0x0060;      // Bit 5..6: FailSafe (0=Keep, 1=ResetZero, 2=SetDefault)
    public const ushort MqttSubControlFailSafeShift = 5;

    // ==========================================
    // 2.4 PHÂN VÙNG MQTT BROKER CONFIGURATION (0x0E00 - 0x0E5F)
    // SPLC-SPEC-MQTT-001: 96 Registers = 192 Bytes
    // ==========================================
    public const ushort MqttBrokerConfigBaseAddress = 0x0E00;
    public const int MqttBrokerConfigRegisters = 96;
    public const int MqttBrokerConfigBytes = 192;
    public const int MqttBrokerHostMaxChars = 64;
    public const int MqttBrokerCredentialsMaxChars = 32;

    // Bitmasks cho BROKER_CONTROL (Offset +0)
    public const ushort MqttBrokerControlBitEnabled = 0x0001;        // Bit 0: Enable
    public const ushort MqttBrokerControlBitUseTls = 0x0002;         // Bit 1: TLS 8883
    public const ushort MqttBrokerControlBitCleanSession = 0x0004;   // Bit 2: Clean Session
    public const ushort MqttBrokerControlVersionMask = 0x0018;       // Bit 3..4: Version (0=3.1.1, 1=5.0)
    public const ushort MqttBrokerControlVersionShift = 3;

    // ==========================================
    // 2.5 PHÂN VÙNG MQTT TLS CERTIFICATE STAGING (0x0F00 - 0x0F4F)
    // SPLC-SPEC-MQTT-003: 64 Registers = 128 Bytes per Chunk Struct
    // ==========================================
    public const ushort MqttCertStagingBaseAddress = 0x0F00;
    public const int MqttCertChunkRegisters = 64;
    public const int MqttCertChunkBytes = 128;
    public const int MqttCertPayloadMaxBytes = 120;
    public const ushort MqttCertAckAddress = 0x0F40;
    public const ushort MqttCertStatusAddress = 0x0F41;
    public const ushort MqttCertCommitAddress = 0x0F42;
    public const ushort MqttCertActiveTypeMaskAddress = 0x0F43;
    public const ushort MqttCertCommitMagic = 0xA5A5;

    // Cert Types
    public const byte MqttCertTypeRootCa = 0x01;
    public const byte MqttCertTypeClientCert = 0x02;
    public const byte MqttCertTypeClientKey = 0x03;

    // ==========================================
    // 2.6 PHÂN VÙNG MODBUS RTU MASTER POLLER (0x1000 - 0x10FF)
    // SPLC-SPEC-MBM-001: 4 Blocks x 64 Registers = 256 Registers (512 Bytes)
    // ==========================================
    public const ushort ModbusMasterTableBaseAddress = 0x1000;
    public const int ModbusMasterRegistersPerBlock = 64;
    public const int ModbusMasterBytesPerBlock = 128;
    public const int ModbusMasterMaxBlocks = 4;
    public const int ModbusMasterMaxPinsPerBlock = 4;
    public const int ModbusMasterPinLabelMaxChars = 12;
    public const ushort ModbusMasterTableTotalLength = ModbusMasterMaxBlocks * ModbusMasterRegistersPerBlock; // 256 registers

    public static ushort GetModbusMasterBlockAddress(int blockIndex) =>
        (ushort)(ModbusMasterTableBaseAddress + (blockIndex * ModbusMasterRegistersPerBlock));

    // ==========================================
    // 2.7 PHÂN VÙNG MODBUS RTU MASTER WRITER / ACTUATOR (0x1100 - 0x11FF)
    // SPLC-SPEC-MBM-002: 4 Blocks x 64 Registers = 256 Registers (512 Bytes)
    // ==========================================
    public const ushort ModbusMasterWriteTableBaseAddress = 0x1100;
    public const int ModbusMasterWriteRegistersPerBlock = 64;
    public const int ModbusMasterWriteBytesPerBlock = 128;
    public const int ModbusMasterWriteMaxBlocks = 4;
    public const ushort ModbusMasterWriteTableTotalLength = ModbusMasterWriteMaxBlocks * ModbusMasterWriteRegistersPerBlock; // 256 registers

    public static ushort GetModbusMasterWriteBlockAddress(int blockIndex) =>
        (ushort)(ModbusMasterWriteTableBaseAddress + (blockIndex * ModbusMasterWriteRegistersPerBlock));

    // ==========================================
    // 2.8 PHÂN VÙNG DIRECT HARDWARE OUTPUT (0x1200 - 0x12FF)
    // SPLC-SPEC-DIRECT-WRITE-003: 8 Blocks x 16 Registers = 128 Registers (256 Bytes)
    // ==========================================
    public const ushort DirectWriteTableBaseAddress = 0x1200;
    public const int DirectWriteRegistersPerBlock = 16;
    public const int DirectWriteBytesPerBlock = 32;
    public const int DirectWriteMaxBlocks = 8;
    public const ushort DirectWriteTableTotalLength = DirectWriteMaxBlocks * DirectWriteRegistersPerBlock; // 128 registers

    public static ushort GetDirectWriteBlockAddress(int blockIndex) =>
        (ushort)(DirectWriteTableBaseAddress + (blockIndex * DirectWriteRegistersPerBlock));

    // ==========================================
    // 2.9 PHÂN VÙNG WI-FI ST67 SUBSYSTEM (0x1300 - 0x13FF)
    // SPLC-SPEC-WIFI-004-ICD: 256 Registers = 512 Bytes
    // ==========================================
    public const ushort WifiConfigBaseAddress = 0x1300;
    public const int WifiConfigRegisters = 64;
    public const int WifiConfigBytes = 128;
    public const int WifiMaxSsidChars = 32;
    public const int WifiMaxPasswordChars = 64;

    public const ushort WifiControlAddress = 0x1340;
    public const int WifiControlRegisters = 16;
    public const int WifiControlBytes = 32;

    public const ushort WifiScanTableBaseAddress = 0x1350;
    public const int WifiScanTableRegisters = 176;
    public const int WifiScanTableBytes = 352;
    public const int WifiMaxScanAps = 8;
    public const int WifiRegistersPerAp = 22;
    public const int WifiBytesPerAp = 44;

    // Control flags bitmask
    public const ushort WifiControlBitEnable = 0x0001;
    public const ushort WifiControlModeMask = 0x0006;
    public const ushort WifiControlModeShift = 1;
    public const ushort WifiControlBitDhcp = 0x0008;
    public const ushort WifiControlBitAutoReconnect = 0x0010;

    // Commands at 0x1340
    public const ushort WifiCmdIdle = 0x0000;
    public const ushort WifiCmdStartScan = 0x0001;
    public const ushort WifiCmdConnect = 0x0002;
    public const ushort WifiCmdDisconnect = 0x0003;
    public const ushort WifiCmdForget = 0x0004;

    // Scan State at 0x1341
    public const ushort WifiScanStateIdle = 0;
    public const ushort WifiScanStateScanning = 1;
    public const ushort WifiScanStateCompleted = 2;
    public const ushort WifiScanStateFailed = 3;

    // Connection Status at 0x133A
    public const ushort WifiConnDisconnected = 0;
    public const ushort WifiConnConnecting = 1;
    public const ushort WifiConnConnected = 2;
    public const ushort WifiConnGotIp = 3;
    public const ushort WifiConnAuthFailed = 4;
    public const ushort WifiConnSsidNotFound = 5;
    public const ushort WifiConnApModeActive = 6;

    // Auth Modes
    public const byte WifiAuthOpen = 0;
    public const byte WifiAuthWep = 1;
    public const byte WifiAuthWpaPsk = 2;
    public const byte WifiAuthWpa2Psk = 3;
    public const byte WifiAuthWpaWpa2Mixed = 4;
    public const byte WifiAuthWpa2Enterprise = 5;
    public const byte WifiAuthWpa3Psk = 6;

    public static ushort GetWifiApAddress(int apIndex)
    {
        if (apIndex < 0 || apIndex >= WifiMaxScanAps)
            throw new ArgumentOutOfRangeException(nameof(apIndex), $"AP index must be between 0 and {WifiMaxScanAps - 1}.");

        return (ushort)(WifiScanTableBaseAddress + (apIndex * WifiRegistersPerAp));
    }

    // ==========================================
    // 3. PHÂN VÙNG RULE TRANSFER & COMMIT (0x9000 - 0xA001)
    // ==========================================
    public const ushort ConfigStatusAddress = 0x9000;
    public const ushort ConfigStatusLength = 1;

    public const ushort ConfigErrorCodeAddress = 0x9001;
    public const ushort ConfigErrorCodeLength = 1;

    public const ushort RuleCountStagedAddress = 0x9002;
    public const ushort RuleCountStagedLength = 1;

    public const ushort ExpectedCrc16Address = 0x9003;
    public const ushort ExpectedCrc16Length = 1;

    public const ushort ActiveRuleCountAddress = 0x9004;
    public const ushort ActiveRuleCountLength = 1;

    public const ushort ActiveRuleCrc16Address = 0x9005;
    public const ushort ActiveRuleCrc16Length = 1;

    public const ushort ReservedAddress = 0x9006;
    public const ushort ReservedLength = 10;

    public const ushort StagingRuleTableBaseAddress = 0x9010;
    public const ushort StagingRuleTableMaxLength = MaxRules * RegistersPerRule; // 1600 registers

    public const ushort CommitCommandAddress = 0xA000;
    public const ushort CommitCommandLength = 1;

    public const ushort ActiveRuleVersionAddress = 0xA001;
    public const ushort ActiveRuleVersionLength = 1;

    // ==========================================
    // 4. HELPER TÍNH TOÁN ĐỊA CHỈ THANH GHI
    // ==========================================
    
    /// <summary>
    /// Tính địa chỉ bắt đầu của một rule trong bảng Active Rule Table (0-based ruleIndex).
    /// Ví dụ: index 0 -> 0x0100; index 2 -> 0x0120.
    /// </summary>
    public static ushort GetActiveRuleAddress(int ruleIndex)
    {
        if (ruleIndex < 0 || ruleIndex >= MaxRules)
            throw new ArgumentOutOfRangeException(nameof(ruleIndex), $"Rule index must be between 0 and {MaxRules - 1}.");

        return (ushort)(ActiveRuleTableBaseAddress + ruleIndex * RegistersPerRule);
    }

    /// <summary>
    /// Tính địa chỉ bắt đầu của một rule trong bảng Staging Rule Table (0-based ruleIndex).
    /// Ví dụ: index 0 -> 0x9010; index 2 -> 0x9030.
    /// </summary>
    public static ushort GetStagingRuleAddress(int ruleIndex)
    {
        if (ruleIndex < 0 || ruleIndex >= MaxRules)
            throw new ArgumentOutOfRangeException(nameof(ruleIndex), $"Rule index must be between 0 and {MaxRules - 1}.");

        return (ushort)(StagingRuleTableBaseAddress + ruleIndex * RegistersPerRule);
    }

    /// <summary>
    /// Tính địa chỉ bắt đầu của một Tag runtime value (0-based tagIndex).
    /// Ví dụ: index 0 -> 0x0900; index 1 -> 0x0902.
    /// </summary>
    public static ushort GetRuntimeTagAddress(int tagIndex)
    {
        if (tagIndex < 0 || tagIndex >= MaxRuntimeTags)
            throw new ArgumentOutOfRangeException(nameof(tagIndex), $"Tag index must be between 0 and {MaxRuntimeTags - 1}.");

        return (ushort)(RuntimeTagValuesBaseAddress + tagIndex * RegistersPerRuntimeTag);
    }

    /// <summary>
    /// Tính địa chỉ bắt đầu của Timer block thứ timerIndex (0..7) trong bảng FbTimerTable.
    /// Ví dụ: index 0 -> 0x0B00; index 1 -> 0x0B08.
    /// </summary>
    public static ushort GetFbTimerAddress(int timerIndex)
    {
        if (timerIndex < 0 || timerIndex >= FbMaxTimers)
            throw new ArgumentOutOfRangeException(nameof(timerIndex), $"Timer index must be between 0 and {FbMaxTimers - 1}.");

        return (ushort)(FbTimerTableBaseAddress + (timerIndex << 3));
    }

    /// <summary>
    /// Tính địa chỉ bắt đầu của Counter block thứ counterIndex (0..7) trong bảng FbCounterTable.
    /// Ví dụ: index 0 -> 0x0B40; index 1 -> 0x0B48.
    /// </summary>
    public static ushort GetFbCounterAddress(int counterIndex)
    {
        if (counterIndex < 0 || counterIndex >= FbMaxCounters)
            throw new ArgumentOutOfRangeException(nameof(counterIndex), $"Counter index must be between 0 and {FbMaxCounters - 1}.");

        return (ushort)(FbCounterTableBaseAddress + (counterIndex << 3));
    }

    /// <summary>
    /// Tính địa chỉ bắt đầu của MQTT Telemetry block thứ blockIndex (0..3) trong bảng MqttTelemetryTable.
    /// Ví dụ: index 0 -> 0x0C00; index 1 -> 0x0C60.
    /// </summary>
    public static ushort GetMqttBlockAddress(int blockIndex)
    {
        if (blockIndex < 0 || blockIndex >= MqttMaxBlocks)
            throw new ArgumentOutOfRangeException(nameof(blockIndex), $"MQTT block index must be between 0 and {MqttMaxBlocks - 1}.");

        return (ushort)(MqttTelemetryTableBaseAddress + blockIndex * MqttRegistersPerBlock);
    }
}
