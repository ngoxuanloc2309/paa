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
            Password = "SuperSecretPassword123!",
            UseMtls = true,
            IgnoreHostname = true,
            AllowSelfSigned = true
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
        Assert.True(decoded.UseMtls);
        Assert.True(decoded.IgnoreHostname);
        Assert.True(decoded.AllowSelfSigned);
    }

    [Fact]
    public void Encode_ControlFlags_ShouldPackBitsCorrectly()
    {
        // Arrange
        var config = new MqttBrokerConfigDto
        {
            Enabled = true,           // bit 0: 1 (0x0001)
            UseTls = true,            // bit 1: 1 (0x0002)
            CleanSession = true,      // bit 2: 1 (0x0004)
            ProtocolVersion = 1,      // bit 3..4: 01b (1 << 3 = 8 = 0x0008)
            UseMtls = true,           // bit 5: 1 (0x0020)
            IgnoreHostname = true,    // bit 6: 1 (0x0040)
            AllowSelfSigned = true    // bit 7: 1 (0x0080)
            // Tổng: 1 + 2 + 4 + 8 + 32 + 64 + 128 = 239 = 0x00EF
        };

        var registers = new ushort[ModbusRegisterMap.MqttBrokerConfigRegisters];

        // Act
        MqttBrokerConfigCodec.Encode(config, registers);

        // Assert
        Assert.Equal((ushort)0x00EF, registers[0]);
    }

    [Fact]
    public void Decode_LegacyZeroSecurityBits_ShouldDecodeAsFalse()
    {
        // Arrange: Cấu hình cũ không có bit 5, 6, 7
        var registers = new ushort[ModbusRegisterMap.MqttBrokerConfigRegisters];
        registers[0] = 0x0007; // Enabled + UseTls + CleanSession, các bit còn lại = 0

        // Act
        var decoded = MqttBrokerConfigCodec.Decode(registers);

        // Assert
        Assert.True(decoded.Enabled);
        Assert.True(decoded.UseTls);
        Assert.True(decoded.CleanSession);
        Assert.False(decoded.UseMtls);
        Assert.False(decoded.IgnoreHostname);
        Assert.False(decoded.AllowSelfSigned);
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
