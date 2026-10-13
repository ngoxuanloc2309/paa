using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Executable Specification Test Suite.
/// Đối chiếu trực tiếp và độc lập với tài liệu đặc tả nhị phân mới nhất của nền tảng:
/// Document ID: SPLC-DOC-SPEC-V20-STRUCTS (Platform V2.0 / V2.2.6)
/// "SimplePLC Platform V2.0 — App-to-MCU Binary Structs & Self-Describing Device Profile Specification"
/// </summary>
public class PlatformV2StructsSpecificationTests
{
    // =========================================================================
    // 1. SPEC SECTION 2 & 3: Byte-Exact Struct Capacities and Wire Sizes
    // =========================================================================

    [Fact]
    [Trait("Specification", "SPLC-DOC-SPEC-V20-STRUCTS-SEC-3")]
    public void SpecSection3_ByteExactStructSizes_MatchSpecificationRequirements()
    {
        // 3.1 DeviceDescriptor: 10 registers = 20 Bytes
        Assert.Equal(10, ModbusRegisterMap.DeviceDescriptorLength);

        // 3.2 DeviceResourceInfo: 10 registers = 20 Bytes
        Assert.Equal(10, ModbusRegisterMap.DeviceResourceInfoLength);

        // 3.3 DeviceHealth: 10 registers = 20 Bytes
        Assert.Equal(10, ModbusRegisterMap.DeviceHealthLength);

        // 3.4 RtcClock: 4 registers = 8 Bytes
        Assert.Equal(4, ModbusRegisterMap.RtcClockLength);

        // 3.5 DiagBlock: 5 registers = 10 Bytes
        Assert.Equal(5, ModbusRegisterMap.DiagBlockLength);

        // 3.6 FB Timers: 8 Timers * 8 registers = 64 registers (128 Bytes)
        Assert.Equal(64, ModbusRegisterMap.FbTimerTableLength);

        // 3.7 FB Counters: 8 Counters * 8 registers = 64 registers (128 Bytes)
        Assert.Equal(64, ModbusRegisterMap.FbCounterTableLength);

        // 3.8 RuleRecord: 16 registers = 32 Bytes per canonical rule
        Assert.Equal(16, ModbusRegisterMap.RegistersPerRule);
    }

    // =========================================================================
    // 2. SPEC SECTION 1 & 2: Platform Constants & Wire Invariants
    // =========================================================================

    [Fact]
    [Trait("Specification", "SPLC-DOC-SPEC-V20-STRUCTS-SEC-1")]
    public void SpecSection1_PlatformProtocolConstants_MatchSpecificationInvariants()
    {
        // Platform Wire Profile V1 and V2 Invariants
        Assert.Equal(1, ModbusRegisterMap.WireProfileV1);
        Assert.Equal(2, ModbusRegisterMap.WireProfileV2);

        // Atomic Commit Handshake Invariants
        Assert.Equal(0xA000, ModbusRegisterMap.CommitCommandAddress);
        Assert.Equal(0xA5A5, ModbusRegisterMap.CommitMagic); // SPLC_COMMIT_MAGIC

        // Rule Capacity
        Assert.Equal(100, ModbusRegisterMap.MaxRules);
    }

    // =========================================================================
    // 3. SPEC SECTION 3.4: Real-Time Clock (RTC) Status Bitmask & Read-Before-Write
    // =========================================================================

    [Fact]
    [Trait("Specification", "SPLC-DOC-SPEC-V20-STRUCTS-SEC-3.4")]
    public void SpecSection3_4_RtcStatusFlagsBitmask_MatchesSpecification()
    {
        // 0x0001: SPLC_RTC_FLAG_SYNCED
        // 0x0002: SPLC_RTC_FLAG_HW_PRESENT
        // 0x0004: SPLC_RTC_FLAG_BATTERY_LOW
        Assert.Equal(0x0001, ModbusRegisterMap.RtcFlagSynced);
        Assert.Equal(0x0002, ModbusRegisterMap.RtcFlagHwPresent);
        Assert.Equal(0x0004, ModbusRegisterMap.RtcFlagBatteryLow);

        var dto = new RtcClockDto
        {
            EpochUtcSeconds = 1728470400,
            TimezoneOffsetMinutes = 420, // UTC+7 Vietnam
            IsSynced = false,
            HasHardwareRtc = true,
            IsBatteryLow = true
        };

        // Ban đầu chưa sync
        Assert.True(dto.HasHardwareRtc);
        Assert.True(dto.IsBatteryLow);
        Assert.False(dto.IsSynced);

        // Giả lập quy tắc Read-Before-Write của Host Studio:
        // Host bảo toàn cờ phần cứng HW_PRESENT & BATTERY_LOW, chỉ bật thêm cờ SYNCED
        dto.IsSynced = true;

        Assert.True(dto.HasHardwareRtc);
        Assert.True(dto.IsBatteryLow);
        Assert.True(dto.IsSynced);
    }

    // =========================================================================
    // 4. SPEC SECTION 3.5: Diagnostic Control Subsystem & Watchdog Lease Invariants
    // =========================================================================

    [Fact]
    [Trait("Specification", "SPLC-DOC-SPEC-V20-STRUCTS-SEC-3.5")]
    public void SpecSection3_5_DiagnosticCommandsAndStates_MatchSpecification()
    {
        // SPLC_DiagCommand_t
        Assert.Equal(1, (ushort)SPLC_DiagCommand.ENTER_DIAG);
        Assert.Equal(2, (ushort)SPLC_DiagCommand.HEARTBEAT);
        Assert.Equal(3, (ushort)SPLC_DiagCommand.EXIT_DIAG);
        Assert.Equal(4, (ushort)SPLC_DiagCommand.COMMIT_RETAIN);
        Assert.Equal(5, (ushort)SPLC_DiagCommand.DISCARD_RETAIN);

        // SPLC_DiagState_t
        Assert.Equal(1, (ushort)SPLC_DiagState.ENGINE_RUNNING);
        Assert.Equal(2, (ushort)SPLC_DiagState.DIAG_CONTROL);
        Assert.Equal(3, (ushort)SPLC_DiagState.TRANSITIONING);
        Assert.Equal(4, (ushort)SPLC_DiagState.FAULT);

        // SPLC_DiagFlags_t
        Assert.Equal(0x0001, (ushort)SPLC_DiagFlags.RETAIN_DIRTY);
        Assert.Equal(0x0002, (ushort)SPLC_DiagFlags.LEASE_ACTIVE);

        // SPLC_DiagErrorCode_t
        Assert.Equal(0, (ushort)SPLC_DiagErrorCode.NONE);
        Assert.Equal(1, (ushort)SPLC_DiagErrorCode.DENIED_FAULT);
        Assert.Equal(2, (ushort)SPLC_DiagErrorCode.LEASE_EXPIRED);
        Assert.Equal(3, (ushort)SPLC_DiagErrorCode.FLASH_CRC_MISMATCH);
        Assert.Equal(4, (ushort)SPLC_DiagErrorCode.INVALID_COMMAND);
        Assert.Equal(5, (ushort)SPLC_DiagErrorCode.RETAIN_DIRTY);

        // Watchdog lease time
        Assert.Equal(3000, ModbusRegisterMap.DiagDefaultLeaseMs);
    }
}
