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
}
