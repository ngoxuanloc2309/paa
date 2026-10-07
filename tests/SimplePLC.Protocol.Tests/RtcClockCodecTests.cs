using SimplePLC.Protocol.Codec;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using Xunit;

namespace SimplePLC.Protocol.Tests;

public class RtcClockCodecTests
{
    [Fact]
    public void EncodeAndDecode_RoundtripsAccurately()
    {
        // 2026-09-30 12:00:00 UTC
        var utcDate = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        uint epochSeconds = (uint)utcDate.ToUnixTimeSeconds(); // 1790769600

        var expected = new RtcClockDto
        {
            EpochUtcSeconds = epochSeconds,
            TimezoneOffsetMinutes = 420, // UTC+7 (Vietnam)
            IsSynced = true,
            HasHardwareRtc = true,
            IsBatteryLow = false
        };

        Span<ushort> buffer = stackalloc ushort[ModbusRegisterMap.RtcClockLength];
        RegisterCodec.EncodeRtcClock(expected, buffer);

        // Verify Big-Endian packing: high word and low word
        ushort expectedHigh = (ushort)(epochSeconds >> 16);
        ushort expectedLow = (ushort)(epochSeconds & 0xFFFF);
        Assert.Equal(expectedHigh, buffer[0]);
        Assert.Equal(expectedLow, buffer[1]);
        Assert.Equal((ushort)420, buffer[2]);
        Assert.Equal((ushort)(ModbusRegisterMap.RtcFlagSynced | ModbusRegisterMap.RtcFlagHwPresent), buffer[3]);

        var decoded = RegisterCodec.DecodeRtcClock(buffer);

        Assert.Equal(expected.EpochUtcSeconds, decoded.EpochUtcSeconds);
        Assert.Equal(expected.TimezoneOffsetMinutes, decoded.TimezoneOffsetMinutes);
        Assert.True(decoded.IsSynced);
        Assert.True(decoded.HasHardwareRtc);
        Assert.False(decoded.IsBatteryLow);

        // Verify local time calculation: 12:00 UTC + 7h = 19:00 local
        Assert.Equal(19, decoded.LocalDateTime.Hour);
        Assert.Equal(0, decoded.LocalDateTime.Minute);
        Assert.Equal(1900, decoded.LocalHhmm);
    }

    [Fact]
    public void Encode_WithNegativeTimezoneOffset_RoundtripsCorrectly()
    {
        // 2026-09-30 12:00:00 UTC
        var utcDate = new DateTimeOffset(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);
        uint epochSeconds = (uint)utcDate.ToUnixTimeSeconds();

        var expected = new RtcClockDto
        {
            EpochUtcSeconds = epochSeconds,
            TimezoneOffsetMinutes = -300, // UTC-5 (US EST)
            IsSynced = true,
            HasHardwareRtc = false,
            IsBatteryLow = true
        };

        Span<ushort> buffer = stackalloc ushort[ModbusRegisterMap.RtcClockLength];
        RegisterCodec.EncodeRtcClock(expected, buffer);

        var decoded = RegisterCodec.DecodeRtcClock(buffer);

        Assert.Equal(expected.EpochUtcSeconds, decoded.EpochUtcSeconds);
        Assert.Equal(-300, decoded.TimezoneOffsetMinutes);
        Assert.True(decoded.IsSynced);
        Assert.False(decoded.HasHardwareRtc);
        Assert.True(decoded.IsBatteryLow);

        // 12:00 UTC - 5h = 07:00 local
        Assert.Equal(7, decoded.LocalDateTime.Hour);
        Assert.Equal(0, decoded.LocalDateTime.Minute);
        Assert.Equal(700, decoded.LocalHhmm);
    }
}
