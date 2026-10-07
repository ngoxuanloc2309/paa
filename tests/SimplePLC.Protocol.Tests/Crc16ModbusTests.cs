using SimplePLC.Protocol.Cryptography;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class Crc16ModbusTests
{
    [Fact]
    public void Compute_EmptyData_ReturnsInitialValue()
    {
        ReadOnlySpan<byte> empty = ReadOnlySpan<byte>.Empty;
        ushort crc = Crc16Modbus.Compute(empty);
        Assert.Equal(0xFFFF, crc);
    }

    [Fact]
    public void Compute_StandardModbusRtuRequest_MatchesKnownCrc()
    {
        // Vector Modbus RTU phổ biến: Đọc 10 holding registers từ địa chỉ 0 (Slave 1, FC 3)
        // Request: 01 03 00 00 00 0A -> Modbus CRC16 = 0xCDC5 (CRC LO = 0xC5, CRC HI = 0xCD)
        byte[] request = [0x01, 0x03, 0x00, 0x00, 0x00, 0x0A];
        ushort crc = Crc16Modbus.Compute(request);

        Assert.Equal(0xCDC5, crc);
    }

    [Fact]
    public void ComputeFromRegisters_MatchesComputeWithBigEndianBytes()
    {
        // 4 thanh ghi Modbus (8 bytes)
        ushort[] registers = [0x1234, 0x5678, 0x9ABC, 0xDEF0];
        
        // Mảng byte Big-Endian tương đương
        byte[] bytes = [
            0x12, 0x34,
            0x56, 0x78,
            0x9A, 0xBC,
            0xDE, 0xF0
        ];

        ushort crcFromBytes = Crc16Modbus.Compute(bytes);
        ushort crcFromRegs = Crc16Modbus.ComputeFromRegisters(registers);

        Assert.Equal(crcFromBytes, crcFromRegs);
    }

    [Fact]
    public void Compute_DeterministicForSameData()
    {
        byte[] data = new byte[1184]; // 37 rules * 32 bytes
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)(i & 0xFF);
        }

        ushort crc1 = Crc16Modbus.Compute(data);
        ushort crc2 = Crc16Modbus.Compute(data);

        Assert.Equal(crc1, crc2);
        Assert.NotEqual(0, crc1);
    }
}
