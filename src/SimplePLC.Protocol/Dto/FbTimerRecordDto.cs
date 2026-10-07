using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO biểu diễn 8 thanh ghi (16 bytes) của 1 khối Timer phần cứng theo Wire Profile V2 (Spec Mục 9.2).
/// Địa chỉ: 0x0B00 + (timerIndex * 8).
/// </summary>
public sealed class FbTimerRecordDto
{
    /// <summary>
    /// Offset +0: Status bitmask tức thời.
    /// </summary>
    public ushort StatusBits { get; set; }

    /// <summary>
    /// Offset +1: Chế độ hoạt động (TON, TOF, TP).
    /// </summary>
    public SPLC_TimerMode Mode { get; set; } = SPLC_TimerMode.DISABLED;

    /// <summary>
    /// Offset +2..3: Preset Time (mili-giây), 32-bit Big-Endian.
    /// </summary>
    public uint PresetMs { get; set; }

    /// <summary>
    /// Offset +4..5: Elapsed Time (mili-giây), 32-bit Big-Endian (Thời gian thực).
    /// </summary>
    public uint ElapsedMs { get; set; }

    // ==========================================
    // CÁC THUỘC TÍNH TIỆN ÍCH BIT TRẠNG THÁI (STATUS BITS)
    // ==========================================

    /// <summary>
    /// Bit 0: Tín hiệu kích hoạt (Input trigger).
    /// </summary>
    public bool In
    {
        get => (StatusBits & ModbusRegisterMap.FbTimerStatusBitIn) != 0;
        set => StatusBits = value
            ? (ushort)(StatusBits | ModbusRegisterMap.FbTimerStatusBitIn)
            : (ushort)(StatusBits & ~ModbusRegisterMap.FbTimerStatusBitIn);
    }

    /// <summary>
    /// Bit 1: Ngõ ra Q (Timer done / tripped).
    /// </summary>
    public bool Q
    {
        get => (StatusBits & ModbusRegisterMap.FbTimerStatusBitQ) != 0;
        set => StatusBits = value
            ? (ushort)(StatusBits | ModbusRegisterMap.FbTimerStatusBitQ)
            : (ushort)(StatusBits & ~ModbusRegisterMap.FbTimerStatusBitQ);
    }

    /// <summary>
    /// Bit 2: Tín hiệu Reset ngắt cưỡng bức.
    /// </summary>
    public bool Reset
    {
        get => (StatusBits & ModbusRegisterMap.FbTimerStatusBitReset) != 0;
        set => StatusBits = value
            ? (ushort)(StatusBits | ModbusRegisterMap.FbTimerStatusBitReset)
            : (ushort)(StatusBits & ~ModbusRegisterMap.FbTimerStatusBitReset);
    }

    /// <summary>
    /// Bit 3: Timer đang tích cực tính thời gian (In=1, Elapsed < Preset).
    /// </summary>
    public bool Running
    {
        get => (StatusBits & ModbusRegisterMap.FbTimerStatusBitRunning) != 0;
        set => StatusBits = value
            ? (ushort)(StatusBits | ModbusRegisterMap.FbTimerStatusBitRunning)
            : (ushort)(StatusBits & ~ModbusRegisterMap.FbTimerStatusBitRunning);
    }
}
