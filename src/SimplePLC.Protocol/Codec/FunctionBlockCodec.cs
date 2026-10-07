using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Dto;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Codec;

/// <summary>
/// Codec đóng gói và giải mã các khối Function Block (Timer & Counter) theo Wire Profile V2 (Spec Mục 9).
/// Mỗi khối chiếm đúng 8 thanh ghi (16 bytes), đảm bảo căn gióng lũy thừa 2 và zero heap allocation khi dùng Span.
/// </summary>
public static class FunctionBlockCodec
{
    // ==========================================
    // 1. TIMER BLOCK CODEC (8 REGISTERS / 16 BYTES)
    // ==========================================

    /// <summary>
    /// Mã hóa một FbTimerRecordDto thành 8 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void EncodeTimer(FbTimerRecordDto timer, Span<ushort> destination)
    {
        if (destination.Length < ModbusRegisterMap.FbRegistersPerBlock)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.FbRegistersPerBlock} registers.", nameof(destination));

        // +0: STATUS_BITS (uint16)
        destination[0] = timer.StatusBits;

        // +1: MODE (uint16)
        destination[1] = (ushort)timer.Mode;

        // +2..+3: PT (uint32, High Word -> Low Word)
        RegisterCodec.EncodeUInt32(timer.PresetMs, destination.Slice(2, 2));

        // +4..+5: ET (uint32, High Word -> Low Word)
        RegisterCodec.EncodeUInt32(timer.ElapsedMs, destination.Slice(4, 2));

        // +6..+7: RESERVED (Sender luôn ghi 0)
        destination[6] = 0;
        destination[7] = 0;
    }

    /// <summary>
    /// Giải mã 8 thanh ghi Modbus thành FbTimerRecordDto.
    /// </summary>
    public static FbTimerRecordDto DecodeTimer(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.FbRegistersPerBlock)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.FbRegistersPerBlock} registers.", nameof(source));

        return new FbTimerRecordDto
        {
            StatusBits = source[0],
            Mode = (SPLC_TimerMode)source[1],
            PresetMs = RegisterCodec.DecodeUInt32(source.Slice(2, 2)),
            ElapsedMs = RegisterCodec.DecodeUInt32(source.Slice(4, 2))
        };
    }

    // ==========================================
    // 2. COUNTER BLOCK CODEC (8 REGISTERS / 16 BYTES)
    // ==========================================

    /// <summary>
    /// Mã hóa một FbCounterRecordDto thành 8 thanh ghi Modbus vào Span đích.
    /// </summary>
    public static void EncodeCounter(FbCounterRecordDto counter, Span<ushort> destination)
    {
        if (destination.Length < ModbusRegisterMap.FbRegistersPerBlock)
            throw new ArgumentException($"Destination span must have at least {ModbusRegisterMap.FbRegistersPerBlock} registers.", nameof(destination));

        // +0: STATUS_BITS (uint16)
        destination[0] = counter.StatusBits;

        // +1: MODE (uint16)
        destination[1] = (ushort)counter.Mode;

        // +2..+3: PV (int32, High Word -> Low Word)
        RegisterCodec.EncodeInt32(counter.PresetValue, destination.Slice(2, 2));

        // +4..+5: CV (int32, High Word -> Low Word)
        RegisterCodec.EncodeInt32(counter.CurrentValue, destination.Slice(4, 2));

        // +6: RETAIN_TAG_INDEX (uint16)
        destination[6] = counter.RetainTagIndex;

        // +7: RESERVED (Sender luôn ghi 0)
        destination[7] = 0;
    }

    /// <summary>
    /// Giải mã 8 thanh ghi Modbus thành FbCounterRecordDto.
    /// </summary>
    public static FbCounterRecordDto DecodeCounter(ReadOnlySpan<ushort> source)
    {
        if (source.Length < ModbusRegisterMap.FbRegistersPerBlock)
            throw new ArgumentException($"Source span must have at least {ModbusRegisterMap.FbRegistersPerBlock} registers.", nameof(source));

        return new FbCounterRecordDto
        {
            StatusBits = source[0],
            Mode = (SPLC_CounterMode)source[1],
            PresetValue = RegisterCodec.DecodeInt32(source.Slice(2, 2)),
            CurrentValue = RegisterCodec.DecodeInt32(source.Slice(4, 2)),
            RetainTagIndex = source[6]
        };
    }
}
