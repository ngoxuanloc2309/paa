using System;
using System.Collections.Generic;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class MqttSubscriptionCodecTests
{
    [Fact]
    public void Encode_Decode_Roundtrip_PreservesAllFields()
    {
        var config = new MqttSubscriptionConfigDto
        {
            Topic = "plant/cmd/furnace_01",
            Qos = 1,
            QueueMode = MqttQueueMode.Fifo,
            IgnoreRetainedOnBoot = true,
            TimeoutMs = 5000,
            FailSafeAction = MqttFailSafeAction.ResetToDefault,
            Pins = new List<MqttPinBindingDto>
            {
                new()
                {
                    Id = "setpoint",
                    Label = "Setpoint",
                    JsonPath = "sp",
                    DataType = MqttDataType.Float,
                    TargetTagIndex = 25,
                    DefaultValue = 120.5
                },
                new()
                {
                    Id = "run_cmd",
                    Label = "Run Command",
                    JsonPath = "run",
                    DataType = MqttDataType.Bool,
                    TargetTagIndex = 8,
                    DefaultValue = 0
                }
            }
        };

        Span<ushort> buffer = stackalloc ushort[ModbusRegisterMap.MqttSubscribeRegistersPerBlock];
        MqttSubscriptionCodec.EncodeBlock(config, buffer);

        // Kiểm tra Offset +0 (Control Flags):
        // enabled=1 (bit 0 = 0x0001)
        // qos=1 (bit 1..2: 1 << 1 = 0x0002)
        // queue_fifo=1 (bit 3 = 0x0008)
        // ignore_retain=1 (bit 4 = 0x0010)
        // failsafe=2 (ResetToDefault, bit 5..6: 2 << 5 = 0x0040)
        // Tổng: 1 | 2 | 8 | 16 | 64 = 91 (0x005B)
        Assert.Equal(0x005B, buffer[0]);

        // Offset +2..+3: TimeoutMs = 5000
        Assert.Equal(0, buffer[2]);
        Assert.Equal(5000, buffer[3]);

        // Offset +4: Pin Count = 2
        Assert.Equal(2, buffer[4]);

        // Decode và kiểm tra roundtrip
        var decoded = MqttSubscriptionCodec.DecodeBlock(buffer);
        Assert.Equal("plant/cmd/furnace_01", decoded.Topic);
        Assert.Equal(1, decoded.Qos);
        Assert.Equal(MqttQueueMode.Fifo, decoded.QueueMode);
        Assert.True(decoded.IgnoreRetainedOnBoot);
        Assert.Equal(5000u, decoded.TimeoutMs);
        Assert.Equal(MqttFailSafeAction.ResetToDefault, decoded.FailSafeAction);

        Assert.Equal(2, decoded.Pins.Count);
        Assert.Equal("sp", decoded.Pins[0].JsonPath);
        Assert.Equal(MqttDataType.Float, decoded.Pins[0].DataType);
        Assert.Equal((ushort)25, decoded.Pins[0].TargetTagIndex);
        Assert.Equal(120.5, decoded.Pins[0].DefaultValue, precision: 2);

        Assert.Equal("run", decoded.Pins[1].JsonPath);
        Assert.Equal(MqttDataType.Bool, decoded.Pins[1].DataType);
        Assert.Equal((ushort)8, decoded.Pins[1].TargetTagIndex);
        Assert.Equal(0, decoded.Pins[1].DefaultValue);
    }

    [Fact]
    public void Encode_ThrowsIfSpanTooSmall()
    {
        var config = new MqttSubscriptionConfigDto();
        var tooSmall = new ushort[30];

        Assert.Throws<ArgumentException>(() => MqttSubscriptionCodec.EncodeBlock(config, tooSmall));
    }

    [Fact]
    public void Encode_Decode_ResetToZero_Roundtrip()
    {
        var config = new MqttSubscriptionConfigDto
        {
            Topic = "test/sub",
            FailSafeAction = MqttFailSafeAction.ResetToZero
        };

        var buffer = new ushort[ModbusRegisterMap.MqttSubscribeRegistersPerBlock];
        MqttSubscriptionCodec.EncodeBlock(config, buffer);

        // FailSafe bit 5..6 = 1 << 5 = 0x0020
        int failSafeCode = (buffer[0] & ModbusRegisterMap.MqttSubControlFailSafeMask) >> ModbusRegisterMap.MqttSubControlFailSafeShift;
        Assert.Equal(1, failSafeCode);

        var decoded = MqttSubscriptionCodec.DecodeBlock(buffer);
        Assert.Equal(MqttFailSafeAction.ResetToZero, decoded.FailSafeAction);
    }
}
