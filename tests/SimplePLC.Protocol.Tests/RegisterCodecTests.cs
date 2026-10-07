using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class RegisterCodecTests
{
    [Fact]
    public void EncodeRuleRecord_ValidDto_MatchesV17RegisterLayout()
    {
        // Arrange: Tạo RuleRecord theo ví dụ chuẩn spec v1.7
        var rule = new RuleRecordDto
        {
            ThresholdLo = 0x12345678,
            ThresholdHi = -500, // 0xFFFFFE0C
            ForMs = 15000,      // 0x00003A98
            ActionParam = 1,    // 0x00000001
            TriggerTag = 1,     // DI0
            ActionTag = 9,      // DO0
            GuardTag = 0x8021,  // bit 15=1 (NEGATE), index=33 (VFLAG0)
            Enabled = true,
            TriggerType = SPLC_TriggerType.ON_FALL,     // 2
            CompareOp = SPLC_CompareOp.BETWEEN,        // 7
            ActionType = SPLC_ActionType.SET_TAG       // 0
        };

        Span<ushort> registers = stackalloc ushort[ModbusRegisterMap.RegistersPerRule];

        // Act
        RegisterCodec.EncodeRuleRecord(rule, registers);

        // Assert theo đúng Offset trong Data Contract V1.7 bảng 8.3:
        // +0..+1: threshold_lo (High Word trước, Low Word sau)
        Assert.Equal(0x1234, registers[0]);
        Assert.Equal(0x5678, registers[1]);

        // +2..+3: threshold_hi (-500 = 0xFFFFFE0C)
        Assert.Equal(0xFFFF, registers[2]);
        Assert.Equal(0xFE0C, registers[3]);

        // +4..+5: for_ms (15000 = 0x00003A98)
        Assert.Equal(0x0000, registers[4]);
        Assert.Equal(0x3A98, registers[5]);

        // +6..+7: action_param (1 = 0x00000001)
        Assert.Equal(0x0000, registers[6]);
        Assert.Equal(0x0001, registers[7]);

        // +8: trigger_tag
        Assert.Equal(1, registers[8]);

        // +9: action_tag
        Assert.Equal(9, registers[9]);

        // +10: guard_tag (index 33 with negate bit 15)
        Assert.Equal(0x8021, registers[10]);

        // +11: enabled (0x01) / trigger_type (0x02) -> 0x0102
        Assert.Equal(0x0102, registers[11]);

        // +12: compare_op (0x07) / action_type (0x00) -> 0x0700
        Assert.Equal(0x0700, registers[12]);

        // +13..+15: reserved[6] (phải là 0x0000 theo R8)
        Assert.Equal(0x0000, registers[13]);
        Assert.Equal(0x0000, registers[14]);
        Assert.Equal(0x0000, registers[15]);
    }

    [Fact]
    public void EncodeRuleRecord_ReservedRegisters_AreAlwaysZero()
    {
        var rule = new RuleRecordDto();
        Span<ushort> registers = stackalloc ushort[16];
        // Đặt trước rác vào buffer để kiểm tra việc ghi đè 0x0000
        registers[13] = 0xDEAD;
        registers[14] = 0xBEEF;
        registers[15] = 0xCAFE;

        RegisterCodec.EncodeRuleRecord(rule, registers);

        Assert.Equal(0, registers[13]);
        Assert.Equal(0, registers[14]);
        Assert.Equal(0, registers[15]);
    }

    [Fact]
    public void RoundtripRuleRecord_AllFields_ArePreserved()
    {
        var original = new RuleRecordDto
        {
            ThresholdLo = -1234567,
            ThresholdHi = 9876543,
            ForMs = 3600000,
            ActionParam = -42,
            TriggerTag = 15,
            ActionTag = 28,
            GuardTag = (ushort)(45 | 0x8000), // tag 45, negated
            Enabled = true,
            TriggerType = SPLC_TriggerType.INTERVAL,
            CompareOp = SPLC_CompareOp.GTE,
            ActionType = SPLC_ActionType.INC_COUNTER
        };

        Span<ushort> registers = stackalloc ushort[16];
        RegisterCodec.EncodeRuleRecord(original, registers);
        var decoded = RegisterCodec.DecodeRuleRecord(registers);

        Assert.Equal(original.ThresholdLo, decoded.ThresholdLo);
        Assert.Equal(original.ThresholdHi, decoded.ThresholdHi);
        Assert.Equal(original.ForMs, decoded.ForMs);
        Assert.Equal(original.ActionParam, decoded.ActionParam);
        Assert.Equal(original.TriggerTag, decoded.TriggerTag);
        Assert.Equal(original.ActionTag, decoded.ActionTag);
        Assert.Equal(original.GuardTag, decoded.GuardTag);
        Assert.Equal(original.GuardTagIndex, decoded.GuardTagIndex);
        Assert.True(decoded.GuardNegated);
        Assert.Equal(original.Enabled, decoded.Enabled);
        Assert.Equal(original.TriggerType, decoded.TriggerType);
        Assert.Equal(original.CompareOp, decoded.CompareOp);
        Assert.Equal(original.ActionType, decoded.ActionType);
    }

    [Fact]
    public void RoundtripRuleRecords_MultipleRules_DecodesCorrectly()
    {
        var rules = new RuleRecordDto[]
        {
            new() { ThresholdLo = 10, ActionParam = 1, TriggerTag = 1, ActionTag = 2, Enabled = true },
            new() { ThresholdLo = 20, ActionParam = 2, TriggerTag = 3, ActionTag = 4, Enabled = false },
            new() { ThresholdLo = 30, ActionParam = 3, TriggerTag = 5, ActionTag = 6, Enabled = true }
        };

        Span<ushort> registers = stackalloc ushort[rules.Length * ModbusRegisterMap.RegistersPerRule];
        RegisterCodec.EncodeRuleRecords(rules, registers);

        var decoded = RegisterCodec.DecodeRuleRecords(registers, rules.Length);

        Assert.Equal(3, decoded.Length);
        Assert.Equal(10, decoded[0].ThresholdLo);
        Assert.Equal(20, decoded[1].ThresholdLo);
        Assert.Equal(30, decoded[2].ThresholdLo);
        Assert.True(decoded[0].Enabled);
        Assert.False(decoded[1].Enabled);
        Assert.True(decoded[2].Enabled);
    }

    [Fact]
    public void EncodeInt32_HighWordLowWord_MatchesSpec()
    {
        Span<ushort> dest = stackalloc ushort[2];
        RegisterCodec.EncodeInt32(0x12345678, dest);

        Assert.Equal(0x1234, dest[0]);
        Assert.Equal(0x5678, dest[1]);
        Assert.Equal(0x12345678, RegisterCodec.DecodeInt32(dest));
    }

    [Fact]
    public void PackBytes_HighByteLowByte_MatchesSpec()
    {
        ushort packed = RegisterCodec.PackBytes(0x12, 0x34);
        Assert.Equal(0x1234, packed);

        RegisterCodec.UnpackBytes(packed, out byte hi, out byte lo);
        Assert.Equal(0x12, hi);
        Assert.Equal(0x34, lo);
    }

    [Fact]
    public void RoundtripDeviceDescriptor_PreservesAllFields()
    {
        var desc = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = (ushort)SPLC_RemoteIoVariant.VARIANT_8DI_8DO_4AI,
            HwVersionMajor = 1,
            HwVersionMinor = 2,
            HwVersionPatch = 0,
            FwVersionMajor = 1,
            FwVersionMinor = 7,
            FwVersionPatch = 3,
            ProtocolVersion = 1,
            RuleFormatVersion = 1
        };

        Span<ushort> regs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceDescriptor(desc, regs);
        var decoded = RegisterCodec.DecodeDeviceDescriptor(regs);

        Assert.Equal(SPLC_DeviceClass.REMOTE_IO, decoded.DeviceClass);
        Assert.Equal(desc.DeviceVariant, decoded.DeviceVariant);
        Assert.Equal("1.2.0", decoded.HwVersionString);
        Assert.Equal("1.7.3", decoded.FwVersionString);
        Assert.Equal(1, decoded.ProtocolVersion);
        Assert.Equal(1, decoded.RuleFormatVersion);
    }

    [Fact]
    public void RoundtripDeviceHealth_PreservesAllFields()
    {
        var health = new DeviceHealthDto
        {
            UptimeSeconds = 86400,
            ResetReason = SPLC_ResetReason.POWER_ON,
            HealthFlags = SPLC_HealthFlags.CPU_HIGH | SPLC_HealthFlags.SCAN_OVERRUN,
            CpuLoadPercent = 85,
            RamUsagePercent = 42,
            ScanTimeMs = 10,
            MaxScanTimeMs = 15
        };

        Span<ushort> regs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceHealth(health, regs);
        var decoded = RegisterCodec.DecodeDeviceHealth(regs);

        Assert.Equal(86400u, decoded.UptimeSeconds);
        Assert.Equal(SPLC_ResetReason.POWER_ON, decoded.ResetReason);
        Assert.Equal(SPLC_HealthFlags.CPU_HIGH | SPLC_HealthFlags.SCAN_OVERRUN, decoded.HealthFlags);
        Assert.Equal(85, decoded.CpuLoadPercent);
        Assert.Equal(42, decoded.RamUsagePercent);
        Assert.Equal(10u, decoded.ScanTimeMs);
        Assert.Equal(15u, decoded.MaxScanTimeMs);
    }

    [Fact]
    public void EncodeRuleRecord_DestinationTooSmall_ThrowsArgumentException()
    {
        var rule = new RuleRecordDto();
        ushort[] tooSmall = new ushort[15];

        Assert.Throws<ArgumentException>(() => RegisterCodec.EncodeRuleRecord(rule, tooSmall));
    }

    [Fact]
    public void DecodeRuleRecord_SourceTooSmall_ThrowsArgumentException()
    {
        ushort[] tooSmall = new ushort[15];

        Assert.Throws<ArgumentException>(() => RegisterCodec.DecodeRuleRecord(tooSmall));
    }
}
