using System;
using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class DirectWriteCodecTests
{
    [Fact]
    public void EncodeBlock_GoldenVector_MatchesExactRegisters()
    {
        // Sort-to-Light 18 Bins on DO3
        var config = new DirectWriteConfigDto
        {
            TargetPin = DirectWriteTargetPin.Do3,
            Mode = DirectWriteMode.AddressableBus,
            ChannelCount = 18,
            IsExclusiveChannel = true,
            TriggerMode = DirectWriteTriggerMode.OnTriggerRisingEdge,
            PulseWidthUs = 50,
            PwmFrequencyHz = 0,
            ChannelTagIndex = 0,
            ParamTagIndex = 1,
            DefaultColorRgb = 0x0000FF00
        };

        var destination = new ushort[ModbusRegisterMap.DirectWriteRegistersPerBlock];
        DirectWriteCodec.EncodeBlock(config, destination);

        // Reg 0: mode=3 (AddressableBus) << 8 | target_pin=3 (Do3) => 0x0303
        Assert.Equal(0x0303, destination[0]);
        // Reg 1: channel_count = 18 => 0x0012
        Assert.Equal(0x0012, destination[1]);
        // Reg 2: trigger_mode=0 (RisingEdge) << 8 | flags (0x01 | 0x02 = 0x03) => 0x0003
        Assert.Equal(0x0003, destination[2]);
        // Reg 3: pulse_width_us = 50 => 0x0032
        Assert.Equal(0x0032, destination[3]);
        // Reg 4..5: pwm_freq_hz = 0
        Assert.Equal(0x0000, destination[4]);
        Assert.Equal(0x0000, destination[5]);
        // Reg 6: duty_max=100 (0x64) << 8 | duty_min=0 => 0x6400
        Assert.Equal(0x6400, destination[6]);
        // Reg 7: channel_tag = 0
        Assert.Equal(0x0000, destination[7]);
        // Reg 8: param_tag = 1
        Assert.Equal(0x0001, destination[8]);
        // Reg 9..10: default_color_rgb = 0x0000FF00
        Assert.Equal(0x0000, destination[9]);
        Assert.Equal(0xFF00, destination[10]);
        // Reg 11..15: reserved = 0
        for (int i = 11; i < 16; i++)
        {
            Assert.Equal(0x0000, destination[i]);
        }
    }

    [Fact]
    public void EncodeDecode_RoundTrip_PreservesAllFields()
    {
        var original = new DirectWriteConfigDto
        {
            TargetPin = DirectWriteTargetPin.Do5,
            Mode = DirectWriteMode.PwmDuty,
            ChannelCount = 1,
            IsExclusiveChannel = false,
            TriggerMode = DirectWriteTriggerMode.Continuous,
            PulseWidthUs = 100,
            PwmFrequencyHz = 2500,
            ChannelTagIndex = 12,
            ParamTagIndex = 15,
            DefaultColorRgb = 0x00FF0000
        };

        var buffer = new ushort[ModbusRegisterMap.DirectWriteRegistersPerBlock];
        DirectWriteCodec.EncodeBlock(original, buffer);
        var decoded = DirectWriteCodec.DecodeBlock(buffer);

        Assert.Equal(original.TargetPin, decoded.TargetPin);
        Assert.Equal(original.Mode, decoded.Mode);
        Assert.Equal(original.ChannelCount, decoded.ChannelCount);
        Assert.Equal(original.IsExclusiveChannel, decoded.IsExclusiveChannel);
        Assert.Equal(original.TriggerMode, decoded.TriggerMode);
        Assert.Equal(original.PulseWidthUs, decoded.PulseWidthUs);
        Assert.Equal(original.PwmFrequencyHz, decoded.PwmFrequencyHz);
        Assert.Equal(original.ChannelTagIndex, decoded.ChannelTagIndex);
        Assert.Equal(original.ParamTagIndex, decoded.ParamTagIndex);
        Assert.Equal(original.DefaultColorRgb, decoded.DefaultColorRgb);
    }

    [Fact]
    public void EncodeDecode_OnChangeOfValue_PreservesDeadband()
    {
        var original = new DirectWriteConfigDto
        {
            TargetPin = DirectWriteTargetPin.Do2,
            Mode = DirectWriteMode.PwmDuty,
            TriggerMode = DirectWriteTriggerMode.OnChangeOfValue,
            Deadband = 0.25f,
            PwmFrequencyHz = 10000
        };

        var buffer = new ushort[ModbusRegisterMap.DirectWriteRegistersPerBlock];
        DirectWriteCodec.EncodeBlock(original, buffer);

        // Reg 11 must contain 250 (0.25 * 1000)
        Assert.Equal((ushort)250, buffer[11]);

        var decoded = DirectWriteCodec.DecodeBlock(buffer);
        Assert.Equal(DirectWriteTriggerMode.OnChangeOfValue, decoded.TriggerMode);
        Assert.Equal(0.25f, decoded.Deadband, precision: 3);
    }
}
