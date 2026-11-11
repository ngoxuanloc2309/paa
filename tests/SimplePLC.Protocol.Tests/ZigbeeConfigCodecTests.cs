namespace SimplePLC.Protocol.Tests;

using System;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

/// <summary>
/// Bộ kiểm thử đơn vị cho ZigbeeConfigCodec tuân thủ quy tắc IEC 61508-3 Triad Testing Rule.
/// </summary>
public sealed class ZigbeeConfigCodecTests
{
    [Fact]
    public void EncodeAndDecode_HappyPath_ShouldPreserveAllFields()
    {
        // Arrange
        var key = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
        var original = new ZigbeeConfigDto
        {
            Enabled = true,
            Role = ZigbeeRole.Slave,
            Framing = ZigbeeFraming.Rtu,
            SecurityEnabled = true,
            Antenna = ZigbeeAntenna.External,
            PreserveKey = false,
            SlaveId = 5,
            PanId = 0x1234,
            Channel = 15,
            NetKey = key
        };

        var registers = new ushort[ModbusRegisterMap.ZigbeeConfigRegisters];

        // Act
        ZigbeeConfigCodec.Encode(original, registers);
        var decoded = ZigbeeConfigCodec.Decode(registers);

        // Assert
        Assert.True(decoded.Enabled);
        Assert.Equal(ZigbeeRole.Slave, decoded.Role);
        Assert.Equal(ZigbeeFraming.Rtu, decoded.Framing);
        Assert.True(decoded.SecurityEnabled);
        Assert.Equal(ZigbeeAntenna.External, decoded.Antenna);
        Assert.False(decoded.PreserveKey);
        Assert.Equal(5, decoded.SlaveId);
        Assert.Equal((ushort)0x1234, decoded.PanId);
        Assert.Equal((ushort)15, decoded.Channel);
        Assert.Equal(key, decoded.NetKey);
    }

    [Fact]
    public void Encode_ControlFlags_ShouldPackBitsCorrectlyAccordingToSpec()
    {
        // Arrange:
        // ENABLE (bit 0 = 1)
        // ROLE_SLAVE (bit 1 = 1 -> 0x0002)
        // FRAMING_RTU (bit 2 = 0)
        // SECURITY (bit 3 = 1 -> 0x0008)
        // ANTENNA_EXTERNAL (bit 4 = 1 -> 0x0010)
        // PRESERVE_KEY (bit 5 = 1 -> 0x0020)
        // Expected CONTROL_FLAGS = 1 + 2 + 8 + 16 + 32 = 59 = 0x003B
        var config = new ZigbeeConfigDto
        {
            Enabled = true,
            Role = ZigbeeRole.Slave,
            Framing = ZigbeeFraming.Rtu,
            SecurityEnabled = true,
            Antenna = ZigbeeAntenna.External,
            PreserveKey = true
        };

        var registers = new ushort[16];

        // Act
        ZigbeeConfigCodec.Encode(config, registers);

        // Assert
        Assert.Equal((ushort)0x003B, registers[0]);
    }

    [Fact]
    public void Encode_NetKey_ShouldPackBigEndianHighByteFirst()
    {
        // Arrange: Sample from zigbee_intergation.md section 13:
        // 0x0011 0x2233 0x4455 0x6677 0x8899 0xAABB 0xCCDD 0xEEFF
        var key = new byte[] { 0x00, 0x11, 0x22, 0x33, 0x44, 0x55, 0x66, 0x77, 0x88, 0x99, 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF };
        var config = new ZigbeeConfigDto
        {
            Enabled = true,
            NetKey = key
        };

        var registers = new ushort[ModbusRegisterMap.ZigbeeConfigRegisters];

        // Act
        ZigbeeConfigCodec.Encode(config, registers);

        // Assert
        Assert.Equal((ushort)0x0011, registers[5]);
        Assert.Equal((ushort)0x2233, registers[6]);
        Assert.Equal((ushort)0x4455, registers[7]);
        Assert.Equal((ushort)0x6677, registers[8]);
        Assert.Equal((ushort)0x8899, registers[9]);
        Assert.Equal((ushort)0xAABB, registers[10]);
        Assert.Equal((ushort)0xCCDD, registers[11]);
        Assert.Equal((ushort)0xEEFF, registers[12]);
    }

    [Fact]
    public void Decode_TelemetryAndStatusFlags_ShouldParseCorrectly()
    {
        // Arrange
        var registers = new ushort[ModbusRegisterMap.ZigbeeConfigRegisters];
        registers[0] = 0x0003; // Enabled + Slave
        registers[1] = 6;      // SlaveId
        registers[32] = 2;     // LinkState = Up
        registers[33] = 0;     // ErrorCode = NoError
        registers[34] = 8;     // NodeCount = 8
        registers[35] = unchecked((ushort)-65); // Rssi = -65 dBm
        registers[36] = 1;     // Bit 0: KEY_SET = 1

        // Act
        var decoded = ZigbeeConfigCodec.Decode(registers);

        // Assert
        Assert.Equal(ZigbeeLinkState.Up, decoded.LinkState);
        Assert.Equal(ZigbeeErrorCode.NoError, decoded.ErrorCode);
        Assert.Equal(8, decoded.NodeCount);
        Assert.Equal((short)-65, decoded.Rssi);
        Assert.True(decoded.IsKeySet);
    }

    [Fact]
    public void Encode_DestinationBufferTooShort_ShouldThrowArgumentException()
    {
        // Arrange
        var config = new ZigbeeConfigDto();
        var shortBuffer = new ushort[10]; // Less than 13

        // Act & Assert
        Assert.Throws<ArgumentException>(() => ZigbeeConfigCodec.Encode(config, shortBuffer));
    }
}
