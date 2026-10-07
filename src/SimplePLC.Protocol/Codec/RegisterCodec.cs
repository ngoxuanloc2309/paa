using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Thư viện giải mã / đóng gói thanh ghi Modbus chuẩn theo Data Contract V1.7.
/// Tuân thủ nguyên tắc:
/// - R3: Khế ước wire cố định 16 thanh ghi (32 bytes) cho mỗi RuleRecord.
/// - R4: Codec tường minh, không dùng memory marshal/cast trực tiếp.
/// - R8: Reserved registers luôn ghi 0x0000; receiver bỏ qua.
/// - Zero memory allocation khi dùng Span.
/// </summary>
public static class RegisterCodec
{
    // ==========================================
    // 1. RULE RECORD CODEC (32 BYTES = 16 REGISTERS)
    // ==========================================

    /// <summary>
    /// Mã hóa một RuleRecordDto thành 16 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void EncodeRuleRecord(RuleRecordDto rule, Span<ushort> destination)
    {
        if (destination.Length < ModbusRegisterMap.RegistersPerRule)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.RegistersPerRule} registers.", nameof(destination));

        // +0..+1: threshold_lo (int32_t, High Word -> Low Word)
        EncodeInt32(rule.ThresholdLo, destination.Slice(0, 2));

        // +2..+3: threshold_hi (int32_t, High Word -> Low Word)
        EncodeInt32(rule.ThresholdHi, destination.Slice(2, 2));

        // +4..+5: for_ms (uint32_t, High Word -> Low Word)
        EncodeUInt32(rule.ForMs, destination.Slice(4, 2));

        // +6..+7: action_param (int32_t, High Word -> Low Word)
        EncodeInt32(rule.ActionParam, destination.Slice(6, 2));

        // +8: trigger_tag (uint16_t)
        destination[8] = rule.TriggerTag;

        // +9: action_tag (uint16_t)
        destination[9] = rule.ActionTag;

        // +10: guard_tag (uint16_t: bit 0..14 index, bit 15 NEGATE)
        destination[10] = rule.GuardTag;

        // +11: enabled / trigger_type (2 x uint8_t: High byte = enabled, Low byte = trigger_type)
        byte enabledByte = rule.Enabled ? (byte)1 : (byte)0;
        byte triggerTypeByte = (byte)rule.TriggerType;
        destination[11] = PackBytes(enabledByte, triggerTypeByte);

        // +12: compare_op / action_type (2 x uint8_t: High byte = compare_op, Low byte = action_type)
        byte compareOpByte = (byte)rule.CompareOp;
        byte actionTypeByte = (byte)rule.ActionType;
        destination[12] = PackBytes(compareOpByte, actionTypeByte);

        // +13..+15: reserved[6] (6 x uint8_t = 3 registers, Sender luôn ghi 0)
        destination[13] = 0;
        destination[14] = 0;
        destination[15] = 0;
    }

    /// <summary>
    /// Giải mã 16 thanh ghi Modbus thành RuleRecordDto.
    /// </summary>
    public static RuleRecordDto DecodeRuleRecord(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.RegistersPerRule)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.RegistersPerRule} registers.", nameof(source));

        UnpackBytes(source[11], out byte enabledByte, out byte triggerTypeByte);
        UnpackBytes(source[12], out byte compareOpByte, out byte actionTypeByte);

        return new RuleRecordDto
        {
            ThresholdLo = DecodeInt32(source.Slice(0, 2)),
            ThresholdHi = DecodeInt32(source.Slice(2, 2)),
            ForMs = DecodeUInt32(source.Slice(4, 2)),
            ActionParam = DecodeInt32(source.Slice(6, 2)),
            TriggerTag = source[8],
            ActionTag = source[9],
            GuardTag = source[10],
            Enabled = enabledByte != 0,
            TriggerType = (SPLC_TriggerType)triggerTypeByte,
            CompareOp = (SPLC_CompareOp)compareOpByte,
            ActionType = (SPLC_ActionType)actionTypeByte
            // source[13..15] là reserved; receiver bỏ qua theo R8
        };
    }

    /// <summary>
    /// Mã hóa danh sách RuleRecordDto thành mảng thanh ghi liên tiếp.
    /// </summary>
    public static void EncodeRuleRecords(ReadOnlySpan<RuleRecordDto> rules, Span<ushort> destination)
    {
        int requiredLength = rules.Length * ModbusRegisterMap.RegistersPerRule;
        if (destination.Length < requiredLength)
            throw new ArgumentException($"Destination span requires {requiredLength} registers, but got {destination.Length}.", nameof(destination));

        for (int i = 0; i < rules.Length; i++)
        {
            EncodeRuleRecord(rules[i], destination.Slice(i * ModbusRegisterMap.RegistersPerRule, ModbusRegisterMap.RegistersPerRule));
        }
    }

    /// <summary>
    /// Giải mã mảng thanh ghi Modbus thành mảng RuleRecordDto.
    /// </summary>
    public static RuleRecordDto[] DecodeRuleRecords(ReadOnlySpan<ushort> source, int count)
    {
        int requiredLength = count * ModbusRegisterMap.RegistersPerRule;
        if (source.Length < requiredLength)
            throw new ArgumentException($"Source span requires {requiredLength} registers, but got {source.Length}.", nameof(source));

        var result = new RuleRecordDto[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = DecodeRuleRecord(source.Slice(i * ModbusRegisterMap.RegistersPerRule, ModbusRegisterMap.RegistersPerRule));
        }
        return result;
    }

    // ==========================================
    // 2. DEVICE DESCRIPTOR CODEC (10 REGISTERS)
    // ==========================================

    public static DeviceDescriptorDto DecodeDeviceDescriptor(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.DeviceDescriptorLength)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.DeviceDescriptorLength} registers.", nameof(source));

        return new DeviceDescriptorDto
        {
            DeviceClass = (SPLC_DeviceClass)source[0],
            DeviceVariant = source[1],
            HwVersionMajor = source[2],
            HwVersionMinor = source[3],
            HwVersionPatch = source[4],
            FwVersionMajor = source[5],
            FwVersionMinor = source[6],
            FwVersionPatch = source[7],
            ProtocolVersion = source[8],
            RuleFormatVersion = source[9]
        };
    }

    public static void EncodeDeviceDescriptor(DeviceDescriptorDto descriptor, Span<ushort> destination)
    {
        if (destination.Length < ModbusRegisterMap.DeviceDescriptorLength)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.DeviceDescriptorLength} registers.", nameof(destination));

        destination[0] = (ushort)descriptor.DeviceClass;
        destination[1] = descriptor.DeviceVariant;
        destination[2] = descriptor.HwVersionMajor;
        destination[3] = descriptor.HwVersionMinor;
        destination[4] = descriptor.HwVersionPatch;
        destination[5] = descriptor.FwVersionMajor;
        destination[6] = descriptor.FwVersionMinor;
        destination[7] = descriptor.FwVersionPatch;
        destination[8] = descriptor.ProtocolVersion;
        destination[9] = descriptor.RuleFormatVersion;
    }

    // ==========================================
    // 2.1 DEVICE RESOURCE INFO CODEC (10 REGISTERS - CONTRACT V1.9)
    // ==========================================

    public static DeviceResourceInfoDto DecodeDeviceResourceInfo(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.DeviceResourceInfoLength)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.DeviceResourceInfoLength} registers.", nameof(source));

        return new DeviceResourceInfoDto
        {
            WireProfile = source[0],
            MaxRules = source[1],
            RuntimeTagCount = source[2],
            DigitalInputCount = source[3],
            DigitalOutputCount = source[4],
            AnalogInputCount = source[5],
            VirtualFlagCount = source[6],
            VirtualRegisterCount = source[7],
            RetentiveRegisterCount = source[8],
            CounterCount = source[9]
        };
    }

    public static ushort[] EncodeDeviceResourceInfo(DeviceResourceInfoDto info)
    {
        var result = new ushort[ModbusRegisterMap.DeviceResourceInfoLength];
        EncodeDeviceResourceInfo(info, result);
        return result;
    }

    public static void EncodeDeviceResourceInfo(DeviceResourceInfoDto info, Span<ushort> destination)
    {
        if (destination.Length < ModbusRegisterMap.DeviceResourceInfoLength)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.DeviceResourceInfoLength} registers.", nameof(destination));

        destination[0] = info.WireProfile;
        destination[1] = info.MaxRules;
        destination[2] = info.RuntimeTagCount;
        destination[3] = info.DigitalInputCount;
        destination[4] = info.DigitalOutputCount;
        destination[5] = info.AnalogInputCount;
        destination[6] = info.VirtualFlagCount;
        destination[7] = info.VirtualRegisterCount;
        destination[8] = info.RetentiveRegisterCount;
        destination[9] = info.CounterCount;
    }

    // ==========================================
    // 3. DEVICE HEALTH CODEC (10 REGISTERS)
    // ==========================================

    public static DeviceHealthDto DecodeDeviceHealth(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.DeviceHealthLength)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.DeviceHealthLength} registers.", nameof(source));

        return new DeviceHealthDto
        {
            UptimeSeconds = DecodeUInt32(source.Slice(0, 2)),
            ResetReason = (SPLC_ResetReason)source[2],
            HealthFlags = (SPLC_HealthFlags)source[3],
            CpuLoadPercent = source[4],
            RamUsagePercent = source[5],
            ScanTimeMs = DecodeUInt32(source.Slice(6, 2)),
            MaxScanTimeMs = DecodeUInt32(source.Slice(8, 2))
        };
    }

    public static void EncodeDeviceHealth(DeviceHealthDto health, Span<ushort> destination)
    {
        if (destination.Length < ModbusRegisterMap.DeviceHealthLength)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.DeviceHealthLength} registers.", nameof(destination));

        EncodeUInt32(health.UptimeSeconds, destination.Slice(0, 2));
        destination[2] = (ushort)health.ResetReason;
        destination[3] = (ushort)health.HealthFlags;
        destination[4] = health.CpuLoadPercent;
        destination[5] = health.RamUsagePercent;
        EncodeUInt32(health.ScanTimeMs, destination.Slice(6, 2));
        EncodeUInt32(health.MaxScanTimeMs, destination.Slice(8, 2));
    }

    // ==========================================
    // 4. RUNTIME TAG CODEC (2 REGISTERS / INT32)
    // ==========================================

    public static int DecodeRuntimeTagValue(ReadOnlySpan<ushort> source)
    {
        return DecodeInt32(source);
    }

    public static void EncodeRuntimeTagValue(int value, Span<ushort> destination)
    {
        EncodeInt32(value, destination);
    }

    // ==========================================
    // 5. HELPER NGUYÊN THỦY (BIG-ENDIAN 32-BIT & PACKING)
    // ==========================================

    /// <summary>
    /// Đóng gói 2 byte vào 1 register: High Byte trước, Low Byte sau.
    /// Ví dụ: high=0x01, low=0x02 -> 0x0102.
    /// </summary>
    public static ushort PackBytes(byte highByte, byte lowByte)
    {
        return (ushort)((highByte << 8) | lowByte);
    }

    /// <summary>
    /// Mở gói 1 register thành 2 byte: High Byte và Low Byte.
    /// </summary>
    public static void UnpackBytes(ushort register, out byte highByte, out byte lowByte)
    {
        highByte = (byte)(register >> 8);
        lowByte = (byte)(register & 0xFF);
    }

    /// <summary>
    /// Mã hóa int32 thành 2 thanh ghi theo thứ tự High Word trước, Low Word sau.
    /// Ví dụ: 0x12345678 -> dest[0]=0x1234, dest[1]=0x5678.
    /// </summary>
    public static void EncodeInt32(int value, Span<ushort> destination)
    {
        uint u = (uint)value;
        destination[0] = (ushort)(u >> 16);
        destination[1] = (ushort)(u & 0xFFFF);
    }

    /// <summary>
    /// Giải mã 2 thanh ghi Modbus thành int32 theo thứ tự High Word trước, Low Word sau.
    /// </summary>
    public static int DecodeInt32(ReadOnlySpan<ushort> source)
    {
        uint u = ((uint)source[0] << 16) | source[1];
        return (int)u;
    }

    /// <summary>
    /// Mã hóa uint32 thành 2 thanh ghi theo thứ tự High Word trước, Low Word sau.
    /// </summary>
    public static void EncodeUInt32(uint value, Span<ushort> destination)
    {
        destination[0] = (ushort)(value >> 16);
        destination[1] = (ushort)(value & 0xFFFF);
    }

    /// <summary>
    /// Giải mã 2 thanh ghi Modbus thành uint32 theo thứ tự High Word trước, Low Word sau.
    /// </summary>
    public static uint DecodeUInt32(ReadOnlySpan<ushort> source)
    {
        return ((uint)source[0] << 16) | source[1];
    }

    // ==========================================
    // 7. RTC CLOCK CODEC (8 BYTES = 4 REGISTERS)
    // ==========================================

    /// <summary>
    /// Mã hóa RtcClockDto thành 4 thanh ghi Modbus vào Span đích.
    /// [0..1] = EpochUtcSeconds (uint32)
    /// [2]    = TimezoneOffsetMinutes (int16)
    /// [3]    = Status Flags (uint16)
    /// </summary>
    public static void EncodeRtcClock(RtcClockDto rtc, Span<ushort> destination)
    {
        if (destination.Length < ModbusRegisterMap.RtcClockLength)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.RtcClockLength} registers.", nameof(destination));

        EncodeUInt32(rtc.EpochUtcSeconds, destination.Slice(0, 2));
        destination[2] = (ushort)(short)rtc.TimezoneOffsetMinutes;

        ushort flags = 0;
        if (rtc.IsSynced) flags |= ModbusRegisterMap.RtcFlagSynced;
        if (rtc.HasHardwareRtc) flags |= ModbusRegisterMap.RtcFlagHwPresent;
        if (rtc.IsBatteryLow) flags |= ModbusRegisterMap.RtcFlagBatteryLow;
        destination[3] = flags;
    }

    /// <summary>
    /// Giải mã 4 thanh ghi Modbus thành RtcClockDto.
    /// </summary>
    public static RtcClockDto DecodeRtcClock(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.RtcClockLength)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.RtcClockLength} registers.", nameof(source));

        uint epoch = DecodeUInt32(source.Slice(0, 2));
        short tzOffset = (short)source[2];
        ushort flags = source[3];

        return new RtcClockDto
        {
            EpochUtcSeconds = epoch,
            TimezoneOffsetMinutes = tzOffset,
            IsSynced = (flags & ModbusRegisterMap.RtcFlagSynced) != 0,
            HasHardwareRtc = (flags & ModbusRegisterMap.RtcFlagHwPresent) != 0,
            IsBatteryLow = (flags & ModbusRegisterMap.RtcFlagBatteryLow) != 0
        };
    }
}
