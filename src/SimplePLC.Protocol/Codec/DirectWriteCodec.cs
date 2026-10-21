using System;
using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Codec đóng gói và giải mã các khối DIRECT_WRITE / PULSE_OUT theo đặc tả SPLC-SPEC-DIRECT-WRITE-003.
/// Mỗi khối chiếm đúng 16 thanh ghi Modbus (32 bytes):
/// - Reg 0: mode (High Byte) | target_pin (Low Byte)
/// - Reg 1: channel_count (uint16)
/// - Reg 2: trigger_mode (High Byte) | flags (Low Byte)
/// - Reg 3: pulse_width_us (uint16)
/// - Reg 4..5: pwm_freq_hz (uint32 Big-Endian)
/// - Reg 6: duty_max (High Byte) | duty_min (Low Byte)
/// - Reg 7: channel_tag_index (uint16)
/// - Reg 8: param_tag_index (uint16)
/// - Reg 9..10: default_color_rgb (uint32 Big-Endian)
/// - Reg 11: deadband_x1000 (uint16, COV deadband = value / 1000.0f; 0xFFFF = not set → default 0.5)
/// - Reg 12..15: reserved (4 registers = 8 bytes)
/// </summary>
public static class DirectWriteCodec
{
    /// <summary>
    /// Mã hóa cấu hình DirectWriteConfigDto thành 16 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void EncodeBlock(DirectWriteConfigDto config, Span<ushort> destination)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (destination.Length < ModbusRegisterMap.DirectWriteRegistersPerBlock)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.DirectWriteRegistersPerBlock} registers.", nameof(destination));

        destination.Slice(0, ModbusRegisterMap.DirectWriteRegistersPerBlock).Fill(0);

        // Reg 0: (mode << 8) | target_pin
        byte targetPin = (byte)config.TargetPin;
        byte mode = (byte)config.Mode;
        destination[0] = (ushort)((mode << 8) | targetPin);

        // Reg 1: channel_count
        destination[1] = config.ChannelCount;

        // Reg 2: (trigger_mode << 8) | flags
        byte flags = 0x01; // Bit 0: is_active_high (default 1)
        if (config.IsExclusiveChannel) flags |= 0x02; // Bit 1: is_exclusive
        byte trigMode = (byte)config.TriggerMode;
        destination[2] = (ushort)((trigMode << 8) | flags);

        // Reg 3: pulse_width_us
        destination[3] = (ushort)Math.Clamp(config.PulseWidthUs, 0, 65535);

        // Reg 4..5: pwm_freq_hz (uint32 Big-Endian)
        RegisterCodec.EncodeUInt32(config.PwmFrequencyHz, destination.Slice(4, 2));

        // Reg 6: (duty_max << 8) | duty_min
        byte dutyMin = 0;
        byte dutyMax = 100;
        destination[6] = (ushort)((dutyMax << 8) | dutyMin);

        // Reg 7: channel_tag_index
        destination[7] = config.ChannelTagIndex ?? 0xFFFF;

        // Reg 8: param_tag_index
        destination[8] = config.ParamTagIndex ?? 0xFFFF;

        // Reg 9..10: default_color_rgb (uint32 Big-Endian)
        RegisterCodec.EncodeUInt32(config.DefaultColorRgb, destination.Slice(9, 2));

        // Reg 11: deadband_x1000 (COV mode: deadband = reg11 / 1000.0f; 0 = not set / default 0.5)
        if (config.TriggerMode == DirectWriteTriggerMode.OnChangeOfValue && config.Deadband >= 0f)
        {
            uint scaledDeadband = (uint)Math.Round(config.Deadband * 1000f);
            destination[11] = (ushort)Math.Clamp(scaledDeadband, 1u, 0xFFFEu);
        }
        else
        {
            destination[11] = 0; // 0 = default / N/A (preserves golden vector)
        }

        // Reg 12..15: reserved (already filled with 0)
    }

    /// <summary>
    /// Giải mã 16 thanh ghi Modbus thành DirectWriteConfigDto.
    /// </summary>
    public static DirectWriteConfigDto DecodeBlock(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.DirectWriteRegistersPerBlock)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.DirectWriteRegistersPerBlock} registers.", nameof(source));

        ushort reg0 = source[0];
        byte targetPin = (byte)(reg0 & 0xFF);
        byte mode = (byte)((reg0 >> 8) & 0xFF);

        ushort channelCount = source[1];

        ushort reg2 = source[2];
        byte flags = (byte)(reg2 & 0xFF);
        byte trigMode = (byte)((reg2 >> 8) & 0xFF);

        ushort pulseWidthUs = source[3];
        uint pwmFreqHz = RegisterCodec.DecodeUInt32(source.Slice(4, 2));

        ushort channelTag = source[7];
        ushort paramTag = source[8];
        uint defaultColor = RegisterCodec.DecodeUInt32(source.Slice(9, 2));

        // Reg 11: deadband_x1000 (0 or 0xFFFF = not set → default 0.5f)
        ushort deadbandRaw = source[11];
        float deadband = (deadbandRaw == 0 || deadbandRaw == 0xFFFF) ? 0.5f : deadbandRaw / 1000.0f;

        return new DirectWriteConfigDto
        {
            TargetPin = Enum.IsDefined(typeof(DirectWriteTargetPin), targetPin) ? (DirectWriteTargetPin)targetPin : DirectWriteTargetPin.Do0,
            Mode = Enum.IsDefined(typeof(DirectWriteMode), mode) ? (DirectWriteMode)mode : DirectWriteMode.DigitalOutput,
            ChannelCount = channelCount == 0 ? (ushort)18 : channelCount,
            IsExclusiveChannel = (flags & 0x02) != 0,
            TriggerMode = Enum.IsDefined(typeof(DirectWriteTriggerMode), trigMode) ? (DirectWriteTriggerMode)trigMode : DirectWriteTriggerMode.OnTriggerRisingEdge,
            PulseWidthUs = pulseWidthUs == 0 ? 50u : pulseWidthUs,
            PwmFrequencyHz = pwmFreqHz == 0 ? 1000u : pwmFreqHz,
            ChannelTagIndex = channelTag == 0xFFFF ? null : channelTag,
            ParamTagIndex = paramTag == 0xFFFF ? null : paramTag,
            DefaultColorRgb = defaultColor == 0 ? 0x00FF00 : defaultColor,
            Deadband = deadband
        };
    }
}
