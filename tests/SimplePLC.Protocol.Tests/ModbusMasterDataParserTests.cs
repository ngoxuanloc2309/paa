using System;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class ModbusMasterDataParserTests
{
    [Fact]
    public void TryParsePinValue_Bool_ParsesCorrectly()
    {
        ushort[] registers = [0x0001, 0x0000];

        bool ok1 = ModbusMasterDataParser.TryParsePinValue(registers, 0, ModbusDataType.Bool, ModbusEndianness.BigEndian_ABCD, 1.0f, out double val1);
        Assert.True(ok1);
        Assert.Equal(1.0, val1);

        bool ok0 = ModbusMasterDataParser.TryParsePinValue(registers, 1, ModbusDataType.Bool, ModbusEndianness.BigEndian_ABCD, 1.0f, out double val0);
        Assert.True(ok0);
        Assert.Equal(0.0, val0);
    }

    [Fact]
    public void TryParsePinValue_Int16_AppliesMultiplier()
    {
        // 0xFFF6 = -10 in Int16
        ushort[] registers = [unchecked((ushort)-10), 250];

        bool okNeg = ModbusMasterDataParser.TryParsePinValue(registers, 0, ModbusDataType.Int16, ModbusEndianness.BigEndian_ABCD, 1.0f, out double valNeg);
        Assert.True(okNeg);
        Assert.Equal(-10.0, valNeg);

        bool okScaled = ModbusMasterDataParser.TryParsePinValue(registers, 1, ModbusDataType.Int16, ModbusEndianness.BigEndian_ABCD, 0.1f, out double valScaled);
        Assert.True(okScaled);
        Assert.Equal(25.0, valScaled, 3);
    }

    [Fact]
    public void TryParsePinValue_UInt16_ParsesCorrectly()
    {
        ushort[] registers = [50000];

        bool ok = ModbusMasterDataParser.TryParsePinValue(registers, 0, ModbusDataType.UInt16, ModbusEndianness.BigEndian_ABCD, 1.0f, out double val);
        Assert.True(ok);
        Assert.Equal(50000.0, val);
    }

    [Fact]
    public void TryParsePinValue_Real_AcrossAll4EndiannessModes()
    {
        // Float 25.5f in IEEE-754:
        // Binary: 0x41CC0000
        // ABCD: 0x41CC, 0x0000 -> Byte seq: [41, CC, 00, 00] -> ABCD gives 0x41CC0000
        // CDAB: 0x0000, 0x41CC -> Byte seq: [00, 00, 41, CC] -> CDAB gives 0x41CC0000
        // BADC: 0xCC41, 0x0000 -> Byte seq: [CC, 41, 00, 00] -> BADC gives 0x41CC0000
        // DCBA: 0x0000, 0x00CC (wait: 0x0000, 0x00CC? Let's check DCBA formula: ((b3<<24)|(b2<<16)|(b1<<8)|b0))
        // In DCBA: reg0=[B0, B1], reg1=[B2, B3]. If B3=41, B2=CC, B1=00, B0=00, then reg0=0x0000, reg1=0xCC41.
        // Let's test with 25.5f:
        float expected = 25.5f;

        // 1. ABCD
        ushort[] abcd = [0x41CC, 0x0000];
        Assert.True(ModbusMasterDataParser.TryParsePinValue(abcd, 0, ModbusDataType.Real, ModbusEndianness.BigEndian_ABCD, 1.0f, out double valAbcd));
        Assert.Equal(expected, (float)valAbcd, 3);

        // 2. CDAB
        ushort[] cdab = [0x0000, 0x41CC];
        Assert.True(ModbusMasterDataParser.TryParsePinValue(cdab, 0, ModbusDataType.Real, ModbusEndianness.MidBig_CDAB, 1.0f, out double valCdab));
        Assert.Equal(expected, (float)valCdab, 3);

        // 3. BADC
        ushort[] badc = [0xCC41, 0x0000];
        Assert.True(ModbusMasterDataParser.TryParsePinValue(badc, 0, ModbusDataType.Real, ModbusEndianness.ByteSwap_BADC, 1.0f, out double valBadc));
        Assert.Equal(expected, (float)valBadc, 3);

        // 4. DCBA
        ushort[] dcba = [0x0000, 0xCC41];
        Assert.True(ModbusMasterDataParser.TryParsePinValue(dcba, 0, ModbusDataType.Real, ModbusEndianness.LittleEndian_DCBA, 1.0f, out double valDcba));
        Assert.Equal(expected, (float)valDcba, 3);
    }

    [Fact]
    public void TryParseHexToRegisters_PureRegisterHex_ExtractsRegisters()
    {
        // 2 registers: 0x0102, 0x0304
        string hex = "0102 0304";
        bool success = ModbusMasterDataParser.TryParseHexToRegisters(hex, out ushort[] regs, out string error);

        Assert.True(success);
        Assert.Empty(error);
        Assert.Equal(2, regs.Length);
        Assert.Equal(0x0102, regs[0]);
        Assert.Equal(0x0304, regs[1]);
    }

    [Fact]
    public void TryParseHexToRegisters_FullModbusRtuResponseFrame_ExtractsPayload()
    {
        // Response frame: SlaveId(01), FC(03), ByteCount(04), Data(01 02 03 04), CRC(B9 33)
        string rtuFrame = "01 03 04 01 02 03 04 B9 33";
        bool success = ModbusMasterDataParser.TryParseHexToRegisters(rtuFrame, out ushort[] regs, out string error);

        Assert.True(success);
        Assert.Empty(error);
        Assert.Equal(2, regs.Length);
        Assert.Equal(0x0102, regs[0]);
        Assert.Equal(0x0304, regs[1]);
    }

    [Fact]
    public void TryParseHexToRegisters_OddHexLength_ReturnsFalseWithError()
    {
        bool success = ModbusMasterDataParser.TryParseHexToRegisters("01020", out _, out string error);
        Assert.False(success);
        Assert.NotEmpty(error);
    }
}
