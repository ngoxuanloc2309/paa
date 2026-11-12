using System;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Kiểm thử bộ mã hóa/giải mã cấu hình Last Will and Testament (LWT) theo chuẩn SPLC-SPEC-MQTT-004.
/// Địa chỉ thanh ghi: 0x0E60..0x0E7F (32 holding registers = 64 bytes).
/// </summary>
public sealed class MqttWillConfigCodecTests
{
    [Fact]
    public void EncodeAndDecode_Roundtrip_ShouldPreserveAllFields()
    {
        // Arrange - Happy path
        var original = new MqttWillConfigDto
        {
            Enabled = true,
            Retain = true,
            Qos = 2,
            Topic = "factory/node1/status",
            Payload = "offline"
        };

        var registers = new ushort[ModbusRegisterMap.MqttWillConfigRegisters];

        // Act
        MqttWillConfigCodec.Encode(original, registers);
        var decoded = MqttWillConfigCodec.Decode(registers);

        // Assert
        Assert.True(decoded.Enabled);
        Assert.True(decoded.Retain);
        Assert.Equal(2, decoded.Qos);
        Assert.Equal("factory/node1/status", decoded.Topic);
        Assert.Equal("offline", decoded.Payload);

        // Check Bitmask explicitly at offset 0
        // Bit 0 = Enabled (1), Bit 1 = Retain (1), Bit 2..3 = QoS (2 << 2 = 8) -> 1 | 2 | 8 = 11 (0x000B)
        Assert.Equal(0x000B, registers[0]);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 4)]
    [InlineData(2, 8)]
    [InlineData(3, 12)]
    public void Encode_QosValues_ShouldMapToBits2And3(byte qos, ushort expectedQosBits)
    {
        var config = new MqttWillConfigDto
        {
            Enabled = false,
            Retain = false,
            Qos = qos,
            Topic = "test",
            Payload = "off"
        };

        var registers = new ushort[ModbusRegisterMap.MqttWillConfigRegisters];
        MqttWillConfigCodec.Encode(config, registers);

        Assert.Equal(expectedQosBits, registers[0] & ModbusRegisterMap.MqttWillControlQosMask);
    }

    [Fact]
    public void Encode_BoundaryStrings_ShouldTruncateToSpecLimits()
    {
        // Topic max 32 chars, Payload max 30 chars
        var config = new MqttWillConfigDto
        {
            Enabled = true,
            Retain = false,
            Qos = 0,
            Topic = new string('A', 50),
            Payload = new string('B', 50)
        };

        var registers = new ushort[ModbusRegisterMap.MqttWillConfigRegisters];
        MqttWillConfigCodec.Encode(config, registers);
        var decoded = MqttWillConfigCodec.Decode(registers);

        Assert.Equal(MqttWillConfigCodec.MaxTopicChars, decoded.Topic.Length);
        Assert.Equal(new string('A', MqttWillConfigCodec.MaxTopicChars), decoded.Topic);

        Assert.Equal(MqttWillConfigCodec.MaxPayloadChars, decoded.Payload.Length);
        Assert.Equal(new string('B', MqttWillConfigCodec.MaxPayloadChars), decoded.Payload);
    }

    [Fact]
    public void Encode_DestinationSpanTooSmall_ThrowsArgumentException()
    {
        var config = new MqttWillConfigDto { Enabled = true };
        var smallSpan = new ushort[10];

        Assert.Throws<ArgumentException>(() => MqttWillConfigCodec.Encode(config, smallSpan));
    }

    [Fact]
    public void Decode_SourceSpanTooSmall_ThrowsArgumentException()
    {
        var smallSpan = new ushort[10];
        Assert.Throws<ArgumentException>(() => MqttWillConfigCodec.Decode(smallSpan));
    }
}
