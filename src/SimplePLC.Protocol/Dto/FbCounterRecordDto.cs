using SimplePLC.Protocol.Constants;
using SimplePLC.Protocol.Enums;

namespace SimplePLC.Protocol.Dto;

/// <summary>
/// DTO biểu diễn 8 thanh ghi (16 bytes) của 1 khối Counter phần cứng theo Wire Profile V2 (Spec Mục 9.3).
/// Địa chỉ: 0x0B40 + (counterIndex * 8).
/// </summary>
public sealed class FbCounterRecordDto
{
    /// <summary>
    /// Offset +0: Status bitmask tức thời.
    /// </summary>
    public ushort StatusBits { get; set; }

    /// <summary>
    /// Offset +1: Chế độ hoạt động (CTU, CTD, CTUD, HSC).
    /// </summary>
    public SPLC_CounterMode Mode { get; set; } = SPLC_CounterMode.DISABLED;

    /// <summary>
    /// Offset +2..3: Preset Value (Ngưỡng đếm), 32-bit Big-Endian.
    /// </summary>
    public int PresetValue { get; set; }

    /// <summary>
    /// Offset +4..5: Current Value (Giá trị đếm hiện tại trên RAM), 32-bit Big-Endian.
    /// </summary>
    public int CurrentValue { get; set; }

    /// <summary>
    /// Offset +6: Chỉ số Tag VREG_RETAIN liên kết (0xFFFF = không lưu Flash).
    /// </summary>
    public ushort RetainTagIndex { get; set; } = ModbusRegisterMap.FbCounterRetainNone;

    // ==========================================
    // CÁC THUỘC TÍNH TIỆN ÍCH BIT TRẠNG THÁI (STATUS BITS)
    // ==========================================

    /// <summary>
    /// Bit 0: Tín hiệu đếm tiến (Count Up trigger).
    /// </summary>
    public bool Cu
    {
        get => (StatusBits & ModbusRegisterMap.FbCounterStatusBitCu) != 0;
        set => StatusBits = value
            ? (ushort)(StatusBits | ModbusRegisterMap.FbCounterStatusBitCu)
            : (ushort)(StatusBits & ~ModbusRegisterMap.FbCounterStatusBitCu);
    }

    /// <summary>
    /// Bit 1: Tín hiệu đếm lùi (Count Down trigger).
    /// </summary>
    public bool Cd
    {
        get => (StatusBits & ModbusRegisterMap.FbCounterStatusBitCd) != 0;
        set => StatusBits = value
            ? (ushort)(StatusBits | ModbusRegisterMap.FbCounterStatusBitCd)
            : (ushort)(StatusBits & ~ModbusRegisterMap.FbCounterStatusBitCd);
    }

    /// <summary>
    /// Bit 2: Tín hiệu Reset bộ đếm.
    /// </summary>
    public bool Reset
    {
        get => (StatusBits & ModbusRegisterMap.FbCounterStatusBitReset) != 0;
        set => StatusBits = value
            ? (ushort)(StatusBits | ModbusRegisterMap.FbCounterStatusBitReset)
            : (ushort)(StatusBits & ~ModbusRegisterMap.FbCounterStatusBitReset);
    }

    /// <summary>
    /// Bit 3: Ngõ ra Q (Counter done).
    /// </summary>
    public bool Q
    {
        get => (StatusBits & ModbusRegisterMap.FbCounterStatusBitQ) != 0;
        set => StatusBits = value
            ? (ushort)(StatusBits | ModbusRegisterMap.FbCounterStatusBitQ)
            : (ushort)(StatusBits & ~ModbusRegisterMap.FbCounterStatusBitQ);
    }
}
