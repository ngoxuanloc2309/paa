using System;
using System.Collections.Generic;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Bộ kiểm thử độc lập cho Codec cấu hình Cổng RS-485 Modbus Master (SPLC-SPEC-MBM-003).
/// Kiểm chứng tính toàn vẹn nhị phân, thứ tự byte/word Big-Endian và khả năng tương thích ngược.
/// </summary>
public sealed class ModbusPortConfigCodecTests
{
    [Fact]
    public void EncodePort_ValidDto_PacksRegistersCorrectly()
    {
        var dto = new ModbusPortConfigDto
        {
            PortIndex = 0,
            PortName = "RS-485 #1",
            Enabled = true,
            BaudRate = 115200,
            DataBits = 8,
            Parity = ModbusParity.Even,
            StopBits = 1,
            ResponseTimeoutMs = 250,
            InterFrameDelayMs = 15,
            RetryCount = 2
        };

        var buffer = new ushort[ModbusRegisterMap.ModbusPortConfigRegistersPerPort];
        ModbusPortConfigCodec.EncodePort(dto, buffer);

        // Offset 0: Enabled (0x0100) | PortNumber (1) = 0x0101
        Assert.Equal(0x0101, buffer[0]);

        // Offset 1..2: Baudrate 115200 = 0x0001 C200 -> [1]=0x0001, [2]=0xC200
        Assert.Equal(0x0001, buffer[1]);
        Assert.Equal(0xC200, buffer[2]);

        // Offset 3: Parity Even = 1
        Assert.Equal(1, buffer[3]);

        // Offset 4: DataBits (8) | StopBits (1) = 0x0801
        Assert.Equal(0x0801, buffer[4]);

        // Offset 5: Timeout 250ms
        Assert.Equal(250, buffer[5]);

        // Offset 6: Delay 15ms
        Assert.Equal(15, buffer[6]);

        // Offset 7: Retries 2
        Assert.Equal(2, buffer[7]);

        // Reserved registers 14..15 must be 0
        Assert.Equal(0, buffer[14]);
        Assert.Equal(0, buffer[15]);
    }

    [Fact]
    public void DecodePort_ValidRegisters_UnpacksFidelity()
    {
        var original = new ModbusPortConfigDto
        {
            PortIndex = 2,
            PortName = "PMP_BUS",
            Enabled = true,
            BaudRate = 38400,
            DataBits = 8,
            Parity = ModbusParity.Odd,
            StopBits = 2,
            ResponseTimeoutMs = 500,
            InterFrameDelayMs = 50,
            RetryCount = 3
        };

        var buffer = new ushort[ModbusRegisterMap.ModbusPortConfigRegistersPerPort];
        ModbusPortConfigCodec.EncodePort(original, buffer);

        var decoded = ModbusPortConfigCodec.DecodePort(buffer, defaultPortIndex: 0);

        Assert.Equal(original.PortIndex, decoded.PortIndex);
        Assert.Equal("PMP_BUS", decoded.PortName);
        Assert.True(decoded.Enabled);
        Assert.Equal(original.BaudRate, decoded.BaudRate);
        Assert.Equal(original.DataBits, decoded.DataBits);
        Assert.Equal(original.Parity, decoded.Parity);
        Assert.Equal(original.StopBits, decoded.StopBits);
        Assert.Equal(original.ResponseTimeoutMs, decoded.ResponseTimeoutMs);
        Assert.Equal(original.InterFrameDelayMs, decoded.InterFrameDelayMs);
        Assert.Equal(original.RetryCount, decoded.RetryCount);
    }

    [Fact]
    public void EncodeAllPorts_And_DecodeAllPorts_FullTableRoundTrip()
    {
        var ports = new List<ModbusPortConfigDto>
        {
            new() { PortIndex = 0, PortName = "PORT1", BaudRate = 9600, Parity = ModbusParity.None, StopBits = 1 },
            new() { PortIndex = 1, PortName = "PORT2", BaudRate = 19200, Parity = ModbusParity.Even, StopBits = 1 },
            new() { PortIndex = 2, PortName = "PORT3", BaudRate = 115200, Parity = ModbusParity.None, StopBits = 2 },
            new() { PortIndex = 3, PortName = "PORT4", BaudRate = 57600, Parity = ModbusParity.Odd, StopBits = 1, Enabled = false }
        };

        var buffer = new ushort[ModbusRegisterMap.ModbusPortConfigTableTotalLength];
        ModbusPortConfigCodec.EncodeAllPorts(ports, buffer);

        var decodedList = ModbusPortConfigCodec.DecodeAllPorts(buffer);

        Assert.Equal(4, decodedList.Count);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(ports[i].PortIndex, decodedList[i].PortIndex);
            Assert.Equal(ports[i].PortName, decodedList[i].PortName);
            Assert.Equal(ports[i].BaudRate, decodedList[i].BaudRate);
            Assert.Equal(ports[i].Parity, decodedList[i].Parity);
            Assert.Equal(ports[i].StopBits, decodedList[i].StopBits);
            Assert.Equal(ports[i].Enabled, decodedList[i].Enabled);
        }
    }

    [Fact]
    public void ModbusMasterReadCodec_PreservesPortIndex()
    {
        var readDto = new ModbusMasterReadConfigDto
        {
            BlockIndex = 1,
            PortIndex = 2, // Port 3
            SlaveId = 5,
            StartAddress = 100,
            RegCount = 4,
            PollIntervalMs = 250,
            Enabled = true
        };

        var buffer = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        ModbusMasterCodec.EncodeBlock(readDto, buffer);

        // Control word bit 4..7 holds port index 2
        int portFromControl = (buffer[0] >> ModbusRegisterMap.ModbusMasterControlPortShift) & 0x0F;
        Assert.Equal(2, portFromControl);
        // Offset 14 holds explicit port index
        Assert.Equal(2, buffer[ModbusRegisterMap.ModbusMasterReadHeaderPortIndexOffset]);

        var decoded = ModbusMasterCodec.DecodeBlock(buffer, blockIndex: 1);
        Assert.Equal(2, decoded.PortIndex);
        Assert.Equal(readDto.SlaveId, decoded.SlaveId);
        Assert.Equal(readDto.StartAddress, decoded.StartAddress);
    }

    [Fact]
    public void ModbusMasterWriteCodec_PreservesPortIndex()
    {
        var writeDto = new ModbusMasterWriteConfigDto
        {
            BlockIndex = 0,
            PortIndex = 3, // Port 4
            SlaveId = 10,
            WriteMode = ModbusWriteMode.DirectRegisters,
            StartAddress = 200,
            PointCount = 2,
            Enabled = true
        };

        var buffer = new ushort[ModbusRegisterMap.ModbusMasterRegistersPerBlock];
        ModbusMasterCodec.EncodeWriteBlock(writeDto, buffer);

        // Control word bit 4..7 holds port index 3
        int portFromControl = (buffer[0] >> ModbusRegisterMap.ModbusMasterControlPortShift) & 0x0F;
        Assert.Equal(3, portFromControl);

        var decoded = ModbusMasterCodec.DecodeWriteBlock(buffer, blockIndex: 0);
        Assert.Equal(3, decoded.PortIndex);
        Assert.Equal(writeDto.SlaveId, decoded.SlaveId);
        Assert.Equal(writeDto.StartAddress, decoded.StartAddress);
    }

    [Fact]
    public void ModbusRegisterMap_CalculatesPortAddressesCorrectly()
    {
        Assert.Equal(0x1400, ModbusRegisterMap.GetModbusPortConfigAddress(0));
        Assert.Equal(0x1410, ModbusRegisterMap.GetModbusPortConfigAddress(1));
        Assert.Equal(0x1420, ModbusRegisterMap.GetModbusPortConfigAddress(2));
        Assert.Equal(0x1430, ModbusRegisterMap.GetModbusPortConfigAddress(3));

        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusRegisterMap.GetModbusPortConfigAddress(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => ModbusRegisterMap.GetModbusPortConfigAddress(-1));
    }

    [Fact]
    public void DecodePort_WhenBit9Is1_ButOffset14Is0_DecodesAsMaster_BecauseOffset14IsAuthoritative()
    {
        var buffer = new ushort[ModbusRegisterMap.ModbusPortConfigRegistersPerPort];
        buffer[0] = 0x0301; // bit 8=Enabled, bit 9=RoleSlave (legacy), port 1
        buffer[14] = 0; // Authoritative: Master

        var decoded = ModbusPortConfigCodec.DecodePort(buffer);
        Assert.Equal(ModbusPortRole.Master, decoded.Role);
    }

    [Fact]
    public void DecodePort_WhenBit9Is0_ButOffset14Is1_DecodesAsSlave_BecauseOffset14IsAuthoritative()
    {
        var buffer = new ushort[ModbusRegisterMap.ModbusPortConfigRegistersPerPort];
        buffer[0] = 0x0101; // bit 8=Enabled, bit 9=0, port 1
        buffer[14] = 1; // Authoritative: Slave
        buffer[15] = 9; // Slave ID 9

        var decoded = ModbusPortConfigCodec.DecodePort(buffer);
        Assert.Equal(ModbusPortRole.Slave, decoded.Role);
        Assert.Equal((byte)9, decoded.SlaveId);
    }
}
