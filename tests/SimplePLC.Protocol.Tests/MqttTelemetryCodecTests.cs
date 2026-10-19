using System;
using System.Collections.Generic;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class MqttTelemetryCodecTests
{
    [Fact]
    public void Encode_Decode_JsonMode_Roundtrip_PreservesAllFields()
    {
        var config = new MqttTelemetryConfigDto
        {
            Topic = "factory/line1/temp",
            PublishMode = "Periodic",
            Qos = 1,
            Retain = true,
            PayloadMode = "JSON",
            IntervalMs = 2500,
            Deadband = 0.75,
            EnTagIndex = 12,
            DataTagIndex = 4,
            Fields = new List<MqttPayloadFieldDto>
            {
                new() { Key = "temp", TagIndex = 4 },
                new() { Key = "press", TagIndex = 5 }
            }
        };

        Span<ushort> buffer = stackalloc ushort[ModbusRegisterMap.MqttRegistersPerBlock];
        MqttTelemetryCodec.EncodeBlock(config, buffer);

        // Kiểm tra Offset +0 (Control Flags):
        // enabled=1 (bit0)
        // publish_mode=2 (Periodic, bit 1..2: 0x0004)
        // qos=1 (bit 3..4: 0x0008)
        // retain=1 (bit 5: 0x0020)
        // payload_mode=0 (JSON, bit 6..7: 0x0000)
        // Tổng: 1 | 4 | 8 | 32 = 45 (0x002D)
        Assert.Equal(0x002D, buffer[0]);

        // Offset +1: en_tag_index
        Assert.Equal(12, buffer[1]);

        // Offset +2..+3: interval_ms = 2500
        Assert.Equal(0, buffer[2]);
        Assert.Equal(2500, buffer[3]);

        // Offset +6: tag_count = 2
        Assert.Equal(2, buffer[6]);
        Assert.Equal(4, buffer[7]);
        Assert.Equal(5, buffer[8]);

        // Decode và kiểm tra roundtrip
        var decoded = MqttTelemetryCodec.DecodeBlock(buffer);
        Assert.Equal("factory/line1/temp", decoded.Topic);
        Assert.Equal("Periodic", decoded.PublishMode);
        Assert.Equal(1, decoded.Qos);
        Assert.True(decoded.Retain);
        Assert.Equal("JSON", decoded.PayloadMode);
        Assert.Equal(2500u, decoded.IntervalMs);
        Assert.Equal(0.75, decoded.Deadband, 2);
        Assert.Equal((ushort)12, decoded.EnTagIndex);
        Assert.Equal(2, decoded.Fields.Count);
        Assert.Equal((ushort)4, decoded.Fields[0].TagIndex);
        Assert.Equal((ushort)5, decoded.Fields[1].TagIndex);
    }

    [Fact]
    public void Encode_Decode_CustomMode_Roundtrip_PreservesTemplate()
    {
        string customTpl = "{\"v\":{val},\"ts\":{ts},\"dev\":\"STX\"}";

        var config = new MqttTelemetryConfigDto
        {
            Topic = "sensor/custom",
            PublishMode = "COV",
            Qos = 2,
            Retain = false,
            PayloadMode = "Custom",
            Deadband = 1.25,
            CustomTemplate = customTpl,
            Fields = new List<MqttPayloadFieldDto>
            {
                new() { Key = "val", TagIndex = 3 }
            }
        };

        Span<ushort> buffer = stackalloc ushort[ModbusRegisterMap.MqttRegistersPerBlock];
        MqttTelemetryCodec.EncodeBlock(config, buffer);

        // Control flags:
        // enabled=1 (bit0)
        // publish_mode=1 (COV, bit 1..2: 0x0002)
        // qos=2 (bit 3..4: 0x0010)
        // retain=0 (bit 5: 0)
        // payload_mode=2 (Custom, bit 6..7: 0x0080)
        // Tổng: 1 | 2 | 16 | 128 = 147 (0x0093)
        Assert.Equal(0x0093, buffer[0]);

        var decoded = MqttTelemetryCodec.DecodeBlock(buffer);
        Assert.Equal("sensor/custom", decoded.Topic);
        Assert.Equal("COV", decoded.PublishMode);
        Assert.Equal(2, decoded.Qos);
        Assert.False(decoded.Retain);
        Assert.Equal("Custom", decoded.PayloadMode);
        Assert.Equal(1.25, decoded.Deadband, 2);
        Assert.Equal(customTpl, decoded.CustomTemplate);
    }

    [Fact]
    public void PackedAscii_MatchesExactHexWordsAccordingToSpecification()
    {
        // Kiểm tra chuỗi ví dụ trong Spec Mục 4: "factory/temp"
        Span<ushort> regs = stackalloc ushort[16];
        MqttTelemetryCodec.EncodePackedAscii("factory/temp", regs, 32);

        // 'f' (0x66), 'a' (0x61) -> 0x6661
        Assert.Equal(0x6661, regs[0]);
        // 'c' (0x63), 't' (0x74) -> 0x6374
        Assert.Equal(0x6374, regs[1]);
        // 'o' (0x6F), 'r' (0x72) -> 0x6F72
        Assert.Equal(0x6F72, regs[2]);
        // 'y' (0x79), '/' (0x2F) -> 0x792F
        Assert.Equal(0x792F, regs[3]);
        // 't' (0x74), 'e' (0x65) -> 0x7465
        Assert.Equal(0x7465, regs[4]);
        // 'm' (0x6D), 'p' (0x70) -> 0x6D70
        Assert.Equal(0x6D70, regs[5]);
        // null terminator
        Assert.Equal(0x0000, regs[6]);

        // Unpack roundtrip
        string unpacked = MqttTelemetryCodec.DecodePackedAscii(regs);
        Assert.Equal("factory/temp", unpacked);
    }

    [Fact]
    public void Encode_Offset15_AlwaysWritesReservedZeroAccordingToSPLC_SPEC_IEC_002()
    {
        var config = new MqttTelemetryConfigDto
        {
            Topic = "plant/furnace",
            PublishMode = "COV",
            Fields = new List<MqttPayloadFieldDto>
            {
                new() { Key = "val0", TagIndex = 10 },
                new() { Key = "val1", TagIndex = 11 },
                new() { Key = "val2", TagIndex = 12 },
                new() { Key = "val3", TagIndex = 13 }
            }
        };

        Span<ushort> buffer = stackalloc ushort[ModbusRegisterMap.MqttRegistersPerBlock];
        MqttTelemetryCodec.EncodeBlock(config, buffer);

        // Expected header at +15: Always 0x0000 (Reserved) per SPLC-SPEC-IEC-002
        Assert.Equal(0x0000, buffer[15]);

        var decoded = MqttTelemetryCodec.DecodeBlock(buffer);
        Assert.Equal(4, decoded.Fields.Count);
        Assert.Equal(10, decoded.Fields[0].TagIndex);
        Assert.Equal(11, decoded.Fields[1].TagIndex);
        Assert.Equal(12, decoded.Fields[2].TagIndex);
        Assert.Equal(13, decoded.Fields[3].TagIndex);
    }
}
