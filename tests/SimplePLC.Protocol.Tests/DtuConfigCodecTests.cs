using System;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public sealed class DtuConfigCodecTests
{
    [Fact]
    public void MqttWillConfig_Roundtrip_ShouldPreserveAllFields()
    {
        // Arrange
        var original = new MqttWillConfigDto
        {
            Enabled = true,
            Topic = "plant/sensor/status",
            Payload = "{\"online\":false}",
            Qos = 2,
            Retain = true
        };

        var registers = new ushort[ModbusRegisterMap.MqttWillConfigRegisters];

        // Act
        MqttWillConfigCodec.Encode(original, registers);
        var decoded = MqttWillConfigCodec.Decode(registers);

        // Assert
        Assert.Equal(original.Enabled, decoded.Enabled);
        Assert.Equal(original.Topic, decoded.Topic);
        Assert.Equal(original.Payload, decoded.Payload);
        Assert.Equal(original.Qos, decoded.Qos);
        Assert.Equal(original.Retain, decoded.Retain);
    }

    [Fact]
    public void MqttWillConfig_ControlBits_ShouldBeCorrect()
    {
        var config = new MqttWillConfigDto
        {
            Enabled = true,
            Retain = true,
            Qos = 1 // bit 2: 1 << 2 = 4. 1 (en) + 2 (retain) + 4 (qos) = 7 (0x0007)
        };

        var registers = new ushort[ModbusRegisterMap.MqttWillConfigRegisters];
        MqttWillConfigCodec.Encode(config, registers);

        Assert.Equal(0x0007, registers[0]);
    }

    [Fact]
    public void DtuHttpConfig_Roundtrip_ShouldPreserveAllFields()
    {
        // Arrange
        var original = new DtuHttpConfigDto
        {
            Enabled = true,
            Method = DtuHttpMethod.Put,
            TimeoutSec = 15,
            PeriodicIntervalSec = 10,
            Url = "https://api.factory-cloud.io/v1/telemetry",
            CustomHeaders = "Authorization: Bearer token123\nContent-Type: app/json"
        };

        var registers = new ushort[ModbusRegisterMap.DtuHttpConfigRegisters];

        // Act
        DtuConfigCodec.EncodeHttp(original, registers);
        var decoded = DtuConfigCodec.DecodeHttp(registers);

        // Assert
        Assert.Equal(original.Enabled, decoded.Enabled);
        Assert.Equal(original.Method, decoded.Method);
        Assert.Equal(original.TimeoutSec, decoded.TimeoutSec);
        Assert.Equal(original.PeriodicIntervalSec, decoded.PeriodicIntervalSec);
        Assert.Equal(original.Url, decoded.Url);
        Assert.Equal(original.CustomHeaders, decoded.CustomHeaders);
    }

    [Fact]
    public void DtuTcpConfig_Roundtrip_ShouldPreserveAllFields()
    {
        // Arrange
        var original = new DtuTcpConfigDto
        {
            Enabled = true,
            SocketMode = DtuTcpSocketMode.Server,
            RemoteHost = "192.168.1.100",
            RemotePort = 502,
            LocalPort = 9000,
            ReconnectDelaySec = 3,
            HeartbeatIntervalSec = 20,
            HeartbeatPayload = "PING_MCU_DTU"
        };

        var registers = new ushort[ModbusRegisterMap.DtuTcpConfigRegisters];

        // Act
        DtuConfigCodec.EncodeTcp(original, registers);
        var decoded = DtuConfigCodec.DecodeTcp(registers);

        // Assert
        Assert.Equal(original.Enabled, decoded.Enabled);
        Assert.Equal(original.SocketMode, decoded.SocketMode);
        Assert.Equal(original.RemoteHost, decoded.RemoteHost);
        Assert.Equal(original.RemotePort, decoded.RemotePort);
        Assert.Equal(original.LocalPort, decoded.LocalPort);
        Assert.Equal(original.ReconnectDelaySec, decoded.ReconnectDelaySec);
        Assert.Equal(original.HeartbeatIntervalSec, decoded.HeartbeatIntervalSec);
        Assert.Equal(original.HeartbeatPayload, decoded.HeartbeatPayload);
    }

    [Fact]
    public void DtuRoutingConfig_Roundtrip_ShouldPreserveAllFields()
    {
        // Arrange
        var original = new DtuConfigDto
        {
            Enabled = true,
            PortIndex = 2,
            SerialMode = DtuSerialMode.ModbusMaster,
            UplinkMode = DtuUplinkMode.Http,
            FrameTimeoutDelayMs = 35,
            MaxPacketSize = 1024
        };

        var registers = new ushort[ModbusRegisterMap.DtuRoutingConfigRegisters];

        // Act
        DtuConfigCodec.EncodeRouting(original, registers);
        var decoded = new DtuConfigDto();
        DtuConfigCodec.DecodeRouting(registers, decoded);

        // Assert
        Assert.Equal(original.Enabled, decoded.Enabled);
        Assert.Equal(original.PortIndex, decoded.PortIndex);
        Assert.Equal(original.SerialMode, decoded.SerialMode);
        Assert.Equal(original.UplinkMode, decoded.UplinkMode);
        Assert.Equal(original.FrameTimeoutDelayMs, decoded.FrameTimeoutDelayMs);
        Assert.Equal(original.MaxPacketSize, decoded.MaxPacketSize);
    }
}
