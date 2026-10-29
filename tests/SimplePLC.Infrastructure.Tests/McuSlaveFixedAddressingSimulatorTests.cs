namespace SimplePLC.Infrastructure.Tests;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SimplePLC.Infrastructure.Simulator;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

/// <summary>
/// Bộ kiểm thử toàn diện cơ chế Modbus Slave theo chuẩn địa chỉ cố định (Fixed Addressing)
/// tuân thủ mô hình IEC 61508-3 Triad (Happy Path, Boundary, Fault Injection).
/// </summary>
public sealed class McuSlaveFixedAddressingSimulatorTests
{
    private static McuReferenceSimulator CreateSimulatorWithSlavePort(byte slavePortIndex = 1, byte slaveId = 5)
    {
        var sim = new McuReferenceSimulator();
        var portConfigs = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "RS-485 #1", Enabled = true, Role = ModbusPortRole.Master },
            new() { PortIndex = slavePortIndex, PortName = "RS-485 #2", Enabled = true, Role = ModbusPortRole.Slave, SlaveId = slaveId }
        };

        var portBuffer = new ushort[ModbusRegisterMap.ModbusPortConfigTableTotalLength];
        ModbusPortConfigCodec.EncodeAllPorts(portConfigs, portBuffer);
        sim.InternalWriteRegisters(ModbusRegisterMap.ModbusPortConfigBaseAddress, portBuffer);
        return sim;
    }

    // =========================================================================
    // TRỤC 1: HAPPY PATH (Kịch bản Chuẩn)
    // =========================================================================

    [Fact]
    public async Task ExecuteSlaveRead_WhenReadingDiWordAtAddress0_ReturnsPackedDigitalInputs()
    {
        var sim = CreateSimulatorWithSlavePort();
        var layout = sim.GetTagLayout();

        // Giả lập DI0 = 1, DI2 = 1, DI3 = 1 -> Bitmask: 0b0000_1101 = 0x000D
        sim.InternalWriteRegisters((ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (layout.DiBase + 0) * 2), new ushort[] { 0, 1 });
        sim.InternalWriteRegisters((ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (layout.DiBase + 2) * 2), new ushort[] { 0, 1 });
        sim.InternalWriteRegisters((ushort)(ModbusRegisterMap.RuntimeTagValuesBaseAddress + (layout.DiBase + 3) * 2), new ushort[] { 0, 1 });

        var regs = await sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.DiWordAddress, 1);

        Assert.Single(regs);
        Assert.Equal((ushort)0x000D, regs[0]);
    }

    [Fact]
    public async Task ExecuteSlaveWrite_WhenWritingDoWordAtAddress1_UpdatesPhysicalOutputs()
    {
        var sim = CreateSimulatorWithSlavePort();
        var layout = sim.GetTagLayout();

        // Ghi DO_WORD = 0x0005 (DO0 = 1, DO1 = 0, DO2 = 1)
        await sim.ExecuteSlaveWriteHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.DoWordAddress, new ushort[] { 0x0005 });

        // Đọc lại DO_WORD qua slave
        var readBack = await sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.DoWordAddress, 1);
        Assert.Equal((ushort)0x0005, readBack[0]);

        // Kiểm tra trực tiếp các tag DO trong bộ nhớ thiết bị
        var do0Val = sim.Memory.ReadRegisters(ModbusRegisterMap.GetRuntimeTagAddress((ushort)(layout.DoBase + 0)), 2);
        var do1Val = sim.Memory.ReadRegisters(ModbusRegisterMap.GetRuntimeTagAddress((ushort)(layout.DoBase + 1)), 2);
        var do2Val = sim.Memory.ReadRegisters(ModbusRegisterMap.GetRuntimeTagAddress((ushort)(layout.DoBase + 2)), 2);

        Assert.Equal(1, RegisterCodec.DecodeInt32(do0Val));
        Assert.Equal(0, RegisterCodec.DecodeInt32(do1Val));
        Assert.Equal(1, RegisterCodec.DecodeInt32(do2Val));
    }

    [Fact]
    public async Task ExecuteSlaveRead_WhenReadingAiFloat32AtAddress0x10_ReturnsValidIeee754Registers()
    {
        var sim = CreateSimulatorWithSlavePort();
        var layout = sim.GetTagLayout();

        // Giả lập AI0 = 25.5f
        float testValue = 25.5f;
        var buf = new ushort[2];
        RegisterCodec.EncodeFloat(testValue, buf);
        ushort ai0TagAddr = ModbusRegisterMap.GetRuntimeTagAddress((ushort)(layout.AiBase + 0));
        sim.InternalWriteRegisters(ai0TagAddr, buf);

        // Đọc AI0 (2 thanh ghi tại 0x0010)
        var regs = await sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.AiBaseAddress, 2);

        Assert.Equal(2, regs.Length);
        float decoded = RegisterCodec.DecodeFloat(regs);
        Assert.Equal(testValue, decoded, 3);
    }

    [Fact]
    public async Task ExecuteSlaveWrite_WhenWritingVregAtAddress0x20_UpdatesRuntimeTagValue()
    {
        var sim = CreateSimulatorWithSlavePort();
        var layout = sim.GetTagLayout();

        // Ghi VREG0 (2 thanh ghi tại 0x0020) = 123456
        int testInt = 123456;
        var buf = new ushort[2];
        RegisterCodec.EncodeInt32(testInt, buf);

        await sim.ExecuteSlaveWriteHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.VregBaseAddress, buf);

        // Đọc lại từ slave
        var readBack = await sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.VregBaseAddress, 2);
        int decoded = RegisterCodec.DecodeInt32(readBack);
        Assert.Equal(testInt, decoded);

        // Kiểm tra trực tiếp tag trong bộ nhớ thiết bị
        ushort vreg0TagAddr = ModbusRegisterMap.GetRuntimeTagAddress((ushort)(layout.VregBase + 0));
        var rawTag = sim.Memory.ReadRegisters(vreg0TagAddr, 2);
        Assert.Equal(testInt, RegisterCodec.DecodeInt32(rawTag));
    }

    [Fact]
    public async Task ExecuteSlaveReadWrite_WhenOperatingCoilsAndDiscreteInputs_MatchesFixedBitMap()
    {
        var sim = CreateSimulatorWithSlavePort();
        var layout = sim.GetTagLayout();

        // Giả lập DI1 = 1
        sim.InternalWriteRegisters(ModbusRegisterMap.GetRuntimeTagAddress((ushort)(layout.DiBase + 1)), new ushort[] { 0, 1 });

        // Đọc Discrete Inputs FC 02 tại address 0..3 (DI0..DI3)
        var diBits = await sim.ExecuteSlaveReadDiscreteInputsAsync(1, 5, 0, 4);
        Assert.False(diBits[0]);
        Assert.True(diBits[1]);
        Assert.False(diBits[2]);
        Assert.False(diBits[3]);

        // Ghi Coils FC 05/15 tại address 0..2 (DO0..DO2)
        await sim.ExecuteSlaveWriteCoilsAsync(1, 5, 0, new[] { true, false, true });

        var doBits = await sim.ExecuteSlaveReadCoilsAsync(1, 5, 0, 3);
        Assert.True(doBits[0]);
        Assert.False(doBits[1]);
        Assert.True(doBits[2]);

        // Ghi Coils VFLAG0 tại address 0x0064 (100)
        await sim.ExecuteSlaveWriteCoilsAsync(1, 5, ModbusSlaveFixedMap.CoilsVflagBaseAddress, new[] { true });
        var vflagBits = await sim.ExecuteSlaveReadCoilsAsync(1, 5, ModbusSlaveFixedMap.CoilsVflagBaseAddress, 1);
        Assert.True(vflagBits[0]);
    }

    [Fact]
    public async Task ExecuteSlaveWrite_WhenWritingVregRetainAtAddress0x60_UpdatesNonVolatileRegister()
    {
        var sim = CreateSimulatorWithSlavePort();
        var layout = sim.GetTagLayout();

        int testVal = 876543;
        var buf = new ushort[2];
        RegisterCodec.EncodeInt32(testVal, buf);

        await sim.ExecuteSlaveWriteHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.VregRetainBaseAddress, buf);

        var readBack = await sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.VregRetainBaseAddress, 2);
        Assert.Equal(testVal, RegisterCodec.DecodeInt32(readBack));

        ushort tagAddr = ModbusRegisterMap.GetRuntimeTagAddress((ushort)(layout.VregRetainBase + 0));
        var rawTag = sim.Memory.ReadRegisters(tagAddr, 2);
        Assert.Equal(testVal, RegisterCodec.DecodeInt32(rawTag));
    }

    [Fact]
    public async Task ExecuteSlaveWrite_WhenWritingCounterAtAddress0xA0_UpdatesCounterValue()
    {
        var sim = CreateSimulatorWithSlavePort();
        var layout = sim.GetTagLayout();

        int counterVal = 1000;
        var buf = new ushort[2];
        RegisterCodec.EncodeInt32(counterVal, buf);

        await sim.ExecuteSlaveWriteHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.CounterBaseAddress, buf);

        var readBack = await sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.CounterBaseAddress, 2);
        Assert.Equal(counterVal, RegisterCodec.DecodeInt32(readBack));

        ushort tagAddr = ModbusRegisterMap.GetRuntimeTagAddress((ushort)(layout.CounterBase + 0));
        var rawTag = sim.Memory.ReadRegisters(tagAddr, 2);
        Assert.Equal(counterVal, RegisterCodec.DecodeInt32(rawTag));
    }

    [Fact]
    public async Task ExecuteSlaveRead_WhenReadingSysDiagRegisters_ReturnsExpectedRegisterArray()
    {
        var sim = CreateSimulatorWithSlavePort();

        var regs = await sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.SystemDiagBaseAddress, 4);
        Assert.Equal(4, regs.Length);
    }

    // =========================================================================
    // TRỤC 2: BOUNDARY & OVERFLOW (Biên & Giới hạn)
    // =========================================================================

    [Fact]
    public async Task ExecuteSlaveRead_WhenReadingMaxVregAddressBoundary_ReturnsCorrectValue()
    {
        var sim = CreateSimulatorWithSlavePort();
        var layout = sim.GetTagLayout();

        // VREG31: address = 0x0020 + 31 * 2 = 0x005E
        ushort vreg31Address = (ushort)(ModbusSlaveFixedMap.VregBaseAddress + (31 * 2));
        int testVal = 99999;
        var buf = new ushort[2];
        RegisterCodec.EncodeInt32(testVal, buf);

        ushort tagAddr = ModbusRegisterMap.GetRuntimeTagAddress((ushort)(layout.VregBase + 31));
        sim.InternalWriteRegisters(tagAddr, buf);

        var readBack = await sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, vreg31Address, 2);
        Assert.Equal(testVal, RegisterCodec.DecodeInt32(readBack));
    }

    [Fact]
    public async Task ExecuteSlaveRead_WhenAddressIsUnmapped_ThrowsIllegalDataAddressException()
    {
        var sim = CreateSimulatorWithSlavePort();

        // 0x0005 nằm ở khoảng trống giữa VFLAG_DWORD (0x0002..0x0003) và AI (0x0010)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, 0x0005, 1));

        Assert.Contains("Modbus Exception 0x02", ex.Message);
        Assert.Contains("ILLEGAL_DATA_ADDRESS", ex.Message);
    }

    [Fact]
    public async Task ExecuteSlaveWrite_WhenCountSpansPastBoundary_ThrowsIllegalDataAddressException()
    {
        var sim = CreateSimulatorWithSlavePort();

        // VREG vùng 0x0020..0x005F (64 thanh ghi). Ghi 0x005F với count = 2 vượt sang 0x0060 (vùng khác)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveWriteHoldingRegistersAsync(1, 5, 0x005F, new ushort[] { 1, 2 }));

        Assert.Contains("Modbus Exception 0x02", ex.Message);
    }

    // =========================================================================
    // TRỤC 3: FAULT INJECTION & SECURITY (Ngoại lệ & Bảo vệ Sandbox)
    // =========================================================================

    [Fact]
    public async Task ExecuteSlaveWrite_WhenWritingToReadOnlyDiWord_ThrowsIllegalDataValueException()
    {
        var sim = CreateSimulatorWithSlavePort();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveWriteHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.DiWordAddress, new ushort[] { 0xFFFF }));

        Assert.Contains("Modbus Exception 0x03", ex.Message);
        Assert.Contains("ILLEGAL_DATA_VALUE", ex.Message);
        Assert.Contains("Read-Only", ex.Message);
    }

    [Fact]
    public async Task ExecuteSlaveWrite_WhenWritingToReadOnlyAnalogInput_ThrowsIllegalDataValueException()
    {
        var sim = CreateSimulatorWithSlavePort();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveWriteHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.AiBaseAddress, new ushort[] { 0x41CC, 0x0000 }));

        Assert.Contains("Modbus Exception 0x03", ex.Message);
        Assert.Contains("Read-Only", ex.Message);
    }

    [Fact]
    public async Task ExecuteSlaveWrite_WhenWritingToReadOnlySysDiag_ThrowsIllegalDataValueException()
    {
        var sim = CreateSimulatorWithSlavePort();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveWriteHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.SystemDiagBaseAddress, new ushort[] { 123 }));

        Assert.Contains("Modbus Exception 0x03", ex.Message);
        Assert.Contains("Read-Only", ex.Message);
    }

    [Fact]
    public async Task ExecuteSlaveRead_WhenSlaveIdMismatched_ThrowsInvalidOperationException()
    {
        var sim = CreateSimulatorWithSlavePort(slavePortIndex: 1, slaveId: 5);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveReadHoldingRegistersAsync(1, 99, ModbusSlaveFixedMap.DiWordAddress, 1));

        Assert.Contains("Slave ID mismatch", ex.Message);
    }

    [Fact]
    public async Task ExecuteSlaveRead_WhenCountIsZero_ThrowsIllegalDataValueException()
    {
        var sim = CreateSimulatorWithSlavePort();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.DiWordAddress, 0));

        Assert.Contains("Modbus Exception 0x03", ex.Message);
        Assert.Contains("ILLEGAL_DATA_VALUE", ex.Message);
    }

    [Fact]
    public async Task ExecuteSlaveRead_WhenCountExceedsModbusPduMax125_ThrowsIllegalDataValueException()
    {
        var sim = CreateSimulatorWithSlavePort();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.VregBaseAddress, 126));

        Assert.Contains("Modbus Exception 0x03", ex.Message);
        Assert.Contains("ILLEGAL_DATA_VALUE", ex.Message);
    }

    [Fact]
    public async Task ExecuteSlaveRead_WhenRequestSpansAcrossPartitionBoundaries_ThrowsIllegalDataAddressException()
    {
        var sim = CreateSimulatorWithSlavePort();

        // 0x0001 (DO_WORD) with count = 2 spans into 0x0002 (VFLAG_DWORD)
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveReadHoldingRegistersAsync(1, 5, ModbusSlaveFixedMap.DoWordAddress, 2));

        Assert.Contains("Modbus Exception 0x02", ex.Message);
        Assert.Contains("spans across partition boundaries", ex.Message);
    }

    [Fact]
    public async Task ExecuteSlaveRead_WhenPortIsConfiguredAsMaster_ThrowsInvalidOperationException()
    {
        var sim = CreateSimulatorWithSlavePort(slavePortIndex: 1, slaveId: 5);

        // Port 0 is Master
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sim.ExecuteSlaveReadHoldingRegistersAsync(0, 5, ModbusSlaveFixedMap.DiWordAddress, 1));

        Assert.Contains("not running in Modbus Slave mode", ex.Message);
    }
}
