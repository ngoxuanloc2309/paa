using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Models;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Executable Specification Test Suite.
/// Xác thực độc lập các bất biến và công thức được quy định trong tài liệu đặc tả:
/// docs/platform/SimplePLC_Modbus_Register_Map_V1.md (Cập nhật V2.1/V2.2.6).
/// </summary>
public class ModbusRegisterMapSpecificationTests
{
    // =========================================================================
    // 1. SPEC SECTION 2: Core & Runtime Address Space Boundaries
    // =========================================================================

    [Fact]
    [Trait("Specification", "SPLC-MODBUS-MAP-SEC-2")]
    public void SpecSection2_CoreRegisterAddressBoundaries_MatchConstants()
    {
        // 0x0000..0x0009: Device Descriptor (Length 10)
        Assert.Equal(0x0000, ModbusRegisterMap.DeviceDescriptorAddress);
        Assert.Equal(10, ModbusRegisterMap.DeviceDescriptorLength);

        // 0x0010: Rule Table Info (Length 1)
        Assert.Equal(0x0010, ModbusRegisterMap.RuleTableInfoAddress);
        Assert.Equal(1, ModbusRegisterMap.RuleTableInfoLength);

        // 0x0020..0x0029: Device Resource Info (Length 10)
        Assert.Equal(0x0020, ModbusRegisterMap.DeviceResourceInfoAddress);
        Assert.Equal(10, ModbusRegisterMap.DeviceResourceInfoLength);

        // 0x0100..0x073F: Active Rule Table (100 rules * 16 registers = 1600)
        Assert.Equal(0x0100, ModbusRegisterMap.ActiveRuleTableBaseAddress);
        Assert.Equal(1600, ModbusRegisterMap.ActiveRuleTableMaxLength);

        // 0x0800..0x0809: Device Health (Length 10)
        Assert.Equal(0x0800, ModbusRegisterMap.DeviceHealthAddress);
        Assert.Equal(10, ModbusRegisterMap.DeviceHealthLength);

        // 0x0810..0x0813: RTC Clock (Length 4)
        Assert.Equal(0x0810, ModbusRegisterMap.RtcClockAddress);
        Assert.Equal(4, ModbusRegisterMap.RtcClockLength);

        // 0x0900..0x09FF: Runtime Tag Values (128 tags * 2 registers = 256)
        Assert.Equal(0x0900, ModbusRegisterMap.RuntimeTagValuesBaseAddress);
        Assert.Equal(256, ModbusRegisterMap.RuntimeTagValuesMaxLength);

        // 0x0A00: System Command
        Assert.Equal(0x0A00, ModbusRegisterMap.SystemCommandAddress);

        // 0x0A20..0x0A24: Diagnostic Control Block (Length 5)
        Assert.Equal(0x0A20, ModbusRegisterMap.DiagBlockBaseAddress);
        Assert.Equal(5, ModbusRegisterMap.DiagBlockLength);
    }

    // =========================================================================
    // 2. SPEC SECTION 3: Staging & Commit Address Space
    // =========================================================================

    [Fact]
    [Trait("Specification", "SPLC-MODBUS-MAP-SEC-3")]
    public void SpecSection3_StagingAndCommitSpace_MatchesConstants()
    {
        // 0x9000: Config Status
        Assert.Equal(0x9000, ModbusRegisterMap.ConfigStatusAddress);

        // 0x9001: Config Error Code
        Assert.Equal(0x9001, ModbusRegisterMap.ConfigErrorCodeAddress);

        // 0x9002: Rule Count Staged
        Assert.Equal(0x9002, ModbusRegisterMap.RuleCountStagedAddress);

        // 0x9003: Expected CRC16
        Assert.Equal(0x9003, ModbusRegisterMap.ExpectedCrc16Address);

        // 0x9010..0x964F: Staging Rule Buffer (Length 1600)
        Assert.Equal(0x9010, ModbusRegisterMap.StagingRuleTableBaseAddress);
        Assert.Equal(1600, ModbusRegisterMap.StagingRuleTableMaxLength);

        // 0xA000: Commit Command Word (Magic 0xA5A5)
        Assert.Equal(0xA000, ModbusRegisterMap.CommitCommandAddress);
        Assert.Equal(0xA5A5, ModbusRegisterMap.CommitMagic);

        // 0xA001: Active Rule Version Monotonic Counter
        Assert.Equal(0xA001, ModbusRegisterMap.ActiveRuleVersionAddress);
    }

    // =========================================================================
    // 3. SPEC SECTION 4.1: Runtime Tag Modbus Address Formula
    // ModbusAddress = 0x0900 + TagIndex * 2
    // =========================================================================

    [Theory]
    [InlineData(0, 0x0900)]    // Tag 0 (DI0)
    [InlineData(8, 0x0910)]    // Tag 8 (DO0 Standard)
    [InlineData(16, 0x0920)]   // Tag 16 (AI0 Standard)
    [InlineData(127, 0x09FE)]  // Tag 127 (Max Wire Tag)
    [Trait("Specification", "SPLC-MODBUS-MAP-SEC-4.1")]
    public void SpecSection4_1_ModbusTagAddressFormula_MatchesExactCalculations(ushort tagIndex, ushort expectedAddress)
    {
        // Formula specified: 0x0900 + TagIndex * 2
        ushort computedAddress = ModbusRegisterMap.GetRuntimeTagAddress(tagIndex);
        Assert.Equal(expectedAddress, computedAddress);
    }

    // =========================================================================
    // 4. SPEC SECTION 4.2: Dynamic Tag Index Layout (TagLayoutMap)
    // Đối chiếu chính xác theo bảng Example 1 và Example 2 trong tài liệu
    // =========================================================================

    [Fact]
    [Trait("Specification", "SPLC-MODBUS-MAP-SEC-4.2-EXAMPLE-1")]
    public void SpecSection4_2_Example1_FullStandardBoard_MatchesSpecificationTable()
    {
        // Specification Example 1: Full Standard Board
        // 8 DI, 8 DO, 4 AI, 32 VFLAG, 32 VREG, 32 VREG_R, 8 COUNTER
        var layout = new TagLayoutMap(
            diCount: 8,
            doCount: 8,
            aiCount: 4,
            vflagCount: 32,
            vregCount: 32,
            vregRetainCount: 32,
            counterCount: 8);

        // Đối chiếu từng Base Index quy định trong tài liệu
        Assert.Equal(0, layout.DiBase);             // DI0..DI7 -> 0..7
        Assert.Equal(8, layout.DoBase);             // DO0..DO7 -> 8..15
        Assert.Equal(16, layout.AiBase);            // AI0..AI3 -> 16..19
        Assert.Equal(20, layout.VflagBase);         // VFLAG0..31 -> 20..51
        Assert.Equal(52, layout.VregBase);          // VREG0..31 -> 52..83
        Assert.Equal(84, layout.VregRetainBase);    // VREG_RETAIN0..31 -> 84..115
        Assert.Equal(116, layout.CounterBase);      // COUNTER0..7 -> 116..123
        Assert.Equal(124, layout.TotalTags);        // Tổng số tags = 124
    }

    [Fact]
    [Trait("Specification", "SPLC-MODBUS-MAP-SEC-4.2-EXAMPLE-2")]
    public void SpecSection4_2_Example2_CompactControllerBoard_MatchesSpecificationTable()
    {
        // Specification Example 2: Compact Controller Board
        // 4 DI, 4 DO, 2 AI, 32 VFLAG, 32 VREG, 32 VREG_R, 8 COUNTER
        var layout = new TagLayoutMap(
            diCount: 4,
            doCount: 4,
            aiCount: 2,
            vflagCount: 32,
            vregCount: 32,
            vregRetainCount: 32,
            counterCount: 8);

        // Đối chiếu từng Base Index quy định trong tài liệu: DO0 nối tiếp DI không khoảng trống
        Assert.Equal(0, layout.DiBase);             // DI0..DI3 -> 0..3
        Assert.Equal(4, layout.DoBase);             // DO0..DO3 -> 4..7 (DO0 mang index = 4)
        Assert.Equal(8, layout.AiBase);             // AI0..AI1 -> 8..9 (AI0 mang index = 8)
        Assert.Equal(10, layout.VflagBase);         // VFLAG0..31 -> 10..41
        Assert.Equal(42, layout.VregBase);          // VREG0..31 -> 42..73
        Assert.Equal(74, layout.VregRetainBase);    // VREG_RETAIN0..31 -> 74..105
        Assert.Equal(106, layout.CounterBase);      // COUNTER0..7 -> 106..113
        Assert.Equal(114, layout.TotalTags);        // Tổng số tags = 114
    }
}
