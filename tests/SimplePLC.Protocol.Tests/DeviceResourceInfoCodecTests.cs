using System;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Protocol.Tests;

/// <summary>
/// Unit tests cho DeviceResourceInfo codec theo Contract V1.9 (0x0020..0x0029, 10 registers = 20 bytes).
/// </summary>
public class DeviceResourceInfoCodecTests
{
    [Fact]
    public void EncodeAndDecode_Roundtrip_PreservesAll10Registers()
    {
        // Arrange: Cấu hình chuẩn Remote IO V1 (124 tags)
        var original = new DeviceResourceInfoDto
        {
            WireProfile = 1,
            MaxRules = 100,
            RuntimeTagCount = 124,
            DigitalInputCount = 8,
            DigitalOutputCount = 8,
            AnalogInputCount = 4,
            VirtualFlagCount = 32,
            VirtualRegisterCount = 32,
            RetentiveRegisterCount = 32,
            CounterCount = 8
        };

        Span<ushort> buffer = stackalloc ushort[ModbusRegisterMap.DeviceResourceInfoLength];

        // Act: Encode
        RegisterCodec.EncodeDeviceResourceInfo(original, buffer);

        // Assert: 10 registers exact mapping
        Assert.Equal(1, buffer[0]);   // wire_profile
        Assert.Equal(100, buffer[1]); // max_rules
        Assert.Equal(124, buffer[2]); // runtime_tag_count
        Assert.Equal(8, buffer[3]);   // di_count
        Assert.Equal(8, buffer[4]);   // do_count
        Assert.Equal(4, buffer[5]);   // ai_count
        Assert.Equal(32, buffer[6]);  // vflag_count
        Assert.Equal(32, buffer[7]);  // vreg_count
        Assert.Equal(32, buffer[8]);  // vreg_retain_count
        Assert.Equal(8, buffer[9]);   // counter_count

        // Act: Decode
        var decoded = RegisterCodec.DecodeDeviceResourceInfo(buffer);

        // Assert: Roundtrip fidelity
        Assert.Equal(original.WireProfile, decoded.WireProfile);
        Assert.Equal(original.MaxRules, decoded.MaxRules);
        Assert.Equal(original.RuntimeTagCount, decoded.RuntimeTagCount);
        Assert.Equal(original.DigitalInputCount, decoded.DigitalInputCount);
        Assert.Equal(original.DigitalOutputCount, decoded.DigitalOutputCount);
        Assert.Equal(original.AnalogInputCount, decoded.AnalogInputCount);
        Assert.Equal(original.VirtualFlagCount, decoded.VirtualFlagCount);
        Assert.Equal(original.VirtualRegisterCount, decoded.VirtualRegisterCount);
        Assert.Equal(original.RetentiveRegisterCount, decoded.RetentiveRegisterCount);
        Assert.Equal(original.CounterCount, decoded.CounterCount);
        Assert.Equal(124, decoded.TotalDeclaredTags);
        Assert.True(decoded.IsValidDeclaredCount);
        Assert.True(decoded.MaxRules > 0);
        Assert.True(decoded.RetentiveRegisterCount > 0);
    }

    [Fact]
    public void SmallProductProfile_WireCounts_EvaluatedCorrectly()
    {
        // Small IO: Không có rule engine (max_rules = 0), không có retain (retain_count = 0)
        var small = new DeviceResourceInfoDto
        {
            WireProfile = 1,
            MaxRules = 0,
            RuntimeTagCount = 18,
            DigitalInputCount = 8,
            DigitalOutputCount = 8,
            AnalogInputCount = 2,
            VirtualFlagCount = 0,
            VirtualRegisterCount = 0,
            RetentiveRegisterCount = 0,
            CounterCount = 0
        };

        Assert.Equal(18, small.TotalDeclaredTags);
        Assert.True(small.IsValidDeclaredCount);
        Assert.Equal(0, small.MaxRules);
        Assert.Equal(0, small.RetentiveRegisterCount);
    }

    [Fact]
    public void Encode_UndersizedSpan_ThrowsArgumentException()
    {
        var info = DeviceResourceInfoDto.CreateRemoteIo8Di8Do4Ai();
        var shortBuffer = new ushort[9];

        Assert.Throws<ArgumentException>(() => RegisterCodec.EncodeDeviceResourceInfo(info, shortBuffer));
    }

    [Fact]
    public void Decode_UndersizedSpan_ThrowsArgumentException()
    {
        var shortBuffer = new ushort[9];

        Assert.Throws<ArgumentException>(() => RegisterCodec.DecodeDeviceResourceInfo(shortBuffer));
    }
}
