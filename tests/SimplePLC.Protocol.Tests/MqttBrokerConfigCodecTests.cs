using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public sealed class MqttBrokerConfigCodecTests
{
    [Fact]
    public void EncodeAndDecode_Roundtrip_ShouldPreserveAllFields()
    {
        // Arrange
        var original = new MqttBrokerConfigDto
        {
            Enabled = true,
            Host = "broker.hivemq.com",
            Port = 8883,
            UseTls = true,
            CleanSession = false,
            ProtocolVersion = 1, // MQTT 5.0
            KeepAliveSec = 120,
            ClientId = "synaptix-station-alpha",
            Username = "factory_operator",
            Password = "SuperSecretPassword123!"
        };

        var registers = new ushort[ModbusRegisterMap.MqttBrokerConfigRegisters];

        // Act
        MqttBrokerConfigCodec.Encode(original, registers);
        var decoded = MqttBrokerConfigCodec.Decode(registers);

        // Assert
        Assert.Equal(original.Enabled, decoded.Enabled);
        Assert.Equal(original.Host, decoded.Host);
        Assert.Equal(original.Port, decoded.Port);
        Assert.Equal(original.UseTls, decoded.UseTls);
        Assert.Equal(original.CleanSession, decoded.CleanSession);
        Assert.Equal(original.ProtocolVersion, decoded.ProtocolVersion);
        Assert.Equal(original.KeepAliveSec, decoded.KeepAliveSec);
        Assert.Equal(original.ClientId, decoded.ClientId);
        Assert.Equal(original.Username, decoded.Username);
        Assert.Equal(original.Password, decoded.Password);
    }

    [Fact]
    public void Encode_ControlFlags_ShouldPackBitsCorrectly()
    {
        // Arrange
        var config = new MqttBrokerConfigDto
        {
            Enabled = true,           // bit 0: 1
            UseTls = true,            // bit 1: 1
            CleanSession = true,      // bit 2: 1
            ProtocolVersion = 1       // bit 3..4: 01b (1 << 3 = 8)
            // Tổng: 1 + 2 + 4 + 8 = 15 = 0x000F
        };

        var registers = new ushort[ModbusRegisterMap.MqttBrokerConfigRegisters];

        // Act
        MqttBrokerConfigCodec.Encode(config, registers);

        // Assert
        Assert.Equal((ushort)0x000F, registers[0]);
    }

    [Fact]
    public void Encode_BoundaryHostAndCredentials_ShouldTruncateSafely()
    {
        // Arrange: Host 70 chars (> 64), ClientId 40 chars (> 32)
        var config = new MqttBrokerConfigDto
        {
            Host = new string('H', 70),
            ClientId = new string('C', 40)
        };

        var registers = new ushort[ModbusRegisterMap.MqttBrokerConfigRegisters];

        // Act
        MqttBrokerConfigCodec.Encode(config, registers);
        var decoded = MqttBrokerConfigCodec.Decode(registers);

        // Assert
        Assert.Equal(64, decoded.Host.Length);
        Assert.Equal(32, decoded.ClientId.Length);
    }
}
