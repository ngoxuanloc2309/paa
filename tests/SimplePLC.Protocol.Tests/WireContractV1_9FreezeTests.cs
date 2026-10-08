using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using SimplePLC.Protocol.Models;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Phase E1: Wire Contract V1.9 Freeze Tests.
/// Khóa cứng các bất biến wire-level bằng literal constants tường minh.
/// Ngăn chặn mọi sự dịch chuyển địa chỉ thanh ghi, kích thước struct hoặc base indices.
/// </summary>
public class WireContractV1_9FreezeTests
{
    [Fact]
    public void ModbusRegisterMap_Addresses_MatchExactV1_9Literals()
    {
        // Core & Runtime Space (0x0000 - 0x0A02)
        Assert.Equal(0x0000, ModbusRegisterMap.DeviceDescriptorAddress);
        Assert.Equal(0x0010, ModbusRegisterMap.RuleTableInfoAddress);
        Assert.Equal(0x0020, ModbusRegisterMap.DeviceResourceInfoAddress);
        Assert.Equal(0x0100, ModbusRegisterMap.ActiveRuleTableBaseAddress);
        Assert.Equal(0x0800, ModbusRegisterMap.DeviceHealthAddress);
        Assert.Equal(0x0900, ModbusRegisterMap.RuntimeTagValuesBaseAddress);
        Assert.Equal(0x0A00, ModbusRegisterMap.SystemCommandAddress);
        Assert.Equal(0x0A01, ModbusRegisterMap.SystemCommandResultAddress);

        // Staging & Commit Space (0x9000 - 0xA001)
        Assert.Equal(0x9000, ModbusRegisterMap.ConfigStatusAddress);
        Assert.Equal(0x9001, ModbusRegisterMap.ConfigErrorCodeAddress);
        Assert.Equal(0x9002, ModbusRegisterMap.RuleCountStagedAddress);
        Assert.Equal(0x9003, ModbusRegisterMap.ExpectedCrc16Address);
        Assert.Equal(0x9004, ModbusRegisterMap.ActiveRuleCountAddress);
        Assert.Equal(0x9005, ModbusRegisterMap.ActiveRuleCrc16Address);
        Assert.Equal(0x9006, ModbusRegisterMap.ReservedAddress);
        Assert.Equal(0x9010, ModbusRegisterMap.StagingRuleTableBaseAddress);
        Assert.Equal(0xA000, ModbusRegisterMap.CommitCommandAddress);
        Assert.Equal(0xA001, ModbusRegisterMap.ActiveRuleVersionAddress);
    }

    [Fact]
    public void ModbusRegisterMap_LengthsAndCapacities_MatchExactV1_9Literals()
    {
        // Block lengths in 16-bit registers
        Assert.Equal(10, ModbusRegisterMap.DeviceDescriptorLength);
        Assert.Equal(1, ModbusRegisterMap.RuleTableInfoLength);
        Assert.Equal(10, ModbusRegisterMap.DeviceResourceInfoLength);
        Assert.Equal(10, ModbusRegisterMap.DeviceHealthLength);
        Assert.Equal(1, ModbusRegisterMap.SystemCommandLength);
        Assert.Equal(2, ModbusRegisterMap.SystemCommandResultLength);

        // Staging handshake lengths
        Assert.Equal(1, ModbusRegisterMap.ConfigStatusLength);
        Assert.Equal(1, ModbusRegisterMap.ConfigErrorCodeLength);
        Assert.Equal(1, ModbusRegisterMap.RuleCountStagedLength);
        Assert.Equal(1, ModbusRegisterMap.ExpectedCrc16Length);
        Assert.Equal(1, ModbusRegisterMap.ActiveRuleCountLength);
        Assert.Equal(1, ModbusRegisterMap.ActiveRuleCrc16Length);
        Assert.Equal(10, ModbusRegisterMap.ReservedLength);
        Assert.Equal(1, ModbusRegisterMap.CommitCommandLength);
        Assert.Equal(1, ModbusRegisterMap.ActiveRuleVersionLength);

        // Rules and Tags sizing
        Assert.Equal(16, ModbusRegisterMap.RegistersPerRule);
        Assert.Equal(32, ModbusRegisterMap.BytesPerRule);
        Assert.Equal(100, ModbusRegisterMap.MaxRules);
        Assert.Equal(1600, ModbusRegisterMap.ActiveRuleTableMaxLength);
        Assert.Equal(1600, ModbusRegisterMap.StagingRuleTableMaxLength);

        Assert.Equal(128, ModbusRegisterMap.MaxRuntimeTags);
        Assert.Equal(2, ModbusRegisterMap.RegistersPerRuntimeTag);
        Assert.Equal(256, ModbusRegisterMap.RuntimeTagValuesMaxLength);
    }

    [Fact]
    public void WireProfileV1_BaseIndicesAndLimits_MatchExactLiterals()
    {
        Assert.Equal(1, ModbusRegisterMap.WireProfileV1);

        // Resource maximum capacity in Wire Profile V1
        Assert.Equal(8, ModbusRegisterMap.MaxDigitalInputs);
        Assert.Equal(8, ModbusRegisterMap.MaxDigitalOutputs);
        Assert.Equal(4, ModbusRegisterMap.MaxAnalogInputs);
        Assert.Equal(32, ModbusRegisterMap.MaxVirtualFlags);
        Assert.Equal(32, ModbusRegisterMap.MaxVirtualRegisters);
        Assert.Equal(32, ModbusRegisterMap.MaxRetentiveRegisters);
        Assert.Equal(8, ModbusRegisterMap.MaxCounters);
    }

    [Fact]
    public void TagLayoutMap_Default_ProducesCorrectDynamicLayout()
    {
        var layout = TagLayoutMap.Default;
        Assert.Equal(0, layout.DiBase);
        Assert.Equal(8, layout.DoBase);   // 8 DI → DoBase=8
        Assert.Equal(16, layout.AiBase);  // 8+8=16
        Assert.Equal(20, layout.VflagBase); // 16+4=20
        Assert.Equal(52, layout.VregBase);  // 20+32=52
        Assert.Equal(84, layout.VregRetainBase); // 52+32=84
        Assert.Equal(116, layout.CounterBase); // 84+32=116
    }

    [Fact]
    public void TagLayoutMap_4Di4Do2Ai_ProducesCompactLayout()
    {
        var layout = new TagLayoutMap(4, 4, 2, 32, 32, 32, 8);
        Assert.Equal(0, layout.DiBase);
        Assert.Equal(4, layout.DoBase);
        Assert.Equal(8, layout.AiBase);
        Assert.Equal(10, layout.VflagBase);
        Assert.Equal(42, layout.VregBase);
        Assert.Equal(74, layout.VregRetainBase);
        Assert.Equal(106, layout.CounterBase);
    }

    [Fact]
    public void ProtocolConstants_MagicAndBitmasks_MatchExactLiterals()
    {
        Assert.Equal(0xA5A5, ModbusRegisterMap.CommitMagic);
        Assert.Equal(0x8000, ModbusRegisterMap.GuardTagNegateMask);
        Assert.Equal(0x7FFF, ModbusRegisterMap.GuardTagIndexMask);
        Assert.Equal(0x7FFF, ModbusRegisterMap.GuardTagNone);
    }

    [Fact]
    public void RegisterAddressCalculation_Helpers_ComputeExactWireOffsets()
    {
        // Active rules
        Assert.Equal(0x0100, ModbusRegisterMap.GetActiveRuleAddress(0));
        Assert.Equal(0x0110, ModbusRegisterMap.GetActiveRuleAddress(1));
        Assert.Equal(0x0730, ModbusRegisterMap.GetActiveRuleAddress(99));

        // Staging rules
        Assert.Equal(0x9010, ModbusRegisterMap.GetStagingRuleAddress(0));
        Assert.Equal(0x9020, ModbusRegisterMap.GetStagingRuleAddress(1));
        Assert.Equal(0x9640, ModbusRegisterMap.GetStagingRuleAddress(99));

        // Runtime tags: 0x0900 + tagIndex * 2
        Assert.Equal(0x0900, ModbusRegisterMap.GetRuntimeTagAddress(0));
        Assert.Equal(0x0910, ModbusRegisterMap.GetRuntimeTagAddress(8));
        Assert.Equal(0x0920, ModbusRegisterMap.GetRuntimeTagAddress(16));
        Assert.Equal(0x0928, ModbusRegisterMap.GetRuntimeTagAddress(20));
        Assert.Equal(0x0968, ModbusRegisterMap.GetRuntimeTagAddress(52));
        Assert.Equal(0x09A8, ModbusRegisterMap.GetRuntimeTagAddress(84));
        Assert.Equal(0x09E8, ModbusRegisterMap.GetRuntimeTagAddress(116));
        Assert.Equal(0x09F8, ModbusRegisterMap.GetRuntimeTagAddress(124));
        Assert.Equal(0x09FE, ModbusRegisterMap.GetRuntimeTagAddress(127));
    }

    [Fact]
    public void DeviceDescriptor_RegisterFieldOrder_IsFrozen()
    {
        var desc = new DeviceDescriptorDto
        {
            DeviceClass = SPLC_DeviceClass.REMOTE_IO,
            DeviceVariant = 1,
            HwVersionMajor = 1,
            HwVersionMinor = 2,
            HwVersionPatch = 3,
            FwVersionMajor = 1,
            FwVersionMinor = 9,
            FwVersionPatch = 0,
            ProtocolVersion = 1,
            RuleFormatVersion = 7
        };

        Span<ushort> regs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceDescriptor(desc, regs);

        Assert.Equal(1, regs[0]); // DeviceClass
        Assert.Equal(1, regs[1]); // DeviceVariant
        Assert.Equal(1, regs[2]); // HwVersionMajor
        Assert.Equal(2, regs[3]); // HwVersionMinor
        Assert.Equal(3, regs[4]); // HwVersionPatch
        Assert.Equal(1, regs[5]); // FwVersionMajor
        Assert.Equal(9, regs[6]); // FwVersionMinor
        Assert.Equal(0, regs[7]); // FwVersionPatch
        Assert.Equal(1, regs[8]); // ProtocolVersion
        Assert.Equal(7, regs[9]); // RuleFormatVersion
    }

    [Fact]
    public void DeviceResourceInfo_RegisterFieldOrder_IsFrozen()
    {
        var res = new DeviceResourceInfoDto
        {
            WireProfile = 1,
            MaxRules = 100,
            RuntimeTagCount = 124,
            DigitalInputCount = 8,
            DigitalOutputCount = 8,
            AnalogInputCount = 4,
            VirtualFlagCount = 32,
            VirtualRegisterCount = 32,
            RetentiveRegisterCount = 32,
            CounterCount = 8
        };

        Span<ushort> regs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceResourceInfo(res, regs);

        Assert.Equal(1, regs[0]);   // wire_profile
        Assert.Equal(100, regs[1]); // max_rules
        Assert.Equal(124, regs[2]); // runtime_tag_count
        Assert.Equal(8, regs[3]);   // di_count
        Assert.Equal(8, regs[4]);   // do_count
        Assert.Equal(4, regs[5]);   // ai_count
        Assert.Equal(32, regs[6]);  // vflag_count
        Assert.Equal(32, regs[7]);  // vreg_count
        Assert.Equal(32, regs[8]);  // vreg_retain_count
        Assert.Equal(8, regs[9]);   // counter_count
    }

    [Fact]
    public void DeviceHealth_RegisterFieldOrder_IsFrozen()
    {
        var health = new DeviceHealthDto
        {
            UptimeSeconds = 0x12345678,
            ResetReason = SPLC_ResetReason.WATCHDOG, // 3
            HealthFlags = SPLC_HealthFlags.CPU_HIGH | SPLC_HealthFlags.RAM_HIGH, // 1 | 2 = 3
            CpuLoadPercent = 45,
            RamUsagePercent = 60,
            ScanTimeMs = 10,
            MaxScanTimeMs = 15
        };

        Span<ushort> regs = stackalloc ushort[10];
        RegisterCodec.EncodeDeviceHealth(health, regs);

        Assert.Equal(0x1234, regs[0]); // UptimeSeconds High Word
        Assert.Equal(0x5678, regs[1]); // UptimeSeconds Low Word
        Assert.Equal(3, regs[2]);      // ResetReason
        Assert.Equal(3, regs[3]);      // HealthFlags
        Assert.Equal(45, regs[4]);     // CpuLoadPercent
        Assert.Equal(60, regs[5]);     // RamUsagePercent
        Assert.Equal(0, regs[6]);       // ScanTimeMs High Word
        Assert.Equal(10, regs[7]);     // ScanTimeMs Low Word
        Assert.Equal(0, regs[8]);       // MaxScanTimeMs High Word
        Assert.Equal(15, regs[9]);     // MaxScanTimeMs Low Word
    }

    [Fact]
    public void RuleRecord_RegisterFieldOrderAndPacking_IsFrozen()
    {
        var rule = new RuleRecordDto
        {
            ThresholdLo = -10,    // 0xFFFFFFF6 -> High: 0xFFFF, Low: 0xFFF6
            ThresholdHi = 200,    // 0x000000C8 -> High: 0x0000, Low: 0x00C8
            ForMs = 500,          // 0x000001F4 -> High: 0x0000, Low: 0x01F4
            ActionParam = 99,     // 0x00000063 -> High: 0x0000, Low: 0x0063
            TriggerTag = 16,      // AI0
            ActionTag = 8,        // DO0
            GuardTag = 0x8014,    // Negated (bit 15=1) + VFLAG0 (index=20)
            Enabled = true,       // 1
            TriggerType = SPLC_TriggerType.INTERVAL,  // 4
            CompareOp = SPLC_CompareOp.BETWEEN,      // 7
            ActionType = SPLC_ActionType.SCALE_TAG   // 7
        };

        Span<ushort> regs = stackalloc ushort[16];
        RegisterCodec.EncodeRuleRecord(rule, regs);

        // Offset 0..1: ThresholdLo
        Assert.Equal(0xFFFF, regs[0]);
        Assert.Equal(0xFFF6, regs[1]);

        // Offset 2..3: ThresholdHi
        Assert.Equal(0x0000, regs[2]);
        Assert.Equal(0x00C8, regs[3]);

        // Offset 4..5: ForMs
        Assert.Equal(0x0000, regs[4]);
        Assert.Equal(0x01F4, regs[5]);

        // Offset 6..7: ActionParam
        Assert.Equal(0x0000, regs[6]);
        Assert.Equal(0x0063, regs[7]);

        // Offset 8: TriggerTag
        Assert.Equal(16, regs[8]);

        // Offset 9: ActionTag
        Assert.Equal(8, regs[9]);

        // Offset 10: GuardTag
        Assert.Equal(0x8014, regs[10]);

        // Offset 11: PackBytes(Enabled=1, TriggerType=4) -> 0x0104
        Assert.Equal(0x0104, regs[11]);

        // Offset 12: PackBytes(CompareOp=7, ActionType=7) -> 0x0707
        Assert.Equal(0x0707, regs[12]);

        // Offset 13..15: Reserved (always 0x0000)
        Assert.Equal(0x0000, regs[13]);
        Assert.Equal(0x0000, regs[14]);
        Assert.Equal(0x0000, regs[15]);
    }
}
