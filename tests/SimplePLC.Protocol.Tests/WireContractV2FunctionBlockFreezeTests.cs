using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Phase 1: Wire Contract V2.0 Function Block Subsystem Freeze Tests.
/// Khóa cứng các bất biến wire-level của phân vùng Function Block (0x0B00..0x0B7F) bằng literal constants tường minh.
/// Ngăn chặn mọi sự dịch chuyển địa chỉ, sai lệch căn gióng 8 thanh ghi, hoặc đảo lộn Big-Endian.
/// </summary>
public class WireContractV2FunctionBlockFreezeTests
{
    [Fact]
    public void ModbusRegisterMap_FbAddressesAndLengths_MatchExactV2Literals()
    {
        // Base addresses
        Assert.Equal(0x0B00, ModbusRegisterMap.FbTimerTableBaseAddress);
        Assert.Equal(0x0B40, ModbusRegisterMap.FbCounterTableBaseAddress);

        // Capacities & Sizing
        Assert.Equal(8, ModbusRegisterMap.FbRegistersPerBlock);
        Assert.Equal(16, ModbusRegisterMap.FbBytesPerBlock);
        Assert.Equal(8, ModbusRegisterMap.FbMaxTimers);
        Assert.Equal(8, ModbusRegisterMap.FbMaxCounters);

        // Table lengths in 16-bit registers
        Assert.Equal(64, ModbusRegisterMap.FbTimerTableLength);   // 8 * 8
        Assert.Equal(64, ModbusRegisterMap.FbCounterTableLength); // 8 * 8
        Assert.Equal(128, ModbusRegisterMap.FbTableTotalLength);  // 64 + 64
    }

    [Fact]
    public void ModbusRegisterMap_FbStatusBitsAndBitmasks_MatchExactLiterals()
    {
        // Timer Status Bitmasks
        Assert.Equal(0x0001, ModbusRegisterMap.FbTimerStatusBitIn);
        Assert.Equal(0x0002, ModbusRegisterMap.FbTimerStatusBitQ);
        Assert.Equal(0x0004, ModbusRegisterMap.FbTimerStatusBitReset);
        Assert.Equal(0x0008, ModbusRegisterMap.FbTimerStatusBitRunning);

        // Counter Status Bitmasks
        Assert.Equal(0x0001, ModbusRegisterMap.FbCounterStatusBitCu);
        Assert.Equal(0x0002, ModbusRegisterMap.FbCounterStatusBitCd);
        Assert.Equal(0x0004, ModbusRegisterMap.FbCounterStatusBitReset);
        Assert.Equal(0x0008, ModbusRegisterMap.FbCounterStatusBitQ);

        // Retain None Sentinel
        Assert.Equal(0xFFFF, ModbusRegisterMap.FbCounterRetainNone);
    }

    [Fact]
    public void ModbusRegisterMap_FbAddressCalculation_ComputesPowerOfTwoShift()
    {
        // Timer Base + (i << 3)
        Assert.Equal(0x0B00, ModbusRegisterMap.GetFbTimerAddress(0));
        Assert.Equal(0x0B08, ModbusRegisterMap.GetFbTimerAddress(1));
        Assert.Equal(0x0B10, ModbusRegisterMap.GetFbTimerAddress(2));
        Assert.Equal(0x0B38, ModbusRegisterMap.GetFbTimerAddress(7));

        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusRegisterMap.GetFbTimerAddress(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusRegisterMap.GetFbTimerAddress(8));

        // Counter Base + (i << 3)
        Assert.Equal(0x0B40, ModbusRegisterMap.GetFbCounterAddress(0));
        Assert.Equal(0x0B48, ModbusRegisterMap.GetFbCounterAddress(1));
        Assert.Equal(0x0B50, ModbusRegisterMap.GetFbCounterAddress(2));
        Assert.Equal(0x0B78, ModbusRegisterMap.GetFbCounterAddress(7));

        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusRegisterMap.GetFbCounterAddress(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusRegisterMap.GetFbCounterAddress(8));
    }

    [Fact]
    public void FunctionBlockCodec_Timer_RegisterFieldOrderAndPacking_IsFrozen()
    {
        var timer = new FbTimerRecordDto
        {
            StatusBits = (ushort)(ModbusRegisterMap.FbTimerStatusBitIn | ModbusRegisterMap.FbTimerStatusBitRunning), // 0x0009
            Mode = SPLC_TimerMode.TON, // 1
            PresetMs = 5000,           // 0x00001388 -> High: 0x0000, Low: 0x1388
            ElapsedMs = 2500           // 0x000009C4 -> High: 0x0000, Low: 0x09C4
        };

        Span<ushort> regs = stackalloc ushort[8];
        FunctionBlockCodec.EncodeTimer(timer, regs);

        // Offset 0: STATUS_BITS
        Assert.Equal(0x0009, regs[0]);

        // Offset 1: MODE
        Assert.Equal(1, regs[1]);

        // Offset 2..3: PT
        Assert.Equal(0x0000, regs[2]);
        Assert.Equal(0x1388, regs[3]);

        // Offset 4..5: ET
        Assert.Equal(0x0000, regs[4]);
        Assert.Equal(0x09C4, regs[5]);

        // Offset 6..7: RESERVED
        Assert.Equal(0x0000, regs[6]);
        Assert.Equal(0x0000, regs[7]);

        // Decode roundtrip
        var decoded = FunctionBlockCodec.DecodeTimer(regs);
        Assert.True(decoded.In);
        Assert.False(decoded.Q);
        Assert.False(decoded.Reset);
        Assert.True(decoded.Running);
        Assert.Equal(SPLC_TimerMode.TON, decoded.Mode);
        Assert.Equal(5000u, decoded.PresetMs);
        Assert.Equal(2500u, decoded.ElapsedMs);
    }

    [Fact]
    public void FunctionBlockCodec_Counter_RegisterFieldOrderAndPacking_IsFrozen()
    {
        var counter = new FbCounterRecordDto
        {
            StatusBits = (ushort)(ModbusRegisterMap.FbCounterStatusBitCu | ModbusRegisterMap.FbCounterStatusBitQ), // 0x0009
            Mode = SPLC_CounterMode.CTU, // 1
            PresetValue = 100,           // 0x00000064 -> High: 0x0000, Low: 0x0064
            CurrentValue = 105,          // 0x00000069 -> High: 0x0000, Low: 0x0069
            RetainTagIndex = 84          // VREG_RETAIN0
        };

        Span<ushort> regs = stackalloc ushort[8];
        FunctionBlockCodec.EncodeCounter(counter, regs);

        // Offset 0: STATUS_BITS
        Assert.Equal(0x0009, regs[0]);

        // Offset 1: MODE
        Assert.Equal(1, regs[1]);

        // Offset 2..3: PV
        Assert.Equal(0x0000, regs[2]);
        Assert.Equal(0x0064, regs[3]);

        // Offset 4..5: CV
        Assert.Equal(0x0000, regs[4]);
        Assert.Equal(0x0069, regs[5]);

        // Offset 6: RETAIN_TAG_INDEX
        Assert.Equal(84, regs[6]);

        // Offset 7: RESERVED
        Assert.Equal(0x0000, regs[7]);

        // Decode roundtrip
        var decoded = FunctionBlockCodec.DecodeCounter(regs);
        Assert.True(decoded.Cu);
        Assert.False(decoded.Cd);
        Assert.False(decoded.Reset);
        Assert.True(decoded.Q);
        Assert.Equal(SPLC_CounterMode.CTU, decoded.Mode);
        Assert.Equal(100, decoded.PresetValue);
        Assert.Equal(105, decoded.CurrentValue);
        Assert.Equal(84, decoded.RetainTagIndex);
    }
}
